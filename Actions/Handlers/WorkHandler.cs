using text_survival.Actors;
using text_survival.Environments;
using text_survival.Environments.Features;
using text_survival.Items;

namespace text_survival.Actions.Handlers;

/// <summary>
/// Pure game logic for work activities (foraging, harvesting).
/// UI code calls these methods; NPCs can call them directly.
/// </summary>
public static class WorkHandler
{
    // ============================================
    // Foraging
    // ============================================

    /// <summary>
    /// NPC foraging - searches location for resources.
    /// Returns inventory of found items (caller adds to their inventory).
    /// </summary>
    public static Inventory Forage(
        Actor actor,
        Inventory inventory,
        Location location,
        int minutesToSpend,
        bool hasLight = true)
    {
        var feature = location.GetFeature<ForageFeature>();
        if (feature == null)
            return new Inventory();

        double hours = minutesToSpend / 60.0;
        var found = feature.Forage(hours);

        // Apply modifiers
        if (!hasLight && location.IsDark)
            found.ApplyMultiplier(0.5);  // Darkness penalty

        // Perception impairment
        var capacities = actor.GetCapacities();
        if (capacities.Sight < 0.5 || capacities.Hearing < 0.5)
            found.ApplyMultiplier(0.85);

        // Tool bonuses (+10% each when tool works)
        var axe = inventory.GetTool(ToolType.Axe);
        if (axe?.Works == true)
            found.ApplyMultiplier(1.1);

        var shovel = inventory.GetTool(ToolType.Shovel);
        if (shovel?.Works == true)
            found.ApplyMultiplier(1.1);

        return found;
    }

    /// <summary>
    /// Check if location has forageable resources.
    /// </summary>
    public static bool CanForage(Location location)
    {
        var feature = location.GetFeature<ForageFeature>();
        return feature != null && feature.CanForage();
    }

    // ============================================
    // Harvesting
    // ============================================

    /// <summary>
    /// NPC harvesting - works a specific harvestable feature.
    /// Returns inventory of harvested items.
    /// </summary>
    public static Inventory Harvest(
        HarvestableFeature feature,
        int minutesToSpend)
    {
        if (!feature.CanBeHarvested())
            return new Inventory();

        return feature.Harvest(minutesToSpend);
    }

    /// <summary>
    /// Get first available harvestable at location.
    /// </summary>
    public static HarvestableFeature? GetAvailableHarvestable(Location location)
    {
        return location.Features
            .OfType<HarvestableFeature>()
            .FirstOrDefault(h => h.CanBeHarvested());
    }

    /// <summary>
    /// NPC harvesting - auto-selects available harvestable at location.
    /// </summary>
    public static Inventory Harvest(Location location, int minutesToSpend)
    {
        var feature = GetAvailableHarvestable(location);
        if (feature == null)
            return new Inventory();

        return feature.Harvest(minutesToSpend);
    }

    /// <summary>
    /// Check if location has harvestable resources.
    /// </summary>
    public static bool CanHarvest(Location location)
    {
        return GetAvailableHarvestable(location) != null;
    }
}
