using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using InjectDotnet.NativeHelper;
using Microsoft.Extensions.Logging;
using NewGMHack.Stub.Hooks;
using SharpDX.DirectInput;
using ZLogger;

public class DirectInputHookManager : IHookManager
{
    private readonly ILogger<DirectInputHookManager> _logger;

    private readonly List<INativeHook> _hooks = new();

    private INativeHook? _getDeviceStateHook;
    private INativeHook? _getDeviceDataHook;

    private GetDeviceStateDelegate? _originalGetDeviceState;
    private GetDeviceDataDelegate? _originalGetDeviceData;

    private GetDeviceStateDelegate? _getDeviceStateHookDelegate;
    private GetDeviceDataDelegate? _getDeviceDataHookDelegate;

    private delegate int GetDeviceStateDelegate(IntPtr devicePtr, int size, IntPtr dataPtr);
    private delegate int GetDeviceDataDelegate(IntPtr devicePtr, int size, IntPtr dataPtr, ref int inOut, int flags);

    public DirectInputHookManager(ILogger<DirectInputHookManager> logger)
    {
        _logger = logger;
    }

    public void HookAll()
    {
        _logger.ZLogInformation($"Starting DirectInput hook setup");

        IntPtr vtablePtr = GetKeyboardDeviceVTable();
        if (vtablePtr == IntPtr.Zero)
        {
            _logger.ZLogError($"Failed to locate DirectInput device vtable");
            return;
        }

        _logger.ZLogInformation($"DirectInput vtable pointer: {vtablePtr}");

        try
        {
            _getDeviceStateHookDelegate = new GetDeviceStateDelegate(HookedGetDeviceState);
            IntPtr stateHookPtr = Marshal.GetFunctionPointerForDelegate(_getDeviceStateHookDelegate);
            IntPtr methodPtr = Marshal.ReadIntPtr(vtablePtr + IntPtr.Size * 9);

            _getDeviceStateHook = JumpHook.Create(methodPtr, stateHookPtr, installAfterCreate: true);
            if (_getDeviceStateHook == null || _getDeviceStateHook.OriginalFunction == IntPtr.Zero)
            {
                _logger.ZLogError($"Failed to create GetDeviceState hook");
            }
            else
            {
                _originalGetDeviceState = Marshal.GetDelegateForFunctionPointer<GetDeviceStateDelegate>(_getDeviceStateHook.OriginalFunction);
                _hooks.Add(_getDeviceStateHook);
                _logger.ZLogInformation($"GetDeviceState hook installed");
            }
        }
        catch (Exception ex)
        {
            _logger.ZLogError($"Exception while hooking GetDeviceState: {ex.Message}");
        }

        try
        {
            _getDeviceDataHookDelegate = new GetDeviceDataDelegate(HookedGetDeviceData);
            IntPtr dataHookPtr = Marshal.GetFunctionPointerForDelegate(_getDeviceDataHookDelegate);
            IntPtr methodPtr = Marshal.ReadIntPtr(vtablePtr + IntPtr.Size * 10);

            _getDeviceDataHook = JumpHook.Create(methodPtr, dataHookPtr, installAfterCreate: true);
            if (_getDeviceDataHook == null || _getDeviceDataHook.OriginalFunction == IntPtr.Zero)
            {
                _logger.ZLogError($"Failed to create GetDeviceData hook");
            }
            else
            {
                _originalGetDeviceData = Marshal.GetDelegateForFunctionPointer<GetDeviceDataDelegate>(_getDeviceDataHook.OriginalFunction);
                _hooks.Add(_getDeviceDataHook);
                _logger.ZLogInformation($"GetDeviceData hook installed");
            }
        }
        catch (Exception ex)
        {
            _logger.ZLogError($"Exception while hooking GetDeviceData: {ex.Message}");
        }

        _logger.ZLogInformation($"DirectInput hook setup completed");
    }
    public void UnHookAll()
    {
        _logger.LogInformation("Unhooking all DirectInput hooks");
        foreach (var hook in _hooks)
        {
            try
            {
                hook.Dispose();
            }
            catch (Exception ex)
            {
            }
        }
        _hooks.Clear();
    }

    private int HookedGetDeviceState(IntPtr devicePtr, int size, IntPtr dataPtr)
    {
        int result = _originalGetDeviceState?.Invoke(devicePtr, size, dataPtr) ?? 1;
        if (result == 0 && size == 256)
        {
            byte[] keys = new byte[256];
            Marshal.Copy(dataPtr, keys, 0, 256);

            // Simulate pressing DIK_A
            keys[0x1E] = 0x80;

            Marshal.Copy(keys, 0, dataPtr, 256);
        }
        return result;
    }

    private int HookedGetDeviceData(IntPtr devicePtr, int size, IntPtr dataPtr, ref int inOut, int flags)
    {
        int result = _originalGetDeviceData?.Invoke(devicePtr, size, dataPtr, ref inOut, flags) ?? 1;
        if (result == 0 && inOut > 0)
        {
            int structSize = Marshal.SizeOf(typeof(DIDEVICEOBJECTDATA));
            for (int i = 0; i < inOut; i++)
            {
                IntPtr entryPtr = IntPtr.Add(dataPtr, i * structSize);
                var entry = Marshal.PtrToStructure<DIDEVICEOBJECTDATA>(entryPtr);

                // Example: remap DIK_I to DIK_L
                if (entry.Offset == 0x17) // DIK_I
                {
                    entry.Offset = 0x26; // DIK_L
                    Marshal.StructureToPtr(entry, entryPtr, false);
                }
            }
        }
        return result;
    }

    private IntPtr GetKeyboardDeviceVTable()
    {
        try
        {
            var directInput = new DirectInput();
            var keyboard = new Keyboard(directInput);
            keyboard.SetCooperativeLevel(IntPtr.Zero, CooperativeLevel.Background | CooperativeLevel.NonExclusive);
            keyboard.Acquire();

            IntPtr devicePtr = keyboard.NativePointer;
            if (devicePtr == IntPtr.Zero)
            {
                _logger.ZLogInformation($"Get Directinput Vtable faied:{devicePtr}");
            }
            return Marshal.ReadIntPtr(devicePtr); // vtable pointer
        }
        catch (Exception e) 
        {
            _logger.ZLogInformation($"{nameof(GetKeyboardDeviceVTable)} | {e.Message} | {e.StackTrace}");
        }
        return IntPtr.Zero;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct DIDEVICEOBJECTDATA
    {
        public int Offset;
        public int Data;
        public int TimeStamp;
        public int Sequence;
        public IntPtr AppData;
    }
}
