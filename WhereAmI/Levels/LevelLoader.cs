using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using WhereAmI.Game;

namespace WhereAmI.Levels;

/// <summary>Loads levels from <c>level-*.xml</c> files. See docs/level-format.md.</summary>
public static class LevelLoader
{
    public static IReadOnlyList<LevelDefinition> LoadAll(string directory)
    {
        var files = Directory.GetFiles(directory, "level-*.xml")
            .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (files.Count == 0)
            throw new InvalidDataException($"No level files found in '{directory}'.");

        return files.Select(Load).ToList();
    }

    public static LevelDefinition Load(string path)
    {
        var doc = XDocument.Load(path, LoadOptions.SetLineInfo);
        var root = doc.Root ?? throw Error(path, null, "File is empty.");
        if (root.Name != "Level")
            throw Error(path, root, "Root element must be <Level>.");

        var start = root.Element("PlayerStart") ?? throw Error(path, root, "Missing <PlayerStart>.");

        var level = new LevelDefinition
        {
            Name = Text(path, root, "name"),
            Background = (string)root.Attribute("background"),
            Width = Number(path, root, "width"),
            Height = Number(path, root, "height"),
            TimeLimit = Number(path, root, "timeLimit"),
            PlayerStartX = Number(path, start, "x"),
            PlayerStartY = Number(path, start, "y"),
        };

        foreach (var e in root.Elements())
        {
            switch (e.Name.LocalName)
            {
                case "PlayerStart":
                    break;
                case "Ground":
                    level.Ground.Add(ReadBox(path, e));
                    break;
                case "Spikes":
                    level.Spikes.Add(ReadBox(path, e));
                    break;
                case "Goal":
                    level.Goals.Add(ReadBox(path, e));
                    break;
                case "Coin":
                    level.Coins.Add((Number(path, e, "x"), Number(path, e, "y")));
                    break;
                case "RewardBox":
                    level.RewardBoxes.Add((Number(path, e, "x"), Number(path, e, "y"),
                        (int)Number(path, e, "coins"), Flag(path, e, "hidden")));
                    break;
                case "Enemy":
                    level.Enemies.Add((ReadEnemyKind(path, e), Number(path, e, "x"), Number(path, e, "y"),
                        Number(path, e, "range")));
                    break;
                case "MovingPlatform":
                    level.MovingPlatforms.Add((ReadBox(path, e), Number(path, e, "toX"), Number(path, e, "toY"),
                        Number(path, e, "speed")));
                    break;
                case "Checkpoint":
                    level.Checkpoints.Add((Number(path, e, "x"), Number(path, e, "y")));
                    break;
                case "Hint":
                    level.Hints.Add((ReadBox(path, e), Text(path, e, "text")));
                    break;
                default:
                    throw Error(path, e, $"Unknown element <{e.Name.LocalName}>.");
            }
        }

        if (level.Goals.Count == 0)
            throw Error(path, root, "A level needs at least one <Goal>.");

        return level;
    }

    private static Box ReadBox(string path, XElement e) =>
        new(Number(path, e, "x"), Number(path, e, "y"), Number(path, e, "w"), Number(path, e, "h"));

    private static EnemyKind ReadEnemyKind(string path, XElement e) =>
        Text(path, e, "kind") switch
        {
            "ghost" => EnemyKind.Ghost,
            "scribble" => EnemyKind.Scribble,
            var other => throw Error(path, e, $"Unknown enemy kind '{other}'."),
        };

    private static string Text(string path, XElement e, string name) =>
        (string)e.Attribute(name) ?? throw Error(path, e, $"Missing attribute '{name}'.");

    private static double Number(string path, XElement e, string name)
    {
        var text = Text(path, e, name);
        return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
            ? value
            : throw Error(path, e, $"Attribute '{name}' is not a number: '{text}'.");
    }

    private static bool Flag(string path, XElement e, string name)
    {
        var text = (string)e.Attribute(name);
        return text switch
        {
            null or "false" => false,
            "true" => true,
            _ => throw Error(path, e, $"Attribute '{name}' must be true or false."),
        };
    }

    private static InvalidDataException Error(string path, XElement e, string message)
    {
        var line = e is IXmlLineInfo info && info.HasLineInfo() ? $":{info.LineNumber}" : "";
        return new InvalidDataException($"{Path.GetFileName(path)}{line}: {message}");
    }
}
