using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;

namespace WhereAmI.Input;

/// <summary>Tracks keyboard state from window key events and maps keys to actions.</summary>
public sealed class KeyboardInput : IInput
{
    private static readonly Dictionary<GameAction, Key[]> Bindings = new()
    {
        [GameAction.MoveLeft] = new[] { Key.Left, Key.A },
        [GameAction.MoveRight] = new[] { Key.Right, Key.D },
        [GameAction.Jump] = new[] { Key.Up, Key.W, Key.Space },
        [GameAction.Confirm] = new[] { Key.Enter, Key.Space },
        [GameAction.Quit] = new[] { Key.Escape },
        [GameAction.ToggleMusic] = new[] { Key.M },
        [GameAction.ToggleDebug] = new[] { Key.F3 },
    };

    private readonly HashSet<Key> _down = new();
    private readonly HashSet<GameAction> _pressed = new();

    public void OnKeyDown(Key key, bool isRepeat)
    {
        _down.Add(key);
        if (isRepeat)
            return;

        foreach (var (action, keys) in Bindings)
            if (keys.Contains(key))
                _pressed.Add(action);
    }

    public void OnKeyUp(Key key) => _down.Remove(key);

    /// <summary>Forget everything, e.g. when the window loses focus.</summary>
    public void Clear()
    {
        _down.Clear();
        _pressed.Clear();
    }

    /// <summary>Call after each game update so presses nobody consumed do not leak into later states.</summary>
    public void EndUpdate() => _pressed.Clear();

    public bool IsDown(GameAction action) => Bindings[action].Any(_down.Contains);

    public bool ConsumePressed(GameAction action) => _pressed.Remove(action);
}
