using Raylib_cs;
using System.Numerics;
using text_survival.Actions;
using text_survival.Actors;
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
    private (int x, int y)? _hoveredCombatCell;
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

        // Update weather effects based on current weather
        var weather = ctx.CurrentLocation?.Weather;
        if (weather != null)
        {
            _effects.UpdateWeather(weather.Precipitation, weather.WindSpeed);
        }

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
    /// Render the world grid or combat grid based on context.
    /// </summary>
    public void Render(GameContext ctx)
    {
        if (ctx.ActiveCombat != null)
        {
            RenderCombatGrid(ctx);
        }
        else
        {
            RenderWorldGrid(ctx);
        }
    }

    /// <summary>
    /// Render the world grid.
    /// </summary>
    private void RenderWorldGrid(GameContext ctx)
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

        // Render herd animal icons
        foreach (var (worldX, worldY) in Camera.GetVisibleTiles())
        {
            if (map.IsValidPosition(worldX, worldY))
            {
                var visibility = map.GetVisibility(worldX, worldY);
                if (visibility == Environments.Grid.TileVisibility.Visible)
                {
                    var position = new GridPosition(worldX, worldY);
                    var herds = ctx.Herds.GetHerdsAt(position);

                    // Render up to 3 herds per tile at cardinal positions
                    int slot = 0;
                    foreach (var herd in herds)
                    {
                        if (slot >= 3) break; // Limit to 3 herds per tile for clarity

                        var screenPos = Camera.GetTileCenter(worldX, worldY);
                        TileRenderer.DrawAnimalIcon(screenPos.X, screenPos.Y, Camera.TileSize, herd.AnimalType, slot);
                        slot++;
                    }
                }
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

    /// <summary>
    /// Render the combat grid (25x25m tactical view).
    /// </summary>
    private void RenderCombatGrid(GameContext ctx)
    {
        var combat = ctx.ActiveCombat;
        if (combat == null) return;

        // Dark battlefield background
        Raylib.DrawRectangle(0, 0, Raylib.GetScreenWidth(), Raylib.GetScreenHeight(), new Color(20, 20, 25, 255));

        // Calculate grid parameters
        int gridSize = 25; // 25x25 meter grid
        int screenWidth = Camera.GridWidth;
        int screenHeight = Camera.GridHeight;
        int cellSize = Math.Min(screenWidth / gridSize, screenHeight / gridSize);
        int gridPixelWidth = cellSize * gridSize;
        int gridPixelHeight = cellSize * gridSize;
        int offsetX = Camera.ScreenOffsetX + (screenWidth - gridPixelWidth) / 2;
        int offsetY = Camera.ScreenOffsetY + (screenHeight - gridPixelHeight) / 2;

        // Draw terrain background (use current location terrain)
        var terrain = ctx.CurrentLocation?.Terrain.ToString() ?? "Plain";
        float timeFactor = CalculateTimeFactor(ctx);

        // Get base terrain color and adjust for time of day (same as main map)
        Color baseColor = TerrainColors.GetColor(terrain);
        float brightness = 0.4f + timeFactor * 0.6f;
        Color terrainColor = RenderUtils.AdjustBrightness(baseColor, brightness);
        Raylib.DrawRectangle(offsetX, offsetY, gridPixelWidth, gridPixelHeight, terrainColor);

        // Tile the terrain texture across the combat grid
        // Use scissor mode to clip terrain elements that extend beyond grid boundaries
        Raylib.BeginScissorMode(offsetX, offsetY, gridPixelWidth, gridPixelHeight);
        int tilePixels = cellSize * 3;  // Tile every 3 cells for good detail
        for (int gridX = 0; gridX < gridPixelWidth; gridX += tilePixels)
        {
            for (int gridY = 0; gridY < gridPixelHeight; gridY += tilePixels)
            {
                // Use TerrainRenderer to draw procedural terrain pattern
                TerrainRenderer.RenderTexture(
                    terrain,                    // Current location terrain type
                    offsetX + gridX,            // X position
                    offsetY + gridY,            // Y position
                    tilePixels,                 // Size
                    gridX / tilePixels,         // World X for seeding
                    gridY / tilePixels,         // World Y for seeding
                    timeFactor);                // Time of day factor
            }
        }
        Raylib.EndScissorMode();

        // Draw grid lines
        var gridLineColor = new Color(60, 60, 65, 100);
        for (int x = 0; x <= gridSize; x++)
        {
            int lineX = offsetX + x * cellSize;
            Raylib.DrawLine(lineX, offsetY, lineX, offsetY + gridPixelHeight, gridLineColor);
        }
        for (int y = 0; y <= gridSize; y++)
        {
            int lineY = offsetY + y * cellSize;
            Raylib.DrawLine(offsetX, lineY, offsetX + gridPixelWidth, lineY, gridLineColor);
        }

        // Draw units
        foreach (var unit in combat.Units)
        {
            if (!unit.actor.IsAlive) continue;

            var screenX = offsetX + unit.Position.X * cellSize + cellSize / 2;
            var screenY = offsetY + unit.Position.Y * cellSize + cellSize / 2;

            // Determine team color
            Color teamColor;
            if (unit == combat.Player)
            {
                teamColor = new Color(80, 150, 255, 255); // Blue for player
            }
            else if (combat.Team1.Contains(unit))
            {
                teamColor = new Color(100, 255, 100, 255); // Green for allies
            }
            else
            {
                teamColor = new Color(255, 80, 80, 255); // Red for enemies
            }

            // Draw unit icon
            float iconScale = cellSize / 40f; // Scale based on cell size
            if (unit.actor is Actors.Animals.Animal animal)
            {
                // Draw team-colored circle underneath
                Raylib.DrawCircle(screenX, screenY, cellSize / 3, new Color((byte)teamColor.R, (byte)teamColor.G, (byte)teamColor.B, (byte)100));

                // Draw animal sprite
                AnimalRenderer.DrawAnimal(animal.AnimalType, screenX, screenY, iconScale);
            }
            else
            {
                // Player or NPC - use proper character sprite
                if (unit == combat.Player)
                {
                    // Use the same detailed player icon as normal gameplay
                    TileRenderer.DrawPlayerIcon(screenX, screenY, cellSize);
                }
                else if (unit.actor is NPC npc)
                {
                    // Use NPC character sprite with consistent color per NPC
                    int hash = npc.Name.GetHashCode();
                    int paletteIndex = Math.Abs(hash) % TileRenderer.NpcPalettes.Length;
                    bool isFemale = (Math.Abs(hash) / TileRenderer.NpcPalettes.Length) % 2 == 1;

                    CharacterPalette palette = TileRenderer.NpcPalettes[paletteIndex];

                    // Draw at full scale for combat
                    if (isFemale)
                        TileRenderer.DrawCharacterFemale(screenX, screenY, cellSize, 1.0f, palette);
                    else
                        TileRenderer.DrawCharacterMale(screenX, screenY, cellSize, 1.0f, palette);
                }
            }

            // Draw health bar above unit
            float vitality = (float)unit.actor.Vitality;
            int barWidth = cellSize - 4;
            int barHeight = 4;
            int barX = screenX - barWidth / 2;
            int barY = screenY - cellSize / 2 - 8;

            // Background (dark)
            Raylib.DrawRectangle(barX, barY, barWidth, barHeight, new Color(40, 40, 40, 200));

            // Health fill (green -> yellow -> red)
            Color healthColor = vitality switch
            {
                >= 0.7f => new Color(100, 255, 100, 255),
                >= 0.4f => new Color(255, 255, 100, 255),
                _ => new Color(255, 100, 100, 255)
            };
            int fillWidth = (int)(barWidth * vitality);
            if (fillWidth > 0)
            {
                Raylib.DrawRectangle(barX, barY, fillWidth, barHeight, healthColor);
            }

            // Draw boldness ring (for enemies)
            if (!combat.Team1.Contains(unit))
            {
                float boldness = (float)unit.Boldness;
                int ringRadius = cellSize / 2 + 2;
                Color ringColor = boldness switch
                {
                    >= 0.7f => new Color(255, 100, 100, 150), // Aggressive - bright red
                    >= 0.5f => new Color(255, 180, 100, 120), // Bold - orange
                    >= 0.3f => new Color(255, 255, 100, 100), // Wary - yellow
                    _ => new Color(200, 200, 200, 80)          // Cautious - gray
                };
                Raylib.DrawCircleLines(screenX, screenY, ringRadius, ringColor);
            }
        }

        // Draw grid border
        Raylib.DrawRectangleLines(offsetX, offsetY, gridPixelWidth, gridPixelHeight, new Color(100, 100, 110, 255));
    }
}
