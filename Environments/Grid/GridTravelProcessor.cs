using text_survival.Actors.Player;
using text_survival.Bodies;
using text_survival.Items;

namespace text_survival.Environments.Grid;

/// <summary>
/// Calculates travel time and hazards for single-tile grid movement.
/// Simplified from TravelProcessor - no pathfinding, just tile-by-tile movement.
/// </summary>
public static class GridTravelProcessor
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
    /// Calculate time in minutes to enter a tile.
    /// Takes into account terrain, weather, player state, and encumbrance.
    /// </summary>
    public static int GetTravelTimeMinutes(Tile tile, Player player, Weather weather, Inventory? inventory = null)
    {
        if (tile.TraversalMinutes == 0) return 1;  // Minimum 1 minute

        double multiplier = 1.0;

        // Terrain hazard slows travel
        multiplier += tile.TerrainHazardLevel * 0.5;

        // Weather effects
        if (weather.WindSpeed > 0.5)
            multiplier += weather.WindSpeed * 0.3 * tile.WindFactor;
        if (weather.Precipitation > 0.5)
            multiplier += weather.Precipitation * 0.2;

        // Encumbrance from inventory
        if (inventory != null && inventory.MaxWeightKg > 0)
        {
            double encumbrance = inventory.CurrentWeightKg / inventory.MaxWeightKg;
            if (encumbrance > 0.5)
                multiplier += encumbrance * 0.4;
        }

        // Player speed
        double speed = player.Speed;
        if (speed < 1.0)
            multiplier *= 1 + (1 - speed);
        else if (speed > 1.0)
            multiplier *= 1 / speed;

        int baseTime = (int)Math.Ceiling(tile.TraversalMinutes * multiplier);

        // Capacity impairments
        var capacities = player.GetCapacities();

        if (AbilityCalculator.IsBreathingImpaired(capacities.Breathing))
            baseTime = (int)(baseTime * 1.10);

        if (baseTime > 15 && AbilityCalculator.IsBloodPumpingImpaired(capacities.BloodPumping))
            baseTime = (int)(baseTime * 1.20);

        return Math.Max(1, baseTime);
    }

    /// <summary>
    /// Get travel time for careful (safe) traversal.
    /// </summary>
    public static int GetCarefulTravelTimeMinutes(Tile tile, Player player, Weather weather, Inventory? inventory = null)
    {
        int normalTime = GetTravelTimeMinutes(tile, player, weather, inventory);
        return (int)Math.Ceiling(normalTime * CarefulTravelMultiplier);
    }

    /// <summary>
    /// Check if a tile is hazardous enough to warrant speed choice.
    /// </summary>
    public static bool IsHazardousTerrain(Tile tile) =>
        tile.TerrainHazardLevel >= HazardousTerrainThreshold;

    /// <summary>
    /// Calculate injury risk for quick travel through hazardous terrain.
    /// Returns 0-0.5 probability of injury.
    /// </summary>
    public static double GetInjuryRisk(Tile tile, Player player, Weather weather)
    {
        double baseRisk = tile.TerrainHazardLevel;
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
    /// Get a description of why terrain is hazardous.
    /// </summary>
    public static string GetHazardDescription(Tile tile, Weather weather)
    {
        var reasons = new List<string>();

        // Terrain-based hazards
        if (tile.Terrain == TerrainType.Water)
            reasons.Add("slippery ice");
        else if (tile.Terrain == TerrainType.Marsh)
            reasons.Add("unstable frozen marsh");
        else if (tile.Terrain == TerrainType.Rock)
            reasons.Add("loose rocks");
        else if (tile.Terrain == TerrainType.Hills)
            reasons.Add("steep terrain");
        else if (tile.TerrainHazardLevel >= HazardousTerrainThreshold)
            reasons.Add("treacherous footing");

        // Weather-based hazards
        if (weather.CurrentCondition == Weather.WeatherCondition.Blizzard)
            reasons.Add("blizzard conditions");
        else if (weather.CurrentCondition == Weather.WeatherCondition.Stormy)
            reasons.Add("stormy weather");
        else if (weather.Precipitation > 0.5)
            reasons.Add("heavy snowfall");

        if (reasons.Count == 0)
            return "hazardous terrain";

        return string.Join(" and ", reasons);
    }

    /// <summary>
    /// Validate that a move from one tile to another is allowed.
    /// Returns null if valid, or an error message if invalid.
    /// </summary>
    public static string? ValidateMove(Tile fromTile, Tile toTile, TileGrid grid)
    {
        if (toTile == null)
            return "Cannot move outside the map.";

        if (!grid.IsAdjacent(fromTile, toTile))
            return "Can only move to adjacent tiles.";

        if (!toTile.IsPassable)
            return $"Cannot traverse {toTile.Terrain.ToString().ToLower()} terrain.";

        return null;  // Valid move
    }
}
