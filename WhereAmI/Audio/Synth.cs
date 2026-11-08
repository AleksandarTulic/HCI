using System;
using System.Collections.Generic;
using System.IO;

namespace WhereAmI.Audio;

/// <summary>
/// Tiny sound generator: builds short effects from sine waves and filtered noise,
/// so the game needs no extra audio files for them.
/// </summary>
public static class Synth
{
    public const int SampleRate = 44100;

    /// <summary>Encodes 16-bit mono samples as a WAV file.</summary>
    public static MemoryStream ToWav(IReadOnlyList<short> samples)
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

    /// <summary>Generates the game's synthesized effects as WAV files in <paramref name="folder"/>.</summary>
    public static Dictionary<Sound, string> WriteGameSounds(string folder)
    {
        Directory.CreateDirectory(folder);
        var random = new Random(3);
        var sounds = new Dictionary<Sound, List<short>>
        {
            // A quick upward "whoosh".
            [Sound.EggThrow] = Noise(random, 0.16, t => 0.5 * Fade(t, 0.16), lowPass: 0.25),
            // A dry crack: a click followed by a short crunchy burst.
            [Sound.EggCrack] = Mix(Tone(1800, 0.02, 0.5), Noise(random, 0.12, t => Fade(t, 0.12) * 0.8, lowPass: 0.6)),
            // A soft hiss with a low warm hum, like a match being struck.
            [Sound.Ember] = Mix(Noise(random, 0.35, t => 0.35 * Fade(t, 0.35), lowPass: 0.5), Tone(140, 0.35, 0.25)),
            // A long rising and falling gust.
            [Sound.Wind] = Noise(random, 1.4, t => 0.45 * Math.Sin(Math.PI * t / 1.4), lowPass: 0.06),
            // A heavy, short thump.
            [Sound.ToadLand] = Sweep(120, 55, 0.22, 0.9),
            // Two soft wing beats.
            [Sound.Flutter] = Mix(Noise(random, 0.08, t => 0.35 * Fade(t, 0.08), lowPass: 0.15),
                                  Delay(Noise(random, 0.08, t => 0.3 * Fade(t, 0.08), lowPass: 0.15), 0.11)),
        };

        var files = new Dictionary<Sound, string>();
        foreach (var (sound, samples) in sounds)
        {
            var path = Path.Combine(folder, $"{sound}.wav");
            using (var file = File.Create(path))
                ToWav(samples).CopyTo(file);
            files[sound] = path;
        }

        return files;
    }

    private static double Fade(double t, double length) => Math.Exp(-4 * t / length);

    private static List<short> Tone(double frequency, double seconds, double volume) =>
        Generate(seconds, t => Math.Sin(2 * Math.PI * frequency * t) * volume * Fade(t, seconds));

    /// <summary>A sine whose pitch slides from one frequency to another.</summary>
    private static List<short> Sweep(double from, double to, double seconds, double volume)
    {
        var phase = 0.0;
        return Generate(seconds, t =>
        {
            phase += 2 * Math.PI * (from + (to - from) * t / seconds) / SampleRate;
            return Math.Sin(phase) * volume * Fade(t, seconds);
        });
    }

    /// <summary>White noise through a one-pole low-pass filter (smaller = darker).</summary>
    private static List<short> Noise(Random random, double seconds, Func<double, double> envelope, double lowPass)
    {
        var value = 0.0;
        return Generate(seconds, t =>
        {
            value += lowPass * ((random.NextDouble() * 2 - 1) - value);
            return value * envelope(t) * (1 / Math.Sqrt(lowPass));
        });
    }

    private static List<short> Generate(double seconds, Func<double, double> sample)
    {
        var count = (int)(SampleRate * seconds);
        var samples = new List<short>(count);
        for (var i = 0; i < count; i++)
        {
            var attack = Math.Min(1, i / (SampleRate * 0.003));   // avoid clicks at the start
            samples.Add((short)(Math.Clamp(sample((double)i / SampleRate) * attack, -1, 1) * short.MaxValue * 0.8));
        }

        return samples;
    }

    private static List<short> Delay(List<short> samples, double seconds)
    {
        var result = new List<short>(new short[(int)(SampleRate * seconds)]);
        result.AddRange(samples);
        return result;
    }

    private static List<short> Mix(List<short> a, List<short> b)
    {
        var result = new List<short>(Math.Max(a.Count, b.Count));
        for (var i = 0; i < Math.Max(a.Count, b.Count); i++)
        {
            var sum = (i < a.Count ? a[i] : 0) + (i < b.Count ? b[i] : 0);
            result.Add((short)Math.Clamp(sum, short.MinValue, short.MaxValue));
        }

        return result;
    }
}
