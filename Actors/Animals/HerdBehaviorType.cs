namespace text_survival.Actors.Animals;

/// <summary>
/// Type of behavior for a herd. Used for serialization - behaviors are recreated from this on load.
/// </summary>
public enum HerdBehaviorType
{
    /// <summary>Prey animals: caribou, megaloceros, bison. Graze and flee from threats.</summary>
    Prey,

    /// <summary>Pack predators: wolves. Patrol territory, hunt cooperatively.</summary>
    PackPredator,

    /// <summary>Solitary predators: bears. Forage, patrol, defend territory.</summary>
    SolitaryPredator
}
