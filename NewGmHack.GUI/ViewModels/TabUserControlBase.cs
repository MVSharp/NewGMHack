using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;

namespace NewGmHack.GUI.ViewModels
{
    public partial class TabUserControlBase : ObservableObject
    {
        [ObservableProperty] private string _header;
    }
}
