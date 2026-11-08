using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Media;
using WhereAmI.Game;
using WhereAmI.Levels;

namespace WhereAmI.Rendering;

/// <summary>
/// Draws level pieces from their shapes, in the game's silhouette style: dark ground with grass,
/// thorny spikes and moving platforms, plus the warm glow around gates and lanterns.
/// Used for levels that have no painted background, and for pieces that never were painted.
/// </summary>
public static class ProceduralArt
{
    private static readonly Color Ink = Color.FromRgb(0x16, 0x16, 0x17);
    private static readonly Brush InkBrush = Freeze(new SolidColorBrush(Ink));
    private static readonly Brush SkyBrush = Freeze(new LinearGradientBrush(
        new GradientStopCollection
        {
            new(Color.FromRgb(0xCF, 0xD3, 0xD8), 0),
            new(Color.FromRgb(0xE9, 0xEA, 0xE8), 0.55),
            new(Color.FromRgb(0xF4, 0xF2, 0xEC), 1),
        },
        new Point(0, 0), new Point(0, 1)));
    private static readonly Brush WarmGlow = Freeze(RadialGlow(0xFF, 0xB5, 0x47));
    // The static parts of a level never change, so they are built once and reused every frame.
    private static readonly ConditionalWeakTable<LevelDefinition, Drawing> StaticCache = new();

    /// <summary>Foggy sky gradient behind everything, covering the visible area.</summary>
    public static void DrawSky(DrawingContext dc, double visibleWidth, double height) =>
        dc.DrawRectangle(SkyBrush, null, new Rect(0, 0, visibleWidth, height));

    /// <summary>Ground, spikes and the goal gate of a level without a painted background.</summary>
    public static void DrawStaticLevel(DrawingContext dc, LevelDefinition level) =>
        dc.DrawDrawing(StaticCache.GetValue(level, BuildStatic));

    /// <summary>Warm light around a goal gate or a lit lantern, breathing slowly.</summary>
    public static void DrawHalo(DrawingContext dc, Point center, double radiusX, double radiusY, double time, double strength = 0.55)
    {
        var pulse = 0.78 + 0.22 * Math.Sin(time * 2.2 + center.X * 0.01);
        dc.PushOpacity(strength * pulse);
        dc.DrawEllipse(WarmGlow, null, center, radiusX, radiusY);
        dc.Pop();
    }

    public static void DrawPlatform(DrawingContext dc, MovingPlatform platform)
    {
        var b = platform.Bounds;
        dc.DrawRoundedRectangle(InkBrush, null, new Rect(b.X, b.Y, b.Width, b.Height), 6, 6);
        dc.DrawGeometry(InkBrush, null, GrassGeometry(b, seed: (int)b.Width * 7 + (int)b.Height));

        // Roots hanging below show it is floating.
        for (var x = b.X + 10; x < b.Right - 6; x += 22)
            dc.DrawRectangle(InkBrush, null, new Rect(x, b.Bottom - 2, 2, 8 + (x * 13 % 9)));
    }

    private static Drawing BuildStatic(LevelDefinition level)
    {
        var group = new DrawingGroup();
        using (var dc = group.Open())
        {
            foreach (var ground in level.Ground)
            {
                dc.DrawRectangle(InkBrush, null, new Rect(ground.X, ground.Y, ground.Width, ground.Height));
                dc.DrawGeometry(InkBrush, null, GrassGeometry(ground, seed: (int)(ground.X * 31 + ground.Y)));
            }

            foreach (var spikes in level.Spikes)
                dc.DrawGeometry(InkBrush, null, SpikeGeometry(spikes));
        }

        group.Freeze();
        return group;
    }

    /// <summary>Uneven grass blades along the top edge of a block.</summary>
    private static Geometry GrassGeometry(Box block, int seed)
    {
        var random = new Random(seed);
        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            for (var x = block.X; x < block.Right - 2; x += 3 + random.NextDouble() * 4)
            {
                var height = 6 + random.NextDouble() * (random.NextDouble() < 0.15 ? 26 : 12);
                var lean = (random.NextDouble() - 0.5) * 8;
                var width = 2 + random.NextDouble() * 3;
                ctx.BeginFigure(new Point(x, block.Y + 1), true, true);
                ctx.LineTo(new Point(x + width / 2 + lean, block.Y - height), true, false);
                ctx.LineTo(new Point(Math.Min(x + width, block.Right), block.Y + 1), true, false);
            }
        }

        geometry.Freeze();
        return geometry;
    }

    /// <summary>A row of thorns, or a thorny pillar when the box is tall and narrow.</summary>
    private static Geometry SpikeGeometry(Box box)
    {
        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            if (box.Height > box.Width * 1.5)
            {
                // Pillar: a stem with thorns sticking out on both sides.
                var stem = box.Width * 0.45;
                var left = box.CenterX - stem / 2;
                AddRect(ctx, new Rect(left, box.Y, stem, box.Height));
                for (var y = box.Y + 6; y < box.Bottom - 6; y += 14)
                {
                    AddTriangle(ctx, new Point(left, y), new Point(box.X, y + 5), new Point(left, y + 10));
                    AddTriangle(ctx, new Point(left + stem, y + 4), new Point(box.Right, y + 9), new Point(left + stem, y + 14));
                }
            }
            else
            {
                // Row: thorns pointing up, filling the box height.
                var count = Math.Max(1, (int)Math.Round(box.Width / 16));
                var step = box.Width / count;
                for (var i = 0; i < count; i++)
                {
                    var x = box.X + i * step;
                    AddTriangle(ctx, new Point(x, box.Bottom), new Point(x + step / 2, box.Y), new Point(x + step, box.Bottom));
                }
            }
        }

        geometry.Freeze();
        return geometry;
    }

    private static void AddTriangle(StreamGeometryContext ctx, Point a, Point b, Point c)
    {
        ctx.BeginFigure(a, true, true);
        ctx.LineTo(b, true, false);
        ctx.LineTo(c, true, false);
    }

    private static void AddRect(StreamGeometryContext ctx, Rect r)
    {
        ctx.BeginFigure(r.TopLeft, true, true);
        ctx.PolyLineTo(new List<Point> { r.TopRight, r.BottomRight, r.BottomLeft }, true, false);
    }

    private static RadialGradientBrush RadialGlow(byte r, byte g, byte b) => new(
        new GradientStopCollection
        {
            new(Color.FromArgb(0x90, r, g, b), 0),
            new(Color.FromArgb(0x30, r, g, b), 0.5),
            new(Color.FromArgb(0x00, r, g, b), 1),
        });

    private static T Freeze<T>(T brush) where T : Freezable
    {
        brush.Freeze();
        return brush;
    }
}
