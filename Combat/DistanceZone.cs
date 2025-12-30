namespace text_survival.Combat;

/// <summary>
/// Combat distance zones. Each zone has different available actions and
/// creates different tactical situations.
/// </summary>
public enum DistanceZone
{
    /// <summary>0-3m: Committed fighting. Trading blows, desperate.</summary>
    Melee,

    /// <summary>3-8m: The dance. Thrust range, circling, key decisions.</summary>
    Close,

    /// <summary>8-15m: Standoff. Throwing range, intimidation, probing.</summary>
    Mid,

    /// <summary>15-20m: Exit window. Disengage possible, animal deciding.</summary>
    Far
}

/// <summary>
/// Helper methods for distance zone calculations.
/// </summary>
public static class DistanceZoneHelper
{
    // Zone boundaries in meters
    public const double MeleeMax = 3.0;
    public const double CloseMax = 8.0;
    public const double MidMax = 15.0;
    public const double FarMax = 20.0;

    // Default distances for zone transitions
    public const double MeleeCenter = 2.0;
    public const double CloseCenter = 5.0;
    public const double MidCenter = 11.0;
    public const double FarCenter = 17.0;

    /// <summary>
    /// Gets the distance zone for a given distance in meters.
    /// </summary>
    public static DistanceZone GetZone(double distanceMeters)
    {
        return distanceMeters switch
        {
            <= MeleeMax => DistanceZone.Melee,
            <= CloseMax => DistanceZone.Close,
            <= MidMax => DistanceZone.Mid,
            _ => DistanceZone.Far
        };
    }

    /// <summary>
    /// Gets the center distance for a zone (used when moving to a zone).
    /// </summary>
    public static double GetZoneCenter(DistanceZone zone)
    {
        return zone switch
        {
            DistanceZone.Melee => MeleeCenter,
            DistanceZone.Close => CloseCenter,
            DistanceZone.Mid => MidCenter,
            DistanceZone.Far => FarCenter,
            _ => MidCenter
        };
    }

    /// <summary>
    /// Gets a display name for the zone.
    /// </summary>
    public static string GetZoneName(DistanceZone zone)
    {
        return zone switch
        {
            DistanceZone.Melee => "Melee Range",
            DistanceZone.Close => "Close Range",
            DistanceZone.Mid => "Mid Range",
            DistanceZone.Far => "Far Range",
            _ => "Unknown"
        };
    }

    /// <summary>
    /// Gets the zone one step closer to the animal.
    /// </summary>
    public static DistanceZone? GetCloserZone(DistanceZone current)
    {
        return current switch
        {
            DistanceZone.Far => DistanceZone.Mid,
            DistanceZone.Mid => DistanceZone.Close,
            DistanceZone.Close => DistanceZone.Melee,
            DistanceZone.Melee => null, // Can't get closer
            _ => null
        };
    }

    /// <summary>
    /// Gets the zone one step further from the animal.
    /// </summary>
    public static DistanceZone? GetFartherZone(DistanceZone current)
    {
        return current switch
        {
            DistanceZone.Melee => DistanceZone.Close,
            DistanceZone.Close => DistanceZone.Mid,
            DistanceZone.Mid => DistanceZone.Far,
            DistanceZone.Far => null, // Can't get farther (disengage instead)
            _ => null
        };
    }

    /// <summary>
    /// Checks if a weapon can reach at this distance zone.
    /// Spears can thrust at Close range. All weapons work at Melee.
    /// </summary>
    public static bool CanReachWithWeapon(DistanceZone zone, bool hasReachWeapon)
    {
        return zone switch
        {
            DistanceZone.Melee => true,
            DistanceZone.Close => hasReachWeapon, // Spear thrust range
            _ => false
        };
    }

    /// <summary>
    /// Checks if throwing is effective at this distance zone.
    /// </summary>
    public static bool CanThrowEffectively(DistanceZone zone)
    {
        return zone is DistanceZone.Close or DistanceZone.Mid;
    }
}
