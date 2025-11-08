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
    public class OverlayManager(SelfInformation self)
    {
        private SharpDX.Direct3D9.Font _font;
        private Line _line;                    // Reused Line object
        private Texture _backgroundTexture;    // Cached 1x1 texture
        private bool _initialized;

        private readonly int RightMargin = 20;
        private readonly int LineHeight = 18;
        private readonly int SectionSpacing = 10;

        public void Initialize(Device device)
        {
            if (_initialized || device == null) return;
            var fontDesc = new FontDescription
            {
                Height = 16,
                FaceName = "Consolas",
                Weight = FontWeight.Normal,
                Quality = FontQuality.ClearType,
                PitchAndFamily = FontPitchAndFamily.Default | FontPitchAndFamily.Mono
            };

            _font = new SharpDX.Direct3D9.Font(device, fontDesc);
            _line = new Line(device)
            {
                Width = 1.5f,           // ← was 1.0f
                Antialias = true,       // ← critical for visibility
            };

            //CreateBackgroundTexture(device);

            _initialized = true;
        }

        // === 1. CACHED 1x1 TEXTURE ===
        //private void CreateBackgroundTexture(Device device)
        //{
        //    if (_backgroundTexture != null && !_backgroundTexture.Disposed) return;

        //    _backgroundTexture = new Texture(device, 1, 1, 1, Usage.Dynamic, Format.A8R8G8B8, Pool.Default);
        //    var surface = _backgroundTexture.GetSurfaceLevel(0);
        //    var stream = surface.LockRectangle(LockFlags.None);
        //    stream.WriteByte(0);   // B
        //    stream.WriteByte(0);   // G
        //    stream.WriteByte(0);   // R
        //    stream.WriteByte(100); // A (semi-transparent black)
        //    surface.UnlockRectangle();
        //}

        //private void DrawBackgroundBox(Device device, Rectangle area, ColorBGRA color)
        //{
        //    if (_backgroundTexture == null || _backgroundTexture.Disposed)
        //        CreateBackgroundTexture(device);

        //    using (var sprite = new Sprite(device))
        //    {
        //        sprite.Begin(SpriteFlags.AlphaBlend);
        //        var tint = new ColorBGRA(color.R, color.G, color.B, color.A);
        //        sprite.Draw(_backgroundTexture, tint, null, null, new Vector3(area.X, area.Y, 0));
        //        sprite.End();
        //    }
        //}

        // === 3. REFACTORED: Use shared _line ===
        private void Draw3DBox(Matrix viewMatrix, Matrix projMatrix, Viewport viewport, Vector3 center, Vector3 size, int currentHp, int maxHp)
        {
            if (_line == null || _line.IsDisposed) return;

            currentHp = Math.Max(currentHp, 1);
            maxHp = Math.Max(maxHp, 1);
            float hpRatio = Math.Clamp((float)currentHp / maxHp, 0.0f, 1.0f);

            float pulse = (float)(Math.Sin(Environment.TickCount / 300.0f) * 0.5f + 0.5f);
            byte glowAlpha = (byte)(160 + pulse * 95);
            ColorBGRA boxColor = GetHpGradientColor(hpRatio);
            boxColor.A = glowAlpha;

            Vector3[] corners = new Vector3[8];
            float hx = size.X / 2, hy = size.Y / 2, hz = size.Z / 2;
            corners[0] = center + new Vector3(-hx, -hy, -hz);
            corners[1] = center + new Vector3(hx, -hy, -hz);
            corners[2] = center + new Vector3(hx, -hy, hz);
            corners[3] = center + new Vector3(-hx, -hy, hz);
            corners[4] = center + new Vector3(-hx, hy, -hz);
            corners[5] = center + new Vector3(hx, hy, -hz);
            corners[6] = center + new Vector3(hx, hy, hz);
            corners[7] = center + new Vector3(-hx, hy, hz);

            Vector2[] screen = new Vector2[8];
            bool anyVisible = false;
            for (int i = 0; i < 8; i++)
            {
                Vector4 clip = Vector4.Transform(new Vector4(corners[i], 1.0f), viewMatrix * projMatrix);
                if (clip.W <= 0.0f) return;
                screen[i] = WorldToScreen(corners[i], viewMatrix, projMatrix, viewport);
                if (screen[i].X >= 0 && screen[i].X <= viewport.Width &&
                    screen[i].Y >= 0 && screen[i].Y <= viewport.Height)
                    anyVisible = true;
            }
            if (!anyVisible) return;

            // Draw all edges using shared _line
            DrawLine(screen[0], screen[1], boxColor, viewport);
            DrawLine(screen[1], screen[2], boxColor, viewport);
            DrawLine(screen[2], screen[3], boxColor, viewport);
            DrawLine(screen[3], screen[0], boxColor, viewport);

            DrawLine(screen[4], screen[5], boxColor, viewport);
            DrawLine(screen[5], screen[6], boxColor, viewport);
            DrawLine(screen[6], screen[7], boxColor, viewport);
            DrawLine(screen[7], screen[4], boxColor, viewport);

            for (int i = 0; i < 4; i++)
                DrawLine(screen[i], screen[i + 4], boxColor, viewport);
        }

        private void DrawRect(Vector2 pos, int w, int h, int currentHp, int maxHp, float distance = 0)
        {
            if (_line == null || _line.IsDisposed) return;

            currentHp = Math.Max(currentHp, 1);
            maxHp = Math.Max(maxHp, 1);
            float hpRatio = Math.Clamp((float)currentHp / maxHp, 0.0f, 1.0f);

            float width = 100, height = 100;
            float halfW = width / 2, halfH = height / 2;

            string hpText = $"{currentHp}/{maxHp}";
            int textX = (int)(pos.X + halfW - 40);
            int textY = (int)(pos.Y - halfH + 5);
            var textRect = new Rectangle(textX, textY, 100, 20);
            _font.DrawText(null, hpText, textRect, FontDrawFlags.NoClip, SharpDX.Color.White);

            Vector2 tl = new(pos.X - halfW, pos.Y - halfH);
            Vector2 tr = new(pos.X + halfW, pos.Y - halfH);
            Vector2 bl = new(pos.X - halfW, pos.Y + halfH);
            Vector2 br = new(pos.X + halfW, pos.Y + halfH);

            var outerColor = new ColorBGRA(255, 255, 255, 255);
            DrawLine(tl, tr, outerColor);
            DrawLine(tr, br, outerColor);
            DrawLine(br, bl, outerColor);
            DrawLine(bl, tl, outerColor);

            float offset = 3.0f;
            Vector2 itl = new(pos.X - halfW + offset, pos.Y - halfH + offset);
            Vector2 itr = new(pos.X + halfW - offset, pos.Y - halfH + offset);
            Vector2 ibl = new(pos.X - halfW + offset, pos.Y + halfH - offset);
            Vector2 ibr = new(pos.X + halfW - offset, pos.Y + halfH - offset);

            var hpColor = InterpolateColor(new ColorBGRA(0, 255, 0, 255), new ColorBGRA(255, 0, 0, 255), 1.0f - hpRatio);
            DrawLine(itl, itr, hpColor);
            DrawLine(itr, ibr, hpColor);
            DrawLine(ibr, ibl, hpColor);
            DrawLine(ibl, itl, hpColor);

            // Health bar (left side)
            float barW = 5.0f;
            float barH = (height - 2 * offset) * hpRatio;
            Vector2 btl = new(pos.X - halfW - barW - 2, pos.Y - halfH + offset);
            Vector2 bbl = new(pos.X - halfW - barW - 2, pos.Y - halfH + offset + barH);
            Vector2 btr = new(pos.X - halfW - 2, pos.Y - halfH + offset);
            Vector2 bbr = new(pos.X - halfW - 2, pos.Y - halfH + offset + barH);

            DrawLine(btl, bbl, hpColor);
            DrawLine(bbl, bbr, hpColor);
            DrawLine(bbr, btr, hpColor);
            DrawLine(btr, btl, hpColor);
        }

        // Helper: Draw line using shared _line
        private void DrawLine(Vector2 p1, Vector2 p2, ColorBGRA color, Viewport? vp = null)
        {
            if (_line == null || float.IsNaN(p1.X) || float.IsNaN(p2.X) || p1 == p2) return;
            if (vp.HasValue && ((p1.X < 0 && p2.X < 0) || (p1.X > vp.Value.Width && p2.X > vp.Value.Width) ||
                                (p1.Y < 0 && p2.Y < 0) || (p1.Y > vp.Value.Height && p2.Y > vp.Value.Height)))
                return;

            _line.Draw(new[] { p1, p2 }, color);
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
                (ndc.X + 1.0f) * 0.5f * vp.Width + vp.X,
                (1.0f - ndc.Y) * 0.5f * vp.Height + vp.Y
            );
        }

        // === 2. REUSE SINGLE LINE OBJECT ===
        private bool _isDrawing = false;
        void DrawEllipse(Device device, float centerX, float centerY, float radiusX, float radiusY, int segments = 40)
        {
            Vector2[] ellipsePoints = new Vector2[segments + 1];
            float angleStep = (float)(2 * Math.PI / segments);

            for (int i = 0; i <= segments; i++)
            {
                float theta = i * angleStep;
                ellipsePoints[i] = new Vector2(
                    centerX + radiusX * (float)Math.Cos(theta),
                    centerY + radiusY * (float)Math.Sin(theta)
                );
            }
            //_line.Antialias = true;

            _line.Draw(ellipsePoints, Color.Red);

            //_line.Antialias = false;
        }

        void DrawEllipse(Device device, int segments = 40)
        {
            var centerX = self.CrossHairX;
            var centerY = self.CrossHairY;
            var radiusX = self.AimRadius;
            var radiusY = self.AimRadius;
            DrawEllipse(device, centerX, centerY, radiusX, radiusY, segments);
        }
        public void DrawEntities(Device device)
        {

            if (device == null || _line == null || _line.IsDisposed || !_initialized)
            {
                Initialize(device);
                return;
            }

            self.ScreenHeight = device.Viewport.Height;
            self.ScreenWidth = device.Viewport.Width;
            self.CrossHairX = device.Viewport.Width / 2;
            self.CrossHairY = device.Viewport.Height / 2;
            self.AimRadius = Math.Min(self.ScreenWidth, self.ScreenHeight) * 0.5f * 0.9f;
            if (self.Targets == null || self.Targets.Count == 0) return;

            try
            {
                var viewMatrix = device.GetTransform(TransformState.View);
                var projMatrix = device.GetTransform(TransformState.Projection);
                var viewport = device.Viewport;

                if (_isDrawing)
                {
                    try { _line.End(); } catch { }
                    _isDrawing = false;
                }

                _line.Begin();
                _isDrawing = true;
                DrawCrosshair(device);

                DrawEllipse(device);
                var playerPos = new Vector3(self.PersonInfo.X, self.PersonInfo.Y, self.PersonInfo.Z);
                //Vector3 playerFeetWorld = playerPos - new Vector3(0, self.PersonInfo.Y, 0);
                //Vector2 playerScreen = new Vector2(viewport.Width / 2f, viewport.Height / 2f);
                //Vector2 feetScreen = WorldToScreen(playerFeetWorld, viewMatrix, projMatrix, viewport);
                //    if (feetScreen == Vector2.Zero) 
                //        feetScreen = new Vector2(viewport.Width / 2f, viewport.Height - 50f); // fallback
                for (int i = 0; i < self.Targets.Count; i++)
                {
                    var entity = self.Targets[i];
                    if (entity.CurrentHp <= 0 || entity.MaxHp <= 0)
                    {

                        entity.ScreenX = -1;
                        entity.ScreenY = -1;
                        continue;
                    }
                    Vector2 screenPos = WorldToScreen(entity.Position, viewMatrix, projMatrix, viewport);
                    entity.ScreenX = screenPos.X;
                    entity.ScreenY = screenPos.Y;
                }
                foreach (var entity in self.Targets.ToList())
                {
                    if (entity.CurrentHp <= 0 || entity.MaxHp <= 0) continue;
                    float distance = Vector3.Distance(playerPos, entity.Position);
                    bool isBehind;
                    var screenPos = new Vector2(entity.ScreenX, entity.ScreenY);
                    Vector2 dirFromCenter = GetScreenDirection(entity.Position, viewMatrix, projMatrix, viewport, out isBehind);
                    bool onScreen = (screenPos != Vector2.Zero) &&
                                    screenPos.X >= 0 && screenPos.X <= viewport.Width &&
                                    screenPos.Y >= 0 && screenPos.Y <= viewport.Height;

                    float hpRatio = Math.Clamp((float)entity.CurrentHp / entity.MaxHp, 0.0f, 1.0f);
                    float pulse = (float)(Math.Sin(Environment.TickCount / 300.0f) * 0.5f + 0.5f);
                    byte alpha = (byte)(160 + pulse * 95);
                    ColorBGRA lineColor = GetHpGradientColor(hpRatio);
                    lineColor.A = alpha;
                    if (onScreen && !isBehind)
                    {

                        // 3D Box
                        Draw3DBox(viewMatrix, projMatrix, viewport, entity.Position, new Vector3(100, 100, 100), entity.CurrentHp, entity.MaxHp);

                        // Line from center-top to entity — USE HELPER!
                        Vector2 centerTop = new(viewport.Width / 2.0f, 10);
                        DrawLine(centerTop, screenPos, lineColor, viewport);  // ← FIXED
                        if (entity.IsBest)
                        {
                            _line.Draw([new Vector2(entity.ScreenX, entity.ScreenY), new Vector2(device.Viewport.Width / 2, device.Viewport.Height / 2)], Color.Red);
                            DrawEllipse(device, entity.ScreenX, entity.ScreenY, 30, 30, 10);
                        }
                    }
                    else
                    {
                        //DrawOffScreenArrow(
                        //        line: _line,
                        //        playerPos: playerPos,
                        //        enemyPos: entity.Position,
                        //        currentHp: entity.CurrentHp,
                        //        maxHp: entity.MaxHp,
                        //        viewMatrix: viewMatrix,
                        //        projMatrix: projMatrix,
                        //        viewport: viewport,
                        //        maxRange: 4800f
                        //    );
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
        private void DrawCrosshair(Device device)
        {
            if (_line == null || _line.IsDisposed) return;
            var viewport = device.Viewport;
            Vector2 center = new Vector2(viewport.Width / 2f, viewport.Height / 2f);
            ColorBGRA color = new ColorBGRA(255, 0, 0, 255); // Red crosshair
            const float size = 12f; // Half-length of arms
                                    // Horizontal line
            _line.Draw(new[]
            {
center + new Vector2(-size, 0),
center + new Vector2(size, 0)
}, color);
            // Vertical line
            _line.Draw(new[]
            {
center + new Vector2(0, -size),
center + new Vector2(0, size)
}, color);
        }
        private void DrawOffScreenArrow(
            Line line,
            Vector3 playerPos,
            Vector3 enemyPos,
            float currentHp,
            float maxHp,
            Matrix viewMatrix,
            Matrix projMatrix,
            Viewport viewport,
            float maxRange = 800f)
        {
            float distance = Vector3.Distance(playerPos, enemyPos);
            if (distance > maxRange) return;

            // -------------------------------------------------
            // 1. Get direction in **NDC space** (-1..1) – works even behind camera
            // -------------------------------------------------
            Vector4 enemyClip = Vector4.Transform(new Vector4(enemyPos, 1f), viewMatrix * projMatrix);
            if (Math.Abs(enemyClip.W) < 0.0001f) return; // degenerate

            float invW = 1f / enemyClip.W;
            float ndcX = enemyClip.X * invW;
            float ndcY = enemyClip.Y * invW;

            // -------------------------------------------------
            // 2. Convert to screen pixels from centre
            // -------------------------------------------------
            Vector2 screenCenter = new Vector2(viewport.Width * 0.5f, viewport.Height * 0.5f);
            Vector2 dirScreen = new Vector2(
                ndcX * viewport.Width * 0.5f,
                ndcY * viewport.Height * 0.5f
            );

            // If enemy is in front and on-screen → skip (handled by normal ESP)
            bool onScreen = Math.Abs(ndcX) <= 1f && Math.Abs(ndcY) <= 1f && enemyClip.W > 0f;
            if (onScreen) return;

            // -------------------------------------------------
            // 3. Arrow length & thickness by distance
            //     FAR  = LONG + THIN
            //     CLOSE = SHORT + THICK
            // -------------------------------------------------
            float arrowLen = MathUtil.Lerp(30f, 150f, Math.Min(distance / 500f, 1f)); // pixels
            float thickness = MathUtil.Lerp(7f, 1.5f, Math.Min(distance / 400f, 1f));
            line.Width = thickness;

            // Clamp arrow to screen edge
            Vector2 arrowDir = Vector2.Normalize(dirScreen);
            Vector2 arrowEnd = screenCenter + arrowDir * arrowLen;

            // -------------------------------------------------
            // 4. HP color + pulse
            // -------------------------------------------------
            float hpRatio = Math.Clamp(currentHp / maxHp, 0f, 1f);
            float pulse = (float)(Math.Sin(Environment.TickCount / 300.0) * 0.5 + 0.5);
            byte alpha = (byte)(200 + pulse * 55);
            ColorBGRA col = GetHpGradientColor(hpRatio);
            col.A = alpha;

            // -------------------------------------------------
            // 5. DRAW ARROW
            // -------------------------------------------------
            DrawArrow(line, screenCenter, arrowEnd, arrowLen, thickness * 2f, col);

            // -------------------------------------------------
            // 6. DISTANCE TEXT (meters)
            // -------------------------------------------------
            if (_font != null && distance < 600f)
            {
                string txt = $"{distance:F0}m";
                int x = (int)(arrowEnd.X + 6);
                int y = (int)(arrowEnd.Y - 8);

                _font.DrawText(null, txt, x, y, col);
            }
        }
        private static void DrawArrow(
            Line line,
            Vector2 from,          // start point (player screen centre)
            Vector2 to,            // end point (projected enemy position)
            float length,          // length of the arrow shaft (in pixels)
            float headSize,        // size of the arrow head (in pixels)
            ColorBGRA color)
        {
            // shaft
            Vector2 dir = Vector2.Normalize(to - from);
            Vector2 shaftEnd = from + dir * length;

            line.Draw(new[] { from, shaftEnd }, color);

            // arrow head (two short lines)
            Vector2 perp = new Vector2(-dir.Y, dir.X) * headSize;   // perpendicular vector
            line.Draw(new[] { shaftEnd, shaftEnd - dir * headSize + perp }, color);
            line.Draw(new[] { shaftEnd, shaftEnd - dir * headSize - perp }, color);
        }
        private static float GetArrowLength(float distance)
        {
            // 0 m  → 0 px
            // 50 m → 60 px (max size)
            // 500 m → 10 px (tiny)
            const float maxDist = 500f;
            const float maxLen = 60f;
            const float minLen = 10f;

            float t = MathUtil.Clamp(1f - (distance / maxDist), 0f, 1f);
            return MathUtil.Lerp(minLen, maxLen, t);
        }
        private static Vector2 GetScreenDirection(Vector3 worldPos, Matrix view, Matrix proj, Viewport viewport, out bool isBehind)
        {
            Vector4 clipPos = Vector4.Transform(new Vector4(worldPos, 1f), view * proj);
            float w = clipPos.W;
            isBehind = (w < 0.0001f);

            if (Math.Abs(w) < 0.0001f)  // Degenerate (on camera plane)
            {
                isBehind = true;
                return Vector2.Zero;  // Or handle as behind
            }

            float invW = 1f / w;
            // NDC coords (-1 to 1)
            float ndcX = clipPos.X * invW;
            float ndcY = clipPos.Y * invW;

            // Direction from screen center in pixel space
            Vector2 center = new Vector2(viewport.Width * 0.5f, viewport.Height * 0.5f);
            Vector2 dirFromCenter = new Vector2(
                ndcX * (viewport.Width * 0.5f),
                ndcY * (viewport.Height * 0.5f)
            );

            return dirFromCenter;  // Raw direction vec (can be negative/large for behind/off)
        }
        private void DrawEdgeIndicator(Vector2 dirFromCenter, ColorBGRA color, Viewport viewport, float indicatorSize = 10f)
        {
            Vector2 center = new Vector2(viewport.Width * 0.5f, viewport.Height * 0.5f);
            if (dirFromCenter.Length() < 0.001f) return;  // Invalid dir

            Vector2 dir = Vector2.Normalize(dirFromCenter);
            // Find scale to hit nearest edge (ray from center in dir)
            float scaleX = float.MaxValue;
            if (dir.X != 0)
                scaleX = dir.X > 0 ? (viewport.Width * 0.5f) / dir.X : (viewport.Width * -0.5f) / dir.X;
            float scaleY = float.MaxValue;
            if (dir.Y != 0)
                scaleY = dir.Y > 0 ? (viewport.Height * 0.5f) / dir.Y : (viewport.Height * -0.5f) / dir.Y;
            float scale = Math.Min(scaleX, scaleY);

            Vector2 edgePos = center + (dir * scale);

            // Clamp to exact edge (float precision)
            edgePos.X = Math.Max(0, Math.Min(viewport.Width, edgePos.X));
            edgePos.Y = Math.Max(0, Math.Min(viewport.Height, edgePos.Y));

            // Draw simple arrow: short line toward entity + head
            Vector2 arrowTip = edgePos + (dir * indicatorSize * 0.5f);  // Extend slightly off-edge if wanted
            _line.Draw(new[] { edgePos, arrowTip }, color);  // Main arrow shaft

            // Arrow head (two short lines)
            Vector2 perp = new Vector2(-dir.Y, dir.X) * (indicatorSize * 0.3f);  // Perp vector
            _line.Draw(new[] { arrowTip, arrowTip - perp }, color);
            _line.Draw(new[] { arrowTip, arrowTip + perp }, color);
        }
        public void DrawUI(Device device)
        {
            if (device == null || _line == null || _line.IsDisposed || !_initialized)
            {
                Initialize(device);
                return;
            }

            int screenWidth = device.Viewport.Width;
            int x = screenWidth - 300;
            int y = 40;
            int uiWidth = 200;
            int totalHeight = 220;

            var bgColor = new ColorBGRA(0, 0, 0, 100);
            //DrawBackgroundBox(device, new Rectangle(x - 10, y - 10, uiWidth + 20, totalHeight), bgColor);

            _font.DrawText(null, "== Hack Features == (By MichaelVan)", new Rectangle(x, y, uiWidth, LineHeight), FontDrawFlags.NoClip, new ColorBGRA(255, 255, 255, 180));
            y += LineHeight + SectionSpacing;

            foreach (var feature in self.ClientConfig.Features)
            {
                string status = feature.IsEnabled ? "Enabled" : "Disabled";
                string line = $"{feature.Name,-18} {status}";
                var color = feature.IsEnabled ? new ColorBGRA(0, 255, 0, 180) : new ColorBGRA(255, 0, 0, 180);
                _font.DrawText(null, line, new Rectangle(x, y, uiWidth, LineHeight), FontDrawFlags.NoClip, color);
                y += LineHeight;
            }

            y += SectionSpacing * 2;
            _font.DrawText(null, "== Player Info ==", new Rectangle(x, y, uiWidth, LineHeight), FontDrawFlags.NoClip, new ColorBGRA(255, 255, 255, 180));
            y += LineHeight + SectionSpacing;

            DrawInfoRow(device, x, ref y, "PersonId", self.PersonInfo.PersonId.ToString());
            DrawInfoRow(device, x, ref y, "GundamId", self.PersonInfo.GundamId.ToString());
            DrawInfoRow(device, x, ref y, "Weapons", $"{self.PersonInfo.Weapon1}, {self.PersonInfo.Weapon2}, {self.PersonInfo.Weapon3}");
            DrawInfoRow(device, x, ref y, "Position", $"X:{self.PersonInfo.X:F1} Y:{self.PersonInfo.Y:F1} Z:{self.PersonInfo.Z:F1}");
            DrawInfoRow(device, x, ref y, "GundamName", self.PersonInfo.GundamName);
            DrawInfoRow(device, x, ref y, "Slot", self.PersonInfo.Slot.ToString());
            int i = 1;
            foreach (var entity in self.Targets.ToList())
            {
                if (entity.MaxHp <= 0 || entity.MaxHp >= 30000) continue;
                DrawInfoRow(device, x, ref y, $"{entity.Id}|", $"{entity.CurrentHp}/{entity.MaxHp}-{entity.Position.X}:{entity.Position.Y}:{entity.Position.Z}");
                i++;
            }
        }

        private void DrawInfoRow(Device device, int x, ref int y, string label, string value)
        {
            string line = $"{label,-10}: {value}";
            var color = new ColorBGRA(173, 216, 230, 160);
            _font.DrawText(null, line, new Rectangle(x, y, 200, LineHeight), FontDrawFlags.NoClip, color);
            y += LineHeight;
        }

        // === 7. FULL DISPOSAL ===
        public void Reset()
        {
            try { if (_isDrawing) _line?.End(); } catch { }
            _isDrawing = false;

            _font?.Dispose();
            _line?.Dispose();
            _backgroundTexture?.Dispose();

            _font = null;
            _line = null;
            _backgroundTexture = null;
            _initialized = false;
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
