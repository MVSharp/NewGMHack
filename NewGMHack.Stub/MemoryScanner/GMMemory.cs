using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Memory;
using Microsoft.Extensions.Logging;
using ZLinq;
using ZLinq.Simd;
using ZLogger;

namespace NewGMHack.Stub.MemoryScanner
{
    public class GmMemory(
        Mem mem,
        //FullAoBScanner fullAoBScanner,
        ILogger<GmMemory> logger)
    {
[DllImport("kernel32.dll", SetLastError = true)]
static extern bool ReadProcessMemory(
    IntPtr hProcess,
    IntPtr lpBaseAddress,
    [Out] byte[] lpBuffer,
    int dwSize,
    out IntPtr lpNumberOfBytesRead);
        private static Encoding chs = Encoding.GetEncoding(936) ?? Encoding.Default;

        private static byte[] IgnoreBytes =
        "sprs/"u8.ToArray();

        private readonly ConcurrentDictionary<uint, (string,int w1, int w2, int w3)> _cache = new();

        public void CleanCache()
        {
            _cache.Clear();
        }
        public async Task<(string gname, int w1, int w2, int w3)> Scan(uint id,CancellationToken token)
        {

            logger.ZLogInformation($"Input : {id}");
            try
            {
                if (_cache.TryGetValue(id, out var weaponCache))
                {

                    logger.ZLogInformation($"Scan cache: {id}");
                    return weaponCache;
                }
                //var    mem    = new Mem();
                string text = id.ToString("X");
                string realID = text.Substring(2, 2) + " " + text.Substring(0, 2);
                string processName = Encoding.UTF8.GetString(Convert.FromBase64String("R09ubGluZQ=="));
                if (!mem.OpenProcess(processName))
                    return ("", 0, 0, 0);
                var addresses = (await mem.AoBScan($"{realID} 00 00", true, true)).ToList();

                logger.ZLogInformation($"Address count : {addresses.Count}");
                if (addresses.Count == 0)
                {
                    logger.ZLogInformation($"not found gundam:{id}");
                    return ("", 0, 0, 0);
                }

                var bufferPool = System.Buffers.ArrayPool<byte>.Shared;
                // Reading 600 bytes, allocate at least that much.
                // Note: mem.ReadBytes returns a new byte[], which is external code we can't change easily unless Mem is our class.
                // Wait, 'using Memory;' suggests a library.
                // If 'mem' is a library class (likely Memory.dll from helper), we can't change its return type.
                // However, the commented out code used ReadProcessMemory directly.
                // I will restore the ReadProcessMemory usage with ArrayPool to avoid the 'mem.ReadBytes' allocation!
                
                var process = System.Diagnostics.Process.GetProcessesByName(processName).FirstOrDefault();
                 if (process == null) return ("", 0, 0, 0);

                byte[] pooledBuffer = bufferPool.Rent(600);
                try
                {
                    foreach (var address in addresses.AsValueEnumerable())
                    {
                        // Direct ReadProcessMemory to pooled buffer
                         bool success = ReadProcessMemory(process.Handle, (IntPtr)address, pooledBuffer, 600, out _);
                        if (!success) continue; 
                        
                        var bufferSpan = pooledBuffer.AsSpan(0, 600);
                        
                        if (bufferSpan[5] == 0x00) // Original Logic: buffer[5] != 0x00 needed? 
                            // wait, original logic:  if (buffer is not not null || buffer[5] == 0x00) continue; 
                            // "is not not null" -> "is not null"? Or double negative typo?
                            // "if buffer is not null OR buffer[5] == 0x00 continue" -> logic seems weird.
                            // If buffer returned by ReadBytes is null, continue.
                            // If buffer[5] is 0x00, continue.
                            // So we want: buffer != null AND buffer[5] != 0x00.
                            
                            // Re-implementing validation:
                            if (bufferSpan[5] == 0x00) continue;

                        var name = GetName(bufferSpan);
                        //logger.ZLogInformation($"name:{name}");
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
                {
                    logger.ZLogInformation($"Error: {ex.Message}");
                    // ignored
                }

                return ("", 0, 0, 0);
            }
        }


        private static readonly HashSet<char> Chars =
        [
            '[', ']', 'A', 'B', 'C', 'D', 'E', 'F', 'G', 'H',
            'I', 'J', 'K', 'L', 'M', 'N', 'O', 'P', 'Q', 'R',
            'S', 'T', 'U', 'V', 'W', 'X', 'Y', 'Z', ' ', '(',
            ')', '.', '0', '1', '2', '3', '4', '5', '6', '7',
            '8', '9', '-', '_', '+', '/', '(', ')', '"', '\u0019',
            'a', 'b', 'c', 'd', 'e', 'f', 'g', 'h', 'i', 'j', 'k', 'l', 'm', 'n', 'o', 'p', 'q', 'r', 's', 't', 'u',
            'v', 'w', 'x', 'y', 'z', '`', '\'', '。', '（', '）', '「', '」', '《', '》', '　', '！', ''
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