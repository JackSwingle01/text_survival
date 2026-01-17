using text_survival.Actions;
using text_survival.Actors;

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
    private Location?[,] _locations;
    private readonly Dictionary<Guid, GridPosition> _locationIndex = new();
    private Dictionary<(GridPosition, Direction), List<TileEdge>> _edges = new();

    public int Width { get; set; }
    public int Height { get; set; }
    public GridPosition CurrentPosition { get; set; }

    [System.Text.Json.Serialization.JsonIgnore]
    public Weather Weather { get; set; } = null!;

    [System.Text.Json.Serialization.JsonIgnore]
    public bool RevealedNewLocations { get; internal set; }

    [System.Text.Json.Serialization.JsonIgnore]
    public Location CurrentLocation => _locations[CurrentPosition.X, CurrentPosition.Y]!;

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

    public IReadOnlyList<Location> GetTravelOptionsFrom(Location from)
    {
        var position = GetPosition(from);

        var season = Weather?.CurrentSeason ?? Weather.Season.Winter;
        var options = new List<Location>();
        foreach (var neighborPos in position.GetCardinalNeighbors())
        {
            // Skip edge-blocked paths
            if (IsEdgeBlocked(position, neighborPos, season))
                continue;

            var location = GetLocationAt(neighborPos);
            if (location != null && location.IsPassable)
                options.Add(location);
        }
        return options;
    }

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

    [System.Text.Json.Serialization.JsonIgnore]
    public IReadOnlyList<Location> VisibleNamedLocations =>
        VisibleLocations.Where(l => !l.IsTerrainOnly).ToList();

    public void MoveTo(Location destination, IMovable? mover = null)
    {
        var position = GetPosition(destination);

        CurrentPosition = position;
        destination.MarkExplored();
        UpdateVisibility();

        if (mover != null)
            mover.CurrentLocation = destination;
    }

    public Location? GetNextInPath(Location from, Location to)
    {
        // greedy pathfinding for now - will break if no direct path
        if (from == to) throw new Exception("Can't get path because you're already there!");

        var fromPos = GetPosition(from);
        var toPos = GetPosition(to);

        int dx = Math.Sign(toPos.X - fromPos.X);
        int dy = Math.Sign(toPos.Y - fromPos.Y);

        Location? locX = null, locY = null;
        if (dx != 0)
        {
            locX = GetLocationAt(new GridPosition(fromPos.X + dx, fromPos.Y));
        }
        if (dy != 0)
        {
            locY = GetLocationAt(new GridPosition(fromPos.X, fromPos.Y + dy));
        }
        if (locX != null && locX.IsPassable)
        {
            if (locY != null && locY.IsPassable)
            {
                if (locX.BaseTraversalMinutes > locY.BaseTraversalMinutes)
                    return locY;
            }
            return locX;
        }
        else if (locY != null && locY.IsPassable)
        {
            return locY;
        }
        Console.WriteLine("WARNING: pathfinding failed. Time to implement a better algorithm");
        return null;
    }

    public Location? GetLocationAt(int x, int y) =>
        IsInBounds(x, y) ? _locations[x, y] : null;

    public Location? GetLocationAt(GridPosition pos) =>
        GetLocationAt(pos.X, pos.Y);

    public GridPosition GetPosition(Location location) =>
        _locationIndex.TryGetValue(location.Id, out var pos) ? pos : throw new Exception("Location doesn't exist!");

    public int DistanceBetween(Location a, Location b)
    {
        return GetPosition(a).ManhattanDistance(GetPosition(b));
    }

    public bool Contains(Location location) =>
        _locationIndex.ContainsKey(location.Id);

    public bool CanMoveTo(int x, int y)
    {
        if (!IsInBounds(x, y)) return false;
        if (!CurrentPosition.IsAdjacentTo(new GridPosition(x, y))) return false;
        var loc = _locations[x, y];
        return loc != null && loc.IsPassable;
    }

    public GameMap(int width, int height)
    {
        Width = width;
        Height = height;
        _locations = new Location?[width, height];
    }

    public GameMap() : this(0, 0) { }

    public bool IsInBounds(int x, int y) =>
        x >= 0 && x < Width && y >= 0 && y < Height;

    public bool IsValidPosition(int x, int y) => IsInBounds(x, y);

    public TileVisibility GetVisibility(int x, int y)
    {
        if (!IsInBounds(x, y)) return TileVisibility.Unexplored;
        var loc = _locations[x, y];
        return loc?.Visibility ?? TileVisibility.Unexplored;
    }

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

    public void UpdateVisibility(double sightCapacity = 1.0)
    {
        int sightRange = GetSightRange(CurrentLocation, sightCapacity);

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

    public static int GetSightRange(Location location, double sightCapacity = 1.0)
    {
        // Combine location visibility with player sight capacity
        double effectiveVisibility = location.VisibilityFactor * sightCapacity;

        if (effectiveVisibility < 0.25)
            return 0;  // Nearly blind - only current tile
        if (effectiveVisibility < 0.5)
            return 1;  // Bordering locations
        if (effectiveVisibility < 1.0)
            return 2;  // 2 tile radius
        if (effectiveVisibility < 1.5)
            return 3;  // 3 tile radius
        if (effectiveVisibility < 2.0)
            return 4;  // 4 tile radius
        return 5;      // Vantage points
    }

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

    public bool HasUnexploredVisibleLocations =>
        VisibleNamedLocations.Any(l => !l.Explored);

    public int UnexploredVisibleCount =>
        VisibleNamedLocations.Count(l => !l.Explored);

    public Location? RevealRandomLocation()
    {
        var unexplored = VisibleNamedLocations.Where(l => !l.Explored).ToList();
        if (unexplored.Count == 0) return null;

        var location = unexplored[Random.Shared.Next(unexplored.Count)];
        location.MarkExplored();
        return location;
    }

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

    public bool IsEdgeBlocked(GridPosition from, GridPosition to, Weather.Season season)
    {
        var edges = GetEdgesBetween(from, to);
        return edges.Any(e => e.IsBlockedIn(season));
    }

    public int GetEdgeTraversalModifier(GridPosition from, GridPosition to)
    {
        return GetEdgesBetween(from, to).Sum(e => e.TraversalModifierMinutes);
    }

    public bool HasEdgeType(GridPosition a, GridPosition b, EdgeType type) =>
        GetEdgesBetween(a, b).Any(e => e.Type == type);

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

    public void RemoveEdge(GridPosition a, GridPosition b, EdgeType type)
    {
        var (canonical, dir, _) = Canonicalize(a, b);
        if (_edges.TryGetValue((canonical, dir), out var edges))
            edges.RemoveAll(e => e.Type == type);
    }

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

    private static (GridPosition pos, Direction dir, bool reversed) Canonicalize(GridPosition a, GridPosition b)
    {
        // Store North→South edges from the northern tile
        if (a.Y < b.Y) return (a, Direction.South, false);
        if (b.Y < a.Y) return (b, Direction.South, true);

        // Same row: store West→East from western tile
        if (a.X < b.X) return (a, Direction.East, false);
        return (b, Direction.East, true);
    }

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

    public List<MapLocationData> Locations
    {
        get
        {
            var result = new List<MapLocationData>();
            for (int x = 0; x < Width; x++)
            {
                for (int y = 0; y < Height; y++)
                {
                    var loc = _locations[x, y];
                    if (loc != null)
                        result.Add(new MapLocationData(x, y, loc));
                }
            }
            return result;
        }
        set
        {
            // Resize array if needed (parameterless constructor creates 0x0)
            if (_locations.GetLength(0) != Width || _locations.GetLength(1) != Height)
            {
                _locations = new Location?[Width, Height];
            }

            // Clear and rebuild index
            _locationIndex.Clear();
            Array.Clear(_locations);

            foreach (var data in value ?? [])
            {
                if (data.X >= 0 && data.X < Width && data.Y >= 0 && data.Y < Height)
                {
                    _locations[data.X, data.Y] = data.Location;
                    _locationIndex[data.Location.Id] = new GridPosition(data.X, data.Y);
                }
            }
        }
    }
}
