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

        public void HookAll()
        {
            logger.LogInformation("Initializing CreateMutexW hook");
            HookFunction("kernel32.dll", "CreateMutexW", new CreateMutexWDelegate(CreateMutexWHook), out _createMutexWHook, out _originalCreateMutexW);
        }

        public void UnHookAll()
        {
            _createMutexWHook?.Disable();
            logger.LogInformation("CreateMutexW hook disabled");
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

                logger.LogInformation($"Trampoline: {hook.IsHookActivated}");
                original = hook.OriginalFunction;
                logger.LogInformation($"Activated: {hook.IsHookActivated}| Enabled:{hook.IsHookEnabled} |Hooked {functionName} from {dllName} at 0x{functionPtr:X}");
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
                }
            }
            catch (Exception ex)
            {
                logger.LogError($"CreateMutexW hook error: {ex}");
            }
            return _originalCreateMutexW!(lpMutexAttributes, bInitialOwner, lpName);
        }

        [Function(CallingConventions.Stdcall)]
        [UnmanagedFunctionPointer(CallingConvention.StdCall, SetLastError = true, CharSet = CharSet.Unicode)]
        private delegate nint CreateMutexWDelegate(nint lpMutexAttributes, bool bInitialOwner, nint lpName);
    }
}
