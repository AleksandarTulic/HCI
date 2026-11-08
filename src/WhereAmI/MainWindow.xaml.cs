using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using WhereAmI.Audio;
using WhereAmI.Core;
using WhereAmI.Game;
using WhereAmI.Input;
using WhereAmI.Levels;
using WhereAmI.Rendering;
using WhereAmI.UI;

namespace WhereAmI;

/// <summary>
/// The game shell: runs the loop, switches between menus and play,
/// forwards input and keeps settings and progress.
/// </summary>
public partial class MainWindow : Window
{
    /// <summary>Menus and HUD are designed for a 1080-unit tall window and scaled from there.</summary>
    private const double UiDesignHeight = 1080;
    private const double UiDesignMinWidth = 1280;

    private enum Mode
    {
        Menu,       // main menu, level select, options, intro, victory: scenery behind
        Playing,
        Paused,     // pause menu or options opened from it
        Results,    // level complete or game over
    }

    private readonly KeyboardInput _input = new();
    private readonly GameLoop _loop;
    private readonly Camera _camera = new();
    private readonly LaunchOptions _launch;
    private readonly BlurEffect _blur = new() { Radius = 12 };

    private IReadOnlyList<LevelDefinition> _levels;
    private Settings _settings;
    private Progress _progress;
    private AudioManager _audio;
    private Sprites _sprites;
    private GameWorld _menuWorld;
    private GameSession _session;
    private Screen _screen;
    private Mode _mode = Mode.Menu;
    private int _toastedLevel = -1;
    private bool _fullscreen;
    private bool _quietFocus;

    public MainWindow()
    {
        InitializeComponent();
        _loop = new GameLoop(Update, Render);
        _launch = LaunchOptions.Parse(Environment.GetCommandLineArgs()[1..]);
        _settings = _launch.NoSave ? new Settings() : SaveStore.LoadSettings();
        _progress = _launch.NoSave ? new Progress() : SaveStore.LoadProgress();

        Loaded += OnLoaded;
        Deactivated += (_, _) =>
        {
            _input.Clear();
            if (_mode == Mode.Playing)
                Pause();
        };
        Closed += (_, _) =>
        {
            _loop.Stop();
            SaveAll();
        };
        Root.SizeChanged += (_, _) => UpdateUiScale();

        // Menu feedback: hovering focuses, focusing ticks, clicking chimes.
        Root.AddHandler(Keyboard.GotKeyboardFocusEvent, new KeyboardFocusChangedEventHandler(OnFocusMoved));
        Root.AddHandler(ButtonBase.ClickEvent, new RoutedEventHandler(OnAnyClick));
        Root.AddHandler(Mouse.MouseMoveEvent, new MouseEventHandler(OnMouseMoved));

        ApplyDisplayMode();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        try
        {
            _levels = LevelLoader.LoadAll(AssetPaths.Get("levels"));
            _sprites = new Sprites();
            _audio = new AudioManager();
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException)
        {
            MessageBox.Show(this, ex.Message, "Where Am I? could not start", MessageBoxButton.OK, MessageBoxImage.Error);
            Close();
            return;
        }

        _progress.UnlockedLevels = Math.Clamp(_progress.UnlockedLevels, 1, _levels.Count);
        _menuWorld = new GameWorld(_levels[0]);
        Surface.Camera = _camera;
        ApplySettings();
        _audio.StartMusic("audio/music/magic.mp3");

        ShowMainMenu();
        if (_launch.StartLevel is { } startLevel)
            StartGame(Math.Min(startLevel, _levels.Count - 1));
        _loop.Start();
    }

    // ───────────────────────────── Screens and flow ─────────────────────────────

    private void ShowMainMenu()
    {
        EnterMenuMode();
        var menu = new MainMenuScreen(_sprites.PlayerIdle, hasProgress: _progress.UnlockedLevels > 1);
        menu.PlayRequested += Play;
        menu.LevelsRequested += ShowLevelSelect;
        menu.OptionsRequested += () => ShowOptions(ShowMainMenu);
        menu.QuitRequested += Close;
        ShowScreen(menu);
    }

    private void ShowLevelSelect()
    {
        EnterMenuMode();
        var select = new LevelSelectScreen(_levels, _progress, _progress.UnlockedLevels - 1);
        select.PlayRequested += StartGame;
        select.BackRequested += ShowMainMenu;
        ShowScreen(select);
    }

    private void ShowOptions(Action back)
    {
        var options = new OptionsScreen(_settings);
        options.SettingsChanged += () =>
        {
            ApplySettings();
            SaveAll();
        };
        options.EffectsPreviewRequested += () => _audio.Play(Sound.Coin);
        options.FullscreenToggled += () => SetFullscreen(_settings.Fullscreen);
        options.BackRequested += back;
        ShowScreen(options);
    }

    /// <summary>Continue from the furthest unlocked level; the very first time, tell the story first.</summary>
    private void Play()
    {
        if (_progress.SeenIntro)
        {
            StartGame(_progress.UnlockedLevels - 1);
            return;
        }

        var intro = new IntroScreen();
        intro.Finished += () =>
        {
            _progress.SeenIntro = true;
            SaveAll();
            StartGame(0);
        };
        FadeThen(() => ShowScreen(intro));
    }

    private void StartGame(int level)
    {
        FadeThen(() =>
        {
            _session = new GameSession(_levels, level);
            _toastedLevel = -1;
            EnterPlayMode();
        });
    }

    private void Pause()
    {
        _mode = Mode.Paused;
        _input.Clear();
        _audio.MusicDucked = true;
        Surface.Effect = _blur;

        var pause = new PauseScreen(_session);
        pause.ResumeRequested += Resume;
        pause.RestartRequested += () =>
        {
            _session.RestartLevel();
            Resume();
        };
        pause.OptionsRequested += () => ShowOptions(Pause);
        pause.MainMenuRequested += () => FadeThen(ShowMainMenu);
        ShowScreen(pause);
    }

    private void Resume()
    {
        _audio.MusicDucked = false;
        EnterPlayMode();
    }

    private void ShowLevelComplete()
    {
        var result = _session.LastResult;
        var isNewBest = (_progress.RecordFor(result.LevelIndex)?.BestScore ?? 0) < result.Score;
        _progress.Record(result, _levels.Count);
        SaveAll();

        EnterResultsMode();
        var screen = new LevelCompleteScreen(result, isNewBest, _session.IsLastLevel);
        screen.StarShown += () => _audio.Play(Sound.Coin);
        screen.ContinueRequested += () =>
        {
            _session.ContinueAfterLevel();
            if (_session.State == SessionState.Victory)
                FadeThen(ShowVictory);
            else
                FadeThen(EnterPlayMode);
        };
        screen.MainMenuRequested += () => FadeThen(ShowMainMenu);
        ShowScreen(screen);
    }

    private void ShowGameOver()
    {
        EnterResultsMode();

        // The darkness closes in on where the player was last seen.
        var p = _session.World.Player.Bounds;
        var at = Surface.WorldToScreen(p.CenterX, Math.Min(p.CenterY, _session.World.Level.Height - 80));
        var focus = new Point(
            Math.Clamp(at.X / Math.Max(1, Surface.ActualWidth), 0.1, 0.9),
            Math.Clamp(at.Y / Math.Max(1, Surface.ActualHeight), 0.1, 0.9));

        var screen = new GameOverScreen(_session.World.Level.Name, _session.Points, focus);
        var level = _session.LevelIndex;
        screen.RetryRequested += () => StartGame(level);
        screen.LevelsRequested += () => FadeThen(ShowLevelSelect);
        screen.MainMenuRequested += () => FadeThen(ShowMainMenu);
        ShowScreen(screen);
    }

    private void ShowVictory()
    {
        EnterMenuMode();
        var screen = new VictoryScreen(_session.Points);
        screen.LevelsRequested += ShowLevelSelect;
        screen.MainMenuRequested += ShowMainMenu;
        ShowScreen(screen);
    }

    private void EnterMenuMode()
    {
        _mode = Mode.Menu;
        _input.Clear();
        _audio.MusicDucked = false;
        Surface.World = _menuWorld;
        Surface.ShowPlayer = false;
        Surface.Effect = null;
        Hud.Visibility = Visibility.Collapsed;
        Fireflies.Visibility = Visibility.Visible;
    }

    private void EnterPlayMode()
    {
        _mode = Mode.Playing;
        _input.Clear();
        Surface.World = _session.World;
        Surface.ShowPlayer = true;
        Surface.Effect = null;
        Hud.Visibility = Visibility.Visible;
        Fireflies.Visibility = Visibility.Collapsed;
        CloseScreen();
    }

    private void EnterResultsMode()
    {
        _mode = Mode.Results;
        _input.Clear();
        Surface.Effect = _blur;
        Hud.Visibility = Visibility.Collapsed;
    }

    private void ShowScreen(Screen screen)
    {
        _screen = screen;
        ScreenHost.Content = screen;

        // Setting the first focus should not make a tick sound.
        _quietFocus = true;
        screen.FocusInitial();
        Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Input, () => _quietFocus = false);
    }

    private void CloseScreen()
    {
        _screen = null;
        ScreenHost.Content = null;
        Keyboard.Focus(this);
    }

    /// <summary>Fades to dark, runs <paramref name="action"/>, then fades back in.</summary>
    private void FadeThen(Action action)
    {
        var fadeOut = new DoubleAnimation(1, TimeSpan.FromSeconds(0.22));
        fadeOut.Completed += (_, _) =>
        {
            action();
            Fade.BeginAnimation(OpacityProperty, new DoubleAnimation(0, TimeSpan.FromSeconds(0.35)));
        };
        Fade.BeginAnimation(OpacityProperty, fadeOut);
    }

    // ───────────────────────────── Loop ─────────────────────────────

    private void Update(double dt)
    {
        switch (_mode)
        {
            case Mode.Playing:
                UpdatePlaying(dt);
                break;
            case Mode.Menu:
                _menuWorld.UpdateAmbient(dt);
                _camera.UpdateDrift(dt, _menuWorld);
                break;
            case Mode.Results:
                _session.Update(dt, _input);   // keeps enemies and effects moving behind the blur
                break;
        }

        _input.EndUpdate();
        _screen?.Update(dt);
        Fireflies.Update(dt);
        if (_session is not null && _mode != Mode.Menu)
            Hud.Update(dt, _session);
    }

    private void UpdatePlaying(double dt)
    {
        if (_input.ConsumePressed(GameAction.ToggleMusic))
        {
            _audio.ToggleMusic();
            Hud.ShowToast(_audio.MusicEnabled ? "Music on" : "Music off", seconds: 1.2);
        }

        if (_input.ConsumePressed(GameAction.ToggleDebug))
            Surface.ShowHitboxes = !Surface.ShowHitboxes;

        _session.Update(dt, _input);
        Surface.World = _session.World;

        foreach (var gameEvent in _session.DrainEvents())
        {
            PlaySound(gameEvent);
            ShakeCamera(gameEvent);
            if (gameEvent == GameEvent.CheckpointReached)
            {
                _audio.Ui.Select();
                Hud.ShowToast("Checkpoint", "LANTERN LIT", 1.8);
            }
        }

        _camera.Update(dt, _session.World);

        if (_toastedLevel != _session.LevelIndex && _session.State == SessionState.Playing)
        {
            _toastedLevel = _session.LevelIndex;
            Hud.ShowToast(_session.World.Level.Name, $"LEVEL {_session.LevelIndex + 1}", 2.8);
        }

        switch (_session.State)
        {
            case SessionState.LevelComplete:
                ShowLevelComplete();
                break;
            case SessionState.GameOver:
                ShowGameOver();
                break;
        }
    }

    private void Render()
    {
        Surface.InvalidateVisual();
        if (Fireflies.Visibility == Visibility.Visible)
            Fireflies.InvalidateVisual();
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

    // ───────────────────────────── Input ─────────────────────────────

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        // Alt+Enter arrives as a "system" key.
        if ((e.Key == Key.System && e.SystemKey == Key.Enter) || e.Key == Key.F11)
        {
            if (!e.IsRepeat)
                ToggleFullscreen();
            e.Handled = true;
            return;
        }

        if (_mode == Mode.Playing)
        {
            if (e.Key == Key.Escape)
                Pause();
            else
                _input.OnKeyDown(e.Key, e.IsRepeat);
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Escape && !e.IsRepeat)
        {
            _audio?.Ui.Back();
            _screen?.OnBack();
            e.Handled = true;
        }
        else if (e.Key == Key.Enter && !e.IsRepeat && ActivateFocused())
        {
            e.Handled = true;
        }
    }

    protected override void OnPreviewKeyUp(KeyEventArgs e) => _input.OnKeyUp(e.Key);

    /// <summary>WPF buttons react to Space only; make Enter work everywhere in the menus too.</summary>
    private static bool ActivateFocused()
    {
        switch (Keyboard.FocusedElement)
        {
            case Button { IsEnabled: true } button:
                (new ButtonAutomationPeer(button).GetPattern(PatternInterface.Invoke) as IInvokeProvider)?.Invoke();
                return true;
            case CheckBox { IsEnabled: true } checkBox:
                checkBox.IsChecked = checkBox.IsChecked != true;
                return true;
            case RadioButton radio:
                radio.IsChecked = true;
                return true;
            default:
                return false;
        }
    }

    private void OnFocusMoved(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (!_quietFocus && e.NewFocus is ButtonBase or Slider)
            _audio?.Ui.Move();
    }

    private void OnAnyClick(object sender, RoutedEventArgs e)
    {
        if (e.OriginalSource is FrameworkElement { Tag: "back" })
            _audio?.Ui.Back();
        else
            _audio?.Ui.Select();
    }

    /// <summary>Hovering a control focuses it, so mouse and keyboard share one highlight.</summary>
    private static void OnMouseMoved(object sender, MouseEventArgs e)
    {
        for (var element = e.OriginalSource as DependencyObject; element is not null; element = ParentOf(element))
        {
            if (element is ButtonBase or Slider && element is UIElement { IsEnabled: true, Focusable: true } control)
            {
                if (!control.IsKeyboardFocused)
                    control.Focus();
                return;
            }
        }
    }

    private static DependencyObject ParentOf(DependencyObject element) =>
        element is Visual or System.Windows.Media.Media3D.Visual3D ? VisualTreeHelper.GetParent(element) : LogicalTreeHelper.GetParent(element);

    // ───────────────────────────── Display and settings ─────────────────────────────

    private void ApplyDisplayMode()
    {
        if (_launch.Fullscreen == false && _launch.WindowWidth is { } width && _launch.WindowHeight is { } height)
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

        SetFullscreen(_launch.Fullscreen ?? _settings.Fullscreen);
    }

    private void ToggleFullscreen()
    {
        SetFullscreen(!_fullscreen);
        _settings.Fullscreen = _fullscreen;
        SaveAll();
        (_screen as OptionsScreen)?.Refresh();
    }

    private void SetFullscreen(bool fullscreen)
    {
        if (_fullscreen == fullscreen && IsLoaded)
            return;

        _fullscreen = fullscreen;

        // The window has to be restored first, otherwise a maximized borderless
        // window keeps the old size and the taskbar stays visible.
        WindowState = WindowState.Normal;
        WindowStyle = fullscreen ? WindowStyle.None : WindowStyle.SingleBorderWindow;
        ResizeMode = fullscreen ? ResizeMode.NoResize : ResizeMode.CanResize;
        WindowState = fullscreen ? WindowState.Maximized : WindowState.Normal;
    }

    private void ApplySettings()
    {
        _audio.MusicVolume = _settings.MusicVolume;
        _audio.EffectsVolume = _settings.EffectsVolume;
        _camera.ShakeEnabled = _settings.ScreenShake;
    }

    private void SaveAll()
    {
        if (_launch.NoSave || _settings is null)
            return;

        SaveStore.Save(_settings);
        SaveStore.Save(_progress);
    }

    private void UpdateUiScale()
    {
        // Follow the height, but shrink further in narrow windows so everything fits.
        var fit = Math.Min(Root.ActualHeight / UiDesignHeight, Root.ActualWidth / UiDesignMinWidth);
        var scale = Math.Clamp(fit, 0.35, 3);
        var transform = new ScaleTransform(scale, scale);
        transform.Freeze();
        Hud.LayoutTransform = transform;
        ScreenHost.LayoutTransform = transform;
    }
}
