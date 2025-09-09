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
        public async Task<(string gname, int w1, int w2, int w3)> Scan(uint id)
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

                //var process = Process.GetProcessesByName(processName).FirstOrDefault();
                //if (process == null)
                //{
                //    logger.ZLogInformation($"Process not found");
                //    return default;
                //}

                //logger.ZLogInformation($"Begin Aob");
                //var addresses = (await fullAoBScanner.ScanAsync(realID, process)).ToList();

                logger.ZLogInformation($"Address count : {addresses.Count}");
                if (addresses.Count == 0)
                {
                    logger.ZLogInformation($"not found gundam:{id}");
                    return ("", 0, 0, 0);
                }

                //foreach (var buffer in from buffer in addresses.AsValueEnumerable()
                //                                               .Select(address => mem.ReadBytes(address.ToString("X"),
                //                                                                600L)).OfType<byte[]>()
                //                       where buffer[5] != 0x00
                //                       let n = GetName(buffer)
                //                       where !n.Contains("sprs")
                //                       where !string.IsNullOrEmpty(n) && !string.IsNullOrWhiteSpace(n)
                //                       where IsValid(n)
                //                       select buffer)
                //{
                //    return (BitConverter.ToInt32(new byte[] { buffer[585], buffer[586], buffer[587], buffer[588] }, 0),
                //            BitConverter.ToInt32(new byte[] { buffer[589], buffer[590], buffer[591], buffer[592] }
                //                               , 0),
                //            BitConverter.ToInt32(new byte[] { buffer[593], buffer[594], buffer[595], buffer[596] }, 0));
                //}

                foreach (var address in addresses.AsValueEnumerable())
                {
                    var buffer = mem.ReadBytes(address.ToString("X"), 600L);
                    //byte[] buffer = new byte[600];
                    //bool success = ReadProcessMemory(process.Handle, (IntPtr)address, buffer, buffer.Length, out _);

                    //logger.ZLogInformation($"read result : {success}");
                    //if (!success) continue;

                    //logger.ZLogInformation($"scan : {name} | {address}");
                    if (buffer is not not null || buffer[5] == 0x00)
                        continue;

                    var name = GetName(buffer);
                    //logger.ZLogInformation($"name:{name}");
                    if (string.IsNullOrWhiteSpace(name) || name.Contains("sprs") || !IsValid(name))
                        continue;

                    var r = (name,
                        BitConverter.ToInt32(buffer, 585),
                        BitConverter.ToInt32(buffer, 589),
                        BitConverter.ToInt32(buffer, 593)
                    );
                    if (r.Item2 + 1 == r.Item3 || r.Item3 + 1 == r.Item4)
                    {
                        _cache.TryAdd(id, r);
                    }
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