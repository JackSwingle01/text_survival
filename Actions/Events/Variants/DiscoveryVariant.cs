using text_survival.Items;

namespace text_survival.Actions.Variants;

/// <summary>
/// Bundles discovery description with matched reward pool.
/// Descriptions are generic to match generic pools - variety comes from
/// how you find things, not what specific item you find.
/// </summary>
public record DiscoveryVariant(
    string Description,     // Generic: "Something catches the light beneath the snow."
    RewardPool Pool,        // Existing pool system determines actual rewards
    int TimeMinutes = 0     // Optional time cost for the find
);

/// <summary>
/// Variant pools for discovery events.
/// Each pool has generic descriptions that work with any reward from that pool.
/// </summary>
public static class DiscoveryVariants
{
    /// <summary>
    /// Generic supply finds - basic mixed resources.
    /// </summary>
    public static readonly DiscoveryVariant[] SupplyFinds =
    [
        new("Something catches the light beneath the snow.", RewardPool.BasicSupplies),
        new("A shape that doesn't belong to the landscape.", RewardPool.BasicSupplies),
        new("Your foot kicks something solid.", RewardPool.BasicSupplies),
        new("Useful materials, half-buried and forgotten.", RewardPool.BasicSupplies),
        new("You almost walk past it. Something useful, barely visible.", RewardPool.BasicSupplies),
    ];

    /// <summary>
    /// Tinder and fire-starting material finds.
    /// </summary>
    public static readonly DiscoveryVariant[] TinderFinds =
    [
        new("Dry material in a sheltered hollow. Perfect for catching sparks.", RewardPool.TinderBundle),
        new("Papery material, protected from the elements.", RewardPool.TinderBundle),
        new("Remnants of an old fire. Someone sheltered here once.", RewardPool.TinderBundle),
        new("A pocket of dry tinder, ready for use.", RewardPool.TinderBundle),
    ];

    /// <summary>
    /// Crafting material discoveries.
    /// </summary>
    public static readonly DiscoveryVariant[] MaterialFinds =
    [
        new("Raw materials, weathered but usable.", RewardPool.CraftingMaterials),
        new("Something useful left behind by time or chance.", RewardPool.CraftingMaterials),
        new("Nature provides, if you know where to look.", RewardPool.CraftingMaterials),
        new("Materials that could serve your purposes.", RewardPool.CraftingMaterials),
    ];

    /// <summary>
    /// Bone discoveries - old kill sites, remains.
    /// </summary>
    public static readonly DiscoveryVariant[] BoneFinds =
    [
        new("Bone, weathered by scavengers and time.", RewardPool.BoneHarvest),
        new("Old kill site. The bones remain.", RewardPool.BoneHarvest),
        new("Remains scattered across the ground.", RewardPool.BoneHarvest),
        new("Something died here. The useful parts are still good.", RewardPool.BoneHarvest),
    ];

    /// <summary>
    /// Camp remnant discoveries - abandoned sites.
    /// </summary>
    public static readonly DiscoveryVariant[] CampFinds =
    [
        new("Signs of old habitation. Someone camped here.", RewardPool.AbandonedCamp),
        new("An abandoned camp. They left in a hurry.", RewardPool.AbandonedCamp),
        new("Scattered remains of a temporary shelter.", RewardPool.AbandonedCamp),
    ];

    /// <summary>
    /// Hidden cache discoveries - deliberately stashed supplies.
    /// </summary>
    public static readonly DiscoveryVariant[] CacheFinds =
    [
        new("A cache, deliberately hidden. Someone meant to return.", RewardPool.HiddenCache),
        new("Supplies tucked away in a crevice. Fortune favors you today.", RewardPool.HiddenCache),
        new("A stash, carefully concealed. Its owner never came back.", RewardPool.HiddenCache),
    ];

    /// <summary>
    /// Small game finds - dead or trapped animals.
    /// </summary>
    public static readonly DiscoveryVariant[] SmallGameFinds =
    [
        new("A small carcass, recently dead. Still good.", RewardPool.SmallGame),
        new("Something caught in a natural trap. Fresh enough.", RewardPool.SmallGame),
        new("Predator leftovers. They didn't finish.", RewardPool.SmallGame),
    ];

    /// <summary>
    /// Hide and leather scrap finds.
    /// </summary>
    public static readonly DiscoveryVariant[] HideFinds =
    [
        new("A piece of hide, weathered but workable.", RewardPool.HideScrap),
        new("Scraps of skin, left behind by hunters.", RewardPool.HideScrap),
        new("Hide remnants. Someone's old work.", RewardPool.HideScrap),
    ];
}

/// <summary>
/// Selects appropriate discovery variants based on context.
/// </summary>
public static class DiscoverySelector
{
    /// <summary>
    /// Select a general discovery variant weighted by location context.
    /// </summary>
    public static DiscoveryVariant SelectGeneralDiscovery(GameContext ctx)
    {
        var pool = new List<(DiscoveryVariant variant, double weight)>();

        // Always include basic supplies
        pool.AddRange(DiscoveryVariants.SupplyFinds.Select(v => (v, 1.0)));
        pool.AddRange(DiscoveryVariants.MaterialFinds.Select(v => (v, 0.8)));
        pool.AddRange(DiscoveryVariants.TinderFinds.Select(v => (v, 0.6)));

        // Bone finds more common in animal territory
        var territory = ctx.CurrentLocation.GetFeature<Environments.Features.AnimalTerritoryFeature>();
        if (territory != null)
            pool.AddRange(DiscoveryVariants.BoneFinds.Select(v => (v, 1.2)));
        else
            pool.AddRange(DiscoveryVariants.BoneFinds.Select(v => (v, 0.4)));

        return SelectWeighted(pool);
    }

    /// <summary>
    /// Select a supply-focused discovery variant.
    /// </summary>
    public static DiscoveryVariant SelectSupplyDiscovery(GameContext ctx)
    {
        return DiscoveryVariants.SupplyFinds[Random.Shared.Next(DiscoveryVariants.SupplyFinds.Length)];
    }

    /// <summary>
    /// Select a tinder discovery variant.
    /// </summary>
    public static DiscoveryVariant SelectTinderDiscovery(GameContext ctx)
    {
        return DiscoveryVariants.TinderFinds[Random.Shared.Next(DiscoveryVariants.TinderFinds.Length)];
    }

    /// <summary>
    /// Select a camp remnant discovery variant.
    /// </summary>
    public static DiscoveryVariant SelectCampDiscovery(GameContext ctx)
    {
        return DiscoveryVariants.CampFinds[Random.Shared.Next(DiscoveryVariants.CampFinds.Length)];
    }

    /// <summary>
    /// Select a cache discovery variant.
    /// </summary>
    public static DiscoveryVariant SelectCacheDiscovery(GameContext ctx)
    {
        return DiscoveryVariants.CacheFinds[Random.Shared.Next(DiscoveryVariants.CacheFinds.Length)];
    }

    /// <summary>
    /// Select a valuable discovery - caches or abandoned camps.
    /// </summary>
    public static DiscoveryVariant SelectValuableDiscovery(GameContext ctx)
    {
        var pool = new List<(DiscoveryVariant variant, double weight)>();

        pool.AddRange(DiscoveryVariants.CacheFinds.Select(v => (v, 1.0)));
        pool.AddRange(DiscoveryVariants.CampFinds.Select(v => (v, 1.5)));

        return SelectWeighted(pool);
    }

    /// <summary>
    /// Select a meat/game discovery variant.
    /// </summary>
    public static DiscoveryVariant SelectGameDiscovery(GameContext ctx)
    {
        return DiscoveryVariants.SmallGameFinds[Random.Shared.Next(DiscoveryVariants.SmallGameFinds.Length)];
    }

    private static DiscoveryVariant SelectWeighted(List<(DiscoveryVariant variant, double weight)> pool)
    {
        if (pool.Count == 0)
            return DiscoveryVariants.SupplyFinds[0];

        var totalWeight = pool.Sum(p => p.weight);
        var roll = Random.Shared.NextDouble() * totalWeight;

        double cumulative = 0;
        foreach (var (variant, weight) in pool)
        {
            cumulative += weight;
            if (roll <= cumulative)
                return variant;
        }

        return pool[^1].variant;
    }
}
