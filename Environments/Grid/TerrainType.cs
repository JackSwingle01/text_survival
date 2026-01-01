namespace text_survival.Environments.Grid;

/// <summary>
/// Types of terrain that can exist on tiles.
/// Each terrain type has base properties for traversal, hazard, wind exposure, etc.
/// </summary>
public enum TerrainType
{
    // Traversable terrain
    Forest,      // Dense trees - slow, wind-protected
    Clearing,    // Open area in forest - faster, some exposure
    Plain,       // Open snowy plain - fast, very exposed
    Hills,       // Hilly terrain - slow, exposed
    Water,       // Frozen water - moderate, hazardous (ice)
    Marsh,       // Frozen marsh - very slow, hazardous
    Rock,        // Rocky terrain - slow, stone resources

    // Impassable terrain
    Mountain,    // Cannot traverse - blocks path
    DeepWater    // Cannot traverse - open/deep water
}

/// <summary>
/// Extension methods providing base terrain properties.
/// Named locations on tiles can override these values.
/// </summary>
public static class TerrainTypeExtensions
{
    /// <summary>
    /// Base traversal time in minutes to enter this terrain type.
    /// Based on ~1/4 mile across per location, ~24 min per mile on flat terrain = 3 min radius.
    /// Higher = slower travel.
    /// </summary>
    public static int BaseTraversalMinutes(this TerrainType terrain) => terrain switch
    {
        TerrainType.Plain => 3,       // Baseline - flat terrain, fast
        TerrainType.Clearing => 3,    // Slight undergrowth
        TerrainType.Water => 4,       // Slippery ice but flat
        TerrainType.Forest => 4,      // Slower through trees
        TerrainType.Rock => 5,        // Careful footing over rocks
        TerrainType.Hills => 5,       // Elevation changes
        TerrainType.Marsh => 6,       // Very slow, treacherous
        TerrainType.Mountain => int.MaxValue,
        TerrainType.DeepWater => int.MaxValue,
        _ => 4
    };

    /// <summary>
    /// Base terrain hazard level (0-1).
    /// Higher = more likely to slip/fall/get injured.
    /// </summary>
    public static double BaseHazardLevel(this TerrainType terrain) => terrain switch
    {
        TerrainType.Plain => 0.0,
        TerrainType.Clearing => 0.05,
        TerrainType.Forest => 0.1,
        TerrainType.Hills => 0.25,
        TerrainType.Rock => 0.3,
        TerrainType.Marsh => 0.35,
        TerrainType.Water => 0.4,  // Ice is slippery
        _ => 0.0
    };

    /// <summary>
    /// Wind exposure factor (0-2).
    /// 0 = no wind (sheltered), 1 = normal, 2 = amplified (ridge/peak).
    /// </summary>
    public static double BaseWindFactor(this TerrainType terrain) => terrain switch
    {
        TerrainType.Forest => 0.3,
        TerrainType.Clearing => 0.7,
        TerrainType.Marsh => 0.8,
        TerrainType.Plain => 1.2,
        TerrainType.Water => 1.3,  // Flat ice, exposed
        TerrainType.Hills => 1.4,
        TerrainType.Rock => 1.0,
        TerrainType.Mountain => 1.8,
        _ => 1.0
    };

    /// <summary>
    /// Overhead cover level (0-1).
    /// Higher = more protection from precipitation.
    /// </summary>
    public static double BaseOverheadCover(this TerrainType terrain) => terrain switch
    {
        TerrainType.Forest => 0.5,
        TerrainType.Clearing => 0.1,
        TerrainType.Rock => 0.1,  // Some overhangs possible
        _ => 0.0
    };

    /// <summary>
    /// Visibility factor (0-2).
    /// Lower = harder to see, higher = better sightlines.
    /// </summary>
    public static double BaseVisibility(this TerrainType terrain) => terrain switch
    {
        TerrainType.Forest => 0.5,
        TerrainType.Marsh => 0.7,
        TerrainType.Clearing => 1.0,
        TerrainType.Plain => 1.2,
        TerrainType.Rock => 0.8,
        TerrainType.Water => 1.3,  // Flat, open
        TerrainType.Hills => 1.5,  // Vantage points
        TerrainType.Mountain => 0.5,  // Blocked views
        _ => 1.0
    };

    /// <summary>
    /// Whether this terrain can be entered.
    /// </summary>
    public static bool IsPassable(this TerrainType terrain) =>
        terrain != TerrainType.Mountain && terrain != TerrainType.DeepWater;

    /// <summary>
    /// Whether this terrain is considered dark (requires torch/fire to work).
    /// </summary>
    public static bool IsDark(this TerrainType terrain) => false;  // Caves are locations, not terrain

    /// <summary>
    /// Display color for placeholder rendering (hex format).
    /// </summary>
    public static string PlaceholderColor(this TerrainType terrain) => terrain switch
    {
        TerrainType.Forest => "#1a3d2e",       // Dark green
        TerrainType.Clearing => "#4a7c59",     // Lighter green
        TerrainType.Plain => "#e8e8e8",        // Snow white
        TerrainType.Hills => "#8b7355",        // Brown
        TerrainType.Water => "#a8d4e6",        // Light ice blue
        TerrainType.Marsh => "#5a6b4f",        // Murky green
        TerrainType.Rock => "#6b6b6b",         // Gray
        TerrainType.Mountain => "#3d3d3d",     // Dark gray
        TerrainType.DeepWater => "#4a90a4",    // Darker blue
        _ => "#888888"
    };
}
