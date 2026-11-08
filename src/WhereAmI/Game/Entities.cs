using System;

namespace WhereAmI.Game;

public enum PlayerAnimation
{
    Idle,
    Walk,
    Jump,
}

public sealed class Player
{
    public double X;
    public double Y;
    public double VelocityX;
    public double VelocityY;
    public bool OnGround;
    public bool FacingLeft;
    public bool IsDead;

    /// <summary>True while rising from a jump the player started, so releasing the key can cut it short.</summary>
    public bool IsJumping;
    public double TimeSinceGrounded;
    public double JumpBuffer;
    public double Invulnerable;

    /// <summary>Visual squash (+) or stretch (−) amount, decays back to 0.</summary>
    public double Squash;

    public PlayerAnimation Animation = PlayerAnimation.Idle;
    public double AnimationTime;

    public Box Bounds => new(X, Y, Tuning.PlayerWidth, Tuning.PlayerHeight);

    public void SetAnimation(PlayerAnimation animation)
    {
        if (Animation == animation)
            return;

        Animation = animation;
        AnimationTime = 0;
    }
}

public sealed class RewardBox
{
    public RewardBox(Box bounds, int coins, bool hidden)
    {
        Bounds = bounds;
        CoinsLeft = coins;
        IsRevealed = !hidden;
    }

    public Box Bounds { get; }
    public int CoinsLeft { get; set; }

    /// <summary>Hidden boxes are invisible and not solid until hit from below.</summary>
    public bool IsRevealed { get; set; }
}

public sealed class Coin
{
    public Coin(Box bounds) => Bounds = bounds;

    public Box Bounds { get; }
    public bool Collected { get; set; }
}

/// <summary>A solid platform that glides back and forth between two points.</summary>
public sealed class MovingPlatform
{
    private readonly double _startX;
    private readonly double _startY;
    private readonly double _endX;
    private readonly double _endY;
    private readonly double _duration;
    private double _time;

    public MovingPlatform(Box start, double toX, double toY, double speed)
    {
        _startX = start.X;
        _startY = start.Y;
        _endX = toX;
        _endY = toY;
        Width = start.Width;
        Height = start.Height;
        X = start.X;
        Y = start.Y;

        var distance = Math.Sqrt((toX - start.X) * (toX - start.X) + (toY - start.Y) * (toY - start.Y));
        _duration = Math.Max(0.1, distance / Math.Max(1, speed));
    }

    public double X { get; private set; }
    public double Y { get; private set; }
    public double Width { get; }
    public double Height { get; }
    public Box Bounds => new(X, Y, Width, Height);

    /// <summary>Moves the platform and returns how far it moved.</summary>
    public (double Dx, double Dy) Advance(double dt)
    {
        _time += dt;

        // Smooth ping-pong: slows down near both ends.
        var phase = _time / _duration % 2;
        var t = phase < 1 ? phase : 2 - phase;
        var eased = (1 - Math.Cos(t * Math.PI)) / 2;

        var x = _startX + (_endX - _startX) * eased;
        var y = _startY + (_endY - _startY) * eased;
        var delta = (x - X, y - Y);
        X = x;
        Y = y;
        return delta;
    }
}

/// <summary>A lantern post. Touching it lights it, and lost lives continue from there.</summary>
public sealed class Checkpoint
{
    public Checkpoint(Box bounds) => Bounds = bounds;

    public Box Bounds { get; }
    public bool IsLit { get; set; }
}

public enum EnemyKind
{
    Ghost,
    Scribble,
}

public sealed class Enemy
{
    public Enemy(EnemyKind kind, double x, double y, double range)
    {
        Kind = kind;
        X = x;
        Y = y;
        MinX = x - range / 2;
        MaxX = x + range / 2;
        VelocityX = Tuning.EnemySpeed;
        (Width, Height) = kind == EnemyKind.Ghost ? (44, 64) : (59, 64);
    }

    public EnemyKind Kind { get; }
    public double X { get; set; }
    public double Y { get; }
    public double Width { get; }
    public double Height { get; }
    public double MinX { get; }
    public double MaxX { get; }
    public double VelocityX { get; set; }
    public bool Alive { get; set; } = true;
    public double AnimationTime { get; set; }

    public int Points => Kind == EnemyKind.Ghost ? 100 : 200;

    public Box Bounds => new(X, Y, Width, Height);
}

public enum EffectKind
{
    PoppedCoin,
    ScorePopup,
}

/// <summary>Short-lived visual effect such as a coin jumping out of a box.</summary>
public sealed class Effect
{
    public Effect(EffectKind kind, double x, double y, double duration, string text = null)
    {
        Kind = kind;
        X = x;
        Y = y;
        Duration = duration;
        Text = text;
    }

    public EffectKind Kind { get; }
    public double X { get; }
    public double Y { get; }
    public double Duration { get; }
    public string Text { get; }
    public double Age { get; set; }

    /// <summary>0 at spawn, 1 when the effect expires.</summary>
    public double Progress => Age / Duration;
}

public enum ParticleKind
{
    Dust,
    Spark,
}

/// <summary>A tiny moving dot: dust from the player's feet or sparks from coins.</summary>
public sealed class Particle
{
    public ParticleKind Kind;
    public double X;
    public double Y;
    public double VelocityX;
    public double VelocityY;
    public double Gravity;
    public double Size;
    public double Life;
    public double Age;

    public double Progress => Age / Life;
}
