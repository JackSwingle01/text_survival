using text_survival.Environments.Grid;

namespace text_survival.Environments.Surface;

/// <summary>
/// What the ground under a tile is like before anything falls on it. Terrain picks one; a
/// named location may override it. Only the properties something reads are here.
/// </summary>
/// <param name="ThicknessM">
/// Depth of the active layer - the part that wets, dries and freezes, and the reference the
/// surface measures its height against.
/// </param>
/// <param name="PoreCapacityPct">Fraction of that volume that is pore space.</param>
/// <param name="PermeabilityMPerMinute">How fast liquid can soak in from above.</param>
/// <param name="DrainagePerMinute">Fraction of free pore water lost downward per minute.</param>
/// <param name="RetentionPct">Fraction of pore capacity held against drainage.</param>
/// <param name="RunoffPerMinute">Fraction of ponded water that runs off the tile per minute.</param>
/// <param name="SoftnessLevel">0 is bare rock, 1 is peat: how far wet ground turns to mud.</param>
/// <param name="HoldsStandingWater">
/// False for permanent water bodies, which <see cref="Features.WaterFeature"/> owns - two
/// systems answering the same question would disagree the first time one changed.
/// </param>
public sealed record SubstrateProfile(
    string Name,
    double ThicknessM,
    double PoreCapacityPct,
    double PermeabilityMPerMinute,
    double DrainagePerMinute,
    double RetentionPct,
    double RunoffPerMinute,
    double InitialSaturationPct,
    double SoftnessLevel,
    bool HoldsStandingWater = true)
{
    /// <summary>Pore volume of the whole active layer, in metres of water.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public double PoreVolumeM => ThicknessM * PoreCapacityPct;

    public static readonly SubstrateProfile Loam =
        new("loam", 0.30, 0.40, 0.00030, 0.00080, 0.35, 0.0006, 0.30, 0.50);

    public static readonly SubstrateProfile ForestFloor =
        new("forest floor", 0.35, 0.45, 0.00050, 0.00100, 0.40, 0.0004, 0.35, 0.45);

    public static readonly SubstrateProfile Slope =
        new("thin slope soil", 0.20, 0.30, 0.00040, 0.00250, 0.25, 0.0060, 0.20, 0.30);

    // Takes almost nothing in, so rain ponds at once - and then runs off the tile, which is
    // why bare rock is wet but rarely flooded.
    public static readonly SubstrateProfile Rock =
        new("bare rock", 0.08, 0.12, 0.00003, 0.00300, 0.10, 0.0100, 0.10, 0.05);

    // The opposite: holds nearly everything and lets go of none of it, so a marsh is wet in
    // any weather and ponds the moment it rains.
    public static readonly SubstrateProfile Peat =
        new("peat", 0.50, 0.60, 0.00005, 0.00005, 0.85, 0.0001, 0.85, 1.00);

    /// <summary>Lake and river ice. Inert here.</summary>
    public static readonly SubstrateProfile IceSheet =
        new("ice", 0.05, 0.0, 0.0, 0.0, 0.0, 0.0200, 0.0, 0.0, HoldsStandingWater: false);

    /// <summary>Nothing walks here, so nothing models it.</summary>
    public static readonly SubstrateProfile Impassable =
        new("impassable", 0.05, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, HoldsStandingWater: false);

    public static SubstrateProfile For(TerrainType terrain) => terrain switch
    {
        TerrainType.Forest => ForestFloor,
        TerrainType.Clearing => Loam,
        TerrainType.Plain => Loam,
        TerrainType.Hills => Slope,
        TerrainType.Rock => Rock,
        TerrainType.Marsh => Peat,
        TerrainType.Water => IceSheet,
        TerrainType.Mountain => Impassable,
        TerrainType.DeepWater => Impassable,
        _ => Loam
    };
}
