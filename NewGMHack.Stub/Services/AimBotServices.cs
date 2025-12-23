using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using ZLinq;
using ZLogger;

namespace NewGMHack.Stub.Services
{
    internal class AimBotServices : BackgroundService
    {
        private readonly SelfInformation _self;
        private readonly ILogger<AimBotServices> _logger;
        const int VK_LBUTTON = 0x01;
        // Config
        private float _fov = 120f;
        private float _smooth = 0.75f;
        private bool _enabled = true;
        private float _aimSpeed = 1.5f;
        //private bool _isAiming = false;
        // Native
        [DllImport("user32.dll")] private static extern short GetAsyncKeyState(int vKey);
        [DllImport("user32.dll")] private static extern bool GetCursorPos(out POINT lpPoint);
        [DllImport("user32.dll")] private static extern void mouse_event(int dwFlags, int dx, int dy, int dwData, UIntPtr dwExtraInfo);

        private const int MOUSEEVENTF_MOVE = 0x0001;
        private const int VK_RBUTTON = 0x02;

        private const int G_BUTTON = 0x47;

        private const int SHIFT_BUTTON = 0x10;

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT { public int X; public int Y; }

        public AimBotServices(SelfInformation self , ILogger<AimBotServices> logger)
        {
            _self = self ?? throw new ArgumentNullException(nameof(self));
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // High-priority loop: ~1000 Hz
            var period = TimeSpan.FromMilliseconds(1);
            Task _aimTask = Task.CompletedTask;
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    if (IsRightMouseDown() &&   _aimTask.IsCompleted)
                    {

                        //if (_isAiming) continue;
                        _aimTask = ProcessAim();
                        //_isAiming = false;
                    }
                }
                catch (OperationCanceledException) { break; }
                catch { /* swallow - don't crash host */ }

                await Task.Delay(10, stoppingToken);
            }
        }

private readonly SemaphoreSlim _aimLock = new(1, 1);
        private bool IsRightMouseDown()
            => (GetAsyncKeyState(VK_RBUTTON) & 0x8000) != 0;
//        private int GetBest(Vector2 center)
//        {
//            //Entity best = null;
//            float bestDist = float.MaxValue;
//            int i = 0;
//            int best = -1;
//            foreach (var entity in _self.Targets)
//            {
//                if (entity.CurrentHp <= 0 || entity.MaxHp <= 0) continue;
//                if (entity.ScreenX <= 0 || entity.ScreenY <= 0) continue;

//                float dist = Vector2.Distance(center, new Vector2(entity.ScreenX, entity.ScreenY));
//                if (dist < bestDist)
//                {
//                    bestDist = dist;
//                    best = i;
//                    //best = entity;
//                }
//                i++;
//            }
//            //return i;
//            return best;
//        }

//private Entity GetBestTarget(Vector2 crosshair)
//{
//    Entity best = null;
//    float bestDist = float.MaxValue;

//    foreach (var t in _self.Targets)
//    {
//        if (t.CurrentHp <= 0 || t.ScreenX <= 0 || t.ScreenY <= 0) continue;

//        float dist = Vector2.Distance(crosshair, new Vector2(t.ScreenX, t.ScreenY));
//                if (dist > _self.AimRadius) continue;
//        if (dist < bestDist)
//        {
//            bestDist = dist;
//            best = t;
//        }
//    }

//    return best;
//}

private async Task ProcessAim()
{
    if (!await _aimLock.WaitAsync(0)) return;

    try
    {
        Vector2 crosshair = new(_self.CrossHairX, _self.CrossHairY);
                //Entity target = GetBestTarget(crosshair);
                //if (target == null) return;
                var target = _self.Targets.FirstOrDefault(x => x.IsBest);
                if (target == null) return;
                if (target.CurrentHp <= 0 || target.ScreenX <=0 || target.ScreenY <= 0) return;
        Vector2 targetPos = new(target.ScreenX, target.ScreenY);
        Vector2 delta = targetPos - crosshair;

        float distance = delta.Length();
        if (distance < 5f) return; // Already close

        // Normalize direction and scale by screen-space distance
        Vector2 direction = Vector2.Normalize(delta);
        float speed = Math.Clamp(distance * 0.6f, 5f, 80f); // Fast but controlled
        Vector2 move = direction * speed;

        // Optional: clamp per axis to avoid overshooting
        float maxClamp = Math.Clamp(distance * 0.5f, 10f, 100f);
        move.X = Math.Clamp(move.X, -maxClamp, maxClamp);
        move.Y = Math.Clamp(move.Y, -maxClamp, maxClamp);
        
        mouse_event(MOUSEEVENTF_MOVE, (int)move.X, (int)move.Y, 0, UIntPtr.Zero);
        await Task.Delay(10);
    }
    finally
    {
        _aimLock.Release();
    }
}
//private async Task ProcessAim()
//{
//    if (!await _aimLock.WaitAsync(0)) return;

//    try
//    {
//        Vector2 crosshair = new(_self.CrossHairX, _self.CrossHairY);
//        Entity target = GetBestTarget(crosshair);
//        if (target == null) return;

//        Vector2 targetPos = new(target.ScreenX, target.ScreenY);
//        Vector2 delta = targetPos - crosshair;

//        float distance = delta.Length();
//        if (distance < 5f) return; // Stop if already close

//        // Normalize direction and scale by speed
//        Vector2 direction = Vector2.Normalize(delta);
//        float speed = distance > 100f ? 80f : Math.Clamp(distance * 0.6f, 5f, 50f);
//        Vector2 move = direction * speed;

//        mouse_event(MOUSEEVENTF_MOVE, (int)move.X, (int)move.Y, 0, UIntPtr.Zero);
//        await Task.Delay(10);
//    }
//    finally
//    {
//        _aimLock.Release();
//    }
//}
//private async Task ProcessAim()
//{
//    if (!await _aimLock.WaitAsync(0)) return;

//    try
//    {
//        if (_self.Targets == null || _self.Targets.Count == 0) return;

//        Vector2 center = new(_self.CrossHairX, _self.CrossHairY);
//        int bestIndex = GetBest(center);
//        if (bestIndex == -1) return;

//        var best = _self.Targets[bestIndex];
//        Vector2 target = new(best.ScreenX, best.ScreenY);

//        // Snap if very close
//        if (Vector2.Distance(center, target) < 5f)
//        {
//            float snapX = target.X - center.X;
//            float snapY = target.Y - center.Y;
//            mouse_event(MOUSEEVENTF_MOVE, (int)snapX, (int)snapY, 0, UIntPtr.Zero);
//            return;
//        }

//        // Fast Lerp
//        Vector2 smoothed = Vector2.Lerp(center, target, 0.75f); // Very fast interpolation
//        float deltaX = smoothed.X - center.X;
//        float deltaY = smoothed.Y - center.Y;

//                deltaX = Math.Clamp(deltaX, -30, 30);
//                deltaY = Math.Clamp(deltaY, -30, 30);

//                if (Math.Abs(deltaX) < 1) deltaX = Math.Sign(deltaX);
//        if (Math.Abs(deltaY) < 1) deltaY = Math.Sign(deltaY);

//        mouse_event(MOUSEEVENTF_MOVE, (int)deltaX, (int)deltaY, 0, UIntPtr.Zero);
//        await Task.Delay(5); // Ultra-fast response
//    }
//    finally
//    {
//        _aimLock.Release();
//    }
//}
//private async Task ProcessAim()
//{
//    if (!await _aimLock.WaitAsync(0)) return;

//    try
//    {
//        if (_self.Targets == null || _self.Targets.Count == 0) return;

//        int centerX = _self.CrossHairX;
//        int centerY = _self.CrossHairY;
//        Vector2 center = new(centerX, centerY);

//        int bestIndex = GetBest(center);
//        if (bestIndex == -1) return;

//        var best = _self.Targets[bestIndex];
//        Vector2 target = new(best.ScreenX, best.ScreenY);

//        int step = 0;
//        while (step < 50)
//        {
//            bestIndex = GetBest(center);
//            if (bestIndex == -1) break;

//            best = _self.Targets[bestIndex];
//            if (best.CurrentHp <= 0) break;

//            target = new(best.ScreenX, best.ScreenY);

//            // Snap if very close
//            if (Vector2.Distance(center, target) < 5f)
//            {
//                center = target;
//            }
//            else
//            {
//                center = Vector2.Lerp(center, target, _smooth); // Smooth interpolation
//            }

//            float deltaX = center.X - _self.CrossHairX;
//            float deltaY = center.Y - _self.CrossHairY;

//            deltaX = Math.Clamp(deltaX, -5, 5);
//            deltaY = Math.Clamp(deltaY, -5, 5);

//            if (Math.Abs(deltaX) < 1) deltaX = Math.Sign(deltaX);
//            if (Math.Abs(deltaY) < 1) deltaY = Math.Sign(deltaY);

//            mouse_event(MOUSEEVENTF_MOVE, (int)deltaX, (int)deltaY, 0, UIntPtr.Zero);

//            step++;
//                    await Task.Delay(10);
//        }
//    }
//    finally
//    {
//        _aimLock.Release();
//    }
//}
//private async Task ProcessAim()
//{

//    if (!await _aimLock.WaitAsync(0)) return;

//    try
//    {
//        if (_self.Targets == null || _self.Targets.Count == 0) return;


//        int centerX = _self.CrossHairX;
//        int centerY = _self.CrossHairY;
//        Vector2 center = new(centerX, centerY);
//                var bestIndex = GetBest(center);

//                if (bestIndex == -1) return;
//                var best = _self.Targets[bestIndex];
//                Vector2 smoothed = Vector2.Lerp(center, new Vector2(best.ScreenX,best.ScreenY), _smooth);
//                float deltaX = smoothed.X - centerX;
//                float deltaY = smoothed.Y - centerY;

//                deltaX /= _aimSpeed;
//                deltaY /= _aimSpeed;
//                if (Math.Abs(deltaX) < 10 && Math.Abs(deltaY) < 10) return;

//                mouse_event(MOUSEEVENTF_MOVE, (int)deltaX, (int)deltaY, 0, UIntPtr.Zero);
//                await Task.Yield();
//                int step = 0;
//                while (step < 50)
//                {

//                 bestIndex = GetBest(center);

//                    if (bestIndex == -1) break ;
//                 best = _self.Targets[bestIndex];
//                    //best = GetBest(center);

//                    if (best == null) break; ;
//                    if (best.CurrentHp <= 0) break;
//                     deltaX = best.ScreenX - centerX;
//                     deltaY = best.ScreenY - centerY;

//                    if (Math.Abs(deltaX) < 10 && Math.Abs(deltaY) < 10) break;
//                    float easeFactor = 1f + (float)Math.Pow(step / 50f, 2) * 5f;
//                    deltaX /= easeFactor;
//                    deltaY /= easeFactor;

//                    deltaX = Math.Clamp(deltaX, -2,2);
//                    deltaY = Math.Clamp(deltaY, -2, 2);

//                    if (Math.Abs(deltaX) < 1) deltaX = Math.Sign(deltaX);
//                    if (Math.Abs(deltaY) < 1) deltaY = Math.Sign(deltaY);

//                    mouse_event(MOUSEEVENTF_MOVE, (int)deltaX, (int)deltaY, 0, UIntPtr.Zero);
//                    step++;
//                    await Task.Yield();
//                    //await Task.Delay(10);
//                }
//            }
//    finally
//    {
//        _aimLock.Release(); // Always release the lock
//    }
//}

    }
}
