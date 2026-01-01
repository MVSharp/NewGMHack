using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace NewGMHack.Stub.Services.Scanning
{
     public class SimdMemoryScanner : IMemoryScanner
    {
        #region Win32 API

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool ReadProcessMemory(
            IntPtr hProcess, IntPtr lpBaseAddress, [Out] byte[] lpBuffer,
            int dwSize, out IntPtr lpNumberOfBytesRead);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool VirtualQueryEx(
            IntPtr hProcess, IntPtr lpAddress,
            out MEMORY_BASIC_INFORMATION lpBuffer, uint dwLength);

        [StructLayout(LayoutKind.Sequential)]
        struct MEMORY_BASIC_INFORMATION
        {
            public IntPtr BaseAddress;
            public IntPtr AllocationBase;
            public uint AllocationProtect;
            public IntPtr RegionSize;
            public uint State;
            public uint Protect;
            public uint Type;
        }

        private const uint MEM_COMMIT = 0x1000;
        private const uint MEM_PRIVATE = 0x20000;
        private const uint MEM_IMAGE = 0x1000000;
        private const uint PAGE_READONLY = 0x02;
        private const uint PAGE_READWRITE = 0x04;
        private const uint PAGE_EXECUTE_READ = 0x20;
        private const uint PAGE_EXECUTE_READWRITE = 0x40;

        #endregion

        public async Task<List<long>> ScanAsync(Process process, byte[] pattern, string? mask = null, CancellationToken token = default)
        {
            if (process == null || process.HasExited) return new List<long>();
            if (pattern == null || pattern.Length == 0) return new List<long>();

            // Normalize mask
            if (string.IsNullOrEmpty(mask))
            {
                mask = new string('x', pattern.Length);
            }
            if (mask.Length != pattern.Length)
            {
                 throw new ArgumentException("Mask length must match pattern length.");
            }

            // Convert mask to bool array for faster access
            bool[] boolMask = mask.Select(c => c != '?').ToArray();

            // 1. Collect and Chunk Regions
            var regions = GetRegions(process);
            var jobs = CreateScanJobs(regions, pattern.Length);

            // 2. Parallel Scan
            var results = new ConcurrentBag<long>();
            byte firstByte = pattern[0];
            bool firstByteMasked = boolMask[0];

            // Limit concurrency slightly to avoid thread pool starvation or aggressive memory usage
            // The default is -1 (ProcessorCount), which is usually fine with small buffers.
            var parallelOptions = new ParallelOptions 
            { 
                CancellationToken = token,
                MaxDegreeOfParallelism = Environment.ProcessorCount 
            };

            await Task.Run(() =>
            {
                Parallel.ForEach(jobs, parallelOptions, (job) =>
                {
                    if (token.IsCancellationRequested) return;

                    // Rent a buffer large enough for the chunk + potential overlap
                    // job.Size is the SCAN SIZE (number of valid start positions)
                    // We need to read job.Size + pattern.Length - 1 bytes to check the last position
                    int readSize = job.Size + pattern.Length - 1;
                    
                    // Clamp readSize to not exceed region boundaries (though CheckScanJobs handles this logically)
                    // The job.Size is calculated such that BaseAddress + Size + PatternLen - 1 <= RegionEnd
                    
                    var bufferPool = ArrayPool<byte>.Shared;
                    byte[] buffer = bufferPool.Rent(readSize);

                    try
                    {
                        if (ReadProcessMemory(process.Handle, job.BaseAddress, buffer, readSize, out var bytesRead))
                        {
                            int actualSize = (int)bytesRead.ToInt64();
                            // We only scan up to 'actualSize' 
                            // but effectively we only care about matches starting within [0, job.Size)
                            // or [0, actualSize - pattern.Length]
                            
                            int scanLimit = actualSize; 
                            
                            if (scanLimit >= pattern.Length)
                            {
                                var matches = FindPatternSIMD(buffer.AsSpan(0, scanLimit), pattern, boolMask, firstByte, firstByteMasked);
                                foreach (var offset in matches)
                                {
                                    // Filter out matches that might belong to the next chunk's overlap 
                                    // if we didn't handle overlap strictly by start index.
                                    // Using job.Size as the limit of *start positions* ensures disjoint keys.
                                    if (offset < job.Size)
                                    {
                                        results.Add(job.BaseAddress.ToInt64() + offset);
                                    }
                                }
                            }
                        }
                    }
                    finally
                    {
                        bufferPool.Return(buffer);
                    }
                });
            }, token);

            return results.ToList();
        }

        private struct ScanJob
        {
            public IntPtr BaseAddress;
            public int Size; // The number of "start positions" to scan
        }
        
        private const int MAX_CHUNK_SIZE = 4 * 1024 * 1024; // 4MB chunks

        private List<ScanJob> CreateScanJobs(List<MEMORY_BASIC_INFORMATION> regions, int patternLength)
        {
            var jobs = new List<ScanJob>();
            
            foreach (var mbi in regions)
            {
                long regionSize = mbi.RegionSize.ToInt64();
                long currentOffset = 0;
                
                while (currentOffset < regionSize)
                {
                    long remaining = regionSize - currentOffset;
                    
                    // If remaining is smaller than pattern, we can't find anything
                    if (remaining < patternLength) break;

                    // Determine chunk size
                    // We want to scan 'chunkSize' distinct start positions.
                    // To do that, we need to read 'chunkSize + patternLength - 1' bytes.
                    
                    int scanSize = (int)Math.Min(remaining - (patternLength - 1), MAX_CHUNK_SIZE);
                    
                    if (scanSize <= 0) break;

                    jobs.Add(new ScanJob 
                    { 
                        BaseAddress = (IntPtr)(mbi.BaseAddress.ToInt64() + currentOffset),
                        Size = scanSize
                    });

                    currentOffset += scanSize;
                }
            }
            return jobs;
        }

        private List<MEMORY_BASIC_INFORMATION> GetRegions(Process process)
        {
            var regions = new List<MEMORY_BASIC_INFORMATION>();
            IntPtr address = IntPtr.Zero;
            IntPtr maxAddress = (IntPtr)0x7FFFFFFF;

            while (address.ToInt64() < maxAddress.ToInt64())
            {
                if (!VirtualQueryEx(process.Handle, address, out var mbi, (uint)Marshal.SizeOf<MEMORY_BASIC_INFORMATION>()))
                    break;

                bool isValidType = (mbi.Type == MEM_PRIVATE || mbi.Type == MEM_IMAGE);
                if (mbi.State == MEM_COMMIT && isValidType && IsReadable(mbi.Protect))
                {
                    long regionSize = mbi.RegionSize.ToInt64();
                    // We can be more permissive here since we chunk it later, 
                    // but still skip empty or absurdly huge (unlikely in 32-bit msg)
                    if (regionSize > 0)
                    {
                        regions.Add(mbi);
                    }
                }

                long nextAddress = mbi.BaseAddress.ToInt64() + mbi.RegionSize.ToInt64();
                if (address.ToInt64() >= nextAddress) break;
                address = (IntPtr)nextAddress;
            }
            return regions;
        }

        private static List<int> FindPatternSIMD(ReadOnlySpan<byte> data, byte[] pattern, bool[] mask, byte firstByte, bool firstByteMasked)
        {
            var results = new List<int>();
            int patternLength = pattern.Length;
            int dataLength = data.Length;
            int vectorSize = Vector<byte>.Count;

            if (dataLength < patternLength) return results;

            // Simple optimization: If first byte matches, we check the rest.
            // If first byte is a wildcard, we can't use SIMD to skip efficiently on it.
            // But usually first byte is known. If not, we scan linearly or pick a different anchor (complexity).
            // For now, assume first byte is known or fallback to linear.
            
            if (firstByteMasked)
            {
                Vector<byte> searchVector = new Vector<byte>(firstByte);
                int i = 0;
                int limit = dataLength - patternLength;
                int vectorLimit = limit - vectorSize + 1;

                while (i < vectorLimit)
                {
                    var chunk = new Vector<byte>(data.Slice(i, vectorSize));
                    var equals = Vector.Equals(chunk, searchVector);

                    if (equals != Vector<byte>.Zero)
                    {
                        for (int j = 0; j < vectorSize && i + j <= limit; j++)
                        {
                            if (data[i + j] == firstByte && MatchPattern(data.Slice(i + j), pattern, mask))
                            {
                                results.Add(i + j);
                            }
                        }
                    }
                    i += vectorSize;
                }

                while (i <= limit)
                {
                    if (data[i] == firstByte && MatchPattern(data.Slice(i), pattern, mask))
                    {
                        results.Add(i);
                    }
                    i++;
                }
            }
            else
            {
                // First byte is wildcard, fallback to linear scan (slower but correct)
                for (int i = 0; i <= dataLength - patternLength; i++)
                {
                    if (MatchPattern(data.Slice(i), pattern, mask))
                    {
                        results.Add(i);
                    }
                }
            }

            return results;
        }

        private static bool MatchPattern(ReadOnlySpan<byte> data, byte[] pattern, bool[] mask)
        {
            for (int i = 0; i < pattern.Length; i++)
            {
                if (mask[i] && data[i] != pattern[i])
                {
                    return false;
                }
            }
            return true;
        }

        private static bool IsReadable(uint protect)
        {
            return protect == PAGE_READONLY ||
                   protect == PAGE_READWRITE ||
                   protect == PAGE_EXECUTE_READ ||
                   protect == PAGE_EXECUTE_READWRITE;
        }

        private static (byte[] bytes, string mask) ParsePattern(string pattern)
        {
            // Example: "AA ?? BB"
            var parts = pattern.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var bytes = new byte[parts.Length];
            var maskChars = new char[parts.Length];

            for (int i = 0; i < parts.Length; i++)
            {
                if (parts[i] == "??")
                {
                    bytes[i] = 0;
                    maskChars[i] = '?';
                }
                else
                {
                    bytes[i] = byte.Parse(parts[i], NumberStyles.HexNumber);
                    maskChars[i] = 'x';
                }
            }

            return (bytes, new string(maskChars));
        }
    }
}
