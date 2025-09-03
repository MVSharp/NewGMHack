using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Memory;
using Microsoft.Extensions.Logging;
using ZLinq;
using ZLinq.Simd;
using ZLogger;

namespace NewGMHack.Stub.MemoryScanner
{
    public class GmMemory(Mem mem, ILogger<GmMemory> logger)
    {
        private static Encoding chs = Encoding.GetEncoding(936) ?? Encoding.Default;

        private static byte[] IgnoreBytes =
        [
            (byte)'s', (byte)'p', (byte)'r', (byte)'s', (byte)'/'
        ];

        public async Task<(int w1, int w2, int w3)> ScanAsync(uint id)
        {
            try
            {

            //var    mem    = new Mem();
            string text   = id.ToString("X");
            string realID = text.Substring(2, 2) + " " + text.Substring(0, 2);
            if (!mem.OpenProcess("GOnline.exe"))
                return (0, 0, 0);
            var addresses = (await mem.AoBScan($"{realID} 00 00", true, true)).ToList();
            if (addresses.Count == 0)
            {

                logger.ZLogInformation($"not found gundam:{id}");    
                return (0, 0, 0);
            }
            logger.ZLogInformation($"Address count : {addresses.Count}");
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
    if (buffer is not byte[] || buffer[5] == 0x00)
        continue;

    var name = GetName(buffer);
                    logger.ZLogInformation($"name:{name}");
    if (string.IsNullOrWhiteSpace(name) || name.Contains("sprs") || !IsValid(name))
        continue;

    return (
        BitConverter.ToInt32(buffer, 585),
        BitConverter.ToInt32(buffer, 589),
        BitConverter.ToInt32(buffer, 593)
    );
}
            return (0, 0, 0);
            }
            catch
            {

            }

            return (0, 0, 0);
        }


        private static readonly HashSet<char> Chars = new HashSet<char>()
        {
            '[', ']', 'A', 'B', 'C', 'D', 'E', 'F', 'G', 'H',
            'I', 'J', 'K', 'L', 'M', 'N', 'O', 'P', 'Q', 'R',
            'S', 'T', 'U', 'V', 'W', 'X', 'Y', 'Z', ' ', '(',
            ')', '.', '0', '1', '2', '3', '4', '5', '6', '7',
            '8', '9', '-', '_', '+', '/', '(', ')', '"', '\u0019',
            'a', 'b', 'c', 'd', 'e', 'f', 'g', 'h', 'i', 'j', 'k', 'l', 'm', 'n', 'o', 'p', 'q', 'r', 's', 't', 'u',
            'v', 'w', 'x', 'y', 'z','`','\'','。','（','）','「','」','《','》','　','！',''
        };

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