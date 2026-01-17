using Raylib_cs;
using System.Numerics;
using text_survival.Environments.Grid;
using text_survival.Desktop;

namespace text_survival.Desktop.Rendering;

/// <summary>
/// Color palette for character sprites.
/// </summary>
public struct CharacterPalette
{
    public Color Cloak;
    public Color CloakHighlight;
    public Color Fur;
    public Color FurMid;
    public Color FurBright;
    public Color Skin;
    public Color Hair;
    public Color Eyes;
}

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
    // Player palette: brown cloak, cream fur, warm skin, dark hair
    private static readonly CharacterPalette PlayerPalette = new()
    {
        Cloak = new Color(139, 90, 43, 255),
        CloakHighlight = new Color(160, 110, 60, 255),
        Fur = new Color(245, 235, 220, 255),
        FurMid = new Color(220, 200, 170, 255),
        FurBright = new Color(255, 250, 240, 255),
        Skin = new Color(222, 184, 135, 255),
        Hair = new Color(60, 40, 30, 255),
        Eyes = new Color(40, 30, 25, 255)
    };

    // NPC palettes: 4 color variants
    private static readonly CharacterPalette[] NpcPalettes =
    [
        // Gray cloak variant
        new()
        {
            Cloak = new Color(100, 100, 110, 255),
            CloakHighlight = new Color(120, 120, 130, 255),
            Fur = new Color(230, 230, 235, 255),
            FurMid = new Color(200, 200, 210, 255),
            FurBright = new Color(250, 250, 255, 255),
            Skin = new Color(210, 175, 130, 255),
            Hair = new Color(80, 60, 50, 255),
            Eyes = new Color(40, 30, 25, 255)
        },
        // Green cloak variant
        new()
        {
            Cloak = new Color(70, 100, 70, 255),
            CloakHighlight = new Color(90, 120, 90, 255),
            Fur = new Color(220, 235, 220, 255),
            FurMid = new Color(190, 210, 190, 255),
            FurBright = new Color(240, 255, 240, 255),
            Skin = new Color(195, 160, 120, 255),
            Hair = new Color(100, 70, 50, 255),
            Eyes = new Color(40, 30, 25, 255)
        },
        // Purple cloak variant
        new()
        {
            Cloak = new Color(90, 70, 100, 255),
            CloakHighlight = new Color(110, 90, 120, 255),
            Fur = new Color(235, 225, 240, 255),
            FurMid = new Color(210, 195, 220, 255),
            FurBright = new Color(250, 245, 255, 255),
            Skin = new Color(230, 190, 145, 255),
            Hair = new Color(50, 35, 30, 255),
            Eyes = new Color(40, 30, 25, 255)
        },
        // Blue cloak variant
        new()
        {
            Cloak = new Color(70, 90, 120, 255),
            CloakHighlight = new Color(90, 110, 140, 255),
            Fur = new Color(225, 235, 245, 255),
            FurMid = new Color(195, 210, 225, 255),
            FurBright = new Color(245, 250, 255, 255),
            Skin = new Color(215, 180, 140, 255),
            Hair = new Color(70, 50, 40, 255),
            Eyes = new Color(40, 30, 25, 255)
        }
    ];

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
        DrawCharacterMale(centerX, centerY, tileSize, 1.0f, PlayerPalette);
    }

    /// <summary>
    /// Draw a hooded figure silhouette (cloaked person).
    /// </summary>
    private static void DrawHoodedFigure(
        float centerX, float centerY, float tileSize,
        float scale, Color bodyColor, Color headColor, Color shadowColor)
    {
        // Proportions relative to tile size
        float figureHeight = tileSize * 0.30f * scale;
        float headRadius = tileSize * 0.06f * scale;
        float bodyWidth = tileSize * 0.18f * scale;
        float bodyHeight = tileSize * 0.18f * scale;
        float shadowOffset = 2f * scale;

        // Position: center the figure, head at top
        float headY = centerY - figureHeight * 0.35f;
        float bodyTopY = headY + headRadius * 0.5f;
        float bodyBottomY = bodyTopY + bodyHeight;

        // Shadow (draw entire figure offset)
        // Shadow body (triangle)
        Vector2 shadowP1 = new(centerX + shadowOffset, bodyTopY + shadowOffset);
        Vector2 shadowP2 = new(centerX - bodyWidth / 2 + shadowOffset, bodyBottomY + shadowOffset);
        Vector2 shadowP3 = new(centerX + bodyWidth / 2 + shadowOffset, bodyBottomY + shadowOffset);
        Raylib.DrawTriangle(shadowP1, shadowP3, shadowP2, shadowColor);
        // Shadow head
        Raylib.DrawCircle((int)(centerX + shadowOffset), (int)(headY + shadowOffset), headRadius, shadowColor);
        // Shadow hood
        Raylib.DrawCircle((int)(centerX + shadowOffset), (int)(headY + headRadius * 0.3f + shadowOffset), headRadius * 1.2f, shadowColor);

        // Hood (slightly larger circle behind and below head)
        var hoodColor = new Color(bodyColor.R, bodyColor.G, bodyColor.B, (byte)(bodyColor.A * 0.8f));
        Raylib.DrawCircle((int)centerX, (int)(headY + headRadius * 0.3f), headRadius * 1.2f, hoodColor);

        // Body/Cloak (triangle pointing up)
        Vector2 p1 = new(centerX, bodyTopY);           // Top center (shoulders)
        Vector2 p2 = new(centerX - bodyWidth / 2, bodyBottomY);  // Bottom left
        Vector2 p3 = new(centerX + bodyWidth / 2, bodyBottomY);  // Bottom right
        Raylib.DrawTriangle(p1, p3, p2, bodyColor);

        // Head
        Raylib.DrawCircle((int)centerX, (int)headY, headRadius, headColor);

        // Hood outline (arc effect using line segments)
        var outlineColor = new Color(
            (byte)Math.Max(0, bodyColor.R - 40),
            (byte)Math.Max(0, bodyColor.G - 40),
            (byte)Math.Max(0, bodyColor.B - 40),
            bodyColor.A);
        float arcRadius = headRadius * 1.3f;
        float arcY = headY + headRadius * 0.2f;
        // Draw a simple arc using line segments
        for (int i = 0; i < 8; i++)
        {
            float angle1 = (float)(Math.PI * 0.7 + i * Math.PI * 0.2 / 8);
            float angle2 = (float)(Math.PI * 0.7 + (i + 1) * Math.PI * 0.2 / 8);
            float x1 = centerX + (float)Math.Cos(angle1) * arcRadius;
            float y1 = arcY - (float)Math.Sin(angle1) * arcRadius;
            float x2 = centerX + (float)Math.Cos(angle2) * arcRadius;
            float y2 = arcY - (float)Math.Sin(angle2) * arcRadius;
            Raylib.DrawLine((int)x1, (int)y1, (int)x2, (int)y2, outlineColor);
        }
        // Right side of hood arc
        for (int i = 0; i < 8; i++)
        {
            float angle1 = (float)(Math.PI * 0.1 + i * Math.PI * 0.2 / 8);
            float angle2 = (float)(Math.PI * 0.1 + (i + 1) * Math.PI * 0.2 / 8);
            float x1 = centerX + (float)Math.Cos(angle1) * arcRadius;
            float y1 = arcY - (float)Math.Sin(angle1) * arcRadius;
            float x2 = centerX + (float)Math.Cos(angle2) * arcRadius;
            float y2 = arcY - (float)Math.Sin(angle2) * arcRadius;
            Raylib.DrawLine((int)x1, (int)y1, (int)x2, (int)y2, outlineColor);
        }
    }

    /// <summary>
    /// Draw a male character with detailed parka sprite.
    /// </summary>
    private static void DrawCharacterMale(
        float centerX, float centerY, float tileSize,
        float scale, CharacterPalette palette)
    {
        // Scale factor: maps mockup's ~100px height to ~30% of tile
        float s = tileSize * 0.003f * scale;

        // Figure bottom is at centerY + figureHeight/2
        // Mockup uses y as bottom-center origin, offsets are upward (negative)
        float bottomY = centerY + 50 * s;

        // Shadow
        float shadowOffset = 2 * s;
        var shadowColor = new Color(0, 0, 0, 80);

        // 1. Shadow for parka body
        Raylib.DrawRectangleRounded(
            new Rectangle(centerX - 22 * s + shadowOffset, bottomY - 70 * s + shadowOffset, 44 * s, 70 * s),
            0.3f, 6, shadowColor);

        // 2. Parka body (rounded rectangle)
        Raylib.DrawRectangleRounded(
            new Rectangle(centerX - 22 * s, bottomY - 70 * s, 44 * s, 70 * s),
            0.3f, 6, palette.Cloak);

        // 3. Parka highlight (left-side shading)
        Raylib.DrawRectangleRounded(
            new Rectangle(centerX - 18 * s, bottomY - 65 * s, 12 * s, 55 * s),
            0.3f, 4, palette.CloakHighlight);

        // 4. Fur ruff (3 ellipses around neck - light, mid, bright)
        // Back fur (wider, behind face)
        Raylib.DrawEllipse((int)centerX, (int)(bottomY - 72 * s), 20 * s, 12 * s, palette.Fur);
        // Mid fur
        Raylib.DrawEllipse((int)centerX, (int)(bottomY - 74 * s), 18 * s, 10 * s, palette.FurMid);
        // Bright fur (top layer)
        Raylib.DrawEllipse((int)centerX, (int)(bottomY - 76 * s), 16 * s, 8 * s, palette.FurBright);

        // 5. Face (circle for skin)
        float faceY = bottomY - 85 * s;
        Raylib.DrawCircle((int)centerX, (int)faceY, 12 * s, palette.Skin);

        // 6. Hair - short crop for male (ellipse on top of head)
        Raylib.DrawEllipse((int)centerX, (int)(faceY - 8 * s), 11 * s, 7 * s, palette.Hair);

        // 7. Eyes (two small rounded rectangles)
        float eyeY = faceY - 1 * s;
        float eyeSpacing = 5 * s;
        float eyeWidth = 3 * s;
        float eyeHeight = 4 * s;
        Raylib.DrawRectangleRounded(
            new Rectangle(centerX - eyeSpacing - eyeWidth / 2, eyeY - eyeHeight / 2, eyeWidth, eyeHeight),
            0.5f, 4, palette.Eyes);
        Raylib.DrawRectangleRounded(
            new Rectangle(centerX + eyeSpacing - eyeWidth / 2, eyeY - eyeHeight / 2, eyeWidth, eyeHeight),
            0.5f, 4, palette.Eyes);
    }

    /// <summary>
    /// Draw a female character with detailed parka sprite.
    /// </summary>
    private static void DrawCharacterFemale(
        float centerX, float centerY, float tileSize,
        float scale, CharacterPalette palette)
    {
        // Scale factor: maps mockup's ~100px height to ~30% of tile
        float s = tileSize * 0.003f * scale;

        // Figure bottom is at centerY + figureHeight/2
        float bottomY = centerY + 50 * s;

        // Shadow
        float shadowOffset = 2 * s;
        var shadowColor = new Color(0, 0, 0, 80);

        // 1. Shadow for parka body
        Raylib.DrawRectangleRounded(
            new Rectangle(centerX - 22 * s + shadowOffset, bottomY - 70 * s + shadowOffset, 44 * s, 70 * s),
            0.3f, 6, shadowColor);

        // 2. Parka body (rounded rectangle)
        Raylib.DrawRectangleRounded(
            new Rectangle(centerX - 22 * s, bottomY - 70 * s, 44 * s, 70 * s),
            0.3f, 6, palette.Cloak);

        // 3. Parka highlight (left-side shading)
        Raylib.DrawRectangleRounded(
            new Rectangle(centerX - 18 * s, bottomY - 65 * s, 12 * s, 55 * s),
            0.3f, 4, palette.CloakHighlight);

        // 4. Side hair wisps over the collar (drawn before fur so fur overlaps it)
        // Left wisp
        Raylib.DrawEllipse((int)(centerX - 16 * s), (int)(bottomY - 70 * s), 6 * s, 14 * s, palette.Hair);
        // Right wisp
        Raylib.DrawEllipse((int)(centerX + 16 * s), (int)(bottomY - 70 * s), 6 * s, 14 * s, palette.Hair);

        // 5. Fur ruff (3 ellipses around neck - light, mid, bright)
        // Back fur (wider, behind face)
        Raylib.DrawEllipse((int)centerX, (int)(bottomY - 72 * s), 20 * s, 12 * s, palette.Fur);
        // Mid fur
        Raylib.DrawEllipse((int)centerX, (int)(bottomY - 74 * s), 18 * s, 10 * s, palette.FurMid);
        // Bright fur (top layer)
        Raylib.DrawEllipse((int)centerX, (int)(bottomY - 76 * s), 16 * s, 8 * s, palette.FurBright);

        // 6. Face (circle for skin)
        float faceY = bottomY - 85 * s;
        Raylib.DrawCircle((int)centerX, (int)faceY, 12 * s, palette.Skin);

        // 7. Hair - fuller for female with face-framing pieces
        // Main hair volume (larger ellipse)
        Raylib.DrawEllipse((int)centerX, (int)(faceY - 6 * s), 14 * s, 10 * s, palette.Hair);
        // Face-framing pieces (small ellipses on sides)
        Raylib.DrawEllipse((int)(centerX - 10 * s), (int)(faceY + 2 * s), 4 * s, 8 * s, palette.Hair);
        Raylib.DrawEllipse((int)(centerX + 10 * s), (int)(faceY + 2 * s), 4 * s, 8 * s, palette.Hair);

        // 8. Eyes (two small rounded rectangles)
        float eyeY = faceY - 1 * s;
        float eyeSpacing = 5 * s;
        float eyeWidth = 3 * s;
        float eyeHeight = 4 * s;
        Raylib.DrawRectangleRounded(
            new Rectangle(centerX - eyeSpacing - eyeWidth / 2, eyeY - eyeHeight / 2, eyeWidth, eyeHeight),
            0.5f, 4, palette.Eyes);
        Raylib.DrawRectangleRounded(
            new Rectangle(centerX + eyeSpacing - eyeWidth / 2, eyeY - eyeHeight / 2, eyeWidth, eyeHeight),
            0.5f, 4, palette.Eyes);
    }

    /// <summary>
    /// Draw a feature icon on a tile.
    /// Delegates to the configured IconRenderer.
    /// </summary>
    public static void DrawFeatureIcon(float x, float y, float tileSize, string icon, int slot, bool hasGlow = false)
    {
        var iconRenderer = DesktopRuntime.IconRenderer;
        if (iconRenderer == null) return;

        // Get icon color and calculate icon size
        var iconColor = iconRenderer.GetIconColor(icon);
        float iconSize = tileSize * 0.3f;
        float margin = tileSize * 0.1f;

        // Calculate position based on slot (0-3 for corners)
        float iconX = slot switch
        {
            0 => x + margin,                            // Top-left
            1 => x + tileSize - margin - iconSize,      // Top-right
            2 => x + margin,                            // Bottom-left
            3 => x + tileSize - margin - iconSize,      // Bottom-right
            _ => x + tileSize / 2 - iconSize / 2
        };

        float iconY = slot switch
        {
            0 or 1 => y + margin,
            2 or 3 => y + tileSize - margin - iconSize,
            _ => y + tileSize / 2 - iconSize / 2
        };

        // Draw the icon using the renderer
        iconRenderer.DrawIcon(icon, iconX, iconY, iconSize, iconColor, hasGlow);
    }

    /// <summary>
    /// Draw an NPC icon on a tile.
    /// </summary>
    public static void DrawNPCIcon(float centerX, float centerY, float tileSize, string name)
    {
        // Offset slightly from center so NPCs don't overlap with player
        float offsetX = tileSize * 0.15f;
        float drawX = centerX + offsetX;
        float drawY = centerY;

        // Use name hash for consistent appearance per NPC
        int hash = name.GetHashCode();
        int paletteIndex = Math.Abs(hash) % NpcPalettes.Length;
        bool isFemale = (Math.Abs(hash) / NpcPalettes.Length) % 2 == 1;

        CharacterPalette palette = NpcPalettes[paletteIndex];

        // Draw at 80% scale compared to player
        if (isFemale)
            DrawCharacterFemale(drawX, drawY, tileSize, 0.8f, palette);
        else
            DrawCharacterMale(drawX, drawY, tileSize, 0.8f, palette);
    }

    /// <summary>
    /// Draw an animal icon on a tile.
    /// </summary>
    public static void DrawAnimalIcon(float centerX, float centerY, float tileSize, Actors.Animals.AnimalType animalType, int position)
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

        // Scale for animal drawing (sized appropriately for tile)
        float scale = tileSize * 0.01f; // ~12-15% of tile size after animal scaling

        // Shadow (oval underneath the animal)
        var shadowColor = new Color(0, 0, 0, 60);
        Raylib.DrawEllipse((int)(iconX + 1), (int)(iconY + scale * 12), scale * 8, scale * 4, shadowColor);

        // Draw the procedural animal
        AnimalRenderer.DrawAnimal(animalType, iconX, iconY, scale);
    }
}
