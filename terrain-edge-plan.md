# Terrain Edge Types - Implementation Plan

This plan implements directional edge features between adjacent tiles (rivers, cliffs, game trails, player-cut trails).

## Core Insight

**Edges own events.** Instead of:
- `ClimbRiskFactor` on tiles + hazard inference + probabilistic events
- `WaterCrossing` event triggering on `NearWater` condition

We get:
- Climb edges with climb events attached
- River edges with crossing events attached
- Events fire when you actually cross the edge, not probabilistically while "near" something

## Migration Path

1. **Remove `Location.ClimbRiskFactor`** - Replaced by Climb edges
2. **Move `WaterCrossing` event** - From expedition pool to River edge events
3. **Named locations specify edges** - Boulder Field declares "Climb on all sides"
4. **Generated edges get events** - Rivers get crossing events, climbs get fall events

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
    River,          // Crossing penalty, water access, crossing events
    Cliff,          // Impassable upward, one-way down
    Climb,          // Passable but slow and risky, fall events
    GameTrail,      // Animal highways - faster traversal

    // Player-created
    TrailMarker,    // Blazed trees, cairns - small navigation bonus
    CutTrail,       // Cleared path - larger bonus, replaces marker
}

/// <summary>
/// An event that can trigger when crossing an edge.
/// </summary>
public class EdgeEvent
{
    /// <summary>
    /// Probability of triggering (0-1). 1.0 = always triggers.
    /// </summary>
    public double TriggerChance { get; set; }

    /// <summary>
    /// Factory function to create the event. Receives context for dynamic content.
    /// </summary>
    public Func<GameContext, GameEvent?> CreateEvent { get; set; } = null!;

    /// <summary>
    /// Optional: only trigger in certain seasons.
    /// </summary>
    public Weather.Season? RequiredSeason { get; set; } = null;

    public EdgeEvent() { }

    public EdgeEvent(double triggerChance, Func<GameContext, GameEvent?> createEvent)
    {
        TriggerChance = triggerChance;
        CreateEvent = createEvent;
    }
}

/// <summary>
/// An edge feature between two adjacent tiles.
/// Edges modify traversal time, risk, passability, and can trigger events.
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
    /// Season when this edge is blocked. Null = never blocked.
    /// Example: River blocked in Spring (flooding).
    /// </summary>
    public Weather.Season? BlockedSeason { get; set; } = null;

    /// <summary>
    /// Events that can trigger when crossing this edge.
    /// Checked in order; first triggered event is used.
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public List<EdgeEvent> Events { get; set; } = [];

    public TileEdge() { }

    public TileEdge(EdgeType type)
    {
        Type = type;
        ApplyDefaults();
    }

    private void ApplyDefaults()
    {
        (TraversalModifierMinutes, Bidirectional, Impassable, BlockedSeason) = Type switch
        {
            EdgeType.River      => (20, true, false, null),    // +20 min to ford
            EdgeType.Cliff      => (0, false, true, null),     // One-way down, blocks up
            EdgeType.Climb      => (30, true, false, null),    // +30 min, risky
            EdgeType.GameTrail  => (-5, true, false, null),    // -5 min, animals keep it clear
            EdgeType.TrailMarker => (-3, true, false, null),   // -3 min, easier navigation
            EdgeType.CutTrail   => (-8, true, false, null),    // -8 min, cleared brush
            _ => (0, true, false, null)
        };

        // Attach default events based on type
        Events = GetDefaultEvents(Type);
    }

    /// <summary>
    /// Get default events for an edge type.
    /// </summary>
    private static List<EdgeEvent> GetDefaultEvents(EdgeType type) => type switch
    {
        EdgeType.River => [
            new EdgeEvent(0.6, EdgeEvents.WaterCrossing)  // 60% chance of crossing event
        ],
        EdgeType.Climb => [
            new EdgeEvent(0.4, EdgeEvents.ClimbingHazard) // 40% chance of climb event
        ],
        _ => []
    };

    /// <summary>
    /// Check if blocked in the given season.
    /// </summary>
    public bool IsBlockedIn(Weather.Season season) =>
        Impassable || BlockedSeason == season;

    /// <summary>
    /// Roll for edge event. Returns event if one triggers, null otherwise.
    /// </summary>
    public GameEvent? TryTriggerEvent(GameContext ctx)
    {
        foreach (var edgeEvent in Events)
        {
            // Check season requirement
            if (edgeEvent.RequiredSeason.HasValue &&
                ctx.Weather.CurrentSeason != edgeEvent.RequiredSeason)
                continue;

            // Roll for trigger
            if (Random.Shared.NextDouble() < edgeEvent.TriggerChance)
            {
                return edgeEvent.CreateEvent(ctx);
            }
        }
        return null;
    }
}
```

### 3. Edge Events (moved from GameEventRegistry)

Add to `Actions/Events/EdgeEvents.cs`:

```csharp
namespace text_survival.Actions.Events;

/// <summary>
/// Events that trigger when crossing edges.
/// Migrated from GameEventRegistry.Expedition.cs
/// </summary>
public static class EdgeEvents
{
    /// <summary>
    /// Water crossing event - moved from WaterCrossing in expedition events.
    /// Now triggers when crossing a River edge, not probabilistically "near water".
    /// </summary>
    public static GameEvent? WaterCrossing(GameContext ctx)
    {
        var slipVariant = VariantSelector.SelectSlipVariant(ctx);

        return new GameEvent("Water Crossing",
            "Water blocks your path. Moving water, or still water with thin ice at the edges.", 0.8)
            .Choice("Wade Across",
                "Straight through. Get wet, get it over with.",
                [
                    new EventResult("Cold but quick. You're through.", 0.50, 8)
                        .WithEffects(EffectFactory.Wet(0.5), EffectFactory.Cold(-10, 30)),
                    new EventResult("Deeper than expected. Soaked to the waist.", 0.30, 12)
                        .WithEffects(EffectFactory.Wet(0.8), EffectFactory.Cold(-15, 45)),
                    new EventResult("Current stronger than it looked.", 0.15, 15)
                        .WithEffects(EffectFactory.Wet(0.9), EffectFactory.Cold(-18, 60)),
                    new EventResult("You slip. Water closes over your head.", 0.05, 18)
                        .DamageWithVariant(slipVariant)
                        .WithEffects(EffectFactory.Wet(1.0), EffectFactory.Cold(-25, 90))
                ])
            .Choice("Find Another Route",
                "Look for a better crossing point.",
                [
                    new EventResult("You find a narrow point. Easy crossing.", 0.40, 20),
                    new EventResult("Long detour but you stay dry.", 0.35, 30),
                    new EventResult("No good options. You cross anyway.", 0.20, 25)
                        .WithEffects(EffectFactory.Wet(0.5), EffectFactory.Cold(-10, 30))
                ])
            .Choice("Drink First",
                "You're here. Might as well hydrate.",
                [
                    new EventResult("Fresh and cold. You drink your fill.", 0.85, 10)
                        .Rewards(RewardPool.WaterSource),
                    new EventResult("Ice breaks. You're in the water.", 0.15, 15)
                        .DamageWithVariant(slipVariant)
                        .WithEffects(EffectFactory.Wet(0.9))
                ]);
    }

    /// <summary>
    /// Climbing hazard event - triggers when crossing a Climb edge.
    /// Uses existing ClimbingFall variants.
    /// </summary>
    public static GameEvent? ClimbingHazard(GameContext ctx)
    {
        var fallVariant = AccidentVariants.ClimbingFall[
            Random.Shared.Next(AccidentVariants.ClimbingFall.Length)];

        return new GameEvent("Difficult Terrain",
            "The way forward requires scrambling over rough ground.", 0.9)
            .Choice("Move Carefully",
                "Take your time. Test each handhold.",
                [
                    new EventResult("Slow but safe. You make it.", 0.70, 20),
                    new EventResult("A handhold crumbles. You recover.", 0.25, 25)
                        .WithEffects(EffectFactory.Shaken(0.2, 30)),
                    new EventResult(fallVariant.Description, 0.05, 15)
                        .DamageWithVariant(fallVariant)
                ])
            .Choice("Move Quickly",
                "Speed over caution. Get it done.",
                [
                    new EventResult("Momentum carries you through.", 0.50, 10),
                    new EventResult(fallVariant.Description, 0.35, 12)
                        .DamageWithVariant(fallVariant),
                    new EventResult("Bad fall. You tumble hard.", 0.15, 15)
                        .DamageWithVariant(fallVariant)
                        .WithEffects(EffectFactory.Dazed(0.3))
                ])
            .Choice("Find Another Way",
                "There might be an easier route.",
                [
                    new EventResult("You find a gentler path.", 0.40, 25),
                    new EventResult("Long way around, but safer.", 0.40, 35),
                    new EventResult("No good options. You climb.", 0.20, 20)
                ]);
    }

    /// <summary>
    /// First-time climb event for named locations (100% trigger).
    /// More narrative than the generic hazard.
    /// </summary>
    public static Func<GameContext, GameEvent?> FirstClimb(string locationName, string description)
    {
        return ctx =>
        {
            var fallVariant = AccidentVariants.ClimbingFall[
                Random.Shared.Next(AccidentVariants.ClimbingFall.Length)];

            return new GameEvent($"The Approach to {locationName}", description, 1.0)
                .Choice("Make the Climb",
                    "Commit. Find your route.",
                    [
                        new EventResult("Challenging but doable. You reach the top.", 0.60, 25),
                        new EventResult("Harder than it looked. You're breathing hard at the top.", 0.30, 35)
                            .WithEffects(EffectFactory.Exhausted(0.2, 30)),
                        new EventResult(fallVariant.Description, 0.10, 20)
                            .DamageWithVariant(fallVariant)
                    ])
                .Choice("Look for Another Way",
                    "Circle the approach. There might be an easier route.",
                    [
                        new EventResult("You find a gentler slope.", 0.50, 30),
                        new EventResult("No luck. You climb anyway.", 0.50, 25)
                    ]);
        };
    }
}
```

### 4. Location Edge Specification

Locations can specify what edges they create when placed on the map.

Add to `Environments/Location.cs`:

```csharp
/// <summary>
/// Edge specifications for this location. Applied when location is placed on map.
/// Key = direction, Value = edge to create on that side.
/// Null means no special edge (use terrain-based generation).
/// </summary>
[System.Text.Json.Serialization.JsonIgnore]
public Dictionary<Direction, TileEdge>? EdgeOverrides { get; set; }

/// <summary>
/// Apply the same edge type on all sides. Convenience for locations like Boulder Field.
/// </summary>
public Location WithEdgesOnAllSides(EdgeType type, List<EdgeEvent>? customEvents = null)
{
    EdgeOverrides = new()
    {
        [Direction.North] = new TileEdge(type) { Events = customEvents ?? [] },
        [Direction.East] = new TileEdge(type) { Events = customEvents ?? [] },
        [Direction.South] = new TileEdge(type) { Events = customEvents ?? [] },
        [Direction.West] = new TileEdge(type) { Events = customEvents ?? [] },
    };
    return this;
}

/// <summary>
/// Set a specific edge on one side.
/// </summary>
public Location WithEdge(Direction dir, EdgeType type, List<EdgeEvent>? customEvents = null)
{
    EdgeOverrides ??= new();
    EdgeOverrides[dir] = new TileEdge(type) { Events = customEvents ?? [] };
    return this;
}
```

**Remove from Location.cs:**
```csharp
// DELETE THIS:
public double ClimbRiskFactor { get; set; } = 0;
```

### 5. Update LocationFactory

Update locations that currently use `ClimbRiskFactor`:

```csharp
// Boulder Field - climb on all sides
public static Location MakeBoulderField(Weather weather)
{
    var location = new Location(...)
    {
        // ... existing properties ...
        // REMOVE: ClimbRiskFactor = 0.3
    }
    .WithEdgesOnAllSides(EdgeType.Climb);  // NEW

    return location;
}

// Rocky Ridge - climb with first-visit event
public static Location MakeRockyRidge(Weather weather)
{
    var location = new Location(...)
    {
        // ... existing properties ...
        // REMOVE: ClimbRiskFactor = 0.4
    }
    .WithEdgesOnAllSides(EdgeType.Climb, [
        new EdgeEvent(1.0, EdgeEvents.FirstClimb("Rocky Ridge",
            "The ridge rises sharply. Broken stone and loose scree. " +
            "Good handholds if you pick your route."))
    ]);

    return location;
}

// The Lookout - tree climb is different (keep as feature/event)
// The tree climbing is a CHOICE at the location, not an approach hazard
public static Location MakeTheLookout(Weather weather)
{
    var location = new Location(...)
    {
        // ... existing properties ...
        // REMOVE: ClimbRiskFactor = 0.25
        // Tree climbing stays as FirstVisitEvent - it's optional, not forced
    };
    // No edge overrides - normal terrain approach
    return location;
}
```

### 6. Edge Storage in GameMap

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

### 8. Integrate with Travel System

**In `TravelProcessor.cs`:**

Update `GetTraversalMinutes` to accept edge modifier:

```csharp
/// <summary>
/// Get total traversal time including edge modifiers.
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
```

**In `TravelRunner.cs`:**

Update `TravelToLocation` to check edge events:

```csharp
internal bool TravelToLocation(Location destination)
{
    Location origin = _ctx.CurrentLocation;
    var originPos = _ctx.Map!.CurrentPosition;
    var destPos = _ctx.Map.GetPosition(destination);

    // Check for blocked edges
    if (destPos.HasValue)
    {
        var season = _ctx.Weather.CurrentSeason;
        if (_ctx.Map.IsEdgeBlocked(originPos, destPos.Value, season))
        {
            GameDisplay.AddNarrative(_ctx, GetBlockedMessage(originPos, destPos.Value));
            return true;  // Not dead, just can't go there
        }
    }

    // Check for edge events BEFORE travel
    if (destPos.HasValue)
    {
        var edgeEvent = _ctx.Map.TryTriggerEdgeEvent(originPos, destPos.Value, _ctx);
        if (edgeEvent != null)
        {
            // Handle the edge event - player might turn back
            var result = GameEventRegistry.HandleEvent(_ctx, edgeEvent);

            // Check if player chose to turn back (event can set this flag)
            if (result.TurnedBack)
            {
                GameDisplay.AddNarrative(_ctx, "You decide not to proceed.");
                return true;  // Didn't travel, but not dead
            }

            if (!_ctx.player.IsAlive) return false;
        }
    }

    // Get edge time modifier
    int edgeModifier = destPos.HasValue
        ? _ctx.Map.GetEdgeTraversalModifier(originPos, destPos.Value)
        : 0;

    // Calculate segment times
    int exitTime = TravelProcessor.CalculateSegmentTime(origin, _ctx.player, _ctx.Inventory);
    int entryTime = TravelProcessor.CalculateSegmentTime(destination, _ctx.player, _ctx.Inventory);
    int totalTime = exitTime + entryTime + edgeModifier;

    // ... rest of existing travel logic (progress bar, arrival, etc.)
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

**Remove from TravelRunner.cs:**
```csharp
// DELETE - hazard type is now determined by edges, not inferred:
private static string GetHazardDescription(Location location)
{
    if (location.ClimbRiskFactor > 0)  // ClimbRiskFactor no longer exists
        return "climb";
    // ...
}
```

**Remove from GameEventRegistry.Expedition.cs:**
```csharp
// DELETE - moved to EdgeEvents.cs and triggered by River edges:
private static GameEvent WaterCrossing(GameContext ctx) { ... }

// Also remove from event registration:
// (WaterCrossing, 3.0, [EventCondition.NearWater, EventCondition.Traveling])
```

**Remove from VariantSelector.cs:**
```csharp
// DELETE - climb risk no longer on location:
bool hasClimbRisk = location.ClimbRiskFactor > 0;
if (hasClimbRisk) { ... }
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
| `TileEdge.cs` | New file - EdgeType enum + TileEdge + EdgeEvent classes |
| `EdgeEvents.cs` | New file - WaterCrossing, ClimbingHazard events |
| `GameMap.cs` | Add edge storage + query methods + event triggering |
| `Location.cs` | Add `EdgeOverrides`, remove `ClimbRiskFactor` |
| `LocationFactory.cs` | Replace `ClimbRiskFactor` with `.WithEdgesOnAllSides()` |
| `TravelRunner.cs` | Check edge events before travel, handle turn-back |
| `TravelProcessor.cs` | Accept edge time modifiers |
| `GameEventRegistry.Expedition.cs` | Remove `WaterCrossing` (moved to edges) |
| `VariantSelector.cs` | Remove `ClimbRiskFactor` checks |
| `TrailMarkingStrategy.cs` | New work strategy for player trails |
| Map generator | Add edge generation layers |

## Edge Type Summary

| Edge Type | Traversal | Direction | Events | Created By |
|-----------|-----------|-----------|--------|------------|
| River | +20 min | Both | WaterCrossing (60%) | Generated |
| Cliff | Blocked | Down only | — | Generated |
| Climb | +30 min | Both | ClimbingHazard (40%) | Generated / Location |
| Game Trail | -5 min | Both | — | Generated |
| Trail Marker | -3 min | Both | — | Player (15 min) |
| Cut Trail | -8 min | Both | — | Player (60 min) |

## Locations to Migrate

| Location | Current | New |
|----------|---------|-----|
| Boulder Field | `ClimbRiskFactor = 0.3` | `.WithEdgesOnAllSides(EdgeType.Climb)` |
| Rocky Ridge | `ClimbRiskFactor = 0.4` | `.WithEdgesOnAllSides(EdgeType.Climb, [100% first-climb event])` |
| The Lookout | `ClimbRiskFactor = 0.25` | Remove (tree climb is a choice, not approach) |
| Ice Shelf | `ClimbRiskFactor = 0.35` | `.WithEdgesOnAllSides(EdgeType.Climb)` |
| Bone Hollow | `ClimbRiskFactor = 0.15` | `.WithEdge(Direction.North, EdgeType.Climb)` or remove |
| Sun-Warmed Cliff | `ClimbRiskFactor = 0.2` | `.WithEdgesOnAllSides(EdgeType.Climb)` |

## Key Design Decisions

1. **Edges own events** - Events trigger when you cross an edge, not probabilistically while "near" something. This makes events contextual and deterministic.

2. **Locations specify their edges** - Named locations like Boulder Field declare "Climb on all sides" instead of having a `ClimbRiskFactor` property that gets inferred.

3. **Events moved, not duplicated** - `WaterCrossing` moves from expedition pool to River edges. Same event, proper trigger.

4. **First-time events can be 100%** - Named locations can set trigger chance to 1.0 for guaranteed narrative moments on first approach.

5. **Turn-back as event outcome** - "Find Another Way" choices in edge events can result in not traveling, handled cleanly in TravelRunner.

6. **Trail marking is a work strategy** - Follows existing `IWorkStrategy` pattern.

7. **Edges stack** - A river + game trail means the trail follows the river (faster crossing point, animals drink there).

## Code Removal Checklist

- [ ] `Location.ClimbRiskFactor` property
- [ ] `TravelRunner.GetHazardDescription()` method
- [ ] `GameEventRegistry.WaterCrossing()` method
- [ ] `WaterCrossing` from event registration
- [ ] `VariantSelector.SelectTravelInjuryVariant()` climb risk logic
- [ ] `EventCondition.NearWater` checks (verify other uses first)
- [ ] UI references to `climbRiskFactor` in `app.js:792` and `GridDto.cs:76`
