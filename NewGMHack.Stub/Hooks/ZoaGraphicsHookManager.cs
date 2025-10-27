using InjectDotnet.NativeHelper;
using Microsoft.Extensions.Logging;
using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;

namespace NewGMHack.Stub.Hooks
{
    public class ZoaGraphicsHookManager : IHookManager
    {
        private readonly ILogger<ZoaGraphicsHookManager> _logger;

        private JumpHook _drawTextureHook;
        private DrawTextureExDelegate _hookDelegate;
        private DrawTextureExDelegate _originalDelegate;

        public ZoaGraphicsHookManager(ILogger<ZoaGraphicsHookManager> logger)
        {
            _logger = logger;
        }

        public void HookAll()
        {
            IntPtr targetAddress = new IntPtr(0x07E20580); // Replace with dynamic scan later if needed

            _hookDelegate = new DrawTextureExDelegate(Hooked_DrawTextureEx);
            IntPtr hookPtr = Marshal.GetFunctionPointerForDelegate(_hookDelegate);

            _drawTextureHook = JumpHook.Create(
                targetAddress,
                hookPtr,
                installAfterCreate: true,
                moduleName: null,
                functionName: "CRenderer::DrawTextureEx"
            );

            _originalDelegate = Marshal.GetDelegateForFunctionPointer<DrawTextureExDelegate>(_drawTextureHook.OriginalFunction);
            _logger.LogInformation("✅ Hooked CRenderer::DrawTextureEx at 0x07E20580.");
        }

        public void UnHookAll()
        {
            _drawTextureHook?.Dispose();
            _logger.LogInformation("🧹 Unhooked CRenderer::DrawTextureEx.");
        }

        private void Hooked_DrawTextureEx(IntPtr ecx, IntPtr edx)
        {
            try
            {
                // ecx is likely the renderer or entity context
                float x = Marshal.PtrToStructure<float>(ecx + 0x88);
                float y = Marshal.PtrToStructure<float>(ecx + 0x8C);
                float z = Marshal.PtrToStructure<float>(ecx + 0x90);

                _logger.LogInformation($"🎯 DrawTextureEx called → Position: X={x:F2}, Y={y:F2}, Z={z:F2}");
            }
            catch (Exception ex)
            {
                _logger.LogError($"❌ Error reading DrawTextureEx position: {ex.Message}");
            }

            _originalDelegate(ecx, edx);
        }

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate void DrawTextureExDelegate(IntPtr ecx, IntPtr edx);
    }
}
