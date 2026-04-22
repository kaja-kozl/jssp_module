using Terminal.Gui;

namespace JSSP.UI.Views;

/// <summary>
/// Button that executes an action delegate when activated.
/// </summary>
public class ActionButton : Button
{
    /// <summary>Gets or sets the action invoked when this button is clicked.</summary>
    public Action? OnClickAction { get; set; }

    /// <summary>Initialises a new instance of <see cref="ActionButton"/>.</summary>
    public ActionButton() : base()
    {
        Accepting += (sender, e) => OnClickAction?.Invoke();
    }
}
