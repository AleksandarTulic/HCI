using System;
using System.Windows;
using System.Windows.Input;

namespace WhereAmI.UI;

public partial class VictoryScreen : Screen
{
    private readonly int _score;
    private double _time;

    public VictoryScreen(int score)
    {
        InitializeComponent();
        _score = score;
        ScoreText.Text = "0";
    }

    public event Action LevelsRequested;
    public event Action MainMenuRequested;

    protected override IInputElement InitialFocus => LevelsButton;

    public override void OnBack() => MainMenuRequested?.Invoke();

    public override void Update(double dt)
    {
        _time += dt;
        var t = Math.Clamp((_time - 0.4) / 1.6, 0, 1);
        ScoreText.Text = $"{(int)Math.Round(_score * (1 - Math.Pow(1 - t, 3))):N0}";
    }

    private void Levels_Click(object sender, RoutedEventArgs e) => LevelsRequested?.Invoke();
    private void Menu_Click(object sender, RoutedEventArgs e) => MainMenuRequested?.Invoke();
}
