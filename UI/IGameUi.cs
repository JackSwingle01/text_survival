using text_survival.Actions.Variants;
using text_survival.Crafting;
using text_survival.Environments.Features;
using text_survival.Items;

namespace text_survival.UI;

/// <summary>
/// The complete surface through which game logic reaches the player. Game logic stays
/// ordinary sequential code and awaits these; nothing below the simulation tick may use
/// them. Implementations own the screen - game logic never draws.
/// </summary>
public interface IGameUi
{
    // ── The primitive. Everything timed is built on it. ──

    /// <summary>Completes at the start of the next frame with that frame's clamped delta time.</summary>
    Task<float> NextFrame();

    /// <summary>Completes once at least <paramref name="seconds"/> of frames have elapsed.</summary>
    Task Wait(float seconds);

    // ── Prompts ──

    Task<T> Select<T>(string prompt, IReadOnlyList<T> choices, Func<T, string> display,
                      Func<T, bool>? isDisabled = null) where T : notnull;

    Task<bool> Confirm(string prompt);

    /// <summary>A message with custom buttons. Returns the id of the button pressed.</summary>
    Task<string> Choose(string message, IReadOnlyList<(string id, string label)> buttons);

    /// <summary>Returns -1 when cancelled (only possible if <paramref name="allowCancel"/>).</summary>
    Task<int> ReadInt(string prompt, int min, int max, bool allowCancel = false);

    Task ShowMessage(string title, string message);

    Task ShowWorkResult(WorkResultView view);

    /// <summary>Shows an event's choices. Returns the chosen choice's id.</summary>
    Task<string> ShowEventChoices(EventDto evt);

    Task ShowEventOutcome(EventDto outcome);

    /// <summary>
    /// Forage focus and duration. focus=null with minutes=0 means cancelled;
    /// focus=null with minutes=-1 means "keep walking" to reroll the clues.
    /// </summary>
    Task<(ForageFocus? focus, int minutes)> SelectForageOptions(ForageFeature feature, IReadOnlyList<ForageClue> clues);

    /// <summary>Butchering mode id, or null if cancelled.</summary>
    Task<string?> SelectButcherMode(CarcassFeature carcass, IReadOnlyList<string> warnings, bool hasCuttingTool);

    // ── Screens ──

    Task ShowInventory();
    Task ShowDiscoveryLog();
    Task ShowNPCs();
    Task ShowTransfer(Inventory storage, string storageName);

    /// <summary>The recipe the player committed to, or null if they closed the screen.</summary>
    Task<CraftOption?> ShowCrafting();

    /// <summary>The fire action the player committed to, or null if they closed the screen.</summary>
    Task<FireOverlayResult?> ShowFire(HeatSourceFeature? fire, FireFeedback? feedback);

    /// <summary>A cooking action that costs time, or null if the player closed the screen.</summary>
    Task<PendingFoodAction?> ShowFood();

    // ── Base screens ──

    /// <summary>The map screen: action panel, tile popup, WASD, hotkeys.</summary>
    Task<PlayerAction> WaitForPlayerAction();

    /// <summary>The combat screen. Null ends the fight.</summary>
    Task<CombatInput?> WaitForCombatAction();

    // ── Progress ──

    /// <summary>
    /// Open a progress display. The caller drives time with <see cref="NextFrame"/> and
    /// updates the view; disposing it closes the display.
    /// </summary>
    ProgressView BeginProgress(ProgressKind kind, string status);
}
