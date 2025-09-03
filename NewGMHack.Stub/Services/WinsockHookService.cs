// using MessagePipe;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SharedMemory;
// using NewGMHack.CommunicationModel.IPC;
using ZLogger;

namespace NewGMHack.Stub.Services;

public class WinsockHookService : IHostedService
{
    private readonly WinsockHookManager          _hookManager;
    private readonly ILogger<WinsockHookService> _logger;
    private readonly RpcBuffer                   _slave;

    public WinsockHookService(WinsockHookManager hookManager, ILogger<WinsockHookService> logger, RpcBuffer slave)
    {
        _hookManager = hookManager;
        _logger      = logger;
        _slave       = slave;
    }
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.ZLogInformation($"Starting hook service...");
        _hookManager.HookAll();
        // _logger.ZLogInformation($"send health check");
        // // var r=    await _handler.InvokeAsync(new DynamicOperationRequest() { Operation = "HealthCheck" });
        // while (r.Success == false)
        // {
        //     r=    await _handler.InvokeAsync(new DynamicOperationRequest() { Operation = "HealthCheck" });
        //     _logger.ZLogInformation($"send health check");
        //     await Task.Delay(1000);
        // }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.ZLogInformation($"Stopping hook service...");
        return Task.CompletedTask;
    }
}