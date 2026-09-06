namespace text_survival.Environments.Surface;

/// <summary>
/// Getting at something the snow has covered. Finding a thing and reaching it are different
/// questions, and the snow only answers the second.
///
/// A hole is one number: how far this target's footprint has been dug below the tile's
/// cover. It refills when it snows and shallows when it thaws, both for free. It is not
/// route packing - clearing one bush does not expose its neighbours or speed up the tile.
/// </summary>
public sealed partial class GroundSurface
{
    /// <summary>Minutes of bare-handed work per cubic metre, loose snow to hard-packed.</summary>
    private const double LooseSnowMinutesPerM3 = 90;
    private const double DenseSnowMinutesPerM3 = 300;
    private const double IceResistanceMultiplier = 2.5;

    /// <summary>Half-buried is the line at which a target needs digging out.</summary>
    private const double ToleratedCoverFraction = 0.5;

    /// <summary>Floor on that, so a flat thing isn't blocked by a dusting.</summary>
    private const double ToleratedCoverFloorM = 0.10;

    /// <summary>Solid cover over this target right now, its own hole taken off.</summary>
    public double GetBlockingCoverM(SurfacePlacement placement)
    {
        double cover = Math.Max(0, SolidCoverHeightM - placement.BaseElevationM);
        double cleared = ClampCleared(placement.Id, cover);
        return Math.Max(0, cover - cleared);
    }

    private static double ToleratedCoverM(SurfacePlacement placement) =>
        Math.Max(ToleratedCoverFloorM, placement.HeightM * ToleratedCoverFraction);

    /// <summary>Tolerant by a rounding error: digging clears to exactly the threshold.</summary>
    public bool IsAccessBlocked(SurfacePlacement placement) =>
        GetBlockingCoverM(placement) > ToleratedCoverM(placement) + SurfacePhysics.NegligibleM;

    /// <summary>
    /// Minutes of bare-handed digging left to reach this target; zero when it is already
    /// reachable.
    /// </summary>
    public double GetExcavationMinutes(SurfacePlacement placement)
    {
        double remove = GetBlockingCoverM(placement) - ToleratedCoverM(placement);
        if (remove <= 0) return 0;

        return remove * placement.FootprintM2 * ResistanceMinutesPerM3();
    }

    /// <summary>
    /// Spend <paramref name="effortMinutes"/> digging. <paramref name="toolFactor"/> is 1
    /// bare-handed and 2 with a working shovel, as every other digging job here treats one.
    /// </summary>
    /// <returns>Metres of cover actually removed.</returns>
    public double ApplyExcavation(SurfacePlacement placement, double effortMinutes, double toolFactor = 1)
    {
        if (effortMinutes <= 0) return 0;

        double cover = Math.Max(0, SolidCoverHeightM - placement.BaseElevationM);
        double cleared = ClampCleared(placement.Id, cover);
        double remaining = Math.Max(0, cover - cleared - ToleratedCoverM(placement));
        if (remaining <= 0) return 0;

        double resistance = ResistanceMinutesPerM3() * Math.Max(placement.FootprintM2, 0.01);
        double removed = Math.Min(remaining, effortMinutes * Math.Max(toolFactor, 0.1) / resistance);
        if (removed <= SurfacePhysics.NegligibleM) return 0;

        // The hole deepens by what came out, and the tile's surface rises by the trickle
        // spread around the rim. Without that second term the redistribution reburies the
        // target it was just dug out of.
        double spread = Redistribute(removed, placement.FootprintM2);
        _clearedDepthM[placement.Id] = cleared + removed + spread;
        return removed;
    }

    /// <summary>
    /// Snow lifted out of the hole goes on the ground around it. Over a hundred metres
    /// square that is a vanishing depth, which is why it is added rather than assumed away.
    /// </summary>
    /// <returns>How much the tile's surface rose as a result.</returns>
    private double Redistribute(double removedDepthM, double footprintM2)
    {
        double spread = removedDepthM * Math.Max(footprintM2, 0) / TileAreaM2;
        if (spread <= SurfacePhysics.NegligibleM) return 0;

        double before = SolidCoverHeightM;
        double waterEquivalent = spread * SurfaceLayer.SnowIceFraction(TopSnowCompaction) / SurfacePhysics.IceExpansion;
        AddSnow(waterEquivalent);
        return Math.Max(0, SolidCoverHeightM - before);
    }

    /// <summary>
    /// A hole can never be deeper than the cover it was dug through, which is what stops an
    /// old hole banking credit against next winter's snowfall.
    /// </summary>
    private double ClampCleared(string id, double coverM)
    {
        if (!_clearedDepthM.TryGetValue(id, out double cleared)) return 0;

        if (cleared <= coverM) return cleared;

        if (coverM <= SurfacePhysics.NegligibleM)
        {
            _clearedDepthM.Remove(id);
            return 0;
        }

        _clearedDepthM[id] = coverM;
        return coverM;
    }

    private double ResistanceMinutesPerM3()
    {
        double compaction = TopSnowCompaction;
        double resistance = LooseSnowMinutesPerM3
            + compaction * (DenseSnowMinutesPerM3 - LooseSnowMinutesPerM3);

        if (TopSnowFrozenLiquid > 0.25) resistance *= IceResistanceMultiplier;

        return resistance;
    }
}
