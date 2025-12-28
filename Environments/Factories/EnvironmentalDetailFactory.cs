using text_survival.Environments.Features;
using text_survival.Environments.Grid;

namespace text_survival.Environments.Factories;

/// <summary>
/// Generates environmental details for terrain tiles based on terrain type.
/// Details add world flavor and minor discoverable resources.
/// </summary>
public static class EnvironmentalDetailFactory
{
    /// <summary>
    /// Generate 0-2 environmental details for a terrain tile.
    /// Uses position hash for deterministic but varied results.
    /// </summary>
    public static List<EnvironmentalDetail> GenerateForTerrain(TerrainType terrain, int seed)
    {
        var rng = new Random(seed);
        var details = new List<EnvironmentalDetail>();

        // 65% chance of no details (details are relatively rare finds)
        if (rng.NextDouble() < 0.65)
            return details;

        // Get pool of possible details for this terrain
        var pool = GetDetailPool(terrain);
        if (pool.Count == 0)
            return details;

        // 50% chance of 1 detail, 50% chance of 2 details (when any)
        int count = rng.NextDouble() < 0.5 ? 1 : 2;

        // Select random details from pool
        var shuffled = pool.OrderBy(_ => rng.Next()).ToList();
        for (int i = 0; i < Math.Min(count, shuffled.Count); i++)
        {
            var factory = shuffled[i];
            details.Add(factory());
        }

        return details;
    }

    /// <summary>
    /// Get the pool of detail factories appropriate for a terrain type.
    /// </summary>
    private static List<Func<EnvironmentalDetail>> GetDetailPool(TerrainType terrain)
    {
        return terrain switch
        {
            TerrainType.Forest => ForestDetails(),
            TerrainType.Clearing => ClearingDetails(),
            TerrainType.Plain => PlainDetails(),
            TerrainType.Hills => HillsDetails(),
            TerrainType.Rock => RockDetails(),
            TerrainType.Marsh => MarshDetails(),
            TerrainType.Water => WaterDetails(),
            _ => []
        };
    }

    private static List<Func<EnvironmentalDetail>> ForestDetails() =>
    [
        EnvironmentalDetail.FallenLog,
        EnvironmentalDetail.HollowTree,
        EnvironmentalDetail.BentBranches,
        () => EnvironmentalDetail.AnimalTracks("deer"),
        () => EnvironmentalDetail.AnimalTracks("wolf"),
        () => EnvironmentalDetail.AnimalDroppings("wolf"),
        EnvironmentalDetail.ScatteredBones
    ];

    private static List<Func<EnvironmentalDetail>> ClearingDetails() =>
    [
        EnvironmentalDetail.FallenLog,
        () => EnvironmentalDetail.AnimalTracks("deer"),
        () => EnvironmentalDetail.AnimalTracks("rabbit"),
        EnvironmentalDetail.ScatteredBones
    ];

    private static List<Func<EnvironmentalDetail>> PlainDetails() =>
    [
        () => EnvironmentalDetail.AnimalTracks("deer"),
        () => EnvironmentalDetail.AnimalTracks("wolf"),
        EnvironmentalDetail.ScatteredBones,
        EnvironmentalDetail.FrozenPuddle
    ];

    private static List<Func<EnvironmentalDetail>> HillsDetails() =>
    [
        EnvironmentalDetail.StonePile,
        EnvironmentalDetail.BentBranches,
        () => EnvironmentalDetail.AnimalDroppings("bear"),
        () => EnvironmentalDetail.AnimalTracks("deer"),
        EnvironmentalDetail.ScatteredBones
    ];

    private static List<Func<EnvironmentalDetail>> RockDetails() =>
    [
        EnvironmentalDetail.StonePile,
        EnvironmentalDetail.StonePile, // Double weight for stone in rocky areas
        () => EnvironmentalDetail.AnimalDroppings("wolf"),
        EnvironmentalDetail.ScatteredBones
    ];

    private static List<Func<EnvironmentalDetail>> MarshDetails() =>
    [
        EnvironmentalDetail.FrozenPuddle,
        () => EnvironmentalDetail.AnimalTracks("deer"),
        EnvironmentalDetail.BentBranches,
        EnvironmentalDetail.ScatteredBones
    ];

    private static List<Func<EnvironmentalDetail>> WaterDetails() =>
    [
        () => EnvironmentalDetail.AnimalTracks("deer"),
        EnvironmentalDetail.ScatteredBones, // Something fell through?
        EnvironmentalDetail.FrozenPuddle    // Edge puddles
    ];
}
