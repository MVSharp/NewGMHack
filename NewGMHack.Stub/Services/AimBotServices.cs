//using Microsoft.Extensions.Hosting;
//using Microsoft.Extensions.Logging;
//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Numerics;
//using System.Runtime.InteropServices;
//using System.Threading;
//using System.Threading.Tasks;
//using ZLinq;
//using ZLogger;

//namespace NewGMHack.Stub.Services
//{
//    internal class AimBotServices : BackgroundService
//    {
//        private readonly SelfInformation _self;
//        private readonly ILogger<AimBotServices> _logger;
//        const int VK_LBUTTON = 0x01;
//        // Config
//        private float _fov = 120f;
//        private float _smooth = 0.75f;
//        private bool _enabled = true;
//        private float _aimSpeed = 1.5f;
//        //private bool _isAiming = false;
//        // Native
//        [DllImport("user32.dll")] private static extern short GetAsyncKeyState(int vKey);
//        [DllImport("user32.dll")] private static extern bool GetCursorPos(out POINT lpPoint);
//        [DllImport("user32.dll")] private static extern void mouse_event(int dwFlags, int dx, int dy, int dwData, UIntPtr dwExtraInfo);

//        private const int MOUSEEVENTF_MOVE = 0x0001;
//        private const int VK_RBUTTON = 0x02;

//        private const int G_BUTTON = 0x47;

//        private const int SHIFT_BUTTON = 0x10;

//        [StructLayout(LayoutKind.Sequential)]
//        private struct POINT { public int X; public int Y; }

//        public AimBotServices(SelfInformation self , ILogger<AimBotServices> logger)
//        {
//            _self = self ?? throw new ArgumentNullException(nameof(self));
//            _logger = logger;
//        }

//        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
//        {
//            // DISABLED: Now using DirectInput aimbot in DirectInputHookManager
//            // This old service was conflicting and causing vibration
//            _logger.ZLogInformation($"AimBotServices disabled - using DirectInput aimbot instead");
//            return;
            
//            /*
//            // High-priority loop: ~1000 Hz
//            var period = TimeSpan.FromMilliseconds(1);
//            Task _aimTask = Task.CompletedTask;
//            while (!stoppingToken.IsCancellationRequested)
//            {
//                try
//                {
                    
//                    if (_self.ClientConfig.Features.IsFeatureEnable(FeatureName.EnableAutoAim) && IsRightMouseDown() &&   _aimTask.IsCompleted)
//                    {

//                        //if (_isAiming) continue;
//                        _aimTask = ProcessAim();
//                        //_isAiming = false;
//                    }
//                }
//                catch (OperationCanceledException) { break; }
//                catch { /* swallow - don't crash host */ }

//                await Task.Delay(10, stoppingToken);
//            }
//            */
//        }

//private readonly SemaphoreSlim _aimLock = new(1, 1);
//        private bool IsRightMouseDown()
//            => (GetAsyncKeyState(VK_RBUTTON) & 0x8000) != 0;
//private async Task ProcessAim()
//{
//    if (!await _aimLock.WaitAsync(0)) return;

//    try
//    {
//        Vector2 crosshair = new(_self.CrossHairX, _self.CrossHairY);
//                //Entity target = GetBestTarget(crosshair);
//                //if (target == null) return;
//                var target = _self.Targets.FirstOrDefault(x => x.IsBest);
//                if (target == null) return;
//                if (target.CurrentHp <= 0 || target.ScreenX <=0 || target.ScreenY <= 0) return;
//        Vector2 targetPos = new(target.ScreenX, target.ScreenY);
//        Vector2 delta = targetPos - crosshair;

//        float distance = delta.Length();
//        if (distance < 5f) return; // Already close

//        // Normalize direction and scale by screen-space distance
//        Vector2 direction = Vector2.Normalize(delta);
//        float speed = Math.Clamp(distance * 0.6f, 5f, 80f); // Fast but controlled
//        Vector2 move = direction * speed;

//        // Optional: clamp per axis to avoid overshooting
//        float maxClamp = Math.Clamp(distance * 0.5f, 10f, 100f);
//        move.X = Math.Clamp(move.X, -maxClamp, maxClamp);
//        move.Y = Math.Clamp(move.Y, -maxClamp, maxClamp);
        
//        mouse_event(MOUSEEVENTF_MOVE, (int)move.X, (int)move.Y, 0, UIntPtr.Zero);
//        await Task.Delay(10);
//    }
//    finally
//    {
//        _aimLock.Release();
//    }
//}
//    }
//}
