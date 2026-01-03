namespace text_survival.Bodies;

/// <summary>
/// Lightweight context for calculating situation-dependent abilities.
/// Capacities measure raw body function; Abilities add environmental context.
/// </summary>
public readonly struct AbilityContext
{
    /// <summary>
    /// Inventory weight / max weight (0-1). Affects Speed via Strength modulation.
    /// </summary>
    public double EncumbrancePct { get; init; }

    /// <summary>
    /// Environmental darkness level (0-1). Affects Perception and Dexterity.
    /// 0 = bright daylight, 1 = pitch black.
    /// </summary>
    public double DarknessLevel { get; init; }

    /// <summary>
    /// Current wetness from weather/water exposure (0-1). Affects Dexterity (slippery grip).
    /// </summary>
    public double WetnessPct { get; init; }

    /// <summary>
    /// Whether actor has an active light source (fire, torch). Negates darkness penalties.
    /// </summary>
    public bool HasLightSource { get; init; }

    /// <summary>
    /// Default context with no penalties applied.
    /// </summary>
    public static AbilityContext Default => new();
}
