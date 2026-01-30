using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using NewGMHack.Stub;
using NewGMHack.Stub.Services;
using NewGMHack.Stub.Logger;
using SharpDX;
using SharpDX.Direct3D9;
using ZLogger;
using static NewGMHack.Stub.Services.DIK;
using DeviceType = SharpDX.DirectInput.DeviceType;

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

    // Target lead prediction and smoothing state
    private float _lastTargetScreenX = 0f;
    private float _lastTargetScreenY = 0f;
    private float _targetVelocityX = 0f;
    private float _targetVelocityY = 0f;
    private long _lastPredictionTimeMs = 0;
    private uint _lastTargetPtr = 0;
    
    // Sub-pixel accumulator for smooth mouse movement
    private float _accumulatedX = 0f;
    private float _accumulatedY = 0f;
    private long _lastControlTimeMs = 0;
    
    // Output velocity smoothing
    private float _outputVelX = 0f;
    private float _outputVelY = 0f;

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
    /// Optimized aimbot with dynamic gain smoothing
    /// - Fast approach at distance
    /// - Smooth decoupling at close range
    /// - Sub-pixel precision
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
            var target = GetBestTarget();
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

            // Reset smoothing if targeting a different entity
            if (target.EntityPtrAddress != _lastTargetPtr)
            {
                _outputVelX = 0;
                _outputVelY = 0;
                _accumulatedX = 0;
                _accumulatedY = 0;
                _targetVelocityX = 0;
                _targetVelocityY = 0;
                _lastTargetPtr = target.EntityPtrAddress;
            }
            else if (_lastPredictionTimeMs > 0 && deltaTimeSeconds > 0 && deltaTimeSeconds < 0.5f)
            {
                float rawVelX = (currentX - _lastTargetScreenX) / deltaTimeSeconds;
                float rawVelY = (currentY - _lastTargetScreenY) / deltaTimeSeconds;
                const float velocitySmoothing = 0.3f;
                _targetVelocityX += (rawVelX - _targetVelocityX) * velocitySmoothing;
                _targetVelocityY += (rawVelY - _targetVelocityY) * velocitySmoothing;
            }

            // Store for next frame
            _lastTargetScreenX = currentX;
            _lastTargetScreenY = currentY;
            _lastPredictionTimeMs = currentTimeMs; // Update _lastPredictionTimeMs here

            // Note: prediction logic remains similar, but we use it for error calculation
            const float predictionTime = 0.06f; 
            float predictedX = currentX + (_targetVelocityX * predictionTime);
            float predictedY = currentY + (_targetVelocityY * predictionTime);

            // Calculate error (Distance to target)
            float deltaX = predictedX - _self.CrossHairX;
            float deltaY = predictedY - _self.CrossHairY;
            float distance = (float)Math.Sqrt(deltaX * deltaX + deltaY * deltaY);

            // Safety break
            if (distance > _self.AimRadius) return;
            
            // --- Time-Based Control Loop ---
            
            // Calculate effective DeltaTime for this control step
            // We use the time since the LAST InjectAimbot call, not the prediction time
            long now = _timer.ElapsedMilliseconds;
            float dt = (_lastControlTimeMs > 0) ? (now - _lastControlTimeMs) / 1000f : 0.001f;
            _lastControlTimeMs = now;
            
            // Clamp dt to prevent huge jumps if thread hangs (e.g. max 100ms)
            if (dt > 0.1f) dt = 0.1f;
            if (dt < 0.0001f) dt = 0.0001f; // Avoid divide by zero if called super fast

            // Get Proportional Gain (Kp)
            float Kp = DistanceToKp(distance);

            // Calculate Target Velocity: V = Error * Kp
            float targetVelX = deltaX * Kp;
            float targetVelY = deltaY * Kp;

            // --- Output Velocity Smoothing ---
            // Low-pass filter to dampen acceleration spikes
            // lerp(current, target, alpha)
            // Lower alpha = smoother/slower reaction, Higher alpha = snappier
            const float smoothAlpha = 0.6f;
            _outputVelX += (targetVelX - _outputVelX) * smoothAlpha;
            _outputVelY += (targetVelY - _outputVelY) * smoothAlpha;

            // Calculate movement step: dX = V * dt
            float moveFloatX = _outputVelX * dt;
            float moveFloatY = _outputVelY * dt;

            // Clamp max step size to prevent "teleporting"
            moveFloatX = Math.Clamp(moveFloatX, -50f, 50f);
            moveFloatY = Math.Clamp(moveFloatY, -50f, 50f);

            // Sub-pixel accumulation
            _accumulatedX += moveFloatX;
            _accumulatedY += moveFloatY;

            int moveX = (int)_accumulatedX;
            int moveY = (int)_accumulatedY;

            // Keep remainder
            _accumulatedX -= moveX;
            _accumulatedY -= moveY;

            // Small hysteresis deadzone (only if very slow)
            if (distance < 2.0f && Math.Abs(moveX) < 1 && Math.Abs(moveY) < 1) 
            {
               // Do nothing, holding steady
            }
            else
            {
                // Inject movement
                if (moveX != 0 || moveY != 0)
                {
                    state.lX += moveX;
                    state.lY += moveY;
                    Marshal.StructureToPtr(state, dataPtr, false);
                }
            }
        }
        catch
        {
            // Swallow errors
        }
    }

    // Proportional Gain Lookup - Tuned for stability
    private float DistanceToKp(float distance)
    {
        // Far: Fast but controlled (Reduced from 18.0)
        // Helps prevent long-range overshoot/vibration
        if (distance > 150f) return 12.0f; 
        
        // Very Close: Increased from 2.0 to 5.0 for better tracking
        if (distance < 5f) return 5.0f;
        
        // Mid range: Linear blend
        // 5..150 -> 5.0..12.0
        return 5.0f + (distance - 5f) * (12.0f - 5.0f) / (150f - 5f);
    }

   Vector2 WorldToScreen(Vector3 world, Matrix view, Matrix proj, Viewport vp)
   {
       Vector4 clip = Vector4.Transform(new Vector4(world, 1.0f), view * proj);
       if (clip.W < 0.1f) return Vector2.Zero;

       Vector3 ndc = new Vector3(clip.X, clip.Y, clip.Z) / clip.W;
       return new Vector2(
                          (ndc.X + 1.0f)  * 0.5f * vp.Width  + vp.X,
                          (1.0f  - ndc.Y) * 0.5f * vp.Height + vp.Y
                         );
   }

    private Entity? GetBestTarget()
    {
        if (_self.DevicePtr == IntPtr.Zero) return null;
        var device = new Device(_self.DevicePtr);
            var viewMatrix = device.GetTransform(TransformState.View);
            var projMatrix = device.GetTransform(TransformState.Projection);
            var viewport   = device.Viewport;
        var crosshair = new Vector2(_self.CrossHairX, _self.CrossHairY);
        Entity best = null;
        float bestDist = float.MaxValue;

        foreach (var t in _self.Targets)
        {
            if (t.Id == 0) continue ;
            if (t.CurrentHp <= 0)
                continue;
            Vector2 screenPos = WorldToScreen(t.Position, viewMatrix, projMatrix, viewport);
            if(screenPos.X <= 0 || screenPos.Y <= 0 ) continue;
            float dist = Vector2.Distance(crosshair, screenPos);
            if (dist <= _self.AimRadius && dist <= bestDist)
            {
                bestDist = dist;
                best = t;
            }
        }

        foreach (var entity in _self.Targets)
        {
            entity.IsBest = (entity == best);
        }

        return best;
    }
    private void ResetPredictionState()
    {
        _lastTargetScreenX = 0;
        _lastTargetScreenY = 0;
        _targetVelocityX = 0;
        _targetVelocityY = 0;
        _lastPredictionTimeMs = 0;
        _lastTargetPtr = 0;
        _accumulatedX = 0;
        _accumulatedY = 0;
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
        //ScheduleKey(DIK_F5, 0, 50);
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

