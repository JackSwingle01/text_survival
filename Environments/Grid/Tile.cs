using text_survival.Environments.Features;

namespace text_survival.Environments.Grid;

/// <summary>
/// Visibility state for fog of war.
/// </summary>
public enum TileVisibility
{
    /// <summary>Never visited - completely hidden.</summary>
    Unexplored,
    /// <summary>Visited before - shows terrain type but not current features.</summary>
    Explored,
    /// <summary>Currently in sight range - shows full details.</summary>
    Visible
}

/// <summary>
/// A single tile on the game grid.
/// Can either be generic terrain or contain a named Location with features.
/// </summary>
public class Tile
{
    // Position //
    public GridPosition Position { get; init; }
    public int X => Position.X;
    public int Y => Position.Y;

    // Terrain //
    public TerrainType Terrain { get; init; }

    /// <summary>
    /// Optional named location at this tile.
    /// If present, features and some properties come from the location.
    /// If null, this is generic wilderness terrain.
    /// </summary>
    public Location? NamedLocation { get; set; }

    /// <summary>
    /// Whether this tile has a named location (vs generic terrain).
    /// </summary>
    public bool HasNamedLocation => NamedLocation != null;

    // Visibility (fog of war) //
    public TileVisibility Visibility { get; set; } = TileVisibility.Unexplored;

    public bool IsExplored => Visibility != TileVisibility.Unexplored;
    public bool IsVisible => Visibility == TileVisibility.Visible;

    // Derived Properties - delegate to NamedLocation if present, else terrain defaults //

    /// <summary>
    /// Display name for this tile.
    /// </summary>
    public string Name => NamedLocation?.Name ?? Terrain.ToString();

    /// <summary>
    /// Tags for this tile (from named location, or empty for terrain).
    /// </summary>
    public string Tags => NamedLocation?.Tags ?? "";

    /// <summary>
    /// Time in minutes to enter this tile.
    /// </summary>
    public int TraversalMinutes =>
        NamedLocation?.BaseTraversalMinutes ?? Terrain.BaseTraversalMinutes();

    /// <summary>
    /// Terrain hazard level (0-1). Higher = more injury risk.
    /// </summary>
    public double TerrainHazardLevel =>
        NamedLocation?.GetEffectiveTerrainHazard() ?? Terrain.BaseHazardLevel();

    /// <summary>
    /// Wind exposure factor.
    /// </summary>
    public double WindFactor =>
        NamedLocation?.WindFactor ?? Terrain.BaseWindFactor();

    /// <summary>
    /// Overhead cover level (0-1).
    /// </summary>
    public double OverheadCoverLevel =>
        NamedLocation?.OverheadCoverLevel ?? Terrain.BaseOverheadCover();

    /// <summary>
    /// Visibility factor for sight range.
    /// </summary>
    public double VisibilityFactor =>
        NamedLocation?.VisibilityFactor ?? Terrain.BaseVisibility();

    /// <summary>
    /// Whether this tile can be entered.
    /// </summary>
    public bool IsPassable => Terrain.IsPassable();

    /// <summary>
    /// Whether this tile requires light to work (dark caves, etc).
    /// </summary>
    public bool IsDark => NamedLocation?.IsDark ?? false;

    // Features - come from named location only //

    /// <summary>
    /// Features at this tile. Empty for generic terrain.
    /// </summary>
    public List<LocationFeature> Features =>
        NamedLocation?.Features ?? [];

    /// <summary>
    /// Get a specific feature type.
    /// </summary>
    public T? GetFeature<T>() where T : LocationFeature =>
        NamedLocation?.GetFeature<T>();

    /// <summary>
    /// Check if this tile has a specific feature type.
    /// </summary>
    public bool HasFeature<T>() where T : LocationFeature =>
        NamedLocation?.HasFeature<T>() ?? false;

    /// <summary>
    /// Check if this tile has an active fire.
    /// </summary>
    public bool HasActiveHeatSource =>
        NamedLocation?.HasActiveHeatSource() ?? false;

    /// <summary>
    /// Check if this tile has a light source.
    /// </summary>
    public bool HasLight =>
        NamedLocation?.HasLight() ?? false;

    // Construction //

    public Tile(GridPosition position, TerrainType terrain)
    {
        Position = position;
        Terrain = terrain;
    }

    public Tile(int x, int y, TerrainType terrain)
        : this(new GridPosition(x, y), terrain)
    {
    }

    // Parameterless constructor for JSON deserialization
    public Tile() : this(new GridPosition(0, 0), TerrainType.Plain)
    {
    }

    /// <summary>
    /// Mark this tile as explored (player has visited).
    /// </summary>
    public void MarkExplored()
    {
        if (Visibility == TileVisibility.Unexplored)
            Visibility = TileVisibility.Explored;

        // Also mark the named location as explored if present
        if (NamedLocation != null)
            NamedLocation.MarkExplored();
    }

    /// <summary>
    /// Set visibility state for fog of war updates.
    /// </summary>
    public void SetVisibility(TileVisibility visibility)
    {
        // Can't "un-explore" - only upgrade visibility or keep explored
        if (visibility == TileVisibility.Unexplored && Visibility != TileVisibility.Unexplored)
            Visibility = TileVisibility.Explored;
        else
            Visibility = visibility;
    }

    public override string ToString() => $"{Name} at {Position}";
}
