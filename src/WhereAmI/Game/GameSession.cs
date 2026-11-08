using System;
using System.Collections.Generic;
using WhereAmI.Input;
using WhereAmI.Levels;

namespace WhereAmI.Game;

public enum SessionState
{
    Playing,
    GameOver,
    Victory,
}

/// <summary>A play-through: lives, score, timer and moving between levels.</summary>
public sealed class GameSession
{
    private readonly IReadOnlyList<LevelDefinition> _levels;
    private readonly List<GameEvent> _events = new();

    private int _pointsAtLevelStart;
    private double _respawnTimer;

    public GameSession(IReadOnlyList<LevelDefinition> levels)
    {
        if (levels.Count == 0)
            throw new ArgumentException("At least one level is required.", nameof(levels));

        _levels = levels;
        NewGame();
    }

    public SessionState State { get; private set; }
    public int LevelIndex { get; private set; }
    public int LevelCount => _levels.Count;
    public int Lives { get; private set; }
    public double TimeLeft { get; private set; }
    public GameWorld World { get; private set; }

    /// <summary>Score so far. After the game ends, the unfinished attempt no longer counts.</summary>
    public int Points => State == SessionState.Playing ? _pointsAtLevelStart + World.Points : _pointsAtLevelStart;

    public void NewGame()
    {
        Lives = Tuning.StartingLives;
        _pointsAtLevelStart = 0;
        StartLevel(0);
    }

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
            if (input.ConsumePressed(GameAction.Confirm))
                NewGame();
            return;
        }

        World.Update(dt, input);

        if (World.IsCompleted)
        {
            _pointsAtLevelStart = Points;
            _events.AddRange(World.DrainEvents());

            if (LevelIndex + 1 < _levels.Count)
            {
                StartLevel(LevelIndex + 1);
            }
            else
            {
                State = SessionState.Victory;
                _events.Add(GameEvent.Victory);
            }

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
    }
}
