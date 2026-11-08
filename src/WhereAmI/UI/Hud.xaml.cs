using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using WhereAmI.Game;
using WhereAmI.Rendering;

namespace WhereAmI.UI;

/// <summary>In-game overlay: lives, coins, score, clock and short messages.</summary>
public partial class Hud : UserControl
{
    private const double LowTime = 30;

    private int _lives = -1;
    private int _coins = -1;
    private int _score = -1;
    private int _seconds = -1;
    private double _coinBump;
    private double _scoreBump;
    private double _toastAge = double.MaxValue;
    private double _toastDuration;
    private double _time;

    public Hud()
    {
        InitializeComponent();
        CoinIcon.Source = SpriteCache.Get("sprites/coin/popped.png");
    }

    /// <summary>Shows a message in the middle of the screen for a few seconds.</summary>
    public void ShowToast(string big, string small = "", double seconds = 2.2)
    {
        ToastBig.Text = big;
        ToastSmall.Text = small;
        ToastSmall.Visibility = string.IsNullOrEmpty(small) ? Visibility.Collapsed : Visibility.Visible;
        _toastAge = 0;
        _toastDuration = seconds;
    }

    public void Update(double dt, GameSession session)
    {
        _time += dt;
        var world = session.World;

        if (session.Lives != _lives)
        {
            _lives = session.Lives;
            BuildLanterns(_lives);
        }

        if (world.CoinsCollected != _coins)
        {
            if (_coins >= 0 && world.CoinsCollected > _coins)
                _coinBump = 1;
            _coins = world.CoinsCollected;
            CoinsText.Text = _coins.ToString();
            CoinsTotal.Text = $"/ {world.TotalCoins}";
        }

        if (session.Points != _score)
        {
            if (_score >= 0 && session.Points > _score)
                _scoreBump = 1;
            _score = session.Points;
            ScoreText.Text = _score.ToString("N0");
        }

        var seconds = (int)Math.Ceiling(session.TimeLeft);
        if (seconds != _seconds)
        {
            _seconds = seconds;
            TimeText.Text = TimeSpan.FromSeconds(seconds).ToString(@"m\:ss");
            var low = seconds <= LowTime;
            var brush = (Brush)FindResource(low ? "DangerBrush" : "TextBrush");
            TimeText.Foreground = brush;
            ClockIcon.Stroke = brush;
        }

        // Little bounces when numbers go up, and a heartbeat when time is short.
        _coinBump = Math.Max(0, _coinBump - dt * 4);
        _scoreBump = Math.Max(0, _scoreBump - dt * 4);
        SetScale(CoinBump, 1 + 0.35 * _coinBump);
        SetScale(ScoreBump, 1 + 0.15 * _scoreBump);
        var pulse = seconds <= LowTime && session.State == SessionState.Playing
            ? 1 + 0.06 * Math.Max(0, Math.Sin(_time * Math.PI * 2))
            : 1;
        SetScale(ClockPulse, pulse);

        _toastAge += dt;
        var fadeIn = Math.Clamp(_toastAge / 0.3, 0, 1);
        var fadeOut = Math.Clamp((_toastDuration - _toastAge) / 0.6, 0, 1);
        Toast.Opacity = Math.Min(fadeIn, fadeOut);
    }

    private static void SetScale(ScaleTransform transform, double scale)
    {
        transform.ScaleX = scale;
        transform.ScaleY = scale;
    }

    private void BuildLanterns(int lives)
    {
        Lanterns.Children.Clear();
        for (var i = 0; i < Tuning.StartingLives; i++)
            Lanterns.Children.Add(Lantern(lit: i < lives));
    }

    /// <summary>A small glowing orb for each life; spent lives are dark rings.</summary>
    private UIElement Lantern(bool lit)
    {
        var size = 22.0;
        var grid = new Grid { Width = size, Height = size, Margin = new Thickness(3, 0, 3, 0), VerticalAlignment = VerticalAlignment.Center };
        if (lit)
        {
            var glow = new RadialGradientBrush();
            glow.GradientStops.Add(new GradientStop(Color.FromRgb(0xFF, 0xF2, 0xC8), 0));
            glow.GradientStops.Add(new GradientStop(Color.FromRgb(0xFF, 0xB5, 0x47), 0.6));
            glow.GradientStops.Add(new GradientStop(Color.FromRgb(0xE0, 0x7A, 0x10), 1));
            grid.Children.Add(new Ellipse { Fill = glow, Effect = (System.Windows.Media.Effects.Effect)FindResource("AmberGlow") });
        }
        else
        {
            grid.Children.Add(new Ellipse { Stroke = (Brush)FindResource("TextFaintBrush"), StrokeThickness = 2 });
        }

        return grid;
    }
}
