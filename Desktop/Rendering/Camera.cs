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
    // Terrain textures meet at their shared edges; highlights still identify cells.
    public int TileGap { get; set; } = 0;
    public int ViewSize { get; private set; } = 7;
    private (float x, float y, float width, float height)? _viewport;
    private float _wheelRemainder;

    /// <summary>Zoom in odd tile-count steps, retaining a tile at the centre of the view.</summary>
    public bool Zoom(int steps)
    {
        int size = (int)Math.Clamp((long)ViewSize - 2L * steps, 3, 31);
        if (size == ViewSize) return false;
        ViewSize = size;
        if (_viewport is { } rect)
            ConfigureForViewport(rect.x, rect.y, rect.width, rect.height);
        return true;
    }

    /// <summary>Accumulate fractional trackpad deltas without losing small gestures.</summary>
    public bool ZoomWheel(float delta)
    {
        if (!float.IsFinite(delta) || delta == 0) return false;
        if (Math.Sign(delta) != Math.Sign(_wheelRemainder)) _wheelRemainder = 0;
        _wheelRemainder += Math.Clamp(delta, -6, 6);
        int steps = (int)_wheelRemainder;
        _wheelRemainder -= steps;
        return Zoom(steps);
    }

    public bool ResetZoom()
    {
        _wheelRemainder = 0;
        return Zoom((ViewSize - 7) / 2);
    }

    /// <summary>How fast the centre closes on the target. Higher is snappier.</summary>
    public const float Smoothing = 10f;

    /// <summary>Beyond this gap the camera teleports instead of gliding (new game, load, restart).</summary>
    public const float SnapDistanceTiles = 6f;

    /// <summary>Current centre of the view, in world tile coordinates.</summary>
    public Vector2 Center { get; private set; }

    /// <summary>Where the centre is heading.</summary>
    public Vector2 Target { get; set; }

    // Screen offset (where to draw the grid on screen)
    public int ScreenOffsetX { get; set; } = 50;
    public int ScreenOffsetY { get; set; } = 50;

    /// <summary>Total grid width in pixels.</summary>
    public int GridWidth => _viewport is { } rect ? Math.Max(1, (int)rect.width) : ViewSize * TileSize + (ViewSize - 1) * TileGap;

    /// <summary>Total grid height in pixels.</summary>
    public int GridHeight => _viewport is { } rect ? Math.Max(1, (int)rect.height) : GridWidth;

    private float Pitch => TileSize + TileGap;
    private float CenterOffsetX => (GridWidth - TileSize) / 2f;
    private float CenterOffsetY => (GridHeight - TileSize) / 2f;

    public Camera(float centerX = 0, float centerY = 0)
    {
        Center = new Vector2(centerX, centerY);
        Target = Center;
    }

    public bool IsFollowingPlayer { get; private set; } = true;

    public void Follow(Vector2 playerPosition)
    {
        IsFollowingPlayer = true;
        Target = playerPosition;
    }

    public void TrackPlayer(Vector2 playerPosition)
    {
        if (IsFollowingPlayer) Target = playerPosition;
    }

    /// <summary>Pan in tiles, bounded by the map. Manual movement releases follow mode.</summary>
    public void Pan(Vector2 tiles, int mapWidth, int mapHeight, bool immediate = false)
    {
        if (tiles == Vector2.Zero) return;
        if (IsFollowingPlayer) Target = Center;
        IsFollowingPlayer = false;
        Target = Vector2.Clamp(Target + tiles, Vector2.Zero,
            new Vector2(Math.Max(0, mapWidth - 1), Math.Max(0, mapHeight - 1)));
        if (immediate) Center = Target;
    }

    public bool ContainsScreenPoint(Vector2 point) =>
        point.X >= ScreenOffsetX && point.Y >= ScreenOffsetY &&
        point.X < ScreenOffsetX + GridWidth && point.Y < ScreenOffsetY + GridHeight;

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
        float viewX = worldX - Center.X;
        float viewY = worldY - Center.Y;

        return new Vector2(ScreenOffsetX + CenterOffsetX + viewX * Pitch,
            ScreenOffsetY + CenterOffsetY + viewY * Pitch);
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

        float viewX = (screenPos.X - ScreenOffsetX - CenterOffsetX) / Pitch + Center.X;
        float viewY = (screenPos.Y - ScreenOffsetY - CenterOffsetY) / Pitch + Center.Y;

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
        int minX = (int)MathF.Floor(Center.X - CenterOffsetX / Pitch) - 1;
        int minY = (int)MathF.Floor(Center.Y - CenterOffsetY / Pitch) - 1;
        int maxX = (int)MathF.Floor(Center.X + (GridWidth - CenterOffsetX) / Pitch) + 1;
        int maxY = (int)MathF.Floor(Center.Y + (GridHeight - CenterOffsetY) / Pitch) + 1;

        for (int y = minY; y <= maxY; y++)
        {
            for (int x = minX; x <= maxX; x++)
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

    /// <summary>Fill the HUD viewport with square tiles, sizing zoom against its shorter edge.</summary>
    public void ConfigureForViewport(float x, float y, float width, float height)
    {
        _viewport = (x, y, width, height);
        int available = Math.Max(1, (int)Math.Min(width, height));
        TileSize = Math.Max(1, (available - (ViewSize - 1) * TileGap) / ViewSize);
        ScreenOffsetX = (int)x;
        ScreenOffsetY = (int)y;
    }
}
