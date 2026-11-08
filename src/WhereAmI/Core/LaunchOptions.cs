using System;
using System.Collections.Generic;

namespace WhereAmI.Core;

/// <summary>
/// Command-line options.
/// <c>--windowed 1280x720</c> starts in a window with that client size;
/// <c>--windowed</c> alone uses a default size. Without options the game starts fullscreen.
/// </summary>
public sealed class LaunchOptions
{
    public bool Fullscreen { get; private init; } = true;
    public int? WindowWidth { get; private init; }
    public int? WindowHeight { get; private init; }

    public static LaunchOptions Parse(IReadOnlyList<string> args)
    {
        for (var i = 0; i < args.Count; i++)
        {
            if (!args[i].Equals("--windowed", StringComparison.OrdinalIgnoreCase))
                continue;

            if (i + 1 < args.Count && TryParseSize(args[i + 1], out var w, out var h))
                return new LaunchOptions { Fullscreen = false, WindowWidth = w, WindowHeight = h };

            return new LaunchOptions { Fullscreen = false };
        }

        return new LaunchOptions();
    }

    private static bool TryParseSize(string text, out int width, out int height)
    {
        width = height = 0;
        var parts = text.Split('x', 'X');
        return parts.Length == 2
               && int.TryParse(parts[0], out width) && width > 0
               && int.TryParse(parts[1], out height) && height > 0;
    }
}
