using System;
using System.Collections.Generic;
using System.Linq;
using WhereAmI.Input;
using WhereAmI.Levels;

namespace WhereAmI.Game;

/// <summary>Everything inside one running level: player, level geometry, enemies and pickups.</summary>
public sealed class GameWorld
{
    private readonly List<GameEvent> _events = new();

    public GameWorld(LevelDefinition level)
    {
        Level = level;
        Ground = level.Ground.ToList();
        Spikes = level.Spikes.ToList();
        Goals = level.Goals.ToList();
        Coins = level.Coins
            .Select(c => new Coin(new Box(c.X, c.Y, Tuning.CoinSize, Tuning.CoinSize)))
            .ToList();
        RewardBoxes = level.RewardBoxes
            .Select(b => new RewardBox(new Box(b.X, b.Y, Tuning.RewardBoxSize, Tuning.RewardBoxSize), b.Coins, b.Hidden))
            .ToList();
        Enemies = level.Enemies.Select(e => new Enemy(e.Kind, e.X, e.Y, e.Range)).ToList();
        Player = new Player { X = level.PlayerStartX, Y = level.PlayerStartY };
    }

    public LevelDefinition Level { get; }
    public Player Player { get; }
    public IReadOnlyList<Box> Ground { get; }
    public IReadOnlyList<Box> Spikes { get; }
    public IReadOnlyList<Box> Goals { get; }
    public IReadOnlyList<Coin> Coins { get; }
    public IReadOnlyList<RewardBox> RewardBoxes { get; }
    public IReadOnlyList<Enemy> Enemies { get; }
    public List<Effect> Effects { get; } = new();

    /// <summary>Points earned in this world since it was created.</summary>
    public int Points { get; private set; }

    public bool IsCompleted { get; private set; }
    public double Time { get; private set; }

    /// <summary>Returns and clears the events raised since the last call.</summary>
    public IReadOnlyList<GameEvent> DrainEvents()
    {
        var events = _events.ToArray();
        _events.Clear();
        return events;
    }

    public void Kill()
    {
        if (Player.IsDead || IsCompleted)
            return;

        Player.IsDead = true;
        _events.Add(GameEvent.PlayerDied);
    }

    public void Update(double dt, IInput input)
    {
        Time += dt;
        UpdateEffects(dt);
        UpdateEnemies(dt);

        if (Player.IsDead || IsCompleted)
            return;

        UpdatePlayer(dt, input);
        CheckInteractions();

        if (Player.Y > Level.Height)
            Kill();
    }

    private void UpdatePlayer(double dt, IInput input)
    {
        var p = Player;
        var direction = (input.IsDown(GameAction.MoveRight) ? 1 : 0) - (input.IsDown(GameAction.MoveLeft) ? 1 : 0);

        p.VelocityX = direction * Tuning.WalkSpeed;
        if (direction != 0)
            p.FacingLeft = direction < 0;

        if (input.ConsumePressed(GameAction.Jump) && p.OnGround)
        {
            p.VelocityY = -Tuning.JumpVelocity;
            p.OnGround = false;
            _events.Add(GameEvent.Jump);
        }

        p.VelocityY = Math.Min(p.VelocityY + Tuning.Gravity * dt, Tuning.MaxFallSpeed);

        MoveHorizontally(p.VelocityX * dt);
        MoveVertically(p.VelocityY * dt);

        p.AnimationTime += dt;
        p.SetAnimation(!p.OnGround ? PlayerAnimation.Jump
            : direction != 0 ? PlayerAnimation.Walk
            : PlayerAnimation.Idle);
    }

    private IEnumerable<Box> Solids() =>
        Ground.Concat(RewardBoxes.Where(b => b.IsRevealed).Select(b => b.Bounds));

    private void MoveHorizontally(double dx)
    {
        var p = Player;
        p.X = Math.Clamp(p.X + dx, 0, Level.Width - Tuning.PlayerWidth);

        foreach (var solid in Solids())
        {
            if (!p.Bounds.Overlaps(solid))
                continue;

            if (dx > 0)
                p.X = solid.Left - Tuning.PlayerWidth;
            else if (dx < 0)
                p.X = solid.Right;
        }
    }

    private void MoveVertically(double dy)
    {
        var p = Player;
        var previousTop = p.Y;
        p.Y += dy;
        p.OnGround = false;

        foreach (var solid in Solids())
        {
            if (!p.Bounds.Overlaps(solid))
                continue;

            if (dy > 0)
            {
                p.Y = solid.Top - Tuning.PlayerHeight;
                p.VelocityY = 0;
                p.OnGround = true;
            }
            else if (dy < 0)
            {
                p.Y = solid.Bottom;
                p.VelocityY = 0;
            }
        }

        if (dy < 0)
            HitBoxesFromBelow(previousTop);
    }

    /// <summary>Reward boxes give a coin when the player's head hits them from below.</summary>
    private void HitBoxesFromBelow(double previousTop)
    {
        var p = Player;
        var head = new Box(p.X, p.Y - 1, Tuning.PlayerWidth, 1);

        foreach (var box in RewardBoxes)
        {
            var b = box.Bounds;
            var hit = box.IsRevealed
                ? p.VelocityY == 0 && head.Overlaps(b) && Math.Abs(p.Y - b.Bottom) < 0.5
                : p.Bounds.Overlaps(b) && previousTop >= b.Bottom - 0.5;

            if (!hit)
                continue;

            if (!box.IsRevealed)
            {
                box.IsRevealed = true;
                p.Y = b.Bottom;
                p.VelocityY = 0;
            }

            if (box.CoinsLeft > 0)
            {
                box.CoinsLeft--;
                Points++;
                Effects.Add(new Effect(EffectKind.PoppedCoin, b.CenterX - Tuning.CoinSize / 2, b.Top - Tuning.CoinSize, 0.4));
                _events.Add(GameEvent.CoinCollected);
            }
            else
            {
                _events.Add(GameEvent.EmptyBoxHit);
            }

            return; // one box per bump
        }
    }

    private void CheckInteractions()
    {
        var p = Player;
        var bounds = p.Bounds;

        foreach (var coin in Coins)
        {
            if (!coin.Collected && bounds.Overlaps(coin.Bounds))
            {
                coin.Collected = true;
                Points++;
                _events.Add(GameEvent.CoinCollected);
            }
        }

        foreach (var enemy in Enemies)
        {
            if (!enemy.Alive || !bounds.Overlaps(enemy.Bounds))
                continue;

            var isStomp = p.VelocityY > 0 && bounds.Bottom <= enemy.Bounds.Top + Tuning.StompTolerance;
            if (!isStomp)
            {
                Kill();
                return;
            }

            enemy.Alive = false;
            p.VelocityY = -Tuning.StompBounce;
            Points += enemy.Points;
            Effects.Add(new Effect(EffectKind.ScorePopup, enemy.X, enemy.Y, 0.8, enemy.Points.ToString()));
            _events.Add(GameEvent.EnemyStomped);
        }

        if (Spikes.Any(bounds.Overlaps))
        {
            Kill();
            return;
        }

        if (Goals.Any(bounds.Overlaps))
        {
            IsCompleted = true;
            _events.Add(GameEvent.LevelCompleted);
        }
    }

    private void UpdateEnemies(double dt)
    {
        foreach (var enemy in Enemies)
        {
            if (!enemy.Alive)
                continue;

            enemy.AnimationTime += dt;
            enemy.X += enemy.VelocityX * dt;

            if (enemy.X <= enemy.MinX)
            {
                enemy.X = enemy.MinX;
                enemy.VelocityX = Tuning.EnemySpeed;
            }
            else if (enemy.X >= enemy.MaxX)
            {
                enemy.X = enemy.MaxX;
                enemy.VelocityX = -Tuning.EnemySpeed;
            }
        }
    }

    private void UpdateEffects(double dt)
    {
        foreach (var effect in Effects)
            effect.Age += dt;

        Effects.RemoveAll(e => e.Age >= e.Duration);
    }
}
