using System;
using System.IO;

namespace WhereAmI.Core;

/// <summary>Resolves asset paths relative to the Assets folder next to the executable.</summary>
public static class AssetPaths
{
    public static string Root { get; } = Path.Combine(AppContext.BaseDirectory, "Assets");

    /// <summary>Full path for a path like <c>sprites/coin/popped.png</c>.</summary>
    public static string Get(string relativePath) =>
        Path.Combine(Root, relativePath.Replace('/', Path.DirectorySeparatorChar));
}
