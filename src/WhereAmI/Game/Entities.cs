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
