using System;
using System.Windows;
using System.Windows.Input;

namespace WhereAmI.UI;

/// <summary>A few lines of story before the first game. Each line fades in and out.</summary>
public partial class IntroScreen : Screen
{
    private const double FadeSeconds = 0.6;
    private const double HoldSeconds = 3.2;

    private static readonly string[] Lines =
    {
        "You wake up somewhere cold and quiet.",
        "Fog hides everything, except a faint trail of light.",
        "There is only one question in your small, dark head…",
        "Where am I?",
    };

    private int _index;
    private double _time;
    private bool _finished;

    public IntroScreen()
    {
        InitializeComponent();
        Line.Text = Lines[0];
        KeyDown += OnKeyDown;
    }

    public event Action Finished;

    protected override IInputElement InitialFocus => this;

    public override void OnBack() => Finish();

    public override void Update(double dt)
    {
        if (_finished)
            return;

        _time += dt;
        var fadeIn = Math.Clamp(_time / FadeSeconds, 0, 1);
        var fadeOut = Math.Clamp((FadeSeconds + HoldSeconds + FadeSeconds - _time) / FadeSeconds, 0, 1);
        Line.Opacity = Math.Min(fadeIn, fadeOut);

        if (_time >= FadeSeconds * 2 + HoldSeconds)
            Next();
    }

    private void Next()
    {
        _index++;
        if (_index >= Lines.Length)
        {
            Finish();
            return;
        }

        Line.Text = Lines[_index];
        _time = 0;
    }

    private void Finish()
    {
        if (_finished)
            return;

        _finished = true;
        Finished?.Invoke();
    }

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key is Key.Space or Key.Enter)
        {
            Next();
            e.Handled = true;
        }
    }

    private void OnClick(object sender, MouseButtonEventArgs e) => Next();
}
