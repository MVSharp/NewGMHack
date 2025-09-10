// using MessagePipe;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NewGMHack.Stub.Hooks;
using SharedMemory;
// using NewGMHack.CommunicationModel.IPC;
using ZLogger;

namespace NewGMHack.Stub.Services;

public class MainHookService : IHostedService
{
    private readonly IEnumerable<IHookManager> _hookManagers;
    private readonly ILogger<MainHookService> _logger;
    private readonly RpcBuffer rpcBuffer;
    public MainHookService(IEnumerable<IHookManager> hookManagers, ILogger<MainHookService> logger, RpcBuffer rpcBuffer)
    {
        _hookManagers = hookManagers;
        _logger = logger;
        this.rpcBuffer = rpcBuffer;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.ZLogInformation($"Starting hook service...");

        foreach (var manager in _hookManagers)
        {
            try
            {

                _logger.ZLogInformation($"Begin Hook: {manager.GetType().Name}");
                manager.HookAll();
                _logger.ZLogInformation($"Hooked: {manager.GetType().Name}");
            }
            catch (Exception ex)
            {
                _logger.ZLogError($"Failed to hook {manager.GetType().Name}: {ex.Message} | {ex.StackTrace}");
            }
        }

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.ZLogInformation($"Stopping hook service...");

        foreach (var manager in _hookManagers)
        {
            try
            {
                manager.UnHookAll();
                _logger.ZLogInformation($"Unhooked: {manager.GetType().Name}");
            }
            catch (Exception ex)
            {
                _logger.ZLogError($"Failed to unhook {manager.GetType().Name}: {ex.Message}");
            }
        }

        return Task.CompletedTask;
    }
}
