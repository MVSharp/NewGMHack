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

    private GetDeviceStateDelegate? _originalKeyboardDelegate;
    private GetDeviceStateDelegate? _originalMouseDelegate;

    private delegate int GetDeviceStateDelegate(IntPtr devicePtr, int size, IntPtr dataPtr);

    //private static bool leftWasDown = false;
    //private static bool rightWasDown = false;
    //private static bool isSwitching = false;
    //private static int lastKeypad = 1;
    //private static int switchStep = 0;
    //private static DateTime lastTriggerTime = DateTime.MinValue;
    //private static readonly TimeSpan triggerCooldown = TimeSpan.FromMilliseconds(100);
    //private static readonly HashSet<int> injectedKeys = new();

    public DirectInputHookManager(ILogger<DirectInputHookManager> logger, SelfInformation self , DirectInputLogicProcessor directInputLogicProcessor)
    {
        _logger = logger;
        _self = self;
        _logicProcessor = directInputLogicProcessor;
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
private int HookedGetDeviceState(IntPtr devicePtr, int size, IntPtr dataPtr, DeviceType deviceType)
{
    var original = deviceType switch
    {
        DeviceType.Keyboard => _originalKeyboardDelegate,
        DeviceType.Mouse => _originalMouseDelegate,
        _ => null
    };

    if (original == null)
    {
        _logicProcessor.ZeroMemory(dataPtr, size);
        return 0;
    }

    if (!_self.ClientConfig.Features.IsFeatureEnable(FeatureName.IsAimSupport) && !_self.ClientConfig.Features.IsFeatureEnable(FeatureName.IsAutoReady))
        return original(devicePtr, size, dataPtr);

    int result = original(devicePtr, size, dataPtr);
    if (result != 0) return result;

    _logicProcessor.Process(deviceType, size, dataPtr);
    return 0;
}
    //private void InjectKey(byte[] keys, int dikCode)
    //{
    //    if ((keys[dikCode] & 0x80) == 0)
    //    {
    //        keys[dikCode] = 0x80;
    //        injectedKeys.Add(dikCode);
    //    }
    //}

    //private void ZeroMemory(IntPtr ptr, int size)
    //{
    //    if (ptr == IntPtr.Zero || size <= 0) return;
    //    byte[] zero = new byte[size];
    //    Marshal.Copy(zero, 0, ptr, size);
    //}

    //[StructLayout(LayoutKind.Sequential)]
    //private struct DIMOUSESTATE
    //{
    //    public int lX;
    //    public int lY;
    //    public int lZ;
    //    public byte rgbButtons0;
    //    public byte rgbButtons1;
    //    public byte rgbButtons2;
    //    public byte rgbButtons3;
    //    public byte rgbButtons4;
    //    public byte rgbButtons5;
    //    public byte rgbButtons6;
    //    public byte rgbButtons7;
    //}
}
