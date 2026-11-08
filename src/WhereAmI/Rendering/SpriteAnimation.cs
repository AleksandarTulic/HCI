using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Media.Imaging;
using WhereAmI.Core;

namespace WhereAmI.Rendering;

public enum PlaybackMode
{
    Loop,
    PingPong,
    Once,
}

/// <summary>A sequence of frames played at a fixed rate, selected by elapsed time.</summary>
public sealed class SpriteAnimation
{
    private readonly BitmapSource[] _frames;
    private readonly double _framesPerSecond;
    private readonly PlaybackMode _mode;

    public SpriteAnimation(IEnumerable<BitmapSource> frames, double framesPerSecond, PlaybackMode mode)
    {
        _frames = frames.ToArray();
        if (_frames.Length == 0)
            throw new ArgumentException("An animation needs at least one frame.", nameof(frames));

        _framesPerSecond = framesPerSecond;
        _mode = mode;
    }

    /// <summary>Loads every PNG in an asset folder, ordered by file name.</summary>
    public static SpriteAnimation FromFolder(string relativeFolder, double framesPerSecond, PlaybackMode mode)
    {
        var files = Directory.GetFiles(AssetPaths.Get(relativeFolder), "*.png")
            .OrderBy(f => f, StringComparer.OrdinalIgnoreCase);
        return new SpriteAnimation(files.Select(SpriteCache.LoadFile), framesPerSecond, mode);
    }

    public BitmapSource FrameAt(double seconds)
    {
        var n = _frames.Length;
        var step = (int)(Math.Max(0, seconds) * _framesPerSecond);

        var index = _mode switch
        {
            PlaybackMode.Loop => step % n,
            PlaybackMode.Once => Math.Min(step, n - 1),
            _ => PingPong(step, n),
        };

        return _frames[index];
    }

    private static int PingPong(int step, int n)
    {
        if (n == 1)
            return 0;

        var period = 2 * (n - 1);
        var t = step % period;
        return t < n ? t : period - t;
    }
}
