using System;
using System.Windows;
using System.Windows.Media;

namespace WhereAmI.UI;

/// <summary>Softly glowing fireflies drifting over the menus. Call <see cref="Update"/> every game step.</summary>
public sealed class FireflyLayer : FrameworkElement
{
    private const int Count = 34;

    private static readonly Brush Glow = CreateGlow();

    private readonly Firefly[] _flies = new Firefly[Count];
    private readonly Random _random = new(7);
    private double _time;

    public FireflyLayer()
    {
        IsHitTestVisible = false;
        for (var i = 0; i < Count; i++)
        {
            _flies[i] = new Firefly
            {
                X = _random.NextDouble(),
                Y = 0.15 + _random.NextDouble() * 0.85,
                Size = 6 + _random.NextDouble() * 10,
                Speed = 0.004 + _random.NextDouble() * 0.01,
                Phase = _random.NextDouble() * Math.PI * 2,
                Blink = 0.6 + _random.NextDouble() * 1.4,
            };
        }
    }

    public void Update(double dt)
    {
        if (Visibility != Visibility.Visible)
            return;

        _time += dt;
        foreach (var f in _flies)
        {
            f.X += f.Speed * dt * Math.Cos(_time * 0.3 + f.Phase);
            f.Y -= f.Speed * 0.6 * dt;
            if (f.Y < 0.05)
            {
                f.Y = 1.02;
                f.X = _random.NextDouble();
            }
        }
    }

    protected override void OnRender(DrawingContext dc)
    {
        var width = ActualWidth;
        var height = ActualHeight;

        foreach (var f in _flies)
        {
            var wobbleX = Math.Sin(_time * 1.3 + f.Phase) * 18;
            var wobbleY = Math.Cos(_time * 0.9 + f.Phase) * 12;
            var pulse = 0.5 + 0.5 * Math.Sin(_time * f.Blink + f.Phase);
            var radius = f.Size * (0.7 + 0.5 * pulse);

            dc.PushOpacity(0.25 + 0.75 * pulse);
            dc.DrawEllipse(Glow, null, new Point(f.X * width + wobbleX, f.Y * height + wobbleY), radius, radius);
            dc.Pop();
        }
    }

    private static Brush CreateGlow()
    {
        var brush = new RadialGradientBrush();
        brush.GradientStops.Add(new GradientStop(Color.FromArgb(0xFF, 0xFF, 0xF0, 0xC0), 0));
        brush.GradientStops.Add(new GradientStop(Color.FromArgb(0xB0, 0xFF, 0xB5, 0x47), 0.25));
        brush.GradientStops.Add(new GradientStop(Color.FromArgb(0x00, 0xFF, 0xB5, 0x47), 1));
        brush.Freeze();
        return brush;
    }

    private sealed class Firefly
    {
        public double X;
        public double Y;
        public double Size;
        public double Speed;
        public double Phase;
        public double Blink;
    }
}
