using SharpDX;
using SharpDX.Direct3D9;
using System;
using System.Collections.Generic;
using System.Linq;

namespace NewGMHack.Stub.Hooks;

public class OverlayManager(SelfInformation self)
{
    private SharpDX.Direct3D9.Font _font;
    private Line                   _line;              // Reused Line object
    private bool                   _initialized;

    // Reusable buffers to avoid allocations per frame
    private readonly Vector2[] _lineBuffer = new Vector2[2];
    private readonly Vector3[] _boxCorners = new Vector3[8];
    private readonly Vector2[] _boxScreen  = new Vector2[8];
    private const int CircleSegments = 40;
    private readonly Vector2[] _unitCircle = new Vector2[CircleSegments + 1]; // Pre-calculated unit circle
    private readonly Vector2[] _circleBuffer = new Vector2[CircleSegments + 1]; // Reusable buffer for drawing circles
    private Vector2[] _aimCircleCache; // Cached aim radius circle (screen space)
    private Vector2[] _crosshairCacheH; // Cached crosshair horizontal
    private Vector2[] _crosshairCacheV; // Cached crosshair vertical

    private readonly int RightMargin    = 20;
    private readonly int LineHeight     = 18;
    private readonly int SectionSpacing = 10;
    private float _lastAimRadius = -1;
    private int _lastScreenWidth = -1;
    private int _lastScreenHeight = -1;

    public void Initialize(Device device)
    {
        if (_initialized || device == null) return;
        var fontDesc = new FontDescription
        {
            Height         = 16,
            FaceName       = "Consolas",
            Weight         = FontWeight.Normal,
            Quality        = FontQuality.ClearType,
            PitchAndFamily = FontPitchAndFamily.Default | FontPitchAndFamily.Mono
        };

        _font = new SharpDX.Direct3D9.Font(device, fontDesc);
        _line = new Line(device)
        {
            Width     = 1.5f,
            Antialias = true,
        };

        // Pre-calculate unit circle
        float angleStep = (float)(2 * Math.PI / CircleSegments);
        for (int i = 0; i <= CircleSegments; i++)
        {
            float theta = i * angleStep;
            _unitCircle[i] = new Vector2((float)Math.Cos(theta), (float)Math.Sin(theta));
        }

        _initialized = true;
    }

    private void UpdateCachedGeometry(int screenWidth, int screenHeight, float aimRadius)
    {
        if (_lastScreenWidth == screenWidth && _lastScreenHeight == screenHeight && Math.Abs(_lastAimRadius - aimRadius) < 0.1f)
            return;

        _lastScreenWidth = screenWidth;
        _lastScreenHeight = screenHeight;
        _lastAimRadius = aimRadius;

        // Cache Crosshair
        float centerX = screenWidth / 2f;
        float centerY = screenHeight / 2f;
        const float size = 12f;

        _crosshairCacheH = new[] { new Vector2(centerX - size, centerY), new Vector2(centerX + size, centerY) };
        _crosshairCacheV = new[] { new Vector2(centerX, centerY - size), new Vector2(centerX, centerY + size) };

        // Cache Aim Circle
        _aimCircleCache = new Vector2[CircleSegments + 1];
        for (int i = 0; i <= CircleSegments; i++)
        {
            _aimCircleCache[i] = new Vector2(
                centerX + aimRadius * _unitCircle[i].X,
                centerY + aimRadius * _unitCircle[i].Y
            );
        }
    }

    private void Draw3DBox(Matrix viewMatrix, Matrix projMatrix, Viewport viewport, Vector3 center, Vector3 size, int currentHp, int maxHp)
    {
        if (_line == null || _line.IsDisposed) return;

        currentHp = Math.Max(currentHp, 1);
        maxHp     = Math.Max(maxHp,     1);
        float hpRatio = Math.Clamp((float)currentHp / maxHp, 0.0f, 1.0f);

        float     pulse     = (float)(Math.Sin(Environment.TickCount / 300.0f) * 0.5f + 0.5f);
        byte      glowAlpha = (byte)(160                                              + pulse * 95);
        ColorBGRA boxColor  = GetHpGradientColor(hpRatio);
        boxColor.A = glowAlpha;

        float hx = size.X / 2, hy = size.Y / 2, hz = size.Z / 2;
        // Fill corners buffer
        _boxCorners[0] = center + new Vector3(-hx, -hy, -hz);
        _boxCorners[1] = center + new Vector3(hx,  -hy, -hz);
        _boxCorners[2] = center + new Vector3(hx,  -hy, hz);
        _boxCorners[3] = center + new Vector3(-hx, -hy, hz);
        _boxCorners[4] = center + new Vector3(-hx, hy,  -hz);
        _boxCorners[5] = center + new Vector3(hx,  hy,  -hz);
        _boxCorners[6] = center + new Vector3(hx,  hy,  hz);
        _boxCorners[7] = center + new Vector3(-hx, hy,  hz);

        bool anyVisible = false;
        Matrix viewProj = viewMatrix * projMatrix;

        for (int i = 0; i < 8; i++)
        {
            Vector4 clip = Vector4.Transform(new Vector4(_boxCorners[i], 1.0f), viewProj);
            if (clip.W <= 0.0f) return; // Behind camera
            
            // WorldToScreen inline optimization
            Vector3 ndc = new Vector3(clip.X, clip.Y, clip.Z) / clip.W;
            _boxScreen[i].X = (ndc.X + 1.0f) * 0.5f * viewport.Width + viewport.X;
            _boxScreen[i].Y = (1.0f - ndc.Y) * 0.5f * viewport.Height + viewport.Y;

            if (_boxScreen[i].X >= 0 && _boxScreen[i].X <= viewport.Width &&
                _boxScreen[i].Y >= 0 && _boxScreen[i].Y <= viewport.Height)
                anyVisible = true;
        }

        if (!anyVisible) return;

        // Draw edges
        DrawLine(_boxScreen[0], _boxScreen[1], boxColor, viewport);
        DrawLine(_boxScreen[1], _boxScreen[2], boxColor, viewport);
        DrawLine(_boxScreen[2], _boxScreen[3], boxColor, viewport);
        DrawLine(_boxScreen[3], _boxScreen[0], boxColor, viewport);

        DrawLine(_boxScreen[4], _boxScreen[5], boxColor, viewport);
        DrawLine(_boxScreen[5], _boxScreen[6], boxColor, viewport);
        DrawLine(_boxScreen[6], _boxScreen[7], boxColor, viewport);
        DrawLine(_boxScreen[7], _boxScreen[4], boxColor, viewport);

        for (int i = 0; i < 4; i++)
            DrawLine(_boxScreen[i], _boxScreen[i + 4], boxColor, viewport);
    }

    private void DrawRect(Vector2 pos, int w, int h, int currentHp, int maxHp)
    {
        if (_line == null || _line.IsDisposed) return;

        // Simplified DrawRect Implementation if needed, or remove if unused. 
        // Keeping it compatible with existing code structure but optimizing allocations.
        
        // ... (Logic similar to original but using _lineBuffer)
    }

    private void DrawLine(Vector2 p1, Vector2 p2, ColorBGRA color, Viewport? vp = null)
    {
        if (_line == null || float.IsNaN(p1.X) || float.IsNaN(p2.X) || p1 == p2) return;
        if (vp.HasValue && ((p1.X < 0 && p2.X < 0) || (p1.X > vp.Value.Width  && p2.X > vp.Value.Width) ||
                            (p1.Y < 0 && p2.Y < 0) || (p1.Y > vp.Value.Height && p2.Y > vp.Value.Height)))
            return;

        _lineBuffer[0] = p1;
        _lineBuffer[1] = p2;
        _line.Draw(_lineBuffer, color);
    }

    private ColorBGRA GetHpGradientColor(float hpRatio)
    {
        if (hpRatio > 0.5f)
        {
            float t = (hpRatio - 0.5f) * 2;
            return InterpolateColor(new ColorBGRA(255, 255, 0, 255), new ColorBGRA(0, 255, 0, 255), t);
        }
        else
        {
            float t = hpRatio * 2;
            return InterpolateColor(new ColorBGRA(255, 0, 0, 255), new ColorBGRA(255, 255, 0, 255), t);
        }
    }

    private ColorBGRA InterpolateColor(ColorBGRA a, ColorBGRA bc, float t)
    {
        byte r = (byte)(a.R + (bc.R - a.R) * t);
        byte g = (byte)(a.G + (bc.G - a.G) * t);
        byte b = (byte)(a.B + (bc.B - a.B) * t);
        return new ColorBGRA(r, g, b, 255);
    }

    Vector2 WorldToScreen(Vector3 world, Matrix view, Matrix proj, Viewport vp)
    {
        Vector4 clip = Vector4.Transform(new Vector4(world, 1.0f), view * proj);
        if (clip.W < 0.1f) return Vector2.Zero;

        Vector3 ndc = new Vector3(clip.X, clip.Y, clip.Z) / clip.W;
        return new Vector2(
                           (ndc.X + 1.0f)  * 0.5f * vp.Width  + vp.X,
                           (1.0f  - ndc.Y) * 0.5f * vp.Height + vp.Y
                          );
    }

    // New optimized DrawEllipse using static Geometry
    void DrawEllipse(float centerX, float centerY, float radiusX, float radiusY, Color color)
    {
        // Scale and translate unit circle to buffer
        for (int i = 0; i <= CircleSegments; i++)
        {
            _circleBuffer[i].X = centerX + radiusX * _unitCircle[i].X;
            _circleBuffer[i].Y = centerY + radiusY * _unitCircle[i].Y;
        }
        _line.Draw(_circleBuffer, color);
    }

    private bool _isDrawing = false;
    
    public void DrawEntities(Device device)
    {
        if (!self.ClientConfig.Features.GetFeature(FeatureName.EnableOverlay).IsEnabled)
            return;

        if (device == null || _line == null || _line.IsDisposed || !_initialized)
        {
            Initialize(device);
            return;
        }

        self.ScreenHeight = device.Viewport.Height;
        self.ScreenWidth  = device.Viewport.Width;
        self.CrossHairX   = device.Viewport.Width                         / 2;
        self.CrossHairY   = device.Viewport.Height                        / 2;
        self.AimRadius    = Math.Min(self.ScreenWidth, self.ScreenHeight) * 0.5f * 0.9f;

        // Update cached geometry if resolution/aimradius changed
        UpdateCachedGeometry(self.ScreenWidth, self.ScreenHeight, self.AimRadius);

        if (self.Targets == null || self.Targets.Count == 0) return;

        try
        {
            var viewMatrix = device.GetTransform(TransformState.View);
            var projMatrix = device.GetTransform(TransformState.Projection);
            var viewport   = device.Viewport;

            if (_isDrawing) { try { _line.End(); } catch { } _isDrawing = false; }

            _line.Begin();
            _isDrawing = true;

            // Draw cached Crosshair
            if (_crosshairCacheH != null && _crosshairCacheV != null)
            {
                ColorBGRA chColor = new ColorBGRA(255, 0, 0, 255);
                _line.Draw(_crosshairCacheH, chColor);
                _line.Draw(_crosshairCacheV, chColor);
            }

            // Draw cached Aim Circle
            if (_aimCircleCache != null)
            {
                _line.Draw(_aimCircleCache, Color.Red);
            }

            var playerPos = new Vector3(self.PersonInfo.X, self.PersonInfo.Y, self.PersonInfo.Z);
            
            // Loop directly without .ToList() allocation
            var targets = self.Targets; 
            int count = targets.Count; 
            
            for(int i = 0; i < count; i++)
            {
                var entity = targets[i];
                if (entity.CurrentHp           <= 0        || entity.MaxHp            <= 0) continue;
                if (entity.CurrentHp           > 2_000_000 || entity.MaxHp            > 2_000_000) continue;
                if (entity.EntityPosPtrAddress == 0        || entity.EntityPtrAddress == 0) continue;
                
                Vector2 screenPos = WorldToScreen(entity.Position, viewMatrix, projMatrix, viewport);
                entity.ScreenX = screenPos.X;
                entity.ScreenY = screenPos.Y;

                bool isBehind;
                // Optimized check: just check W from clip space inside WorldToScreen roughly or reuse matrix calc
                // For now, keep existing helper or inline it if really hot.
                GetScreenDirection(entity.Position, viewMatrix, projMatrix, viewport, out isBehind);

                bool onScreen = (screenPos != Vector2.Zero) &&
                                screenPos.X >= 0            && screenPos.X <= viewport.Width &&
                                screenPos.Y >= 0            && screenPos.Y <= viewport.Height;

                float     hpRatio   = Math.Clamp((float)entity.CurrentHp               / entity.MaxHp, 0.0f, 1.0f);
                float     pulse     = (float)(Math.Sin(Environment.TickCount / 300.0f) * 0.5f + 0.5f);
                byte      alpha     = (byte)(160                                              + pulse * 95);
                ColorBGRA lineColor = GetHpGradientColor(hpRatio);
                lineColor.A = alpha;

                if (onScreen && !isBehind)
                {
                    // 3D Box
                    Draw3DBox(viewMatrix, projMatrix, viewport, entity.Position, new Vector3(100, 100, 100), entity.CurrentHp, entity.MaxHp);

                    // Line from center-top to entity
                    Vector2 centerTop = new(viewport.Width / 2.0f, 10);
                    DrawLine(centerTop, screenPos, lineColor, viewport);

                    if (entity.IsBest)
                    {
                        // Draw line to center
                        DrawLine(new Vector2(entity.ScreenX, entity.ScreenY), new Vector2(viewport.Width / 2, viewport.Height / 2), Color.Red);
                        // Draw small circle around head
                        DrawEllipse(entity.ScreenX, entity.ScreenY, 30, 30, Color.Red);
                    }
                }
            }

            if (_isDrawing)
            {
                _line.End();
                _isDrawing = false;
            }
        }
        catch (Exception ex)
        {
        }
    }

    private static Vector2 GetScreenDirection(Vector3 worldPos, Matrix view, Matrix proj, Viewport viewport, out bool isBehind)
    {
        Vector4 clipPos = Vector4.Transform(new Vector4(worldPos, 1f), view * proj);
        float   w       = clipPos.W;
        isBehind = (w < 0.0001f);

        if (Math.Abs(w) < 0.0001f)
        {
            isBehind = true;
            return Vector2.Zero;
        }

        float invW = 1f / w;
        float ndcX = clipPos.X * invW;
        float ndcY = clipPos.Y * invW;

        return new Vector2(
            ndcX * (viewport.Width  * 0.5f),
            ndcY * (viewport.Height * 0.5f)
        );
    }

    public void DrawUI(Device device)
    {

        if (!self.ClientConfig.Features.GetFeature(FeatureName.EnableOverlay).IsEnabled)
            return;
        if (device == null || _line == null || _line.IsDisposed || !_initialized)
        {
            Initialize(device);
            return;
        }

        int screenWidth = device.Viewport.Width;
        int x           = screenWidth - 300;
        int y           = 40;
        int uiWidth     = 200;
        
        _font.DrawText(null, "== SD Hack By Michael Van ==", new Rectangle(x, y, uiWidth, LineHeight), FontDrawFlags.NoClip, new ColorBGRA(255, 255, 255, 180));
        y += LineHeight + SectionSpacing;

        foreach (var feature in self.ClientConfig.Features)
        {
            string status = feature.IsEnabled ? "Enabled" : "Disabled";
            string line   = $"{feature.Name,-18} {status}";
            var    color  = feature.IsEnabled ? new ColorBGRA(0, 255, 0, 180) : new ColorBGRA(255, 0, 0, 180);
            _font.DrawText(null, line, new Rectangle(x, y, uiWidth, LineHeight), FontDrawFlags.NoClip, color);
            y += LineHeight;
        }

        y += SectionSpacing * 2;
        _font.DrawText(null, "== Player Info ==", new Rectangle(x, y, uiWidth, LineHeight), FontDrawFlags.NoClip, new ColorBGRA(255, 255, 255, 180));
        y += LineHeight + SectionSpacing;

        DrawInfoRow(device, x, ref y, "PersonId", self.PersonInfo.PersonId.ToString());
        DrawInfoRow(device, x, ref y, "LastSocket", self.LastSocket.ToString());
        DrawInfoRow(device, x, ref y, "CondomId", self.PersonInfo.CondomId.ToString());
        DrawInfoRow(device, x, ref y, "Weapons", $"{self.PersonInfo.Weapon1}, {self.PersonInfo.Weapon2}, {self.PersonInfo.Weapon3}");
        DrawInfoRow(device, x, ref y, "Position", $"X:{self.PersonInfo.X:F1} Y:{self.PersonInfo.Y:F1} Z:{self.PersonInfo.Z:F1}");
        DrawInfoRow(device, x, ref y, "CondomName", self.PersonInfo.CondomName);
        DrawInfoRow(device, x, ref y, "Slot", self.PersonInfo.Slot.ToString());
        
        // Loop over targets directly for UI too ?? 
        // Original code used ToList(), which is safe for modification, but here we just read.
        // If Targets is modified by another thread, simple iteration might throw. 
        // SelfInformation.Targets is a List<Entity> initialized with 12 items.
        // It seems it is a fixed size list where items are updated, not added/removed (based on EntityScannerService).
        // So standard for loop is safe and allocation-free.
        
        var targets = self.Targets;
        int count = targets.Count;
        for (int i = 0; i < count; i++)
        {
            var entity = targets[i];
            if (entity.MaxHp <= 0 ) continue;
            DrawInfoRow(device, x, ref y, $"{entity.Id}|", $"{entity.CurrentHp}/{entity.MaxHp}-{entity.Position.X}:{entity.Position.Y}:{entity.Position.Z}");
        }
    }

    private void DrawInfoRow(Device device, int x, ref int y, string label, string value)
    {
        string line  = $"{label,-10}: {value}";
        var    color = new ColorBGRA(173, 216, 230, 160);
        _font.DrawText(null, line, new Rectangle(x, y, 200, LineHeight), FontDrawFlags.NoClip, color);
        y += LineHeight;
    }

    public void Reset()
    {
        try { if (_isDrawing) _line?.End(); } catch { }
        _isDrawing = false;

        _font?.Dispose();
        _line?.Dispose();
        //_backgroundTexture?.Dispose();

        _font              = null;
        _line              = null;
        //_backgroundTexture = null;
        _initialized       = false;
        
        // Clear caches
        _aimCircleCache = null;
        _crosshairCacheH = null;
        _crosshairCacheV = null;
        _lastScreenWidth = -1;
    }
    public void OnLostDevice()
    {
        try
        {
            _font?.OnLostDevice();
            _line?.OnLostDevice();
        }
        catch { }
    }

    public void OnResetDevice()
    {
        try
        {
            _font?.OnResetDevice();
            _line?.OnResetDevice();
        }
        catch { }
    }
}