using CommunityToolkit.Mvvm.ComponentModel;
using NewGmHack.GUI.Abstracts;
using NewGMHack.Stub;

namespace NewGmHack.GUI.ViewModels
{
    public partial class PersonInfoUserControlsViewModel : TabUserControlBase, IPersonInfoHandler
    {
        [ObservableProperty] 
        private Info _personInfo;

        public PersonInfoUserControlsViewModel()
        {
            this.Header      = "Personal Info";
            PersonInfo       = new Info();
            ContentViewModel = this;
        }

        /// <inheritdoc />
        public void SetInfo(Info info)
        {
            this.PersonInfo = info;
            // PersonInfo = new InfoViewModel
            // {
            // PersonInfo.PersonId = info.PersonId;
            // PersonInfo.GundamId = info.GundamId;
            // PersonInfo.Weapon1  = info.Weapon1;
            // PersonInfo.Weapon2  = info.Weapon2;
            // PersonInfo.Weapon3  = info.Weapon3;
            // };
        }
    }

    public partial class InfoViewModel //: ObservableObject
    {
        // [ObservableProperty] private uint _personId;
        // [ObservableProperty] private uint _gundamId;
        // [ObservableProperty] private uint _weapon1;
        // [ObservableProperty] private uint _weapon2;
        // [ObservableProperty] private uint _weapon3;
    }
}