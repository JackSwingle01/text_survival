using text_survival.Actions;
using text_survival.Actions.Events;

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
    /// Season when this edge is blocked. Null = never blocked by season.
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

    /// <summary>
    /// Create an edge with custom events (overriding defaults).
    /// </summary>
    public TileEdge(EdgeType type, List<EdgeEvent> customEvents)
    {
        Type = type;
        ApplyDefaults();
        Events = customEvents;
    }

    private void ApplyDefaults()
    {
        (TraversalModifierMinutes, Bidirectional, Impassable, BlockedSeason) = Type switch
        {
            EdgeType.River => (20, true, false, (Weather.Season?)null),       // +20 min to ford
            EdgeType.Cliff => (0, false, true, (Weather.Season?)null),        // One-way down, blocks up
            EdgeType.Climb => (30, true, false, (Weather.Season?)null),       // +30 min, risky
            EdgeType.GameTrail => (-5, true, false, (Weather.Season?)null),   // -5 min, animals keep it clear
            EdgeType.TrailMarker => (-3, true, false, (Weather.Season?)null), // -3 min, easier navigation
            EdgeType.CutTrail => (-8, true, false, (Weather.Season?)null),    // -8 min, cleared brush
            _ => (0, true, false, (Weather.Season?)null)
        };

        // Attach default events based on type (populated by EdgeEvents.cs)
        Events = GetDefaultEvents(Type);
    }

    /// <summary>
    /// Get default events for an edge type.
    /// </summary>
    private static List<EdgeEvent> GetDefaultEvents(EdgeType type) =>
        EdgeEvents.DefaultEventsFor(type);

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
