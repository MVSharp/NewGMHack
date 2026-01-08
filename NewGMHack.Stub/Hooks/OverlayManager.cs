using SharpDX;
using SharpDX.Direct3D9;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace NewGMHack.Stub.Hooks;

public class OverlayManager(SelfInformation self)
{
    private SharpDX.Direct3D9.Font _font;
    private bool                   _initialized;

    // Reusable buffers to avoid allocations per frame
    // Reusable buffers to avoid allocations per frame
    private readonly Vector3[] _boxCorners = new Vector3[8];
    private readonly Vector2[] _boxScreen  = new Vector2[8];
    private readonly D3DVertex[] _boxEdgeBuffer = new D3DVertex[24]; // 12 lines * 2 vertices
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

    // FPS Counter
    private readonly Stopwatch _fpsStopwatch = new();
    private int _frameCount = 0;
    private float _fps = 0;
    private long _lastFpsUpdate = 0;

    // Radar settings
    private const float RadarSize = 120f;
    private const float RadarRange = 800f; // World units
    private readonly Vector2[] _radarCircle = new Vector2[CircleSegments + 1];

    // Warning flash
    private const float WarningDistance = 150f;
    
    // Larger font for damage numbers
    private SharpDX.Direct3D9.Font _damageFont;

    #region Static Resources
    // Colors
    private static readonly ColorBGRA ColorWhite = new(255, 255, 255, 255);
    private static readonly ColorBGRA ColorRed = new(255, 0, 0, 255);
    private static readonly ColorBGRA ColorOrange = new(255, 128, 0, 255);
    private static readonly ColorBGRA ColorGreen = new(0, 255, 0, 255);
    private static readonly ColorBGRA ColorBlack = new(0, 0, 0, 255);
    private static readonly ColorBGRA ColorGlassBg = new(0, 0, 0, 100);
    private static readonly ColorBGRA ColorAccentRed = new(255, 60, 60, 200);
    private static readonly ColorBGRA ColorTextWhite = new(230, 230, 230, 255);
    private static readonly ColorBGRA ColorShadow = new(0, 0, 0, 255);
    private static readonly ColorBGRA ColorCrosshair = new(255, 255, 255, 200);
    private static readonly ColorBGRA ColorAimCircle = new(0, 255, 255, 100);
    private static readonly ColorBGRA ColorBestLine = new(0, 255, 255, 140);
    private static readonly ColorBGRA ColorBestCircle = new(0, 255, 255, 180);
    private static readonly ColorBGRA ColorUIEnabled = new(0, 255, 0, 140);
    private static readonly ColorBGRA ColorUIDisabled = new(255, 0, 0, 140);
    private static readonly ColorBGRA ColorUIHeader = new(255, 255, 255, 180);
    private static readonly ColorBGRA ColorInfoLabel = new(173, 216, 230, 140);
    private static readonly ColorBGRA ColorRadarBg = new(100, 100, 100, 80);
    private static readonly ColorBGRA ColorRadarLines = new(80, 80, 80, 60);
    private static readonly ColorBGRA ColorRadarIndicator = new(255, 255, 255, 150);
    private static readonly ColorBGRA ColorRadarPlayer = new(255, 255, 255, 200);
    private static readonly ColorBGRA ColorRadarEnemy = new(255, 50, 50, 180);
    private static readonly ColorBGRA ColorRadarBest = new(255, 255, 0, 220);
    private static readonly ColorBGRA ColorDistText = new(255, 255, 255, 180);

    // Strings
    private const string StrSuperDanger = "!! SUPER DANGER !!";
    private const string StrDanger = "! DANGER !";
    private const string StrTitle = "== SD Hack By Michael Van ==";
    private const string StrPlayerInfo = "== Player Info ==";
    private const string StrEnabled = "Enabled";
    private const string StrDisabled = "Disabled";
    #endregion

    public void Initialize(Device device)
    {
        if (_initialized || device == null) return;
        var fontDesc = new FontDescription
        {
            Height         = 12,
            FaceName       = "Consolas",
            Weight         = FontWeight.Normal,
            Quality        = FontQuality.ClearType,
            PitchAndFamily = FontPitchAndFamily.Default | FontPitchAndFamily.Mono
        };

        _font = new SharpDX.Direct3D9.Font(device, fontDesc);
        
        // Create larger font for damage numbers
        var damageFontDesc = new FontDescription
        {
            Height         = 24,
            FaceName       = "Arial",
            Weight         = FontWeight.Bold,
            Quality        = FontQuality.ClearType,
            PitchAndFamily = FontPitchAndFamily.Default
        };
        _damageFont = new SharpDX.Direct3D9.Font(device, damageFontDesc);
        


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
        const float size = 8f;

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

    private void Draw3DBox(Device device, Matrix viewMatrix, Matrix projMatrix, Viewport viewport, Vector3 center, Vector3 size, int currentHp, int maxHp)
    {

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

        if (!anyVisible) return;

        // Optimized Batch Drawing
        // 12 lines, 2 vertices each = 24 vertices
        // Edges: 0-1, 1-2, 2-3, 3-0 (Bottom)
        //        4-5, 5-6, 6-7, 7-4 (Top)
        //        0-4, 1-5, 2-6, 3-7 (Verticals)

        int intColor = ((boxColor.A & 0xFF) << 24) | ((boxColor.R & 0xFF) << 16) | ((boxColor.G & 0xFF) << 8) | (boxColor.B & 0xFF);
        float rhw = 1f;
        float z = 0f;

        // Helper to set vertex
        void SetV(int idx, int cornerIdx)
        {
            _boxEdgeBuffer[idx].X = _boxScreen[cornerIdx].X;
            _boxEdgeBuffer[idx].Y = _boxScreen[cornerIdx].Y;
            _boxEdgeBuffer[idx].Z = z;
            _boxEdgeBuffer[idx].RHW = rhw;
            _boxEdgeBuffer[idx].Color = intColor;
        }

        // Bottom Face
        SetV(0, 0); SetV(1, 1);
        SetV(2, 1); SetV(3, 2);
        SetV(4, 2); SetV(5, 3);
        SetV(6, 3); SetV(7, 0);

        // Top Face
        SetV(8, 4); SetV(9, 5);
        SetV(10, 5); SetV(11, 6);
        SetV(12, 6); SetV(13, 7);
        SetV(14, 7); SetV(15, 4);

        // Verticals
        SetV(16, 0); SetV(17, 4);
        SetV(18, 1); SetV(19, 5);
        SetV(20, 2); SetV(21, 6);
        SetV(22, 3); SetV(23, 7);

        device.SetTexture(0, null);
        device.VertexFormat = D3DVertex.Format;
        device.DrawUserPrimitives(PrimitiveType.LineList, 12, _boxEdgeBuffer);
    }



    private void DrawLine(Device device, Vector2 p1, Vector2 p2, ColorBGRA color, Viewport? vp = null)
    {
        if (device == null || device.IsDisposed) return;
        if (float.IsNaN(p1.X) || float.IsNaN(p2.X) || p1 == p2) return;
        if (vp.HasValue && ((p1.X < 0 && p2.X < 0) || (p1.X > vp.Value.Width  && p2.X > vp.Value.Width) ||
                            (p1.Y < 0 && p2.Y < 0) || (p1.Y > vp.Value.Height && p2.Y > vp.Value.Height)))
            return;

        int intColor = ((color.A & 0xFF) << 24) | ((color.R & 0xFF) << 16) | ((color.G & 0xFF) << 8) | (color.B & 0xFF);
        
        var vertices = new D3DVertex[]
        {
            new D3DVertex { X = p1.X, Y = p1.Y, Z = 0f, RHW = 1f, Color = intColor },
            new D3DVertex { X = p2.X, Y = p2.Y, Z = 0f, RHW = 1f, Color = intColor }
        };

        device.SetTexture(0, null);
        device.VertexFormat = D3DVertex.Format;
        device.DrawUserPrimitives(PrimitiveType.LineList, 1, vertices);
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
    // New optimized DrawEllipse using DrawUserPrimitives
    void DrawEllipse(Device device, float centerX, float centerY, float radiusX, float radiusY, ColorBGRA color)
    {
        if (device == null || device.IsDisposed) return;

        int intColor = ((color.A & 0xFF) << 24) | ((color.R & 0xFF) << 16) | ((color.G & 0xFF) << 8) | (color.B & 0xFF);
        var vertices = new D3DVertex[CircleSegments + 1];

        // Scale and translate unit circle to buffer and create vertices
        for (int i = 0; i <= CircleSegments; i++)
        {
            vertices[i] = new D3DVertex 
            { 
                X = centerX + radiusX * _unitCircle[i].X,
                Y = centerY + radiusY * _unitCircle[i].Y,
                Z = 0f,
                RHW = 1f,
                Color = intColor
            };
        }
        
        device.SetTexture(0, null);
        device.VertexFormat = D3DVertex.Format;
        device.DrawUserPrimitives(PrimitiveType.LineStrip, CircleSegments, vertices);
    }

    private bool _isDrawing = false;
    
    public void DrawEntities(Device device)
    {
        if (!self.ClientConfig.Features.GetFeature(FeatureName.EnableOverlay).IsEnabled)
            return;

        if (device == null || !_initialized)
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

            // Draw cached Crosshair (white, thin)
            if (_crosshairCacheH != null && _crosshairCacheV != null)
            {
                // We need to implement DrawLine helper for arrays or just loop
                DrawLine(device, _crosshairCacheH[0], _crosshairCacheH[1], ColorCrosshair);
                DrawLine(device, _crosshairCacheV[0], _crosshairCacheV[1], ColorCrosshair);
            }

            // Draw cached Aim Circle (semi-transparent cyan)
            if (_aimCircleCache != null)
            {
                // DrawEllipse uses primitives now, Draw aim circle via primitives too
                // We have _aimCircleCache points, let's just draw them as a LineStrip
                // Or reuse DrawEllipse if we have center/radius. We do: self.AimRadius
                // But _aimCircleCache is pre-calculated vertices.
                // Let's create a helper for drawing a PolyLine/Strip from cache?
                
                // Construct vertices from the cached Vectors
                // This allocates! But it's only once per frame. 
                // Or better: Re-calculate valid vertices on spot? No, use DrawEllipse logic.
                // Center is CrossHairX, CrossHairY. Radius is self.AimRadius.
                DrawEllipse(device, self.CrossHairX, self.CrossHairY, self.AimRadius, self.AimRadius, ColorAimCircle);
            }

            var playerPos = new Vector3(self.PersonInfo.X, self.PersonInfo.Y, self.PersonInfo.Z);

            // NEW: Draw FPS Counter
            DrawFpsCounter(device);

            // NEW: Draw Radar (with camera rotation)
            DrawRadar(device, playerPos, viewport, viewMatrix);
            
            // Loop directly without .ToList() allocation
            var targets = self.Targets; 
            int count = targets.Count;
            float closestDistance = float.MaxValue;
            
            for(int i = 0; i < count; i++)
            {
                var entity = targets[i];
                if (entity.CurrentHp           <= 0        || entity.MaxHp            <= 0) continue;
                if (entity.CurrentHp           > 2_000_000 || entity.MaxHp            > 2_000_000) continue;
                if (entity.EntityPosPtrAddress == 0        || entity.EntityPtrAddress == 0) continue;
                
                Vector2 screenPos = WorldToScreen(entity.Position, viewMatrix, projMatrix, viewport);
                entity.ScreenX = screenPos.X;
                entity.ScreenY = screenPos.Y;

                // Calculate distance for indicator and warning
                float distance = Vector3.Distance(playerPos, entity.Position);
                if (distance < closestDistance) closestDistance = distance;

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
                    Draw3DBox(device, viewMatrix, projMatrix, viewport, entity.Position, new Vector3(100, 100, 100), entity.CurrentHp, entity.MaxHp);

                    // Line from center-top to entity
                    Vector2 centerTop = new(viewport.Width / 2.0f, 10);
                    DrawLine(device, centerTop, screenPos, lineColor, viewport);

                    // NEW: Distance Indicator
                    DrawDistanceIndicator(screenPos, distance, viewport);
                    
                    // NEW: Danger Indicator (SP based)
                    DrawDangerIndicator(screenPos, (uint)entity.Id);

                    if (entity.IsBest)
                    {
                        // Draw line to center (subtle cyan gradient)
                        DrawLine(device, new Vector2(entity.ScreenX, entity.ScreenY), new Vector2(viewport.Width / 2, viewport.Height / 2), ColorBestLine);
                        // Draw small circle around head
                        DrawEllipse(device, entity.ScreenX, entity.ScreenY, 30, 30, ColorBestCircle);
                    }
                }
            }

            // NEW: Warning Flash if enemy close
            DrawWarningFlash(device, closestDistance, viewport);
            
            // NEW: Floating damage numbers
            DrawFloatingDamage(viewport);

            // NEW: Damage Log Panel
            DrawDamageLog(device, viewport);


        }
        catch (Exception ex)
        {
        }
    }

    private static Vector2 GetScreenDirection(Vector3 worldPos, Matrix view, Matrix proj, Viewport viewport, out bool isBehind)
    {
        // Safe check for singular matrix?
        // SharpDX Vector4.Transform should be safe.
        Vector4 clipPos;
        try 
        {
             clipPos = Vector4.Transform(new Vector4(worldPos, 1f), view * proj);
        }
        catch { isBehind = true; return Vector2.Zero; } // Matrix multiplication might overflow?

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
        if (device == null || !_initialized)
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
            var    color  = feature.IsEnabled ? new ColorBGRA(0, 255, 0, 140) : new ColorBGRA(255, 0, 0, 140);
            _font.DrawText(null, line, new Rectangle(x, y, uiWidth, LineHeight), FontDrawFlags.NoClip, color);
            y += LineHeight;
        }

        y += SectionSpacing * 2;
        _font.DrawText(null, "== Player Info ==", new Rectangle(x, y, uiWidth, LineHeight), FontDrawFlags.NoClip, new ColorBGRA(255, 255, 255, 180));
        y += LineHeight + SectionSpacing;

        DrawInfoRow(device, x, ref y, "PersonId", self.PersonInfo.PersonId.ToString());
        DrawInfoRow(device, x, ref y, "PersonName", self.PersonInfo.PlayerName);
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
        var    color = new ColorBGRA(173, 216, 230, 140);
        _font.DrawText(null, line, new Rectangle(x, y, 200, LineHeight), FontDrawFlags.NoClip, color);
        y += LineHeight;
    }

    // ==================== NEW OVERLAY FEATURES ====================

    /// <summary>
    /// Draw distance indicator below entity screen position
    /// </summary>
    private void DrawDistanceIndicator(Vector2 screenPos, float distance, Viewport viewport)
    {
        if (_font == null || screenPos == Vector2.Zero) return;
        if (screenPos.X < 0 || screenPos.X > viewport.Width || screenPos.Y < 0 || screenPos.Y > viewport.Height) return;

        string distText = $"{distance:F0}m";
        int textX = (int)screenPos.X - 15;
        int textY = (int)screenPos.Y + 20;
        _font.DrawText(null, distText, new Rectangle(textX, textY, 60, 14), FontDrawFlags.NoClip, new ColorBGRA(255, 255, 255, 180));
    }

    /// <summary>
    /// Draw a 2D radar/minimap in corner showing entity positions (rotated by camera)
    /// </summary>
    private void DrawRadar(Device device, Vector3 playerPos, Viewport viewport, Matrix viewMatrix)
    {
        // Radar center position (top-left corner with padding)
        float radarX = 20 + RadarSize / 2;
        float radarY = 60 + RadarSize / 2;

        // Extract camera yaw from view matrix (looking at M31, M33 for forward direction in XZ plane)
        // The view matrix's inverse gives us camera orientation
        // Forward direction in world space: (-M13, -M23, -M33) after considering row-major
        // For D3D row-major: forward is (M31, M32, M33) inversed, but we can use M31/M33 for yaw
        float camForwardX = -viewMatrix.M31;
        float camForwardZ = -viewMatrix.M33;
        float cameraYaw = (float)Math.Atan2(camForwardX, camForwardZ) + (float)Math.PI; // +180° to fix inversion

        // Draw radar background circle
        // Reuse DrawEllipse
        DrawEllipse(device, radarX, radarY, RadarSize / 2, RadarSize / 2, ColorRadarBg);

        // Draw cross lines on radar (vertical line = forward direction)
        DrawLine(device, new Vector2(radarX - RadarSize / 2, radarY), new Vector2(radarX + RadarSize / 2, radarY), ColorRadarLines);
        DrawLine(device, new Vector2(radarX, radarY - RadarSize / 2), new Vector2(radarX, radarY + RadarSize / 2), ColorRadarLines);

        // Draw forward indicator (small triangle at top of radar)
        DrawLine(device, new Vector2(radarX - 5, radarY - RadarSize / 2 + 5), new Vector2(radarX, radarY - RadarSize / 2 - 2), ColorRadarIndicator);
        DrawLine(device, new Vector2(radarX + 5, radarY - RadarSize / 2 + 5), new Vector2(radarX, radarY - RadarSize / 2 - 2), ColorRadarIndicator);

        // Draw player at center (white dot)
        DrawEllipse(device, radarX, radarY, 3, 3, ColorRadarPlayer);

        // Precompute rotation values
        float cosYaw = (float)Math.Cos(-cameraYaw);
        float sinYaw = (float)Math.Sin(-cameraYaw);

        // Draw entities as dots relative to player (rotated by camera yaw)
        var targets = self.Targets;
        int count = targets.Count;
        for (int i = 0; i < count; i++)
        {
            var entity = targets[i];
            if (entity.CurrentHp <= 0 || entity.MaxHp <= 0) continue;

            // Calculate relative position in world space
            float dx = entity.Position.X - playerPos.X;
            float dz = entity.Position.Z - playerPos.Z;

            // Rotate by camera yaw so "up" on radar = camera forward
            float rotatedX = dx * cosYaw - dz * sinYaw;
            float rotatedZ = dx * sinYaw + dz * cosYaw;

            // Scale to radar size
            float scale = (RadarSize / 2) / RadarRange;
            float dotX = radarX + rotatedX * scale;
            float dotY = radarY - rotatedZ * scale; // Negative because screen Y is inverted

            // Clamp to radar bounds
            float dist = (float)Math.Sqrt((dotX - radarX) * (dotX - radarX) + (dotY - radarY) * (dotY - radarY));
            if (dist > RadarSize / 2)
            {
                float angle = (float)Math.Atan2(dotY - radarY, dotX - radarX);
                dotX = radarX + (RadarSize / 2 - 3) * (float)Math.Cos(angle);
                dotY = radarY + (RadarSize / 2 - 3) * (float)Math.Sin(angle);
            }

            // Color based on IsBest
            var dotColor = entity.IsBest ? ColorRadarBest : ColorRadarEnemy;
            DrawEllipse(device, dotX, dotY, 4, 4, dotColor);
        }
    }

    /// <summary>
    /// Draw red border flash when enemy is close
    /// </summary>
    private void DrawWarningFlash(Device device, float closestDistance, Viewport viewport)
    {
        if (closestDistance > WarningDistance || closestDistance <= 0) return;

        // Calculate alpha based on distance (closer = more intense)
        float intensity = 1.0f - (closestDistance / WarningDistance);
        float pulse = (float)(Math.Sin(Environment.TickCount / 100.0) * 0.3 + 0.7);
        byte alpha = (byte)(intensity * pulse * 120);

        var flashColor = new ColorBGRA(255, 0, 0, alpha);
        int borderWidth = 8;

        // Top border
        DrawLine(device, new Vector2(0, borderWidth / 2), new Vector2(viewport.Width, borderWidth / 2), flashColor);
        // Bottom border
        DrawLine(device, new Vector2(0, viewport.Height - borderWidth / 2), new Vector2(viewport.Width, viewport.Height - borderWidth / 2), flashColor);
        // Left border
        DrawLine(device, new Vector2(borderWidth / 2, 0), new Vector2(borderWidth / 2, viewport.Height), flashColor);
        // Right border
        DrawLine(device, new Vector2(viewport.Width - borderWidth / 2, 0), new Vector2(viewport.Width - borderWidth / 2, viewport.Height), flashColor);
    }

    /// <summary>
    /// Update and draw FPS counter
    /// </summary>
    private void DrawFpsCounter(Device device)
    {
        if (_font == null) return;

        if (!_fpsStopwatch.IsRunning)
            _fpsStopwatch.Start();

        _frameCount++;
        long elapsed = _fpsStopwatch.ElapsedMilliseconds;

        if (elapsed - _lastFpsUpdate >= 500) // Update every 500ms
        {
            _fps = _frameCount / ((elapsed - _lastFpsUpdate) / 1000f);
            _frameCount = 0;
            _lastFpsUpdate = elapsed;
        }

        string fpsText = $"FPS: {_fps:F0}";
        _font.DrawText(null, fpsText, new Rectangle(20, 20, 80, 16), FontDrawFlags.NoClip, new ColorBGRA(0, 255, 0, 180));
    }

    /// <summary>
    /// Draw damage log panel at bottom-left
    /// </summary>
    // Cached list for rendering to avoid ToArray() allocations
    private readonly List<DamageLog> _logRenderCache = new List<DamageLog>(64);

    /// <summary>
    /// Draw damage log panel at bottom-left
    /// </summary>
    private void DrawDamageLog(Device device, Viewport viewport)
    {
        if (_font == null) return;
        
        long now = Environment.TickCount64;
            
        // 1. Synchronize Cache
        while (self.ReceivedDamageLogs.TryDequeue(out var newLog))
        {
            _logRenderCache.Add(newLog);
        }

        // Remove expired items
        int removeCount = 0;
        for (int i = 0; i < _logRenderCache.Count; i++)
        {
            if (now - _logRenderCache[i].TimeAdded > 1500)
                removeCount++;
            else
                break; 
        }

        if (removeCount > 0)
            _logRenderCache.RemoveRange(0, removeCount);

        // Limit to 48 recent items
        if (_logRenderCache.Count > 10)
        {
            int excessive = _logRenderCache.Count - 10;
            _logRenderCache.RemoveRange(0, excessive);
        }

        if (_logRenderCache.Count == 0) return;

        // 2. Prep Rendering Layout
        int count       = _logRenderCache.Count;
        int drawn       = 0;
        int startX      = 20;
        int bottomY     = viewport.Height - 50;
        int totalHeight = count * 18;
        int startY      = bottomY - totalHeight;

        // CRITICAL FIX: End the Line Batch BEFORE drawing User Primitives or Font
        // 3. END GEOMETRY BATCH
        // We removed _line so no need to End() anything here.
        // if (_isDrawing)
        // {
        //     _line.End();
        //     _isDrawing = false;
        // }

        // PASS 1: Draw All Backgrounds (Geometry Batch via DrawUserPrimitives)
        // This is much faster and safer than ID3DXLine for filled rects
        try 
        {
            for (int i = count - 1; i >= 0; i--)
            {
                int currentY = startY + (drawn * 20);
                
                // Draw Backgrounds using Primitives
                DrawSolidRect(device, startX, currentY, 400, 18, ColorGlassBg);
                DrawSolidRect(device, startX, currentY, 4, 18, ColorAccentRed);
                
                drawn++;
            }
        }
        catch (Exception) { /* If Primitive drawing fails, ignore to prevent crash */ }

        // PASS 2: Draw All Text (Font Batch)
        // Font handles its own Begin/End internally
        drawn = 0;
        for (int i = count - 1; i >= 0; i--)
        {
            var log      = _logRenderCache[i];
            int currentY = startY + (drawn * 20);
            int textX    = startX + 10;

            //Draw Shadow
            Rectangle shadowRect = new Rectangle(textX + 1, currentY + 1, 400, 18);
            _font.DrawText(null, log.Message, shadowRect,
                           FontDrawFlags.Left | FontDrawFlags.VerticalCenter | FontDrawFlags.NoClip, ColorShadow);

            // Draw Main Text
            Rectangle textRect = new Rectangle(textX, currentY, 400, 18);
            _font.DrawText(null, log.Message, textRect,
                           FontDrawFlags.Left | FontDrawFlags.VerticalCenter | FontDrawFlags.NoClip,
                           ColorTextWhite);
            
            drawn++;
        }
    }

    // Vertex Structure for DrawUserPrimitives
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    private struct D3DVertex
    {
        public float X, Y, Z, RHW;
        public int Color;

        public static readonly VertexFormat Format = VertexFormat.PositionRhw | VertexFormat.Diffuse;
    }

    private void DrawSolidRect(Device device, float x, float y, float w, float h, ColorBGRA color)
    {
        if (device == null || device.IsDisposed) return;

        // Convert ColorBGRA to int ARGB expected by D3D9
        // Safest: Pack it manually to avoid library confusion
        int intColor = ((color.A & 0xFF) << 24) | 
                       ((color.R & 0xFF) << 16) | 
                       ((color.G & 0xFF) << 8)  | 
                       (color.B & 0xFF);

        float z = 0f;
        float rhw = 1f;

        var vertices = new D3DVertex[]
        {
            new D3DVertex { X = x,     Y = y,     Z = z, RHW = rhw, Color = intColor }, // Top-Left
            new D3DVertex { X = x + w, Y = y,     Z = z, RHW = rhw, Color = intColor }, // Top-Right
            new D3DVertex { X = x,     Y = y + h, Z = z, RHW = rhw, Color = intColor }, // Bottom-Left
            new D3DVertex { X = x + w, Y = y + h, Z = z, RHW = rhw, Color = intColor }, // Bottom-Right
        };

        // Reset Render States to ensure pure vertex color rendering
        // (Fixes issue where previous ID3DXLine/Font states caused white textures to override color)
        device.SetTexture(0, null);
        device.SetTextureStageState(0, TextureStage.ColorOperation, TextureOperation.SelectArg1);
        device.SetTextureStageState(0, TextureStage.ColorArg1, TextureArgument.Diffuse);
        device.SetTextureStageState(0, TextureStage.AlphaOperation, TextureOperation.SelectArg1);
        device.SetTextureStageState(0, TextureStage.AlphaArg1, TextureArgument.Diffuse);
        
        device.VertexFormat = D3DVertex.Format;
        device.DrawUserPrimitives(PrimitiveType.TriangleStrip, 2, vertices);
    }

    // Active damage numbers being displayed
    private readonly List<FloatingDamage> _activeDamageNumbers = new();
    
    /// <summary>
    /// Draw floating damage numbers that animate upward and fade out
    /// </summary>
    private void DrawFloatingDamage(Viewport viewport)
    {
        if (_damageFont == null) return;
        
        long now = Environment.TickCount64;
        var rng = Random.Shared;
        
        // Pull new damage from queue (max 100 per frame to avoid lag, but fast enough for bursts)
        int pulled = 0;
        int activeCount = _activeDamageNumbers.Count;
        while (self.DamageNumbers.TryDequeue(out var dmg) && pulled < 100)
        {
            // Skip expired items that were stuck in queue
            if (now - dmg.SpawnTime > FloatingDamage.DurationMs) continue;

            //if (dmg.IsReceivedDamage)
            //{
            //    // Position at bottom left for received damage
            //    // Stagger upwards based on count to prevent overlap
            //    float staggerOffset = (activeCount + pulled) * 35;
            //    dmg.X = 150 + (float)(rng.NextDouble() * 20 - 10);
            //    dmg.Y = viewport.Height - 200 - staggerOffset;
            //}
            //else
            //{
                // Position at crosshair for outgoing damage
                float spreadX = (float)(rng.NextDouble() * 120 - 60);  // ±60 pixels
                float spreadY = (float)(rng.NextDouble() * 60 - 30);   // ±30 pixels
                float staggerOffset = (activeCount + pulled) * 25;     // Stagger each by 25px
                
                dmg.X = self.CrossHairX + spreadX;
                dmg.Y = self.CrossHairY - 50 + spreadY - staggerOffset;
            //}
            _activeDamageNumbers.Add(dmg);
            pulled++;
        }
        
        // Remove expired
        _activeDamageNumbers.RemoveAll(d => now - d.SpawnTime > FloatingDamage.DurationMs);
        
        // Draw each active damage number
        foreach (var dmg in _activeDamageNumbers)
        {
            float elapsed = now - dmg.SpawnTime;
            float progress = elapsed / FloatingDamage.DurationMs;  // 0 to 1
            
            // Animation: float upward
            float yOffset = progress * 100f;  // Move up 100 pixels over duration
            float drawY = dmg.Y - yOffset;
            
            // Animation: fade out (keep visible longer before fading)
            float fadeStart = 0.4f;  // Start fading at 40% progress
            byte alpha = progress < fadeStart 
                ? (byte)255 
                : (byte)(255 * (1 - (progress - fadeStart) / (1 - fadeStart)));
            
            ColorBGRA color;
            //if (dmg.IsReceivedDamage)
            //{
            //    // Deep red for damage TAKEN
            //    color = new ColorBGRA(255, 0, 0, alpha);
            //}
            //else
            //{
                // Brighter red/orange for damage DEALT, green for heal
                color = dmg.Amount > 0 
                    ? new ColorBGRA(255, 100, 50, alpha)   // Orange-Red for outgoing
                    : new ColorBGRA(80, 255, 80, alpha);  // Bright green for heal
            //}
            
            string text = dmg.Amount > 0 ? $"-{dmg.Amount}" : $"+{Math.Abs(dmg.Amount)}";
            //if (dmg.IsReceivedDamage) text = $"Hit {text}"; // Optional prefix for clarity
            
            // Use larger damage font
            _damageFont.DrawText(null, text, new Rectangle((int)dmg.X - 50, (int)drawY, 200, 30), 
                FontDrawFlags.Left | FontDrawFlags.NoClip, color);
        }
    }

    private void DrawDangerIndicator(Vector2 screenPos, uint entityId)
    {
        // Lookup by EntityId (which maps to RoomSlot + 1)
        var state = self.BattleState.GetPlayerByEntityId(entityId);
        if (state != null)
        {
            if (state.SP >= 30000)
            {
                // Super Danger
                // Draw slightly above entity
                Rectangle rect = new Rectangle((int)screenPos.X - 60, (int)screenPos.Y - 80, 120, 20);
                _damageFont.DrawText(null, StrSuperDanger, rect, FontDrawFlags.Center | FontDrawFlags.NoClip, ColorRed);
            }
            else if (state.SP > 20000)
            {
                // Danger
                Rectangle rect = new Rectangle((int)screenPos.X - 40, (int)screenPos.Y - 60, 80, 20);
                _damageFont.DrawText(null, StrDanger, rect, FontDrawFlags.Center | FontDrawFlags.NoClip, ColorOrange);
            }
        }
    }

    // ==================== END NEW FEATURES ====================
        public void Reset() 
        {
        _isDrawing = false;

        _font?.Dispose();
        _damageFont?.Dispose();
        //_line?.Dispose();
        //_backgroundTexture?.Dispose();

        _font              = null;
        _damageFont        = null;
        //_line              = null;
        //_backgroundTexture = null;
        _initialized       = false;
        
        // Clear caches
        _aimCircleCache = null;
        _crosshairCacheH = null;
        _crosshairCacheV = null;
        _lastScreenWidth = -1;
        _activeDamageNumbers.Clear();
    }
    public void OnLostDevice()
    {
            _font?.OnLostDevice();
            //_line?.OnLostDevice(); // Removed
    }

    public void OnResetDevice()
    {
            _font?.OnResetDevice();
            //_line?.OnResetDevice(); // Removed
    }
}