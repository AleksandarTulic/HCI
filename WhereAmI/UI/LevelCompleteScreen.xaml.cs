using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using WhereAmI.Game;

namespace WhereAmI.UI;

/// <summary>Results of a finished level. The numbers count up and the stars light one by one.</summary>
public partial class LevelCompleteScreen : Screen
{
    private const double CountSeconds = 0.6;
    private const double StarInterval = 0.35;

    private readonly LevelResult _result;
    private double _time;
    private int _starsShown;

    public LevelCompleteScreen(LevelResult result, bool isNewBest, bool isLastLevel)
    {
        InitializeComponent();
        _result = result;

        LevelName.Text = result.LevelName;
        RecordBadge.Visibility = isNewBest ? Visibility.Visible : Visibility.Collapsed;
        ContinueButton.Content = isLastLevel ? "Finish" : "Next level";
        ShowNumbers(0);

        Loaded += (_, _) =>
        {
            var ease = new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.4 };
            Card.BeginAnimation(OpacityProperty, new DoubleAnimation(1, TimeSpan.FromSeconds(0.25)));
            CardScale.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(1, TimeSpan.FromSeconds(0.35)) { EasingFunction = ease });
            CardScale.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(1, TimeSpan.FromSeconds(0.35)) { EasingFunction = ease });
        };
    }

    public event Action ContinueRequested;
    public event Action MainMenuRequested;

    /// <summary>Raised as each star lights up, so a sound can play.</summary>
    public event Action StarShown;

    protected override IInputElement InitialFocus => ContinueButton;

    public override void OnBack() => MainMenuRequested?.Invoke();

    public override void Update(double dt)
    {
        _time += dt;

        // Rows count up one after another, then the stars appear.
        ShowNumbers(_time);

        var starsStart = CountSeconds * 3 + 0.2;
        while (_starsShown < _result.Stars && _time >= starsStart + _starsShown * StarInterval)
        {
            Stars.PopStar(_starsShown);
            _starsShown++;
            StarShown?.Invoke();
        }
    }

    private void ShowNumbers(double time)
    {
        double Part(int index) => Math.Clamp((time - index * CountSeconds) / CountSeconds, 0, 1);

        CoinsValue.Text = $"{(int)Math.Round(_result.Coins * Part(0))} / {_result.TotalCoins}";
        EnemiesValue.Text = $"{(int)Math.Round(_result.Enemies * Part(1))} / {_result.TotalEnemies}";
        BonusValue.Text = $"+{(int)Math.Round(_result.TimeBonus * Part(2)):N0}";

        // The score fills up together with the rows above it.
        var shownScore = _result.LevelPoints * (Part(0) + Part(1)) / 2 + _result.TimeBonus * Part(2);
        ScoreValue.Text = $"{(int)Math.Round(shownScore):N0}";
    }

    private void Continue_Click(object sender, RoutedEventArgs e) => ContinueRequested?.Invoke();
    private void Menu_Click(object sender, RoutedEventArgs e) => MainMenuRequested?.Invoke();
}
