using Raylib_cs;
using System.Numerics;
using text_survival.Actions;
using text_survival.Environments.Grid;

namespace text_survival.Desktop.Rendering;

/// <summary>
/// Main world renderer that coordinates all grid rendering.
/// </summary>
public class WorldRenderer
{
    public Camera Camera { get; }

    private (int x, int y)? _hoveredTile;
    private readonly EffectsRenderer _effects;

    public WorldRenderer()
    {
        Camera = new Camera();
        _effects = new EffectsRenderer();
    }

    /// <summary>
    /// Update renderer state. Call once per frame.
    /// </summary>
    public void Update(GameContext ctx, float deltaTime)
    {
        // Update camera to follow player
        var playerPos = ctx.Map.CurrentPosition;
        Camera.SetCenter(playerPos.X, playerPos.Y);
        Camera.Update(deltaTime);

        // Update hover state
        UpdateHover();

        // Update effects
        _effects.Update(deltaTime);
    }

    /// <summary>
    /// Update the hovered tile based on mouse position.
    /// </summary>
    private void UpdateHover()
    {
        Vector2 mousePos = Raylib.GetMousePosition();
        _hoveredTile = Camera.ScreenToWorld(mousePos);
    }

    /// <summary>
    /// Render the world grid.
    /// </summary>
    public void Render(GameContext ctx)
    {
        float timeFactor = CalculateTimeFactor(ctx);

        // Draw background
        DrawBackground(timeFactor);

        // Get map data
        var map = ctx.Map;
        var playerPos = map.CurrentPosition;

        // Render all visible tiles
        foreach (var (worldX, worldY) in Camera.GetVisibleTiles())
        {
            RenderTileAt(ctx, worldX, worldY, playerPos, timeFactor);
        }

        // Render edges between tiles (rivers, cliffs, trails)
        EdgeRenderer.RenderEdges(ctx, Camera, timeFactor);

        // Render player icon
        Vector2 playerScreenPos = Camera.GetTileCenter(playerPos.X, playerPos.Y);
        TileRenderer.DrawPlayerIcon(playerScreenPos.X, playerScreenPos.Y, Camera.TileSize);

        // Render weather effects
        _effects.RenderSnow(Camera.ScreenOffsetX, Camera.ScreenOffsetY, Camera.GridWidth, Camera.GridHeight);

        // Render vignette
        _effects.RenderVignette(Camera.ScreenOffsetX, Camera.ScreenOffsetY, Camera.GridWidth, Camera.GridHeight);

        // Render night overlay
        _effects.RenderNightOverlay(Camera.ScreenOffsetX, Camera.ScreenOffsetY, Camera.GridWidth, Camera.GridHeight, timeFactor);
    }

    /// <summary>
    /// Render a single tile at the given world coordinates.
    /// </summary>
    private void RenderTileAt(GameContext ctx, int worldX, int worldY, GridPosition playerPos, float timeFactor)
    {
        var map = ctx.Map;

        // Check if tile exists in the map
        if (!map.IsValidPosition(worldX, worldY))
        {
            // Out of bounds - draw as hidden
            Vector2 pos = Camera.WorldToScreen(worldX, worldY);
            Raylib.DrawRectangle((int)pos.X, (int)pos.Y, Camera.TileSize, Camera.TileSize, TerrainColors.Unexplored);
            return;
        }

        var location = map.GetLocationAt(worldX, worldY);
        var visibility = GetTileVisibility(map, worldX, worldY);

        // Get terrain type
        string terrain = location?.Terrain.ToString() ?? "Plain";

        // Check tile state
        bool isPlayerTile = worldX == playerPos.X && worldY == playerPos.Y;
        bool isHovered = _hoveredTile.HasValue && _hoveredTile.Value.x == worldX && _hoveredTile.Value.y == worldY;
        bool isAdjacent = IsAdjacent(worldX, worldY, playerPos.X, playerPos.Y);

        // Get screen position
        Vector2 screenPos = Camera.WorldToScreen(worldX, worldY);

        // Render the tile
        TileRenderer.RenderTile(
            screenPos.X, screenPos.Y, Camera.TileSize,
            worldX, worldY,
            terrain,
            visibility,
            isPlayerTile,
            isHovered,
            isAdjacent && visibility == TileVisibility.Visible,
            timeFactor);

        // Render feature icons if visible
        if (visibility == TileVisibility.Visible && location != null)
        {
            RenderLocationFeatures(location, screenPos.X, screenPos.Y);
        }
    }

    /// <summary>
    /// Render feature icons for a location.
    /// </summary>
    private void RenderLocationFeatures(Environments.Location location, float x, float y)
    {
        int slot = 0;

        // Check for fire
        var fire = location.GetFeature<Environments.Features.HeatSourceFeature>();
        if (fire != null && fire.IsActive)
        {
            TileRenderer.DrawFeatureIcon(x, y, Camera.TileSize, "local_fire_department", slot++, hasGlow: true);
        }
        else if (fire != null && fire.HasEmbers)
        {
            TileRenderer.DrawFeatureIcon(x, y, Camera.TileSize, "fireplace", slot++, hasGlow: true);
        }

        // Check for water
        var water = location.GetFeature<Environments.Features.WaterFeature>();
        if (water != null)
        {
            TileRenderer.DrawFeatureIcon(x, y, Camera.TileSize, "water_drop", slot++);
        }

        // Check for traps with catches
        var traps = location.GetFeature<Environments.Features.SnareLineFeature>();
        if (traps != null && traps.HasCatchWaiting)
        {
            TileRenderer.DrawFeatureIcon(x, y, Camera.TileSize, "check_circle", slot++, hasGlow: true);
        }
    }

    /// <summary>
    /// Get visibility state for a tile.
    /// </summary>
    private static TileVisibility GetTileVisibility(GameMap map, int x, int y)
    {
        var vis = map.GetVisibility(x, y);
        return vis switch
        {
            Environments.Grid.TileVisibility.Visible => TileVisibility.Visible,
            Environments.Grid.TileVisibility.Explored => TileVisibility.Explored,
            _ => TileVisibility.Hidden
        };
    }

    /// <summary>
    /// Check if two tiles are adjacent (including diagonals).
    /// </summary>
    private static bool IsAdjacent(int x1, int y1, int x2, int y2)
    {
        int dx = Math.Abs(x1 - x2);
        int dy = Math.Abs(y1 - y2);
        return dx <= 1 && dy <= 1 && (dx + dy > 0);
    }

    /// <summary>
    /// Calculate time of day factor (0 = midnight, 1 = noon).
    /// </summary>
    private static float CalculateTimeFactor(GameContext ctx)
    {
        var time = ctx.GameTime;
        int minutes = time.Hour * 60 + time.Minute;

        // Map to 0-1: 0 at midnight, 1 at noon, 0 at midnight again
        if (minutes <= 720)
            return minutes / 720f;
        else
            return (1440 - minutes) / 720f;
    }

    /// <summary>
    /// Draw background behind the grid.
    /// </summary>
    private void DrawBackground(float timeFactor)
    {
        // Interpolate background color based on time of day
        // Midnight: very dark blue-gray, Noon: slightly lighter
        float h = 215;
        float s = 30 - timeFactor * 5;
        float l = 5 + timeFactor * 10;

        // Convert HSL to RGB (simplified)
        Color bgColor = HslToRgb(h, s / 100f, l / 100f);

        // Draw full screen background
        Raylib.DrawRectangle(0, 0, Raylib.GetScreenWidth(), Raylib.GetScreenHeight(), bgColor);
    }

    /// <summary>
    /// Convert HSL to RGB color.
    /// </summary>
    private static Color HslToRgb(float h, float s, float l)
    {
        float c = (1 - Math.Abs(2 * l - 1)) * s;
        float x = c * (1 - Math.Abs((h / 60) % 2 - 1));
        float m = l - c / 2;

        float r, g, b;
        if (h < 60) { r = c; g = x; b = 0; }
        else if (h < 120) { r = x; g = c; b = 0; }
        else if (h < 180) { r = 0; g = c; b = x; }
        else if (h < 240) { r = 0; g = x; b = c; }
        else if (h < 300) { r = x; g = 0; b = c; }
        else { r = c; g = 0; b = x; }

        return new Color(
            (byte)((r + m) * 255),
            (byte)((g + m) * 255),
            (byte)((b + m) * 255),
            (byte)255);
    }

    /// <summary>
    /// Get the tile currently under the mouse, if any.
    /// </summary>
    public (int x, int y)? GetHoveredTile() => _hoveredTile;

    /// <summary>
    /// Handle a tile click.
    /// </summary>
    public (int x, int y)? HandleClick()
    {
        if (Raylib.IsMouseButtonPressed(MouseButton.Left) && _hoveredTile.HasValue)
        {
            return _hoveredTile;
        }
        return null;
    }
}
