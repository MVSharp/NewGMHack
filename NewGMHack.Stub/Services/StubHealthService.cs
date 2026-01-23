using System.Diagnostics;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace NewGMHack.Stub.Services;

public class StubHealthService : BackgroundService
{
    private readonly SelfInformation _selfInformation;
    private readonly ILogger<StubHealthService> _logger;

    public StubHealthService(SelfInformation selfInformation, ILogger<StubHealthService> logger)
    {
        _selfInformation = selfInformation;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (_selfInformation.ProcessId != 0)
                {
                    try
                    {
                        // Check if process exists by trying to get it
                        // If process doesn't exist, GetProcessById throws ArgumentException
                        var process = Process.GetProcessById(_selfInformation.ProcessId);
                        
                        // Double check HasExited property just in case
                        if (process.HasExited)
                        {
                            _logger.LogWarning($"Process {_selfInformation.ProcessId} has exited. Application exiting.");
                            Environment.Exit(0);
                        }
                    }
                    catch (ArgumentException)
                    {
                        // Process ID found but not running
                        _logger.LogWarning($"Process {_selfInformation.ProcessId} not found. Application exiting.");
                        Environment.Exit(0);
                    }
                    catch (Exception ex)
                    {
                        // Permission denied or other error - might not mean it doesn't exist, but typically we should be able to query basic info
                        // However, to be safe, we only exit on ArgumentException (not found) or HasExited.
                        // AccessDenied might happen if the other process is elevated and we are not, but existence check usually works.
                        _logger.LogError(ex, $"Error checking process {_selfInformation.ProcessId}.");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in StubHealthService execution loop.");
            }

            await Task.Delay(10000, stoppingToken);
        }
    }
}
