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
/// thorny spikes, a glowing gate, lantern checkpoints and moving platforms.
/// Used for levels that have no painted background, and for pieces that never were painted.
/// </summary>
public static class ProceduralArt
{
    private static readonly Color Ink = Color.FromRgb(0x16, 0x16, 0x17);
    private static readonly Brush InkBrush = Freeze(new SolidColorBrush(Ink));
    private static readonly Brush GlassBrush = Freeze(new SolidColorBrush(Color.FromRgb(0x3A, 0x3D, 0x42)));
    private static readonly Brush SkyBrush = Freeze(new LinearGradientBrush(
        new GradientStopCollection
        {
            new(Color.FromRgb(0xCF, 0xD3, 0xD8), 0),
            new(Color.FromRgb(0xE9, 0xEA, 0xE8), 0.55),
            new(Color.FromRgb(0xF4, 0xF2, 0xEC), 1),
        },
        new Point(0, 0), new Point(0, 1)));
    private static readonly Brush WarmGlow = Freeze(RadialGlow(0xFF, 0xB5, 0x47));
    private static readonly Brush GateLight = Freeze(new RadialGradientBrush(
        new GradientStopCollection
        {
            new(Color.FromRgb(0xFF, 0xF4, 0xD6), 0),
            new(Color.FromRgb(0xFF, 0xC8, 0x6A), 0.55),
            new(Color.FromRgb(0xE0, 0x86, 0x20), 1),
        }));

    // The static parts of a level never change, so they are built once and reused every frame.
    private static readonly ConditionalWeakTable<LevelDefinition, Drawing> StaticCache = new();

    public static void DrawSky(DrawingContext dc, LevelDefinition level) =>
        dc.DrawRectangle(SkyBrush, null, new Rect(0, 0, level.Width, level.Height));

    /// <summary>Ground, spikes and the goal gate of a level without a painted background.</summary>
    public static void DrawStaticLevel(DrawingContext dc, LevelDefinition level) =>
        dc.DrawDrawing(StaticCache.GetValue(level, BuildStatic));

    /// <summary>The light inside goal gates breathes slowly.</summary>
    public static void DrawGoalLight(DrawingContext dc, Box goal, double time)
    {
        var pulse = 0.75 + 0.25 * Math.Sin(time * 2.2);
        dc.PushOpacity(0.55 * pulse);
        dc.DrawEllipse(WarmGlow, null, new Point(goal.CenterX, goal.CenterY), goal.Width * 1.6, goal.Height * 1.1);
        dc.Pop();
        dc.DrawGeometry(GateLight, null, ArchGeometry(Inset(goal, 8)));
    }

    public static void DrawCheckpoint(DrawingContext dc, Checkpoint checkpoint, double time)
    {
        var b = checkpoint.Bounds;
        var lamp = new Rect(b.CenterX - 12, b.Top, 24, 28);

        if (checkpoint.IsLit)
        {
            var pulse = 0.8 + 0.2 * Math.Sin(time * 3 + b.X);
            dc.PushOpacity(0.6 * pulse);
            dc.DrawEllipse(WarmGlow, null, new Point(b.CenterX, lamp.Top + 14), 90, 90);
            dc.Pop();
        }

        // Post, little roof, glass.
        dc.DrawRectangle(InkBrush, null, new Rect(b.CenterX - 3, lamp.Bottom, 6, b.Bottom - lamp.Bottom));
        dc.DrawRectangle(InkBrush, null, new Rect(b.CenterX - 14, b.Bottom - 6, 28, 6));
        dc.DrawRoundedRectangle(InkBrush, null, new Rect(lamp.X - 3, lamp.Y - 3, lamp.Width + 6, lamp.Height + 6), 5, 5);
        dc.DrawRoundedRectangle(checkpoint.IsLit ? GateLight : GlassBrush, null, Inset(lamp, 3), 3, 3);
        dc.DrawGeometry(InkBrush, null, Geometry.Parse(
            $"M {lamp.Left - 6},{lamp.Top - 2} L {b.CenterX},{lamp.Top - 12} L {lamp.Right + 6},{lamp.Top - 2} Z"));
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

            foreach (var goal in level.Goals)
            {
                var frame = new Box(goal.X - 10, goal.Y - 10, goal.Width + 20, goal.Height + 10);
                dc.DrawGeometry(InkBrush, null, ArchGeometry(frame));
            }
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

    /// <summary>A doorway: straight sides and a round top.</summary>
    private static Geometry ArchGeometry(Box box)
    {
        var radius = box.Width / 2;
        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            ctx.BeginFigure(new Point(box.X, box.Bottom), true, true);
            ctx.LineTo(new Point(box.X, box.Y + radius), true, false);
            ctx.ArcTo(new Point(box.Right, box.Y + radius), new Size(radius, radius), 0, false, SweepDirection.Clockwise, true, false);
            ctx.LineTo(new Point(box.Right, box.Bottom), true, false);
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

    private static Box Inset(Box b, double by) => new(b.X + by, b.Y + by, b.Width - 2 * by, b.Height - by);

    private static Rect Inset(Rect r, double by) => new(r.X + by, r.Y + by, r.Width - 2 * by, r.Height - 2 * by);

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
