using System.Windows;
using System.Windows.Threading;
using MahApps.Metro.Controls.Dialogs;
// using MessagePipe;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NewGmHack.GUI.Abstracts;
using NewGmHack.GUI.Services;
using NewGmHack.GUI.ViewModels;
using NewGmHack.GUI.Views;
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
                                                       services.AddSingleton<NewMainWindow>();
                                                       services.AddSingleton<IDialogCoordinator>(sp=> DialogCoordinator.Instance);
                                                       services.AddSingleton<MainViewModel>();

                                                       
                                                       services.AddSingleton<NotificationHandler>();
                                                       services.AddSingleton(sp => 
                                                       {
                                                           var handler = sp.GetRequiredService<NotificationHandler>();
                                                           // Combine: "Sdhook" now handles notifications too
                                                           return new RpcBuffer("Sdhook", (msgId, payload) => handler.HandleAsync(msgId, payload.AsMemory()));
                                                       });
                                                       
                                                       services.AddSingleton<IWebServerStatus, WebServerStatus>();
                                                       services.AddSingleton(System.Threading.Channels.Channel.CreateUnbounded<NewGMHack.CommunicationModel.IPC.Responses.RewardNotification>());
                                                       services.AddSingleton(System.Threading.Channels.Channel.CreateUnbounded<WebMessage>());
                                                       services.AddHostedService<WebHostService>();

                                                       services.AddSingleton<RemoteHandler>();
                                                       services.AddSingleton<PersonInfoView>();
                                                       services.AddSingleton<PersonInfoUserControlsViewModel>();
                                                       services.AddSingleton<IHealthCheckHandler>(sp => sp
                                                                                                    .GetRequiredService<MainViewModel>());
                                                       services.AddSingleton<FeaturesView>();
                                                       services.AddSingleton<FeaturesViewModel>();
                                                       services.AddSingleton<IFeatureHandler>(sp => sp
                                                                                                    .GetRequiredService<FeaturesViewModel>());
                                                       services.AddSingleton<IPersonInfoHandler>(sp => sp.GetRequiredService<PersonInfoUserControlsViewModel>());
                                                       //i do this for non blocking
                                                       services.AddSingleton< IHostedService,HealthCheckServices>();
                                                       services.AddSingleton<RoommatesView>();
                                                       services.AddSingleton<RoommatesViewModel>();
                                                       services.AddSingleton<IRoomManager>(sp=> sp.GetRequiredService<RoommatesViewModel>());
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
            try
            {
                await _host.StartAsync();
                var main = _host.Services.GetRequiredService<NewMainWindow>();
                main.Show();
            }
            catch (Exception ex)
            {
                throw; // TODO handle exception
            }
        }

        /// <summary>
        /// Occurs when the application is closing.
        /// </summary>
        private async void OnExit(object sender, ExitEventArgs e)
        {
            try
            {
                await _host.StopAsync();

                _host.Dispose();
            }
            catch (Exception ex)
            {
                throw; // TODO handle exception
            }
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