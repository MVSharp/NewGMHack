using System;
using System.Threading.Tasks;
using System.Windows;
using MahApps.Metro.Controls;
using Microsoft.Web.WebView2.Core;
using NewGmHack.GUI.Services;

namespace NewGmHack.GUI
{
    public partial class NewMainWindow : MetroWindow
    {
        private readonly IWebServerStatus _webStatus;

        public NewMainWindow(IWebServerStatus webStatus)
        {
            InitializeComponent();
            _webStatus = webStatus;
            
            InitializeWebViewAsync();
        }

        private async void InitializeWebViewAsync()
        {
            try 
            {
                // Ensure the CoreWebView2 environment is initialized
                await DashboardWebView.EnsureCoreWebView2Async();

                // Construct URI. 
                string url = _webStatus.BaseUrl; 
                if (string.IsNullOrEmpty(url)) url = "http://localhost:5000";

                DashboardWebView.Source = new Uri(url);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load WebView2: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
