namespace text_survival.Environments.Surface;

/// <summary>
/// Where a thing sits on the ground and how big it is - enough to say whether snow has
/// covered it and what digging it out would cost. Features carry defaults; only where an
/// instance was put down is stored.
/// </summary>
/// <param name="Id">Stable per-object key, so a half-dug hole survives a save.</param>
/// <param name="BaseElevationM">Against <see cref="GroundSurface.SurfaceHeightM"/>; 0 is ground level.</param>
/// <param name="HeightM">How tall it stands. A berry bush is not buried by an inch of snow.</param>
/// <param name="FootprintM2">How much ground has to be cleared to reach it.</param>
public readonly record struct SurfacePlacement(
    string Id,
    double BaseElevationM,
    double HeightM,
    double FootprintM2);
