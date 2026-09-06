namespace text_survival.Environments.Surface;

/// <summary>
/// One horizontal layer of the ground, stored bottom-to-top by <see cref="GroundSurface"/>.
///
/// Fractions are a representation, not permission to lose quantities. Operations work in
/// metres of <see cref="SkeletonM"/> and <see cref="StoredWaterM"/> and recompute the
/// fractions afterwards, which is why nothing outside this file writes
/// <see cref="WaterSaturationPct"/> directly.
///
/// Saturation counts liquid *and* frozen pore water, so freezing cannot conjure free space
/// that a thaw would have to un-conjure.
/// </summary>
public sealed class SurfaceLayer
{
    public SurfaceMaterial Material { get; set; }
    public double ThicknessM { get; set; }
    public double CompactionPct { get; set; }
    public double WaterSaturationPct { get; set; }
    public double FrozenFractionPct { get; set; }

    public SurfaceLayer() { }

    public SurfaceLayer(SurfaceMaterial material, double thicknessM, double compactionPct = 0,
        double waterSaturationPct = 0, double frozenFractionPct = 0)
    {
        Material = material;
        ThicknessM = thicknessM;
        CompactionPct = compactionPct;
        WaterSaturationPct = waterSaturationPct;
        FrozenFractionPct = frozenFractionPct;
    }

    // ---- Derived geometry ----

    /// <summary>
    /// Fraction of this layer's volume that is pore space. Compaction is not porosity - the
    /// material decides what a given compaction means, which is why only one is stored.
    /// </summary>
    public double PoreCapacityPct(SubstrateProfile substrate) => Material switch
    {
        SurfaceMaterial.Snow => 1 - SnowIceFraction(CompactionPct),
        SurfaceMaterial.Ground => substrate.PoreCapacityPct * (1 - 0.35 * CompactionPct),
        SurfaceMaterial.Water => 1,   // a water layer's thickness is its water
        _ => 0
    };

    public static double SnowIceFraction(double compactionPct) =>
        SurfacePhysics.LooseSnowIceFraction
        + Math.Clamp(compactionPct, 0, 1)
            * (SurfacePhysics.DenseSnowIceFraction - SurfacePhysics.LooseSnowIceFraction);

    /// <summary>Metres of water this layer could hold if completely saturated.</summary>
    public double PoreVolumeM(SubstrateProfile substrate) => ThicknessM * PoreCapacityPct(substrate);

    /// <summary>Water in the pores, liquid and frozen together.</summary>
    public double StoredWaterM(SubstrateProfile substrate) => PoreVolumeM(substrate) * WaterSaturationPct;

    public double LiquidWaterM(SubstrateProfile substrate) => StoredWaterM(substrate) * (1 - FrozenFractionPct);

    public double FrozenWaterM(SubstrateProfile substrate) => StoredWaterM(substrate) * FrozenFractionPct;

    /// <summary>
    /// Water-equivalent of a snow layer's ice skeleton - water the layer is made of rather
    /// than holding, so melting it releases water *and* shrinks the layer.
    /// </summary>
    public double SkeletonWaterEquivalentM(SubstrateProfile substrate) =>
        Material == SurfaceMaterial.Snow
            ? ThicknessM * (1 - PoreCapacityPct(substrate)) / SurfacePhysics.IceExpansion
            : 0;

    /// <summary>Every drop of water this layer accounts for, however it is held.</summary>
    public double TotalWaterM(SubstrateProfile substrate) => Material switch
    {
        SurfaceMaterial.Water => ThicknessM,
        _ => StoredWaterM(substrate) + SkeletonWaterEquivalentM(substrate)
    };

    /// <summary>
    /// How tall this layer stands. A water layer's thickness is water-equivalent, and ice
    /// takes up more room than the water it froze from.
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public double PhysicalThicknessM => Material == SurfaceMaterial.Water
        ? ThicknessM * (1 - FrozenFractionPct) + ThicknessM * FrozenFractionPct * SurfacePhysics.IceExpansion
        : ThicknessM;

    /// <summary>Set the stored water directly, in metres.</summary>
    public void SetStoredWaterM(SubstrateProfile substrate, double waterM, double frozenM)
    {
        double capacity = PoreVolumeM(substrate);
        double stored = Math.Max(0, waterM);

        if (capacity <= SurfacePhysics.NegligibleM || stored <= SurfacePhysics.NegligibleM)
        {
            WaterSaturationPct = 0;
            FrozenFractionPct = 0;
            return;
        }

        WaterSaturationPct = Math.Clamp(stored / capacity, 0, 1);
        FrozenFractionPct = Math.Clamp(frozenM / stored, 0, 1);
    }
}
