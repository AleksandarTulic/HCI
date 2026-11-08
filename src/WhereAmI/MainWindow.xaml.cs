using System;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
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
    private readonly Camera _camera = new();
    private AudioManager _audio;
    private GameSession _session;

    /// <summary>The HUD is designed for a window of at least this size and scaled from there.</summary>
    private const double UiDesignHeight = 1080;
    private const double UiDesignMinWidth = 1280;

    private bool _fullscreen;

    public MainWindow()
    {
        InitializeComponent();
        _loop = new GameLoop(Update, Render);
        Loaded += OnLoaded;
        Deactivated += (_, _) => _input.Clear();
        Closed += (_, _) => _loop.Stop();
        Root.SizeChanged += (_, _) => UpdateUiScale();

        ApplyLaunchOptions(LaunchOptions.Parse(Environment.GetCommandLineArgs()[1..]));
    }

    private void ApplyLaunchOptions(LaunchOptions options)
    {
        if (options.Fullscreen)
        {
            SetFullscreen(true);
            return;
        }

        if (options.WindowWidth is { } width && options.WindowHeight is { } height)
        {
            // Size the window around the requested client area, then let the user resize it freely.
            Root.Width = width;
            Root.Height = height;
            SizeToContent = SizeToContent.WidthAndHeight;
            ContentRendered += (_, _) =>
            {
                SizeToContent = SizeToContent.Manual;
                Root.Width = double.NaN;
                Root.Height = double.NaN;
            };
        }
    }

    private void SetFullscreen(bool fullscreen)
    {
        _fullscreen = fullscreen;

        // The window has to be restored first, otherwise a maximized borderless
        // window keeps the old size and the taskbar stays visible.
        WindowState = WindowState.Normal;
        WindowStyle = fullscreen ? WindowStyle.None : WindowStyle.SingleBorderWindow;
        ResizeMode = fullscreen ? ResizeMode.NoResize : ResizeMode.CanResize;
        WindowState = fullscreen ? WindowState.Maximized : WindowState.Normal;
    }

    private void UpdateUiScale()
    {
        // Follow the height, but shrink further in narrow windows so the HUD always fits.
        var fit = Math.Min(Root.ActualHeight / UiDesignHeight, Root.ActualWidth / UiDesignMinWidth);
        var scale = Math.Clamp(fit, 0.4, 3);
        var transform = new ScaleTransform(scale, scale);
        transform.Freeze();
        HudPanel.LayoutTransform = transform;
        MessagePanel.LayoutTransform = transform;
        HudPanel.Margin = new Thickness(32 * scale, 24 * scale, 0, 0);
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
        Surface.Camera = _camera;
        _audio.StartMusic("audio/music/magic.mp3");
        UpdateSoundIcon();
        _loop.Start();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        // Alt+Enter arrives as a "system" key.
        if (e.Key == Key.System && e.SystemKey == Key.Enter)
        {
            if (!e.IsRepeat)
                SetFullscreen(!_fullscreen);
            e.Handled = true;
            return;
        }

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

        if (_input.ConsumePressed(GameAction.ToggleFullscreen))
            SetFullscreen(!_fullscreen);

        _session.Update(dt, _input);
        _input.EndUpdate();

        foreach (var gameEvent in _session.DrainEvents())
        {
            PlaySound(gameEvent);
            ShakeCamera(gameEvent);
        }

        _camera.Update(dt, _session.World);
    }

    private void ShakeCamera(GameEvent gameEvent)
    {
        switch (gameEvent)
        {
            case GameEvent.PlayerDied:
                _camera.AddTrauma(0.6);
                break;
            case GameEvent.EnemyStomped:
                _camera.AddTrauma(0.3);
                break;
        }
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
