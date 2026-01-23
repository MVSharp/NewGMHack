using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Channels;
using System.Xml;
using InjectDotnet.NativeHelper.Native;
using MessagePack;
// using MessagePipe;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NewGMHack.Stub.Hooks;
using NewGMHack.Stub.MemoryScanner;
using NewGMHack.Stub.PacketStructs.Recv;
using NewGMHack.Stub.Services;
using NewGMHack.Stub.Services.Scanning;
using NewGMHack.Stub.Services.Caching;
using NewGMHack.Stub.Models;
using NewGMHack.CommunicationModel.Models;
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
                                       //services.AddTransient<Mem>();
                                       
                                       // SQLite entity caches for GmMemory
                                       services.AddSingleton<IEntityCache<MachineBaseInfo>, SqliteEntityCache<MachineBaseInfo>>();
                                       services.AddSingleton<IEntityCache<SkillBaseInfo>, SqliteEntityCache<SkillBaseInfo>>();
                                       services.AddSingleton<IEntityCache<WeaponBaseInfo>, SqliteEntityCache<WeaponBaseInfo>>();
                                       
                                       services.AddTransient<GmMemory>();
                                       //services.AddSingleton<FullAoBScanner>();
                                       services.AddTransient<IBuffSplitter, BuffSplitter>();
                                       services.AddSingleton<IPacketAccumulator, PacketAccumulator>();
                                       services.AddSingleton<IHostedService, MainHookService>();
                                       services.AddSingleton<IMemoryScanner, SimdMemoryScanner>();
                                       services.AddSingleton<IHostedService, EntityScannerService>();
                                       services.AddSingleton<IHostedService, BombHistoryServices>(); //BUG it locked my services
                                       //services.AddHostedService<MainHookService>();
                                       services.AddSingleton(Channel.CreateBounded<PacketContext>(
                                                              new BoundedChannelOptions(1000)
                                                                  { SingleReader = false, SingleWriter = true, FullMode = BoundedChannelFullMode.DropWrite }));

                                       //for (int i = 0; i < 3; i++)
                                       //{
                                           services.AddSingleton<IHostedService, PacketProcessorService>();
                                       //}
                                       services.AddSingleton<OverlayManager>();

                                       services.AddSingleton(Channel.CreateBounded<(IntPtr, List<Reborn>)>(
                                                              new BoundedChannelOptions(1000)
                                                                  { SingleReader = true, SingleWriter = false, FullMode = BoundedChannelFullMode.DropWrite }));
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
                                       var packetChannel = Channel.CreateBounded<ReadOnlyMemory<byte>>(new BoundedChannelOptions(2000)
                                       {
                                           FullMode = BoundedChannelFullMode.DropWrite,
                                           SingleReader = true,
                                           SingleWriter = false
                                       });
                                       services.AddSingleton(packetChannel);
                                       services.AddHostedService<PacketDispatcher>();
                                       //services.AddHostedService<AimBotServices>();
                                       services.AddSingleton<InputStateTracker>();
                                       //services.AddSingleton<WinsockHookManager>(); // this is no problem , only once
                                       //services.AddSingleton<WinsockHookManager>(); // this is no problem , only once
                                       //services.AddSingleton<IHookManager, ZoaGraphicsHookManager>();
                                       services.AddHostedService<StealthService>();
                                       //services.AddHostedService<PebMasquerader>();
                                       services.AddHostedService<HandleCleanerService>();


                                       services.AddSingleton<IHookManager, D3D9HookManager>();
                                       services.AddSingleton<IHookManager,WinsockHookManager>();
services.AddSingleton<IReloadedHooks>(provider =>
{
    return new ReloadedHooks();
});
                                       services.AddSingleton<IHookManager, DirectInputHookManager>();
                                       //services.AddSingleton<IHookManager, CreateMutexHookManager>();
                                       //services.AddHostedService<PacketProcessorService>();
                                       services.AddSingleton(Channel.CreateBounded<RewardEvent>(new BoundedChannelOptions(100)
                                       {
                                           FullMode = BoundedChannelFullMode.DropWrite,
                                           SingleReader = true,
                                           SingleWriter = false
                                       }));
                                       
                                       // Battle logging channel and service
                                       services.AddSingleton(Channel.CreateBounded<BattleLogEvent>(new BoundedChannelOptions(500)
                                       {
                                           FullMode = BoundedChannelFullMode.DropWrite,
                                           SingleReader = true,
                                           SingleWriter = false
                                       }));
                                       services.AddHostedService<BattleLoggerService>();
                                       
                                       services.AddSingleton<IpcNotificationService>();
                                       services.AddHostedService<RewardPersisterService>();
                                   })
                                  .Build();

            var selfInfo = hostBuilder.Services.GetRequiredService<SelfInformation>();
            selfInfo.ProcessId = Process.GetCurrentProcess().Id;

            var t = new Thread( async void () =>
            {
                // Global Exception Handlers to catch "Bombs"
                AppDomain.CurrentDomain.UnhandledException += (sender, e) => 
                {
                    try 
                    {
                        var ex = e.ExceptionObject as Exception;
                        File.AppendAllText("sdlog.txt", $"[CRITICAL] Unhandled Exception: {ex?.Message} \nStack: {ex?.StackTrace}\n");
                    }
                    catch {}
                };

                TaskScheduler.UnobservedTaskException += (sender, e) => 
                {
                    try
                    {
                        File.AppendAllText("sdlog.txt", $"[CRITICAL] Unobserved Task Exception: {e.Exception.Message} \nStack: {e.Exception.StackTrace}\n");
                        e.SetObserved(); 
                    }
                    catch {}
                };

                try
                {
                     await hostBuilder.RunAsync();
                }
                catch (Exception ex)
                {
                    MessageBox(0, $"{ex.Message} {ex.StackTrace}", "Error", 0);
                    // Log to file as well just in case MessageBox is suppressed or fails
                     try { File.AppendAllText("sdlog.txt", $"[CRITICAL] Host Run Error: {ex.Message} \nStack: {ex.StackTrace}\n"); } catch {}
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