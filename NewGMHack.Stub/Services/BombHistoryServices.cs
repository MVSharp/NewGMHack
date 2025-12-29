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
                    if (_selfInformation.BombHistory.TryGetValue(key, out var count))
                    {
                        if (count >= 10)
                        {
                            var isRemoved =_selfInformation.BombHistory.TryRemove(key, out _);
                            if (isRemoved)
                            {
                                //_logger.ZLogInformation($"Removed {key} from bomb history due to exeeed limit");
                            }
                            else
                            {

                                _logger.ZLogInformation($"failed to Removed {key} from bomb history");
                            }
                            continue;
                        }

                        _selfInformation.BombHistory.AddOrUpdate(key, count + 1, (_, c) => c + 1);
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
            // Use stackalloc for high performance and zero allocation
            // Packet size calculation:
            // Attack1335 struct size (approx 30 bytes) + 12 * TargetData struct size + extra bytes
            // It's safe to allocate 1024 bytes on stack as it fits comfortably
            Span<byte> buffer = stackalloc byte[1024]; 
            var packetBuilder = new PacketBuilder(buffer);

            // Construct Attack1335 manually or via struct if blittable
            // Let's assume we build it manually or use the struct writer if available.
            // Since we don't have the Attack1335 struct definition fully revealed here but we know the fields,
            // we will reconstruct it efficiently.
            
            // Header parts based on previous code:
            // Version (2), Split (2), Method (2), Unknown1 (4), PlayerId (4), WeaponId (4), WeaponSlot (4 + ?)
            // The original code used DefinitionsExtensions.WriteAttack or ToByteArray. 
            // We'll write directly to the packet builder.
            
            packetBuilder.Write<ushort>(167); // Version
            packetBuilder.Write<ushort>(1008); // Split
            packetBuilder.Write<ushort>(1868); // Method
            packetBuilder.Write<uint>(0);  // Unknown1 (assuming 0 or padding) - Wait, struct had Unknown1. Let's assume 0 for now as it wasn't set explicitly in the 'new' block except implicitly 0.
            
            packetBuilder.Write<uint>(_selfInformation.PersonInfo.PersonId); // PlayerId
            packetBuilder.Write<uint>(_selfInformation.PersonInfo.Weapon2);  // WeaponId
            // WeaponSlot = 65281 (0xFF01) - 4 bytes? or 2? struct name suggests slot. Usually int.
            packetBuilder.Write<uint>(65281); 
            
            // PlayerId2 (4), Unknown2 (4)?
            // The original code commented out keys but we need to match the struct layout EXACTLY.
            // Let's rely on the previous struct fields order:
            // Version, Split, Method, Unknown1, PlayerId, WeaponId, WeaponSlot, PlayerId2, Unknown2, TargetCount
            
            packetBuilder.Write<uint>(0); // PlayerId2 (default 0)
            packetBuilder.Write<uint>(0); // Unknown2 (default 0)
            
            // Target count
            int count = Math.Min(chunkedReborn.Length, 12);
            packetBuilder.Write<byte>((byte)count); // TargetCount
            
            // Now targets
            // TargetData: TargetId (4), Damage (2), Unknown1 (2?), Unknown2 (2?), Unknown3 (2?)
            // Based on previous code: TargetId, Damage, Unknown1, Unknown2, Unknown3
            
            for (int k = 0; k < count; k++)
            {
                var reborn = chunkedReborn[k];
                packetBuilder.Write<uint>(reborn.TargetId);
                packetBuilder.Write<ushort>(ushort.MaxValue); // Damage
                packetBuilder.Write<ushort>(0); // Unknown1
                packetBuilder.Write<ushort>(0); // Unknown2
                packetBuilder.Write<ushort>(0); // Unknown3
            }
            
            // Fill remaining if needed? The loop `while (i < 12)` suggests we might need to pad to 12 targets always?
            // "while (i < 12)" logic in original code: fills with random reborns.
            // Yes, we must stick to that logic.
            
            for (int k = count; k < 12; k++)
            {
                var randomReborn = chunkedReborn[Random.Shared.Next(chunkedReborn.Length)];
                 packetBuilder.Write<uint>(randomReborn.TargetId);
                packetBuilder.Write<ushort>(ushort.MaxValue); // Damage
                packetBuilder.Write<ushort>(0); // Unknown1
                packetBuilder.Write<ushort>(0); // Unknown2
                packetBuilder.Write<ushort>(0); // Unknown3
            }
            
             packetBuilder.WriteBytes((ReadOnlySpan<byte>)[0x00]); // Final byte

            var finalPacket = packetBuilder.ToSpan();

            for (int j = 0; j < 3; j++)
            {
                SendPacket(socket, finalPacket);
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