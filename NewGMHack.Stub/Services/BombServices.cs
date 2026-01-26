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
        ILogger<BombServices>         _logger,
        IEnumerable<IHookManager>     managers,
        SelfInformation               _selfInformation) : BackgroundService
    {
        /// <inheritdoc />
        private readonly WinsockHookManager _hookManager = managers.OfType<WinsockHookManager>().First();

        public unsafe void SendPacket(nint socket, ReadOnlySpan<byte> data, int flags = 0)
        {
            try
            {
                fixed (byte* ptr = data)
                {
                    nint buffer = (nint)ptr;
                    _hookManager.SendPacket(socket, data, flags);
                }
            }
            catch
            {
                // ignored
            }
        }


        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (_selfInformation.PersonInfo.PersonId == 0)
            {
                await Task.Delay(100, stoppingToken);
            }

            while (_selfInformation.PersonInfo.Weapon2 <= 0)
            {
                await Task.Delay(100, stoppingToken);
            }

            await foreach (var distinctTargets in bombChannel.Reader.ReadAllAsync(stoppingToken))
            {
                foreach (var chunkedReborn in distinctTargets.Item2.AsValueEnumerable().OrderBy(c => c.Location)
                                                             .Chunk(12).ToArray())
                {
                    try
                    {
                        Attack(chunkedReborn, distinctTargets.Item1);
                        AddHistories(chunkedReborn);
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
                var isInserted = _selfInformation.BombHistory.TryAdd(r.TargetId, 1);
                if (!isInserted)
                {
                    if (_selfInformation.BombHistory.TryGetValue(r.TargetId, out var count))
                    {
                        if (count > 10)
                        {
                            var isRemoved = _selfInformation.BombHistory.TryRemove(r.TargetId, out var _);
                            if (isRemoved)
                            {
                                _logger.ZLogInformation($"removed {r.TargetId} from bomb histroy due to exeed limit");
                            }
                            else
                            {
                                _logger.ZLogInformation($"failed to remove {r.TargetId} from bomb histroy");
                            }
                        }
                        else
                        {
                            _selfInformation.BombHistory.AddOrUpdate(r.TargetId, count + 1, (_, c) => c + 1);
                        }
                    }
                }
            }
        }

        private void Attack(Reborn[] chunkedReborn, IntPtr socket)
        {
            var targets = ValueEnumerable.Repeat(1, 12)
                                         .Select(_ => new TargetData() { Damage = ushort.MaxValue - 1 })
                                         .ToArray(); // new TargetData1335[12>
            var attack = new Attack1335
            {
                Length = 167,
                Split  = 1008,
                Method = 1868,
                //     TargetCount = 12,
                PlayerId = _selfInformation.PersonInfo.PersonId,
                //PlayerId2 = _selfInformation.MyPlayerId,
                WeaponId   = _selfInformation.PersonInfo.Weapon2,
                WeaponSlot = 65281,
            };
            if (chunkedReborn.Length == 0) return;
            for (int i = 0; i < 12; i++)
            {
                var reborn = chunkedReborn[i % chunkedReborn.Length];
                targets[i].TargetId = reborn.TargetId;
                targets[i].Damage   = ushort.MaxValue;
            }

            attack.TargetCount = 12;
            var attackBytes  = attack.ToByteArray().AsSpan();
            var targetBytes  = targets.AsSpan().AsByteSpan();
            var attackPacket = attackBytes.CombineWith(targetBytes).CombineWith((ReadOnlySpan<byte>)[0x00]).ToArray();
            SendPacket(socket, attackPacket);
            for (int i = 0; i < targets.Length; i++)
            {
                targets[i].Count = (byte)i;
            }

            var targetBytes1  = targets.AsSpan().AsByteSpan();
            var attackPacket1 = attackBytes.CombineWith(targetBytes1).CombineWith((ReadOnlySpan<byte>)[0x00]).ToArray();
            SendPacket(socket, attackPacket1);
        }
    }
}