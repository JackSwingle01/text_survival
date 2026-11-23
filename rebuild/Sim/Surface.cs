namespace Survival;

/// The ground at a place as it is (Ground.Underfoot), and its effects, from it alone: the column stays Ground's.
/// TroddenLevel is a trodden edge's packing at its half of a tile (Trodden).
public readonly record struct Surface(
    double SnowDepthM,
    double SnowPackingLevel,
    double SlushLevel,
    double CrustLevel,
    double MudLevel,
    double DampnessLevel,
    double StandingLiquidM,
    double IceM,
    bool Iced,
    double CoverHeightM,
    double TroddenLevel = 0)
{
    /// Snow deeper than this is what a foot stands on, hiding the footing of whatever is under it.
    const double SnowFootingDepthM = 0.05;
    /// Pond ice this thick is as slick as ice gets; a thinner film is slick in proportion.
    const double FullSlickIceM = 0.004;
    const double SlickIceHazardDelta = 0.35;
    const double TroddenSlickHazardDelta = 0.05;

    bool SnowIsFooting => SnowDepthM > SnowFootingDepthM;

    public bool Crusted => SnowIsFooting && CrustLevel > 0.25;

    public double WaterDepthM => StandingLiquidM + IceM;

    public double DrinkableWaterM => Iced ? 0 : StandingLiquidM;

    public Surface Trodden(double level) => level <= 0 ? this
        : this with { SnowPackingLevel = SnowPackingLevel + (1 - SnowPackingLevel) * level, TroddenLevel = level };

    public double TraversalFactor
    {
        get
        {
            double factor = 1;
            if (SnowDepthM > 0.02)
            {
                double looseness = 1 - 0.55 * SnowPackingLevel; // loose snow you wade; packed you walk on
                factor *= 1 + Math.Pow(SnowDepthM / 0.10, 1.3) * 0.25 * looseness;
            }
            if (StandingLiquidM > 0.01) factor *= 1 + StandingLiquidM / Ground.MaxStandingWaterDepthM * 0.8;
            factor *= 1 + MudLevel * 0.25;
            return Math.Clamp(factor, 1, 6);
        }
    }

    public double HazardDelta
    {
        get
        {
            double hazard = 0;
            if (SnowIsFooting)
            {
                hazard += 0.05;
                if (Crusted) hazard += 0.10;
                hazard += TroddenSlickHazardDelta * TroddenLevel;
            }
            else hazard += SlickIceHazardDelta * Math.Min(1, IceM / FullSlickIceM);
            hazard += MudLevel * 0.05;
            return Math.Clamp(hazard, 0, 0.5);
        }
    }

    /// In proportion to how much of it is under the cover, floored: a discovery that can never be made should not have been placed.
    public double SearchFactor(double baseElevationM, double verticalExtentM)
    {
        const double Floor = 0.1;
        double extent = Math.Max(verticalExtentM, 0.05);
        double cover = CoverHeightM - baseElevationM;
        if (cover <= 0) return 1;
        return 1 - Math.Clamp(cover / extent, 0, 1) * (1 - Floor);
    }

    /// Per minute, before clothing or bedding. Each contact has a ceiling; frozen ground has no liquid to give.
    public double ContactWettingRate(SurfaceContact contact, double currentWetnessPct)
    {
        double pond = Math.Clamp(StandingLiquidM / 0.15, 0, 1); // ankle-deep is as wet as walking gets
        double slush = SlushLevel * 0.3;
        double damp = DampnessLevel * 0.05;
        double source = Math.Clamp(pond + slush + damp, 0, 1);
        if (source <= 0) return 0;

        double rate = contact switch
        {
            SurfaceContact.Walking => 0.12,
            SurfaceContact.RestingOnGround => 0.10,
            SurfaceContact.WorkingOnGround => 0.06,
            _ => throw new ArgumentOutOfRangeException(nameof(contact), contact, null),
        };
        return currentWetnessPct >= ContactCeiling(contact) ? 0 : source * rate;
    }

    double ContactCeiling(SurfaceContact contact) => contact switch
    {
        SurfaceContact.RestingOnGround => 0.7,
        SurfaceContact.WorkingOnGround => 0.4,
        // Hip-deep (0.9 m) wets you all over; wet ground with nothing on it still gets into your boots.
        _ => Math.Clamp(Math.Max(StandingLiquidM, SnowDepthM) / 0.9, 0.1, 1),
    };
}
