using System.Windows.Media.Imaging;
using WhereAmI.Game;

namespace WhereAmI.Rendering;

/// <summary>All sprites and animations used by the game, loaded once at startup.</summary>
public sealed class Sprites
{
    public SpriteAnimation PlayerIdle { get; } = SpriteAnimation.FromFolder("sprites/player/idle", 30, PlaybackMode.Loop);
    public SpriteAnimation PlayerWalk { get; } = SpriteAnimation.FromFolder("sprites/player/walk", 40, PlaybackMode.Loop);
    public SpriteAnimation PlayerJump { get; } = SpriteAnimation.FromFolder("sprites/player/jump", 16, PlaybackMode.Once);

    public SpriteAnimation CoinSpin { get; } = SpriteAnimation.FromFolder("sprites/coin/spin", 25, PlaybackMode.Loop);
    public BitmapSource CoinPopped { get; } = SpriteCache.Get("sprites/coin/popped.png");

    public SpriteAnimation Ghost { get; } = SpriteAnimation.FromFolder("sprites/enemies/ghost", 20, PlaybackMode.PingPong);
    public SpriteAnimation Scribble { get; } = SpriteAnimation.FromFolder("sprites/enemies/scribble", 12, PlaybackMode.PingPong);

    public BitmapSource RewardBoxFull { get; } = SpriteCache.Get("sprites/reward-box/full.png");
    public BitmapSource RewardBoxEmpty { get; } = SpriteCache.Get("sprites/reward-box/empty.png");

    public BitmapSource GoalGate { get; } = SpriteCache.Get("sprites/goal-gate.png");
    public BitmapSource CheckpointLit { get; } = SpriteCache.Get("sprites/checkpoint/lit.png");
    public BitmapSource CheckpointUnlit { get; } = SpriteCache.Get("sprites/checkpoint/unlit.png");

    /// <summary>Background layers from far to near, with how fast each one follows the camera.</summary>
    public (BitmapSource Image, double Speed)[] Parallax { get; } =
    {
        (SpriteCache.Get("backgrounds/parallax/far.png"), 0.12),
        (SpriteCache.Get("backgrounds/parallax/mid.png"), 0.28),
        (SpriteCache.Get("backgrounds/parallax/near.png"), 0.5),
    };

    public SpriteAnimation ForPlayer(PlayerAnimation animation) => animation switch
    {
        PlayerAnimation.Walk => PlayerWalk,
        PlayerAnimation.Jump => PlayerJump,
        _ => PlayerIdle,
    };

    public SpriteAnimation ForEnemy(EnemyKind kind) => kind == EnemyKind.Ghost ? Ghost : Scribble;
}
