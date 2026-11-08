using System.Collections.Generic;
using WhereAmI.Game;

namespace WhereAmI.Levels;

/// <summary>Static description of a level, as loaded from an XML file.</summary>
public sealed class LevelDefinition
{
    public string Name { get; init; }
    public string Background { get; init; }
    public double Width { get; init; }
    public double Height { get; init; }
    public double TimeLimit { get; init; }
    public bool ShowTutorial { get; init; }
    public double PlayerStartX { get; init; }
    public double PlayerStartY { get; init; }

    public List<Box> Ground { get; } = new();
    public List<Box> Spikes { get; } = new();
    public List<Box> Goals { get; } = new();
    public List<(double X, double Y)> Coins { get; } = new();
    public List<(double X, double Y, int Coins, bool Hidden)> RewardBoxes { get; } = new();
    public List<(EnemyKind Kind, double X, double Y, double Range)> Enemies { get; } = new();
}
