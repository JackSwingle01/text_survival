namespace text_survival.Environments.Surface;

/// <summary>
/// Weather as the ground feels it, at one tile, already localized.
///
/// Deliberately not <c>Location.GetTemperature</c>: that is a human's experience of the air,
/// with wind chill on skin, clothing, shelter and their fire in it, and none of that melts
/// snow. <see cref="Location.GetSurfaceWeather"/> is the one place that builds this, so every
/// attenuation is applied exactly once.
/// </summary>
public readonly record struct SurfaceWeather(
    double AirTemperatureF,
    double WindLevel,
    double SunlightLevel,
    double SnowfallWeMPerMinute,
    double RainfallMPerMinute);
