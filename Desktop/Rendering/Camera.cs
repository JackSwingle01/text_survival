using System.Numerics;
using Raylib_cs;

namespace text_survival.Desktop.Rendering;

/// <summary>
/// Camera for the world grid view.
/// Handles viewport tracking, coordinate conversion, and smooth transitions.
/// </summary>
public class Camera
{
    // Grid settings
    public int TileSize { get; set; } = 100;
    public int TileGap { get; set; } = 2;
    public int ViewSize { get; set; } = 7;  // 7x7 tile viewport

    // Camera position (world coordinates - which tile is centered)
    public int CenterX { get; private set; }
    public int CenterY { get; private set; }

    // Smooth transition state
    private float _transitionProgress = 1f;
    private int _fromX, _fromY;
    private int _toX, _toY;
    private float _transitionDuration = 0.3f;  // seconds

    // Screen offset (where to draw the grid on screen)
    public int ScreenOffsetX { get; set; } = 50;
    public int ScreenOffsetY { get; set; } = 50;

    /// <summary>
    /// Total grid width in pixels.
    /// </summary>
    public int GridWidth => ViewSize * TileSize + (ViewSize - 1) * TileGap;

    /// <summary>
    /// Total grid height in pixels.
    /// </summary>
    public int GridHeight => GridWidth;  // Square grid

    /// <summary>
    /// Is the camera currently animating a transition?
    /// </summary>
    public bool IsTransitioning => _transitionProgress < 1f;

    /// <summary>
    /// Current interpolated camera offset for smooth panning.
    /// </summary>
    public Vector2 CurrentOffset
    {
        get
        {
            if (!IsTransitioning)
                return Vector2.Zero;

            float t = RenderUtils.EaseOutCubic(_transitionProgress);
            float dx = (_toX - _fromX) * (1 - t);
            float dy = (_toY - _fromY) * (1 - t);
            return new Vector2(dx * (TileSize + TileGap), dy * (TileSize + TileGap));
        }
    }

    public Camera(int centerX = 0, int centerY = 0)
    {
        CenterX = centerX;
        CenterY = centerY;
        _fromX = _toX = centerX;
        _fromY = _toY = centerY;
    }

    /// <summary>
    /// Update camera position. Call this when player moves.
    /// </summary>
    public void SetCenter(int x, int y, bool animate = true)
    {
        if (x == CenterX && y == CenterY)
            return;

        if (animate)
        {
            _fromX = CenterX;
            _fromY = CenterY;
            _toX = x;
            _toY = y;
            _transitionProgress = 0f;
        }
        else
        {
            _transitionProgress = 1f;
        }

        CenterX = x;
        CenterY = y;
    }

    /// <summary>
    /// Update camera animation. Call once per frame.
    /// </summary>
    public void Update(float deltaTime)
    {
        if (IsTransitioning)
        {
            _transitionProgress += deltaTime / _transitionDuration;
            if (_transitionProgress >= 1f)
                _transitionProgress = 1f;
        }
    }

    /// <summary>
    /// Convert world tile coordinates to screen position.
    /// Returns the top-left corner of the tile on screen.
    /// </summary>
    public Vector2 WorldToScreen(int worldX, int worldY)
    {
        // Calculate view-space position (relative to center of viewport)
        int viewX = worldX - CenterX + ViewSize / 2;
        int viewY = worldY - CenterY + ViewSize / 2;

        // Convert to screen pixels
        float screenX = ScreenOffsetX + viewX * (TileSize + TileGap);
        float screenY = ScreenOffsetY + viewY * (TileSize + TileGap);

        // Apply transition offset
        Vector2 offset = CurrentOffset;
        screenX += offset.X;
        screenY += offset.Y;

        return new Vector2(screenX, screenY);
    }

    /// <summary>
    /// Convert screen position to world tile coordinates.
    /// Returns null if position is outside the grid.
    /// </summary>
    public (int x, int y)? ScreenToWorld(Vector2 screenPos)
    {
        // Adjust for transition offset
        Vector2 offset = CurrentOffset;
        float adjustedX = screenPos.X - ScreenOffsetX - offset.X;
        float adjustedY = screenPos.Y - ScreenOffsetY - offset.Y;

        // Calculate view-space tile
        int viewX = (int)(adjustedX / (TileSize + TileGap));
        int viewY = (int)(adjustedY / (TileSize + TileGap));

        // Check bounds
        if (viewX < 0 || viewX >= ViewSize || viewY < 0 || viewY >= ViewSize)
            return null;

        // Check if within tile (not in gap)
        float tileLocalX = adjustedX - viewX * (TileSize + TileGap);
        float tileLocalY = adjustedY - viewY * (TileSize + TileGap);
        if (tileLocalX > TileSize || tileLocalY > TileSize)
            return null;

        // Convert to world coordinates
        int worldX = viewX - ViewSize / 2 + CenterX;
        int worldY = viewY - ViewSize / 2 + CenterY;

        return (worldX, worldY);
    }

    /// <summary>
    /// Get the screen rectangle for a tile.
    /// </summary>
    public Rectangle GetTileRect(int worldX, int worldY)
    {
        Vector2 pos = WorldToScreen(worldX, worldY);
        return new Rectangle(pos.X, pos.Y, TileSize, TileSize);
    }

    /// <summary>
    /// Check if a world tile is visible in the current viewport.
    /// </summary>
    public bool IsTileVisible(int worldX, int worldY)
    {
        int halfView = ViewSize / 2;
        return worldX >= CenterX - halfView && worldX <= CenterX + halfView &&
               worldY >= CenterY - halfView && worldY <= CenterY + halfView;
    }

    /// <summary>
    /// Get all visible tile coordinates.
    /// </summary>
    public IEnumerable<(int x, int y)> GetVisibleTiles()
    {
        int halfView = ViewSize / 2;
        for (int y = CenterY - halfView; y <= CenterY + halfView; y++)
        {
            for (int x = CenterX - halfView; x <= CenterX + halfView; x++)
            {
                yield return (x, y);
            }
        }
    }

    /// <summary>
    /// Get the center point of a tile in screen coordinates.
    /// </summary>
    public Vector2 GetTileCenter(int worldX, int worldY)
    {
        Vector2 topLeft = WorldToScreen(worldX, worldY);
        return new Vector2(topLeft.X + TileSize / 2f, topLeft.Y + TileSize / 2f);
    }
}
