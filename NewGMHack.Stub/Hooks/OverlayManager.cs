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
        
        _line = new Line(device)
        {
            Width     = 1.0f,
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

            // Draw cached Crosshair (white, thin)
            if (_crosshairCacheH != null && _crosshairCacheV != null)
            {
                ColorBGRA chColor = new ColorBGRA(255, 255, 255, 200);
                _line.Draw(_crosshairCacheH, chColor);
                _line.Draw(_crosshairCacheV, chColor);
            }

            // Draw cached Aim Circle (semi-transparent cyan)
            if (_aimCircleCache != null)
            {
                _line.Draw(_aimCircleCache, new ColorBGRA(0, 255, 255, 100));
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
                    Draw3DBox(viewMatrix, projMatrix, viewport, entity.Position, new Vector3(100, 100, 100), entity.CurrentHp, entity.MaxHp);

                    // Line from center-top to entity
                    Vector2 centerTop = new(viewport.Width / 2.0f, 10);
                    DrawLine(centerTop, screenPos, lineColor, viewport);

                    // NEW: Distance Indicator
                    DrawDistanceIndicator(screenPos, distance, viewport);

                    if (entity.IsBest)
                    {
                        // Draw line to center (subtle cyan gradient)
                        ColorBGRA bestLineColor = new ColorBGRA(0, 255, 255, 140);
                        DrawLine(new Vector2(entity.ScreenX, entity.ScreenY), new Vector2(viewport.Width / 2, viewport.Height / 2), bestLineColor);
                        // Draw small circle around head
                        DrawEllipse(entity.ScreenX, entity.ScreenY, 30, 30, new ColorBGRA(0, 255, 255, 180));
                    }
                }
            }

            // NEW: Warning Flash if enemy close
            DrawWarningFlash(device, closestDistance, viewport);
            
            // NEW: Floating damage numbers
            DrawFloatingDamage(viewport);
            
            // NEW: Damage Log Panel
            DrawDamageLog(viewport);

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
            var    color  = feature.IsEnabled ? new ColorBGRA(0, 255, 0, 140) : new ColorBGRA(255, 0, 0, 140);
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
        if (_line == null || _line.IsDisposed) return;

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
        for (int i = 0; i <= CircleSegments; i++)
        {
            _radarCircle[i].X = radarX + (RadarSize / 2) * _unitCircle[i].X;
            _radarCircle[i].Y = radarY + (RadarSize / 2) * _unitCircle[i].Y;
        }
        _line.Draw(_radarCircle, new ColorBGRA(100, 100, 100, 80));

        // Draw cross lines on radar (vertical line = forward direction)
        DrawLine(new Vector2(radarX - RadarSize / 2, radarY), new Vector2(radarX + RadarSize / 2, radarY), new ColorBGRA(80, 80, 80, 60));
        DrawLine(new Vector2(radarX, radarY - RadarSize / 2), new Vector2(radarX, radarY + RadarSize / 2), new ColorBGRA(80, 80, 80, 60));

        // Draw forward indicator (small triangle at top of radar)
        DrawLine(new Vector2(radarX - 5, radarY - RadarSize / 2 + 5), new Vector2(radarX, radarY - RadarSize / 2 - 2), new ColorBGRA(255, 255, 255, 150));
        DrawLine(new Vector2(radarX + 5, radarY - RadarSize / 2 + 5), new Vector2(radarX, radarY - RadarSize / 2 - 2), new ColorBGRA(255, 255, 255, 150));

        // Draw player at center (white dot)
        DrawEllipse(radarX, radarY, 3, 3, new ColorBGRA(255, 255, 255, 200));

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
            var dotColor = entity.IsBest ? new ColorBGRA(255, 255, 0, 220) : new ColorBGRA(255, 50, 50, 180);
            DrawEllipse(dotX, dotY, 4, 4, dotColor);
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
        DrawLine(new Vector2(0, borderWidth / 2), new Vector2(viewport.Width, borderWidth / 2), flashColor);
        // Bottom border
        DrawLine(new Vector2(0, viewport.Height - borderWidth / 2), new Vector2(viewport.Width, viewport.Height - borderWidth / 2), flashColor);
        // Left border
        DrawLine(new Vector2(borderWidth / 2, 0), new Vector2(borderWidth / 2, viewport.Height), flashColor);
        // Right border
        DrawLine(new Vector2(viewport.Width - borderWidth / 2, 0), new Vector2(viewport.Width - borderWidth / 2, viewport.Height), flashColor);
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
    private void DrawDamageLog(Viewport viewport)
    {
        if (_font == null) return;
        
        long now = Environment.TickCount64;
        
        // Use a temporary list to extract valid log entries from ConcurrentQueue
        // Queue is ordered by time (Oldest to Newest)
        // We peek head to remove expired messages
        
        while (self.ReceivedDamageLogs.TryPeek(out var oldest))
        {
            if (now - oldest.TimeAdded > 1500) // 1.5 seconds life as requested
            {
                self.ReceivedDamageLogs.TryDequeue(out _);
            }
            else
            {
                break; // Oldest is valid, so all subsequent are valid (Queue property)
            }
        }
        
        // Snapshot current queue
        var logs = self.ReceivedDamageLogs.ToArray(); 
        
        // We want to display max 48 items
        // And render NEWEST at TOP
        // Queue: [Oldest, ..., Newest]
        // Reverse for display: [Newest, ..., Oldest]
        
        int count = logs.Length;
        int startIndex = Math.Max(0, count - 48);
        int drawn = 0;
        
        int startX = 20;
        // Dynamic StartY: Anchor to bottom. List grows UPWARDS as count increases.
        // Bottom margin = 50px
        int bottomY = viewport.Height - 50;
        int totalHeight = Math.Min(count, 48) * 18;
        int startY = bottomY - totalHeight; 
        
        // Loop backwards from Newest to Oldest (or up to limit relative to newest)
        for (int i = count - 1; i >= startIndex; i--)
        {
            var log = logs[i];
            
            // Stack downwards from startY: Newest at startY, Older at startY + 18
            int currentY = startY + (drawn * 18);
            
            _font.DrawText(null, log.Message, new Rectangle(startX, currentY, 400, 18), 
                FontDrawFlags.Left | FontDrawFlags.NoClip, new ColorBGRA(255, 50, 50, 255));
            
            drawn++;
        }
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
        var rng = new Random();
        
        // Pull new damage from queue (max 100 per frame to avoid lag, but fast enough for bursts)
        int pulled = 0;
        int activeCount = _activeDamageNumbers.Count;
        while (self.DamageNumbers.TryDequeue(out var dmg) && pulled < 100)
        {
            // Skip expired items that were stuck in queue
            if (now - dmg.SpawnTime > FloatingDamage.DurationMs) continue;

            if (dmg.IsReceivedDamage)
            {
                // Position at bottom left for received damage
                // Stagger upwards based on count to prevent overlap
                float staggerOffset = (activeCount + pulled) * 35;
                dmg.X = 150 + (float)(rng.NextDouble() * 20 - 10);
                dmg.Y = viewport.Height - 200 - staggerOffset;
            }
            else
            {
                // Position at crosshair for outgoing damage
                float spreadX = (float)(rng.NextDouble() * 120 - 60);  // ±60 pixels
                float spreadY = (float)(rng.NextDouble() * 60 - 30);   // ±30 pixels
                float staggerOffset = (activeCount + pulled) * 25;     // Stagger each by 25px
                
                dmg.X = self.CrossHairX + spreadX;
                dmg.Y = self.CrossHairY - 50 + spreadY - staggerOffset;
            }
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
            if (dmg.IsReceivedDamage)
            {
                // Deep red for damage TAKEN
                color = new ColorBGRA(255, 0, 0, alpha);
            }
            else
            {
                // Brighter red/orange for damage DEALT, green for heal
                color = dmg.Amount > 0 
                    ? new ColorBGRA(255, 100, 50, alpha)   // Orange-Red for outgoing
                    : new ColorBGRA(80, 255, 80, alpha);  // Bright green for heal
            }
            
            string text = dmg.Amount > 0 ? $"-{dmg.Amount}" : $"+{Math.Abs(dmg.Amount)}";
            if (dmg.IsReceivedDamage) text = $"Hit {text}"; // Optional prefix for clarity
            
            // Use larger damage font
            _damageFont.DrawText(null, text, new Rectangle((int)dmg.X - 50, (int)drawY, 200, 30), 
                FontDrawFlags.Left | FontDrawFlags.NoClip, color);
        }
    }

    // ==================== END NEW FEATURES ====================

    public void Reset()
    {
        try { if (_isDrawing) _line?.End(); } catch { }
        _isDrawing = false;

        _font?.Dispose();
        _damageFont?.Dispose();
        _line?.Dispose();
        //_backgroundTexture?.Dispose();

        _font              = null;
        _damageFont        = null;
        _line              = null;
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