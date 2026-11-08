using System;
using System.Windows;
using System.Windows.Input;

namespace WhereAmI.UI;

public partial class GameOverScreen : Screen
{
    private const double CloseSeconds = 1.4;
    private double _time;

    /// <param name="focus">Where the darkness closes in, as a fraction of the window (0..1).</param>
    public GameOverScreen(string levelName, int points, Point focus)
    {
        InitializeComponent();
        Subtitle.Text = $"Lost in {levelName}";
        ScoreText.Text = $"{points:N0} points";

        Vignette.Center = focus;
        Vignette.GradientOrigin = focus;
        RetryButton.IsEnabled = false;
    }

    public event Action RetryRequested;
    public event Action LevelsRequested;
    public event Action MainMenuRequested;

    protected override IInputElement InitialFocus => RetryButton;

    public override void OnBack() => MainMenuRequested?.Invoke();

    public override void Update(double dt)
    {
        _time += dt;

        // The circle of light shrinks, then the text fades in.
        var t = Math.Clamp(_time / CloseSeconds, 0, 1);
        var eased = 1 - Math.Pow(1 - t, 3);
        var radius = 1.6 - 1.25 * eased;
        Vignette.RadiusX = radius;
        Vignette.RadiusY = radius;

        Body.Opacity = Math.Clamp((_time - CloseSeconds * 0.6) / 0.5, 0, 1);

        if (!RetryButton.IsEnabled && Body.Opacity >= 1)
        {
            RetryButton.IsEnabled = true;
            Keyboard.Focus(RetryButton);
        }
    }

    private void Retry_Click(object sender, RoutedEventArgs e) => RetryRequested?.Invoke();
    private void Levels_Click(object sender, RoutedEventArgs e) => LevelsRequested?.Invoke();
    private void Menu_Click(object sender, RoutedEventArgs e) => MainMenuRequested?.Invoke();
}
