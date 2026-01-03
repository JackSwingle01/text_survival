using text_survival.Actions;
using text_survival.Actors;
using text_survival.Bodies;
using text_survival.Environments.Grid;
using text_survival.Items;

namespace text_survival.Environments;

public static class TravelProcessor
{
    /// <summary>
    /// Threshold above which terrain is considered hazardous enough to offer speed choice.
    /// </summary>
    public const double HazardousTerrainThreshold = 0.3;

    /// <summary>
    /// Time multiplier for careful travel (slower but safe).
    /// </summary>
    public const double CarefulTravelMultiplier = 1.5;

    /// <summary>
    /// Maximum injury risk cap.
    /// </summary>
    public const double MaxInjuryRisk = 0.5;

    /// <summary>
    /// Calculate injury risk for quick travel through hazardous terrain.
    /// Returns 0-0.5 probability of injury.
    /// </summary>
    public static double GetInjuryRisk(Location location, Actor actor, Weather weather)
    {
        double baseRisk = location.GetEffectiveTerrainHazard();
        if (baseRisk < HazardousTerrainThreshold) return 0;

        // Weather modifiers
        double weatherMod = 0;
        if (weather.Precipitation > 0.3 || weather.CurrentCondition == Weather.WeatherCondition.LightSnow)
            weatherMod = 0.15;
        if (weather.CurrentCondition == Weather.WeatherCondition.Blizzard ||
            weather.CurrentCondition == Weather.WeatherCondition.Stormy)
            weatherMod = 0.25;

        // Actor capacity modifier - impaired movement increases risk
        var capacities = actor.GetCapacities();
        double capacityMod = (1 - capacities.Moving) * 0.3;

        return Math.Min(MaxInjuryRisk, baseRisk + weatherMod + capacityMod);
    }

    /// <summary>
    /// Check if terrain is hazardous enough to warrant speed choice.
    /// </summary>
    public static bool IsHazardousTerrain(Location location) =>
        location.GetEffectiveTerrainHazard() >= HazardousTerrainThreshold;


    /// <summary>
    /// Calculate traversal time for a single segment (exiting or entering a location).
    /// </summary>
    public static int CalculateSegmentTime(Location location, Actor actor, Inventory? inventory = null)
    {
        if (location.BaseTraversalMinutes == 0) return 0;

        double multiplier = location.GetEffectiveTerrainHazard();

        // Weather from location's zone
        var weather = location.Weather;
        if (weather.WindSpeed > 0.5)
            multiplier *= 1 + (weather.WindSpeed * 0.3 * location.WindFactor);
        if (weather.Precipitation > 0.5)
            multiplier *= 1 + (weather.Precipitation * 0.2);

        // Encumbrance from inventory
        if (inventory != null && inventory.MaxWeightKg > 0)
        {
            double encumbrance = inventory.CurrentWeightKg / inventory.MaxWeightKg;
            if (encumbrance > 0.5)
                multiplier *= 1 + (encumbrance * 0.4);
        }

        int baseTime = (int)Math.Ceiling(location.BaseTraversalMinutes * (1 + multiplier) * actor.GetMovementFactor());

        return baseTime;
    }

    /// <summary>
    /// Get total traversal time from origin to destination (exit origin + enter destination).
    /// </summary>
    public static int GetTraversalMinutes(Location origin, Location destination, Actor actor, Inventory? inventory = null)
    {
        int exitTime = CalculateSegmentTime(origin, actor, inventory);
        int entryTime = CalculateSegmentTime(destination, actor, inventory);
        return exitTime + entryTime;
    }

}