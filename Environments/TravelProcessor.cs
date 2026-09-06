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
        if (weather.PrecipitationPct > 0.3 || weather.CurrentCondition == Weather.WeatherCondition.LightSnow)
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

        // The shape of the ground, not its state. Snow and mud are charged once, by
        // SurfaceExcessMinutes; folding them in here too would bill a blizzard twice.
        double multiplier = location.GetTravelHazardLevel();

        // Weather from location's zone
        var weather = location.Weather;
        if (weather.WindSpeedPct > 0.5)
            multiplier *= 1 + (weather.WindSpeedPct * 0.3 * location.WindFactor);
        if (weather.PrecipitationPct > 0.5)
            multiplier *= 1 + (weather.PrecipitationPct * 0.2);

        // Build ability context for Speed calculation
        // Speed ability handles: vitality, strength modulating encumbrance
        var context = new AbilityContext
        {
            EncumbrancePct = (inventory != null && inventory.MaxWeightKg > 0)
                ? inventory.CurrentWeightKg / inventory.MaxWeightKg
                : 0
        };

        // Speed is a rate (higher = faster), convert to time multiplier (higher = slower)
        double speed = actor.GetSpeed(context);
        double speedMultiplier = 1.0 / Math.Max(0.1, speed); // Floor to prevent infinity

        int baseTime = (int)Math.Ceiling(location.BaseTraversalMinutes * (1 + multiplier) * speedMultiplier);

        return baseTime;
    }

    /// <summary>
    /// Shortest a crossing can take, however many trail bonuses stack on it.
    /// </summary>
    public const int MinimumCrossingMinutes = 5;

    /// <summary>
    /// Get total traversal time from origin to destination (exit origin + enter destination).
    /// </summary>
    /// <param name="map">
    /// Supply it to include what lies between the two tiles - rivers, climbs, and how far
    /// the route has been beaten in. Everyone who walks the map should pass it; it is
    /// optional only for callers that have no map to hand.
    /// </param>
    public static int GetTraversalMinutes(Location origin, Location destination, Actor actor,
        Inventory? inventory = null, GameMap? map = null)
    {
        int exitTime = CalculateSegmentTime(origin, actor, inventory);
        int entryTime = CalculateSegmentTime(destination, actor, inventory);

        int edgeModifier = map != null
            ? map.GetEdgeTraversalModifier(map.GetPosition(origin), map.GetPosition(destination))
            : 0;

        double surface = SurfaceExcessMinutes(origin, exitTime) + SurfaceExcessMinutes(destination, entryTime);

        return Math.Max(MinimumCrossingMinutes,
            (int)Math.Ceiling(exitTime + entryTime + edgeModifier + surface));
    }

    /// <summary>
    /// Minutes today's ground adds to a segment costing <paramref name="segmentMinutes"/>.
    ///
    /// Added after the route's own bonus and worked out from the baseline segment, because a
    /// worn path is bare earth under the snow rather than a cleared lane through it. Packing
    /// a route so that it *is* faster in deep snow needs the route's own surface state, which
    /// does not exist yet.
    /// </summary>
    private static double SurfaceExcessMinutes(Location location, int segmentMinutes) =>
        segmentMinutes * (location.Surface.TraversalFactor - 1);

    /// <summary>
    /// What crossing to this destination actually costs, for the given actor right now -
    /// the same numbers <see cref="TravelRunner"/> uses to run the crossing, so a preview
    /// (the tile popup) can never show a time or risk that doesn't match what happens.
    /// QuickMinutes/CarefulMinutes and RiskLevel already reflect the actor's current
    /// capacities (injuries included), since they're built from the actor's live Speed.
    /// </summary>
    public static CrossingPreview PreviewCrossing(Location origin, Location destination, Actor actor,
        Weather weather, Inventory? inventory = null, GameMap? map = null)
    {
        int exitTime = CalculateSegmentTime(origin, actor, inventory);
        int entryTime = CalculateSegmentTime(destination, actor, inventory);
        int edgeModifier = map != null
            ? map.GetEdgeTraversalModifier(map.GetPosition(origin), map.GetPosition(destination))
            : 0;

        bool originHazardous = IsHazardousTerrain(origin);
        bool destHazardous = IsHazardousTerrain(destination);

        double surface = SurfaceExcessMinutes(origin, exitTime) + SurfaceExcessMinutes(destination, entryTime);

        int quickMinutes = Math.Max(MinimumCrossingMinutes,
            (int)Math.Ceiling(exitTime + entryTime + edgeModifier + surface));

        if (!originHazardous && !destHazardous)
            return new CrossingPreview(quickMinutes, quickMinutes, 0, false);

        int carefulExitTime = originHazardous ? (int)Math.Ceiling(exitTime * CarefulTravelMultiplier) : exitTime;
        int carefulEntryTime = destHazardous ? (int)Math.Ceiling(entryTime * CarefulTravelMultiplier) : entryTime;
        int carefulMinutes = Math.Max(MinimumCrossingMinutes,
            (int)Math.Ceiling(carefulExitTime + carefulEntryTime + edgeModifier + surface));

        double originRisk = originHazardous ? GetInjuryRisk(origin, actor, weather) : 0;
        double destRisk = destHazardous ? GetInjuryRisk(destination, actor, weather) : 0;

        return new CrossingPreview(quickMinutes, carefulMinutes, Math.Max(originRisk, destRisk), true);
    }
}

/// <summary>
/// A crossing's cost as the player would choose between paces. <see cref="RiskLevel"/> is
/// 0 when <see cref="IsHazardous"/> is false - the risk only applies to a quick crossing.
/// </summary>
public readonly record struct CrossingPreview(int QuickMinutes, int CarefulMinutes, double RiskLevel, bool IsHazardous);