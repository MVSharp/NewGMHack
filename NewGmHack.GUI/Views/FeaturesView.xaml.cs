using NewGmHack.GUI.ViewModels;
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

namespace NewGmHack.GUI.Views
{
    /// <summary>
    /// Interaction logic for FeaturesView.xaml
    /// </summary>
    public partial class FeaturesView : UserControl
    {
        public FeaturesViewModel Model { get; }
        public FeaturesView( FeaturesViewModel vm)
        {
            InitializeComponent();
            Model = vm;
            DataContext = Model;
        }
    }
}
