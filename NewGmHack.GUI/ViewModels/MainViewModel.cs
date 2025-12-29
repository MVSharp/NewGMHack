using System.Diagnostics;
using System.Windows.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using InjectDotnet;
using MahApps.Metro.Controls.Dialogs;
using Microsoft.Extensions.DependencyInjection;
using NewGMHack.CommunicationModel.IPC.Requests;
using NewGmHack.GUI.Abstracts;
using NewGmHack.GUI.Services;
using NewGmHack.GUI.Views;
using ObservableCollections;
using ZLinq;
using System.Text;

// using MessagePipe;
// using NewGMHack.CommunicationModel.IPC;

namespace NewGmHack.GUI.ViewModels
{
    public partial class MainViewModel : ObservableObject, IHealthCheckHandler
    {
        [ObservableProperty] private bool _isConnected = false;
        [ObservableProperty] private bool _isRandomLocation = false;
        ObservableList<TabItem> tabslist;
        [ObservableProperty] private NotifyCollectionChangedSynchronizedViewList<TabItem> _tabs;
        private readonly             RemoteHandler _handler;
        private readonly             IDialogCoordinator _dialogCoordinator;
        private readonly IFeatureHandler _featureHandler;
        private readonly PersonInfoUserControlsViewModel _personInfoVm;
        private readonly IWebServerStatus _webServerStatus;

        public MainViewModel( RemoteHandler master,
                              IFeatureHandler featureHandler,
                              PersonInfoUserControlsViewModel personInfoVm,
                              IWebServerStatus webServerStatus,
                              IDialogCoordinator              dialogCoordinator)
        {
            tabslist = [];
            Tabs = tabslist.ToNotifyCollectionChanged(SynchronizationContextCollectionEventDispatcher.Current);
            AddTab<PersonInfoView, PersonInfoUserControlsViewModel>("Person Info");
            AddTab<RoommatesView, RoommatesViewModel>("RoommatesView");
            AddTab<FeaturesView, FeaturesViewModel>("Features");
            _handler           = master;
            _dialogCoordinator = dialogCoordinator;
            _featureHandler = featureHandler;
            _personInfoVm = personInfoVm;
            _webServerStatus = webServerStatus;
        }

        [RelayCommand]
        private void OpenWebReport()
        {
            try
            {
                var pid = _personInfoVm.PersonInfo?.PersonId ?? 0;
                var url = _webServerStatus.BaseUrl; // Dynamic URL
                
                if (pid != 0)
                {
                    url += $"?pid={pid}";
                }

                Process.Start(new ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to open browser: {ex.Message}");
            }
        }

        [RelayCommand]
        private async Task Deattach()
        {
            await _handler.DeattachRequest();
        }

        [RelayCommand]
        private async Task Inject()
        {
            try
            {
                string processName = Encoding.UTF8.GetString(Convert.FromBase64String("R09ubGluZQ=="));
                var target = Process.GetProcessesByName(processName).FirstOrDefault();
                while (target == null)
                {
                    target = Process.GetProcessesByName(processName).FirstOrDefault();
                    Console.WriteLine("Waiting inject");
                    await Task.Delay(2000);
                }

                var t = await Task.Run(() =>
                {
                    var arg = new Argument
                    {
                        Title = target.WriteMemory("Injected Form"),
                        Text =
                            target.WriteMemory($"This form has been injected into {target.MainModule?.FileName} and is running in its memory space"),
                        //Picture = target.WriteMemory(picBytes),
                        //pic_sz = picBytes.Length
                    };
                    return target.Inject(
                                          "NewGMHack.Stub.runtimeconfig.json",
                                          "NewGMHack.Stub.dll",
                                          "NewGMHack.Stub.Entry, NewGMHack.Stub, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null",
                                          "Bootstrap",
                                          arg, true);
                });

                if (t == 0x128)
                {
                    Console.WriteLine("injected sucessfully");
                    while (!IsConnected)
                    {
                        await Task.Delay(1000);
                    }
                    await _featureHandler.Refresh();
                    //await _featureHandler.BeginFetch().ConfigureAwait(false);
                    await _dialogCoordinator.ShowMessageAsync(this, "Injected", "Injected");
                }

                else
                {
                    Console.WriteLine("fucked , it failed,we r fucked up");
                    await _dialogCoordinator.ShowMessageAsync(this, "failed to inject", "failed to inject");
                }

            }
            catch(Exception ex)
            {

                await _dialogCoordinator.ShowMessageAsync(this, "failed to inject", $"{ex.Message}|{ex.StackTrace}");
            }
   }

        /// <inheritdoc />
        public void SetHealthStatus(bool isConnected)
        {
            if (!isConnected)
            {
                _featureHandler.Clear();
            }

            IsConnected = isConnected;
        }

        public void AddTab<TView, TViewModel>(string header)
            where TView : UserControl
            where TViewModel : class
        {
            var view      = App.Services.GetRequiredService<TView>();
            var viewModel = App.Services.GetRequiredService<TViewModel>();
            view.DataContext = viewModel;

            var tab = new TabItem
            {
                Header  = header,
                Content = view
            };

            tabslist.Add(tab);
        }

//#pragma warning disable
        [RelayCommand]
        public Task OnLoaded()
        {
            return Task.CompletedTask;
        }
    }
}