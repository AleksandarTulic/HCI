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
    private readonly Random _random = new();

    public GameWorld(LevelDefinition level, bool isRespawn = false)
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
        Player = new Player
        {
            X = level.PlayerStartX,
            Y = level.PlayerStartY,
            Invulnerable = isRespawn ? Tuning.RespawnInvulnerability : 0,
        };
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
    public List<Particle> Particles { get; } = new();

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

    /// <param name="fell">True when the player fell out of the level; there is nothing left to animate.</param>
    public void Kill(bool fell = false)
    {
        if (Player.IsDead || IsCompleted)
            return;

        var p = Player;
        p.IsDead = true;
        p.VelocityX = 0;
        p.VelocityY = fell ? 0 : -Tuning.DeathPopVelocity;
        _events.Add(GameEvent.PlayerDied);
    }

    public void Update(double dt, IInput input)
    {
        Time += dt;
        UpdateEffects(dt);
        UpdateParticles(dt);
        UpdateEnemies(dt);

        if (IsCompleted)
            return;

        if (Player.IsDead)
        {
            // The player pops up and falls off the screen, ignoring the level.
            Player.VelocityY = Math.Min(Player.VelocityY + Tuning.Gravity * dt, Tuning.MaxFallSpeed);
            Player.Y += Player.VelocityY * dt;
            return;
        }

        UpdatePlayer(dt, input);
        CheckInteractions(input);

        if (Player.Y > Level.Height)
            Kill(fell: true);
    }

    private void UpdatePlayer(double dt, IInput input)
    {
        var p = Player;
        var direction = (input.IsDown(GameAction.MoveRight) ? 1 : 0) - (input.IsDown(GameAction.MoveLeft) ? 1 : 0);

        p.AnimationTime += dt;
        p.Invulnerable = Math.Max(0, p.Invulnerable - dt);
        p.Squash *= Math.Exp(-12 * dt);

        // Horizontal speed eases towards the target instead of jumping to it.
        var target = direction * Tuning.WalkSpeed;
        var accelerating = direction != 0 && (Math.Sign(p.VelocityX) == direction || p.VelocityX == 0);
        var rate = (p.OnGround, accelerating) switch
        {
            (true, true) => Tuning.GroundAcceleration,
            (true, false) => Tuning.GroundDeceleration,
            (false, true) => Tuning.AirAcceleration,
            (false, false) => Tuning.AirDeceleration,
        };
        p.VelocityX = MoveTowards(p.VelocityX, target, rate * dt);
        if (direction != 0)
            p.FacingLeft = direction < 0;

        // Jump buffering and coyote time make jumps forgiving about exact timing.
        if (input.ConsumePressed(GameAction.Jump))
            p.JumpBuffer = Tuning.JumpBufferTime;
        else
            p.JumpBuffer = Math.Max(0, p.JumpBuffer - dt);

        p.TimeSinceGrounded = p.OnGround ? 0 : p.TimeSinceGrounded + dt;

        if (p.JumpBuffer > 0 && p.TimeSinceGrounded <= Tuning.CoyoteTime && !p.IsJumping)
            StartJump();

        // Releasing the key early cuts the jump short.
        if (p.IsJumping && p.VelocityY < 0 && !input.IsDown(GameAction.Jump))
        {
            p.VelocityY *= Tuning.JumpCutMultiplier;
            p.IsJumping = false;
        }

        var gravity = p.VelocityY > 0 ? Tuning.Gravity * Tuning.FallGravityMultiplier : Tuning.Gravity;
        p.VelocityY = Math.Min(p.VelocityY + gravity * dt, Tuning.MaxFallSpeed);

        MoveHorizontally(p.VelocityX * dt);
        MoveVertically(p.VelocityY * dt);

        if (p.VelocityY >= 0)
            p.IsJumping = false;

        p.SetAnimation(!p.OnGround ? PlayerAnimation.Jump
            : Math.Abs(p.VelocityX) > 20 ? PlayerAnimation.Walk
            : PlayerAnimation.Idle);
    }

    private void StartJump()
    {
        var p = Player;
        p.VelocityY = -Tuning.JumpVelocity;
        p.OnGround = false;
        p.IsJumping = true;
        p.JumpBuffer = 0;
        p.TimeSinceGrounded = Tuning.CoyoteTime + 1;   // no second jump from the same ledge
        p.Squash = -0.25;
        SpawnDust(p.Bounds.CenterX, p.Bounds.Bottom, 6, 160);
        _events.Add(GameEvent.Jump);
    }

    private static double MoveTowards(double value, double target, double maxStep) =>
        value < target ? Math.Min(value + maxStep, target) : Math.Max(value - maxStep, target);

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

            p.VelocityX = 0;
        }
    }

    private void MoveVertically(double dy)
    {
        var p = Player;
        var previousTop = p.Y;
        var wasOnGround = p.OnGround;
        var impactSpeed = p.VelocityY;
        p.Y += dy;
        p.OnGround = false;

        // The top of the level is a ceiling, like in the original game.
        if (p.Y < 0)
        {
            p.Y = 0;
            p.VelocityY = 0;
            p.IsJumping = false;
        }

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

        if (p.OnGround && !wasOnGround && impactSpeed > 300)
        {
            var strength = Math.Min(1, impactSpeed / Tuning.MaxFallSpeed);
            p.Squash = 0.3 * strength;
            SpawnDust(p.Bounds.CenterX, p.Bounds.Bottom, (int)(4 + 8 * strength), 220 * strength);
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

            p.IsJumping = false;

            if (box.CoinsLeft > 0)
            {
                box.CoinsLeft--;
                Points++;
                Effects.Add(new Effect(EffectKind.PoppedCoin, b.CenterX - Tuning.CoinSize / 2, b.Top - Tuning.CoinSize, 0.4));
                SpawnSparks(b.CenterX, b.Top - 8, 8);
                _events.Add(GameEvent.CoinCollected);
            }
            else
            {
                _events.Add(GameEvent.EmptyBoxHit);
            }

            return; // one box per bump
        }
    }

    private void CheckInteractions(IInput input)
    {
        var p = Player;
        var bounds = p.Bounds;

        foreach (var coin in Coins)
        {
            if (!coin.Collected && bounds.Overlaps(coin.Bounds))
            {
                coin.Collected = true;
                Points++;
                SpawnSparks(coin.Bounds.CenterX, coin.Bounds.CenterY, 10);
                _events.Add(GameEvent.CoinCollected);
            }
        }

        foreach (var enemy in Enemies)
        {
            if (!enemy.Alive || !bounds.Overlaps(enemy.Bounds))
                continue;

            var isStomp = p.VelocityY > 0 && bounds.Bottom <= enemy.Bounds.Top + Tuning.StompTolerance;
            if (isStomp)
            {
                enemy.Alive = false;
                var held = input.IsDown(GameAction.Jump);
                p.VelocityY = -(held ? Tuning.StompBounceHeld : Tuning.StompBounce);
                p.IsJumping = held;
                p.Squash = -0.2;
                Points += enemy.Points;
                Effects.Add(new Effect(EffectKind.ScorePopup, enemy.X, enemy.Y, 0.8, enemy.Points.ToString()));
                SpawnDust(enemy.Bounds.CenterX, enemy.Bounds.CenterY, 12, 260);
                _events.Add(GameEvent.EnemyStomped);
            }
            else if (p.Invulnerable <= 0)
            {
                Kill();
                return;
            }
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

    private void UpdateParticles(double dt)
    {
        foreach (var particle in Particles)
        {
            particle.Age += dt;
            particle.VelocityY += particle.Gravity * dt;
            particle.X += particle.VelocityX * dt;
            particle.Y += particle.VelocityY * dt;
        }

        Particles.RemoveAll(p => p.Age >= p.Life);
    }

    private void SpawnDust(double x, double y, int count, double speed)
    {
        for (var i = 0; i < count; i++)
        {
            var side = i % 2 == 0 ? -1 : 1;
            Particles.Add(new Particle
            {
                Kind = ParticleKind.Dust,
                X = x + side * _random.NextDouble() * 14,
                Y = y - _random.NextDouble() * 6,
                VelocityX = side * speed * (0.4 + _random.NextDouble() * 0.6),
                VelocityY = -speed * 0.35 * _random.NextDouble(),
                Gravity = 300,
                Size = 5 + _random.NextDouble() * 6,
                Life = 0.35 + _random.NextDouble() * 0.25,
            });
        }
    }

    private void SpawnSparks(double x, double y, int count)
    {
        for (var i = 0; i < count; i++)
        {
            var angle = _random.NextDouble() * Math.PI * 2;
            var speed = 120 + _random.NextDouble() * 160;
            Particles.Add(new Particle
            {
                Kind = ParticleKind.Spark,
                X = x,
                Y = y,
                VelocityX = Math.Cos(angle) * speed,
                VelocityY = Math.Sin(angle) * speed - 80,
                Gravity = 500,
                Size = 3 + _random.NextDouble() * 3,
                Life = 0.3 + _random.NextDouble() * 0.2,
            });
        }
    }
}
