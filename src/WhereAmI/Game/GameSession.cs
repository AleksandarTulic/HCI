using System;
using System.Collections.Generic;
using WhereAmI.Input;
using WhereAmI.Levels;

namespace WhereAmI.Game;

public enum SessionState
{
    Playing,
    LevelComplete,
    GameOver,
    Victory,
}

/// <summary>Summary of a finished level, shown on the level complete screen and saved as progress.</summary>
public sealed record LevelResult(
    int LevelIndex,
    string LevelName,
    int Coins,
    int TotalCoins,
    int Enemies,
    int TotalEnemies,
    int LevelPoints,
    int TimeBonus,
    int Stars)
{
    public int Score => LevelPoints + TimeBonus;

    /// <summary>1 star for finishing, 2 for at least half the coins, 3 for every coin.</summary>
    public static int StarsFor(int coins, int totalCoins) =>
        totalCoins == 0 || coins >= totalCoins ? 3
        : coins * 2 >= totalCoins ? 2
        : 1;
}

/// <summary>A play-through starting at some level: lives, score, timer and moving between levels.</summary>
public sealed class GameSession
{
    public const int TimeBonusPerSecond = 5;

    private readonly IReadOnlyList<LevelDefinition> _levels;
    private readonly List<GameEvent> _events = new();

    private int _pointsAtLevelStart;
    private double _respawnTimer;

    public GameSession(IReadOnlyList<LevelDefinition> levels, int startLevel = 0)
    {
        if (levels.Count == 0)
            throw new ArgumentException("At least one level is required.", nameof(levels));

        _levels = levels;
        Lives = Tuning.StartingLives;
        StartLevel(Math.Clamp(startLevel, 0, levels.Count - 1));
    }

    public SessionState State { get; private set; }
    public int LevelIndex { get; private set; }
    public int LevelCount => _levels.Count;
    public int Lives { get; private set; }
    public double TimeLeft { get; private set; }
    public GameWorld World { get; private set; }

    /// <summary>Set when a level is finished, until the next one starts.</summary>
    public LevelResult LastResult { get; private set; }

    /// <summary>Score so far. Outside of play, an unfinished attempt does not count.</summary>
    public int Points => State == SessionState.Playing ? _pointsAtLevelStart + World.Points : _pointsAtLevelStart;

    public bool IsLastLevel => LevelIndex == _levels.Count - 1;

    /// <summary>Returns and clears session and world events since the last call.</summary>
    public IReadOnlyList<GameEvent> DrainEvents()
    {
        _events.AddRange(World.DrainEvents());
        var events = _events.ToArray();
        _events.Clear();
        return events;
    }

    public void Update(double dt, IInput input)
    {
        if (State != SessionState.Playing)
        {
            World.UpdateAmbient(dt);
            return;
        }

        World.Update(dt, input);

        if (World.IsCompleted)
        {
            CompleteLevel();
            return;
        }

        if (World.Player.IsDead)
        {
            _respawnTimer += dt;
            if (_respawnTimer >= Tuning.RespawnDelay)
                LoseLife();
            return;
        }

        TimeLeft = Math.Max(0, TimeLeft - dt);
        if (TimeLeft == 0)
            World.Kill();
    }

    /// <summary>After the level complete screen: go to the next level, or finish the game.</summary>
    public void ContinueAfterLevel()
    {
        if (State != SessionState.LevelComplete)
            return;

        if (IsLastLevel)
        {
            State = SessionState.Victory;
            _events.Add(GameEvent.Victory);
        }
        else
        {
            StartLevel(LevelIndex + 1);
        }
    }

    /// <summary>Starts the current level again without losing a life (from the pause menu).</summary>
    public void RestartLevel()
    {
        if (State == SessionState.Playing)
            StartLevel(LevelIndex);
    }

    private void CompleteLevel()
    {
        var world = World;
        var timeBonus = (int)Math.Ceiling(TimeLeft) * TimeBonusPerSecond;
        LastResult = new LevelResult(
            LevelIndex,
            world.Level.Name,
            world.CoinsCollected,
            world.TotalCoins,
            world.EnemiesDefeated,
            world.TotalEnemies,
            world.Points,
            timeBonus,
            LevelResult.StarsFor(world.CoinsCollected, world.TotalCoins));

        _pointsAtLevelStart += LastResult.Score;
        _events.AddRange(world.DrainEvents());
        State = SessionState.LevelComplete;
    }

    private void LoseLife()
    {
        Lives--;
        _events.AddRange(World.DrainEvents());

        if (Lives > 0)
        {
            // Points earned in a failed attempt are lost, so coins cannot be farmed by dying.
            StartLevel(LevelIndex, isRespawn: true);
        }
        else
        {
            State = SessionState.GameOver;
            _events.Add(GameEvent.GameOver);
        }
    }

    private void StartLevel(int index, bool isRespawn = false)
    {
        LevelIndex = index;
        World = new GameWorld(_levels[index], isRespawn);
        TimeLeft = _levels[index].TimeLimit;
        State = SessionState.Playing;
        _respawnTimer = 0;
        LastResult = null;
    }
}
