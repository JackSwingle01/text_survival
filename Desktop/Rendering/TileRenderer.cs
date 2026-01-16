using Raylib_cs;
using System.Numerics;
using text_survival.Environments.Grid;

namespace text_survival.Desktop.Rendering;

/// <summary>
/// Visibility state for a tile.
/// </summary>
public enum TileVisibility
{
    Hidden,     // Never seen - completely black
    Explored,   // Previously seen - dimmed
    Visible     // Currently visible - full brightness
}

/// <summary>
/// Renders individual tiles with terrain, fog of war, and highlights.
/// </summary>
public static class TileRenderer
{
    /// <summary>
    /// Render a single tile.
    /// </summary>
    public static void RenderTile(
        float x, float y, float size,
        int worldX, int worldY,
        string terrain,
        TileVisibility visibility,
        bool isPlayerTile,
        bool isHovered,
        bool isAdjacent,
        float timeFactor)
    {
        // Skip completely hidden tiles
        if (visibility == TileVisibility.Hidden)
        {
            Raylib.DrawRectangle((int)x, (int)y, (int)size, (int)size, TerrainColors.Unexplored);
            return;
        }

        // Get base terrain color and adjust for time of day
        Color baseColor = TerrainColors.GetColor(terrain);
        baseColor = AdjustForTimeOfDay(baseColor, timeFactor);

        // Draw base color
        Raylib.DrawRectangle((int)x, (int)y, (int)size, (int)size, baseColor);

        // Draw terrain texture
        TerrainRenderer.RenderTexture(terrain, x, y, size, worldX, worldY, timeFactor);

        // Apply fog of war for explored but not visible tiles
        if (visibility == TileVisibility.Explored)
        {
            var fogColor = new Color(0, 0, 0, 160);
            Raylib.DrawRectangle((int)x, (int)y, (int)size, (int)size, fogColor);
        }

        // Highlight effects
        if (isPlayerTile)
        {
            DrawPlayerTileHighlight(x, y, size);
        }
        else if (isHovered)
        {
            DrawHoverHighlight(x, y, size);
        }
        else if (isAdjacent)
        {
            DrawAdjacentHighlight(x, y, size);
        }

        // Tile border
        DrawTileBorder(x, y, size, visibility == TileVisibility.Visible);
    }

    /// <summary>
    /// Adjust color for time of day (darker at night).
    /// </summary>
    private static Color AdjustForTimeOfDay(Color color, float timeFactor)
    {
        // timeFactor: 0 = midnight (darkest), 1 = noon (brightest)
        // Map to brightness multiplier: 0.4 at midnight, 1.0 at noon
        float brightness = 0.4f + timeFactor * 0.6f;
        return RenderUtils.AdjustBrightness(color, brightness);
    }

    /// <summary>
    /// Draw highlight for the player's current tile.
    /// </summary>
    private static void DrawPlayerTileHighlight(float x, float y, float size)
    {
        // Subtle warm glow
        var glowColor = new Color(255, 200, 150, 30);
        Raylib.DrawRectangle((int)x, (int)y, (int)size, (int)size, glowColor);

        // Brighter border
        var borderColor = new Color(255, 200, 150, 80);
        Raylib.DrawRectangleLinesEx(new Rectangle(x, y, size, size), 2, borderColor);
    }

    /// <summary>
    /// Draw highlight for hovered tile.
    /// </summary>
    private static void DrawHoverHighlight(float x, float y, float size)
    {
        var hoverColor = new Color(255, 255, 255, 40);
        Raylib.DrawRectangle((int)x, (int)y, (int)size, (int)size, hoverColor);

        var borderColor = new Color(255, 255, 255, 100);
        Raylib.DrawRectangleLinesEx(new Rectangle(x, y, size, size), 2, borderColor);
    }

    /// <summary>
    /// Draw subtle highlight for adjacent (reachable) tiles.
    /// </summary>
    private static void DrawAdjacentHighlight(float x, float y, float size)
    {
        // Very subtle white overlay
        var adjColor = new Color(255, 255, 255, 15);
        Raylib.DrawRectangle((int)x, (int)y, (int)size, (int)size, adjColor);
    }

    /// <summary>
    /// Draw tile border.
    /// </summary>
    private static void DrawTileBorder(float x, float y, float size, bool isVisible)
    {
        var borderColor = isVisible
            ? new Color(255, 255, 255, 20)
            : new Color(255, 255, 255, 10);
        Raylib.DrawRectangleLinesEx(new Rectangle(x, y, size, size), 1, borderColor);
    }

    /// <summary>
    /// Draw the player icon at a tile position.
    /// </summary>
    public static void DrawPlayerIcon(float centerX, float centerY, float tileSize)
    {
        float iconSize = tileSize * 0.25f;

        // Shadow
        var shadowColor = new Color(0, 0, 0, 100);
        Raylib.DrawCircle((int)(centerX + 2), (int)(centerY + 2), iconSize, shadowColor);

        // Player circle
        var playerColor = new Color(200, 180, 140, 255);
        Raylib.DrawCircle((int)centerX, (int)centerY, iconSize, playerColor);

        // Inner detail
        var innerColor = new Color(160, 140, 100, 255);
        Raylib.DrawCircle((int)centerX, (int)centerY, iconSize * 0.6f, innerColor);
    }

    /// <summary>
    /// Draw a feature icon on a tile.
    /// </summary>
    public static void DrawFeatureIcon(float x, float y, float tileSize, string icon, int slot, bool hasGlow = false)
    {
        // Calculate icon position based on slot (0-3 for corners)
        float iconSize = tileSize * 0.15f;
        float margin = tileSize * 0.1f;

        float iconX = slot switch
        {
            0 => x + margin,              // Top-left
            1 => x + tileSize - margin - iconSize,  // Top-right
            2 => x + margin,              // Bottom-left
            3 => x + tileSize - margin - iconSize,  // Bottom-right
            _ => x + tileSize / 2
        };

        float iconY = slot switch
        {
            0 or 1 => y + margin,
            2 or 3 => y + tileSize - margin - iconSize,
            _ => y + tileSize / 2
        };

        // Draw glow if requested
        if (hasGlow)
        {
            var glowColor = new Color(255, 200, 100, 60);
            Raylib.DrawCircle((int)(iconX + iconSize / 2), (int)(iconY + iconSize / 2), iconSize * 1.5f, glowColor);
        }

        // Draw icon background
        var bgColor = new Color(30, 30, 30, 180);
        Raylib.DrawCircle((int)(iconX + iconSize / 2), (int)(iconY + iconSize / 2), iconSize, bgColor);

        // For now, draw a placeholder - actual icons will use font or sprites
        var iconColor = GetIconColor(icon);
        Raylib.DrawCircle((int)(iconX + iconSize / 2), (int)(iconY + iconSize / 2), iconSize * 0.6f, iconColor);
    }

    /// <summary>
    /// Get color for a feature icon.
    /// </summary>
    private static Color GetIconColor(string icon)
    {
        return icon switch
        {
            "local_fire_department" => UIColors.FireOrange,
            "fireplace" => new Color(160, 96, 48, 255),
            "water_drop" => new Color(144, 208, 224, 255),
            "check_circle" => new Color(224, 160, 48, 255),
            "warning" => UIColors.Danger,
            "restaurant" => new Color(180, 150, 120, 255),
            "construction" => new Color(150, 150, 150, 255),
            _ => new Color(200, 200, 200, 255)
        };
    }

    /// <summary>
    /// Draw an animal icon on a tile.
    /// </summary>
    public static void DrawAnimalIcon(float centerX, float centerY, float tileSize, string emoji, int position)
    {
        // Position animals around the tile edges
        float offset = tileSize * 0.35f;
        float iconX = centerX + position switch
        {
            0 => 0,       // North
            1 => offset,  // East
            2 => 0,       // South
            3 => -offset, // West
            _ => 0
        };
        float iconY = centerY + position switch
        {
            0 => -offset,
            1 => 0,
            2 => offset,
            3 => 0,
            _ => 0
        };

        float iconSize = tileSize * 0.12f;

        // Shadow
        var shadowColor = new Color(0, 0, 0, 80);
        Raylib.DrawCircle((int)(iconX + 1), (int)(iconY + 1), iconSize, shadowColor);

        // Icon background
        var bgColor = new Color(60, 60, 60, 200);
        Raylib.DrawCircle((int)iconX, (int)iconY, iconSize, bgColor);

        // Placeholder - actual emoji rendering would need font support
        var animalColor = new Color(200, 180, 160, 255);
        Raylib.DrawCircle((int)iconX, (int)iconY, iconSize * 0.7f, animalColor);
    }
}
