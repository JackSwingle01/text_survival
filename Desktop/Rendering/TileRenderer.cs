using Raylib_cs;
using text_survival.Environments.Grid;

namespace text_survival.Desktop.Rendering;

/// <summary>
/// Renders individual tiles with terrain, fog of war, and highlights.
/// </summary>
public static class TileRenderer
{
    /// <summary>Male and female art in four cloak colors; an NPC's name picks one.</summary>
    private const int NpcVariantCount = 4;

    /// <summary>Near-black. Ground the player has never laid eyes on, and off-map.</summary>
    private static readonly Color UnexploredColor = new(8, 10, 12, 255);

    private static Texture2D _playerSprite;
    private static readonly Dictionary<TerrainType, List<Texture2D>> _tileSprites = new();
    private static readonly Dictionary<string, Texture2D> _npcSprites = new();

    /// <summary>
    /// Load sprite textures from assets/icons/. Call after the Raylib window is initialized.
    /// Loads player.png, npc/*.png, terrain tiles, and (via AnimalRenderer) animal sprites.
    ///
    /// A terrain may supply several tiles - forest_tile.png, forest_tile2.png and so on.
    /// They are all filed under the same terrain and chosen between per map position,
    /// so a field of one terrain does not read as a stamped grid.
    /// </summary>
    private static IconRenderer? _iconRenderer;

    public static void LoadSprites()
    {
        string assetsPath = AssetPaths.Icons();

        _iconRenderer = new IconRenderer(assetsPath);

        _playerSprite = LoadPixelTexture(Path.Combine(assetsPath, "player.png"))
            ?? throw new FileNotFoundException($"Player sprite not found: {Path.Combine(assetsPath, "player.png")}");

        // Ordinal sort, so variant order - and therefore which tile a position gets - is
        // identical on every machine. It also keeps the unsuffixed base tile at index 0,
        // which VariantIndex weights most heavily; a culture-aware sort does not.
        foreach (string filePath in Directory.GetFiles(assetsPath, "*_tile*.png").Order(StringComparer.Ordinal))
        {
            string fileName = Path.GetFileNameWithoutExtension(filePath);
            string terrainName = fileName[..fileName.IndexOf("_tile", StringComparison.Ordinal)];

            if (!Enum.TryParse(terrainName, ignoreCase: true, out TerrainType terrain))
            {
                Console.Error.WriteLine($"TileRenderer: '{filePath}' does not match any TerrainType, skipping.");
                continue;
            }

            Texture2D? texture = LoadPixelTexture(filePath);
            if (texture == null) continue;

            if (!_tileSprites.TryGetValue(terrain, out var variants))
                _tileSprites[terrain] = variants = [];
            variants.Add(texture.Value);
        }

        var missingTerrain = Enum.GetValues<TerrainType>().Where(t => !_tileSprites.ContainsKey(t)).ToList();
        if (missingTerrain.Count > 0)
            Console.Error.WriteLine($"TileRenderer: no tile art for {string.Join(", ", missingTerrain)}.");

        string npcPath = Path.Combine(assetsPath, "npc");
        if (!Directory.Exists(npcPath))
            throw new DirectoryNotFoundException($"NPC sprites directory not found: {npcPath}");

        foreach (string filePath in Directory.GetFiles(npcPath, "*.png"))
        {
            Texture2D? texture = LoadPixelTexture(filePath);
            if (texture != null)
                _npcSprites[Path.GetFileNameWithoutExtension(filePath).ToLowerInvariant()] = texture.Value;
        }

        AnimalRenderer.LoadSprites(assetsPath);
    }

    private static Texture2D? LoadPixelTexture(string filePath)
    {
        Texture2D texture = Raylib.LoadTexture(filePath);
        if (texture.Id == 0)
        {
            Console.Error.WriteLine($"TileRenderer: failed to load '{filePath}'.");
            return null;
        }

        // Pixel art must not be smoothed when scaled up.
        Raylib.SetTextureFilter(texture, TextureFilter.Point);
        return texture;
    }

    /// <summary>
    /// Which variant a map position gets. Deterministic in world position - never
    /// per-frame random, or tiles would shimmer as the camera moves. The base variant
    /// is weighted to appear half the time and the rest share the remainder, so a field
    /// reads as ground with occasional incident rather than as noise.
    ///
    /// Mirrored by VariantIndex in tools/PixelArtCli, which previews the tiling.
    /// </summary>
    private static int VariantIndex(int worldX, int worldY, int count)
    {
        if (count <= 1) return 0;

        unchecked
        {
            int h = worldX * 73856093 ^ worldY * 19349663;
            h ^= h >> 13;
            h *= 1274126177;
            h ^= h >> 16;

            int roll = (int)((uint)h % (uint)(2 * (count - 1)));
            return roll < count - 1 ? 0 : roll - (count - 2);
        }
    }

    /// <summary>
    /// Draw the ground for one terrain square. The world map and the combat grid both
    /// go through here, so the ground you fight on is the ground you walked onto.
    /// </summary>
    /// <param name="timeFactor">0 at midnight, 1 at noon; dims the tile toward night.</param>
    public static void DrawTerrain(TerrainType terrain, float x, float y, float size, int worldX, int worldY, float timeFactor)
    {
        if (!_tileSprites.TryGetValue(terrain, out var variants))
            return;

        Texture2D tile = variants[VariantIndex(worldX, worldY, variants.Count)];

        byte brightness = (byte)(255 * (0.4f + timeFactor * 0.6f));
        var tint = new Color(brightness, brightness, brightness, (byte)255);

        var source = new Rectangle(0, 0, tile.Width, tile.Height);
        Raylib.DrawTexturePro(tile, source, new Rectangle(x, y, size, size),
            System.Numerics.Vector2.Zero, 0f, tint);
    }

    /// <summary>
    /// Render a single map tile: ground, fog of war, highlight, border.
    /// </summary>
    public static void RenderTile(
        float x, float y, float size,
        int worldX, int worldY,
        TerrainType terrain,
        TileVisibility visibility,
        bool isPlayerTile,
        bool isHovered,
        bool isAdjacent,
        float timeFactor)
    {
        if (visibility == TileVisibility.Unexplored)
        {
            DrawUnexplored(x, y, size);
            return;
        }

        DrawTerrain(terrain, x, y, size, worldX, worldY, timeFactor);

        // Fog of war for explored but not currently visible tiles
        if (visibility == TileVisibility.Explored)
            Raylib.DrawRectangle((int)x, (int)y, (int)size, (int)size, new Color(0, 0, 0, 160));

        if (isPlayerTile)
            DrawPlayerTileHighlight(x, y, size);
        else if (isHovered)
            DrawHoverHighlight(x, y, size);
        else if (isAdjacent)
            DrawAdjacentHighlight(x, y, size);

        DrawTileBorder(x, y, size, visibility == TileVisibility.Visible);
    }

    /// <summary>
    /// Fill a square with the unexplored color - never-seen tiles and off-map squares.
    /// </summary>
    public static void DrawUnexplored(float x, float y, float size)
    {
        Raylib.DrawRectangle((int)x, (int)y, (int)size, (int)size, UnexploredColor);
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
        DrawSprite(_playerSprite, centerX, centerY, tileSize, scale);
    }

    private static void DrawSprite(Texture2D texture, float centerX, float centerY, float tileSize, float scale)
    {
        float spriteHeight = tileSize * 0.55f * scale;
        float spriteWidth = spriteHeight * texture.Width / texture.Height;

        var source = new Rectangle(0, 0, texture.Width, texture.Height);
        var dest = new Rectangle(centerX - spriteWidth / 2, centerY - spriteHeight / 2, spriteWidth, spriteHeight);

        Raylib.DrawTexturePro(texture, source, dest, System.Numerics.Vector2.Zero, 0f, Color.White);
    }

    /// <summary>
    /// Draw a feature icon in one of the tile's four corner slots.
    /// </summary>
    public static void DrawFeatureIcon(float x, float y, float tileSize, string icon, int slot, Color? glow = null)
    {
        var iconRenderer = _iconRenderer
            ?? throw new InvalidOperationException("Feature icons were drawn before TileRenderer.LoadSprites ran.");

        float iconSize = tileSize * 0.3f;
        float margin = tileSize * 0.1f;

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

        iconRenderer.DrawIcon(icon, iconX, iconY, iconSize, glow);
    }

    /// <summary>
    /// Draw an NPC icon on a tile.
    /// </summary>
    public static void DrawNPCIcon(float centerX, float centerY, float tileSize, string name)
    {
        // Offset slightly from center so NPCs don't overlap with player
        float offsetX = tileSize * 0.15f;

        // 80% scale compared to player
        DrawNPCSprite(centerX + offsetX, centerY, tileSize, name, 0.8f);
    }

    /// <summary>
    /// Draw an NPC, its appearance derived from its name so the same person looks the
    /// same wherever they are drawn. Shared by the map and the combat grid so the two
    /// cannot drift apart.
    /// </summary>
    public static void DrawNPCSprite(float centerX, float centerY, float tileSize, string name, float scale)
    {
        int hash = StableHash(name);
        bool isFemale = hash / NpcVariantCount % 2 == 1;
        string key = $"{(isFemale ? "female" : "male")}_{hash % NpcVariantCount}";

        if (!_npcSprites.TryGetValue(key, out Texture2D sprite))
        {
            Console.Error.WriteLine($"TileRenderer: no NPC sprite '{key}' - {name} not drawn.");
            return;
        }

        DrawSprite(sprite, centerX, centerY, tileSize, scale);
    }

    /// <summary>
    /// FNV-1a over the characters. string.GetHashCode() is randomised per process on
    /// .NET Core, so using it here would give an NPC a different face every launch -
    /// the opposite of the "consistent appearance per NPC" this is for.
    /// </summary>
    private static int StableHash(string text)
    {
        unchecked
        {
            uint hash = 2166136261;
            foreach (char c in text)
            {
                hash ^= c;
                hash *= 16777619;
            }
            return (int)(hash & 0x7FFFFFFF);
        }
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

        AnimalRenderer.DrawAnimal(animalType, iconX, iconY, animalSize);
    }
}
