using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Channels;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NewGMHack.Stub.Hooks;
using NewGMHack.Stub.PacketStructs;
using NewGMHack.Stub.PacketStructs.Recv;
using NewGMHack.Stub.PacketStructs.Send;
using ZLinq;
using ZLogger;

namespace NewGMHack.Stub.Services
{
    internal sealed class BombServices(
        Channel<(nint, List<Reborn>)> bombChannel,
        ILogger<BombServices>   _logger,
        IEnumerable<IHookManager> managers,
        SelfInformation               _selfInformation) : BackgroundService
    {
        /// <inheritdoc />
        // [DllImport("ws2_32.dll", CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Unicode,
        //            SetLastError = true)]
        // private static extern int send(nint socket, nint buffer, int length, int flags);
        private readonly WinsockHookManager _hookManager = managers.OfType<WinsockHookManager>().First();
        public unsafe void SendPacket(nint socket, ReadOnlySpan<byte> data, int flags = 0)
        {
            try
            {

                fixed (byte* ptr = data)
                {
                    nint buffer = (nint)ptr;
                    _hookManager.SendPacket(socket, data, flags);
                    //send(socket, buffer, data.Length, flags);
                    //this.OriginalSend(socket, buffer, data.Length, flags);
                }
            }
            catch
            {
                // ignored
            }
        }

        private IntPtr cachedSocket = 0;
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (_selfInformation.PersonInfo.PersonId == 0)
            {
                await Task.Delay(100, stoppingToken);
            }

            while (_selfInformation.PersonInfo.Weapon2 <=0)
            {
                await Task.Delay(100, stoppingToken);
            }

            await foreach (var distinctTargets in bombChannel.Reader.ReadAllAsync(stoppingToken)) {
                foreach (var chunkedReborn in distinctTargets.Item2.AsValueEnumerable().OrderBy(c => c.Location)
                                                             .Chunk(12).ToArray())
                {
                    cachedSocket = distinctTargets.Item1;
                    //   InitTargetData();
                    try
                    {
                        AddHistories(chunkedReborn);
                        await Attack(chunkedReborn, distinctTargets.Item1);
                        await HandleHistories();
                    }
                    catch
                    {

                    }
                }
            }
        }

        private void AddHistories(IEnumerable<Reborn> reborns)
        {
            foreach (var r in reborns)
            {
                
                var isInserted =_selfInformation.BombHistory.Emplace(r.TargetId ,1);
                if (!isInserted)
                {
                    if (_selfInformation.BombHistory.Get(r.TargetId, out var count))
                    {
                        _selfInformation.BombHistory.Update(r.TargetId, ++count);
                    }
                }
            }
        }

        private async Task HandleHistories()
        {
            foreach (var keys in _selfInformation.BombHistory.Keys.Chunk(12))
            {
                List<uint> reborns = new(12);
                foreach (var key in keys)
                {
                    var isGetSucessfully = _selfInformation.BombHistory.Get(key, out var count);
                    if (isGetSucessfully)
                    {
                        if (count >= 10)
                        {
                            _selfInformation.BombHistory.Remove(key);
                            continue;
                        }
                        _selfInformation.BombHistory.Update(key, ++count);
                        reborns.Add(key);
                    }
                }
                _logger.ZLogInformation($"bomb history bomb : {string.Join("|",reborns)}");
                if (reborns.Count > 0)
                {

                    await Attack(reborns.AsValueEnumerable().Select(c => new Reborn(_selfInformation.PersonInfo.PersonId, c, 0)).ToArray(),cachedSocket);
                }
            }
        }
        private async Task Attack(Reborn[] chunkedReborn,IntPtr socket)
        {
            var targets = ValueEnumerable.Repeat(1, 12)
                                         .Select(_ => new TargetData() { Damage = ushort.MaxValue - 1 })
                                         .ToArray(); // new TargetData1335[12>
            //var attack = new Attack1335
            //{
            //    Version = 166,
            //    Split   = 1008,
            //    Method  = 1335,
            //    //     TargetCount = 12,
            //    PlayerId = _selfInformation.PersonInfo.PersonId,
            //    //PlayerId2 = _selfInformation.PlayerId,
            //    WeaponId   = _selfInformation.PersonInfo.Weapon2,
            //    WeaponSlot = 1
            //};

            var attack = new Attack1335
            {
                Version = 167,
                Split   = 1008,
                Method  = 1868,
                //     TargetCount = 12,
                PlayerId = _selfInformation.PersonInfo.PersonId,
                //PlayerId2 = _selfInformation.PlayerId,
                WeaponId   = _selfInformation.PersonInfo.Weapon2,
                WeaponSlot = 65281,
            };
            var i = 0;
            foreach (var reborn in chunkedReborn)
            {
                //  targets[i]          = new TargetData1335();
                targets[i].TargetId = reborn.TargetId;
                targets[i].Damage   = ushort.MaxValue;
                i++;
            }
            while (i < 12)
            {
                var randomReborn = chunkedReborn[Random.Shared.Next(chunkedReborn.Length)];
                targets[i].TargetId = randomReborn.TargetId;
                targets[i].Damage   = ushort.MaxValue;
                i++;
            }
            //attack.TargetData  = targets;
            attack.TargetCount = BitConverter.GetBytes(i)[0];
            //var buf    = DefinitionsExtensions.WriteAttack(attack);
            //if(buf.Length ==0)continue;
            //var length = BitConverter.GetBytes(buf.Length);
            // var hex = BitConverter.ToString(buf).Replace("-", " ");
            var attackBytes  = attack.ToByteArray().AsSpan();
            var targetBytes  = targets.AsSpan().AsByteSpan();
            var attackPacket = attackBytes.CombineWith(targetBytes).CombineWith((ReadOnlySpan<byte>)[0x00]).ToArray();
            //var hex = BitConverter.ToString(attackPacket);
            //_logger.ZLogInformation($"bomb bomb {attackPacket.Length} |the hex: {hex}");
            //await Task.Run(() =>
            //{
                for (int j = 0; j < 3; j++)
            {
                SendPacket(socket, attackPacket);
                //_hookManager.SendPacket(distinctTargets.Item1, buf);
            }
            //});
        }
    }
}