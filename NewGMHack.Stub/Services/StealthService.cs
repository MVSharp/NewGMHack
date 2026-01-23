using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace NewGMHack.Stub.Services
{
    public class StealthService : BackgroundService
    {
        private readonly ILogger<StealthService> _logger;

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        static extern bool SetWindowText(IntPtr hwnd, string lpString);

        [DllImport("user32.dll", SetLastError = true)]
        static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("user32.dll")]
        static extern bool EnumWindows(EnumWindowsProc enumProc, IntPtr lParam);

        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        public StealthService(ILogger<StealthService> logger)
        {
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("[Stealth] Service Started.");

            // Wait a bit for the main window to be created by the game logic
            // Retrying periodically in case the game restores the title or creates new windows
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    RandomizeWindowTitle();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[Stealth] Failed to randomize window title");
                }

                // Check every 5 seconds
                await Task.Delay(5000, stoppingToken);
            }
        }

        private void RandomizeWindowTitle()
        {
            uint currentPid = (uint)Process.GetCurrentProcess().Id;
            // Generate a random convincing looking title or just a random hash
            // Using a unique ID ensures no two instances have the same name
            string newTitle = $"Client_{Guid.NewGuid().ToString("N").Substring(0, 8)}";

            EnumWindows((hwnd, lParam) =>
            {
                GetWindowThreadProcessId(hwnd, out uint pid);
                if (pid == currentPid)
                {
                    // Only change if it's visible or main window? 
                    // For now changes all windows belonging to this process to be safe.
                    // We can check if it's already changed to avoid spamming SetWindowText, 
                    // but SetWindowText is cheap.
                    
                    // We could also get the current text to see if it needs changing
                    // But forcing it ensures we overwrite if the game resets it.
                    SetWindowText(hwnd, newTitle);
                }
                return true;
            }, IntPtr.Zero);
        }
    }
}
