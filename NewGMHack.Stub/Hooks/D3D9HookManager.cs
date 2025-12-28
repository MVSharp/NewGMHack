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

    //public class OverlayManager
    //{
    //    private ID3DXFont? _font;
    //    private bool _initialized;

    //    private readonly Color _titleColor = new Color(255, 0, 0, 255); // Red
    //    private readonly Rectangle _titleRect = new Rectangle(10, 10, 400, 30);

    //    public void Initialize(IDirect3DDevice9 device)
    //    {
    //        if (_initialized || device == null)
    //            return;

    //        var fontDesc = new D3DXFontDescription
    //        {
    //            Height = 20,
    //            FaceName = "Arial",
    //            Weight = FontWeight.Bold,
    //            OutputPrecision = FontPrecision.Default,
    //            Quality = FontQuality.Default,
    //            PitchAndFamily = FontPitchAndFamily.Default | FontPitchAndFamily.Roman
    //        };
    //        _font = D3DX9.CreateFont(device, fontDesc);
    //        _initialized = true;
    //    }

    //    public void Draw(IDirect3DDevice9 device)
    //    {
    //        if (!_initialized || _font == null || device == null)
    //            return;

    //        string timestamp = $"NewGmHack:{DateTime.Now:yyyy:MM:dd HH:mm:ss}";
    //        _font.DrawText(null, timestamp, _titleRect, DrawTextFormat.NoClip, _titleColor);
    //    }

    //    public void Reset()
    //    {
    //        _font?.Dispose();
    //        _font = null;
    //        _initialized = false;
    //    }
    //}
    //https://github.com/justinstenning/Direct3DHook/blob/master/Capture/Hook/DXHookD3D9.cs
    //    public class D3D9HookManager(ILogger<D3D9HookManager> logger , OverlayManager overlayManager) : IHookManager
    //    {
    //        [DllImport("user32.dll", SetLastError = true)]
    //        static extern nint CreateWindowEx(
    //            int dwExStyle, string lpClassName, string lpWindowName,
    //            int dwStyle, int x, int y, int nWidth, int nHeight,
    //            nint hWndParent, nint hMenu, nint hInstance, nint lpParam);

    //        [DllImport("user32.dll", SetLastError = true)]
    //        static extern bool DestroyWindow(nint hWnd);
    //        private const int D3D9_DEVICE_METHOD_COUNT = 119;
    //        private const int D3D9Ex_DEVICE_METHOD_COUNT = 15;
    //        private readonly List<nint> id3dDeviceFunctionAddresses = new List<nint>();
    //        bool _supportsDirect3D9Ex = false;

    //        private EndSceneDelegate? _endSceneHookDelegate;
    //        private EndSceneDelegate? _originalEndScene;
    //        private INativeHook? _endSceneHook;

    //        private PresentDelegate? _presentHookDelegate;
    //        private PresentDelegate? _originalPresent;
    //        private INativeHook? _presentHook;
    //        private readonly List<INativeHook> _hooks = new();
    //        private nint _devicePtr = nint.Zero;

    //        private delegate int ResetDelegate(nint devicePtr, nint presentationParameters);
    //        private ResetDelegate _resetHookDelegate;
    //        private JumpHook _resetHook;
    //        private ResetDelegate _originalReset;
    //        #region Hook Delegates

    //        private delegate int EndSceneDelegate(nint devicePtr);
    //        //DI later , now for test 
    //        //private readonly OverlayManager _overlayManager;

    //private DateTime _lastFrameTime = DateTime.Now;
    //private int _frameCount = 0;
    //private double _measuredFps = 60.0;
    //private DateTime _lastFpsUpdate = DateTime.Now;
    //private DateTime _lastOverlayDraw = DateTime.MinValue;
    //private Device _device;           // Cached device
    //private bool _deviceInitialized = false;
    //private int EndSceneHook(nint devicePtr)
    //{
    //    if (devicePtr == 0) return 0;

    //    try
    //    {
    //        // Lazily get or create the Device wrapper
    //        if (_device == null || _device.NativePointer != devicePtr)
    //        {
    //            _device?.Dispose();
    //            _device = new Device(devicePtr);
    //            _deviceInitialized = false; // Force reinitialize
    //            overlayManager.Reset(); // Full reset
    //        }
    //        var coopLevel = _device.TestCooperativeLevel();
    //        if (coopLevel == ResultCode.DeviceLost || coopLevel == ResultCode.DeviceNotReset)
    //        {
    //            //logger.ZLogInformation("Device lost detected. Skipping frame.");
    //            _deviceInitialized = false;
    //                    overlayManager.Reset();
    //            return _originalEndScene?.Invoke(devicePtr) ?? 0;
    //        }

    //        // Initialize only once (or after reset)
    //        if (!_deviceInitialized)
    //        {
    //            overlayManager.Initialize(_device);
    //            _deviceInitialized = true;
    //        }

    //                // At top of DrawEntities, after Begin()
    //                // Now safe to draw
    //        overlayManager.DrawEntities(_device);
    //        overlayManager.DrawUI(_device);
    //                //overlayManager.DrawAimCircle(_device);
    //    }
    //    catch (Exception ex)
    //    {
    //        logger.ZLogError($"OverlayManager.Draw failed: {ex}");
    //    }

    //    return _originalEndScene?.Invoke(devicePtr) ?? 0;
    //}

    //        private delegate int PresentDelegate(nint devicePtr, nint srcRect, nint destRect, nint hDestWindowOverride, nint dirtyRegion);
    //private int PresentHook(nint devicePtr, nint srcRect, nint destRect, nint hDestWindowOverride, nint dirtyRegion)
    //{
    //    if (devicePtr != nint.Zero)
    //    {
    //        _devicePtr = devicePtr; // Keep for Reset hook
    //    }

    //    // DO NOT INITIALIZE HERE — moved to EndScene
    //    return _originalPresent?.Invoke(devicePtr, srcRect, destRect, hDestWindowOverride, dirtyRegion) ?? 0;
    //}
    //        #endregion

    //private int ResetHook(nint devicePtr, nint presentationParameters)
    //{
    //    //logger.ZLogInformation("Device Reset called");

    //    try
    //    {
    //        // 1. Tell overlay to release D3D9 pool resources
    //        overlayManager.OnLostDevice();

    //        // 2. Dispose our cached device wrapper
    //        _device?.Dispose();
    //        _device = null;
    //        _deviceInitialized = false;
    //    }
    //    catch (Exception ex)
    //    {
    //        logger.ZLogError($"Pre-reset cleanup failed: {ex}");
    //    }

    //    // 3. Let the real D3D reset happen
    //    int result = _originalReset?.Invoke(devicePtr, presentationParameters) ?? 0;

    //    try
    //    {
    //        // 4. Recreate device wrapper
    //        _device = new Device(devicePtr);

    //        // 5. Recreate overlay resources
    //        overlayManager.OnResetDevice();     // Recreates internal font/line surfaces
    //        overlayManager.Initialize(_device); // Recreates font, line, texture
    //        _deviceInitialized = true;
    //    }
    //    catch (Exception ex)
    //    {
    //        logger.ZLogError($"Post-reset init failed: {ex}");
    //    }

    //    return result;
    //}
    //        public void HookAll()
    //        {
    //            logger.ZLogInformation($"Starting D3D9 hook setup");

    //            GetD3D9Address();

    //            if (id3dDeviceFunctionAddresses.Count == 0)
    //            {
    //                logger.ZLogError($"No D3D9 function addresses found");
    //                return;
    //            }

    //logger.ZLogInformation($"Setting up EndScene hook...");
    //_endSceneHookDelegate = new EndSceneDelegate(EndSceneHook);
    //nint hookPtr = Marshal.GetFunctionPointerForDelegate(_endSceneHookDelegate);
    //logger.ZLogInformation($"EndScene delegate pointer: {hookPtr:X}");

    //nint endSceneTarget = id3dDeviceFunctionAddresses[(int)Direct3DDevice9FunctionOrdinals.EndScene];
    //logger.ZLogInformation($"EndScene target address: {endSceneTarget:X}");

    //_endSceneHook = JumpHook.Create(
    //    endSceneTarget,
    //    hookPtr,
    //    installAfterCreate: true,
    //    moduleName: "d3d9.dll",
    //    functionName: "EndScene"
    //);

    //if (_endSceneHook == null)
    //{
    //    logger.ZLogError($"Failed to create EndScene hook.");
    //}
    //else
    //{
    //    _originalEndScene = Marshal.GetDelegateForFunctionPointer<EndSceneDelegate>(_endSceneHook.OriginalFunction);
    //    logger.ZLogInformation($"EndScene hook installed. Original function: {_endSceneHook.OriginalFunction:X}");
    //}

    //logger.ZLogInformation($"Setting up Present hook...");
    //_presentHookDelegate = new PresentDelegate(PresentHook);
    //nint presentHookPtr = Marshal.GetFunctionPointerForDelegate(_presentHookDelegate);
    //logger.ZLogInformation($"Present delegate pointer: {presentHookPtr:X}");

    //nint presentTarget = id3dDeviceFunctionAddresses[(int)Direct3DDevice9FunctionOrdinals.Present];
    //logger.ZLogInformation($"Present target address: {presentTarget:X}");

    //_presentHook = JumpHook.Create(
    //    presentTarget,
    //    presentHookPtr,
    //    installAfterCreate: true,
    //    moduleName: "d3d9.dll",
    //    functionName: "Present"
    //);

    //if (_presentHook == null)
    //{
    //    logger.ZLogError($"Failed to create Present hook.");
    //}
    //else
    //{
    //    _originalPresent = Marshal.GetDelegateForFunctionPointer<PresentDelegate>(_presentHook.OriginalFunction);
    //    logger.ZLogInformation($"Present hook installed. Original function: {_presentHook.OriginalFunction:X}");
    //}

    //logger.ZLogInformation($"Setting up Reset hook...");
    //_resetHookDelegate = new ResetDelegate(ResetHook);
    //nint resetHookPtr = Marshal.GetFunctionPointerForDelegate(_resetHookDelegate);
    //logger.ZLogInformation($"Reset delegate pointer: {resetHookPtr:X}");

    //nint resetTarget = id3dDeviceFunctionAddresses[(int)Direct3DDevice9FunctionOrdinals.Reset];
    //logger.ZLogInformation($"Reset target address: {resetTarget:X}");

    //_resetHook = JumpHook.Create(
    //    resetTarget,
    //    resetHookPtr,
    //    installAfterCreate: true,
    //    moduleName: "d3d9.dll",
    //    functionName: "Reset"
    //);

    //if (_resetHook == null)
    //{
    //    logger.ZLogError($"Failed to create Reset hook.");
    //}
    //else
    //{
    //    _originalReset = Marshal.GetDelegateForFunctionPointer<ResetDelegate>(_resetHook.OriginalFunction);
    //    logger.ZLogInformation($"Reset hook installed. Original function: {_resetHook.OriginalFunction:X}");
    //}

    //_hooks.AddRange([_endSceneHook, _presentHook]);

    //logger.ZLogInformation($"D3D9 hooks enabled: EndScene, Present, and Reset");
    //        }

    //        public void UnHookAll()
    //        {
    //            foreach (var hook in _hooks)
    //            {
    //                hook?.Dispose();
    //            }
    //            _hooks.Clear();
    //        }

    //        private void GetD3D9Address()
    //        {
    //            try
    //            {
    //                logger.ZLogInformation($" Begin Get D3d9 Address");
    //                Device device;
    //                nint renderPtr = CreateWindowEx(0, "STATIC", "DummyWindow", 0, 0, 0, 1, 1, nint.Zero, nint.Zero, nint.Zero, nint.Zero);
    //                logger.ZLogInformation($"Render Ptr : {renderPtr}");
    //                using (Direct3D d3d = new Direct3D())
    //                {
    //                    using (device = new Device(d3d, 0, DeviceType.NullReference, nint.Zero, CreateFlags.HardwareVertexProcessing, new PresentParameters() { BackBufferWidth = 1, BackBufferHeight = 1, DeviceWindowHandle = renderPtr }))
    //                    {
    //                        id3dDeviceFunctionAddresses.AddRange(GetVTblAddresses(device.NativePointer, D3D9_DEVICE_METHOD_COUNT));
    //                        logger.ZLogInformation($"Devices Address Count : {id3dDeviceFunctionAddresses.Count}");
    //                    }
    //                }
    //                DestroyWindow(renderPtr);
    //            }
    //            catch
    //            {

    //            }
    //        }
    //        private nint[] GetVTblAddresses(nint pointer, int numberOfMethods)
    //        {
    //            return GetVTblAddresses(pointer, 0, numberOfMethods);
    //        }
    //        private nint[] GetVTblAddresses(nint pointer, int startIndex, int numberOfMethods)
    //        {
    //            List<nint> vtblAddresses = new List<nint>();

    //            nint vTable = Marshal.ReadIntPtr(pointer);
    //            for (int i = startIndex; i < startIndex + numberOfMethods; i++)
    //                vtblAddresses.Add(Marshal.ReadIntPtr(vTable, i * nint.Size)); // using IntPtr.Size allows us to support both 32 and 64-bit processes

    //            return vtblAddresses.ToArray();
    //        }

    //    }
    public class D3D9HookManager(
        ILogger<D3D9HookManager> logger,
        OverlayManager overlayManager,
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
