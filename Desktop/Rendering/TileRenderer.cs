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
    /// Draw a feature icon on a tile as a text badge.
    /// </summary>
    public static void DrawFeatureIcon(float x, float y, float tileSize, string icon, int slot, bool hasGlow = false)
    {
        // Get label and color for this icon
        string label = GetIconLabel(icon);
        var iconColor = GetIconColor(icon);

        // Calculate badge dimensions based on label length
        int fontSize = Math.Max(8, (int)(tileSize * 0.12f));
        int textWidth = Raylib.MeasureText(label, fontSize);
        float badgeWidth = textWidth + 6;
        float badgeHeight = fontSize + 4;
        float margin = tileSize * 0.08f;

        // Calculate position based on slot (0-3 for corners)
        float badgeX = slot switch
        {
            0 => x + margin,                           // Top-left
            1 => x + tileSize - margin - badgeWidth,   // Top-right
            2 => x + margin,                           // Bottom-left
            3 => x + tileSize - margin - badgeWidth,   // Bottom-right
            _ => x + tileSize / 2 - badgeWidth / 2
        };

        float badgeY = slot switch
        {
            0 or 1 => y + margin,
            2 or 3 => y + tileSize - margin - badgeHeight,
            _ => y + tileSize / 2 - badgeHeight / 2
        };

        // Draw glow if requested
        if (hasGlow)
        {
            var glowColor = new Color(iconColor.R, iconColor.G, iconColor.B, (byte)60);
            float glowPadding = 4;
            Raylib.DrawRectangleRounded(
                new Rectangle(badgeX - glowPadding, badgeY - glowPadding, badgeWidth + glowPadding * 2, badgeHeight + glowPadding * 2),
                0.3f, 4, glowColor);
        }

        // Draw badge background (dark, semi-transparent, rounded)
        var bgColor = new Color(20, 20, 20, 200);
        Raylib.DrawRectangleRounded(new Rectangle(badgeX, badgeY, badgeWidth, badgeHeight), 0.3f, 4, bgColor);

        // Draw colored left edge for quick visual recognition
        var accentColor = new Color(iconColor.R, iconColor.G, iconColor.B, (byte)180);
        Raylib.DrawRectangle((int)badgeX, (int)(badgeY + 2), 2, (int)(badgeHeight - 4), accentColor);

        // Draw label text
        int textX = (int)(badgeX + 4);
        int textY = (int)(badgeY + 2);
        Raylib.DrawText(label, textX, textY, fontSize, iconColor);
    }

    /// <summary>
    /// Get short label for a feature icon.
    /// </summary>
    private static string GetIconLabel(string icon)
    {
        return icon switch
        {
            "local_fire_department" => "FIRE",
            "fireplace" => "EMBR",
            "water_drop" => "H2O",
            "check_circle" => "TRAP!",
            "circle" => "TRAP",
            "cabin" => "SHLT",
            "ac_unit" => "SNOW",
            "inventory_2" => "STASH",
            "restaurant" => "MEAT",
            "construction" => "WIP",
            "bed" => "BED",
            "nutrition" => "HARV",
            "warning" => "!!!",
            "person_off" => "BODY",
            "search" => "LOOT",
            "done_all" => "DONE",
            "timelapse" => "CURE",
            "pets" => "GAME",
            "cruelty_free" => "GAME",
            _ => DeriveLabel(icon)
        };
    }

    /// <summary>
    /// Derive a readable label from an icon name.
    /// Takes first word (before underscore), uppercases, max 4 chars.
    /// </summary>
    private static string DeriveLabel(string icon)
    {
        var firstWord = icon.Split('_')[0];
        return firstWord.ToUpper()[..Math.Min(4, firstWord.Length)];
    }

    /// <summary>
    /// Get color for a feature icon.
    /// </summary>
    private static Color GetIconColor(string icon)
    {
        return icon switch
        {
            "local_fire_department" => UIColors.FireOrange,
            "fireplace" => new Color(160, 96, 48, 255),       // Amber/ember
            "water_drop" => new Color(144, 208, 224, 255),    // Light blue
            "check_circle" => new Color(100, 220, 100, 255),  // Green (catch ready)
            "circle" => new Color(180, 160, 100, 255),        // Tan (trap set)
            "cabin" => new Color(180, 140, 80, 255),          // Warm brown
            "ac_unit" => new Color(200, 220, 240, 255),       // Ice blue
            "inventory_2" => new Color(160, 140, 100, 255),   // Tan/brown
            "restaurant" => new Color(180, 100, 100, 255),    // Meat red
            "construction" => new Color(150, 150, 150, 255),  // Gray
            "bed" => new Color(140, 120, 180, 255),           // Soft purple
            "nutrition" => new Color(120, 180, 100, 255),     // Plant green
            "warning" => UIColors.Danger,                     // Red
            "person_off" => new Color(150, 130, 130, 255),    // Muted gray-brown
            "search" => new Color(200, 180, 100, 255),        // Gold
            "done_all" => new Color(100, 200, 100, 255),      // Green
            "timelapse" => new Color(180, 160, 120, 255),     // Tan
            "pets" => new Color(200, 160, 120, 255),          // Warm tan (predator)
            "cruelty_free" => new Color(160, 180, 140, 255),  // Soft green (prey)
            _ => new Color(200, 200, 200, 255)
        };
    }

    /// <summary>
    /// Draw an NPC icon on a tile.
    /// </summary>
    public static void DrawNPCIcon(float centerX, float centerY, float tileSize, string name)
    {
        float iconSize = tileSize * 0.2f;

        // Offset slightly from center so NPCs don't overlap with player
        float offsetX = tileSize * 0.15f;
        float drawX = centerX + offsetX;
        float drawY = centerY;

        // Shadow
        var shadowColor = new Color(0, 0, 0, 100);
        Raylib.DrawCircle((int)(drawX + 2), (int)(drawY + 2), iconSize, shadowColor);

        // NPC circle (blue-ish to distinguish from player)
        var npcColor = new Color(120, 160, 200, 255);
        Raylib.DrawCircle((int)drawX, (int)drawY, iconSize, npcColor);

        // Inner detail
        var innerColor = new Color(80, 120, 160, 255);
        Raylib.DrawCircle((int)drawX, (int)drawY, iconSize * 0.6f, innerColor);

        // Draw first letter of name
        if (!string.IsNullOrEmpty(name))
        {
            string initial = name[0].ToString().ToUpper();
            int fontSize = Math.Max(8, (int)(iconSize * 1.2f));
            int textWidth = Raylib.MeasureText(initial, fontSize);
            int textX = (int)(drawX - textWidth / 2);
            int textY = (int)(drawY - fontSize / 2);
            Raylib.DrawText(initial, textX, textY, fontSize, Color.White);
        }
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
