using System;
using System.Collections.Generic;
using System.Windows.Media.Imaging;
using WhereAmI.Core;

namespace WhereAmI.Rendering;

/// <summary>Loads each image once and shares it. Images are frozen so WPF can render them cheaply.</summary>
public static class SpriteCache
{
    private static readonly Dictionary<string, BitmapSource> Cache = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Image for a path like <c>sprites/coin/popped.png</c>.</summary>
    public static BitmapSource Get(string relativePath) => LoadFile(AssetPaths.Get(relativePath));

    public static BitmapSource LoadFile(string fullPath)
    {
        if (Cache.TryGetValue(fullPath, out var cached))
            return cached;

        var image = new BitmapImage();
        image.BeginInit();
        image.UriSource = new Uri(fullPath);
        image.CacheOption = BitmapCacheOption.OnLoad;   // read now, do not keep the file open
        image.EndInit();
        image.Freeze();

        Cache[fullPath] = image;
        return image;
    }
}
