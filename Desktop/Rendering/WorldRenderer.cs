using Raylib_cs;
using System.Numerics;
using text_survival.Actions;
using text_survival.Actors;
using text_survival.Actors.Animals;
using text_survival.Environments.Features;
using text_survival.Environments.Grid;

namespace text_survival.Desktop.Rendering;

/// <summary>
/// Main world renderer that coordinates all grid rendering.
/// </summary>
public class WorldRenderer : IDisposable
{
    public Camera Camera { get; }
    private readonly FogRenderer _fog = new();
    private bool _draggingCamera;

    public void Dispose()
    {
        _fog.Dispose();
    }

    private (int x, int y)? _hoveredTile;
    private readonly UI.HudState _hud;
    private (int x, int y)? _selectedTile => _hud.SelectedTile;
    private (int x, int y)? _hoveredCombatCell;
    private readonly EffectsRenderer _effects;

    /// <summary>
    /// The combat unit currently under the mouse cursor, if any.
    /// </summary>
    public Combat.Unit? HoveredCombatUnit { get; private set; }

    public WorldRenderer(UI.HudState hud)
    {
        _hud = hud;
        Camera = new Camera();
        _effects = new EffectsRenderer();
    }

    public void SetViewport(UI.HudRect rect) => Camera.ConfigureForViewport(rect.X, rect.Y, rect.Width, rect.Height);

    /// <summary>
    /// Update renderer state. Call once per frame.
    /// </summary>
    public void Update(GameContext ctx, float deltaTime)
    {
        // The camera follows the player sprite - the same position the sprite is drawn at,
        // so the two arrive together instead of racing on separate clocks.
        Camera.TrackPlayer(PlayerWorldPosition(ctx));
        Camera.Update(deltaTime);

        // Update hover state
        UpdateHover();

        // Update weather effects based on current weather
        var weather = ctx.CurrentLocation?.Weather;
        if (weather != null)
        {
            _effects.UpdateWeather(weather.PrecipitationPct, weather.WindSpeedPct);
        }

        // Update effects
        _effects.Update(deltaTime);
    }

    public bool HandleCameraInput(GameContext ctx, float deltaTime, bool mouseCaptured, bool keyboardCaptured)
    {
        if (ctx.ActiveCombat != null || ctx.Map == null)
        {
            _draggingCamera = false;
            return false;
        }

        bool changed = false;
        if (!mouseCaptured && Camera.ContainsScreenPoint(Raylib.GetMousePosition()))
            changed |= Camera.ZoomWheel(Raylib.GetMouseWheelMove());

        if (!keyboardCaptured)
        {
            bool zoomModifier = Raylib.IsKeyDown(KeyboardKey.LeftControl) || Raylib.IsKeyDown(KeyboardKey.RightControl) ||
                Raylib.IsKeyDown(KeyboardKey.LeftSuper) || Raylib.IsKeyDown(KeyboardKey.RightSuper);
            if (zoomModifier)
            {
                if (Raylib.IsKeyPressed(KeyboardKey.Equal) || Raylib.IsKeyPressed(KeyboardKey.KpAdd))
                    changed |= Camera.Zoom(1);
                if (Raylib.IsKeyPressed(KeyboardKey.Minus) || Raylib.IsKeyPressed(KeyboardKey.KpSubtract))
                    changed |= Camera.Zoom(-1);
                if (Raylib.IsKeyPressed(KeyboardKey.Zero) || Raylib.IsKeyPressed(KeyboardKey.Kp0))
                    changed |= Camera.ResetZoom();
            }
            if (Input.HotkeyRegistry.IsPressed(Input.HotkeyAction.FollowPlayer))
            {
                Camera.Follow(PlayerWorldPosition(ctx));
                changed = true;
            }
            Vector2 direction = new(
                (Raylib.IsKeyDown(KeyboardKey.Right) ? 1 : 0) - (Raylib.IsKeyDown(KeyboardKey.Left) ? 1 : 0),
                (Raylib.IsKeyDown(KeyboardKey.Down) ? 1 : 0) - (Raylib.IsKeyDown(KeyboardKey.Up) ? 1 : 0));
            if (direction != Vector2.Zero)
            {
                Camera.Pan(Vector2.Normalize(direction) * (8f * deltaTime), ctx.Map.Width, ctx.Map.Height);
                changed = true;
            }
        }

        if (!Raylib.IsMouseButtonDown(MouseButton.Middle) || mouseCaptured)
            _draggingCamera = false;
        if (!mouseCaptured && Raylib.IsMouseButtonPressed(MouseButton.Middle) &&
            Camera.ContainsScreenPoint(Raylib.GetMousePosition()))
            _draggingCamera = true;
        if (_draggingCamera)
        {
            Camera.Pan(-Raylib.GetMouseDelta() / (Camera.TileSize + Camera.TileGap),
                ctx.Map.Width, ctx.Map.Height, immediate: true);
            changed = true;
        }
        return changed;
    }

    /// <summary>
    /// Where the player is on the world grid right now, in tile coordinates. While
    /// travelling this interpolates along the path from the travel run's own clock, so
    /// sprite and camera share one source of truth.
    /// </summary>
    public void RecenterOnPlayer(GameContext ctx) => Camera.Follow(PlayerWorldPosition(ctx));

    private static Vector2 PlayerWorldPosition(GameContext ctx)
    {
        var travel = ctx.ActiveTravel;
        if (travel == null)
        {
            var pos = ctx.Map!.CurrentPosition;
            return new Vector2(pos.X, pos.Y);
        }

        var destination = ctx.Map!.GetPosition(travel.Destination);
        float t = Easing.OutCubic(travel.Run.Progress);
        return Vector2.Lerp(
            new Vector2(travel.OriginPosition.X, travel.OriginPosition.Y),
            new Vector2(destination.X, destination.Y),
            t);
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
        TerrainRenderer.BeginFrame();
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
        var map = ctx.Map ?? throw new InvalidOperationException("Cannot render without an initialized map.");
        var playerPos = map.CurrentPosition;

        _fog.Begin();

        // Everything grid-bound is clipped to the grid rect so the overscan tiles never
        // spill under the side panels while the camera pans.
        Raylib.BeginScissorMode(Camera.ScreenOffsetX, Camera.ScreenOffsetY, Camera.GridWidth, Camera.GridHeight);

        // Render all visible tiles
        foreach (var (worldX, worldY) in Camera.GetVisibleTiles())
        {
            RenderTileAt(ctx, worldX, worldY, playerPos, timeFactor);
        }

        // Worn ground runs continuously beneath the prints left on it.
        TrailRenderer.Render(ctx, Camera, timeFactor);

        // Prints sit on the ground, under everything that made them.
        TrackRenderer.Render(ctx, Camera, timeFactor);

        // Render edges between tiles (rivers, cliffs, cave entrances)
        EdgeRenderer.RenderEdges(ctx, Camera, timeFactor);

        Raylib.EndScissorMode();
        _fog.End(Camera, map);
        Raylib.BeginScissorMode(Camera.ScreenOffsetX, Camera.ScreenOffsetY, Camera.GridWidth, Camera.GridHeight);

        // The player's camp is a persistent landmark. Once discovered, its marker
        // remains available while the camp is outside the current sight radius.
        RenderCampMarker(ctx, map);

        // Render player icon at the position the simulation says they are - interpolated
        // along the path while travelling, on their tile otherwise.
        Vector2 spritePos = PlayerWorldPosition(ctx);
        Vector2 playerScreenPos = Camera.GetTileCenter(spritePos.X, spritePos.Y);
        TileRenderer.DrawPlayerIcon(playerScreenPos.X, playerScreenPos.Y, Camera.TileSize);

        // Render NPC icons
        foreach (var npc in ctx.NPCs)
        {
            var npcPos = map.GetPosition(npc.CurrentLocation);
            if (map.GetVisibility(npcPos.X, npcPos.Y) == Environments.Grid.TileVisibility.Visible)
            {
                var screenPos = Camera.GetTileCenter(npcPos.X, npcPos.Y);
                TileRenderer.DrawNPCIcon(screenPos.X, screenPos.Y, Camera.TileSize, npc.Name);

                // Draw progress bar if NPC has active action with duration
                if (npc.CurrentAction != null && npc.CurrentAction.DurationMinutes > 0)
                {
                    float progress = (float)npc.CurrentAction.MinutesSpent / npc.CurrentAction.DurationMinutes;
                    TileRenderer.DrawActionProgressBar(
                        screenPos.X, screenPos.Y, Camera.TileSize,
                        progress, npc.CurrentAction.Name);
                }
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
                    var herds = ctx.Herds.At(position);

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

        if (_selectedTile is { } selected)
        {
            var selectedPosition = Camera.WorldToScreen(selected.x, selected.y);
            var outline = new Color(235, 180, 75, 255);
            Raylib.DrawRectangleLinesEx(new Rectangle(selectedPosition.X + 2, selectedPosition.Y + 2,
                Camera.TileSize - 4, Camera.TileSize - 4), 2, outline);
            var destination = new GridPosition(selected.x, selected.y);
            if (playerPos.IsAdjacentTo(destination) && map.CanMoveTo(selected.x, selected.y) &&
                !map.IsEdgeBlocked(playerPos, destination, ctx.Weather.CurrentSeason))
            {
                var end = Camera.GetTileCenter(selected.x, selected.y);
                for (float t = .3f; t < .9f; t += .12f)
                    Raylib.DrawCircleV(Vector2.Lerp(playerScreenPos, end, t), 2, outline);
            }
        }
        Raylib.EndScissorMode();

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
        var map = ctx.Map ?? throw new InvalidOperationException("Cannot render without an initialized map.");

        // Check if tile exists in the map
        if (!map.IsValidPosition(worldX, worldY))
        {
            Vector2 pos = Camera.WorldToScreen(worldX, worldY);
            TileRenderer.DrawUnexplored(pos.X, pos.Y, Camera.TileSize);
            return;
        }

        var location = map.GetLocationAt(worldX, worldY)
            ?? throw new InvalidOperationException($"Map has no location at in-bounds position ({worldX}, {worldY}).");
        var visibility = map.GetVisibility(worldX, worldY);

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
            map.DisplayTerrain(location),
            visibility,
            isPlayerTile,
            isHovered,
            isAdjacent && visibility == TileVisibility.Visible,
            timeFactor,
            (dx, dy) =>
            {
                int nx = worldX+dx, ny = worldY+dy;
                return map.IsValidPosition(nx, ny) && map.GetVisibility(nx, ny) != TileVisibility.Unexplored
                    ? map.DisplayTerrain(map.GetLocationAt(nx, ny)!) : null;
            }, caveFloor: location.CaveId.HasValue && !map.IsCaveConcealed(location));

        // Render feature icons if visible
        if (visibility == TileVisibility.Visible && !map.IsCaveConcealed(location))
            RenderLocationFeatures(location, screenPos.X, screenPos.Y);
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
                // A lit fire glows warm; a snare with a catch glows ready.
                Color? glow = feature switch
                {
                    HeatSourceFeature { IsActive: true } => new Color(224, 136, 48, 255),
                    SnareLineFeature { HasCatchWaiting: true } => new Color(100, 220, 100, 255),
                    _ => null
                };
                TileRenderer.DrawFeatureIcon(x, y, Camera.TileSize, feature.MapIcon, slot++, glow);
            }
        }
    }

    private void RenderCampMarker(GameContext ctx, GameMap map)
    {
        var camp = ctx.Camp;
        var campPosition = map.GetPosition(camp);
        if (map.GetVisibility(campPosition.X, campPosition.Y) != TileVisibility.Explored)
            return;

        Vector2 screenPosition = Camera.WorldToScreen(campPosition.X, campPosition.Y);
        TileRenderer.DrawFeatureIcon(
            screenPosition.X, screenPosition.Y, Camera.TileSize, "shelter", slot: 0,
            glow: new Color(150, 210, 255, 180));
    }

    /// <summary>
    /// Check cardinal adjacency, matching legal map movement.
    /// </summary>
    private static bool IsAdjacent(int x1, int y1, int x2, int y2)
    {
        int dx = Math.Abs(x1 - x2);
        int dy = Math.Abs(y1 - y2);
        return dx + dy == 1;
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
    /// Handle a combat grid click. Returns clicked cell coordinates.
    /// </summary>
    public (int x, int y)? HandleCombatClick()
    {
        if (Raylib.IsMouseButtonPressed(MouseButton.Left) && _hoveredCombatCell.HasValue)
        {
            return _hoveredCombatCell;
        }
        return null;
    }

    /// <summary>
    /// Render the combat grid (50x50m tactical view).
    /// </summary>
    private void RenderCombatGrid(GameContext ctx)
    {
        var combat = ctx.ActiveCombat;
        if (combat == null) return;

        // Dark battlefield background
        Raylib.DrawRectangle(0, 0, Raylib.GetScreenWidth(), Raylib.GetScreenHeight(), new Color(20, 20, 25, 255));

        // Calculate grid parameters
        int gridSize = Combat.CombatScenario.MAP_SIZE; // Dynamic grid size from scenario
        int screenWidth = Camera.GridWidth;
        int screenHeight = Camera.GridHeight;
        int cellSize = Math.Min(screenWidth / gridSize, screenHeight / gridSize);
        int gridPixelWidth = cellSize * gridSize;
        int gridPixelHeight = cellSize * gridSize;
        int offsetX = Camera.ScreenOffsetX + (screenWidth - gridPixelWidth) / 2;
        int offsetY = Camera.ScreenOffsetY + (screenHeight - gridPixelHeight) / 2;

        // Update hovered combat cell from mouse position
        Vector2 mousePos = Raylib.GetMousePosition();
        int mouseGridX = (int)((mousePos.X - offsetX) / cellSize);
        int mouseGridY = (int)((mousePos.Y - offsetY) / cellSize);
        if (mouseGridX >= 0 && mouseGridX < gridSize && mouseGridY >= 0 && mouseGridY < gridSize
            && mousePos.X >= offsetX && mousePos.Y >= offsetY
            && mousePos.X < offsetX + gridPixelWidth && mousePos.Y < offsetY + gridPixelHeight)
        {
            _hoveredCombatCell = (mouseGridX, mouseGridY);
            // Find unit at hovered cell
            HoveredCombatUnit = combat.Units.FirstOrDefault(u =>
                u.actor.IsAlive && u.Position.X == mouseGridX && u.Position.Y == mouseGridY);
        }
        else
        {
            _hoveredCombatCell = null;
            HoveredCombatUnit = null;
        }

        // Ground: the same tile art as the map, so the terrain you walked onto is the
        // terrain you fight on. Tiled every 3 cells; scissored so no tile spills past the grid.
        var terrain = ctx.CurrentLocation.Terrain;
        float timeFactor = CalculateTimeFactor(ctx);

        Raylib.BeginScissorMode(offsetX, offsetY, gridPixelWidth, gridPixelHeight);
        int tilePixels = cellSize * 3;
        for (int gridX = 0; gridX < gridPixelWidth; gridX += tilePixels)
        {
            for (int gridY = 0; gridY < gridPixelHeight; gridY += tilePixels)
            {
                TileRenderer.DrawTerrain(
                    terrain,
                    offsetX + gridX, offsetY + gridY, tilePixels,
                    gridX / tilePixels, gridY / tilePixels,
                    timeFactor, caveFloor: ctx.CurrentLocation.CaveId.HasValue);
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
            float animalSize = cellSize * 0.8f; // 80% of cell size
            if (unit.actor is Actors.Animals.Animal animal)
            {
                // Draw team-colored circle underneath
                Raylib.DrawCircle(screenX, screenY, cellSize / 3, new Color((byte)teamColor.R, (byte)teamColor.G, (byte)teamColor.B, (byte)100));

                // Draw animal sprite
                AnimalRenderer.DrawAnimal(animal.AnimalType, screenX, screenY, animalSize);
            }
            else
            {
                // Player or NPC - use proper character sprite
                if (unit == combat.Player)
                {
                    // Use the same detailed player icon as normal gameplay
                    TileRenderer.DrawPlayerIcon(screenX, screenY, cellSize, 3.0f);
                }
                else if (unit.actor is NPC npc)
                {
                    // Same appearance rules as the map, at full scale for combat
                    TileRenderer.DrawNPCSprite(screenX, screenY, cellSize, npc.Name, 3.0f);
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

            // Draw awareness/boldness ring (for enemies)
            if (!combat.Team1.Contains(unit))
            {
                int ringRadius = cellSize / 2 + 2;
                Color ringColor = unit.Awareness switch
                {
                    Combat.AwarenessState.Unaware => new Color(100, 200, 100, 100),  // Green - safe to approach
                    Combat.AwarenessState.Alert => new Color(255, 200, 100, 120),     // Orange - be careful
                    _ => (float)unit.Boldness switch  // Engaged - color based on boldness
                    {
                        >= 0.7f => new Color(255, 100, 100, 150), // Aggressive - bright red
                        >= 0.5f => new Color(255, 180, 100, 120), // Bold - orange
                        >= 0.3f => new Color(255, 255, 100, 100), // Wary - yellow
                        _ => new Color(200, 200, 200, 80)          // Cautious - gray
                    }
                };
                Raylib.DrawCircleLines(screenX, screenY, ringRadius, ringColor);
            }
        }

        // Draw movement range indicator around player
        var player = combat.Player;
        if (player != null && player.actor.IsAlive)
        {
            int playerScreenX = offsetX + player.Position.X * cellSize + cellSize / 2;
            int playerScreenY = offsetY + player.Position.Y * cellSize + cellSize / 2;
            int moveDistPixels = 3 * cellSize; // MOVE_DIST = 3 meters

            // Draw movement range circle (semi-transparent blue)
            Raylib.DrawCircleLines(playerScreenX, playerScreenY, moveDistPixels, new Color(80, 150, 255, 100));
            Raylib.DrawCircle(playerScreenX, playerScreenY, moveDistPixels, new Color(80, 150, 255, 20));

            // Highlight hovered cell if within movement range
            if (_hoveredCombatCell.HasValue)
            {
                var (hx, hy) = _hoveredCombatCell.Value;
                int hoveredCellX = offsetX + hx * cellSize;
                int hoveredCellY = offsetY + hy * cellSize;

                // Calculate distance from player to hovered cell
                double dist = Math.Sqrt(Math.Pow(hx - player.Position.X, 2) + Math.Pow(hy - player.Position.Y, 2));
                bool isInRange = dist <= 3.0 && dist > 0; // MOVE_DIST = 3, can't move to own cell

                // Check if cell is occupied by another unit
                bool isOccupied = combat.Units.Any(u => u.actor.IsAlive && u.Position.X == hx && u.Position.Y == hy);

                Color highlightColor;
                if (isOccupied)
                {
                    // Yellow for occupied cells
                    highlightColor = new Color(255, 200, 100, 60);
                }
                else if (isInRange)
                {
                    // Green for valid movement
                    highlightColor = new Color(100, 255, 100, 60);
                }
                else
                {
                    // Red for out of range
                    highlightColor = new Color(255, 100, 100, 40);
                }

                Raylib.DrawRectangle(hoveredCellX, hoveredCellY, cellSize, cellSize, highlightColor);
                Raylib.DrawRectangleLines(hoveredCellX, hoveredCellY, cellSize, cellSize,
                    new Color((byte)(highlightColor.R), (byte)(highlightColor.G), (byte)(highlightColor.B), (byte)150));
            }
        }

        // Draw grid border
        Raylib.DrawRectangleLines(offsetX, offsetY, gridPixelWidth, gridPixelHeight, new Color(100, 100, 110, 255));
    }
}
