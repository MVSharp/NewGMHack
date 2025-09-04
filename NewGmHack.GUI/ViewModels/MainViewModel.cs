using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using InjectDotnet;
using NewGMHack.CommunicationModel.IPC.Requests;
using NewGmHack.GUI.Abstracts;
using NewGMHack.Stub;
using ObservableCollections;
using SharedMemory;

// using MessagePipe;
// using NewGMHack.CommunicationModel.IPC;

namespace NewGmHack.GUI.ViewModels
{
    public partial class MainViewModel : ObservableObject , IHealthCheckHandler
    {
        [ObservableProperty] private bool _isConnected = false;
        private ObservableList<GMHackFeatures> featuresList;
        [ObservableProperty] private NotifyCollectionChangedSynchronizedViewList<GMHackFeatures>           _features;
        [ObservableProperty] private bool                                                            _isRandomLocation  = false;
        ObservableList<TabUserControlBase>                                                           tabslist;
        [ObservableProperty] private NotifyCollectionChangedSynchronizedViewList<TabUserControlBase> _tabs;
        private readonly             RemoteHandler _handler;
        public MainViewModel(PersonInfoUserControlsViewModel PersonViewModel, RemoteHandler master)
        {
            featuresList = new();
            _features = featuresList.ToNotifyCollectionChanged(SynchronizationContextCollectionEventDispatcher.Current);
            tabslist     = new();
            tabslist.AddRange([PersonViewModel]);
            Tabs = tabslist.ToNotifyCollectionChanged(SynchronizationContextCollectionEventDispatcher.Current);
            _handler = master;
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
            }

            else
            {
                Console.WriteLine("fucked , it failed,we r fucked up");
            }

        }

        /// <inheritdoc />
        public async Task SetHealthStatus(bool isConnected)
        {
            if (!isConnected)
            {
                featuresList.Clear();
            }
            else
            {

                featuresList.Clear();
             var features =await _handler.GetFeatures();
             featuresList.AddRange(features.Select(c=>new GMHackFeatures(c.Name,c.IsEnabled, async (gmHackFeatures) =>
             {
                 if (featuresList.Contains(gmHackFeatures))
                 {
                     await _handler.SetFeatureEnable(new FeatureChangeRequests
                     {
                         FeatureName =  gmHackFeatures.Name,
                         IsEnabled   = gmHackFeatures.IsEnabled
                     });
                 }
             })));
            }
            IsConnected = isConnected;
        }

//#pragma warning disable
        [RelayCommand]
        public async Task OnLoaded()
        {

        }
    }
}