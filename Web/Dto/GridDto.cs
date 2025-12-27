using text_survival.Actions;
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
        if (ctx.Grid == null || ctx.CurrentTile == null)
            throw new InvalidOperationException("Cannot create GridStateDto without grid mode enabled");

        var tiles = new List<TileDto>();

        // Include all explored or visible tiles
        foreach (var tile in ctx.Grid.AllTiles)
        {
            if (tile.IsExplored || tile.IsVisible)
            {
                tiles.Add(TileDto.FromTile(tile, ctx));
            }
        }

        return new GridStateDto(
            Width: ctx.Grid.Width,
            Height: ctx.Grid.Height,
            PlayerX: ctx.CurrentTile.X,
            PlayerY: ctx.CurrentTile.Y,
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
    bool IsAdjacent  // Can move here from current position
)
{
    public static TileDto FromTile(Tile tile, GameContext ctx)
    {
        bool isPlayerHere = ctx.CurrentTile == tile;
        bool isAdjacent = ctx.CurrentTile != null &&
                          tile.Position.IsAdjacentTo(ctx.CurrentTile.Position);

        return new TileDto(
            X: tile.X,
            Y: tile.Y,
            Terrain: tile.Terrain.ToString(),
            Visibility: tile.Visibility.ToString().ToLower(),
            LocationName: tile.HasNamedLocation ? tile.NamedLocation!.Name : null,
            LocationTags: tile.HasNamedLocation ? tile.NamedLocation!.Tags : null,
            FeatureIcons: GetFeatureIcons(tile),
            HasFire: tile.HasActiveHeatSource,
            IsHazardous: GridTravelProcessor.IsHazardousTerrain(tile),
            IsPassable: tile.IsPassable,
            IsPlayerHere: isPlayerHere,
            IsAdjacent: isAdjacent && tile.IsPassable
        );
    }

    private static List<string> GetFeatureIcons(Tile tile)
    {
        var icons = new List<string>();

        if (!tile.HasNamedLocation) return icons;

        var location = tile.NamedLocation!;

        // Only show feature icons if tile is visible (not just explored)
        if (tile.Visibility != TileVisibility.Visible) return icons;

        // Heat source / fire
        if (location.HasActiveHeatSource())
            icons.Add("fire");

        // Forage
        if (location.HasFeature<ForageFeature>())
            icons.Add("forage");

        // Harvest
        if (location.HasFeature<HarvestableFeature>())
            icons.Add("harvest");

        // Animals
        if (location.HasFeature<AnimalTerritoryFeature>())
            icons.Add("animals");

        // Water
        if (location.HasFeature<WaterFeature>())
            icons.Add("water");

        // Cache
        if (location.HasFeature<CacheFeature>())
            icons.Add("cache");

        // Shelter
        if (location.HasFeature<ShelterFeature>())
            icons.Add("shelter");

        // Wooded area (chopping)
        if (location.HasFeature<WoodedAreaFeature>())
            icons.Add("wood");

        // Snare line
        if (location.HasFeature<SnareLineFeature>())
            icons.Add("trap");

        // Curing rack
        if (location.HasFeature<CuringRackFeature>())
            icons.Add("curing");

        // Crafting project
        if (location.HasFeature<CraftingProjectFeature>())
            icons.Add("project");

        // Salvage
        if (location.HasFeature<SalvageFeature>())
            icons.Add("salvage");

        return icons;
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
