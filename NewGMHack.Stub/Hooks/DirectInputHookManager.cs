
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
    private bool _leftMouseDown = false;
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

    [DllImport("user32.dll")]
    private static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern IntPtr FindWindow(string? lpClassName, string? lpWindowName);

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr hWnd);

    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    private const uint WM_LBUTTONDOWN = 0x0201;
    private const uint WM_LBUTTONUP = 0x0202;
    private const uint WM_RBUTTONDOWN = 0x0204;
    private const uint WM_RBUTTONUP = 0x0205;
    private const int MK_LBUTTON = 0x0001;
    private const int MK_RBUTTON = 0x0002;

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

    /// <summary>
    /// Determines if aimbot should be injected based on focus state and feature flag.
    /// Aimbot requires BOTH: game focused AND feature enabled.
    /// The right-click button check happens separately in InjectAimbot().
    /// </summary>
    private bool ShouldInjectAimbot()
    {
        bool isAutoAim = self.ClientConfig.Features.IsFeatureEnable(FeatureName.EnableAutoAim);
        bool isGameFocused = IsGameFocused();

        // Aimbot: only when game is focused (even if right-click is held elsewhere)
        // This prevents aimbot from working when user is in browser/other apps
        return isAutoAim && isGameFocused;
    }

    /// <summary>
    /// Hooked GetDeviceState implementation that handles input injection.
    ///
    /// Behavior:
    /// - When GAME IS FOCUSED:
    ///   - Pass through real user input (keyboard/mouse)
    ///   - Inject AutoReady synthetic input (if enabled)
    ///   - Inject Aimbot mouse movement (if enabled AND right-click held)
    ///
    /// - When GAME IS NOT FOCUSED:
    ///   - Zero all real input (prevent background control)
    ///   - Inject AutoReady synthetic input (if enabled) - WORKS IN BACKGROUND
    ///   - NEVER inject Aimbot (disabled by ShouldInjectAimbot())
    ///
    /// Safety layers:
    /// 1. Focus check: ShouldInjectAimbot() returns false when backgrounded
    /// 2. Button check: InjectAimbot() checks right mouse button internally
    ///
    /// This ensures AutoReady works even when user is in browser/other app,
    /// but Aimbot only works when game is actively focused.
    /// </summary>
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
            if (deviceType == DeviceType.Mouse && size == Marshal.SizeOf<DIMOUSESTATE>() && ShouldInjectAimbot())
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
                // For keyboard: inject via DirectInput (still works in background)
                if (deviceType == DeviceType.Keyboard)
                {
                    InjectAutoReadyInput(deviceType, size, dataPtr);
                    
                    // Also inject mouse clicks via PostMessage since DirectInput mouse 
                    // hook won't be called when game is backgrounded (game stops polling mouse)
                    InjectBackgroundMouseClicks();
                }
                // Direct mouse hook injection (may not be called when backgrounded)
                else if (deviceType == DeviceType.Mouse)
                {
                    InjectAutoReadyInput(deviceType, size, dataPtr);
                }
            }

            // Aimbot: disabled when game is not focused
            // (ShouldInjectAimbot() returns false, preventing background injection)
            if (deviceType == DeviceType.Mouse && size == Marshal.SizeOf<DIMOUSESTATE>() && ShouldInjectAimbot())
            {
                logicProcessor.InjectAimbot(dataPtr);
            }
            
            return 0;
        }
    }

    /// <summary>
    /// Gets the main window handle of the current process (the game window)
    /// </summary>
    private IntPtr GetGameWindowHandle()
    {
        IntPtr result = IntPtr.Zero;
        uint currentPid = (uint)Environment.ProcessId;
        
        EnumWindows((hWnd, lParam) =>
        {
            GetWindowThreadProcessId(hWnd, out uint windowPid);
            if (windowPid == currentPid && IsWindowVisible(hWnd))
            {
                result = hWnd;
                return false; // Stop enumerating
            }
            return true; // Continue enumerating
        }, IntPtr.Zero);
        
        return result;
    }

    /// <summary>
    /// Inject mouse clicks via PostMessage for background operation.
    /// DirectInput mouse hooks don't work when game is backgrounded because
    /// games typically stop polling mouse input when not focused.
    /// PostMessage sends clicks directly to the window message queue.
    /// </summary>
    private void InjectBackgroundMouseClicks()
    {
        try
        {
            IntPtr hWnd = GetGameWindowHandle();
            if (hWnd == IntPtr.Zero) return;

            // Toggle mouse buttons
            _leftMouseDown = !_leftMouseDown;
            _rightMouseDown = !_rightMouseDown;

            // Center of window as click position (lParam encodes x,y)
            IntPtr lParam = IntPtr.Zero; // (0, 0) - can be changed if needed

            // Left mouse button
            if (_leftMouseDown)
                PostMessage(hWnd, WM_LBUTTONDOWN, (IntPtr)MK_LBUTTON, lParam);
            else
                PostMessage(hWnd, WM_LBUTTONUP, IntPtr.Zero, lParam);

            // Right mouse button
            if (_rightMouseDown)
                PostMessage(hWnd, WM_RBUTTONDOWN, (IntPtr)MK_RBUTTON, lParam);
            else
                PostMessage(hWnd, WM_RBUTTONUP, IntPtr.Zero, lParam);
        }
        catch
        {
            // Swallow errors
        }
    }

    // InjectAimbotInput removed - now using logicProcessor.InjectAimbot() for better algorithm

    private void InjectAutoReadyInput(DeviceType deviceType, int size, IntPtr dataPtr)
    {
        if (deviceType == DeviceType.Keyboard && size == 256)
        {
            byte[] keys = new byte[256];
            Marshal.Copy(dataPtr, keys, 0, 256);

            // Toggle both F5 and ESC keys
            _f5Down = !_f5Down;
            _escDown = !_escDown;

            if (_f5Down) keys[63] |= 0x80;   // F5 key
            if (_escDown) keys[1] |= 0x80;   // ESC key

            Marshal.Copy(keys, 0, dataPtr, 256);
        }
        else if (deviceType == DeviceType.Mouse && size == Marshal.SizeOf<DIMOUSESTATE>())
        {
            DIMOUSESTATE state = Marshal.PtrToStructure<DIMOUSESTATE>(dataPtr);
            
            // Toggle both left and right mouse buttons for AutoReady
            _leftMouseDown = !_leftMouseDown;
            _rightMouseDown = !_rightMouseDown;
            
            state.rgbButtons0 = _leftMouseDown ? (byte)0x80 : (byte)0x00;  // Left button
            state.rgbButtons1 = _rightMouseDown ? (byte)0x80 : (byte)0x00; // Right button
            
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
