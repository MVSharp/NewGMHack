using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using NewGmHack.GUI.Abstracts;
using NewGMHack.Stub;

namespace NewGmHack.GUI.ViewModels
{
    public partial class PersonInfoUserControlsViewModel : TabUserControlBase , IPersonInfoHandler
    {
        [ObservableProperty] private Info _personInfo;
        public PersonInfoUserControlsViewModel()
        {
            this.Header = "Personal Info";
            _personInfo = new Info();
        }

        /// <inheritdoc />
        public void SetInfo(Info info)
        {
            PersonInfo = info;
        }
    }
}
