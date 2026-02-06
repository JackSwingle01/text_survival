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
    private static Texture2D? _playerSprite;
    private static readonly Dictionary<string, Texture2D> _tileSprites = new();

    /// <summary>
    /// Load sprite textures from assets/icons/. Call after Raylib window is initialized.
    /// Loads player.png and *_tile.png (e.g. forest_tile.png) files.
    /// </summary>
    public static void LoadSprites()
    {
        string assetsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "assets", "icons");

        if (!Directory.Exists(assetsPath))
            assetsPath = Path.Combine(Directory.GetCurrentDirectory(), "assets", "icons");

        if (!Directory.Exists(assetsPath))
            return;

        string playerPath = Path.Combine(assetsPath, "player.png");
        if (File.Exists(playerPath))
        {
            var texture = Raylib.LoadTexture(playerPath);
            if (texture.Id != 0)
                _playerSprite = texture;
        }

        foreach (string filePath in Directory.GetFiles(assetsPath, "*_tile.png"))
        {
            string fileName = Path.GetFileNameWithoutExtension(filePath);
            string terrainKey = fileName.Replace("_tile", "").ToLowerInvariant();

            var texture = Raylib.LoadTexture(filePath);
            if (texture.Id != 0)
                _tileSprites[terrainKey] = texture;
        }
    }

    /// <summary>
    /// Try to get a loaded tile sprite for a terrain type.
    /// </summary>
    public static bool TryGetTileSprite(string terrain, out Texture2D sprite)
    {
        return _tileSprites.TryGetValue(terrain.ToLowerInvariant(), out sprite);
    }

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
    public static readonly CharacterPalette[] NpcPalettes =
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

        // Draw terrain: use sprite if available, otherwise procedural
        if (_tileSprites.TryGetValue(terrain.ToLowerInvariant(), out var tileSprite))
        {
            float brightness = 0.4f + timeFactor * 0.6f;
            byte b = (byte)(255 * brightness);
            var tint = new Color(b, b, b, (byte)255);

            var source = new Rectangle(0, 0, tileSprite.Width, tileSprite.Height);
            var dest = new Rectangle(x, y, size, size);
            Raylib.DrawTexturePro(tileSprite, source, dest, System.Numerics.Vector2.Zero, 0f, tint);
        }
        else
        {
            Color baseColor = TerrainColors.GetColor(terrain);
            baseColor = AdjustForTimeOfDay(baseColor, timeFactor);
            Raylib.DrawRectangle((int)x, (int)y, (int)size, (int)size, baseColor);
            TerrainRenderer.RenderTexture(terrain, x, y, size, worldX, worldY, timeFactor);
        }

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
    public static void DrawPlayerIcon(float centerX, float centerY, float tileSize, float scale = 1.0f)
    {
        if (_playerSprite.HasValue)
            DrawSprite(_playerSprite.Value, centerX, centerY, tileSize, scale);
        else
            DrawCharacterMale(centerX, centerY, tileSize, scale, PlayerPalette);
    }

    private static void DrawSprite(Texture2D texture, float centerX, float centerY, float tileSize, float scale)
    {
        float spriteHeight = tileSize * 0.55f * scale;
        float aspectRatio = (float)texture.Width / texture.Height;
        float spriteWidth = spriteHeight * aspectRatio;

        float x = centerX - spriteWidth / 2;
        float y = centerY - spriteHeight / 2;

        var source = new Rectangle(0, 0, texture.Width, texture.Height);
        var dest = new Rectangle(x, y, spriteWidth, spriteHeight);

        Raylib.DrawTexturePro(texture, source, dest, System.Numerics.Vector2.Zero, 0f, Color.White);
    }

    /// <summary>
    /// Draw a male character with detailed parka sprite.
    /// </summary>
    public static void DrawCharacterMale(
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
    public static void DrawCharacterFemale(
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
    /// Draw a progress bar with action label above an NPC sprite.
    /// </summary>
    public static void DrawActionProgressBar(float centerX, float centerY, float tileSize, float progress, string actionName)
    {
        // NPC is offset from center horizontally
        float offsetX = tileSize * 0.15f;
        float drawX = centerX + offsetX;

        // NPC sprite top is at approximately centerY - tileSize * 0.12 (for 0.8 scale)
        // Position bar just above that with small margin
        float npcTop = centerY - tileSize * 0.12f;
        float barY = npcTop - tileSize * 0.08f;

        // Bar dimensions (smaller bar)
        float barWidth = tileSize * 0.35f;
        float barHeight = Math.Max(3f, tileSize * 0.035f);
        float barX = drawX - barWidth / 2;

        // Label above bar (smaller font)
        int fontSize = Math.Max(8, (int)(tileSize * 0.09f));
        int textWidth = Raylib.MeasureText(actionName, fontSize);
        float textX = drawX - textWidth / 2;
        float textY = barY - fontSize - 2;

        // Draw label shadow + label
        Raylib.DrawText(actionName, (int)(textX + 1), (int)(textY + 1), fontSize, new Color(0, 0, 0, 180));
        Raylib.DrawText(actionName, (int)textX, (int)textY, fontSize, new Color(255, 255, 255, 230));

        // Draw bar background
        Raylib.DrawRectangle((int)barX, (int)barY, (int)barWidth, (int)barHeight, new Color(40, 40, 40, 180));

        // Draw bar fill
        int fillWidth = (int)(barWidth * Math.Clamp(progress, 0f, 1f));
        if (fillWidth > 0)
            Raylib.DrawRectangle((int)barX, (int)barY, fillWidth, (int)barHeight, new Color(80, 180, 140, 220));

        // Draw bar border
        Raylib.DrawRectangleLines((int)barX, (int)barY, (int)barWidth, (int)barHeight, new Color(60, 60, 60, 150));
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

        // Animal size as percentage of tile
        float animalSize = tileSize * 0.25f;

        // Shadow (oval underneath the animal, proportional to animal size)
        var shadowColor = new Color(0, 0, 0, 60);
        Raylib.DrawEllipse((int)(iconX + 1), (int)(iconY + animalSize * 0.15f), animalSize * 0.1f, animalSize * 0.05f, shadowColor);

        // Draw the procedural animal
        AnimalRenderer.DrawAnimal(animalType, iconX, iconY, animalSize);
    }
}
