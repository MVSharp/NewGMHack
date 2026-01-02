
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using Reloaded.Hooks.Definitions;
using NewGMHack.Stub.Services;
using SharpDX.DirectInput;
using Reloaded.Hooks.Definitions.X86;

namespace NewGMHack.Stub.Hooks;

public class DirectInputHookManager(
    ILogger<DirectInputHookManager> logger,
    SelfInformation self,
    DirectInputLogicProcessor logicProcessor,
    InputStateTracker inputStateTracker,
    IReloadedHooks hooksEngine // Injected Reloaded.Hooks engine
) : IHookManager
{
    private readonly List<IHook<GetDeviceStateDelegate>> _hooks = [];
    private readonly List<Delegate> _activeDelegates = []; // Prevent GC


    private GetDeviceStateDelegate? _originalKeyboardDelegate;
    private GetDeviceStateDelegate? _originalMouseDelegate;

        [Function(CallingConventions.Stdcall)]
    private delegate int GetDeviceStateDelegate(IntPtr devicePtr, int size, IntPtr dataPtr);

    private bool _f5Down = false;
    private bool _escDown = false;
    private bool _rightMouseDown = false;

    public void HookAll()
    {
        logger.LogInformation($"Starting DirectInput hook setup");

        var directInput = new DirectInput();
        var devices = directInput.GetDevices(DeviceClass.All, DeviceEnumerationFlags.AttachedOnly);

        foreach (var deviceInfo in devices)
        {
            try
            {
                Device? device = deviceInfo.Type switch
                {
                    DeviceType.Keyboard => new Keyboard(directInput),
                    DeviceType.Mouse => new Mouse(directInput),
                    _ => null
                };

                if (device == null)
                    continue;

                device.SetCooperativeLevel(IntPtr.Zero, CooperativeLevel.Background | CooperativeLevel.NonExclusive);
                device.Acquire();

                IntPtr nativePtr = device.NativePointer;
                IntPtr vtablePtr = Marshal.ReadIntPtr(nativePtr);
                IntPtr methodPtr = Marshal.ReadIntPtr(vtablePtr + IntPtr.Size * 9); // GetDeviceState

                var hookDelegate = new GetDeviceStateDelegate((devicePtr, size, dataPtr) =>
                    HookedGetDeviceState(devicePtr, size, dataPtr, deviceInfo.Type));
                _activeDelegates.Add(hookDelegate);

                var hook = hooksEngine.CreateHook(hookDelegate, methodPtr);
                hook.Activate();
                hook.Enable();

                if (hook.OriginalFunction != null)
                {
                    if (deviceInfo.Type == DeviceType.Keyboard)
                        _originalKeyboardDelegate = hook.OriginalFunction;
                    else if (deviceInfo.Type == DeviceType.Mouse)
                        _originalMouseDelegate = hook.OriginalFunction;

                    _hooks.Add(hook);
                    logger.LogInformation($"{deviceInfo.Type} hook installed: {deviceInfo.ProductName}");
                }
                else
                {
                    logger.LogWarning($"Hook created but original function was null for {deviceInfo.ProductName}");
                }
            }
            catch (Exception ex)
            {
                logger.LogError($"Failed to hook device '{deviceInfo.ProductName}': {ex.Message}");
            }
        }

        logger.LogInformation($"DirectInput hook setup completed");
    }

    public void UnHookAll()
    {
        logger.LogInformation("Unhooking all DirectInput hooks");
        foreach (var hook in _hooks)
        {
            try
            {
                hook.Disable();
            }
            catch (Exception ex)
            {
                logger.LogWarning($"Error disabling hook: {ex.Message}");
            }
        }
        _hooks.Clear();
    }

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

    private bool IsGameFocused()
    {
        try
        {
            IntPtr foreground = GetForegroundWindow();
            if (foreground == IntPtr.Zero) return false;
            
            GetWindowThreadProcessId(foreground, out uint foregroundPid);
            return foregroundPid == Environment.ProcessId;
        }
        catch
        {
            return true; // Assume focused on error
        }
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
            logger.LogWarning($"Original delegate missing for {deviceType}");
            logicProcessor.ZeroMemory(dataPtr, size);
            return 0;
        }

        // Call original to get real input first
        int result = original(devicePtr, size, dataPtr);

        bool isGameFocused = IsGameFocused();
        bool isAutoReady = self.ClientConfig.Features.IsFeatureEnable(FeatureName.IsAutoReady);
        bool isAutoAim = self.ClientConfig.Features.IsFeatureEnable(FeatureName.EnableAutoAim);

        // If game is focused: pass through real input (don't zero, don't modify)
        // Unless AutoReady or AutoAim is enabled, then also inject synthetic input
        if (isGameFocused)
        {
            // Game is focused - real input should work
            if (isAutoReady)
            {
                // Merge synthetic input on top of real input
                InjectAutoReadyInput(deviceType, size, dataPtr);
            }

            // Aimbot: inject mouse delta when aiming (using refined algorithm in logicProcessor)
            if (isAutoAim && deviceType == DeviceType.Mouse && size == Marshal.SizeOf<DIMOUSESTATE>())
            {
                logicProcessor.InjectAimbot(dataPtr);
            }

            // Return original result - real input passes through
            return result;
        }
        else
        {
            // Game is NOT focused (backgrounded)
            // Zero real input, inject synthetic if needed
            logicProcessor.ZeroMemory(dataPtr, size);
            
            if (isAutoReady)
            {
                InjectAutoReadyInput(deviceType, size, dataPtr);
            }

            // Aimbot also works in background
            if (isAutoAim && deviceType == DeviceType.Mouse && size == Marshal.SizeOf<DIMOUSESTATE>())
            {
                logicProcessor.InjectAimbot(dataPtr);
            }
            
            return 0;
        }
    }

    // InjectAimbotInput removed - now using logicProcessor.InjectAimbot() for better algorithm

    private void InjectAutoReadyInput(DeviceType deviceType, int size, IntPtr dataPtr)
    {
        if (deviceType == DeviceType.Keyboard && size == 256)
        {
            byte[] keys = new byte[256];
            Marshal.Copy(dataPtr, keys, 0, 256);

            _f5Down = !_f5Down;
            _escDown = !_escDown;

            if (_f5Down) keys[63] |= 0x80;
            if (_escDown) keys[1] |= 0x80;

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
}
//using System;
//using System.Collections.Generic;
//using System.Runtime.InteropServices;
//using InjectDotnet.NativeHelper;
//using Microsoft.Extensions.Logging;
//using NewGMHack.Stub;
//using NewGMHack.Stub.Hooks;
//using NewGMHack.Stub.Services;
//using SharpDX.DirectInput;
//using ZLogger;

//public class DirectInputHookManager : IHookManager
//{
//    private readonly ILogger<DirectInputHookManager> _logger;
//    private readonly SelfInformation _self;
//    private readonly DirectInputLogicProcessor _logicProcessor;
//    private readonly List<INativeHook> _hooks = new();
//    private readonly List<Delegate> _activeDelegates = new(); // Prevent GC
//    private readonly InputStateTracker _inputStateTracker;
//    private GetDeviceStateDelegate? _originalKeyboardDelegate;
//    private GetDeviceStateDelegate? _originalMouseDelegate;

//    private delegate int GetDeviceStateDelegate(IntPtr devicePtr, int size,
//                                                IntPtr dataPtr);

//    //private Thread? _pollingThread;
//    //private bool _pollingActive;
//    //private Keyboard? _pollingKeyboard;
//    //private Mouse? _pollingMouse;

//    public DirectInputHookManager(
//        ILogger<DirectInputHookManager> logger, SelfInformation self,
//        DirectInputLogicProcessor directInputLogicProcessor,
//        InputStateTracker input)
//    {
//        _logger = logger;
//        _self = self;
//        _logicProcessor = directInputLogicProcessor;
//        _inputStateTracker = input;
//    }

//    public void HookAll()
//    {
//        _logger.ZLogInformation($"Starting DirectInput hook setup");

//        var directInput = new DirectInput();
//        var devices = directInput.GetDevices(
//            DeviceClass.All, DeviceEnumerationFlags.AttachedOnly);

//        foreach (var deviceInfo in devices)
//        {
//            try
//            {
//                Device device = deviceInfo.Type switch
//                {
//                    DeviceType.Keyboard => new Keyboard(directInput),
//                    DeviceType.Mouse => new Mouse(directInput),
//                    _ => null
//                };

//                if (device == null)
//                    continue;

//                device.SetCooperativeLevel(IntPtr.Zero,
//                                           CooperativeLevel.Background |
//                                               CooperativeLevel.NonExclusive);
//                device.Acquire();

//                IntPtr nativePtr = device.NativePointer;
//                IntPtr vtablePtr = Marshal.ReadIntPtr(nativePtr);
//                IntPtr methodPtr = Marshal.ReadIntPtr(
//                    vtablePtr + IntPtr.Size * 9); // GetDeviceState

//                var hookDelegate = new GetDeviceStateDelegate(
//                    (devicePtr, size, dataPtr) => HookedGetDeviceState(
//                        devicePtr, size, dataPtr, deviceInfo.Type));
//                _activeDelegates.Add(hookDelegate);

//                IntPtr hookPtr =
//                    Marshal.GetFunctionPointerForDelegate(hookDelegate);
//                var hook = JumpHook.Create(methodPtr, hookPtr,
//                                           installAfterCreate: true);

//                if (hook != null && hook.OriginalFunction != IntPtr.Zero)
//                {
//                    var original = Marshal.GetDelegateForFunctionPointer<
//                        GetDeviceStateDelegate>(hook.OriginalFunction);

//                    if (deviceInfo.Type == DeviceType.Keyboard)
//                        _originalKeyboardDelegate = original;
//                    else if (deviceInfo.Type == DeviceType.Mouse)
//                        _originalMouseDelegate = original;

//                    _hooks.Add(hook);
//                    _logger.ZLogInformation(
//                        $"{deviceInfo.Type} hook installed: {deviceInfo.ProductName}");
//                }
//            }
//            catch (Exception ex)
//            {
//                _logger.ZLogError(
//                    $"Failed to hook device '{deviceInfo.ProductName}': {ex.Message}");
//            }
//        }

//        _logger.ZLogInformation($"DirectInput hook setup completed");
//    }

//    public void UnHookAll()
//    {
//        _logger.LogInformation("Unhooking all DirectInput hooks");
//        foreach (var hook in _hooks)
//        {
//            try
//            {
//                hook.Dispose();
//            }
//            catch
//            {
//            }
//        }
//        _hooks.Clear();
//    }

//    private bool _f5Down = false;
//    private bool _escDown = false;
//    private bool _rightMouseDown = false;
//    private int HookedGetDeviceState(IntPtr devicePtr, int size, IntPtr dataPtr,
//                                     DeviceType deviceType)
//    {
        
//        var original =
//            deviceType switch
//            {
//                DeviceType.Keyboard =>
//                                    _originalKeyboardDelegate,
//                DeviceType.Mouse => _originalMouseDelegate,
//                _ => null
//            };
//        //_logger.ZLogInformation($"{size} | {deviceType}");
//        if (original == null)
//        {
//            _logger.ZLogInformation($"Oh fuck original delegate lost");
//            _logicProcessor.ZeroMemory(dataPtr, size);
//            return 0;
//        }
        
//        int result = original(devicePtr, size, dataPtr);
//        if (result != 0)
//        {
//            _logicProcessor.ZeroMemory(dataPtr, size);
//        }
//        bool isAutoReady = _self.ClientConfig.Features.IsFeatureEnable(
//            FeatureName.IsAutoReady);

//        if (isAutoReady)
//        {
//            if (deviceType == DeviceType.Keyboard && size == 256)
//            {
//                byte[] keys = new byte[256];
//                Marshal.Copy(dataPtr, keys, 0, 256);

//                _f5Down = !_f5Down;
//                _escDown = !_escDown;

//                if (_f5Down) keys[63] |= 0x80;
//                if (_escDown) keys[1] |= 0x80;

//                Marshal.Copy(keys, 0, dataPtr, 256);
//            }
//            else if (deviceType == DeviceType.Mouse &&
//                       size == Marshal.SizeOf<DIMOUSESTATE>())
//            {
//                DIMOUSESTATE state =
//                    Marshal.PtrToStructure<DIMOUSESTATE>(dataPtr);

//                _rightMouseDown = !_rightMouseDown;
//                state.rgbButtons1 = _rightMouseDown ? (byte)0x80 : (byte)0x00;

//                Marshal.StructureToPtr(state, dataPtr, false);
//            }
//        }
//        else
//        {
//            return result; // make the inner buffer work after hook
//        }
//        // if (result != 0) // dont enable this fucker , if mouse or keyboard no
//        // update , this fucker may lost
//        //{

//        //    //_logger.ZLogInformation($"so strange , result not 0 :
//        //    result:{result} | {deviceType} {size} {devicePtr}");
//        //    //return result;
//        //}
//        //_inputStateTracker.Update(deviceType, size, dataPtr);
//        //_logicProcessor.Process(deviceType, size, dataPtr);
//        return 0;
//    }
//}
