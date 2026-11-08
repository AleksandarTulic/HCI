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

    public AudioManager()
    {
        _music.MediaEnded += (_, _) =>
        {
            _music.Position = TimeSpan.Zero;
            _music.Play();
        };

        // Open every effect up front so playing one has no file-loading delay.
        foreach (var (sound, file) in Files)
        {
            var voices = new MediaPlayer[VoicesPerSound];
            for (var i = 0; i < voices.Length; i++)
            {
                voices[i] = new MediaPlayer { Volume = 0.8 };
                voices[i].Open(new Uri(AssetPaths.Get(file)));
            }

            _voices[sound] = voices;
            _nextVoice[sound] = 0;
        }
    }

    public bool MusicEnabled { get; private set; } = true;

    public void StartMusic(string relativePath)
    {
        _music.Open(new Uri(AssetPaths.Get(relativePath)));
        _music.Volume = 0.5;
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
        var voices = _voices[sound];
        var index = _nextVoice[sound];
        _nextVoice[sound] = (index + 1) % voices.Length;

        var voice = voices[index];
        voice.Stop();
        voice.Position = TimeSpan.Zero;
        voice.Play();
    }
}
