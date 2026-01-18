// using MessagePipe;

using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NewGMHack.Stub.Hooks;
using SharedMemory;
// using NewGMHack.CommunicationModel.IPC;
using ZLogger;

using NewGMHack.Stub.Logger;

namespace NewGMHack.Stub.Services;

public partial class MainHookService : IHostedService
{

[DllImport("kernelbase.dll", CharSet = CharSet.Unicode, SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)]
public static extern bool GetAppContainerNamedObjectPath( IntPtr token, // Access token (NULL for current process)
                                                          IntPtr appContainerSid, // SID (NULL for current process)
                                                          StringBuilder objectPath, // Buffer to receive path
                                                          ref uint objectPathLength );
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
        _logger.LogStartingHookService();
        uint          length = 512;
        StringBuilder sb     = new StringBuilder((int)length);
        bool          result = GetAppContainerNamedObjectPath(IntPtr.Zero, IntPtr.Zero, sb, ref length);
        if (result)
        {
            _logger.LogContainerPath(sb.ToString());
        }
        else
        {
            _logger.LogContainerPathFailed(Marshal.GetLastWin32Error());
        }
            System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
        foreach (var manager in _hookManagers)
        {
            try
            {

                _logger.LogBeginHook(manager.GetType().Name);
                manager.HookAll();
                _logger.LogHooked(manager.GetType().Name);
            }
            catch (Exception ex)
            {
                _logger.LogHookFailed(manager.GetType().Name, ex.Message, ex.StackTrace);
            }
        }

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogStoppingHookService();

        foreach (var manager in _hookManagers)
        {
            try
            {
                manager.UnHookAll();
                _logger.LogUnhooked(manager.GetType().Name);
            }
            catch (Exception ex)
            {
                _logger.LogUnhookFailed(manager.GetType().Name, ex.Message);
            }
        }

        return Task.CompletedTask;
    }
}

