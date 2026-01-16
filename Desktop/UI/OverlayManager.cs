using text_survival.Actions;
using text_survival.Crafting;
using text_survival.Desktop.Dto;

namespace text_survival.Desktop.UI;

/// <summary>
/// Manages all UI overlays, ensuring only appropriate overlays are shown at a time.
/// </summary>
public class OverlayManager
{
    public InventoryOverlay Inventory { get; } = new();
    public CraftingOverlay Crafting { get; } = new();
    public GameEventOverlay Event { get; } = new();

    private readonly NeedCraftingSystem _craftingSystem = new();

    /// <summary>
    /// Check if any blocking overlay is open (requires user interaction before game continues).
    /// </summary>
    public bool HasBlockingOverlay => Event.IsOpen;

    /// <summary>
    /// Check if any overlay is open.
    /// </summary>
    public bool AnyOverlayOpen => Inventory.IsOpen || Crafting.IsOpen || Event.IsOpen;

    /// <summary>
    /// Close all overlays.
    /// </summary>
    public void CloseAll()
    {
        Inventory.IsOpen = false;
        Crafting.IsOpen = false;
        Event.IsOpen = false;
    }

    /// <summary>
    /// Toggle the inventory overlay.
    /// </summary>
    public void ToggleInventory()
    {
        if (HasBlockingOverlay) return;

        Inventory.IsOpen = !Inventory.IsOpen;
        if (Inventory.IsOpen)
        {
            Crafting.IsOpen = false;
        }
    }

    /// <summary>
    /// Toggle the crafting overlay.
    /// </summary>
    public void ToggleCrafting()
    {
        if (HasBlockingOverlay) return;

        Crafting.IsOpen = !Crafting.IsOpen;
        if (Crafting.IsOpen)
        {
            Inventory.IsOpen = false;
        }
    }

    /// <summary>
    /// Show an event overlay.
    /// </summary>
    public void ShowEvent(EventDto eventData)
    {
        // Close other non-blocking overlays when showing an event
        Inventory.IsOpen = false;
        Crafting.IsOpen = false;

        Event.ShowEvent(eventData);
    }

    /// <summary>
    /// Show an event outcome.
    /// </summary>
    public void ShowOutcome(EventOutcomeDto outcome)
    {
        Event.ShowOutcome(outcome);
    }

    /// <summary>
    /// Render all active overlays.
    /// Returns action results that need to be processed.
    /// </summary>
    public OverlayResults Render(GameContext ctx, float deltaTime)
    {
        var results = new OverlayResults();

        // Render blocking overlays first (events)
        if (Event.IsOpen)
        {
            results.EventChoice = Event.Render(deltaTime);
        }

        // Render non-blocking overlays
        if (Inventory.IsOpen)
        {
            Inventory.Render(ctx, deltaTime);
        }

        if (Crafting.IsOpen)
        {
            results.CraftedItem = Crafting.Render(ctx, _craftingSystem, deltaTime);
        }

        return results;
    }

    /// <summary>
    /// Handle keyboard shortcuts for overlays.
    /// </summary>
    public void HandleKeyboardShortcuts(bool iPressed, bool cPressed, bool escPressed)
    {
        if (escPressed)
        {
            // Close the topmost non-blocking overlay
            if (Crafting.IsOpen)
                Crafting.IsOpen = false;
            else if (Inventory.IsOpen)
                Inventory.IsOpen = false;
            return;
        }

        if (iPressed)
        {
            ToggleInventory();
        }

        if (cPressed)
        {
            ToggleCrafting();
        }
    }
}

/// <summary>
/// Results from overlay rendering that need to be processed by the game loop.
/// </summary>
public class OverlayResults
{
    /// <summary>
    /// The ID of the event choice selected, if any.
    /// </summary>
    public string? EventChoice { get; set; }

    /// <summary>
    /// The name of an item that was crafted, if any.
    /// </summary>
    public string? CraftedItem { get; set; }

    /// <summary>
    /// True if any result needs processing.
    /// </summary>
    public bool HasResults => EventChoice != null || CraftedItem != null;
}
