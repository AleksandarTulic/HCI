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

    private System.Windows.Rect? _content;

    public int FrameCount => _frames.Length;

    /// <summary>
    /// The part of the frames that is not transparent, over all frames, in pixels.
    /// Lets a sprite with empty space around it be drawn so the creature itself fills a given area.
    /// </summary>
    public System.Windows.Rect ContentBounds => _content ??= MeasureContent();

    private System.Windows.Rect MeasureContent()
    {
        int left = int.MaxValue, top = int.MaxValue, right = -1, bottom = -1;
        foreach (var frame in _frames)
        {
            var converted = new FormatConvertedBitmap(frame, System.Windows.Media.PixelFormats.Bgra32, null, 0);
            int width = converted.PixelWidth, height = converted.PixelHeight;
            var pixels = new byte[width * height * 4];
            converted.CopyPixels(pixels, width * 4, 0);
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    if (pixels[(y * width + x) * 4 + 3] < 24)
                        continue;
                    left = Math.Min(left, x);
                    right = Math.Max(right, x);
                    top = Math.Min(top, y);
                    bottom = Math.Max(bottom, y);
                }
            }
        }

        return right < 0
            ? new System.Windows.Rect(0, 0, _frames[0].PixelWidth, _frames[0].PixelHeight)
            : new System.Windows.Rect(left, top, right - left + 1, bottom - top + 1);
    }

    /// <summary>A specific frame, for animations driven by state rather than time.</summary>
    public BitmapSource Frame(int index) => _frames[System.Math.Clamp(index, 0, _frames.Length - 1)];

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
