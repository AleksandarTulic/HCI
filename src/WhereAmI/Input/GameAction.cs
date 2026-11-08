namespace WhereAmI.Input;

public enum GameAction
{
    MoveLeft,
    MoveRight,
    Jump,
    Confirm,
    Quit,
    ToggleMusic,
    ToggleDebug,
}

/// <summary>Read-only view of player input used by the game logic.</summary>
public interface IInput
{
    bool IsDown(GameAction action);

    /// <summary>Returns true once per key press, then forgets it.</summary>
    bool ConsumePressed(GameAction action);
}
