using CommunityToolkit.Mvvm.ComponentModel;

namespace NewGmHack.GUI.ViewModels
{
    public partial class TabUserControlBase : ObservableObject
    {
        [ObservableProperty] private string _header;
        [ObservableProperty] private object _contentViewModel;
    }
}
