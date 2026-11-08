using System;
using System.Windows;
using System.Windows.Input;
using WhereAmI.Rendering;

namespace WhereAmI.UI;

public partial class MainMenuScreen : Screen
{
    private readonly SpriteAnimation _heroIdle;
    private double _time;

    public MainMenuScreen(SpriteAnimation heroIdle, bool hasProgress)
    {
        InitializeComponent();
        _heroIdle = heroIdle;
        PlayButton.Content = hasProgress ? "Continue" : "Play";
        Hero.Source = heroIdle.FrameAt(0);
    }

    public event Action PlayRequested;
    public event Action LevelsRequested;
    public event Action OptionsRequested;
    public event Action QuitRequested;

    protected override IInputElement InitialFocus => PlayButton;

    public override void OnBack() => QuitRequested?.Invoke();

    public override void Update(double dt)
    {
        _time += dt;
        Hero.Source = _heroIdle.FrameAt(_time);
    }

    private void Play_Click(object sender, RoutedEventArgs e) => PlayRequested?.Invoke();
    private void Levels_Click(object sender, RoutedEventArgs e) => LevelsRequested?.Invoke();
    private void Options_Click(object sender, RoutedEventArgs e) => OptionsRequested?.Invoke();
    private void Quit_Click(object sender, RoutedEventArgs e) => QuitRequested?.Invoke();
}
