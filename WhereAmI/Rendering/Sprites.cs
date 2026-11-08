using System.Windows.Media.Imaging;
using WhereAmI.Game;

namespace WhereAmI.Rendering;

/// <summary>All sprites and animations used by the game, loaded once at startup.</summary>
public sealed class Sprites
{
    // The chicken, rendered in Blender (tools/blender/character.py). Frames are 128x128.
    public SpriteAnimation PlayerIdle { get; } = SpriteAnimation.FromFolder("sprites/player/idle", 10, PlaybackMode.Loop);
    public SpriteAnimation PlayerWalk { get; } = SpriteAnimation.FromFolder("sprites/player/walk", 22, PlaybackMode.Loop);
    public SpriteAnimation PlayerJump { get; } = SpriteAnimation.FromFolder("sprites/player/jump", 1, PlaybackMode.Once);
    public SpriteAnimation PlayerFlutter { get; } = SpriteAnimation.FromFolder("sprites/player/flutter", 20, PlaybackMode.Loop);
    public SpriteAnimation PlayerThrow { get; } = SpriteAnimation.FromFolder("sprites/player/throw", 20, PlaybackMode.Once);

    public SpriteAnimation CoinSpin { get; } = SpriteAnimation.FromFolder("sprites/coin/spin", 25, PlaybackMode.Loop);
    public BitmapSource CoinPopped { get; } = SpriteCache.Get("sprites/coin/popped.png");

    // The two original hand-drawn enemies.
    public SpriteAnimation Ghost { get; } = SpriteAnimation.FromFolder("sprites/enemies/ghost", 20, PlaybackMode.PingPong);
    public SpriteAnimation Scribble { get; } = SpriteAnimation.FromFolder("sprites/enemies/scribble", 12, PlaybackMode.PingPong);

    // Enemies rendered in Blender (tools/blender/enemies.py).
    public SpriteAnimation Moth { get; } = SpriteAnimation.FromFolder("sprites/enemies/moth/fly", 16, PlaybackMode.Loop);
    public SpriteAnimation OwlIdle { get; } = SpriteAnimation.FromFolder("sprites/enemies/owl/idle", 6, PlaybackMode.Loop);
    public SpriteAnimation OwlFlap { get; } = SpriteAnimation.FromFolder("sprites/enemies/owl/flap", 16, PlaybackMode.Loop);
    public SpriteAnimation BurrWalk { get; } = SpriteAnimation.FromFolder("sprites/enemies/burr/walk", 12, PlaybackMode.Loop);
    public SpriteAnimation ToadIdle { get; } = SpriteAnimation.FromFolder("sprites/enemies/toad/idle", 6, PlaybackMode.Loop);
    public SpriteAnimation ToadJump { get; } = SpriteAnimation.FromFolder("sprites/enemies/toad/jump", 1, PlaybackMode.Once);

    public BitmapSource Egg { get; } = SpriteCache.Get("sprites/projectiles/egg.png");
    public SpriteAnimation EggShells { get; } = SpriteAnimation.FromFolder("sprites/projectiles/eggshell", 1, PlaybackMode.Once);
    public SpriteAnimation Ember { get; } = SpriteAnimation.FromFolder("sprites/projectiles/ember", 14, PlaybackMode.Loop);

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
}
