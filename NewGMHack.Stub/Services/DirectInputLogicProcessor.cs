using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using NewGMHack.Stub;
using NewGMHack.Stub.Services;
using NewGMHack.Stub.Logger;
using SharpDX.DirectInput;
using ZLogger;
using static NewGMHack.Stub.Services.DIK;

namespace NewGMHack.Stub.Services;

public record InputState(bool IsLeftDown, bool IsRightDown);

public static class DIK
{
    public const int DIK_ESCAPE = 0x01;
    public const int DIK_1 = 0x02;
    public const int DIK_2 = 0x03;
    public const int DIK_3 = 0x04;
    public const int DIK_F5 = 0x3F;
}

public class ScheduledEvent
{
    public int Code;
    public bool IsDown;
    public long TriggerTimeMs;
    public bool Fired;
    public bool IsMouse;

    public ScheduledEvent(int code, bool down, long time, bool isMouse = false)
    {
        Code = code;
        IsDown = down;
        TriggerTimeMs = time;
        IsMouse = isMouse;
    }
}
public partial class DirectInputLogicProcessor
{
    private readonly SelfInformation _self;
    private readonly ILogger<DirectInputLogicProcessor> _logger;
    private readonly InputStateTracker InputTracker;

    private int _lastManualWeapon = DIK_1;
    private readonly Stopwatch _timer = Stopwatch.StartNew();
    private readonly List<ScheduledEvent> _scheduledEvents = new();

    private bool _isSwitching = false;
    private bool _leftWasDown = false;

    // Aimbot state - no longer needed but kept for compatibility
    private float _lastDeltaX = 0f;
    private float _lastDeltaY = 0f;

    // Target lead prediction state
    private float _lastTargetScreenX = 0f;
    private float _lastTargetScreenY = 0f;
    private float _targetVelocityX = 0f;
    private float _targetVelocityY = 0f;
    private long _lastPredictionTimeMs = 0;
    private uint _lastTargetPtr = 0;

    private const int DIK_ESCAPE = 0x01;
    private const int DIK_1 = 0x02;
    private const int DIK_2 = 0x03;
    private const int DIK_3 = 0x04;
    private const int DIK_F5 = 0x3F;

    public DirectInputLogicProcessor(SelfInformation self, ILogger<DirectInputLogicProcessor> logger, InputStateTracker tracker)
    {
        _self = self;
        _logger = logger;
        InputTracker = tracker;
    }

    /// <summary>
    /// Optimized aimbot with target lead prediction
    /// - Fast: 65% of distance per frame
    /// - Stable: Can't overshoot (mathematically impossible)
    /// - Predictive: Aims ahead of moving targets
    /// </summary>
    public void InjectAimbot(IntPtr dataPtr)
    {
        try
        {
            DIMOUSESTATE state = Marshal.PtrToStructure<DIMOUSESTATE>(dataPtr);

            // Check if right mouse button is held (aiming)
            bool isRightMouseDown = (state.rgbButtons1 & 0x80) != 0;
            if (!isRightMouseDown)
            {
                ResetPredictionState();
                return;
            }

            // Get best target
            var target = _self.Targets.FirstOrDefault(x => x.IsBest && x.CurrentHp > 0);
            if (target == null || target.ScreenX <= 0 || target.ScreenY <= 0)
            {
                ResetPredictionState();
                return;
            }

            // Get current time
            long currentTimeMs = _timer.ElapsedMilliseconds;
            float deltaTimeSeconds = (_lastPredictionTimeMs > 0) 
                ? (currentTimeMs - _lastPredictionTimeMs) / 1000f 
                : 0f;

            // Current target screen position
            float currentX = target.ScreenX;
            float currentY = target.ScreenY;

            // Reset velocity if targeting a different entity
            if (target.EntityPtrAddress != _lastTargetPtr)
            {
                _targetVelocityX = 0;
                _targetVelocityY = 0;
                _lastTargetPtr = target.EntityPtrAddress;
            }
            else if (_lastPredictionTimeMs > 0 && deltaTimeSeconds > 0 && deltaTimeSeconds < 0.5f)
            {
                // Calculate velocity (pixels per second)
                float rawVelX = (currentX - _lastTargetScreenX) / deltaTimeSeconds;
                float rawVelY = (currentY - _lastTargetScreenY) / deltaTimeSeconds;

                // Smooth velocity with exponential moving average (more smoothing = less jitter)
                const float velocitySmoothing = 0.15f; // Reduced from 0.3 for smoother velocity
                _targetVelocityX = _targetVelocityX + (rawVelX - _targetVelocityX) * velocitySmoothing;
                _targetVelocityY = _targetVelocityY + (rawVelY - _targetVelocityY) * velocitySmoothing;
            }

            // Store for next frame
            _lastTargetScreenX = currentX;
            _lastTargetScreenY = currentY;
            _lastPredictionTimeMs = currentTimeMs;

            // Predict future position (lead the target)
            // Reduced prediction time to prevent overshoot
            const float predictionTime = 0.04f; // 40ms ahead (was 80ms)
            float predictedX = currentX + (_targetVelocityX * predictionTime);
            float predictedY = currentY + (_targetVelocityY * predictionTime);

            // Calculate delta to predicted position
            float deltaX = predictedX - _self.CrossHairX;
            float deltaY = predictedY - _self.CrossHairY;
            float distance = (float)Math.Sqrt(deltaX * deltaX + deltaY * deltaY);

            // Check if within aim circle
            if (distance > _self.AimRadius) return;

            // Larger dead zone to prevent jitter (was 3px)
            if (distance < 10f) return;

            // ALWAYS use exponential decay - no snap zone (snap caused vibration)
            // Lower decay factor = slower but more stable
            const float decayFactor = 0.50f; // Reduced from 0.65
            
            int moveX = (int)(deltaX * decayFactor);
            int moveY = (int)(deltaY * decayFactor);

            // Minimum movement to prevent stalling
            if (moveX == 0 && Math.Abs(deltaX) > 1f) moveX = Math.Sign(deltaX);
            if (moveY == 0 && Math.Abs(deltaY) > 1f) moveY = Math.Sign(deltaY);

            // Lower max speed clamp for stability
            moveX = Math.Clamp(moveX, -80, 80);
            moveY = Math.Clamp(moveY, -80, 80);

            // Inject movement
            if (moveX != 0 || moveY != 0)
            {
                state.lX += moveX;
                state.lY += moveY;
                Marshal.StructureToPtr(state, dataPtr, false);
            }
        }
        catch
        {
            // Swallow errors
        }
    }

    private void ResetPredictionState()
    {
        _lastTargetScreenX = 0;
        _lastTargetScreenY = 0;
        _targetVelocityX = 0;
        _targetVelocityY = 0;
        _lastPredictionTimeMs = 0;
        _lastTargetPtr = 0;
    }



    public void Process(DeviceType deviceType, int size, IntPtr dataPtr)
    {
        if (dataPtr == IntPtr.Zero)
        {

            _logger.LogNullDataPointer();
            return;
        }
        if (!IsAutoReadyEnabled)
        {
            _scheduledEvents.Clear();
            _logger.LogAllFeaturesDisabled();
            return;
        }
        if (deviceType == DeviceType.Keyboard && size == 256)
            ProcessKeyboard(dataPtr);
        else if (deviceType == DeviceType.Mouse && size == Marshal.SizeOf<DIMOUSESTATE>())
            ProcessMouse(dataPtr);
        else
            _logger.LogUnknownDevice(deviceType, size);
    }

    private void ProcessKeyboard(IntPtr dataPtr)
    {
        byte[] keys = new byte[256];
        Marshal.Copy(dataPtr, keys, 0, 256);

        if (IsAutoReadyEnabled )//&& InputTracker.IsKeyboardIdle)
        {
            //_logger.ZLogInformation($"Handle auto ready");
            HandleAutoReady();
        }

for (int i = DIK_1; i <= DIK_3; i++)
{
    if ((keys[i] & 0x80) != 0)
    {
        _lastManualWeapon = i;
        //_logger.ZLogInformation($"our things: Manual weapon selected {_lastManualWeapon}");
    }
}
        InjectScheduledKeys(keys);
        Marshal.Copy(keys, 0, dataPtr, 256);
    }

    private void ProcessMouse(IntPtr dataPtr)
    {
        DIMOUSESTATE state = Marshal.PtrToStructure<DIMOUSESTATE>(dataPtr);

        if (IsAutoReadyEnabled)
        {
            //_logger.ZLogInformation($"handle aim");
            HandleAimSupport(ref state);
        }

        InjectScheduledMouse(ref state);
        Marshal.StructureToPtr(state, dataPtr, false);
    }

    private bool IsAutoReadyEnabled =>
        _self.ClientConfig.Features.IsFeatureEnable(FeatureName.IsAutoReady);

    //private bool IsAimSupportEnabled =>
    //    _self.ClientConfig.Features.IsFeatureEnable(FeatureName.IsAutoAIm);

    private void HandleAutoReady()
    {
        _logger.LogHandleAutoReady();
        ScheduleKey(DIK_F5, 0, 50);
        ScheduleKey(DIK_ESCAPE, 100, 50);
        ScheduleMouse(1, true, 200);
        ScheduleMouse(1, false, 250);
    }

private long _lastRightSpamTime = 0;
private const int RightSpamIntervalMs = 80;
private void HandleAimSupport(ref DIMOUSESTATE state)
{

        _logger.LogHandleAimSupport();
    bool leftDown = InputTracker.IsLeftDown;
    bool rightDown = InputTracker.IsRightDown;

    if (leftDown && !rightDown)
    {
        state.rgbButtons1 = 0x80;
    }
    else if (_leftWasDown && !leftDown)
    {
        state.rgbButtons1 = 0x00;
        //SwitchWeapon();
    }

    _leftWasDown = leftDown;
}
    //private void HandleAimSupport(ref DIMOUSESTATE state)
    //{
    //    bool leftDown = InputTracker.IsLeftDown;
    //    bool rightDown = InputTracker.IsRightDown;

    //    if (leftDown && !rightDown)
    //    {
    //        state.rgbButtons1 = 0x80;
    //        //_logger.ZLogInformation($"our things: Injected right click DOWN");
    //    }
    //    else if (_leftWasDown && !leftDown)
    //    {
    //        state.rgbButtons1 = 0x00;
    //        //_logger.ZLogInformation($"our things: Injected right click UP");
    //        SwitchWeapon();
    //    }

    //    _leftWasDown = leftDown;
    //}

private void SwitchWeapon()
{
    _scheduledEvents.RemoveAll(e => !e.Fired); // Clear pending junk
    long now = _timer.ElapsedMilliseconds;

    switch (_lastManualWeapon)
    {
        case DIK_1:
            ScheduleKey(DIK_2, 0, 100);
            ScheduleKey(DIK_1, 150, 100);
            break;
        case DIK_2:
            ScheduleKey(DIK_1, 0, 100);
            ScheduleKey(DIK_2, 150, 100);
            break;
        case DIK_3:
            ScheduleKey(DIK_1, 0, 100);
            ScheduleKey(DIK_3, 150, 100);
            break;
        default:
            ScheduleKey(DIK_2, 0, 100);
            ScheduleKey(DIK_1, 150, 100);
            break;
    }

    //_logger.ZLogInformation($"our things: Weapon switch based on {_lastManualWeapon}");
}

    private void ScheduleKey(int code, int downDelay, int upDelay)
    {
        long now = _timer.ElapsedMilliseconds;
        _scheduledEvents.Add(new ScheduledEvent(code, true, now + downDelay));
        _scheduledEvents.Add(new ScheduledEvent(code, false, now + downDelay + 10 + upDelay));
    }

    private void ScheduleMouse(int button, bool down, int delay)
    {
        long now = _timer.ElapsedMilliseconds;
        _scheduledEvents.Add(new ScheduledEvent(button, down, now + delay, isMouse: true));
    }

    private void InjectScheduledKeys(byte[] keys)
    {
        long now = _timer.ElapsedMilliseconds;
        foreach (var evt in _scheduledEvents.Where(e => !e.Fired && !e.IsMouse && now >= e.TriggerTimeMs))
        {
            keys[evt.Code] = evt.IsDown ? (byte)0x80 : (byte)0x00;
            evt.Fired = true;
            //_logger.ZLogInformation($"inject key fire");
        }
    }

    private void InjectScheduledMouse(ref DIMOUSESTATE state)
    {
        long now = _timer.ElapsedMilliseconds;
        foreach (var evt in _scheduledEvents.Where(e => !e.Fired && e.IsMouse && now >= e.TriggerTimeMs))
        {
            byte val = evt.IsDown ? (byte)0x80 : (byte)0x00;
            switch (evt.Code)
            {
                case 0: state.rgbButtons0 = val; break;
                case 1: state.rgbButtons1 = val; break;
                case 2: state.rgbButtons2 = val; break;
            }
            evt.Fired = true;
            //_logger.ZLogInformation($"inject mouse fire");
        }
    }

    public void ZeroMemory(IntPtr ptr, int size)
    {
        unsafe
        {
            byte* p = (byte*)ptr;
            for (int i = 0; i < size; i++)
                p[i] = 0;
        }
    }
}

