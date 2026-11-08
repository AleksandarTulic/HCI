using System;
using System.IO;
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

        var player = new SoundPlayer(ToWav(samples));
        player.Load();
        return player;
    }

    private static MemoryStream ToWav(System.Collections.Generic.List<short> samples)
    {
        var stream = new MemoryStream();
        using (var w = new BinaryWriter(stream, System.Text.Encoding.ASCII, leaveOpen: true))
        {
            var dataBytes = samples.Count * 2;
            w.Write("RIFF"u8.ToArray());
            w.Write(36 + dataBytes);
            w.Write("WAVEfmt "u8.ToArray());
            w.Write(16);                 // fmt chunk size
            w.Write((short)1);           // PCM
            w.Write((short)1);           // mono
            w.Write(SampleRate);
            w.Write(SampleRate * 2);     // bytes per second
            w.Write((short)2);           // block align
            w.Write((short)16);          // bits per sample
            w.Write("data"u8.ToArray());
            w.Write(dataBytes);
            foreach (var s in samples)
                w.Write(s);
        }

        stream.Position = 0;
        return stream;
    }
}
