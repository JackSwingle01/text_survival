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
    public FoodOverlay Food { get; } = new();
    public TransferOverlay Transfer { get; } = new();
    public DiscoveryLogOverlay DiscoveryLog { get; } = new();
    public NPCOverlay NPCs { get; } = new();

    private readonly NeedCraftingSystem _craftingSystem = new();

    public bool HasBlockingOverlay => Event.IsOpen;

    public bool AnyOverlayOpen => Inventory.IsOpen || Crafting.IsOpen || Event.IsOpen ||
                                   Fire.IsOpen || Food.IsOpen || Transfer.IsOpen ||
                                   DiscoveryLog.IsOpen || NPCs.IsOpen;

    public void CloseAll()
    {
        Inventory.IsOpen = false;
        Crafting.IsOpen = false;
        Event.IsOpen = false;
        Fire.IsOpen = false;
        Food.IsOpen = false;
        Transfer.IsOpen = false;
        DiscoveryLog.IsOpen = false;
        NPCs.IsOpen = false;
    }

    private void CloseNonBlocking()
    {
        Inventory.IsOpen = false;
        Crafting.IsOpen = false;
        Fire.IsOpen = false;
        Food.IsOpen = false;
        Transfer.IsOpen = false;
        DiscoveryLog.IsOpen = false;
        NPCs.IsOpen = false;
    }

    public void ToggleInventory()
    {
        if (HasBlockingOverlay) return;

        Inventory.IsOpen = !Inventory.IsOpen;
        if (Inventory.IsOpen)
        {
            Crafting.IsOpen = false;
        }
    }

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

    public void OpenFire(HeatSourceFeature? fire)
    {
        if (HasBlockingOverlay) return;

        CloseNonBlocking();
        Fire.Open(fire);
    }

    public void OpenFood()
    {
        if (HasBlockingOverlay) return;

        CloseNonBlocking();
        Food.Open();
    }

    public void OpenTransfer(Inventory storage, string storageName)
    {
        if (HasBlockingOverlay) return;

        CloseNonBlocking();
        Transfer.Open(storage, storageName);
    }

    public void ShowEvent(EventDto eventData)
    {
        // Close other non-blocking overlays when showing an event
        Inventory.IsOpen = false;
        Crafting.IsOpen = false;

        Event.ShowEvent(eventData);
    }

    public void ShowOutcome(EventOutcomeDto outcome)
    {
        Event.ShowOutcome(outcome);
    }

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

        if (Food.IsOpen)
        {
            Food.Render(ctx, deltaTime);
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

    public void HandleKeyboardShortcuts(bool iPressed, bool cPressed, bool lPressed, bool nPressed, bool escPressed)
    {
        if (escPressed)
        {
            // Close the topmost non-blocking overlay
            if (Transfer.IsOpen)
                Transfer.IsOpen = false;
            else if (Food.IsOpen)
                Food.IsOpen = false;
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

public class OverlayResults
{
    public string? EventChoice { get; set; }
    public string? CraftedItem { get; set; }
    public FireOverlayResult? FireResult { get; set; }
    public TransferResult? TransferResult { get; set; }

    public bool HasResults => EventChoice != null || CraftedItem != null ||
                              FireResult != null || TransferResult != null;
}
