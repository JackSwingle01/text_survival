using Raylib_cs;
using System.Numerics;
using text_survival.Actions;
using text_survival.Environments.Features;
using text_survival.Environments.Grid;

namespace text_survival.Desktop.Rendering;

/// <summary>
/// Main world renderer that coordinates all grid rendering.
/// </summary>
public class WorldRenderer
{
    public Camera Camera { get; }

    private (int x, int y)? _hoveredTile;
    private (int x, int y)? _selectedTile;
    private readonly EffectsRenderer _effects;

    // Track screen size for resize handling
    private int _lastScreenWidth;
    private int _lastScreenHeight;

    /// <summary>
    /// Override position for player icon during travel animation.
    /// When set, player is drawn at this position instead of ctx.Map.CurrentPosition.
    /// </summary>
    public (float x, float y)? PlayerPositionOverride { get; set; }

    public WorldRenderer()
    {
        Camera = new Camera();
        _effects = new EffectsRenderer();

        // Initialize camera size based on current screen
        ConfigureCameraSize();
    }

    /// <summary>
    /// Configure camera dimensions based on current screen size.
    /// Called on init and when window is resized.
    /// </summary>
    private void ConfigureCameraSize()
    {
        int screenWidth = Raylib.GetScreenWidth();
        int screenHeight = Raylib.GetScreenHeight();

        if (screenWidth != _lastScreenWidth || screenHeight != _lastScreenHeight)
        {
            _lastScreenWidth = screenWidth;
            _lastScreenHeight = screenHeight;
            Camera.ConfigureForScreenSize(screenWidth, screenHeight);
        }
    }

    /// <summary>
    /// Get or set the currently selected tile (for popup display).
    /// </summary>
    public (int x, int y)? SelectedTile
    {
        get => _selectedTile;
        set => _selectedTile = value;
    }

    /// <summary>
    /// Update renderer state. Call once per frame.
    /// </summary>
    public void Update(GameContext ctx, float deltaTime)
    {
        // Check for window resize
        if (Raylib.IsWindowResized())
        {
            ConfigureCameraSize();
        }

        // Update camera to follow player (skip during travel - ProcessTravelTick handles it)
        if (ctx.ActiveTravel == null)
        {
            var playerPos = ctx.Map.CurrentPosition;
            Camera.SetCenter(playerPos.X, playerPos.Y);
        }
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

        // Render player icon (use override position if set, for travel animation)
        Vector2 playerScreenPos;
        if (PlayerPositionOverride.HasValue)
        {
            // Interpolate screen position for smooth travel animation
            playerScreenPos = GetInterpolatedTileCenter(PlayerPositionOverride.Value.x, PlayerPositionOverride.Value.y);
        }
        else
        {
            playerScreenPos = Camera.GetTileCenter(playerPos.X, playerPos.Y);
        }
        TileRenderer.DrawPlayerIcon(playerScreenPos.X, playerScreenPos.Y, Camera.TileSize);

        // Render NPC icons
        foreach (var npc in ctx.NPCs)
        {
            var npcPos = map.GetPosition(npc.CurrentLocation);
            if (map.GetVisibility(npcPos.X, npcPos.Y) == Environments.Grid.TileVisibility.Visible)
            {
                var screenPos = Camera.GetTileCenter(npcPos.X, npcPos.Y);
                TileRenderer.DrawNPCIcon(screenPos.X, screenPos.Y, Camera.TileSize, npc.Name);
            }
        }

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

        foreach (var feature in location.Features)
        {
            if (feature.MapIcon != null && slot < 4)
            {
                // Determine if this feature should glow
                bool hasGlow = feature is HeatSourceFeature { IsActive: true }
                            || feature is SnareLineFeature { HasCatchWaiting: true };
                TileRenderer.DrawFeatureIcon(x, y, Camera.TileSize, feature.MapIcon, slot++, hasGlow);
            }
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
    /// Handle a tile click. Returns the clicked tile coordinates.
    /// </summary>
    public (int x, int y)? HandleClick()
    {
        if (Raylib.IsMouseButtonPressed(MouseButton.Left) && _hoveredTile.HasValue)
        {
            return _hoveredTile;
        }
        return null;
    }

    /// <summary>
    /// Get the screen position for a tile (top-left corner).
    /// Used for popup positioning.
    /// </summary>
    public Vector2 GetTileScreenPosition(int x, int y)
    {
        return Camera.WorldToScreen(x, y);
    }

    /// <summary>
    /// Get the center screen position for a fractional world position.
    /// Used for smooth player movement animation.
    /// </summary>
    private Vector2 GetInterpolatedTileCenter(float worldX, float worldY)
    {
        // Get the four surrounding tile centers for bilinear interpolation
        int x0 = (int)MathF.Floor(worldX);
        int y0 = (int)MathF.Floor(worldY);
        float fx = worldX - x0;
        float fy = worldY - y0;

        // For simplicity, just use linear interpolation between tile centers
        Vector2 topLeft = Camera.GetTileCenter(x0, y0);
        Vector2 topRight = Camera.GetTileCenter(x0 + 1, y0);
        Vector2 bottomLeft = Camera.GetTileCenter(x0, y0 + 1);
        Vector2 bottomRight = Camera.GetTileCenter(x0 + 1, y0 + 1);

        // Bilinear interpolation
        Vector2 top = Vector2.Lerp(topLeft, topRight, fx);
        Vector2 bottom = Vector2.Lerp(bottomLeft, bottomRight, fx);
        return Vector2.Lerp(top, bottom, fy);
    }

    /// <summary>
    /// Clear the selected tile (hide popup).
    /// </summary>
    public void ClearSelection()
    {
        _selectedTile = null;
    }
}
