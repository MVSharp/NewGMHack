using System.Configuration;
using System.Data;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Threading;
using MessagePack;
// using MessagePipe;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NewGmHack.GUI.ViewModels;
using SharedMemory;

namespace NewGmHack.GUI
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App
    {
        private static readonly IHost _host = Host.CreateDefaultBuilder()
                                                  .ConfigureServices(services =>
                                                   {
                                                       services.AddSingleton<MainWindow>();
                                                       services.AddSingleton<MainViewModel>();
                                                       services.AddSingleton(new RpcBuffer("Sdhook"));
                                                       services.AddSingleton<RemoteHandler>();
                                                       // services.AddMessagePipe()
                                                       //         .AddNamedPipeInterprocess("SdHook",
                                                       //              options =>
                                                       //              {
                                                       //                  options.HostAsServer = true;
                                                       //                  options.InstanceLifetime =
                                                       //                      InstanceLifetime.Singleton;
                                                       //                  options.MessagePackSerializerOptions =
                                                       //                      MessagePackSerializerOptions.Standard;
                                                       //              });
                                                       // .AddNamedPipeInterprocess("SdHook",
                                                       //           options =>
                                                       //           {
                                                       //
                                                       //               options.HostAsServer = true;
                                                       //               options.MessagePackSerializerOptions =
                                                       //                   MessagePackSerializerOptions.Standard;
                                                       //           }
                                                       // );
                                                   }).Build();

        /// <summary>
        /// Gets services.
        /// </summary>
        public static IServiceProvider Services
        {
            get { return _host.Services; }
        }

        /// <summary>
        /// Occurs when the application is loading.
        /// </summary>
        private async void OnStartup(object sender, StartupEventArgs e)
        {
            await _host.StartAsync();
            var main = _host.Services.GetRequiredService<MainWindow>();
            main.Show();
        }

        /// <summary>
        /// Occurs when the application is closing.
        /// </summary>
        private async void OnExit(object sender, ExitEventArgs e)
        {
            await _host.StopAsync();

            _host.Dispose();
        }

        /// <summary>
        /// Occurs when an exception is thrown by an application but not handled.
        /// </summary>
        private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            // For more info see https://docs.microsoft.com/en-us/dotnet/api/system.windows.application.dispatcherunhandledexception?view=windowsdesktop-6.0
        }
    }
}