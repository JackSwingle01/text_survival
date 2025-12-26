using text_survival.Actors.Player;
using text_survival.Bodies;
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
    public static double GetInjuryRisk(Location location, Player player, Weather weather)
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

        // Player capacity modifier - impaired movement increases risk
        var capacities = player.GetCapacities();
        double capacityMod = (1 - capacities.Moving) * 0.3;

        return Math.Min(MaxInjuryRisk, baseRisk + weatherMod + capacityMod);
    }

    /// <summary>
    /// Get travel time for careful (safe) traversal.
    /// </summary>
    public static int GetCarefulTraversalMinutes(Location origin, Location destination, Player player, Inventory? inventory = null)
    {
        int normalTime = GetTraversalMinutes(origin, destination, player, inventory);
        return (int)Math.Ceiling(normalTime * CarefulTravelMultiplier);
    }

    /// <summary>
    /// Check if terrain is hazardous enough to warrant speed choice.
    /// </summary>
    public static bool IsHazardousTerrain(Location location) =>
        location.GetEffectiveTerrainHazard() >= HazardousTerrainThreshold;

    /// <summary>
    /// Check if a hazard value is hazardous enough to warrant speed choice.
    /// </summary>
    public static bool IsHazardousTerrain(double hazardValue) =>
        hazardValue >= HazardousTerrainThreshold;

    /// <summary>
    /// Calculate traversal time for a single segment (exiting or entering a location).
    /// </summary>
    public static int CalculateSegmentTime(Location location, Player player, Inventory? inventory = null)
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

        double speed = player.Speed;
        if (speed < 1.0)
            multiplier *= 1 + (1 - speed);  // Slower = longer time
        else if (speed > 1.0)
            multiplier *= 1 / speed;  // Faster = shorter time

        int baseTime = (int)Math.Ceiling(location.BaseTraversalMinutes * (1 + multiplier));

        var capacities = player.GetCapacities();

        // Breathing impairment adds +10% time for all journeys
        // Labored breathing slows pace
        if (AbilityCalculator.IsBreathingImpaired(capacities.Breathing))
        {
            baseTime = (int)(baseTime * 1.10);
        }

        // BloodPumping impairment adds +20% time for long journeys (> 30 min)
        // Weak circulation makes sustained exertion harder
        if (baseTime > 30)
        {
            if (AbilityCalculator.IsBloodPumpingImpaired(capacities.BloodPumping))
            {
                baseTime = (int)(baseTime * 1.20);
            }
        }

        return baseTime;
    }

    /// <summary>
    /// Get total traversal time from origin to destination (exit origin + enter destination).
    /// </summary>
    public static int GetTraversalMinutes(Location origin, Location destination, Player player, Inventory? inventory = null)
    {
        int exitTime = CalculateSegmentTime(origin, player, inventory);
        int entryTime = CalculateSegmentTime(destination, player, inventory);
        return exitTime + entryTime;
    }

    public static int GetPathMinutes(List<Location> path, Player player, Inventory? inventory = null)
    {
        if (path.Count <= 1) return 0;

        int totalTime = 0;
        for (int i = 1; i < path.Count; i++)
        {
            totalTime += GetTraversalMinutes(path[i - 1], path[i], player, inventory);
        }
        return totalTime;
    }

    public static List<Location>? FindPath(Location start, Location target, Actions.GameContext ctx)
    {
        if (start == target) return [start];

        var visited = new HashSet<Location>();
        var queue = new Queue<List<Location>>();
        queue.Enqueue([start]);

        while (queue.Count > 0)
        {
            var path = queue.Dequeue();
            var current = path.Last();

            if (current == target)
                return path;

            if (visited.Contains(current))
                continue;

            visited.Add(current);

            foreach (var connection in current.GetConnections(ctx))
            {
                if (connection.Explored &&
                    !visited.Contains(connection))
                {
                    queue.Enqueue([.. path, connection]);
                }
            }
        }

        return null;
    }

    public static List<Location> BuildRoundTripPath(List<Location> outboundPath)
    {
        // outbound: [Camp, TrailA, Dest]
        // return portion: [TrailA, Camp]
        // full: [Camp, TrailA, Dest, TrailA, Camp]

        var returnPath = outboundPath.Take(outboundPath.Count - 1).Reverse();
        return outboundPath.Concat(returnPath).ToList();
    }

    public static List<Location>? BuildRoundTripPath(Location origin, Location destination, Actions.GameContext ctx)
    {
        var outboundPath = FindPath(origin, destination, ctx);
        if (outboundPath is null) return null;
        return BuildRoundTripPath(outboundPath);
    }

}