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

        private unsafe void Attack(Reborn[] chunkedReborn, IntPtr socket)
        {
            if (chunkedReborn.Length == 0) return;

            // Calculate actual packet size using sizeof
            // Attack1335 (26 bytes) + 12 * TargetData (12 bytes each = 144) + terminator (1) = 171 bytes total
            int attackHeaderSize = sizeof(Attack1335);
            int targetsDataSize = 12 * sizeof(TargetData);
            int packetSize = attackHeaderSize + targetsDataSize + 1; // +1 for null terminator

            // OPTIMIZATION: Stack allocate the entire packet buffer - ZERO HEAP ALLOCATIONS!
            Span<byte> packetBuffer = stackalloc byte[packetSize];

            var attack = new Attack1335
            {
                Length = 167,  // This is the protocol field, not the actual buffer size
                Split  = 1008,
                Method = 1868,
                PlayerId = _selfInformation.PersonInfo.PersonId,
                WeaponId   = _selfInformation.PersonInfo.Weapon2,
                WeaponSlot = 65281,
            };

            // OPTIMIZATION: Stack allocate targets array - no heap allocation
            Span<TargetData> targets = stackalloc TargetData[12];

            // Fill targets with actual targets using modulo (current logic)
            for (int i = 0; i < 12; i++)
            {
                var reborn = chunkedReborn[i % chunkedReborn.Length];
                targets[i].TargetId = reborn.TargetId;
                targets[i].Damage   = ushort.MaxValue;
            }

            attack.TargetCount = 12;

            // OPTIMIZATION: Use MemoryMarshal.Write for fastest serialization - direct memcpy
            // This writes the Attack1335 struct directly to the buffer
            MemoryMarshal.Write(packetBuffer, in attack);

            // Get byte view of targets array - ZERO ALLOCATION, just a cast
            var targetsBytes = MemoryMarshal.AsBytes(targets);
            targetsBytes.CopyTo(packetBuffer.Slice(attackHeaderSize));

            // Null terminator at the end
            packetBuffer[packetSize - 1] = 0x00;

            // Send first packet (Count fields are 0 by default)
            SendPacket(socket, packetBuffer);

            // Update Count field for second packet (0, 1, 2, ..., 11)
            for (int i = 0; i < 12; i++)
            {
                targets[i].Count = (byte)i;
            }

            // Reuse the same buffer - just update the targets section
            var targetsBytesUpdated = MemoryMarshal.AsBytes(targets);
            targetsBytesUpdated.CopyTo(packetBuffer.Slice(attackHeaderSize));

            // Send second packet with updated Count values
            SendPacket(socket, packetBuffer);
        }
    }
}