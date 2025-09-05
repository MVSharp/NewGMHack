using System.Diagnostics;
using System.Windows.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using InjectDotnet;
using MahApps.Metro.Controls.Dialogs;
using Microsoft.Extensions.DependencyInjection;
using NewGMHack.CommunicationModel.IPC.Requests;
using NewGmHack.GUI.Abstracts;
using NewGmHack.GUI.Views;
using ObservableCollections;
using ZLinq;

// using MessagePipe;
// using NewGMHack.CommunicationModel.IPC;

namespace NewGmHack.GUI.ViewModels
{
    public partial class MainViewModel : ObservableObject, IHealthCheckHandler
    {
        [ObservableProperty] private bool _isConnected = false;
        private                      ObservableList<GMHackFeatures> featuresList;
        [ObservableProperty] private NotifyCollectionChangedSynchronizedViewList<GMHackFeatures> _features;
        [ObservableProperty] private bool _isRandomLocation = false;
        ObservableList<TabItem> tabslist;
        [ObservableProperty] private NotifyCollectionChangedSynchronizedViewList<TabItem> _tabs;
        private readonly             RemoteHandler _handler;
        private readonly             IDialogCoordinator _dialogCoordinator;

        public MainViewModel( RemoteHandler master,
                             IDialogCoordinator              dialogCoordinator)
        {
            featuresList = new();
            _features = featuresList.ToNotifyCollectionChanged(SynchronizationContextCollectionEventDispatcher.Current);
            tabslist = new();
            Tabs = tabslist.ToNotifyCollectionChanged(SynchronizationContextCollectionEventDispatcher.Current);
            AddTab<PersonInfoView, PersonInfoUserControlsViewModel>("Person Info");
            _handler           = master;
            _dialogCoordinator = dialogCoordinator;
        }

        [RelayCommand]
        private async Task Deattach()
        {
            await _handler.DeattachRequest();
        }

        [RelayCommand]
        private async Task Inject()
        {
            var target = Process.GetProcessesByName("GOnline").FirstOrDefault();
            while (target == null)
            {
                target = Process.GetProcessesByName("GOnline").FirstOrDefault();
                Console.WriteLine("Waiting inject");
                await Task.Delay(2000);
            }

            var arg = new Argument
            {
                Title = target.WriteMemory("Injected Form"),
                Text =
                    target.WriteMemory($"This form has been injected into {target.MainModule?.FileName} and is running in its memory space"),
                //Picture = target.WriteMemory(picBytes),
                //pic_sz = picBytes.Length
            };
            var t = target.Inject(
                                  "NewGMHack.Stub.runtimeconfig.json",
                                  "NewGMHack.Stub.dll",
                                  "NewGMHack.Stub.Entry, NewGMHack.Stub, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null",
                                  "Bootstrap",
                                  arg, true);

            if (t == 0x128)
            {
                Console.WriteLine("injected sucessfully");
                while (!IsConnected)
                {
                    await Task.Delay(1000);
                }

                featuresList.Clear();

                var features = await _handler.GetFeatures();
                featuresList.AddRange(features.AsValueEnumerable().Select(c => new GMHackFeatures(c.Name, c.IsEnabled,
                                                                              async (gmHackFeatures) =>
                                                                              {
                                                                                  if (featuresList
                                                                                  .Contains(gmHackFeatures))
                                                                                  {
                                                                                      await _handler
                                                                                         .SetFeatureEnable(new
                                                                                              FeatureChangeRequests
                                                                                              {
                                                                                                  FeatureName =
                                                                                                      gmHackFeatures
                                                                                                         .Name,
                                                                                                  IsEnabled =
                                                                                                      gmHackFeatures
                                                                                                         .IsEnabled
                                                                                              });
                                                                                  }
                                                                              })).ToArray());
                await _dialogCoordinator.ShowMessageAsync(this, "Injected", "Injected");
            }

            else
            {
                Console.WriteLine("fucked , it failed,we r fucked up");
                await _dialogCoordinator.ShowMessageAsync(this, "failed to inject", "failed to inject");
            }
        }

        /// <inheritdoc />
        public void SetHealthStatus(bool isConnected)
        {
            if (!isConnected)
            {
                featuresList.Clear();
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
        public async Task OnLoaded()
        {
        }
    }
}