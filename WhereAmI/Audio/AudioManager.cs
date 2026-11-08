using System;
using System.Collections.Generic;
using System.Windows.Media;
using WhereAmI.Core;

namespace WhereAmI.Audio;

public enum Sound
{
    Coin,
    Jump,
    EmptyBox,
    Stomp,
    Death,
    GameOver,

    // Generated in code by Synth.
    EggThrow,
    EggCrack,
    Ember,
    Wind,
    ToadLand,
    Flutter,
}

/// <summary>Looping background music plus short sound effects that may overlap.</summary>
public sealed class AudioManager
{
    private const int VoicesPerSound = 3;

    private static readonly Dictionary<Sound, string> Files = new()
    {
        [Sound.Coin] = "audio/sfx/coin.wav",
        [Sound.Jump] = "audio/sfx/jump.wav",
        [Sound.EmptyBox] = "audio/sfx/empty-box.mp3",
        [Sound.Stomp] = "audio/sfx/jump.wav",
        [Sound.Death] = "audio/sfx/death.wav",
        [Sound.GameOver] = "audio/sfx/game-over.wav",
    };

    private readonly MediaPlayer _music = new();
    private readonly Dictionary<Sound, MediaPlayer[]> _voices = new();
    private readonly Dictionary<Sound, int> _nextVoice = new();

    private double _musicVolume = 0.05;
    private double _effectsVolume = 0.05;
    private bool _musicDucked;

    public AudioManager()
    {
        _music.MediaEnded += (_, _) =>
        {
            _music.Position = TimeSpan.Zero;
            _music.Play();
        };

        // Synthesized effects are written to a temporary folder once, then played like the others.
        var paths = new Dictionary<Sound, string>();
        foreach (var (sound, file) in Files)
            paths[sound] = AssetPaths.Get(file);
        try
        {
            foreach (var (sound, file) in Synth.WriteGameSounds(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "WhereAmI-sounds")))
                paths[sound] = file;
        }
        catch (System.IO.IOException)
        {
            // Without a writable temp folder the game simply has fewer sounds.
        }

        // Open every effect up front so playing one has no file-loading delay.
        foreach (var (sound, file) in paths)
        {
            var voices = new MediaPlayer[VoicesPerSound];
            for (var i = 0; i < voices.Length; i++)
            {
                voices[i] = new MediaPlayer();
                voices[i].Open(new Uri(file));
            }

            _voices[sound] = voices;
            _nextVoice[sound] = 0;
        }

        ApplyVolumes();
    }

    public UiSounds Ui { get; } = new();

    public bool MusicEnabled { get; private set; } = true;

    public double MusicVolume
    {
        get => _musicVolume;
        set
        {
            _musicVolume = Math.Clamp(value, 0, 1);
            ApplyVolumes();
        }
    }

    public double EffectsVolume
    {
        get => _effectsVolume;
        set
        {
            _effectsVolume = Math.Clamp(value, 0, 1);
            ApplyVolumes();
        }
    }

    /// <summary>Plays the music quieter, e.g. while the game is paused.</summary>
    public bool MusicDucked
    {
        get => _musicDucked;
        set
        {
            _musicDucked = value;
            ApplyVolumes();
        }
    }

    public void StartMusic(string relativePath)
    {
        _music.Open(new Uri(AssetPaths.Get(relativePath)));
        if (MusicEnabled)
            _music.Play();
    }

    public void ToggleMusic()
    {
        MusicEnabled = !MusicEnabled;
        if (MusicEnabled)
            _music.Play();
        else
            _music.Pause();
    }

    public void Play(Sound sound)
    {
        if (!_voices.TryGetValue(sound, out var voices))
            return;

        var index = _nextVoice[sound];
        _nextVoice[sound] = (index + 1) % voices.Length;

        var voice = voices[index];
        voice.Stop();
        voice.Position = TimeSpan.Zero;
        voice.Play();
    }

    private void ApplyVolumes()
    {
        _music.Volume = _musicVolume * (_musicDucked ? 0.35 : 1);
        foreach (var voices in _voices.Values)
            foreach (var voice in voices)
                voice.Volume = _effectsVolume;
        Ui.Volume = _effectsVolume;
    }
}
