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
        var (found, _) = feature.Forage(hours);  // NPCs don't use luck tier

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
    /// The harvestable a worker can use here. <paramref name="wanted"/> narrows the choice to
    /// features that actually yield one of those resources; passing null takes whatever is
    /// available, which is what a player picking off a menu means.
    /// </summary>
    /// <remarks>
    /// The filter is the whole point. This used to return the first harvestable on the tile
    /// regardless of what was being looked for, and every caller then checked whether *that
    /// one* provided what it wanted - so a berry bush sitting ahead of a water pool made the
    /// water invisible to anything searching for it, and an NPC that walked to a marsh for
    /// water harvested berries once it arrived. Water was hit hardest because it is the only
    /// resource whose other source needs a lit fire, but food and medicine were hidden the
    /// same way.
    /// </remarks>
    public static HarvestableFeature? GetAvailableHarvestable(
        Location location, IReadOnlyCollection<Resource>? wanted = null)
    {
        return location.Features
            .OfType<HarvestableFeature>()
            .FirstOrDefault(h => h.CanBeHarvested()
                && (wanted == null || h.ProvidedResources().Any(wanted.Contains)));
    }

    /// <summary>
    /// NPC harvesting. <paramref name="wanted"/> must match whatever the decision to come here
    /// was made on, or the NPC harvests something other than what it came for.
    /// </summary>
    public static Inventory Harvest(
        Location location, int minutesToSpend, IReadOnlyCollection<Resource>? wanted = null)
    {
        var feature = GetAvailableHarvestable(location, wanted);
        if (feature == null)
            return new Inventory();

        return feature.Harvest(minutesToSpend);
    }

}
