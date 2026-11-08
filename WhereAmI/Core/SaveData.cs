using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using WhereAmI.Game;

namespace WhereAmI.Core;

/// <summary>Player settings, stored in %AppData%\WhereAmI\settings.xml.</summary>
public sealed class Settings
{
    public double MusicVolume { get; set; } = 0.05;
    public double EffectsVolume { get; set; } = 0.05;
    public bool Fullscreen { get; set; } = true;
    public bool ScreenShake { get; set; } = true;
}

/// <summary>Best result for one level.</summary>
public sealed class LevelRecord
{
    public int BestScore { get; set; }
    public int BestCoins { get; set; }
    public int BestStars { get; set; }
}

/// <summary>Which levels are unlocked and the best results, stored in %AppData%\WhereAmI\progress.xml.</summary>
public sealed class Progress
{
    /// <summary>How many levels can be played; the first is always open.</summary>
    public int UnlockedLevels { get; set; } = 1;
    public bool SeenIntro { get; set; }
    public Dictionary<int, LevelRecord> Levels { get; } = new();

    public LevelRecord RecordFor(int levelIndex) =>
        Levels.TryGetValue(levelIndex, out var record) ? record : null;

    /// <summary>Keeps the best values and unlocks the next level.</summary>
    public void Record(LevelResult result, int levelCount)
    {
        if (!Levels.TryGetValue(result.LevelIndex, out var record))
            Levels[result.LevelIndex] = record = new LevelRecord();

        record.BestScore = Math.Max(record.BestScore, result.Score);
        record.BestCoins = Math.Max(record.BestCoins, result.Coins);
        record.BestStars = Math.Max(record.BestStars, result.Stars);
        UnlockedLevels = Math.Clamp(Math.Max(UnlockedLevels, result.LevelIndex + 2), 1, levelCount);
    }
}

/// <summary>Loads and saves <see cref="Settings"/> and <see cref="Progress"/> as small XML files.</summary>
public static class SaveStore
{
    private static readonly string Folder =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "WhereAmI");

    private static string SettingsPath => Path.Combine(Folder, "settings.xml");
    private static string ProgressPath => Path.Combine(Folder, "progress.xml");

    public static Settings LoadSettings()
    {
        var settings = new Settings();
        var root = TryLoad(SettingsPath);
        if (root is null)
            return settings;

        settings.MusicVolume = Math.Clamp(Number(root, "musicVolume", settings.MusicVolume), 0, 1);
        settings.EffectsVolume = Math.Clamp(Number(root, "effectsVolume", settings.EffectsVolume), 0, 1);
        settings.Fullscreen = Flag(root, "fullscreen", settings.Fullscreen);
        settings.ScreenShake = Flag(root, "screenShake", settings.ScreenShake);
        return settings;
    }

    public static void Save(Settings settings) =>
        TrySave(SettingsPath, new XElement("Settings",
            new XAttribute("musicVolume", Format(settings.MusicVolume)),
            new XAttribute("effectsVolume", Format(settings.EffectsVolume)),
            new XAttribute("fullscreen", settings.Fullscreen),
            new XAttribute("screenShake", settings.ScreenShake)));

    public static Progress LoadProgress()
    {
        var progress = new Progress();
        var root = TryLoad(ProgressPath);
        if (root is null)
            return progress;

        progress.UnlockedLevels = Math.Max(1, (int)Number(root, "unlockedLevels", 1));
        progress.SeenIntro = Flag(root, "seenIntro", false);
        foreach (var level in root.Elements("Level"))
        {
            progress.Levels[(int)Number(level, "index", 0)] = new LevelRecord
            {
                BestScore = (int)Number(level, "bestScore", 0),
                BestCoins = (int)Number(level, "bestCoins", 0),
                BestStars = (int)Number(level, "bestStars", 0),
            };
        }

        return progress;
    }

    public static void Save(Progress progress) =>
        TrySave(ProgressPath, new XElement("Progress",
            new XAttribute("unlockedLevels", progress.UnlockedLevels),
            new XAttribute("seenIntro", progress.SeenIntro),
            progress.Levels.OrderBy(l => l.Key).Select(l => new XElement("Level",
                new XAttribute("index", l.Key),
                new XAttribute("bestScore", l.Value.BestScore),
                new XAttribute("bestCoins", l.Value.BestCoins),
                new XAttribute("bestStars", l.Value.BestStars)))));

    // A broken or unreadable save file must never stop the game from starting.
    private static XElement TryLoad(string path)
    {
        try
        {
            return File.Exists(path) ? XDocument.Load(path).Root : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Xml.XmlException)
        {
            return null;
        }
    }

    private static void TrySave(string path, XElement root)
    {
        try
        {
            Directory.CreateDirectory(Folder);
            new XDocument(root).Save(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Saving is best effort; the game keeps working without it.
        }
    }

    private static double Number(XElement e, string name, double fallback) =>
        double.TryParse((string)e.Attribute(name), NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : fallback;

    private static bool Flag(XElement e, string name, bool fallback) =>
        bool.TryParse((string)e.Attribute(name), out var v) ? v : fallback;

    private static string Format(double value) => value.ToString("0.###", CultureInfo.InvariantCulture);
}
