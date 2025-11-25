using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Channels;
using System.Xml;
using InjectDotnet.NativeHelper.Native;
using Memory;
using MessagePack;
// using MessagePipe;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NewGMHack.Stub.Hooks;
using NewGMHack.Stub.MemoryScanner;
using NewGMHack.Stub.PacketStructs.Recv;
using NewGMHack.Stub.Services;
using Reloaded.Hooks;
using Reloaded.Hooks.Definitions;
using SharedMemory;
using ZLogger;

namespace NewGMHack.Stub
{
    internal static partial class Entry
    {
        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        public static extern int MessageBox(IntPtr hWnd, string lpText, string lpCaption, uint uType);

// [DllImport("kernel32.dll", SetLastError = true)]
// [return: MarshalAs(UnmanagedType.Bool)]
// static extern bool AllocConsole();
        [STAThread]
        public static int Bootstrap(IntPtr argument, int size)
        {
            //AllocConsole();
            //Console.WriteLine("hi");

            var hostBuilder = Host.CreateDefaultBuilder()
                                  .ConfigureLogging(c =>
                                   {
                                       c.AddZLoggerFile("sdlog.txt", options =>
                                       {
                                           options.UsePlainTextFormatter(formatter =>
                                           {
                                               formatter.SetPrefixFormatter($"{0}|{1}|",
                                                                            (in MessageTemplate template,
                                                                             in LogInfo info) =>
                                                                                template.Format(info.Timestamp,
                                                                                    info.LogLevel));
                                               formatter.SetSuffixFormatter($" ({0})",
                                                                            (in MessageTemplate template,
                                                                             in LogInfo info) =>
                                                                                template.Format(info.Category));
                                               formatter.SetExceptionFormatter((writer, ex) =>
                                                                                   Utf8StringInterpolation.Utf8String
                                                                                      .Format(writer, $"{ex.Message}"));
                                           });
                                       });

                                       // c.AddZLoggerLogProcessor((options, provider) =>
                                       // {
                                       //     var batchProvider =
                                       //         provider.GetRequiredService<AsyncIPCLogProcessor>();
                                       //     return batchProvider;
                                       // });
                                   })
                                  .ConfigureServices(services =>
                                   {
                                       services.Configure<HostOptions>(hostOptions =>
                                       {
                                           hostOptions
                                                  .BackgroundServiceExceptionBehavior =
                                               BackgroundServiceExceptionBehavior.Ignore;
                                       });
                                       // services.AddMessagePipe()
                                       //                         .AddNamedPipeInterprocess("SdHook",
                                       //                                   options =>
                                       //                                   {
                                       //                                       options.HostAsServer = false;
                                       //                                       options.InstanceLifetime =
                                       //                                           InstanceLifetime.Singleton;
                                       //                                       options.MessagePackSerializerOptions =
                                       //                                           MessagePackSerializerOptions.Standard;
                                       //                                   });
                                       //services.AddSingleton<AsyncIPCLogProcessor>();
                                       services.AddSingleton<SelfInformation>();
                                       services.AddTransient<Mem>();
                                       services.AddTransient<GmMemory>();
                                       //services.AddSingleton<FullAoBScanner>();
                                       services.AddTransient<IBuffSplitter, BuffSplitter>();
                                       services.AddSingleton<IHostedService, MainHookService>();
                                       services.AddSingleton<IHostedService, EntityScannerService>();
                                       //services.AddHostedService<MainHookService>();
                                       services.AddSingleton(Channel.CreateUnbounded<PacketContext>(
                                                              new UnboundedChannelOptions
                                                                  { SingleReader = false, SingleWriter = true }));

                                       // for (int i = 0; i < 3; i++)
                                       // {
                                           services.AddSingleton<IHostedService, PacketProcessorService>();
                                       // }
                                       services.AddSingleton<OverlayManager>();

                                       services.AddSingleton(Channel.CreateUnbounded<(IntPtr, List<Reborn>)>(
                                                              new UnboundedChannelOptions
                                                                  { SingleReader = true, SingleWriter = false }));
                                       services.AddSingleton<IHostedService, BombServices>();
                                       services.AddSingleton<RemoteHandler>();
                                       services.AddSingleton<PacketDataModifier>();
                                       services.AddSingleton(sp =>
                                       {
                                           var handler = sp.GetRequiredService<RemoteHandler>();
                                           return new RpcBuffer("Sdhook",
                                                                (msgId, payload) =>
                                                                    handler.HandleAsync(msgId, payload.AsMemory()));
                                       });
                                       services.AddSingleton<DirectInputLogicProcessor>();
var packetChannel = Channel.CreateUnbounded<ReadOnlyMemory<byte>>();

services.AddSingleton(packetChannel);
services.AddHostedService<PacketDispatcher>();
                                       services.AddHostedService<AimBotServices>();
                                       services.AddSingleton<InputStateTracker>();
                                       //services.AddSingleton<WinsockHookManager>(); // this is no problem , only once
                                       //services.AddSingleton<WinsockHookManager>(); // this is no problem , only once
                                       //services.AddSingleton<IHookManager, ZoaGraphicsHookManager>();

                                       services.AddSingleton<IHookManager, D3D9HookManager>();
                                       services.AddSingleton<IHookManager,WinsockHookManager>();
services.AddSingleton<IReloadedHooks>(provider =>
{
    return new ReloadedHooks();
});
                                       services.AddSingleton<IHookManager, DirectInputHookManager>();
                                       //services.AddHostedService<PacketProcessorService>();
                                   })
                                  .Build();
            var t = new Thread( async void () =>
            {
                try
                {
                     await hostBuilder.RunAsync();
                }
                catch (Exception ex)
                {
                    MessageBox(0, $"{ex.Message} {ex.StackTrace}", "Error", 0);
                    await hostBuilder.StopAsync();
                }
            });
            //Run the form on STA thread so it can use COM
            t.SetApartmentState(ApartmentState.STA);
            t.Priority = ThreadPriority.Highest;
            t.Start();

            return 0x128;
        }
    }
}