using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Threading;
using WhereAmI.Core;

namespace WhereAmI.UI;

public partial class OptionsScreen : Screen
{
    private readonly Settings _settings;
    private readonly DispatcherTimer _previewDelay;
    private readonly bool _ready;

    public OptionsScreen(Settings settings)
    {
        InitializeComponent();
        _settings = settings;

        MusicSlider.Value = Math.Round(settings.MusicVolume * 100);
        EffectsSlider.Value = Math.Round(settings.EffectsVolume * 100);
        FullscreenToggle.IsChecked = settings.Fullscreen;
        ShakeToggle.IsChecked = settings.ScreenShake;
        UpdateLabels();

        ControlsList.ItemsSource = new[]
        {
            new ControlRow("Walk", "←", "→", "A", "D"),
            new ControlRow("Jump (hold to go higher)", "↑", "W", "Space"),
            new ControlRow("Pause", "Esc"),
            new ControlRow("Music on/off", "M"),
            new ControlRow("Fullscreen", "F11", "Alt+Enter"),
            new ControlRow("Show hitboxes", "F3"),
        };

        // Play one sample after the slider stops moving, not on every step.
        _previewDelay = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(180) };
        _previewDelay.Tick += (_, _) =>
        {
            _previewDelay.Stop();
            EffectsPreviewRequested?.Invoke();
        };

        // Sliders use Left/Right for their value; Up/Down should still move between settings.
        foreach (var slider in new[] { MusicSlider, EffectsSlider })
            slider.PreviewKeyDown += Slider_PreviewKeyDown;

        // Moving through the tabs with the keyboard opens them right away.
        foreach (RadioButton tab in Tabs.Children)
            tab.GotKeyboardFocus += (_, _) => tab.IsChecked = true;

        PreviewKeyDown += OnPreviewKeyDown;
        _ready = true;
    }

    /// <summary>Right jumps from the tabs into the open page; Left on a switch jumps back.</summary>
    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        var focused = Keyboard.FocusedElement as DependencyObject;
        if (e.Key == Key.Right && focused is RadioButton && FirstControlOnOpenPage() is { } first)
        {
            Keyboard.Focus(first);
            e.Handled = true;
        }
        else if (e.Key == Key.Left && focused is CheckBox)
        {
            Keyboard.Focus(CheckedTab());
            e.Handled = true;
        }
    }

    private IInputElement FirstControlOnOpenPage()
    {
        if (SoundPage.Visibility == Visibility.Visible) return MusicSlider;
        if (DisplayPage.Visibility == Visibility.Visible) return FullscreenToggle;
        if (GameplayPage.Visibility == Visibility.Visible) return ShakeToggle;
        return null;
    }

    private RadioButton CheckedTab()
    {
        foreach (RadioButton tab in Tabs.Children)
            if (tab.IsChecked == true)
                return tab;
        return SoundTab;
    }

    public event Action BackRequested;

    /// <summary>A setting changed; the new values are already in the settings object.</summary>
    public event Action SettingsChanged;

    public event Action EffectsPreviewRequested;

    /// <summary>The fullscreen switch was flipped by the player.</summary>
    public event Action FullscreenToggled;

    protected override IInputElement InitialFocus => SoundTab;

    public override void OnBack() => BackRequested?.Invoke();

    /// <summary>Fullscreen can also change with F11 while this screen is open.</summary>
    public void Refresh() => FullscreenToggle.IsChecked = _settings.Fullscreen;

    private void Tab_Checked(object sender, RoutedEventArgs e)
    {
        if (SoundPage is null)
            return;

        var tab = (string)((RadioButton)sender).Content;
        SoundPage.Visibility = tab == "Sound" ? Visibility.Visible : Visibility.Collapsed;
        DisplayPage.Visibility = tab == "Display" ? Visibility.Visible : Visibility.Collapsed;
        GameplayPage.Visibility = tab == "Gameplay" ? Visibility.Visible : Visibility.Collapsed;
        ControlsPage.Visibility = tab == "Controls" ? Visibility.Visible : Visibility.Collapsed;
    }

    private void Music_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!_ready)
            return;

        _settings.MusicVolume = MusicSlider.Value / 100;
        UpdateLabels();
        SettingsChanged?.Invoke();
    }

    private void Effects_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!_ready)
            return;

        _settings.EffectsVolume = EffectsSlider.Value / 100;
        UpdateLabels();
        SettingsChanged?.Invoke();
        _previewDelay.Stop();
        _previewDelay.Start();
    }

    private void Fullscreen_Click(object sender, RoutedEventArgs e)
    {
        if (!_ready)
            return;

        var fullscreen = FullscreenToggle.IsChecked == true;
        if (_settings.Fullscreen == fullscreen)
            return;   // set from outside, e.g. by F11

        _settings.Fullscreen = fullscreen;
        FullscreenToggled?.Invoke();
        SettingsChanged?.Invoke();
    }

    private void Shake_Click(object sender, RoutedEventArgs e)
    {
        if (!_ready)
            return;

        _settings.ScreenShake = ShakeToggle.IsChecked == true;
        SettingsChanged?.Invoke();
    }

    private void Back_Click(object sender, RoutedEventArgs e) => BackRequested?.Invoke();

    private void UpdateLabels()
    {
        MusicValue.Text = $"{MusicSlider.Value:0}%";
        EffectsValue.Text = $"{EffectsSlider.Value:0}%";
    }

    private static void Slider_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key is not (Key.Up or Key.Down))
            return;

        var direction = e.Key == Key.Up ? FocusNavigationDirection.Up : FocusNavigationDirection.Down;
        ((UIElement)sender).MoveFocus(new TraversalRequest(direction));
        e.Handled = true;
    }

    private sealed record ControlRow(string Action, params string[] Keys);
}
