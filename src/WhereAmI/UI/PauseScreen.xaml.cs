using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using WhereAmI.Game;

namespace WhereAmI.UI;

public partial class PauseScreen : Screen
{
    public PauseScreen(GameSession session)
    {
        InitializeComponent();

        var world = session.World;
        LevelName.Text = $"Level {session.LevelIndex + 1}  ·  {world.Level.Name}";
        CoinsText.Text = $"{world.CoinsCollected}/{world.TotalCoins}";
        EnemiesText.Text = $"{world.EnemiesDefeated}/{world.TotalEnemies}";
        TimeText.Text = TimeSpan.FromSeconds(Math.Ceiling(session.TimeLeft)).ToString(@"m\:ss");

        Loaded += (_, _) =>
        {
            var slide = new DoubleAnimation(0, TimeSpan.FromSeconds(0.28))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
            };
            PanelSlide.BeginAnimation(TranslateTransform.XProperty, slide);
        };
    }

    public event Action ResumeRequested;
    public event Action RestartRequested;
    public event Action OptionsRequested;
    public event Action MainMenuRequested;

    protected override IInputElement InitialFocus => ResumeButton;

    public override void OnBack() => ResumeRequested?.Invoke();

    private void Resume_Click(object sender, RoutedEventArgs e) => ResumeRequested?.Invoke();
    private void Restart_Click(object sender, RoutedEventArgs e) => RestartRequested?.Invoke();
    private void Options_Click(object sender, RoutedEventArgs e) => OptionsRequested?.Invoke();
    private void Menu_Click(object sender, RoutedEventArgs e) => MainMenuRequested?.Invoke();
}
