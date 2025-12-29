using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using NewGMHack.CommunicationModel.Models;
using NewGmHack.GUI.Abstracts;
using ObservableCollections;

namespace NewGmHack.GUI.ViewModels
{
    public partial class RoommatesViewModel: TabUserControlBase , IRoomManager
    {
        private ObservableList<Roommate> roommatesList;
        [ObservableProperty] private NotifyCollectionChangedSynchronizedViewList<Roommate> _roommates;

        public RoommatesViewModel()
        {
            ContentViewModel = this;
            Header           = "RoommatesView";
            roommatesList    = [];
            Roommates = roommatesList.ToNotifyCollectionChanged(SynchronizationContextCollectionEventDispatcher
                                                                   .Current);
        }

        /// <inheritdoc />
        public void UpdateRoomList(IEnumerable<Roommate> roommates)
        {
            var newList = roommates as IList<Roommate> ?? roommates.ToList();
            
            // Optimization: If both are empty, do nothing (avoids UI refresh churn)
            if (newList.Count == 0 && this.roommatesList.Count == 0) return;

            // TODO: Implement smarter diffing to avoid Clear/Add if identical
            
            this.roommatesList.Clear();
            this.roommatesList.AddRange(newList);
        }
    }
}
