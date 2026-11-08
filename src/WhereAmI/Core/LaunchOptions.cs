using System;
using System.Collections.Generic;

namespace WhereAmI.Core;

/// <summary>
/// Command-line options:
/// <list type="bullet">
/// <item><c>--windowed [WxH]</c> start in a window (optionally with that client size) instead of the saved display mode.</item>
/// <item><c>--no-save</c> start with fresh settings and progress and never write them (useful for testing).</item>
/// </list>
/// </summary>
public sealed class LaunchOptions
{
    /// <summary>Null means "use the saved setting".</summary>
    public bool? Fullscreen { get; private set; }
    public int? WindowWidth { get; private set; }
    public int? WindowHeight { get; private set; }
    public bool NoSave { get; private set; }

    public static LaunchOptions Parse(IReadOnlyList<string> args)
    {
        var options = new LaunchOptions();

        for (var i = 0; i < args.Count; i++)
        {
            if (args[i].Equals("--no-save", StringComparison.OrdinalIgnoreCase))
            {
                options.NoSave = true;
            }
            else if (args[i].Equals("--windowed", StringComparison.OrdinalIgnoreCase))
            {
                options.Fullscreen = false;
                if (i + 1 < args.Count && TryParseSize(args[i + 1], out var w, out var h))
                {
                    options.WindowWidth = w;
                    options.WindowHeight = h;
                    i++;
                }
            }
        }

        return options;
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
