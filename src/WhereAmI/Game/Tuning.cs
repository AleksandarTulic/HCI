namespace WhereAmI.Game;

/// <summary>Gameplay constants. Distances are in world units, times in seconds.</summary>
public static class Tuning
{
    public const double PlayerWidth = 48;
    public const double PlayerHeight = 60;

    public const double WalkSpeed = 420;
    public const double Gravity = 2600;
    public const double JumpVelocity = 1450;   // apex ≈ v² / 2g ≈ 400 units, like the original game
    public const double MaxFallSpeed = 1400;

    public const double StompBounce = 750;
    public const double StompTolerance = 20;   // how far below an enemy's top a stomp still counts

    public const double EnemySpeed = 70;

    public const double CoinSize = 32;
    public const double RewardBoxSize = 64;

    public const int StartingLives = 3;
    public const double RespawnDelay = 0.6;
}
