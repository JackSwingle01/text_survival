using text_survival.Environments.Features;
using text_survival.Environments.Grid;
using text_survival.Items;

namespace text_survival.Environments.Factories;

/// <summary>
/// Template for a discoverable feature.
/// Defines what can spawn, how likely, and how long it typically takes to find.
/// </summary>
public record DiscoveryTemplate(
    Func<LocationFeature> CreateFeature,
    double SpawnChance,         // 0-1, probability this feature exists at a location
    double ExpectedHours,       // Expected hours of foraging to discover
    DiscoveryCategory Category  // Minor = inline message, Major = mini-event
);

/// <summary>
/// Generates hidden features for locations using seeded RNG.
/// Features have pre-calculated reveal thresholds using exponential distribution.
///
/// Discovery through foraging creates exploration incentive:
/// - Obvious things (abundance >= 1.0) are auto-discovered
/// - Hidden things require time investment to find
/// </summary>
public class DiscoveryGenerator
{
    private readonly Random _rng;

    /// <summary>
    /// Maximum multiplier for reveal threshold to prevent outliers.
    /// With expected 1 hour, no feature should require more than 10 hours to discover.
    /// </summary>
    private const double MaxRevealMultiplier = 10.0;

    public DiscoveryGenerator(int seed) => _rng = new Random(seed);

    /// <summary>
    /// Generate hidden features for a location based on its terrain.
    /// Each feature gets a pre-calculated reveal threshold.
    /// </summary>
    public List<HiddenFeature> GenerateFor(TerrainType terrain)
    {
        var pool = GetPoolForTerrain(terrain);
        var results = new List<HiddenFeature>();

        foreach (var template in pool)
        {
            if (_rng.NextDouble() < template.SpawnChance)
            {
                var feature = template.CreateFeature();
                double revealAt = GenerateRevealThreshold(template.ExpectedHours);

                results.Add(new HiddenFeature(
                    feature,
                    revealAt,
                    template.Category
                ));
            }
        }

        return results;
    }

    /// <summary>
    /// Generate a reveal threshold using exponential distribution.
    /// Creates natural variance: sometimes quick finds, sometimes long searches.
    ///
    /// Distribution properties:
    /// - 50% found before expected time
    /// - 37% found between 1x-2x expected
    /// - 13% take longer than 2x expected
    /// </summary>
    private double GenerateRevealThreshold(double expectedHours)
    {
        // Exponential distribution: -ln(U) * expected
        // U is uniform random (0, 1]
        double u = _rng.NextDouble();
        if (u == 0) u = double.Epsilon;  // Avoid ln(0)
        double threshold = -Math.Log(u) * expectedHours;

        // Cap to prevent frustrating outliers
        return Math.Min(threshold, expectedHours * MaxRevealMultiplier);
    }

    private static List<DiscoveryTemplate> GetPoolForTerrain(TerrainType terrain) => terrain switch
    {
        TerrainType.Forest => ForestPool,
        TerrainType.Clearing => ClearingPool,
        TerrainType.Plain => PlainPool,
        TerrainType.Hills => HillsPool,
        TerrainType.Rock => RockyPool,
        TerrainType.Marsh => MarshPool,
        TerrainType.Water => WaterPool,
        _ => []
    };

    #region Terrain Pools

    private static readonly List<DiscoveryTemplate> ForestPool =
    [
        // Minor discoveries - common finds
        new(FeatureFactory.CreateBerryBush,
            SpawnChance: 0.3, ExpectedHours: 1.0, DiscoveryCategory.Minor),

        new(FeatureFactory.CreateMixedDeadfall,
            SpawnChance: 0.25, ExpectedHours: 1.5, DiscoveryCategory.Minor),

        new(FeatureFactory.CreateCattails,
            SpawnChance: 0.15, ExpectedHours: 2.0, DiscoveryCategory.Minor),

        // Game trails - animals require scouting to locate
        new(DiscoveryFeatureFactory.CreateForestGameTrail,
            SpawnChance: 0.25, ExpectedHours: 1.5, DiscoveryCategory.Minor),

        // Minor harvestable discoveries
        new(DiscoveryFeatureFactory.CreateMedicinePatch,
            SpawnChance: 0.12, ExpectedHours: 2.5, DiscoveryCategory.Minor),

        new(DiscoveryFeatureFactory.CreateResinPocket,
            SpawnChance: 0.15, ExpectedHours: 1.5, DiscoveryCategory.Minor),

        new(DiscoveryFeatureFactory.CreateAbandonedDen,
            SpawnChance: 0.10, ExpectedHours: 2.0, DiscoveryCategory.Minor),

        new(DiscoveryFeatureFactory.CreateCharDeposit,
            SpawnChance: 0.10, ExpectedHours: 2.0, DiscoveryCategory.Minor),

        // New discoveries - materials and caches
        new(DiscoveryFeatureFactory.CreateAntlerShed,
            SpawnChance: 0.08, ExpectedHours: 2.0, DiscoveryCategory.Minor),

        new(DiscoveryFeatureFactory.CreateOldButcheringSite,
            SpawnChance: 0.06, ExpectedHours: 2.5, DiscoveryCategory.Minor),

        new(DiscoveryFeatureFactory.CreateTallowPot,
            SpawnChance: 0.05, ExpectedHours: 3.0, DiscoveryCategory.Minor),

        new(DiscoveryFeatureFactory.CreateDryWoodStack,
            SpawnChance: 0.07, ExpectedHours: 2.0, DiscoveryCategory.Minor),

        new(DiscoveryFeatureFactory.CreateCordageBundle,
            SpawnChance: 0.06, ExpectedHours: 2.5, DiscoveryCategory.Minor),

        new(DiscoveryFeatureFactory.CreateTinderCache,
            SpawnChance: 0.08, ExpectedHours: 2.0, DiscoveryCategory.Minor),

        new(DiscoveryFeatureFactory.CreateMedicineStash,
            SpawnChance: 0.05, ExpectedHours: 3.5, DiscoveryCategory.Major),

        // Major discoveries - rarer, more impactful
        new(FeatureFactory.CreateMassiveDeadfall,
            SpawnChance: 0.08, ExpectedHours: 3.0, DiscoveryCategory.Major),

        new(DiscoveryFeatureFactory.CreateNaturalShelter,
            SpawnChance: 0.05, ExpectedHours: 4.0, DiscoveryCategory.Major),

        // Salvage sites - major finds
        new(SalvageFeature.CreateAbandonedCamp,
            SpawnChance: 0.06, ExpectedHours: 3.5, DiscoveryCategory.Major),

        new(() => SalvageFeature.FromRewardPool(
            "TrapperStash", "Trapper's Stash", RewardPool.TrapperStash,
            "Cord wrapped around a stake. Someone's trapping supplies, cached here.",
            "They never came back for their gear.", 25),
            SpawnChance: 0.07, ExpectedHours: 4.0, DiscoveryCategory.Major),

        new(() => SalvageFeature.FromRewardPool(
            "HuntersBlind", "Hunter's Blind", RewardPool.HuntersBlind,
            "Branches piled for concealment. Someone watched and waited here.",
            "A spear butt protrudes from the debris.", 20),
            SpawnChance: 0.06, ExpectedHours: 3.0, DiscoveryCategory.Major),

        // Event trigger discoveries - trigger an event when found, then consumed
        new(() => new EventTriggerFeature("old_campsite"),
            SpawnChance: 0.05, ExpectedHours: 4.0, DiscoveryCategory.Major),

        new(() => new EventTriggerFeature("old_kill_site"),
            SpawnChance: 0.06, ExpectedHours: 3.0, DiscoveryCategory.Major),

        new(() => new EventTriggerFeature("beehive"),
            SpawnChance: 0.06, ExpectedHours: 3.0, DiscoveryCategory.Major),

        new(() => new EventTriggerFeature("squirrel_cache"),
            SpawnChance: 0.08, ExpectedHours: 2.0, DiscoveryCategory.Minor),

        new(() => new EventTriggerFeature("mushroom_patch"),
            SpawnChance: 0.10, ExpectedHours: 2.0, DiscoveryCategory.Minor),
    ];

    private static readonly List<DiscoveryTemplate> ClearingPool =
    [
        new(FeatureFactory.CreateBerryBush,
            SpawnChance: 0.35, ExpectedHours: 0.8, DiscoveryCategory.Minor),

        new(FeatureFactory.CreateMixedDeadfall,
            SpawnChance: 0.2, ExpectedHours: 1.2, DiscoveryCategory.Minor),

        new(DiscoveryFeatureFactory.CreateClearingGameTrail,
            SpawnChance: 0.2, ExpectedHours: 1.2, DiscoveryCategory.Minor),

        // Minor harvestable discoveries
        new(DiscoveryFeatureFactory.CreateMedicinePatch,
            SpawnChance: 0.12, ExpectedHours: 2.5, DiscoveryCategory.Minor),

        new(DiscoveryFeatureFactory.CreateCharDeposit,
            SpawnChance: 0.10, ExpectedHours: 2.0, DiscoveryCategory.Minor),

        // New discoveries
        new(DiscoveryFeatureFactory.CreateOldButcheringSite,
            SpawnChance: 0.05, ExpectedHours: 2.5, DiscoveryCategory.Minor),

        new(DiscoveryFeatureFactory.CreateDryWoodStack,
            SpawnChance: 0.06, ExpectedHours: 2.0, DiscoveryCategory.Minor),

        // Major salvage sites
        new(SalvageFeature.CreateAbandonedCamp,
            SpawnChance: 0.06, ExpectedHours: 3.5, DiscoveryCategory.Major),

        new(() => SalvageFeature.FromRewardPool(
            "HuntersBlind", "Hunter's Blind", RewardPool.HuntersBlind,
            "Branches piled for concealment. Someone watched and waited here.",
            "A spear butt protrudes from the debris.", 20),
            SpawnChance: 0.06, ExpectedHours: 3.0, DiscoveryCategory.Major),
    ];

    private static readonly List<DiscoveryTemplate> PlainPool =
    [
        new(DiscoveryFeatureFactory.CreateBonePile,
            SpawnChance: 0.15, ExpectedHours: 2.0, DiscoveryCategory.Minor),

        new(FeatureFactory.CreateMeltwaterPool,
            SpawnChance: 0.1, ExpectedHours: 2.5, DiscoveryCategory.Minor),

        new(DiscoveryFeatureFactory.CreatePlainSmallGame,
            SpawnChance: 0.2, ExpectedHours: 1.5, DiscoveryCategory.Minor),

        new(DiscoveryFeatureFactory.CreateCharDeposit,
            SpawnChance: 0.10, ExpectedHours: 2.0, DiscoveryCategory.Minor),

        // New discoveries
        new(DiscoveryFeatureFactory.CreateFrozenSmallGame,
            SpawnChance: 0.08, ExpectedHours: 2.0, DiscoveryCategory.Minor),

        new(DiscoveryFeatureFactory.CreateOldButcheringSite,
            SpawnChance: 0.06, ExpectedHours: 2.5, DiscoveryCategory.Minor),

        // Major salvage sites
        new(() => SalvageFeature.FromRewardPool(
            "HuntersBlind", "Hunter's Blind", RewardPool.HuntersBlind,
            "Branches piled for concealment. Someone watched and waited here.",
            "A spear butt protrudes from the debris.", 20),
            SpawnChance: 0.06, ExpectedHours: 3.0, DiscoveryCategory.Major),

        // Event trigger discoveries (triggers authored event on discovery)
        new(() => new EventTriggerFeature("frozen_traveler"),
            SpawnChance: 0.08, ExpectedHours: 5.0, DiscoveryCategory.Major),

        new(() => new EventTriggerFeature("bone_scatter"),
            SpawnChance: 0.08, ExpectedHours: 3.0, DiscoveryCategory.Major),
    ];

    private static readonly List<DiscoveryTemplate> HillsPool =
    [
        new(DiscoveryFeatureFactory.CreateFlintOutcrop,
            SpawnChance: 0.2, ExpectedHours: 2.5, DiscoveryCategory.Minor),

        new(DiscoveryFeatureFactory.CreateHillsSmallGame,
            SpawnChance: 0.15, ExpectedHours: 2.0, DiscoveryCategory.Minor),

        new(DiscoveryFeatureFactory.CreateAbandonedDen,
            SpawnChance: 0.10, ExpectedHours: 2.0, DiscoveryCategory.Minor),

        // New discoveries
        new(DiscoveryFeatureFactory.CreateFrozenSmallGame,
            SpawnChance: 0.07, ExpectedHours: 2.0, DiscoveryCategory.Minor),

        new(DiscoveryFeatureFactory.CreateAntlerShed,
            SpawnChance: 0.10, ExpectedHours: 1.5, DiscoveryCategory.Minor),

        new(DiscoveryFeatureFactory.CreateKnappingScatter,
            SpawnChance: 0.08, ExpectedHours: 2.0, DiscoveryCategory.Minor),

        new(DiscoveryFeatureFactory.CreateMedicineStash,
            SpawnChance: 0.05, ExpectedHours: 3.5, DiscoveryCategory.Major),

        new(DiscoveryFeatureFactory.CreateNaturalShelter,
            SpawnChance: 0.1, ExpectedHours: 3.0, DiscoveryCategory.Major),

        new(DiscoveryFeatureFactory.CreateIceCave,
            SpawnChance: 0.06, ExpectedHours: 3.5, DiscoveryCategory.Major),

        new(DiscoveryFeatureFactory.CreatePyriteSeam,
            SpawnChance: 0.08, ExpectedHours: 4.0, DiscoveryCategory.Major),

        // Major discoveries - water and salvage
        new(DiscoveryFeatureFactory.CreateHiddenSpring,
            SpawnChance: 0.08, ExpectedHours: 3.5, DiscoveryCategory.Major),

        // Event trigger discoveries (triggers authored event on discovery)
        new(() => new EventTriggerFeature("hidden_cache"),
            SpawnChance: 0.10, ExpectedHours: 4.0, DiscoveryCategory.Major),

        new(() => new EventTriggerFeature("frozen_traveler"),
            SpawnChance: 0.06, ExpectedHours: 5.0, DiscoveryCategory.Major),

        new(() => new EventTriggerFeature("water_source"),
            SpawnChance: 0.06, ExpectedHours: 3.5, DiscoveryCategory.Major),
    ];

    private static readonly List<DiscoveryTemplate> RockyPool =
    [
        new(DiscoveryFeatureFactory.CreateFlintOutcrop,
            SpawnChance: 0.25, ExpectedHours: 2.0, DiscoveryCategory.Minor),

        new(DiscoveryFeatureFactory.CreateBonePile,
            SpawnChance: 0.1, ExpectedHours: 2.0, DiscoveryCategory.Minor),

        new(DiscoveryFeatureFactory.CreateRockySmallGame,
            SpawnChance: 0.12, ExpectedHours: 2.0, DiscoveryCategory.Minor),

        new(DiscoveryFeatureFactory.CreateAbandonedDen,
            SpawnChance: 0.10, ExpectedHours: 2.0, DiscoveryCategory.Minor),

        // New discoveries
        new(DiscoveryFeatureFactory.CreateKnappingScatter,
            SpawnChance: 0.10, ExpectedHours: 2.0, DiscoveryCategory.Minor),

        new(DiscoveryFeatureFactory.CreateTallowPot,
            SpawnChance: 0.06, ExpectedHours: 2.5, DiscoveryCategory.Minor),

        new(DiscoveryFeatureFactory.CreateTinderCache,
            SpawnChance: 0.08, ExpectedHours: 2.0, DiscoveryCategory.Minor),

        new(DiscoveryFeatureFactory.CreateNaturalShelter,
            SpawnChance: 0.12, ExpectedHours: 2.5, DiscoveryCategory.Major),

        new(DiscoveryFeatureFactory.CreateIceCave,
            SpawnChance: 0.07, ExpectedHours: 3.5, DiscoveryCategory.Major),

        // Major discoveries - water
        new(DiscoveryFeatureFactory.CreateHiddenSpring,
            SpawnChance: 0.08, ExpectedHours: 3.5, DiscoveryCategory.Major),
    ];

    private static readonly List<DiscoveryTemplate> MarshPool =
    [
        new(FeatureFactory.CreateCattails,
            SpawnChance: 0.3, ExpectedHours: 1.0, DiscoveryCategory.Minor),

        new(FeatureFactory.CreateMarshWater,
            SpawnChance: 0.25, ExpectedHours: 1.5, DiscoveryCategory.Minor),

        new(DiscoveryFeatureFactory.CreateMarshWaterfowl,
            SpawnChance: 0.25, ExpectedHours: 1.0, DiscoveryCategory.Minor),

        new(DiscoveryFeatureFactory.CreateMedicinePatch,
            SpawnChance: 0.12, ExpectedHours: 2.5, DiscoveryCategory.Minor),

        // New discoveries
        new(DiscoveryFeatureFactory.CreateCordageBundle,
            SpawnChance: 0.07, ExpectedHours: 2.0, DiscoveryCategory.Minor),

        new(DiscoveryFeatureFactory.CreateMedicineStash,
            SpawnChance: 0.05, ExpectedHours: 3.5, DiscoveryCategory.Major),

        // Major salvage site
        new(() => SalvageFeature.FromRewardPool(
            "TrapperStash", "Trapper's Stash", RewardPool.TrapperStash,
            "Cord wrapped around a stake. Someone's trapping supplies, cached here.",
            "They never came back for their gear.", 25),
            SpawnChance: 0.07, ExpectedHours: 4.0, DiscoveryCategory.Major),

        // Event trigger discovery
        new(() => new EventTriggerFeature("mushroom_patch"),
            SpawnChance: 0.08, ExpectedHours: 2.5, DiscoveryCategory.Minor),
    ];

    private static readonly List<DiscoveryTemplate> WaterPool =
    [
        new(FeatureFactory.CreateIceSource,
            SpawnChance: 0.3, ExpectedHours: 1.0, DiscoveryCategory.Minor),
    ];

    #endregion
}
