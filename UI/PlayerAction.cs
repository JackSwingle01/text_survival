using text_survival.Actions;
using text_survival.Actions.Expeditions.WorkStrategies;

namespace text_survival.UI;

/// <summary>
/// What the player committed to on the map screen. The map screen folds clicks, WASD,
/// hotkeys and the action panel into exactly one of these.
/// </summary>
public abstract record PlayerAction
{
    /// <summary>Walk to the tile at these grid coordinates.</summary>
    public sealed record Travel(int X, int Y) : PlayerAction;

    /// <summary>A camp or menu action.</summary>
    public sealed record Camp(CampAction Action) : PlayerAction;

    /// <summary>Work at the current location.</summary>
    public sealed record Work(IWorkStrategy Strategy) : PlayerAction;

    /// <summary>Leave the run.</summary>
    public sealed record Quit : PlayerAction;
}

/// <summary>
/// Something the simulation needs to tell the player. The tick queues these; game logic
/// drains them and shows them, so nothing below the tick ever opens a dialog.
/// </summary>
public record Notice(string Title, string Text);

/// <summary>
/// The outcome of a work session, as the player sees it.
/// </summary>
public record WorkResultView(
    string Title,
    string Message,
    List<string> ItemsGained,
    List<string>? Narrative = null,
    List<string>? Warnings = null);

/// <summary>
/// Feedback from the last fire action, shown when the fire screen reopens.
/// <paramref name="AttemptSucceeded"/> is non-null only for fire-starting attempts,
/// which the screen presents differently from ordinary tending.
/// </summary>
public record FireFeedback(string Message, bool? AttemptSucceeded = null);
