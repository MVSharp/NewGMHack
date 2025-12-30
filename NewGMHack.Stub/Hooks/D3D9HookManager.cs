using InjectDotnet.NativeHelper;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using ZLogger;
//using Vortice.Direct3D9;
//using Vortice.Mathematics;
//using Color = Vortice.Mathematics.Color;
using SharpDX.Direct3D9;
using Reloaded.Hooks.Definitions;
using Reloaded.Hooks.Definitions.X86;
using Squalr.Engine.Utils.Extensions;
namespace NewGMHack.Stub.Hooks
{

       public class D3D9HookManager(
        ILogger<D3D9HookManager> logger,
        OverlayManager overlayManager,
        SelfInformation self,
        IReloadedHooks reloadedHooks // Injected Reloaded.Hooks engine
    ) : IHookManager
    {
        [DllImport("user32.dll", SetLastError = true)]
        private static extern nint CreateWindowEx(int dwExStyle, string lpClassName, string lpWindowName,
            int dwStyle, int x, int y, int nWidth, int nHeight,
            nint hWndParent, nint hMenu, nint hInstance, nint lpParam);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool DestroyWindow(nint hWnd);

        private const int D3D9_DEVICE_METHOD_COUNT = 119;
        private readonly List<nint> _vtableAddresses = [];

        private IHook<EndSceneDelegate>? _endSceneHook;
        private IHook<PresentDelegate>? _presentHook;
        private IHook<ResetDelegate>? _resetHook;

        private EndSceneDelegate? _originalEndScene;
        private PresentDelegate? _originalPresent;
        private ResetDelegate? _originalReset;

        private Device? _device;
        private bool _deviceInitialized;
        private nint _devicePtr;

        [Function(CallingConventions.Stdcall)]
        private delegate int EndSceneDelegate(nint devicePtr);

        [Function(CallingConventions.Stdcall)]
        private delegate int PresentDelegate(nint devicePtr, nint srcRect, nint destRect, nint hDestWindowOverride, nint dirtyRegion);

        [Function(CallingConventions.Stdcall)]
        private delegate int ResetDelegate(nint devicePtr, nint presentationParameters);

        public void HookAll()
        {
            logger.LogInformation($"Initializing D3D9 hooks using Reloaded.Hooks");
            GetD3D9Addresses();

            if (_vtableAddresses.Count == 0)
            {
                logger.LogError($"No D3D9 vtable addresses found.");
                return;
            }

            HookMethod("EndScene", (int)Direct3DDevice9FunctionOrdinals.EndScene, new EndSceneDelegate(EndSceneHook), out _endSceneHook, out _originalEndScene);
            HookMethod("Present", (int)Direct3DDevice9FunctionOrdinals.Present, new PresentDelegate(PresentHook), out _presentHook, out _originalPresent);
            HookMethod("Reset", (int)Direct3DDevice9FunctionOrdinals.Reset, new ResetDelegate(ResetHook), out _resetHook, out _originalReset);
            logger.LogInformation($"D3D9 hooks installed: EndScene, Present, Reset");
        }

        public void UnHookAll()
        {
            _endSceneHook?.Disable();
            _presentHook?.Disable();
            _resetHook?.Disable();
            logger.LogInformation($"D3D9 hooks disabled");
        }

        private void HookMethod<T>(string name, int ordinal, T hookDelegate, out IHook<T>? hook, out T? original) where T : Delegate
        {
            hook = null;
            original = null;

            try
            {
                var target = _vtableAddresses[ordinal];
                hook = reloadedHooks.CreateHook(hookDelegate, target);
                hook.Activate();
                hook.Enable();
                logger.LogInformation($"Trampoline: {hook.PrintDebugTag()}");
                original = hook.OriginalFunction;
                logger.LogInformation($"Activated:{hook.IsHookActivated}|Enabled:{hook.IsHookEnabled}|{name} hook installed at 0x{target:X}");
            }
            catch (Exception ex)
            {
                logger.LogError($"Failed to hook {name}: {ex}");
            }
        }

        private int EndSceneHook(nint devicePtr)
        {
            if (devicePtr == 0) return 0;

            try
            {
                if (_device == null || _device.NativePointer != devicePtr)
                {
                    _device?.Dispose();
                    _device = new Device(devicePtr);
                    _deviceInitialized = false;
                    overlayManager.Reset();
                }

                var coopLevel = _device.TestCooperativeLevel();
                if (coopLevel == ResultCode.DeviceLost || coopLevel == ResultCode.DeviceNotReset)
                {
                    _deviceInitialized = false;
                    overlayManager.Reset();
                    return _originalEndScene?.Invoke(devicePtr) ?? 0;
                }

                if (!_deviceInitialized)
                {
                    overlayManager.Initialize(_device);
                    _deviceInitialized = true;
                }
                overlayManager.DrawEntities(_device);
                overlayManager.DrawUI(_device);
            }
            catch (Exception ex)
            {
                logger.LogError($"EndSceneHook error: {ex}");
            }

            return _originalEndScene?.Invoke(devicePtr) ?? 0;
        }

        private int PresentHook(nint devicePtr, nint srcRect, nint destRect, nint hDestWindowOverride, nint dirtyRegion)
        {
            if (devicePtr != nint.Zero)
            {
                _devicePtr = devicePtr;
            }

            if (self.ClientConfig.Features.IsFeatureEnable(FeatureName.BackGroundMode))
            {
                Thread.Sleep(350);
            }
            return _originalPresent?.Invoke(devicePtr, srcRect, destRect, hDestWindowOverride, dirtyRegion) ?? 0;
        }

        private int ResetHook(nint devicePtr, nint presentationParameters)
        {
            try
            {
                overlayManager.OnLostDevice();
                _device?.Dispose();
                _device = null;
                _deviceInitialized = false;
            }
            catch (Exception ex)
            {
                logger.LogError($"ResetHook pre-cleanup error: {ex}");
            }

            int result = _originalReset?.Invoke(devicePtr, presentationParameters) ?? 0;

            try
            {
                _device = new Device(devicePtr);
                overlayManager.OnResetDevice();
                overlayManager.Initialize(_device);
                _deviceInitialized = true;
            }
            catch (Exception ex)
            {
                logger.LogError($"ResetHook post-init error: {ex}");
            }

            return result;
        }

        private void GetD3D9Addresses()
        {
            try
            {
                logger.LogInformation($"Getting D3D9 vtable addresses");
                nint hwnd = CreateWindowEx(0, "STATIC", "DummyWindow", 0, 0, 0, 1, 1, nint.Zero, nint.Zero, nint.Zero, nint.Zero);
                using var d3d = new Direct3D();
                using var device = new Device(d3d, 0, DeviceType.NullReference, hwnd,
                    CreateFlags.HardwareVertexProcessing,
                    new PresentParameters { BackBufferWidth = 1, BackBufferHeight = 1, DeviceWindowHandle = hwnd });

                _vtableAddresses.AddRange(GetVTblAddresses(device.NativePointer, D3D9_DEVICE_METHOD_COUNT));
                DestroyWindow(hwnd);
                logger.LogInformation($"Retrieved {_vtableAddresses.Count} vtable addresses");
            }
            catch (Exception ex)
            {
                logger.LogError($"GetD3D9Addresses error: {ex}");
            }
        }

        private static IEnumerable<nint> GetVTblAddresses(nint pointer, int count)
        {
            var vtbl = Marshal.ReadIntPtr(pointer);
            for (int i = 0; i < count; i++)
            {
                yield return Marshal.ReadIntPtr(vtbl, i * IntPtr.Size);
            }
        }
    }
}
        #region https://github.com/justinstenning/Direct3DHook/blob/master/Capture/Hook/D3D9.cs
        // https://github.com/justinstenning/Direct3DHook/blob/master/Capture/Hook/D3D9.cs
        public enum Direct3DDevice9FunctionOrdinals : short
        {
            QueryInterface = 0,
            AddRef = 1,
            Release = 2,
            TestCooperativeLevel = 3,
            GetAvailableTextureMem = 4,
            EvictManagedResources = 5,
            GetDirect3D = 6,
            GetDeviceCaps = 7,
            GetDisplayMode = 8,
            GetCreationParameters = 9,
            SetCursorProperties = 10,
            SetCursorPosition = 11,
            ShowCursor = 12,
            CreateAdditionalSwapChain = 13,
            GetSwapChain = 14,
            GetNumberOfSwapChains = 15,
            Reset = 16,
            Present = 17,
            GetBackBuffer = 18,
            GetRasterStatus = 19,
            SetDialogBoxMode = 20,
            SetGammaRamp = 21,
            GetGammaRamp = 22,
            CreateTexture = 23,
            CreateVolumeTexture = 24,
            CreateCubeTexture = 25,
            CreateVertexBuffer = 26,
            CreateIndexBuffer = 27,
            CreateRenderTarget = 28,
            CreateDepthStencilSurface = 29,
            UpdateSurface = 30,
            UpdateTexture = 31,
            GetRenderTargetData = 32,
            GetFrontBufferData = 33,
            StretchRect = 34,
            ColorFill = 35,
            CreateOffscreenPlainSurface = 36,
            SetRenderTarget = 37,
            GetRenderTarget = 38,
            SetDepthStencilSurface = 39,
            GetDepthStencilSurface = 40,
            BeginScene = 41,
            EndScene = 42,
            Clear = 43,
            SetTransform = 44,
            GetTransform = 45,
            MultiplyTransform = 46,
            SetViewport = 47,
            GetViewport = 48,
            SetMaterial = 49,
            GetMaterial = 50,
            SetLight = 51,
            GetLight = 52,
            LightEnable = 53,
            GetLightEnable = 54,
            SetClipPlane = 55,
            GetClipPlane = 56,
            SetRenderState = 57,
            GetRenderState = 58,
            CreateStateBlock = 59,
            BeginStateBlock = 60,
            EndStateBlock = 61,
            SetClipStatus = 62,
            GetClipStatus = 63,
            GetTexture = 64,
            SetTexture = 65,
            GetTextureStageState = 66,
            SetTextureStageState = 67,
            GetSamplerState = 68,
            SetSamplerState = 69,
            ValidateDevice = 70,
            SetPaletteEntries = 71,
            GetPaletteEntries = 72,
            SetCurrentTexturePalette = 73,
            GetCurrentTexturePalette = 74,
            SetScissorRect = 75,
            GetScissorRect = 76,
            SetSoftwareVertexProcessing = 77,
            GetSoftwareVertexProcessing = 78,
            SetNPatchMode = 79,
            GetNPatchMode = 80,
            DrawPrimitive = 81,
            DrawIndexedPrimitive = 82,
            DrawPrimitiveUP = 83,
            DrawIndexedPrimitiveUP = 84,
            ProcessVertices = 85,
            CreateVertexDeclaration = 86,
            SetVertexDeclaration = 87,
            GetVertexDeclaration = 88,
            SetFVF = 89,
            GetFVF = 90,
            CreateVertexShader = 91,
            SetVertexShader = 92,
            GetVertexShader = 93,
            SetVertexShaderConstantF = 94,
            GetVertexShaderConstantF = 95,
            SetVertexShaderConstantI = 96,
            GetVertexShaderConstantI = 97,
            SetVertexShaderConstantB = 98,
            GetVertexShaderConstantB = 99,
            SetStreamSource = 100,
            GetStreamSource = 101,
            SetStreamSourceFreq = 102,
            GetStreamSourceFreq = 103,
            SetIndices = 104,
            GetIndices = 105,
            CreatePixelShader = 106,
            SetPixelShader = 107,
            GetPixelShader = 108,
            SetPixelShaderConstantF = 109,
            GetPixelShaderConstantF = 110,
            SetPixelShaderConstantI = 111,
            GetPixelShaderConstantI = 112,
            SetPixelShaderConstantB = 113,
            GetPixelShaderConstantB = 114,
            DrawRectPatch = 115,
            DrawTriPatch = 116,
            DeletePatch = 117,
            CreateQuery = 118,
        }

        public enum Direct3DDevice9ExFunctionOrdinals : short
        {
            SetConvolutionMonoKernel = 119,
            ComposeRects = 120,
            PresentEx = 121,
            GetGPUThreadPriority = 122,
            SetGPUThreadPriority = 123,
            WaitForVBlank = 124,
            CheckResourceResidency = 125,
            SetMaximumFrameLatency = 126,
            GetMaximumFrameLatency = 127,
            CheckDeviceState_ = 128,
            CreateRenderTargetEx = 129,
            CreateOffscreenPlainSurfaceEx = 130,
            CreateDepthStencilSurfaceEx = 131,
            ResetEx = 132,
            GetDisplayModeEx = 133,
        }
        #endregion
