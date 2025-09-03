using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Channels;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using NewGMHack.Stub.PacketStructs;
using NewGMHack.Stub.PacketStructs.Recv;
using NewGMHack.Stub.PacketStructs.Send;
using ZLinq;

namespace NewGMHack.Stub.Services
{
    internal sealed class BombServices(
        Channel<(nint, List<Reborn>)> bombChannel,
        //WinsockHookManager            _hookManager,
        SelfInformation               _selfInformation) : BackgroundService
    {
        /// <inheritdoc />
        [DllImport("ws2_32.dll", CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Unicode,
                   SetLastError = true)]
        private static extern int send(nint socket, nint buffer, int length, int flags);

        public unsafe void SendPacket(nint socket, ReadOnlySpan<byte> data, int flags = 0)
        {
            try
            {

            fixed (byte* ptr = data)
            {
                nint buffer = (nint)ptr;
                send(socket, buffer, data.Length, flags);
                //this.OriginalSend(socket, buffer, data.Length, flags);
            }
            }
            catch
            {
                
            }
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (_selfInformation.info.PersonId == 0)
            {
                await Task.Delay(100);
            }

            while (_selfInformation.info.Weapon2 == 0)
            {
                await Task.Delay(100);
            }

            await foreach (var distinctTargets in bombChannel.Reader.ReadAllAsync(stoppingToken))
            {
                foreach (var chunkedReborn in distinctTargets.Item2.AsValueEnumerable().OrderBy(c => c.Location)
                                                             .Chunk(12).ToArray())
                {
                    //   InitTargetData();
                    var targets = ValueEnumerable.Repeat(1, 12)
                                                 .Select(y => new TargetData() { Damage = ushort.MaxValue - 1 })
                                                 .ToArray(); // new TargetData[12>
                    var attack = new Attack
                    {
                        Version = 166,
                        Split   = 1008,
                        Method  = 1335,
                        //     TargetCount = 12,
                        PlayerId = _selfInformation.info.PersonId,
                        //PlayerId2 = _selfInformation.PersonId,
                        WeaponId   = _selfInformation.info.Weapon2,
                        WeaponSlot = 1
                    };
                    var i = 0;
                    foreach (var reborn in chunkedReborn)
                    {
                        //  targets[i]          = new TargetData();
                        targets[i].TargetId = reborn.TargetId;
                        targets[i].Damage   = ushort.MaxValue;
                        i++;
                    }

                    attack.TargetData  = targets;
                    attack.TargetCount = BitConverter.GetBytes(i)[0];
                    var buf    = DefinitionsExtensions.WriteAttack(attack);
                    if(buf.Length ==0)continue;
                    var length = BitConverter.GetBytes(buf.Length);
                    // var hex = BitConverter.ToString(buf).Replace("-", " ");
                    // _logger.ZLogInformation($"bomb bomb {buf.Length} |the hex: {hex}");
                    await Task.Run(() =>
                    {
                        for (int j = 0; j < 5; j++)
                        {
                            SendPacket(distinctTargets.Item1, buf);
                            //_hookManager.SendPacket(distinctTargets.Item1, buf);
                        }
                    });
                }
            }
        }
    }
}