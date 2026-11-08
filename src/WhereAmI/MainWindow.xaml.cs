using System;
using System.IO;
using System.Windows;
using System.Windows.Input;
using WhereAmI.Audio;
using WhereAmI.Core;
using WhereAmI.Game;
using WhereAmI.Input;
using WhereAmI.Levels;
using WhereAmI.Rendering;

namespace WhereAmI;

/// <summary>Hosts the game: forwards input, runs the loop and updates the HUD.</summary>
public partial class MainWindow : Window
{
    private readonly KeyboardInput _input = new();
    private readonly GameLoop _loop;
    private AudioManager _audio;
    private GameSession _session;

    public MainWindow()
    {
        InitializeComponent();
        _loop = new GameLoop(Update, Render);
        Loaded += OnLoaded;
        Deactivated += (_, _) => _input.Clear();
        Closed += (_, _) => _loop.Stop();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        try
        {
            var levels = LevelLoader.LoadAll(AssetPaths.Get("levels"));
            _session = new GameSession(levels);
            _audio = new AudioManager();
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException)
        {
            MessageBox.Show(this, ex.Message, "Where Am I? could not start", MessageBoxButton.OK, MessageBoxImage.Error);
            Close();
            return;
        }

        Surface.Session = _session;
        _audio.StartMusic("audio/music/magic.mp3");
        UpdateSoundIcon();
        _loop.Start();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        _input.OnKeyDown(e.Key, e.IsRepeat);
        e.Handled = true;
    }

    protected override void OnKeyUp(KeyEventArgs e)
    {
        _input.OnKeyUp(e.Key);
        e.Handled = true;
    }

    private void Update(double dt)
    {
        if (_input.ConsumePressed(GameAction.Quit))
        {
            Close();
            return;
        }

        if (_input.ConsumePressed(GameAction.ToggleMusic))
        {
            _audio.ToggleMusic();
            UpdateSoundIcon();
        }

        if (_input.ConsumePressed(GameAction.ToggleDebug))
            Surface.ShowHitboxes = !Surface.ShowHitboxes;

        _session.Update(dt, _input);
        _input.EndUpdate();

        foreach (var gameEvent in _session.DrainEvents())
            PlaySound(gameEvent);
    }

    private void PlaySound(GameEvent gameEvent)
    {
        Sound? sound = gameEvent switch
        {
            GameEvent.Jump => Sound.Jump,
            GameEvent.CoinCollected => Sound.Coin,
            GameEvent.EmptyBoxHit => Sound.EmptyBox,
            GameEvent.EnemyStomped => Sound.Stomp,
            GameEvent.PlayerDied => Sound.Death,
            GameEvent.GameOver => Sound.GameOver,
            _ => null,
        };

        if (sound is { } s)
            _audio.Play(s);
    }

    private void Render()
    {
        Surface.InvalidateVisual();

        TimeText.Text = $"Time: {Math.Ceiling(_session.TimeLeft)}";
        LivesText.Text = $"Lives: {_session.Lives}";
        PointsText.Text = $"Points: {_session.Points}";
        LevelText.Text = $"{_session.LevelIndex + 1}/{_session.LevelCount}  {_session.World.Level.Name}";

        switch (_session.State)
        {
            case SessionState.GameOver:
                ShowMessage("Game Over", $"Points: {_session.Points}\n\nSpace / Enter – play again      Esc – quit");
                break;
            case SessionState.Victory:
                ShowMessage("You found your way!", $"Points: {_session.Points}\n\nSpace / Enter – play again      Esc – quit");
                break;
            default:
                MessagePanel.Visibility = Visibility.Collapsed;
                break;
        }
    }

    private void ShowMessage(string title, string body)
    {
        MessageTitle.Text = title;
        MessageBody.Text = body;
        MessagePanel.Visibility = Visibility.Visible;
    }

    private void SoundIcon_Click(object sender, MouseButtonEventArgs e)
    {
        _audio?.ToggleMusic();
        UpdateSoundIcon();
    }

    private void UpdateSoundIcon()
    {
        if (_audio is null)
            return;

        SoundIcon.Source = SpriteCache.Get(_audio.MusicEnabled ? "sprites/ui/sound-on.png" : "sprites/ui/sound-off.png");
    }
}
