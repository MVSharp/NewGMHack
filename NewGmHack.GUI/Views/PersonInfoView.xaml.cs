using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using CommunityToolkit.Mvvm.ComponentModel;
using NewGmHack.GUI.ViewModels;

namespace NewGmHack.GUI.Views
{
    /// <summary>
    /// Interaction logic for PersonInfo.xaml
    /// </summary>
    public partial class PersonInfoView : UserControl
    {
        public PersonInfoUserControlsViewModel ViewModel { get; set; }

        public PersonInfoView()
        {
            
        }
        public PersonInfoView(PersonInfoUserControlsViewModel _viewModel)
        {
            ViewModel   = _viewModel;
            DataContext = ViewModel;
            InitializeComponent();
        }
    }
}
