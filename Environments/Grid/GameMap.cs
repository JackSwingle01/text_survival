using text_survival.Actions;

namespace text_survival.Environments.Grid;

/// <summary>
/// Serializable location data: position + location.
/// Uses class with settable properties for JSON reference preservation compatibility.
/// </summary>
public class MapLocationData
{
    public int X { get; set; }
    public int Y { get; set; }
    public Location Location { get; set; } = null!;

    public MapLocationData() { }

    public MapLocationData(int x, int y, Location location)
    {
        X = x;
        Y = y;
        Location = location;
    }
}

/// <summary>
/// The game world map. A simple matrix of Locations with a current position.
/// Owns all spatial relationships - locations don't know their positions.
/// </summary>
public class GameMap
{
    // === Location Storage ===
    private Location?[,] _locations;

    /// <summary>
    /// Reverse lookup: Location ID -> Position. For finding where a location is.
    /// </summary>
    private readonly Dictionary<Guid, GridPosition> _locationIndex = new();

    // === Edge Storage ===

    /// <summary>
    /// Edge features between tiles. Key is (canonical position, direction).
    /// Multiple edges can exist between same tiles (river + game trail).
    /// </summary>
    private Dictionary<(GridPosition, Direction), List<TileEdge>> _edges = new();

    public int Width { get; private set; }
    public int Height { get; private set; }

    /// <summary>
    /// Current player position on the map.
    /// </summary>
    public GridPosition CurrentPosition { get; set; }

    /// <summary>
    /// Weather reference (not serialized - restored after load).
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public Weather Weather { get; set; } = null!;

    /// <summary>
    /// Transient flag: true if the last visibility update revealed new named locations.
    /// Used to weight discovery-related events. Reset after event check.
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public bool RevealedNewLocations { get; internal set; }

    // === Core Operations ===

    /// <summary>
    /// Where the player currently is.
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public Location CurrentLocation => _locations[CurrentPosition.X, CurrentPosition.Y]!;

    /// <summary>
    /// Adjacent passable locations the player can travel to from current position.
    /// Filters out edge-blocked paths based on current season.
    /// </summary>
    public IReadOnlyList<Location> GetTravelOptions()
    {
        var season = Weather?.CurrentSeason ?? Weather.Season.Winter;
        var options = new List<Location>();
        foreach (var neighborPos in CurrentPosition.GetCardinalNeighbors())
        {
            // Skip edge-blocked paths
            if (IsEdgeBlocked(CurrentPosition, neighborPos, season))
                continue;

            var location = GetLocationAt(neighborPos);
            if (location != null && location.IsPassable)
                options.Add(location);
        }
        return options;
    }

    /// <summary>
    /// Get travel options from a specific location.
    /// Filters out edge-blocked paths based on current season.
    /// </summary>
    public IReadOnlyList<Location> GetTravelOptionsFrom(Location from)
    {
        var position = GetPosition(from);
        if (!position.HasValue) return [];

        var season = Weather?.CurrentSeason ?? Weather.Season.Winter;
        var options = new List<Location>();
        foreach (var neighborPos in position.Value.GetCardinalNeighbors())
        {
            // Skip edge-blocked paths
            if (IsEdgeBlocked(position.Value, neighborPos, season))
                continue;

            var location = GetLocationAt(neighborPos);
            if (location != null && location.IsPassable)
                options.Add(location);
        }
        return options;
    }

    /// <summary>
    /// All locations currently visible to the player.
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public IReadOnlyList<Location> VisibleLocations
    {
        get
        {
            var visible = new List<Location>();
            for (int x = 0; x < Width; x++)
            {
                for (int y = 0; y < Height; y++)
                {
                    var loc = _locations[x, y];
                    if (loc != null && loc.Visibility == TileVisibility.Visible)
                        visible.Add(loc);
                }
            }
            return visible;
        }
    }

    /// <summary>
    /// All named locations (locations with features) that are visible.
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public IReadOnlyList<Location> VisibleNamedLocations =>
        VisibleLocations.Where(l => !l.IsTerrainOnly).ToList();

    /// <summary>
    /// Move player to a location. Updates visibility.
    /// </summary>
    public void MoveTo(Location destination)
    {
        var position = GetPosition(destination);
        if (!position.HasValue)
            throw new ArgumentException($"Location {destination.Name} is not on this map");

        CurrentPosition = position.Value;
        destination.MarkExplored();
        UpdateVisibility();
    }

    // === UI/Rendering (grid-specific) ===

    /// <summary>
    /// Get location at grid position. Returns null if out of bounds or empty.
    /// </summary>
    public Location? GetLocationAt(int x, int y) =>
        IsInBounds(x, y) ? _locations[x, y] : null;

    /// <summary>
    /// Get location at grid position.
    /// </summary>
    public Location? GetLocationAt(GridPosition pos) =>
        GetLocationAt(pos.X, pos.Y);

    /// <summary>
    /// Get the position of a location on this map.
    /// </summary>
    public GridPosition? GetPosition(Location location) =>
        _locationIndex.TryGetValue(location.Id, out var pos) ? pos : null;

    /// <summary>
    /// Check if a location is on this map.
    /// </summary>
    public bool Contains(Location location) =>
        _locationIndex.ContainsKey(location.Id);

    /// <summary>
    /// Check if position is adjacent to current position and passable.
    /// </summary>
    public bool CanMoveTo(int x, int y)
    {
        if (!IsInBounds(x, y)) return false;
        if (!CurrentPosition.IsAdjacentTo(new GridPosition(x, y))) return false;
        var loc = _locations[x, y];
        return loc != null && loc.IsPassable;
    }

    // === Construction ===

    /// <summary>
    /// Create a new empty map of the specified size.
    /// </summary>
    public GameMap(int width, int height)
    {
        Width = width;
        Height = height;
        _locations = new Location?[width, height];
    }

    /// <summary>
    /// Parameterless constructor for JSON deserialization.
    /// </summary>
    public GameMap() : this(0, 0)
    {
    }

    /// <summary>
    /// Check if coordinates are within map bounds.
    /// </summary>
    public bool IsInBounds(int x, int y) =>
        x >= 0 && x < Width && y >= 0 && y < Height;

    /// <summary>
    /// Place a location at a position.
    /// </summary>
    public void SetLocation(int x, int y, Location location)
    {
        if (!IsInBounds(x, y)) return;

        // Remove old location from index
        var oldLocation = _locations[x, y];
        if (oldLocation != null)
            _locationIndex.Remove(oldLocation.Id);

        _locations[x, y] = location;
        _locationIndex[location.Id] = new GridPosition(x, y);
    }

    // === Visibility ===

    /// <summary>
    /// Update visibility based on current position.
    /// </summary>
    public void UpdateVisibility()
    {
        int sightRange = GetSightRange(CurrentLocation);

        // Reset reveal flag
        RevealedNewLocations = false;

        // First, downgrade all visible locations to explored
        for (int x = 0; x < Width; x++)
        {
            for (int y = 0; y < Height; y++)
            {
                var loc = _locations[x, y];
                if (loc != null && loc.Visibility == TileVisibility.Visible)
                    loc.Visibility = TileVisibility.Explored;
            }
        }

        // Then mark locations in range as visible
        foreach (var pos in CurrentPosition.GetPositionsInRange(sightRange))
        {
            var loc = GetLocationAt(pos);
            if (loc != null)
            {
                bool wasHidden = loc.Visibility == TileVisibility.Unexplored;
                loc.Visibility = TileVisibility.Visible;

                // Track if we just revealed a new named location
                if (wasHidden && !loc.IsTerrainOnly && !loc.Explored)
                    RevealedNewLocations = true;
            }
        }
    }

    /// <summary>
    /// Calculate sight range based on location's visibility factor.
    /// </summary>
    public static int GetSightRange(Location location)
    {
        double visibility = location.VisibilityFactor;

        if (visibility < 0.5)
            return 1;  // Bordering locations (was 0)
        if (visibility < 1.0)
            return 2;  // 2 tile radius (was 1)
        if (visibility < 1.5)
            return 3;  // 3 tile radius (was 2)
        if (visibility < 2.0)
            return 4;  // 4 tile radius (was 3)
        return 5;      // Vantage points (was 4)
    }

    // === Serialization ===

    /// <summary>
    /// Serializable location data. Set during serialization, used during deserialization.
    /// </summary>
    public List<MapLocationData> LocationData
    {
        get
        {
            var data = new List<MapLocationData>();
            for (int x = 0; x < Width; x++)
            {
                for (int y = 0; y < Height; y++)
                {
                    var loc = _locations[x, y];
                    if (loc != null)
                        data.Add(new MapLocationData(x, y, loc));
                }
            }
            return data;
        }
        set
        {
            if (value == null || value.Count == 0) return;

            // Determine dimensions from the data if array not yet sized
            if (_locations.GetLength(0) == 0 || _locations.GetLength(1) == 0)
            {
                int maxX = value.Max(ld => ld.X) + 1;
                int maxY = value.Max(ld => ld.Y) + 1;
                // Use Width/Height if set (they should be), otherwise infer from data
                int width = Width > 0 ? Width : maxX;
                int height = Height > 0 ? Height : maxY;
                _locations = new Location?[width, height];
                Width = width;
                Height = height;
            }

            // Rebuild map from serialized data
            foreach (var ld in value)
            {
                if (IsInBounds(ld.X, ld.Y))
                {
                    _locations[ld.X, ld.Y] = ld.Location;
                    _locationIndex[ld.Location.Id] = new GridPosition(ld.X, ld.Y);
                }
            }
        }
    }

    /// <summary>
    /// Initialize dimensions after deserialization.
    /// </summary>
    public void InitializeDimensions(int width, int height)
    {
        Width = width;
        Height = height;
        _locations = new Location?[width, height];
    }

    // === Queries ===

    /// <summary>
    /// Get all named locations (non-terrain-only) on the map.
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public IEnumerable<Location> NamedLocations
    {
        get
        {
            for (int x = 0; x < Width; x++)
            {
                for (int y = 0; y < Height; y++)
                {
                    var loc = _locations[x, y];
                    if (loc != null && !loc.IsTerrainOnly)
                        yield return loc;
                }
            }
        }
    }

    /// <summary>
    /// Check if there are unexplored named locations visible.
    /// </summary>
    public bool HasUnexploredVisibleLocations =>
        VisibleNamedLocations.Any(l => !l.Explored);

    /// <summary>
    /// Get count of unexplored visible named locations.
    /// </summary>
    public int UnexploredVisibleCount =>
        VisibleNamedLocations.Count(l => !l.Explored);

    /// <summary>
    /// Reveal a random unexplored named location within visible range.
    /// Returns the location if found, null otherwise.
    /// </summary>
    public Location? RevealRandomLocation()
    {
        var unexplored = VisibleNamedLocations.Where(l => !l.Explored).ToList();
        if (unexplored.Count == 0) return null;

        var location = unexplored[Random.Shared.Next(unexplored.Count)];
        location.MarkExplored();
        return location;
    }

    // === Edge Operations ===

    /// <summary>
    /// Get all edges when moving from one position to another.
    /// Filters out one-way edges when traveling the reverse direction.
    /// </summary>
    public IReadOnlyList<TileEdge> GetEdgesBetween(GridPosition from, GridPosition to)
    {
        var (canonical, dir, reversed) = Canonicalize(from, to);

        if (!_edges.TryGetValue((canonical, dir), out var edges))
            return [];

        // Filter out one-way edges if traveling reverse direction
        if (reversed)
            return edges.Where(e => e.Bidirectional).ToList();

        return edges;
    }

    /// <summary>
    /// Check if passage is blocked between two positions.
    /// </summary>
    public bool IsEdgeBlocked(GridPosition from, GridPosition to, Weather.Season season)
    {
        var edges = GetEdgesBetween(from, to);
        return edges.Any(e => e.IsBlockedIn(season));
    }

    /// <summary>
    /// Get total traversal modifier for crossing between positions.
    /// </summary>
    public int GetEdgeTraversalModifier(GridPosition from, GridPosition to)
    {
        return GetEdgesBetween(from, to).Sum(e => e.TraversalModifierMinutes);
    }

    /// <summary>
    /// Check for specific edge types (for fishing, hunting, events).
    /// </summary>
    public bool HasEdgeType(GridPosition a, GridPosition b, EdgeType type) =>
        GetEdgesBetween(a, b).Any(e => e.Type == type);

    /// <summary>
    /// Add an edge between two adjacent positions.
    /// </summary>
    public void AddEdge(GridPosition a, GridPosition b, TileEdge edge)
    {
        var (canonical, dir, _) = Canonicalize(a, b);
        var key = (canonical, dir);

        if (!_edges.ContainsKey(key))
            _edges[key] = new List<TileEdge>();

        // Replace existing edge of same type (trail marker → cut trail)
        _edges[key].RemoveAll(e => e.Type == edge.Type);
        _edges[key].Add(edge);
    }

    /// <summary>
    /// Remove edge of specific type.
    /// </summary>
    public void RemoveEdge(GridPosition a, GridPosition b, EdgeType type)
    {
        var (canonical, dir, _) = Canonicalize(a, b);
        if (_edges.TryGetValue((canonical, dir), out var edges))
            edges.RemoveAll(e => e.Type == type);
    }

    /// <summary>
    /// Try to trigger an edge event when crossing between positions.
    /// Returns the first event that triggers, or null if none.
    /// </summary>
    public GameEvent? TryTriggerEdgeEvent(GridPosition from, GridPosition to, GameContext ctx)
    {
        var edges = GetEdgesBetween(from, to);
        foreach (var edge in edges)
        {
            var evt = edge.TryTriggerEvent(ctx);
            if (evt != null) return evt;
        }
        return null;
    }

    /// <summary>
    /// Canonicalize edge storage. Store from top-left position.
    /// Returns (canonical position, direction, whether input was reversed).
    /// </summary>
    private static (GridPosition pos, Direction dir, bool reversed) Canonicalize(GridPosition a, GridPosition b)
    {
        // Store North→South edges from the northern tile
        if (a.Y < b.Y) return (a, Direction.South, false);
        if (b.Y < a.Y) return (b, Direction.South, true);

        // Same row: store West→East from western tile
        if (a.X < b.X) return (a, Direction.East, false);
        return (b, Direction.East, true);
    }

    // === Edge Serialization ===

    /// <summary>
    /// Serializable edge data.
    /// </summary>
    public class EdgeData
    {
        public int X { get; set; }
        public int Y { get; set; }
        public Direction Direction { get; set; }
        public TileEdge Edge { get; set; } = null!;

        public EdgeData() { }

        public EdgeData(int x, int y, Direction direction, TileEdge edge)
        {
            X = x;
            Y = y;
            Direction = direction;
            Edge = edge;
        }
    }

    /// <summary>
    /// Serializable edge data property.
    /// </summary>
    public List<EdgeData> Edges
    {
        get => _edges
            .SelectMany(kvp => kvp.Value.Select(edge =>
                new EdgeData(kvp.Key.Item1.X, kvp.Key.Item1.Y, kvp.Key.Item2, edge)))
            .ToList();
        set
        {
            _edges.Clear();
            foreach (var data in value ?? [])
            {
                var key = (new GridPosition(data.X, data.Y), data.Direction);
                if (!_edges.ContainsKey(key))
                    _edges[key] = new List<TileEdge>();
                _edges[key].Add(data.Edge);
            }
        }
    }
}
