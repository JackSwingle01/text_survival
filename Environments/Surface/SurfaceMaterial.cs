namespace text_survival.Environments.Surface;

/// <summary>
/// What a layer is made of.
///
/// Mud, slush and ice are not here. Mud is <see cref="Ground"/> that is wet and unfrozen;
/// slush is <see cref="Snow"/> holding liquid; ice is the frozen fraction of whatever holds
/// the water. Making them materials would mean a table of pairwise reactions, and every one
/// of those rules is already implied by the numbers on the layer.
/// </summary>
public enum SurfaceMaterial
{
    Ground,
    Snow,
    Water
}

/// <summary>
/// Depths are metres of a column one square metre in section, so a depth is also a volume
/// and, times a density, a mass.
/// </summary>
public static class SurfacePhysics
{
    public const double WaterDensityKgPerM3 = 1000;
    public const double IceDensityKgPerM3 = 917;

    public const double IceExpansion = WaterDensityKgPerM3 / IceDensityKgPerM3;

    public const double FreezingPointF = 32;

    /// <summary>Depths below this are rounding error, not state.</summary>
    public const double NegligibleM = 1e-9;

    // Fresh snow is about a tenth the density of ice; wind-packed old snow is nearer 0.6.
    // Compaction interpolates; the pore space is whatever is left.
    public const double LooseSnowIceFraction = 0.109;
    public const double DenseSnowIceFraction = 0.60;

    /// <summary>Fraction of its pore space wet snow holds against gravity before it drips.</summary>
    public const double SnowRetentionPct = 0.05;

    /// <summary>How fast meltwater moves down through snow, metres per minute.</summary>
    public const double SnowPermeabilityMPerMinute = 0.02;

    /// <summary>An e-fold of settling every two and a half days.</summary>
    public const double SnowSettlingPerMinute = 0.00028;

    // A degree-hour model, not a thermal solver: roughly 3 mm water-equivalent per degree
    // Celsius per day, the textbook figure for melting snow, which also gives about two
    // centimetres of ice growth a night on standing water at 20F.
    public const double PhaseChangeMPerFPerMinute = 1.16e-6;

    /// <summary>Extra melt from sunlight on the surface, metres water-equivalent per minute.</summary>
    public const double SunMeltMPerMinute = 3.3e-5;

    /// <summary>Wind strips heat from the surface, whichever way the phase change runs.</summary>
    public const double WindPhaseChangeFactor = 0.8;

    /// <summary>Evaporation and sublimation in still air, metres per minute.</summary>
    public const double BaseEvaporationMPerMinute = 1.0e-7;

    /// <summary>Reciprocal insulation from snow lying over a layer, per metre.</summary>
    public const double SnowInsulationPerMetre = 12;

    /// <summary>
    /// Deepest liquid water this pass models. Above it, water leaves the tile through the
    /// overflow sink rather than piling up; routing between tiles is a later problem.
    /// </summary>
    public const double MaxStandingWaterDepthM = 0.3048;
}
