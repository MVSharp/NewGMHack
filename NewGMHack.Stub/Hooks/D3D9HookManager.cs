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
using SharpDX;
using SharpDX.Direct3D9;
using Rectangle = SharpDX.Rectangle;
using Color = SharpDX.Color;
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
public class OverlayManager(SelfInformation self)
{
    private Font _font;
    private bool _initialized;

    private readonly int RightMargin = 20;
    private readonly int LineHeight = 18;
    private readonly int SectionSpacing = 10;

    public void Initialize(Device device)
    {
        if (_initialized || device == null)
            return;

        var fontDesc = new FontDescription
        {
            Height = 16,
            FaceName = "Consolas",
            Weight = FontWeight.Normal,
            Quality = FontQuality.ClearType,
            PitchAndFamily = FontPitchAndFamily.Default | FontPitchAndFamily.Mono
        };

        _font = new Font(device, fontDesc);
        _initialized = true;
    }

    void DrawRect(Device device, Vector2 pos, int w, int h, System.Drawing.Color color)
    {
        var line = new Line(device);
        line.Width = 1.0f;
        line.Begin();

        var sharpColor = new SharpDX.ColorBGRA(color.R, color.G, color.B, color.A);

        line.Draw(new[] {
            new Vector2(pos.X - w / 2, pos.Y - h / 2),
            new Vector2(pos.X + w / 2, pos.Y - h / 2),
            new Vector2(pos.X + w / 2, pos.Y + h / 2),
            new Vector2(pos.X - w / 2, pos.Y + h / 2),
            new Vector2(pos.X - w / 2, pos.Y - h / 2)
        }, sharpColor);

        line.End();
        line.Dispose();
    }
    Vector2 WorldToScreen(Vector3 worldPos, Matrix view, Matrix proj, Viewport vp)
    {
        Vector4 worldPos4 = new Vector4(worldPos, 1.0f);
        Vector4 clipSpace = Vector4.Transform(worldPos4, view * proj);

        if (clipSpace.W < 0.1f) return Vector2.Zero;

        Vector3 ndc = new Vector3(clipSpace.X, clipSpace.Y, clipSpace.Z) / clipSpace.W;

        return new Vector2(
            (ndc.X + 1.0f) * 0.5f * vp.Width,
            (1.0f - ndc.Y) * 0.5f * vp.Height
        );
    }

    public void DrawEntities(Device device) 
    {
        // Get matrices and viewport
        var viewMatrix= device.GetTransform(TransformState.View);
        var projectionMatrix =device.GetTransform(TransformState.Projection);
        Viewport viewport = device.Viewport;

        var line = new Line(device)
        {
            Width = 1.0f
        };
        line.Begin();

        //foreach (var worldPos in worldPositions)
        //{
            Vector2 screenPos = WorldToScreen(new Vector3(self.PersonInfo.X , self.PersonInfo.Y ,self.PersonInfo.Z ), viewMatrix, projectionMatrix, viewport);

            if (screenPos != Vector2.Zero)
            {
                DrawRect(device, screenPos, 50, 50, System.Drawing.Color.Red);
            }
        //}

        line.End();
        line.Dispose();
    }
    public void DrawUI(Device device)
    {
        if (!_initialized || _font == null || device == null)
            return;

        int screenWidth = device.Viewport.Width;
        int x = screenWidth - 300; // right side
        int y = 50;

        // Draw Features Table
        _font.DrawText(null, "== Hack Features ==(By MichaelVan", new Rectangle(x, y, 300, LineHeight), FontDrawFlags.NoClip, Color.White);
        y += LineHeight + SectionSpacing;

        foreach (var feature in self.ClientConfig.Features)
        {
            string status = feature.IsEnabled ? "🟢 Enabled" : "🔴 Disabled";
            string line = $"{feature.Name,-20} {status}";
            _font.DrawText(null, line, new Rectangle(x, y, 300, LineHeight), FontDrawFlags.NoClip, feature.IsEnabled ? Color.Lime : Color.Red);
            y += LineHeight;
        }

        y += SectionSpacing * 2;

        // Draw Info Table
        _font.DrawText(null, "== Player Info ==", new Rectangle(x, y, 300, LineHeight), FontDrawFlags.NoClip, Color.White);
        y += LineHeight + SectionSpacing;

        DrawInfoRow(device, x, ref y, "PersonId", self.PersonInfo.PersonId.ToString());
        DrawInfoRow(device, x, ref y, "GundamId", self.PersonInfo.GundamId.ToString());
        DrawInfoRow(device, x, ref y, "Weapons", $"{self.PersonInfo.Weapon1}, {self.PersonInfo.Weapon2}, {self.PersonInfo.Weapon3}");
        DrawInfoRow(device, x, ref y, "Position", $"X:{self.PersonInfo.X} Y:{self.PersonInfo.Y} Z:{self.PersonInfo.Z}");
        DrawInfoRow(device, x, ref y, "GundamName", self.PersonInfo.GundamName);
        DrawInfoRow(device, x, ref y, "Slot", self.PersonInfo.Slot.ToString());
    }

    private void DrawInfoRow(Device device, int x, ref int y, string label, string value)
    {
        string line = $"{label,-12}: {value}";
        _font.DrawText(null, line, new Rectangle(x, y, 300, LineHeight), FontDrawFlags.NoClip, Color.LightBlue);
        y += LineHeight;
    }

    public void Reset()
    {
        _font?.Dispose();
        _font = null;
        _initialized = false;
    }
}
    //https://github.com/justinstenning/Direct3DHook/blob/master/Capture/Hook/DXHookD3D9.cs
    public class D3D9HookManager(ILogger<D3D9HookManager> logger , OverlayManager overlayManager) : IHookManager
    {
        [DllImport("user32.dll", SetLastError = true)]
        static extern nint CreateWindowEx(
            int dwExStyle, string lpClassName, string lpWindowName,
            int dwStyle, int x, int y, int nWidth, int nHeight,
            nint hWndParent, nint hMenu, nint hInstance, nint lpParam);

        [DllImport("user32.dll", SetLastError = true)]
        static extern bool DestroyWindow(nint hWnd);
        private const int D3D9_DEVICE_METHOD_COUNT = 119;
        private const int D3D9Ex_DEVICE_METHOD_COUNT = 15;
        private readonly List<nint> id3dDeviceFunctionAddresses = new List<nint>();
        bool _supportsDirect3D9Ex = false;

        private EndSceneDelegate? _endSceneHookDelegate;
        private EndSceneDelegate? _originalEndScene;
        private INativeHook? _endSceneHook;

        private PresentDelegate? _presentHookDelegate;
        private PresentDelegate? _originalPresent;
        private INativeHook? _presentHook;
        private readonly List<INativeHook> _hooks = new();
        private nint _devicePtr = nint.Zero;

        private delegate int ResetDelegate(nint devicePtr, nint presentationParameters);
        private ResetDelegate _resetHookDelegate;
        private JumpHook _resetHook;
        private ResetDelegate _originalReset;
        #region Hook Delegates

        private delegate int EndSceneDelegate(nint devicePtr);
        //DI later , now for test 
        //private readonly OverlayManager _overlayManager;
        private int EndSceneHook(nint devicePtr)
        {
            //logger.ZLogInformation($"EndScene called");
            var device = new Device(devicePtr); 
            try
            {
                device.Viewport = new Viewport(0,0,device.Viewport.Width,device.Viewport.Height,0,1.0f);
                overlayManager.DrawUI(device);
                overlayManager.DrawEntities(device);
            }
            catch (Exception ex)
            {
                logger.ZLogError($"OverlayManager.Draw failed: {ex.GetType().Name} - {ex.Message}");
            }
            
            return _originalEndScene?.Invoke(devicePtr) ?? 0;
        }

        private delegate int PresentDelegate(nint devicePtr, nint srcRect, nint destRect, nint hDestWindowOverride, nint dirtyRegion);
        private int PresentHook(nint devicePtr, nint srcRect, nint destRect, nint hDestWindowOverride, nint dirtyRegion)
        {
            //logger.ZLogInformation($"Present called");
            if(devicePtr != nint.Zero)
            {
                _devicePtr = devicePtr;
            }
            var device = new Device(devicePtr); 

            try
            {
                overlayManager.Initialize(device);
            }
            catch (Exception ex)
            {
                logger.ZLogError($"OverlayManager.Initialize failed: {ex.GetType().Name} - {ex.Message}");
            }
            return _originalPresent?.Invoke(devicePtr, srcRect, destRect, hDestWindowOverride, dirtyRegion) ?? 0;
        }

        #endregion

        private int ResetHook(nint devicePtr, nint presentationParameters)
        {
            logger.ZLogInformation($"Reset called");

            try
            {
                overlayManager.Reset(); // Dispose font and other resources
            }
            catch (Exception ex)
            {
                logger.ZLogError($"OverlayManager.Reset failed: {ex.GetType().Name} - {ex.Message}");
            }

            int result = _originalReset?.Invoke(devicePtr, presentationParameters) ?? 0;

            try
            {
                var device = CppObject.FromPointer<Device>(devicePtr);
                overlayManager.Initialize(device); // Recreate font after reset
            }
            catch (Exception ex)
            {
                logger.ZLogError($"OverlayManager.Initialize after Reset failed: {ex.GetType().Name} - {ex.Message}");
            }

            return result;
        }
        public void HookAll()
        {
            logger.ZLogInformation($"Starting D3D9 hook setup");

            GetD3D9Address();

            if (id3dDeviceFunctionAddresses.Count == 0)
            {
                logger.ZLogError($"No D3D9 function addresses found");
                return;
            }


           // Hook EndScene
            _endSceneHookDelegate = new EndSceneDelegate(EndSceneHook);
            nint hookPtr = Marshal.GetFunctionPointerForDelegate(_endSceneHookDelegate);

            _endSceneHook = JumpHook.Create(
                id3dDeviceFunctionAddresses[(int)Direct3DDevice9FunctionOrdinals.EndScene], // vtable address for EndScene
                hookPtr,
                installAfterCreate: true,
                moduleName: "d3d9.dll",
                functionName: "EndScene"
            );

            _originalEndScene = Marshal.GetDelegateForFunctionPointer<EndSceneDelegate>(_endSceneHook.OriginalFunction);

            _presentHookDelegate = new PresentDelegate(PresentHook);
            nint presentHookPtr = Marshal.GetFunctionPointerForDelegate(_presentHookDelegate);

            _presentHook = JumpHook.Create(
                id3dDeviceFunctionAddresses[(int)Direct3DDevice9FunctionOrdinals.Present], // vtable index for Present
                presentHookPtr,
                installAfterCreate: true,
                moduleName: "d3d9.dll",
                functionName: "Present"
            );

            _resetHookDelegate = new ResetDelegate(ResetHook);
            nint resetHookPtr = Marshal.GetFunctionPointerForDelegate(_resetHookDelegate);

            _resetHook = JumpHook.Create(
                id3dDeviceFunctionAddresses[(int)Direct3DDevice9FunctionOrdinals.Reset], // index 16
                resetHookPtr,
                installAfterCreate: true,
                moduleName: "d3d9.dll",
                functionName: "Reset"
            );

            _originalReset = Marshal.GetDelegateForFunctionPointer<ResetDelegate>(_resetHook.OriginalFunction);
            _originalPresent = Marshal.GetDelegateForFunctionPointer<PresentDelegate>(_presentHook.OriginalFunction);
            _hooks.AddRange([_endSceneHook, _presentHook]);

            logger.ZLogInformation($"D3D9 hooks enabled: EndScene and Present");

        }

        public void UnHookAll()
        {
            foreach (var hook in _hooks)
            {
                hook?.Dispose();
            }
            _hooks.Clear();
        }

        private void GetD3D9Address()
        {
            try
            {
                logger.ZLogInformation($" Begin Get D3d9 Address");
                Device device;
                nint renderPtr = CreateWindowEx(0, "STATIC", "DummyWindow", 0, 0, 0, 1, 1, nint.Zero, nint.Zero, nint.Zero, nint.Zero);
                logger.ZLogInformation($"Render Ptr : {renderPtr}");
                using (Direct3D d3d = new Direct3D())
                {
                    using (device = new Device(d3d, 0, DeviceType.NullReference, nint.Zero, CreateFlags.HardwareVertexProcessing, new PresentParameters() { BackBufferWidth = 1, BackBufferHeight = 1, DeviceWindowHandle = renderPtr }))
                    {
                        id3dDeviceFunctionAddresses.AddRange(GetVTblAddresses(device.NativePointer, D3D9_DEVICE_METHOD_COUNT));
                        logger.ZLogInformation($"Devices Address Count : {id3dDeviceFunctionAddresses.Count}");
                    }
                }
                DestroyWindow(renderPtr);
            }
            catch
            {

            }
        }
        private nint[] GetVTblAddresses(nint pointer, int numberOfMethods)
        {
            return GetVTblAddresses(pointer, 0, numberOfMethods);
        }
        private nint[] GetVTblAddresses(nint pointer, int startIndex, int numberOfMethods)
        {
            List<nint> vtblAddresses = new List<nint>();

            nint vTable = Marshal.ReadIntPtr(pointer);
            for (int i = startIndex; i < startIndex + numberOfMethods; i++)
                vtblAddresses.Add(Marshal.ReadIntPtr(vTable, i * nint.Size)); // using IntPtr.Size allows us to support both 32 and 64-bit processes

            return vtblAddresses.ToArray();
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
}
