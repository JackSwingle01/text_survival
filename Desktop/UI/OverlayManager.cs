using text_survival.Actions;
using text_survival.Crafting;
using text_survival.Desktop.Dto;
using text_survival.Environments.Features;
using text_survival.Items;

namespace text_survival.Desktop.UI;

/// <summary>
/// Manages all UI overlays, ensuring only appropriate overlays are shown at a time.
/// </summary>
public class OverlayManager
{
    public InventoryOverlay Inventory { get; } = new();
    public CraftingOverlay Crafting { get; } = new();
    public GameEventOverlay Event { get; } = new();
    public FireOverlay Fire { get; } = new();
    public EatingOverlay Eating { get; } = new();
    public CookingOverlay Cooking { get; } = new();
    public TransferOverlay Transfer { get; } = new();
    public DiscoveryLogOverlay DiscoveryLog { get; } = new();
    public NPCOverlay NPCs { get; } = new();

    private readonly NeedCraftingSystem _craftingSystem = new();

    /// <summary>
    /// Check if any blocking overlay is open (requires user interaction before game continues).
    /// </summary>
    public bool HasBlockingOverlay => Event.IsOpen;

    /// <summary>
    /// Check if any overlay is open.
    /// </summary>
    public bool AnyOverlayOpen => Inventory.IsOpen || Crafting.IsOpen || Event.IsOpen ||
                                   Fire.IsOpen || Eating.IsOpen || Cooking.IsOpen || Transfer.IsOpen ||
                                   DiscoveryLog.IsOpen || NPCs.IsOpen;

    /// <summary>
    /// Close all overlays.
    /// </summary>
    public void CloseAll()
    {
        Inventory.IsOpen = false;
        Crafting.IsOpen = false;
        Event.IsOpen = false;
        Fire.IsOpen = false;
        Eating.IsOpen = false;
        Cooking.IsOpen = false;
        Transfer.IsOpen = false;
        DiscoveryLog.IsOpen = false;
        NPCs.IsOpen = false;
    }

    /// <summary>
    /// Close all non-blocking overlays.
    /// </summary>
    private void CloseNonBlocking()
    {
        Inventory.IsOpen = false;
        Crafting.IsOpen = false;
        Fire.IsOpen = false;
        Eating.IsOpen = false;
        Cooking.IsOpen = false;
        Transfer.IsOpen = false;
        DiscoveryLog.IsOpen = false;
        NPCs.IsOpen = false;
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
            CloseNonBlocking();
            Crafting.IsOpen = true;
        }
    }

    /// <summary>
    /// Toggle the discovery log overlay.
    /// </summary>
    public void ToggleDiscoveryLog()
    {
        if (HasBlockingOverlay) return;

        DiscoveryLog.IsOpen = !DiscoveryLog.IsOpen;
        if (DiscoveryLog.IsOpen)
        {
            CloseNonBlocking();
            DiscoveryLog.IsOpen = true;
        }
    }

    /// <summary>
    /// Toggle the NPC overlay.
    /// </summary>
    public void ToggleNPCs()
    {
        if (HasBlockingOverlay) return;

        NPCs.IsOpen = !NPCs.IsOpen;
        if (NPCs.IsOpen)
        {
            CloseNonBlocking();
            NPCs.IsOpen = true;
        }
    }

    /// <summary>
    /// Open the fire overlay.
    /// </summary>
    public void OpenFire(HeatSourceFeature? fire)
    {
        if (HasBlockingOverlay) return;

        CloseNonBlocking();
        Fire.Open(fire);
    }

    /// <summary>
    /// Open the eating overlay.
    /// </summary>
    public void OpenEating()
    {
        if (HasBlockingOverlay) return;

        CloseNonBlocking();
        Eating.Open();
    }

    /// <summary>
    /// Open the cooking overlay.
    /// </summary>
    public void OpenCooking()
    {
        if (HasBlockingOverlay) return;

        CloseNonBlocking();
        Cooking.Open();
    }

    /// <summary>
    /// Open the transfer overlay.
    /// </summary>
    public void OpenTransfer(Inventory storage, string storageName)
    {
        if (HasBlockingOverlay) return;

        CloseNonBlocking();
        Transfer.Open(storage, storageName);
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

        if (Fire.IsOpen)
        {
            results.FireResult = Fire.Render(ctx, deltaTime);
        }

        if (Eating.IsOpen)
        {
            results.ConsumedItemId = Eating.Render(ctx, deltaTime);
        }

        if (Cooking.IsOpen)
        {
            results.CookingResult = Cooking.Render(ctx, deltaTime);
        }

        if (Transfer.IsOpen)
        {
            results.TransferResult = Transfer.Render(ctx, deltaTime);
        }

        if (DiscoveryLog.IsOpen)
        {
            DiscoveryLog.Render(deltaTime);
        }

        if (NPCs.IsOpen)
        {
            NPCs.Render(ctx, deltaTime);
        }

        return results;
    }

    /// <summary>
    /// Handle keyboard shortcuts for overlays.
    /// </summary>
    public void HandleKeyboardShortcuts(bool iPressed, bool cPressed, bool lPressed, bool nPressed, bool escPressed)
    {
        if (escPressed)
        {
            // Close the topmost non-blocking overlay
            if (Transfer.IsOpen)
                Transfer.IsOpen = false;
            else if (Cooking.IsOpen)
                Cooking.IsOpen = false;
            else if (Eating.IsOpen)
                Eating.IsOpen = false;
            else if (Fire.IsOpen)
                Fire.IsOpen = false;
            else if (NPCs.IsOpen)
                NPCs.IsOpen = false;
            else if (DiscoveryLog.IsOpen)
                DiscoveryLog.IsOpen = false;
            else if (Crafting.IsOpen)
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

        if (lPressed)
        {
            ToggleDiscoveryLog();
        }

        if (nPressed)
        {
            ToggleNPCs();
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
    /// Fire overlay action result, if any.
    /// </summary>
    public FireOverlayResult? FireResult { get; set; }

    /// <summary>
    /// ID of consumed item, if any.
    /// </summary>
    public string? ConsumedItemId { get; set; }

    /// <summary>
    /// Cooking action result, if any.
    /// </summary>
    public CookingOverlayResult? CookingResult { get; set; }

    /// <summary>
    /// Transfer action result, if any.
    /// </summary>
    public TransferResult? TransferResult { get; set; }

    /// <summary>
    /// True if any result needs processing.
    /// </summary>
    public bool HasResults => EventChoice != null || CraftedItem != null ||
                              FireResult != null || ConsumedItemId != null ||
                              CookingResult != null || TransferResult != null;
}
