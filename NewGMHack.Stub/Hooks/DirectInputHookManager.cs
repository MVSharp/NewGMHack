using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using InjectDotnet.NativeHelper;
using Microsoft.Extensions.Logging;
using NewGMHack.Stub;
using NewGMHack.Stub.Hooks;
using NewGMHack.Stub.Services;
using SharpDX.DirectInput;
using ZLogger;

public class DirectInputHookManager : IHookManager
{
    private readonly ILogger<DirectInputHookManager> _logger;
    private readonly SelfInformation _self;
    private readonly DirectInputLogicProcessor _logicProcessor;
    private readonly List<INativeHook> _hooks = new();
    private readonly List<Delegate> _activeDelegates = new(); // Prevent GC
    private readonly InputStateTracker _inputStateTracker;
    private GetDeviceStateDelegate? _originalKeyboardDelegate;
    private GetDeviceStateDelegate? _originalMouseDelegate;

    private delegate int GetDeviceStateDelegate(IntPtr devicePtr, int size, IntPtr dataPtr);

private Thread? _pollingThread;
private bool _pollingActive;
private Keyboard? _pollingKeyboard;
private Mouse? _pollingMouse;

    public DirectInputHookManager(ILogger<DirectInputHookManager> logger, SelfInformation self , DirectInputLogicProcessor directInputLogicProcessor , InputStateTracker input)
    {
        _logger = logger;
        _self = self;
        _logicProcessor = directInputLogicProcessor;
        _inputStateTracker = input;
    }

    public void HookAll()
    {
        _logger.ZLogInformation($"Starting DirectInput hook setup");

        var directInput = new DirectInput();
        var devices = directInput.GetDevices(DeviceClass.All, DeviceEnumerationFlags.AttachedOnly);

        foreach (var deviceInfo in devices)
        {
            try
            {
                Device device = deviceInfo.Type switch
                {
                    DeviceType.Keyboard => new Keyboard(directInput),
                    DeviceType.Mouse => new Mouse(directInput),
                    _ => null
                };

                if (device == null) continue;

                device.SetCooperativeLevel(IntPtr.Zero, CooperativeLevel.Background | CooperativeLevel.NonExclusive);
                device.Acquire();

                IntPtr nativePtr = device.NativePointer;
                IntPtr vtablePtr = Marshal.ReadIntPtr(nativePtr);
                IntPtr methodPtr = Marshal.ReadIntPtr(vtablePtr + IntPtr.Size * 9); // GetDeviceState

                var hookDelegate = new GetDeviceStateDelegate((devicePtr, size, dataPtr) =>
                    HookedGetDeviceState(devicePtr, size, dataPtr, deviceInfo.Type));
                _activeDelegates.Add(hookDelegate);

                IntPtr hookPtr = Marshal.GetFunctionPointerForDelegate(hookDelegate);
                var hook = JumpHook.Create(methodPtr, hookPtr, installAfterCreate: true);

                if (hook != null && hook.OriginalFunction != IntPtr.Zero)
                {
                    var original = Marshal.GetDelegateForFunctionPointer<GetDeviceStateDelegate>(hook.OriginalFunction);

                    if (deviceInfo.Type == DeviceType.Keyboard)
                        _originalKeyboardDelegate = original;
                    else if (deviceInfo.Type == DeviceType.Mouse)
                        _originalMouseDelegate = original;

                    _hooks.Add(hook);
                    _logger.ZLogInformation($"{deviceInfo.Type} hook installed: {deviceInfo.ProductName}");
                }
            }
            catch (Exception ex)
            {
                _logger.ZLogError($"Failed to hook device '{deviceInfo.ProductName}': {ex.Message}");
            }
        }

        _logger.ZLogInformation($"DirectInput hook setup completed");

    }


    public void UnHookAll()
    {
        _logger.LogInformation("Unhooking all DirectInput hooks");
        foreach (var hook in _hooks)
        {
            try { hook.Dispose(); } catch { }
        }
        _hooks.Clear();
    }

private bool _f5Down = false;
private bool _escDown = false;
private bool _rightMouseDown = false;
private int HookedGetDeviceState(IntPtr devicePtr, int size, IntPtr dataPtr, DeviceType deviceType)
{
    var original = deviceType switch
    {
        DeviceType.Keyboard => _originalKeyboardDelegate,
        DeviceType.Mouse => _originalMouseDelegate,
        _ => null
    };
        //_logger.ZLogInformation($"{size} | {deviceType}");
    if (original == null)
    {
            _logger.ZLogInformation($"Oh fuck original delegate lost");
        _logicProcessor.ZeroMemory(dataPtr, size);
        return 0;
    }

        //if (!_self.ClientConfig.Features.IsFeatureEnable(FeatureName.IsAimSupport) && !_self.ClientConfig.Features.IsFeatureEnable(FeatureName.IsAutoReady))
        //{

        //    _logger.ZLogInformation($"no features enabled");
        //    return original(devicePtr, size, dataPtr);
        //}

        int result = original(devicePtr, size, dataPtr);
        if (result != 0)
        {
            _logicProcessor.ZeroMemory(dataPtr, size);
        }
        bool isAutoReady = _self.ClientConfig.Features.IsFeatureEnable(FeatureName.IsAutoReady);

    if (isAutoReady)
    {
if (deviceType == DeviceType.Keyboard && size == 256)
{
    byte[] keys = new byte[256];
    Marshal.Copy(dataPtr, keys, 0, 256);

    _f5Down = !_f5Down;
    _escDown = !_escDown;

    keys[63] = _f5Down ? (byte)0x80 : (byte)0x00;
    keys[1] = _escDown ? (byte)0x80 : (byte)0x00;

    Marshal.Copy(keys, 0, dataPtr, 256);
}
else if (deviceType == DeviceType.Mouse && size == Marshal.SizeOf<DIMOUSESTATE>())
{
    DIMOUSESTATE state = Marshal.PtrToStructure<DIMOUSESTATE>(dataPtr);

    _rightMouseDown = !_rightMouseDown;
    state.rgbButtons1 = _rightMouseDown ? (byte)0x80 : (byte)0x00;

    Marshal.StructureToPtr(state, dataPtr, false);
}
    }
    else
        {
            return result; // make the inner buffer work after hook
        }
            //if (result != 0) // dont enable this fucker , if mouse or keyboard no update , this fucker may lost
            //{

            //    //_logger.ZLogInformation($"so strange , result not 0 : result:{result} | {deviceType} {size} {devicePtr}");
            //    //return result;
            //}
            //_inputStateTracker.Update(deviceType, size, dataPtr);
            //_logicProcessor.Process(deviceType, size, dataPtr);
            return 0;
    }
}
