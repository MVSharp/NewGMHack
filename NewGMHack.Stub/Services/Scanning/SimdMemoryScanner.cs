using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Runtime.ExceptionServices;
using System.Security;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ZLogger;
using System.Runtime.CompilerServices; // For Unsafe

namespace NewGMHack.Stub.Services.Scanning
{
     public class SimdMemoryScanner : IMemoryScanner
    {
        private readonly ILogger<SimdMemoryScanner> _logger;

        public SimdMemoryScanner(ILogger<SimdMemoryScanner> logger)
        {
            _logger = logger;
        }

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
            var batchInput = new List<(byte[] pattern, string mask, int id)>
            {
                (pattern, mask ?? "", 0)
            };

            var results = await ScanBatchAsync(process, batchInput, token);
            return results.TryGetValue(0, out var list) ? list : new List<long>();
        }

        private struct PatternInfo
        {
            public int Id;
            public byte[] Pattern;
            public bool[] Mask;
            public byte AnchorByte;
            public int AnchorOffset;
            public int PatternLength;
            public SearchValues<byte> AnchorSearch; // Optimized SearchValues
        }

        public async Task<Dictionary<int, List<long>>> ScanBatchAsync(Process process, List<(byte[] pattern, string mask, int id)> searchPatterns, CancellationToken token = default)
        {
            var results = new ConcurrentDictionary<int, ConcurrentBag<long>>();
            if (process == null || process.HasExited || searchPatterns == null || searchPatterns.Count == 0) 
                return new Dictionary<int, List<long>>();

            // 1. Pre-process patterns
            var patterns = new List<PatternInfo>();
            int maxPatternLength = 0;

            foreach (var (p, m, id) in searchPatterns)
            {
                if (p == null || p.Length == 0) continue;
                
                string mask = string.IsNullOrEmpty(m) ? new string('x', p.Length) : m;
                if (mask.Length != p.Length) continue;

                var boolMask = mask.Select(c => c != '?').ToArray();
                var anchor = SelectAnchor(p, boolMask);

                patterns.Add(new PatternInfo
                {
                    Id = id,
                    Pattern = p,
                    Mask = boolMask,
                    AnchorByte = anchor.Byte,
                    AnchorOffset = anchor.Offset,
                    PatternLength = p.Length,
                    AnchorSearch = SearchValues.Create([anchor.Byte]) // Pre-calculate SearchValues
                });

                if (p.Length > maxPatternLength) maxPatternLength = p.Length;
                
                // Initialize result bag
                results[id] = new ConcurrentBag<long>();
            }

            if (patterns.Count == 0) return new Dictionary<int, List<long>>();

            // 2. Collect Regions & Jobs
            // We use the LONGEST pattern to determine minimum chunk size validity, 
            // but for safety we should arguably use the shortest or handle them individually.
            // However, CreateScanJobs filters out regions smaller than pattern. 
            // We should pass maxPatternLength to ensure we don't process tiny regions useless to everyone,
            // or better: pass 1 (or min) and filter inside. 
            // For now, let's use the SHORTEST pattern length for job creation to be inclusive,
            // and filter individually.
            int minPatternLength = patterns.Min(x => x.PatternLength);
            
            var regions = GetRegions(process);
            var jobs = CreateScanJobs(regions, minPatternLength);

            // 3. Parallel Scan (Direct Memory Access - No ReadProcessMemory!)
            // Since we're injected, we can read memory directly using unsafe pointers.
            // This eliminates syscall overhead entirely.
            
            Parallel.ForEach(jobs, 
                new ParallelOptions { CancellationToken = token, MaxDegreeOfParallelism = Math.Max(1, 3) },
                job => ScanJobDirect(job, maxPatternLength, patterns, results));
            
            return results.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.ToList());
        }

        /// <summary>
        /// Scans a job directly using unsafe pointers. Must be a separate method
        /// with HandleProcessCorruptedStateExceptions to catch AccessViolationException.
        /// </summary>
        [HandleProcessCorruptedStateExceptions]
        [SecurityCritical]
        private unsafe void ScanJobDirect(ScanJob job, int maxPatternLength, List<PatternInfo> patterns, ConcurrentDictionary<int, ConcurrentBag<long>> results)
        {             
            try
            {
                if (!VirtualQueryEx(Process.GetCurrentProcess().Handle, job.BaseAddress, out var mbi, (uint)Marshal.SizeOf<MEMORY_BASIC_INFORMATION>()))
                    return;

                if (mbi.State != MEM_COMMIT || !IsReadable(mbi.Protect))
                    return;

                long regionEnd = mbi.BaseAddress.ToInt64() + mbi.RegionSize.ToInt64();
                long requestedEnd = job.BaseAddress.ToInt64() + job.Size + maxPatternLength;
                
                int actualSize = job.Size + maxPatternLength; 
                if (requestedEnd > regionEnd)
                {
                    actualSize = (int)(regionEnd - job.BaseAddress.ToInt64());
                }
                
                if (actualSize < maxPatternLength) return;

                byte* pMemory = (byte*)job.BaseAddress;

                foreach (var pat in patterns)
                {
                     try
                     {
                         if (actualSize < pat.PatternLength) continue;
                         ScanRegionInternal(pMemory, actualSize, pat, job.BaseAddress.ToInt64(), results[pat.Id]);
                     }
                     catch (AccessViolationException) { /* Skip page if it unloads */ }
                     catch (Exception ex) { _logger?.ZLogWarning($"ScanJob Pattern {pat.Id} error: {ex.Message}"); }
                }

            }
            catch (Exception ex)
            {
                _logger?.ZLogError($"Batch Scan Job Error at 0x{job.BaseAddress:X}: {ex.Message}");
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        private unsafe void ScanRegionInternal(byte* regionStart, int regionSize, PatternInfo pat, long baseAddress, ConcurrentBag<long> results)
        {
            // Direct Memory Access (Internal) - Zero Copy
            // We search for the Anchor Byte using hardware acceleration
            
            // Limit: Ensure we don't read past the region
            // effectiveEnd depends on where the pattern would end relative to the anchor
            // If Anchor is at index 'A' in pattern (Len 'L'), and we find Anchor at 'P' in memory:
            // Pattern starts at 'P - A'. Pattern ends at 'P - A + L'.
            // We need 'P - A + L <= regionSize'.
            // So P <= regionSize - L + A.
            
            int searchLimit = regionSize - pat.PatternLength + pat.AnchorOffset;
            int offset = pat.AnchorOffset;
            
            // Create a span over the search area
            // We only search for the ANCHOR byte within the valid range
            int len = searchLimit - offset + 1;
            if (len <= 0) return;
            
            ReadOnlySpan<byte> searchSpace = new ReadOnlySpan<byte>(regionStart + offset, len);
            
            int currentPos = 0;
            while (true)
            {
                // .NET 10 / 8+ Optimization: Uses AVX-512 / AVX2 automatically
                int idx = searchSpace.Slice(currentPos).IndexOfAny(pat.AnchorSearch);
                
                if (idx < 0) break;
                
                currentPos += idx;
                
                // Found Anchor at (regionStart + offset + currentPos)
                // Pattern starts at (offset + currentPos) - pat.AnchorOffset
                // relative to regionStart.
                // Since start of searchSpace is 'regionStart + pat.AnchorOffset', 
                // The actual memory address of anchor is: regionStart + pat.AnchorOffset + currentPos
                // Pattern start address: (regionStart + pat.AnchorOffset + currentPos) - pat.AnchorOffset
                // = regionStart + currentPos
                
                // Wait, let's re-verify math.
                // SearchSpace starts at `regionStart + AnchorOffset`.
                // Found index `idx` is relative to `currentPos` (which is relative to SearchSpace start).
                // So Anchor is at: `regionStart + AnchorOffset + currentPos`
                // Pattern Start is: `AnchorAddress - AnchorOffset` = `regionStart + currentPos`
                
                // Check the full pattern
                // We use ReadOnlySpan to compare memory
                ReadOnlySpan<byte> memorySpan = new ReadOnlySpan<byte>(regionStart + currentPos, pat.PatternLength);
                
                if (MatchPattern(memorySpan, pat.Pattern, pat.Mask))
                {
                    results.Add(baseAddress + currentPos);
                }
                
                currentPos++; // Advance past this match
                if (currentPos >= len) break;
            }
        }

        
        private readonly struct AnchorInfo
        {
            public readonly int Offset;
            public readonly byte Byte;
            public AnchorInfo(int offset, byte b) { Offset = offset; Byte = b; }
        }

        private AnchorInfo SelectAnchor(byte[] pattern, bool[] mask)
        {
            // Improved heuristic: Score each candidate anchor byte
            // Prefer: rare bytes > later positions > non-00/FF bytes
            
            int bestScore = -1;
            int bestOffset = 0;
            byte bestByte = 0;
            
            // Common bytes that appear frequently in memory (low score)
            // 0x00 (null), 0xFF (padding), 0x20 (space), 0xCC (debug fill)
            ReadOnlySpan<byte> commonBytes = stackalloc byte[] { 0x00, 0xFF, 0x20, 0xCC, 0x90, 0xCD };
            
            for (int i = 0; i < pattern.Length; i++)
            {
                if (!mask[i]) continue; // Skip wildcards
                
                byte b = pattern[i];
                int score = 0;
                
                // Score: Later positions are better (reduces re-checks on false positives)
                score += i;
                
                // Score: Non-common bytes get big bonus
                bool isCommon = false;
                foreach (byte c in commonBytes)
                {
                    if (b == c) { isCommon = true; break; }
                }
                if (!isCommon) score += 100;
                
                // Score: Signature-like bytes (ASCII letters used in paths like 'm', 'd', 'r', 's', 'f', 'x')
                if ((b >= 0x61 && b <= 0x7A) || (b >= 0x41 && b <= 0x5A)) // a-z, A-Z
                {
                    score += 50;
                }
                
                if (score > bestScore)
                {
                    bestScore = score;
                    bestOffset = i;
                    bestByte = b;
                }
            }
            
            // Fallback if no valid mask found
            if (bestScore < 0 && pattern.Length > 0)
            {
                for (int i = 0; i < pattern.Length; i++)
                {
                    if (mask[i]) return new AnchorInfo(i, pattern[i]);
                }
                return new AnchorInfo(0, pattern[0]);
            }
            
            return new AnchorInfo(bestOffset, bestByte);
        }

        private struct ScanJob
        {
            public IntPtr BaseAddress;
            public int Size; // The number of "start positions" to scan
        }
        
        private const int MAX_CHUNK_SIZE = 8 * 1024 * 1024; // 8MB chunks

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

                // Optimization: Search only MEM_PRIVATE (Dynamic Heaps). 
                // Exclude MEM_IMAGE (Static DLL/EXE code) and MEM_MAPPED (File mappings).
                bool isValidType = (mbi.Type == MEM_PRIVATE);
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

        private static List<int> FindPatternSIMD(ReadOnlySpan<byte> data, byte[] pattern, bool[] mask, byte anchorByte, int anchorOffset)
        {
            var results = new List<int>();
            int patternLength = pattern.Length;
            int dataLength = data.Length;
            int vectorSize = Vector<byte>.Count;

            // We scan 'data' looking for 'anchorByte'.
            // Effective search range for anchor: [anchorOffset, dataLength - (patternLength - anchorOffset)]
            // Because if anchor is found at 'p', the pattern starts at 'p - anchorOffset'.
            
            // Adjust data range to search for the ANCHOR
            int searchStart = anchorOffset;
            int searchEnd = dataLength - (patternLength - anchorOffset); 
            // e.g. Data=100, Pat=10, AnchorAt=2. Anchor must be in [2, 100-(8)] = [2, 92].
            // If Anchor at 2, Start at 0. If Anchor at 92, Start at 90. End at 90+10=100.
            
            if (searchEnd < searchStart) return results;

            int limit = searchEnd - searchStart + 1;
            // The slice we actually search in:
            var searchSpace = data.Slice(searchStart, searchEnd - searchStart);
            
            // SIMD Search on the Search Space
            Vector<byte> searchVector = new Vector<byte>(anchorByte);
            
            int i = 0;
            int vectorLimit = searchSpace.Length - vectorSize;

            // 1. Vectorized Search for Anchor
            while (i <= vectorLimit)
            {
                var chunk = new Vector<byte>(searchSpace.Slice(i, vectorSize));
                var equals = Vector.Equals(chunk, searchVector);
                
                if (equals != Vector<byte>.Zero)
                {
                    // Fallback for missing ExtractMostSignificantBits in older .NET versions / targets
                    for (int k = 0; k < vectorSize; k++)
                    {
                        if (equals[k] != 0) // Byte matched
                        {
                            int patternStart = i + k;
                            if (patternStart >= 0 && patternStart <= dataLength - patternLength)
                            {
                                if (MatchPattern(data.Slice(patternStart), pattern, mask))
                                {
                                    results.Add(patternStart);
                                }
                            }
                        }
                    }
                }
                i += vectorSize;
            }

            // 2. Tail Search
            while (i < searchSpace.Length)
            {
                if (searchSpace[i] == anchorByte)
                {
                    int patternStart = i; // Same logic as above
                    if (MatchPattern(data.Slice(patternStart), pattern, mask))
                    {
                        results.Add(patternStart);
                    }
                }
                i++;
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
            // Restrict to WRITABLE memory only to match Cheat Engine's default behavior.
            // Scanning ReadOnly (0x02) or ExecuteRead (0x20) often yields static/garbage pointers
            // that cause crashes when we try to write to them or treat them as valid objects.
            return protect == PAGE_READWRITE ||
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
