using text_survival.Actions;
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
    List<TileDto> Tiles  // Only explored/visible tiles
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

        return new GridStateDto(
            Width: map.Width,
            Height: map.Height,
            PlayerX: map.CurrentPosition.X,
            PlayerY: map.CurrentPosition.Y,
            Tiles: tiles
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
    List<string> FeatureIcons,
    bool HasFire,
    bool IsHazardous,
    bool IsPassable,
    bool IsPlayerHere,
    bool IsAdjacent,  // Can move here from current position
    int? TravelTimeMinutes  // Estimated travel time from current position (null if not adjacent)
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

        return new TileDto(
            X: x,
            Y: y,
            Terrain: location.Terrain.ToString(),
            Visibility: location.Visibility.ToString().ToLower(),
            LocationName: locationName,
            LocationTags: locationTags,
            FeatureIcons: GetFeatureIcons(location),
            HasFire: location.HasActiveHeatSource(),
            IsHazardous: TravelProcessor.IsHazardousTerrain(location),
            IsPassable: location.IsPassable,
            IsPlayerHere: isPlayerHere,
            IsAdjacent: isAdjacent,
            TravelTimeMinutes: travelTime
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
    double InjuryRiskPercent  // 0-50
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
