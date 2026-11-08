using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace WhereAmI.UI;

/// <summary>Base class for full-window menu screens shown on top of the game.</summary>
public abstract class Screen : UserControl
{
    protected Screen()
    {
        Focusable = false;
    }

    /// <summary>Esc was pressed. Most screens go back.</summary>
    public virtual void OnBack()
    {
    }

    /// <summary>Called every game step while the screen is visible, for animations.</summary>
    public virtual void Update(double dt)
    {
    }

    /// <summary>The element that gets keyboard focus when the screen opens.</summary>
    protected virtual IInputElement InitialFocus => null;

    public void FocusInitial()
    {
        // Wait until the screen is laid out, otherwise focus does not stick.
        Dispatcher.BeginInvoke(DispatcherPriority.Loaded, () =>
        {
            if (InitialFocus is { } element)
                Keyboard.Focus(element);
        });
    }
}
