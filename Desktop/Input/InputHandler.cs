using ImGuiNET;
using Raylib_cs;
using text_survival.Actions;
using text_survival.Desktop.Rendering;
using text_survival.Environments.Grid;

namespace text_survival.Desktop.Input;

/// <summary>
/// Handles user input: clicks on world tiles, keyboard shortcuts.
/// </summary>
public class InputHandler
{
    private readonly WorldRenderer _worldRenderer;

    public InputHandler(WorldRenderer worldRenderer)
    {
        _worldRenderer = worldRenderer;
    }

    /// <summary>
    /// Process input and return any actions to execute.
    /// Call once per frame.
    /// </summary>
    public InputResult ProcessInput(GameContext ctx)
    {
        var result = new InputResult();

        // Handle tile clicks (only if ImGui doesn't want the mouse)
        if (Raylib.IsMouseButtonPressed(MouseButton.Left) && !ImGui.GetIO().WantCaptureMouse)
        {
            var clickedTile = _worldRenderer.HandleClick();
            if (clickedTile.HasValue)
            {
                result.ClickedTile = clickedTile;
                HandleTileClick(ctx, clickedTile.Value.x, clickedTile.Value.y, result);
            }
        }

        // Handle keyboard shortcuts
        ProcessKeyboardInput(ctx, result);

        return result;
    }

    /// <summary>
    /// Handle a click on a world tile.
    /// Shows tile popup instead of instant travel.
    /// </summary>
    private void HandleTileClick(GameContext ctx, int x, int y, InputResult result)
    {
        var map = ctx.Map;
        if (map == null) return;

        var currentPos = map.CurrentPosition;

        // Any valid tile click shows the popup
        if (map.IsValidPosition(x, y))
        {
            var location = map.GetLocationAt(x, y);
            if (location != null && location.Visibility != Environments.Grid.TileVisibility.Unexplored)
            {
                // Show popup for this tile
                result.ShowTilePopup = true;
                result.PopupTileX = x;
                result.PopupTileY = y;
            }
        }
    }

    /// <summary>
    /// Process keyboard input.
    /// </summary>
    private void ProcessKeyboardInput(GameContext ctx, InputResult result)
    {
        var map = ctx.Map;
        if (map == null) return;

        var currentPos = map.CurrentPosition;

        // WASD / Arrow keys for movement
        int dx = 0, dy = 0;

        if (Raylib.IsKeyPressed(KeyboardKey.W) || Raylib.IsKeyPressed(KeyboardKey.Up))
            dy = -1;
        else if (Raylib.IsKeyPressed(KeyboardKey.S) || Raylib.IsKeyPressed(KeyboardKey.Down))
            dy = 1;
        else if (Raylib.IsKeyPressed(KeyboardKey.A) || Raylib.IsKeyPressed(KeyboardKey.Left))
            dx = -1;
        else if (Raylib.IsKeyPressed(KeyboardKey.D) || Raylib.IsKeyPressed(KeyboardKey.Right))
            dx = 1;

        if (dx != 0 || dy != 0)
        {
            int targetX = currentPos.X + dx;
            int targetY = currentPos.Y + dy;

            if (map.CanMoveTo(targetX, targetY))
            {
                var targetPos = new GridPosition(targetX, targetY);
                var season = ctx.Weather?.CurrentSeason ?? Weather.Season.Winter;

                if (!map.IsEdgeBlocked(currentPos, targetPos, season))
                {
                    ctx.PendingTravelTarget = (targetPos.X, targetPos.Y);
                    result.TravelInitiated = true;
                }
                else
                {
                    result.Message = "The way is blocked.";
                }
            }
            else
            {
                result.Message = "Cannot move there.";
            }
        }

        // Quick action shortcuts
        if (Raylib.IsKeyPressed(KeyboardKey.I))
            result.OpenInventory = true;

        if (Raylib.IsKeyPressed(KeyboardKey.C))
            result.OpenCrafting = true;

        if (Raylib.IsKeyPressed(KeyboardKey.F))
            result.ToggleFire = true;

        if (Raylib.IsKeyPressed(KeyboardKey.L))
            result.OpenDiscoveryLog = true;

        if (Raylib.IsKeyPressed(KeyboardKey.N))
            result.OpenNPCs = true;

        if (Raylib.IsKeyPressed(KeyboardKey.Space))
            result.Wait = true;

        if (Raylib.IsKeyPressed(KeyboardKey.Escape))
            result.Cancel = true;
    }
}

/// <summary>
/// Result of processing input for a frame.
/// </summary>
public class InputResult
{
    public (int x, int y)? ClickedTile { get; set; }
    public bool TravelInitiated { get; set; }
    public bool OpenLocationMenu { get; set; }
    public bool OpenInventory { get; set; }
    public bool OpenCrafting { get; set; }
    public bool OpenDiscoveryLog { get; set; }
    public bool OpenNPCs { get; set; }
    public bool ToggleFire { get; set; }
    public bool Wait { get; set; }
    public bool Cancel { get; set; }
    public string? Message { get; set; }

    // Tile popup
    public bool ShowTilePopup { get; set; }
    public int PopupTileX { get; set; }
    public int PopupTileY { get; set; }
}
