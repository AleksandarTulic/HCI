namespace WhereAmI.Game;

/// <summary>Something that happened during an update; used for sound and other feedback.</summary>
public enum GameEvent
{
    Jump,
    CoinCollected,
    EmptyBoxHit,
    EnemyStomped,
    EnemyHitByEgg,
    EggThrown,
    EggBroke,
    EmberShot,
    OwlGust,
    ToadLanded,
    Flutter,
    CheckpointReached,
    PlayerDied,
    LevelCompleted,
    GameOver,
    Victory,
}
