using System;
using System.Diagnostics;
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
            
            // Handle window resize for proper scaling (optional zoom reset)
            SizeChanged += OnWindowSizeChanged;
        }

        private async void InitializeWebViewAsync()
        {
            try 
            {
                // Ensure the CoreWebView2 environment is initialized
                await DashboardWebView.EnsureCoreWebView2Async();
                
                // Configure WebView2 settings for better scaling
                var settings = DashboardWebView.CoreWebView2.Settings;
                settings.IsZoomControlEnabled = true;  // Allow zoom
                settings.IsPinchZoomEnabled = true;    // Allow pinch zoom
                
                // Set default zoom to 100% (frontend CSS handles responsive design)
                DashboardWebView.ZoomFactor = 1.0;
                
                // Construct URI. 
                string url = _webStatus.BaseUrl; 
                if (string.IsNullOrEmpty(url)) url = "http://localhost:5000";
                //Process.Start(url);
                DashboardWebView.Source = new Uri(url);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load WebView2: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void OnWindowSizeChanged(object sender, SizeChangedEventArgs e)
        {
            // WebView2 automatically resizes with its container
            // The frontend uses CSS vh/vw and flexbox, so it scales automatically
            // No manual zoom adjustment needed - just ensure zoom stays at 100%
            if (DashboardWebView.CoreWebView2 != null)
            {
                // Keep zoom at 100% - the CSS will handle scaling
                DashboardWebView.ZoomFactor = 1.0;
            }
        }

        /// <summary>
        /// Reload WebView2 (called when frontend is updated)
        /// </summary>
        public async Task ReloadWebViewAsync()
        {
            try
            {
                if (DashboardWebView?.CoreWebView2 != null)
                {
                    // Reload the current page
                    DashboardWebView.CoreWebView2.Reload();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to reload WebView2: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
