using text_survival.Actors;
using text_survival.Environments;
using text_survival.Items;

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

    /// <summary>
    /// Create context from inventory (for encumbrance).
    /// </summary>
    public static AbilityContext FromInventory(Inventory? inventory)
    {
        double encumbrance = 0;
        if (inventory != null && inventory.MaxWeightKg > 0)
        {
            encumbrance = inventory.CurrentWeightKg / inventory.MaxWeightKg;
        }
        return new AbilityContext { EncumbrancePct = encumbrance };
    }

    /// <summary>
    /// Create context from actor (for wetness) and inventory (for encumbrance).
    /// Does not include darkness - use FromFullContext for perception calculations.
    /// </summary>
    public static AbilityContext FromActorAndInventory(Actor actor, Inventory? inventory)
    {
        double encumbrance = 0;
        if (inventory != null && inventory.MaxWeightKg > 0)
        {
            encumbrance = inventory.CurrentWeightKg / inventory.MaxWeightKg;
        }

        double wetness = actor.EffectRegistry.GetEffectsByKind("Wet").FirstOrDefault()?.Severity ?? 0;

        return new AbilityContext
        {
            EncumbrancePct = encumbrance,
            WetnessPct = wetness
        };
    }

    /// <summary>
    /// Create full context including darkness for perception/dexterity calculations.
    /// </summary>
    /// <param name="actor">Actor for wetness effects</param>
    /// <param name="inventory">Inventory for encumbrance and lit torch check</param>
    /// <param name="location">Location for darkness and active heat source</param>
    /// <param name="hourOfDay">Hour (0-23) for time-based darkness</param>
    public static AbilityContext FromFullContext(Actor actor, Inventory? inventory, Location location, int hourOfDay)
    {
        double encumbrance = 0;
        if (inventory != null && inventory.MaxWeightKg > 0)
        {
            encumbrance = inventory.CurrentWeightKg / inventory.MaxWeightKg;
        }

        double wetness = actor.EffectRegistry.GetEffectsByKind("Wet").FirstOrDefault()?.Severity ?? 0;

        // Calculate darkness level
        double darkness = CalculateDarkness(location.IsDark, hourOfDay);

        // Check for light sources
        bool hasLight = location.HasActiveHeatSource() || (inventory?.HasLitTorch ?? false);

        return new AbilityContext
        {
            EncumbrancePct = encumbrance,
            WetnessPct = wetness,
            DarknessLevel = darkness,
            HasLightSource = hasLight
        };
    }

    /// <summary>
    /// Calculate darkness level from location and time of day.
    /// Returns 0-1 where 0 = bright daylight, 1 = pitch black.
    /// </summary>
    private static double CalculateDarkness(bool locationIsDark, int hourOfDay)
    {
        // Dark locations (caves) are always fully dark
        if (locationIsDark)
            return 1.0;

        // Time-based darkness (matches GameContext.TimeOfDay logic)
        return hourOfDay switch
        {
            < 5 => 1.0,    // Night - full darkness
            < 6 => 0.5,    // Dawn - partial darkness
            < 11 => 0.0,   // Morning - daylight
            < 13 => 0.0,   // Noon - daylight
            < 17 => 0.0,   // Afternoon - daylight
            < 20 => 0.2,   // Evening - dim light
            < 21 => 0.6,   // Dusk - partial darkness
            _ => 1.0       // Night - full darkness
        };
    }
}
