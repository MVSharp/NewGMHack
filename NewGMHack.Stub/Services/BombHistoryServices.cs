using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Channels;
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
    internal class BombHistoryServices(
        ILogger<BombHistoryServices>  _logger,
        IEnumerable<IHookManager>     managers,
        Channel<(nint, List<Reborn>)> bombChannel,
        SelfInformation               _selfInformation) : BackgroundService
    {
        private readonly WinsockHookManager _hookManager = managers.OfType<WinsockHookManager>().First();

        /// <inheritdoc />
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await HandleHistories(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.ZLogError(ex, $"Error processing bomb history");
                } // run every 10 ms await Task.Delay(10, stoppingToken); }

                await Task.Delay(100,stoppingToken); 
            }
        }

        private async Task HandleHistories(CancellationToken stoppingToken)
        {
            foreach (var keys in _selfInformation.BombHistory.Keys.Chunk(12).ToList())
            {
                var reborns = new List<uint>(12);
                foreach (var key in keys)
                {
                    if (_selfInformation.BombHistory.Get(key, out var count))
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

                if (reborns.Count > 0)
                {
                    //_logger.ZLogInformation($"bomb history bomb : {string.Join("|", reborns)}");
                    var rebornObjs = reborns.AsValueEnumerable()
                                            .Select(c => new Reborn(_selfInformation.PersonInfo.PersonId, c, 0))
                                            .ToList(); // reuse BombServices.Attack await _bombServices.Attack(rebornObjs, cachedSocket); }
                   await bombChannel.Writer.WriteAsync(( _selfInformation.LastSocket,rebornObjs));
                    //await Attack(rebornObjs, _selfInformation.LastSocket);
                }
            }
        }

        private async Task Attack(Reborn[] chunkedReborn, IntPtr socket)
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
    }
}