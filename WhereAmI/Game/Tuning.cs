namespace WhereAmI.Game;

/// <summary>Gameplay constants. Distances are in world units, times in seconds.</summary>
public static class Tuning
{
    public const double PlayerWidth = 48;
    public const double PlayerHeight = 60;

    // Running: speed builds up and drops off quickly instead of switching instantly.
    public const double WalkSpeed = 420;
    public const double GroundAcceleration = 4200;
    public const double GroundDeceleration = 5200;
    public const double AirAcceleration = 2800;
    public const double AirDeceleration = 1600;

    // Jumping. A full jump reaches about v² / 2g ≈ 400 units, like the original game.
    public const double Gravity = 2600;
    public const double FallGravityMultiplier = 1.2;   // falling is a bit faster than rising: jumps feel less floaty
    public const double JumpVelocity = 1450;
    public const double JumpCutMultiplier = 0.45;      // releasing jump early gives a lower jump
    public const double MaxFallSpeed = 1400;
    public const double CoyoteTime = 0.10;             // a jump still works this long after walking off a ledge
    public const double JumpBufferTime = 0.12;         // a jump pressed this long before landing still happens

    public const double StompBounce = 650;
    public const double StompBounceHeld = 1250;        // holding jump while stomping bounces higher
    public const double StompTolerance = 20;           // how far below an enemy's top a stomp still counts

    public const double DeathPopVelocity = 900;
    public const double RespawnDelay = 1.2;
    public const double RespawnInvulnerability = 1.5;  // enemies cannot hurt the player right after respawning

    // Flutter: pressing jump again in the air flaps the wings and slows the fall for a while.
    public const double FlutterGravityMultiplier = 0.3;
    public const double FlutterFallSpeed = 170;
    public const double FlutterDuration = 1.4;

    // Eggs: thrown in an arc, bounce once on the ground, defeat any enemy they hit.
    public const int MaxEggs = 3;
    public const double EggRecharge = 2.0;             // seconds to get one egg back
    public const double EggCooldown = 0.3;
    public const double EggSpeedX = 620;
    public const double EggSpeedY = 420;
    public const double EggGravity = 1700;
    public const double EggWidth = 18;
    public const double EggHeight = 22;
    public const double EggLifetime = 2.5;

    public const double EnemySpeed = 70;
    public const double ScribbleChargeSpeed = 170;
    public const double ScribbleSightRange = 340;
    public const double EmberSpeed = 230;
    public const double EmberSize = 22;
    public const double MothShootInterval = 3.2;
    public const double MothSightRange = 650;
    public const double OwlRestTime = 2.6;
    public const double OwlFlapTime = 1.8;
    public const double OwlWindReach = 460;
    public const double OwlWindSpeed = 300;            // how fast the wind pushes the player
    public const double BurrSpeed = 60;
    public const double ToadJumpInterval = 1.8;
    public const double ToadSightRange = 520;
    public const double ToadJumpSpeedX = 210;
    public const double ToadJumpSpeedY = 760;

    public const double CheckpointWidth = 40;
    public const double CheckpointHeight = 96;

    public const double CoinSize = 32;
    public const double RewardBoxSize = 64;

    public const int StartingLives = 3;
}
