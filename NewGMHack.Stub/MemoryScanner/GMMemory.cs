using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ZLinq;
using ZLogger;

namespace NewGMHack.Stub.MemoryScanner
{
    /// <summary>
    /// Fast memory scanner using SIMD (Vector&lt;byte&gt;) for pattern matching
    /// </summary>
    public class GmMemory(ILogger<GmMemory> logger)
    {
        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool ReadProcessMemory(
            IntPtr hProcess,
            IntPtr lpBaseAddress,
            [Out] byte[] lpBuffer,
            int dwSize,
            out IntPtr lpNumberOfBytesRead);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool VirtualQueryEx(
            IntPtr hProcess,
            IntPtr lpAddress,
            out MEMORY_BASIC_INFORMATION lpBuffer,
            uint dwLength);

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
        private const uint PAGE_READWRITE = 0x04;
        private const uint PAGE_READONLY = 0x02;
        private const uint PAGE_EXECUTE_READ = 0x20;
        private const uint PAGE_EXECUTE_READWRITE = 0x40;

        private static Encoding chs = Encoding.GetEncoding(936) ?? Encoding.Default;
        private static byte[] IgnoreBytes = "sprs/"u8.ToArray();
        private readonly ConcurrentDictionary<uint, (string, int w1, int w2, int w3)> _cache = new();

        public void CleanCache() => _cache.Clear();

        public async Task<(string gname, int w1, int w2, int w3)> Scan(uint id, CancellationToken token)
        {
            logger.ZLogInformation($"Input : {id}");
            try
            {
                if (_cache.TryGetValue(id, out var weaponCache))
                {
                    logger.ZLogInformation($"Scan cache: {id}");
                    return weaponCache;
                }

                // Build pattern bytes
                byte[] patternBytes = BuildPattern(id);
                string processName = Encoding.UTF8.GetString(Convert.FromBase64String("R09ubGluZQ=="));

                var process = Process.GetProcessesByName(processName).FirstOrDefault();
                if (process == null)
                {
                    logger.ZLogInformation($"Process not found");
                    return ("", 0, 0, 0);
                }

                var sw = Stopwatch.StartNew();
                
                // Use SIMD-optimized scanner
                var addresses = await Task.Run(() => ScanProcessMemorySIMD(process, patternBytes), token);
                
                sw.Stop();
                logger.ZLogInformation($"SIMD scan found {addresses.Count} addresses in {sw.ElapsedMilliseconds}ms");

                if (addresses.Count == 0)
                {
                    logger.ZLogInformation($"not found gundam:{id}");
                    return ("", 0, 0, 0);
                }

                var bufferPool = ArrayPool<byte>.Shared;
                byte[] pooledBuffer = bufferPool.Rent(600);
                
                try
                {
                    foreach (var address in addresses)
                    {
                        bool success = ReadProcessMemory(process.Handle, (IntPtr)address, pooledBuffer, 600, out _);
                        if (!success) continue;

                        var bufferSpan = pooledBuffer.AsSpan(0, 600);
                        if (bufferSpan[5] == 0x00) continue;

                        var name = GetName(bufferSpan);
                        if (string.IsNullOrWhiteSpace(name) || name.Contains("sprs") || !IsValid(name))
                            continue;

                        var r = (name,
                            BitConverter.ToInt32(pooledBuffer, 585),
                            BitConverter.ToInt32(pooledBuffer, 589),
                            BitConverter.ToInt32(pooledBuffer, 593)
                        );
                        
                        if (r.Item2 + 1 == r.Item3 || r.Item3 + 1 == r.Item4)
                        {
                            _cache.TryAdd(id, r);
                            return r;
                        }
                    }
                }
                finally
                {
                    bufferPool.Return(pooledBuffer);
                }

                return ("", 0, 0, 0);
            }
            catch (Exception ex)
            {
                logger.ZLogInformation($"Error: {ex.Message}");
                return ("", 0, 0, 0);
            }
        }

        /// <summary>
        /// Build the 4-byte pattern from ID (little-endian + 00 00)
        /// </summary>
        private static byte[] BuildPattern(uint id)
        {
            string hex = id.ToString("X").PadLeft(4, '0');
            return new byte[]
            {
                Convert.ToByte(hex.Substring(2, 2), 16),
                Convert.ToByte(hex.Substring(0, 2), 16),
                0x00,
                0x00
            };
        }

        /// <summary>
        /// SIMD-optimized memory scan using Vector&lt;byte&gt;
        /// </summary>
        private List<long> ScanProcessMemorySIMD(Process process, byte[] pattern)
        {
            var results = new List<long>();
            var bufferPool = ArrayPool<byte>.Shared;
            
            IntPtr address = IntPtr.Zero;
            IntPtr maxAddress = (IntPtr)0x7FFFFFFF; // 32-bit max

            byte firstByte = pattern[0];
            int patternLength = pattern.Length;

            while (address.ToInt64() < maxAddress.ToInt64())
            {
                if (!VirtualQueryEx(process.Handle, address, out var mbi, (uint)Marshal.SizeOf<MEMORY_BASIC_INFORMATION>()))
                    break;

                if (mbi.State == MEM_COMMIT && IsReadable(mbi.Protect))
                {
                    int regionSize = (int)mbi.RegionSize.ToInt64();
                    
                    if (regionSize > 0 && regionSize <= 64 * 1024 * 1024)
                    {
                        byte[] buffer = bufferPool.Rent(regionSize);
                        try
                        {
                            if (ReadProcessMemory(process.Handle, mbi.BaseAddress, buffer, regionSize, out var bytesRead))
                            {
                                int actualSize = (int)bytesRead.ToInt64();
                                if (actualSize >= patternLength)
                                {
                                    // SIMD scan this region
                                    var matches = FindPatternSIMD(buffer.AsSpan(0, actualSize), pattern, firstByte);
                                    foreach (var offset in matches)
                                    {
                                        results.Add(mbi.BaseAddress.ToInt64() + offset);
                                    }
                                }
                            }
                        }
                        finally
                        {
                            bufferPool.Return(buffer);
                        }
                    }
                }

                address = (IntPtr)(mbi.BaseAddress.ToInt64() + mbi.RegionSize.ToInt64());
                if (mbi.RegionSize == IntPtr.Zero) break;
            }

            return results;
        }

        /// <summary>
        /// SIMD pattern search using Vector&lt;byte&gt; for first-byte filtering
        /// </summary>
        private static List<int> FindPatternSIMD(ReadOnlySpan<byte> data, byte[] pattern, byte firstByte)
        {
            var results = new List<int>();
            int patternLength = pattern.Length;
            int dataLength = data.Length;
            int vectorSize = Vector<byte>.Count;
            
            if (dataLength < patternLength) return results;

            // Create vector filled with first byte for SIMD comparison
            Vector<byte> searchVector = new Vector<byte>(firstByte);
            
            int i = 0;
            int limit = dataLength - patternLength;
            int vectorLimit = limit - vectorSize + 1;

            // SIMD phase: scan vectorSize bytes at a time
            while (i < vectorLimit)
            {
                var chunk = new Vector<byte>(data.Slice(i, vectorSize));
                var equals = Vector.Equals(chunk, searchVector);
                
                if (equals != Vector<byte>.Zero)
                {
                    // Potential matches in this chunk - check each position
                    for (int j = 0; j < vectorSize && i + j <= limit; j++)
                    {
                        if (data[i + j] == firstByte && MatchPattern(data.Slice(i + j), pattern))
                        {
                            results.Add(i + j);
                        }
                    }
                }
                i += vectorSize;
            }

            // Scalar phase: handle remaining bytes
            while (i <= limit)
            {
                if (data[i] == firstByte && MatchPattern(data.Slice(i), pattern))
                {
                    results.Add(i);
                }
                i++;
            }

            return results;
        }

        /// <summary>
        /// Match full pattern at position
        /// </summary>
        private static bool MatchPattern(ReadOnlySpan<byte> data, byte[] pattern)
        {
            for (int i = 0; i < pattern.Length; i++)
            {
                if (data[i] != pattern[i]) return false;
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

        private static readonly HashSet<char> Chars =
        [
            '[', ']', 'A', 'B', 'C', 'D', 'E', 'F', 'G', 'H',
            'I', 'J', 'K', 'L', 'M', 'N', 'O', 'P', 'Q', 'R',
            'S', 'T', 'U', 'V', 'W', 'X', 'Y', 'Z', ' ', '(',
            ')', '.', '0', '1', '2', '3', '4', '5', '6', '7',
            '8', '9', '-', '_', '+', '/', '(', ')', '"', '\u0019',
            'a', 'b', 'c', 'd', 'e', 'f', 'g', 'h', 'i', 'j', 'k', 'l', 'm', 'n', 'o', 'p', 'q', 'r', 's', 't', 'u',
            'v', 'w', 'x', 'y', 'z', '`', '\'', '。', '（', '）', '「', '」', '《', '》', '　', '！', 
        ];

        public bool IsValid(ReadOnlySpan<char> buffer)
        {
            return buffer.Length >= 6 && Chars.Contains(buffer[0])
                                      && Chars.Contains(buffer[1])
                                      && Chars.Contains(buffer[2])
                                      && Chars.Contains(buffer[3])
                                      && Chars.Contains(buffer[4])
                                      && Chars.Contains(buffer[5]);
        }

        public string GetName(ReadOnlySpan<byte> buffer)
        {
            if (buffer.IndexOf(IgnoreBytes) >= 0) return "";
            var nameBuf = buffer.Slice(105, 205).ToArray().AsValueEnumerable()
                                .Where(x => x != (byte)'\b' && x != (byte)'\t' && x != (byte)'.' && x != 0x00)
                                .ToArray();
            return chs.GetString(nameBuf);
        }
    }
}
