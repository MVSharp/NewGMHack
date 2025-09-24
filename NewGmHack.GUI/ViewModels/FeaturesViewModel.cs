using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NewGmHack.GUI.Abstracts;
using NewGMHack.CommunicationModel.IPC.Requests;
using ObservableCollections;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ZLinq;

namespace NewGmHack.GUI.ViewModels
{
    public interface IFeatureHandler
    {
        Task Refresh();
        Task EndFetch();
        void Clear();
    }
    public partial class FeaturesViewModel : TabUserControlBase ,IFeatureHandler
    {

        private                      ObservableList<GMHackFeatures> featuresList;
        [ObservableProperty] private NotifyCollectionChangedSynchronizedViewList<GMHackFeatures> _features;
        private readonly             RemoteHandler _handler;
        private  CancellationTokenSource cancellationTokenSource;
        public FeaturesViewModel(RemoteHandler handler)
        {
            _handler = handler; 
            featuresList = [];
            _features = featuresList.ToNotifyCollectionChanged(SynchronizationContextCollectionEventDispatcher.Current);
            cancellationTokenSource = new();
        }


        [RelayCommand]
        public async Task Refresh()
        {
            //while (!cancellationTokenSource.IsCancellationRequested)
            //{

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
                await Task.Delay(100).ConfigureAwait(false);
            //}
        }

        public async Task EndFetch()
        {
            await cancellationTokenSource.CancelAsync();
            featuresList.Clear();
        }

        public void Clear()
        {
            featuresList.Clear();
        }
    }
}
