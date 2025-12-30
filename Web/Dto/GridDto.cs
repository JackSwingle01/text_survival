using text_survival.Actions;
using text_survival.Actors.Animals;
using text_survival.Environments;
using text_survival.Environments.Grid;
using text_survival.Environments.Features;

namespace text_survival.Web.Dto;

/// <summary>
/// Complete grid state for rendering the tile map.
/// </summary>
public record GridStateDto(
    int Width,
    int Height,
    int PlayerX,
    int PlayerY,
    List<TileDto> Tiles,  // Only explored/visible tiles
    List<EdgeDto> Edges   // Edges between explored/visible tiles
)
{
    public static GridStateDto FromContext(GameContext ctx)
    {
        if (ctx.Map == null)
            throw new InvalidOperationException("Cannot create GridStateDto without a map");

        var map = ctx.Map;
        var tiles = new List<TileDto>();

        // Include all explored or visible locations
        for (int x = 0; x < map.Width; x++)
        {
            for (int y = 0; y < map.Height; y++)
            {
                var location = map.GetLocationAt(x, y);
                if (location != null && location.Visibility != TileVisibility.Unexplored)
                {
                    tiles.Add(TileDto.FromLocation(location, x, y, ctx));
                }
            }
        }

        // Build edge list - include edges where EITHER tile is explored
        var edges = new List<EdgeDto>();
        foreach (var edgeData in map.Edges)
        {
            var pos = new GridPosition(edgeData.X, edgeData.Y);
            var neighborPos = edgeData.Direction switch
            {
                Direction.East => new GridPosition(edgeData.X + 1, edgeData.Y),
                Direction.South => new GridPosition(edgeData.X, edgeData.Y + 1),
                _ => pos
            };

            var tile1 = map.GetLocationAt(pos);
            var tile2 = map.GetLocationAt(neighborPos);

            // Edge visible if either tile is at least explored
            bool visible = (tile1?.Visibility != TileVisibility.Unexplored) ||
                          (tile2?.Visibility != TileVisibility.Unexplored);

            if (visible)
            {
                edges.Add(new EdgeDto(
                    X: edgeData.X,
                    Y: edgeData.Y,
                    Direction: edgeData.Direction.ToString(),
                    EdgeType: edgeData.Edge.Type.ToString(),
                    Bidirectional: edgeData.Edge.Bidirectional,
                    TraversalModifier: edgeData.Edge.TraversalModifierMinutes
                ));
            }
        }

        return new GridStateDto(
            Width: map.Width,
            Height: map.Height,
            PlayerX: map.CurrentPosition.X,
            PlayerY: map.CurrentPosition.Y,
            Tiles: tiles,
            Edges: edges
        );
    }
}

/// <summary>
/// Individual tile data for rendering.
/// </summary>
public record TileDto(
    int X,
    int Y,
    string Terrain,
    string Visibility,  // "unexplored", "explored", "visible"
    string? LocationName,
    string? LocationTags,
    bool HasNamedLocation,  // True if this tile has a named location (even if unexplored)
    List<string> FeatureIcons,
    List<string> AnimalIcons,  // Emojis for herds at this tile
    bool HasFire,
    bool IsHazardous,
    bool IsPassable,
    bool IsPlayerHere,
    bool IsAdjacent,  // Can move here from current position
    int? TravelTimeMinutes,  // Estimated travel time from current position (null if not adjacent)

    // Environmental properties (null if unexplored)
    double? WindFactor,           // 0-2
    double? OverheadCoverLevel,   // 0-1
    double? TemperatureDeltaF,    // location temp modifier

    // Hazard properties
    double? TerrainHazardLevel,   // 0-1 (from GetEffectiveTerrainHazard)

    // Tactical properties
    bool? IsEscapeTerrain,
    bool? IsVantagePoint,
    double? VisibilityFactor,     // 0-2
    bool? IsDark,

    // Detailed feature info
    List<FeatureDetailDto>? FeatureDetails
)
{
    public static TileDto FromLocation(Location location, int x, int y, GameContext ctx)
    {
        var map = ctx.Map!;
        var currentPos = map.CurrentPosition;
        var thisPos = new GridPosition(x, y);

        bool isPlayerHere = currentPos.X == x && currentPos.Y == y;
        bool isAdjacent = currentPos.IsAdjacentTo(thisPos) && location.IsPassable;

        // Named locations (with features) show "???" until visited
        string? locationName = null;
        string? locationTags = null;
        if (!location.IsTerrainOnly)
        {
            if (location.Explored)
            {
                locationName = location.Name;
                locationTags = location.Tags;
            }
            else
            {
                locationName = "???";
                locationTags = null;
            }
        }

        // Calculate travel time for adjacent tiles
        int? travelTime = null;
        if (isAdjacent && !isPlayerHere)
        {
            travelTime = TravelProcessor.GetTraversalMinutes(
                ctx.CurrentLocation, location, ctx.player, ctx.Inventory);
        }

        // Only include detailed info for explored locations
        bool isExplored = location.Explored;

        return new TileDto(
            X: x,
            Y: y,
            Terrain: location.Terrain.ToString(),
            Visibility: location.Visibility.ToString().ToLower(),
            LocationName: locationName,
            LocationTags: locationTags,
            HasNamedLocation: !location.IsTerrainOnly,
            FeatureIcons: GetFeatureIcons(location),
            AnimalIcons: GetAnimalIcons(x, y, location, ctx),
            HasFire: location.HasActiveHeatSource(),
            IsHazardous: TravelProcessor.IsHazardousTerrain(location),
            IsPassable: location.IsPassable,
            IsPlayerHere: isPlayerHere,
            IsAdjacent: isAdjacent,
            TravelTimeMinutes: travelTime,

            // Environmental (only for explored)
            WindFactor: isExplored ? location.WindFactor : null,
            OverheadCoverLevel: isExplored ? location.OverheadCoverLevel : null,
            TemperatureDeltaF: isExplored ? location.TemperatureDeltaF : null,

            // Hazards
            TerrainHazardLevel: isExplored ? location.GetEffectiveTerrainHazard() : null,

            // Tactical
            IsEscapeTerrain: isExplored ? location.IsEscapeTerrain : null,
            IsVantagePoint: isExplored ? location.IsVantagePoint : null,
            VisibilityFactor: isExplored ? location.VisibilityFactor : null,
            IsDark: isExplored ? location.IsDark : null,

            // Feature details
            FeatureDetails: isExplored ? GetFeatureDetails(location, x, y, ctx) : null
        );
    }

    private static List<string> GetFeatureIcons(Location location)
    {
        // Only show feature icons for explored locations that are visible
        if (location.Visibility != TileVisibility.Visible) return [];

        // Get all feature icons, sorted by priority (highest first), limit to top 3
        return location.Features
            .Where(f => f.MapIcon != null)
            .OrderByDescending(f => f.IconPriority)
            .Select(f => f.MapIcon!)
            .Take(3)
            .ToList();
    }

    private static List<FeatureDetailDto> GetFeatureDetails(Location location, int x, int y, GameContext ctx)
    {
        // Features are self-describing - each feature knows how to represent itself for UI
        var details = location.Features
            .Select(f => f.GetUIInfo())
            .Where(info => info != null)
            .Select(info => new FeatureDetailDto(info!.Type, info.Label, info.Status, info.Details))
            .ToList();

        // Add herd information for visible tiles
        if (location.Visibility == TileVisibility.Visible)
        {
            var herds = ctx.Herds.GetHerdsAt(new GridPosition(x, y));
            foreach (var herd in herds)
            {
                var countDesc = herd.Count switch
                {
                    1 => "lone",
                    2 => "pair",
                    <= 4 => "small group",
                    <= 8 => "group",
                    _ => "large herd"
                };

                var stateDesc = herd.State switch
                {
                    HerdState.Resting => "resting",
                    HerdState.Grazing => "grazing",
                    HerdState.Patrolling => "patrolling",
                    HerdState.Alert => "alert",
                    HerdState.Fleeing => "fleeing",
                    HerdState.Hunting => "hunting",
                    HerdState.Feeding => "feeding",
                    _ => ""
                };

                var displayName = herd.AnimalType.DisplayName();
                var label = herd.Count == 1 ? displayName : displayName + "s";
                var status = $"{countDesc}, {stateDesc}";
                var emoji = herd.AnimalType.Emoji();

                details.Add(new FeatureDetailDto("herd", label, status, [emoji]));
            }
        }

        return details;
    }

    private static List<string> GetAnimalIcons(int x, int y, Location location, GameContext ctx)
    {
        // Only show animal icons for visible tiles
        if (location.Visibility != TileVisibility.Visible) return [];

        var herds = ctx.Herds.GetHerdsAt(new GridPosition(x, y));
        if (herds.Count == 0) return [];

        return herds
            .Take(4)  // Max 4 positions available
            .Select(h => h.AnimalType.Emoji())
            .ToList();
    }
}

/// <summary>
/// Request from client to move to a tile.
/// </summary>
public record MoveRequestDto(
    int TargetX,
    int TargetY
);

/// <summary>
/// Prompt for hazardous terrain (quick vs careful choice).
/// </summary>
public record HazardPromptDto(
    int TargetX,
    int TargetY,
    string HazardDescription,
    int QuickTimeMinutes,
    int CarefulTimeMinutes,
    double InjuryRisk  // 0-1
);

/// <summary>
/// Result of a move attempt.
/// </summary>
public record MoveResultDto(
    bool Success,
    string? ErrorMessage,
    int TimeElapsedMinutes,
    bool InjuryOccurred,
    string? InjuryDescription
);

/// <summary>
/// Detailed feature information for the enhanced tile popup.
/// </summary>
public record FeatureDetailDto(
    string Type,           // "shelter", "forage", "animal", "cache", etc.
    string Label,          // Display name
    string? Status,        // e.g., "75% insulation", "abundant"
    List<string>? Details  // Additional details like resource types
);

/// <summary>
/// Edge data for rendering between tiles.
/// Uses canonical storage: from lower position toward higher (East/South only).
/// </summary>
public record EdgeDto(
    int X,                  // Canonical position X
    int Y,                  // Canonical position Y
    string Direction,       // "East" or "South" (canonical directions only)
    string EdgeType,        // "River", "Cliff", "Climb", "GameTrail", "TrailMarker", "CutTrail"
    bool Bidirectional,     // False for cliffs (one-way descent)
    int TraversalModifier   // Time modifier in minutes (negative = faster)
);
