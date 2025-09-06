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
using NewGmHack.GUI.ViewModels;

namespace NewGmHack.GUI.Views
{
    /// <summary>
    /// Interaction logic for RoommatesView.xaml
    /// </summary>
    public partial class RoommatesView : UserControl
    {
        public RoommatesViewModel ViewModel { get; set; }
        public RoommatesView(RoommatesViewModel roommatesViewModel)
        {
            InitializeComponent();
            ViewModel   = roommatesViewModel;
            DataContext = ViewModel;
        }
    }
}
