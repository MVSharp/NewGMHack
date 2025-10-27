using Microsoft.Extensions.Hosting;
using System.Threading.Channels;
using System.Threading.Tasks;
using System.Threading;
using System;
using NewGMHack.Stub.Hooks;

namespace NewGMHack.Stub.Services
{
    public class PacketDispatcher : BackgroundService
    {
        private readonly Channel<ReadOnlyMemory<byte>> _packetChannel;
        private readonly WinsockHookManager _winsockManager;

        public PacketDispatcher(Channel<ReadOnlyMemory<byte>> packetChannel, IEnumerable<IHookManager> hookManagers)
        {
            _packetChannel = packetChannel;
            _winsockManager = hookManagers.OfType<WinsockHookManager>().FirstOrDefault();
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await foreach (var packet in _packetChannel.Reader.ReadAllAsync(stoppingToken))
            {
                if (stoppingToken.IsCancellationRequested)
                    break;

                _winsockManager?.SendPacket(0, packet.Span);
            }
        }
    }
}
