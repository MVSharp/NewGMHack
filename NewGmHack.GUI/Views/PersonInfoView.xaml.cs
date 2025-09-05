using System.Windows.Controls;
using NewGmHack.GUI.ViewModels;

namespace NewGmHack.GUI.Views
{
    /// <summary>
    /// Interaction logic for PersonInfo.xaml
    /// </summary>
    public partial class PersonInfoView : UserControl
    {
        public PersonInfoUserControlsViewModel ViewModel { get; set; }

        // public PersonInfoView()
        // {
        //     InitializeComponent();
        // }
        public PersonInfoView(PersonInfoUserControlsViewModel _viewModel)
        {
            ViewModel   = _viewModel;
            DataContext = ViewModel;
            InitializeComponent();
        }
    }
}
