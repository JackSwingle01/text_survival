# Terrain Edge Types - Implementation Plan

This plan implements directional edge features between adjacent tiles (rivers, cliffs, game trails, player-cut trails).

## Design Review Against Codebase

### Issues Found in Original Design

1. **HazardType enum duplicates existing system** - The codebase already calculates hazard through `TravelProcessor.GetInjuryRisk()` using `Location.TerrainHazardLevel` and `Location.ClimbRiskFactor`. The `VariantSelector.SelectTravelInjuryVariant()` already handles contextual injury selection (ice, climbing, terrain severity). A new `HazardType` enum creates parallel mechanics.

2. **EdgeCrossingResult is over-engineered** - The existing injury flow is:
   - `TravelRunner.PromptForSpeed()` → player chooses quick/careful
   - `TravelProcessor.GetInjuryRisk()` → calculates probability
   - `TravelHandler.ApplyTravelInjury()` → applies damage via `VariantSelector`

   Edges should feed into this existing flow, not create a separate injury resolution system.

3. **GameState doesn't exist** - The codebase uses `GameContext`.

4. **IAction interface doesn't match** - Player activities use `IWorkStrategy` for expedition work or handlers for camp actions. Trail marking should be a work strategy.

5. **Season is nested** - Should reference `Weather.Season`, not a standalone enum.

6. **Direction enum missing** - `GridPosition.GetCardinalNeighbors()` returns positions but there's no Direction enum.

---

## Revised Implementation

### 1. Direction Enum

Add to `Environments/Grid/Direction.cs`:

```csharp
namespace text_survival.Environments.Grid;

/// <summary>
/// Cardinal directions for tile-to-tile movement and edge storage.
/// </summary>
public enum Direction { North, East, South, West }

public static class DirectionExtensions
{
    /// <summary>
    /// Get the neighbor position in this direction.
    /// </summary>
    public static GridPosition GetNeighbor(this Direction dir, GridPosition from) => dir switch
    {
        Direction.North => new GridPosition(from.X, from.Y - 1),
        Direction.East => new GridPosition(from.X + 1, from.Y),
        Direction.South => new GridPosition(from.X, from.Y + 1),
        Direction.West => new GridPosition(from.X - 1, from.Y),
        _ => from
    };

    /// <summary>
    /// Get the opposite direction.
    /// </summary>
    public static Direction Opposite(this Direction dir) => dir switch
    {
        Direction.North => Direction.South,
        Direction.East => Direction.West,
        Direction.South => Direction.North,
        Direction.West => Direction.East,
        _ => dir
    };

    /// <summary>
    /// Get direction from one position to an adjacent position.
    /// Returns null if not adjacent.
    /// </summary>
    public static Direction? GetDirection(GridPosition from, GridPosition to)
    {
        int dx = to.X - from.X;
        int dy = to.Y - from.Y;

        if (dx == 0 && dy == -1) return Direction.North;
        if (dx == 1 && dy == 0) return Direction.East;
        if (dx == 0 && dy == 1) return Direction.South;
        if (dx == -1 && dy == 0) return Direction.West;
        return null;
    }
}
```

### 2. Edge Types and TileEdge Class

Add to `Environments/Grid/TileEdge.cs`:

```csharp
namespace text_survival.Environments.Grid;

/// <summary>
/// Types of edge features between adjacent tiles.
/// </summary>
public enum EdgeType
{
    // Natural (generated during map creation)
    River,          // Crossing penalty, provides water access
    Cliff,          // Impassable upward, one-way down
    Climb,          // Passable but slow and risky (uses existing climb hazard system)
    GameTrail,      // Animal highways - faster traversal

    // Player-created
    TrailMarker,    // Blazed trees, cairns - small navigation bonus
    CutTrail,       // Cleared path - larger bonus, replaces marker
}

/// <summary>
/// An edge feature between two adjacent tiles.
/// Edges modify traversal time, risk, and passability.
/// </summary>
public class TileEdge
{
    public EdgeType Type { get; set; }

    /// <summary>
    /// Traversal time modifier in minutes. Negative = faster.
    /// </summary>
    public int TraversalModifierMinutes { get; set; }

    /// <summary>
    /// If false, can only traverse in the stored direction (A→B, not B→A).
    /// Used for cliffs (can descend, can't ascend).
    /// </summary>
    public bool Bidirectional { get; set; } = true;

    /// <summary>
    /// Completely blocks passage. Checked per-season via IsBlockedIn().
    /// </summary>
    public bool Impassable { get; set; } = false;

    /// <summary>
    /// Additional hazard contribution (0-1). Adds to location hazard level.
    /// Uses existing TravelProcessor injury system.
    /// </summary>
    public double HazardContribution { get; set; } = 0;

    /// <summary>
    /// Season when this edge is blocked. Null = never blocked.
    /// Example: River blocked in Spring (flooding).
    /// </summary>
    public Weather.Season? BlockedSeason { get; set; } = null;

    public TileEdge() { }

    public TileEdge(EdgeType type)
    {
        Type = type;
        ApplyDefaults();
    }

    private void ApplyDefaults()
    {
        (TraversalModifierMinutes, Bidirectional, Impassable, HazardContribution, BlockedSeason) = Type switch
        {
            EdgeType.River      => (20, true, false, 0.15, null),       // +20 min, slight hazard
            EdgeType.Cliff      => (0, false, true, 0, null),            // One-way down, blocks up
            EdgeType.Climb      => (30, true, false, 0.35, null),        // +30 min, triggers hazard system
            EdgeType.GameTrail  => (-5, true, false, 0, null),           // -5 min, animals keep it clear
            EdgeType.TrailMarker => (-3, true, false, 0, null),          // -3 min, easier navigation
            EdgeType.CutTrail   => (-8, true, false, 0, null),           // -8 min, cleared brush
            _ => (0, true, false, 0, null)
        };
    }

    /// <summary>
    /// Check if blocked in the given season.
    /// </summary>
    public bool IsBlockedIn(Weather.Season season) =>
        Impassable || BlockedSeason == season;
}
```

### 3. Edge Storage in GameMap

Add to `Environments/Grid/GameMap.cs`:

```csharp
// === Edge Storage ===

/// <summary>
/// Edge features between tiles. Key is (canonical position, direction).
/// Multiple edges can exist between same tiles (river + game trail).
/// </summary>
private Dictionary<(GridPosition, Direction), List<TileEdge>> _edges = new();

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
/// Get max hazard contribution from edges.
/// </summary>
public double GetEdgeHazardContribution(GridPosition from, GridPosition to)
{
    var edges = GetEdgesBetween(from, to);
    return edges.Count > 0 ? edges.Max(e => e.HazardContribution) : 0;
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
```

Add serialization support (similar to `LocationData`):

```csharp
/// <summary>
/// Serializable edge data.
/// </summary>
public class EdgeData
{
    public int X { get; set; }
    public int Y { get; set; }
    public Direction Direction { get; set; }
    public TileEdge Edge { get; set; } = null!;
}

// In GameMap:
public List<EdgeData> EdgeData
{
    get => _edges
        .SelectMany(kvp => kvp.Value.Select(edge =>
            new EdgeData {
                X = kvp.Key.Item1.X,
                Y = kvp.Key.Item1.Y,
                Direction = kvp.Key.Item2,
                Edge = edge
            }))
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
```

### 4. Integrate with Travel System

**In `TravelProcessor.cs`:**

Update `CalculateSegmentTime` to accept edge modifier:

```csharp
/// <summary>
/// Calculate traversal time including edge modifiers.
/// </summary>
public static int GetTraversalMinutes(
    Location origin,
    Location destination,
    Player player,
    Inventory? inventory,
    int edgeModifierMinutes = 0)
{
    int exitTime = CalculateSegmentTime(origin, player, inventory);
    int entryTime = CalculateSegmentTime(destination, player, inventory);

    // Edge modifier applies once to total crossing
    int total = exitTime + entryTime + edgeModifierMinutes;
    return Math.Max(5, total);  // Minimum 5 minutes
}

/// <summary>
/// Get effective hazard for travel, including edge contribution.
/// </summary>
public static double GetEffectiveHazard(Location location, double edgeHazard)
{
    return Math.Max(location.GetEffectiveTerrainHazard(), edgeHazard);
}
```

**In `TravelRunner.cs`:**

Update `TravelToLocation` to check edge blocking and include edge modifiers:

```csharp
internal bool TravelToLocation(Location destination)
{
    Location origin = _ctx.CurrentLocation;

    // Check for blocked edges
    var originPos = _ctx.Map!.CurrentPosition;
    var destPos = _ctx.Map.GetPosition(destination);

    if (destPos.HasValue)
    {
        var season = _ctx.Weather.CurrentSeason;
        if (_ctx.Map.IsEdgeBlocked(originPos, destPos.Value, season))
        {
            GameDisplay.AddNarrative(_ctx, GetBlockedMessage(originPos, destPos.Value));
            return true;  // Not dead, just can't go there
        }
    }

    // Get edge modifiers
    int edgeModifier = destPos.HasValue
        ? _ctx.Map.GetEdgeTraversalModifier(originPos, destPos.Value)
        : 0;
    double edgeHazard = destPos.HasValue
        ? _ctx.Map.GetEdgeHazardContribution(originPos, destPos.Value)
        : 0;

    // Calculate segment times (existing code)
    int exitTime = TravelProcessor.CalculateSegmentTime(origin, _ctx.player, _ctx.Inventory);
    int entryTime = TravelProcessor.CalculateSegmentTime(destination, _ctx.player, _ctx.Inventory);

    // Apply edge modifier to total
    int totalBase = exitTime + entryTime + edgeModifier;

    // ... rest of existing hazard prompt logic, using edgeHazard for threshold checks
}

private string GetBlockedMessage(GridPosition from, GridPosition to)
{
    var edges = _ctx.Map!.GetEdgesBetween(from, to);
    var blocking = edges.FirstOrDefault(e => e.IsBlockedIn(_ctx.Weather.CurrentSeason));

    return blocking?.Type switch
    {
        EdgeType.Cliff => "Sheer cliff face. No way up.",
        EdgeType.River when blocking.BlockedSeason == Weather.Season.Spring =>
            "The river is in full flood. Impassable until the waters recede.",
        _ => "The way is blocked."
    };
}
```

Update `GetHazardDescription` to include edge-based hazards:

```csharp
private string GetHazardDescription(Location location, double edgeHazard)
{
    // Edge hazard takes priority for description
    if (edgeHazard >= 0.3)
    {
        var edges = GetCurrentEdges();
        if (edges.Any(e => e.Type == EdgeType.Climb))
            return "climb";
        if (edges.Any(e => e.Type == EdgeType.River))
            return "crossing";
    }

    // Existing location-based checks
    if (location.ClimbRiskFactor > 0)
        return "climb";

    var water = location.GetFeature<WaterFeature>();
    if (water != null && water.GetTerrainHazardContribution() > 0)
        return "ice";

    return "terrain";
}
```

### 5. Extend GetTravelOptions for Blocking

In `GameMap.GetTravelOptions()`:

```csharp
public IReadOnlyList<Location> GetTravelOptions(Weather.Season season)
{
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
```

### 6. Trail Marking as Work Strategy

Create `Actions/Expeditions/WorkStrategies/TrailMarkingStrategy.cs`:

```csharp
namespace text_survival.Actions.Expeditions.WorkStrategies;

/// <summary>
/// Strategy for marking and cutting trails between locations.
/// </summary>
public class TrailMarkingStrategy : IWorkStrategy
{
    private readonly Direction _direction;
    private readonly bool _fullCut;  // false = marker, true = cut trail

    public TrailMarkingStrategy(Direction direction, bool fullCut = false)
    {
        _direction = direction;
        _fullCut = fullCut;
    }

    public string GetActivityName() => _fullCut ? "Cutting trail" : "Marking trail";

    public ActivityType GetActivityType() => ActivityType.Crafting;  // Physical work

    public bool AllowedInDarkness => false;  // Need to see what you're doing

    public string? ValidateLocation(GameContext ctx, Location location)
    {
        var pos = ctx.Map?.GetPosition(location);
        if (pos == null) return "Cannot mark trails here.";

        var targetPos = _direction.GetNeighbor(pos.Value);
        var targetLoc = ctx.Map!.GetLocationAt(targetPos);

        if (targetLoc == null || !targetLoc.IsPassable)
            return "No accessible terrain in that direction.";

        // Can't mark through cliffs or water
        var edges = ctx.Map.GetEdgesBetween(pos.Value, targetPos);
        if (edges.Any(e => e.Type is EdgeType.Cliff or EdgeType.River))
            return "Cannot mark a trail through this terrain.";

        // Check if already has this trail type
        var existingType = _fullCut ? EdgeType.CutTrail : EdgeType.TrailMarker;
        if (edges.Any(e => e.Type == existingType))
            return $"Trail already {(_fullCut ? "cut" : "marked")} here.";

        // Tool check
        if (_fullCut)
        {
            if (!ctx.Inventory.HasGear(g => g.Type == GearType.Axe))
                return "Need an axe to cut a trail.";
        }
        else
        {
            if (!ctx.Inventory.HasGear(g => g.Type is GearType.Axe or GearType.Knife))
                return "Need a knife or axe to blaze marks.";
        }

        return null;
    }

    public Choice<int>? GetTimeOptions(GameContext ctx, Location location)
    {
        int baseTime = _fullCut ? 60 : 15;  // 1 hour to cut, 15 min to mark
        return new Choice<int>($"Time to {(_fullCut ? "cut" : "mark")} trail:")
            .AddOption($"{baseTime} minutes", baseTime);
    }

    public (int adjustedTime, List<string> warnings) ApplyImpairments(
        GameContext ctx, Location location, int baseTime)
    {
        var warnings = new List<string>();
        var capacities = ctx.player.GetCapacities();

        double multiplier = 1.0;

        if (capacities.Manipulation < 0.7)
        {
            multiplier += (1 - capacities.Manipulation) * 0.5;
            warnings.Add("Impaired hands slow the work.");
        }

        return ((int)(baseTime * multiplier), warnings);
    }

    public WorkResult Execute(GameContext ctx, Location location, int actualTime)
    {
        var pos = ctx.Map!.GetPosition(location)!.Value;
        var targetPos = _direction.GetNeighbor(pos);

        var edgeType = _fullCut ? EdgeType.CutTrail : EdgeType.TrailMarker;
        ctx.Map.AddEdge(pos, targetPos, new TileEdge(edgeType));

        // Degrade tool
        if (_fullCut)
        {
            var axe = ctx.Inventory.GetGear(g => g.Type == GearType.Axe);
            axe?.Degrade(5);
        }
        else
        {
            var tool = ctx.Inventory.GetGear(g => g.Type is GearType.Axe or GearType.Knife);
            tool?.Degrade(1);
        }

        string targetName = ctx.Map.GetLocationAt(targetPos)?.Name ?? "that direction";
        string message = _fullCut
            ? $"You clear a proper trail toward {targetName}."
            : $"You blaze marks on the trees toward {targetName}.";

        GameDisplay.AddNarrative(ctx, message);

        return new WorkResult([], null, actualTime, false);
    }
}
```

### 7. Map Generation (Conceptual)

Edges are generated during map creation. Pseudocode for the generator:

```
// After terrain and locations are placed:

LAYER: RIVERS
  start near mountain pass
  meander south toward map edge
  add River edge between each pair of tiles

LAYER: CLIFFS & CLIMBS
  for each tile pair with 2+ elevation difference:
    50% chance Cliff (impassable up)
    50% chance Climb (passable but hazardous)

LAYER: GAME TRAILS
  connect water sources to nearby clearings (40% chance)
  run trails along rivers (30% chance)
```

---

## Summary of Changes

| Component | Change |
|-----------|--------|
| `Direction.cs` | New file - Direction enum + extensions |
| `TileEdge.cs` | New file - EdgeType enum + TileEdge class |
| `GameMap.cs` | Add edge storage + query methods + serialization |
| `TravelProcessor.cs` | Accept edge modifiers in time/hazard calculations |
| `TravelRunner.cs` | Check edge blocking, include edge modifiers |
| `TrailMarkingStrategy.cs` | New work strategy for player trail creation |
| Map generator | Add edge generation layers |

## Edge Type Summary

| Edge Type | Traversal | Direction | Hazard | Created By |
|-----------|-----------|-----------|--------|------------|
| River | +20 min | Both | 0.15 | Generated |
| Cliff | Blocked | Down only | — | Generated |
| Climb | +30 min | Both | 0.35 | Generated |
| Game Trail | -5 min | Both | — | Generated |
| Trail Marker | -3 min | Both | — | Player (15 min) |
| Cut Trail | -8 min | Both | — | Player (60 min) |

## Key Design Decisions

1. **No new HazardType enum** - Edge hazard contribution feeds into existing `TravelProcessor.GetInjuryRisk()` which already handles injury selection via `VariantSelector`.

2. **Extends existing systems** - Edge modifiers add to, don't replace, the existing travel time and hazard calculations.

3. **Trail marking is a work strategy** - Follows existing pattern of `IWorkStrategy` implementations.

4. **Cliff = one-way, Climb = risky** - Cliffs are impassable upward (find another way), climbs are hazardous both ways (risk it or use the pass).

5. **Rivers can flood** - `BlockedSeason` allows Spring floods to make rivers impassable.

6. **Edges stack** - A river + game trail means the trail follows the river (faster crossing, animals drink there).
