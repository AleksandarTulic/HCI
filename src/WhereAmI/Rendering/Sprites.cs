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

    public BitmapSource IconLeft { get; } = SpriteCache.Get("sprites/ui/left.png");
    public BitmapSource IconRight { get; } = SpriteCache.Get("sprites/ui/right.png");
    public BitmapSource IconJump { get; } = SpriteCache.Get("sprites/ui/jump.png");
    public BitmapSource SoundOn { get; } = SpriteCache.Get("sprites/ui/sound-on.png");
    public BitmapSource SoundOff { get; } = SpriteCache.Get("sprites/ui/sound-off.png");

    public SpriteAnimation ForPlayer(PlayerAnimation animation) => animation switch
    {
        PlayerAnimation.Walk => PlayerWalk,
        PlayerAnimation.Jump => PlayerJump,
        _ => PlayerIdle,
    };

    public SpriteAnimation ForEnemy(EnemyKind kind) => kind == EnemyKind.Ghost ? Ghost : Scribble;
}
