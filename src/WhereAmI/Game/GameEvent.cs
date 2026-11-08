namespace WhereAmI.Game;

/// <summary>Something that happened during an update; used for sound and other feedback.</summary>
public enum GameEvent
{
    Jump,
    CoinCollected,
    EmptyBoxHit,
    EnemyStomped,
    CheckpointReached,
    PlayerDied,
    LevelCompleted,
    GameOver,
    Victory,
}
