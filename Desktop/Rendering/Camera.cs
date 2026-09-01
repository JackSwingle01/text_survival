using System.Numerics;

namespace text_survival.Desktop.Rendering;

/// <summary>
/// Camera for the world grid view. Holds a continuous world-space centre in tiles and
/// glides toward a target. Coordinate conversion is pure geometry off that centre.
/// </summary>
public class Camera
{
    // Grid settings
    public int TileSize { get; set; } = 100;
    public int TileGap { get; set; } = 2;
    public int ViewSize { get; set; } = 5;  // 5x5 tile viewport

    /// <summary>How fast the centre closes on the target. Higher is snappier.</summary>
    public const float Smoothing = 10f;

    /// <summary>Beyond this gap the camera teleports instead of gliding (new game, load, restart).</summary>
    public const float SnapDistanceTiles = 6f;

    /// <summary>Current centre of the view, in world tile coordinates.</summary>
    public Vector2 Center { get; private set; }

    /// <summary>Where the centre is heading. Rendering sets this every frame.</summary>
    public Vector2 Target { get; set; }

    // Screen offset (where to draw the grid on screen)
    public int ScreenOffsetX { get; set; } = 50;
    public int ScreenOffsetY { get; set; } = 50;

    /// <summary>Total grid width in pixels.</summary>
    public int GridWidth => ViewSize * TileSize + (ViewSize - 1) * TileGap;

    /// <summary>Total grid height in pixels.</summary>
    public int GridHeight => GridWidth;  // Square grid

    private float Pitch => TileSize + TileGap;

    public Camera(float centerX = 0, float centerY = 0)
    {
        Center = new Vector2(centerX, centerY);
        Target = Center;
    }

    /// <summary>Move the centre to the target immediately.</summary>
    public void Snap() => Center = Target;

    /// <summary>Glide toward the target. Call once per frame.</summary>
    public void Update(float deltaTime)
    {
        Vector2 delta = Target - Center;

        if (delta.Length() > SnapDistanceTiles)
        {
            Center = Target;
            return;
        }

        Center += delta * (1 - MathF.Exp(-Smoothing * deltaTime));
    }

    /// <summary>
    /// Convert world tile coordinates to screen position (top-left corner of the tile).
    /// </summary>
    public Vector2 WorldToScreen(float worldX, float worldY)
    {
        float viewX = worldX - Center.X + ViewSize / 2;
        float viewY = worldY - Center.Y + ViewSize / 2;

        return new Vector2(ScreenOffsetX + viewX * Pitch, ScreenOffsetY + viewY * Pitch);
    }

    /// <summary>
    /// Convert screen position to world tile coordinates.
    /// Returns null if the position is outside the grid rect or lands in a gap.
    /// </summary>
    public (int x, int y)? ScreenToWorld(Vector2 screenPos)
    {
        if (screenPos.X < ScreenOffsetX || screenPos.Y < ScreenOffsetY ||
            screenPos.X >= ScreenOffsetX + GridWidth || screenPos.Y >= ScreenOffsetY + GridHeight)
            return null;

        float viewX = (screenPos.X - ScreenOffsetX) / Pitch + Center.X - ViewSize / 2;
        float viewY = (screenPos.Y - ScreenOffsetY) / Pitch + Center.Y - ViewSize / 2;

        int worldX = (int)MathF.Floor(viewX);
        int worldY = (int)MathF.Floor(viewY);

        // Reject the gap between tiles so a click never lands on the wrong side of a seam.
        Vector2 topLeft = WorldToScreen(worldX, worldY);
        if (screenPos.X - topLeft.X > TileSize || screenPos.Y - topLeft.Y > TileSize)
            return null;

        return (worldX, worldY);
    }

    /// <summary>
    /// Every tile the view can show, plus one tile of overscan on each side so panning
    /// never leaves a blank strip on the incoming edge.
    /// </summary>
    public IEnumerable<(int x, int y)> GetVisibleTiles()
    {
        int half = ViewSize / 2 + 1;
        int centerX = (int)MathF.Round(Center.X);
        int centerY = (int)MathF.Round(Center.Y);

        for (int y = centerY - half; y <= centerY + half; y++)
        {
            for (int x = centerX - half; x <= centerX + half; x++)
            {
                yield return (x, y);
            }
        }
    }

    /// <summary>Get the centre point of a tile in screen coordinates.</summary>
    public Vector2 GetTileCenter(float worldX, float worldY)
    {
        Vector2 topLeft = WorldToScreen(worldX, worldY);
        return new Vector2(topLeft.X + TileSize / 2f, topLeft.Y + TileSize / 2f);
    }

    /// <summary>
    /// Configure camera dimensions based on available screen space.
    /// Reserves space for UI panels and centers the grid.
    /// </summary>
    public void ConfigureForScreenSize(int screenWidth, int screenHeight,
        int leftPanelWidth = 300, int rightPanelWidth = 320, int padding = 20)
    {
        int availableWidth = screenWidth - leftPanelWidth - rightPanelWidth - padding * 2;
        int availableHeight = screenHeight - padding * 2;

        int availableSize = Math.Min(availableWidth, availableHeight);

        int calculatedTileSize = (availableSize - (ViewSize - 1) * TileGap) / ViewSize;
        TileSize = Math.Clamp(calculatedTileSize, 60, 300);

        int actualGridWidth = ViewSize * TileSize + (ViewSize - 1) * TileGap;
        int gridAreaStart = leftPanelWidth + padding;
        int gridAreaWidth = screenWidth - leftPanelWidth - rightPanelWidth - padding * 2;
        ScreenOffsetX = gridAreaStart + (gridAreaWidth - actualGridWidth) / 2;

        ScreenOffsetY = (screenHeight - actualGridWidth) / 2;
    }

    /// <summary>Get the X position where UI panels on the right should start.</summary>
    public int GetRightPanelX()
    {
        return ScreenOffsetX + GridWidth + 20; // 20px gap after grid
    }
}
