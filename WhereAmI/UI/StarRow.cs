using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;

namespace WhereAmI.UI;

/// <summary>Three stars; earned ones are filled amber.</summary>
public sealed class StarRow : StackPanel
{
    private readonly Path[] _stars = new Path[3];
    private double _starSize = 32;

    public StarRow()
    {
        Orientation = Orientation.Horizontal;
        for (var i = 0; i < _stars.Length; i++)
        {
            _stars[i] = new Path
            {
                Data = (Geometry)Application.Current.FindResource("StarGeometry"),
                Stretch = Stretch.Uniform,
                StrokeThickness = 2,
                Margin = new Thickness(0, 0, 8, 0),
                RenderTransformOrigin = new Point(0.5, 0.5),
                RenderTransform = new ScaleTransform(1, 1),
            };
            Children.Add(_stars[i]);
        }

        ApplySize();
        SetStars(0);
    }

    public double StarSize
    {
        get => _starSize;
        set
        {
            _starSize = value;
            ApplySize();
        }
    }

    public void SetStars(int count)
    {
        for (var i = 0; i < _stars.Length; i++)
            ApplyLook(_stars[i], i < count);
    }

    /// <summary>Lights star <paramref name="index"/> with a little bounce.</summary>
    public void PopStar(int index)
    {
        var star = _stars[index];
        ApplyLook(star, earned: true);

        var bounce = new DoubleAnimation(1.6, 1, TimeSpan.FromSeconds(0.35))
        {
            EasingFunction = new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.6 },
        };
        var scale = (ScaleTransform)star.RenderTransform;
        scale.BeginAnimation(ScaleTransform.ScaleXProperty, bounce);
        scale.BeginAnimation(ScaleTransform.ScaleYProperty, bounce);
    }

    private void ApplySize()
    {
        foreach (var star in _stars)
            star.Width = star.Height = _starSize;
    }

    private static void ApplyLook(Path star, bool earned)
    {
        star.Fill = earned ? (Brush)Application.Current.FindResource("AmberBrush") : Brushes.Transparent;
        star.Stroke = earned
            ? (Brush)Application.Current.FindResource("AmberSoftBrush")
            : (Brush)Application.Current.FindResource("TextFaintBrush");
        star.Effect = earned ? (System.Windows.Media.Effects.Effect)Application.Current.FindResource("AmberGlow") : null;
    }
}
