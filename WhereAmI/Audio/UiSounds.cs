using System;
using System.Media;

namespace WhereAmI.Audio;

/// <summary>
/// Short menu sounds generated in code, so the game needs no extra audio files for them.
/// Each sound is a soft sine "blip" with a quick fade out.
/// </summary>
public sealed class UiSounds
{
    private const int SampleRate = 44100;

    private double _volume = 0.05;
    private SoundPlayer _move;
    private SoundPlayer _select;
    private SoundPlayer _back;

    public UiSounds() => Build();

    public double Volume
    {
        get => _volume;
        set
        {
            if (Math.Abs(_volume - value) < 0.001)
                return;
            _volume = value;
            Build();
        }
    }

    /// <summary>Focus moved to another menu item.</summary>
    public void Move() => Play(_move);

    /// <summary>A menu item was chosen.</summary>
    public void Select() => Play(_select);

    /// <summary>Going back or closing a screen.</summary>
    public void Back() => Play(_back);

    private static void Play(SoundPlayer player)
    {
        try
        {
            player.Play();
        }
        catch (InvalidOperationException)
        {
            // No audio device; menus still work silently.
        }
    }

    private void Build()
    {
        _move = Create((1320, 0.035));
        _select = Create((880, 0.06), (1320, 0.09));
        _back = Create((990, 0.05), (660, 0.08));
    }

    private SoundPlayer Create(params (double Frequency, double Seconds)[] notes)
    {
        var samples = new System.Collections.Generic.List<short>();
        foreach (var (frequency, seconds) in notes)
        {
            var count = (int)(SampleRate * seconds);
            for (var i = 0; i < count; i++)
            {
                var t = (double)i / SampleRate;
                var envelope = Math.Min(1, i / (SampleRate * 0.004)) * Math.Exp(-t / (seconds * 0.35));
                var wave = Math.Sin(2 * Math.PI * frequency * t) * 0.7 + Math.Sin(4 * Math.PI * frequency * t) * 0.15;
                samples.Add((short)(wave * envelope * _volume * 0.35 * short.MaxValue));
            }
        }

        var player = new SoundPlayer(Synth.ToWav(samples));
        player.Load();
        return player;
    }
}
