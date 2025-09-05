using MahApps.Metro.Controls;
using NewGmHack.GUI.ViewModels;

namespace NewGmHack.GUI
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : MetroWindow
    {
        public MainViewModel ViewModel { get; set; }

        public MainWindow(MainViewModel model)
        {
            ViewModel = model;
            DataContext = ViewModel;
            InitializeComponent();
        }
    }
}