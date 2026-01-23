using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using Reloaded.Hooks.Definitions;
using Reloaded.Hooks.Definitions.X86;

namespace NewGMHack.Stub.Hooks
{
    public sealed class CreateMutexHookManager(
        ILogger<CreateMutexHookManager> logger,
        IReloadedHooks hooksEngine
    ) : IHookManager
    {
        private IHook<CreateMutexWDelegate>? _createMutexWHook;
        private CreateMutexWDelegate? _originalCreateMutexW;

        private IHook<CreateMutexADelegate>? _createMutexAHook;
        private CreateMutexADelegate? _originalCreateMutexA;

        private IHook<OpenMutexWDelegate>? _openMutexWHook;
        private OpenMutexWDelegate? _originalOpenMutexW;

        private IHook<OpenMutexADelegate>? _openMutexAHook;
        private OpenMutexADelegate? _originalOpenMutexA;

        public void HookAll()
        {
            logger.LogInformation("Initializing CreateMutex/OpenMutex hooks");
            
            HookFunction("kernel32.dll", "CreateMutexW", new CreateMutexWDelegate(CreateMutexWHook), out _createMutexWHook, out _originalCreateMutexW);
            HookFunction("kernel32.dll", "CreateMutexA", new CreateMutexADelegate(CreateMutexAHook), out _createMutexAHook, out _originalCreateMutexA);
            HookFunction("kernel32.dll", "OpenMutexW",   new OpenMutexWDelegate(OpenMutexWHook),     out _openMutexWHook,   out _originalOpenMutexW);
            HookFunction("kernel32.dll", "OpenMutexA",   new OpenMutexADelegate(OpenMutexAHook),     out _openMutexAHook,   out _originalOpenMutexA);
        }

        public void UnHookAll()
        {
            _createMutexWHook?.Disable();
            _createMutexAHook?.Disable();
            _openMutexWHook?.Disable();
            _openMutexAHook?.Disable();
            logger.LogInformation("CreateMutex/OpenMutex hooks disabled");
        }

        private void HookFunction<T>(string dllName, string functionName, T hookDelegate, out IHook<T>? hook, out T? original) where T : Delegate
        {
            hook = null;
            original = null;
            try
            {
                var functionPtr = NativeLibrary.GetExport(NativeLibrary.Load(dllName), functionName);
                hook = hooksEngine.CreateHook(hookDelegate, functionPtr);
                hook.Activate();
                hook.Enable();

                logger.LogInformation($"Activated hook for {functionName} at 0x{functionPtr:X}");
                original = hook.OriginalFunction;
            }
            catch (Exception ex)
            {
                logger.LogError($"Failed to hook {functionName}: {ex}");
            }
        }

        private nint CreateMutexWHook(nint lpMutexAttributes, bool bInitialOwner, nint lpName)
        {
            try
            {
                string? mutexName = Marshal.PtrToStringUni(lpName);
                if (!string.IsNullOrEmpty(mutexName))
                {
                    logger.LogInformation($"CreateMutexW: {mutexName}");
                    string newName = $"{mutexName}_{System.Diagnostics.Process.GetCurrentProcess().Id}";
                    IntPtr newPtr = Marshal.StringToHGlobalUni(newName);
                    try 
                    {
                        return _originalCreateMutexW!(lpMutexAttributes, bInitialOwner, newPtr);
                    } 
                    finally 
                    {
                        Marshal.FreeHGlobal(newPtr);
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogError($"CreateMutexW hook error: {ex}");
            }
            return _originalCreateMutexW!(lpMutexAttributes, bInitialOwner, lpName);
        }

        private nint CreateMutexAHook(nint lpMutexAttributes, bool bInitialOwner, nint lpName)
        {
            try
            {
                string? mutexName = Marshal.PtrToStringAnsi(lpName);
                if (!string.IsNullOrEmpty(mutexName))
                {
                    logger.LogInformation($"CreateMutexA: {mutexName}");
                    string newName = $"{mutexName}_{System.Diagnostics.Process.GetCurrentProcess().Id}";
                    IntPtr newPtr = Marshal.StringToHGlobalAnsi(newName);
                    try 
                    {
                        return _originalCreateMutexA!(lpMutexAttributes, bInitialOwner, newPtr);
                    } 
                    finally 
                    {
                        Marshal.FreeHGlobal(newPtr);
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogError($"CreateMutexA hook error: {ex}");
            }
            return _originalCreateMutexA!(lpMutexAttributes, bInitialOwner, lpName);
        }

        private nint OpenMutexWHook(uint dwDesiredAccess, bool bInheritHandle, nint lpName)
        {
            try
            {
                string? mutexName = Marshal.PtrToStringUni(lpName);
                if (!string.IsNullOrEmpty(mutexName))
                {
                    logger.LogInformation($"OpenMutexW: {mutexName}");
                    string newName = $"{mutexName}_{System.Diagnostics.Process.GetCurrentProcess().Id}";
                    IntPtr newPtr = Marshal.StringToHGlobalUni(newName);
                    try 
                    {
                        return _originalOpenMutexW!(dwDesiredAccess, bInheritHandle, newPtr);
                    } 
                    finally 
                    {
                        Marshal.FreeHGlobal(newPtr);
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogError($"OpenMutexW hook error: {ex}");
            }
            return _originalOpenMutexW!(dwDesiredAccess, bInheritHandle, lpName);
        }

        private nint OpenMutexAHook(uint dwDesiredAccess, bool bInheritHandle, nint lpName)
        {
            try
            {
                string? mutexName = Marshal.PtrToStringAnsi(lpName);
                if (!string.IsNullOrEmpty(mutexName))
                {
                    logger.LogInformation($"OpenMutexA: {mutexName}");
                    string newName = $"{mutexName}_{System.Diagnostics.Process.GetCurrentProcess().Id}";
                    IntPtr newPtr = Marshal.StringToHGlobalAnsi(newName);
                    try 
                    {
                         return _originalOpenMutexA!(dwDesiredAccess, bInheritHandle, newPtr);
                    } 
                    finally 
                    {
                        Marshal.FreeHGlobal(newPtr);
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogError($"OpenMutexA hook error: {ex}");
            }
            return _originalOpenMutexA!(dwDesiredAccess, bInheritHandle, lpName);
        }

        [Function(CallingConventions.Stdcall)]
        [UnmanagedFunctionPointer(CallingConvention.StdCall, SetLastError = true, CharSet = CharSet.Unicode)]
        private delegate nint CreateMutexWDelegate(nint lpMutexAttributes, bool bInitialOwner, nint lpName);

        [Function(CallingConventions.Stdcall)]
        [UnmanagedFunctionPointer(CallingConvention.StdCall, SetLastError = true, CharSet = CharSet.Ansi)]
        private delegate nint CreateMutexADelegate(nint lpMutexAttributes, bool bInitialOwner, nint lpName);

        [Function(CallingConventions.Stdcall)]
        [UnmanagedFunctionPointer(CallingConvention.StdCall, SetLastError = true, CharSet = CharSet.Unicode)]
        private delegate nint OpenMutexWDelegate(uint dwDesiredAccess, bool bInheritHandle, nint lpName);

        [Function(CallingConventions.Stdcall)]
        [UnmanagedFunctionPointer(CallingConvention.StdCall, SetLastError = true, CharSet = CharSet.Ansi)]
        private delegate nint OpenMutexADelegate(uint dwDesiredAccess, bool bInheritHandle, nint lpName);
    }
}
