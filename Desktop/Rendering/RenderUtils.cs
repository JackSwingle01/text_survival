using Raylib_cs;

namespace text_survival.Desktop.Rendering;

/// <summary>
/// Utility methods for rendering - seeded random, color helpers, etc.
/// </summary>
public static class RenderUtils
{
    /// <summary>
    /// Seeded random for consistent terrain patterns per tile.
    /// Same algorithm as JavaScript version for visual parity.
    /// </summary>
    public static float SeededRandom(int worldX, int worldY, int seed)
    {
        int h = (worldX * 73856093) ^ (worldY * 19349663) ^ (seed * 83492791);
        return MathF.Abs(MathF.Sin(h)) % 1.0f;
    }

    /// <summary>
    /// Parse hex color string to Raylib Color.
    /// Supports #RGB, #RRGGBB, #RRGGBBAA formats.
    /// </summary>
    public static Color ParseHex(string hex)
    {
        if (hex.StartsWith('#'))
            hex = hex[1..];

        return hex.Length switch
        {
            3 => new Color(
                Convert.ToByte(new string(hex[0], 2), 16),
                Convert.ToByte(new string(hex[1], 2), 16),
                Convert.ToByte(new string(hex[2], 2), 16),
                (byte)255),
            6 => new Color(
                Convert.ToByte(hex[0..2], 16),
                Convert.ToByte(hex[2..4], 16),
                Convert.ToByte(hex[4..6], 16),
                (byte)255),
            8 => new Color(
                Convert.ToByte(hex[0..2], 16),
                Convert.ToByte(hex[2..4], 16),
                Convert.ToByte(hex[4..6], 16),
                Convert.ToByte(hex[6..8], 16)),
            _ => new Color(128, 128, 128, 255)
        };
    }

    /// <summary>
    /// Create color with alpha from existing color.
    /// </summary>
    public static Color WithAlpha(Color color, byte alpha)
    {
        return new Color(color.R, color.G, color.B, alpha);
    }

    /// <summary>
    /// Create color with alpha from existing color (0-1 float).
    /// </summary>
    public static Color WithAlpha(Color color, float alpha)
    {
        return new Color(color.R, color.G, color.B, (byte)(alpha * 255));
    }

    /// <summary>
    /// Lerp between two colors.
    /// </summary>
    public static Color LerpColor(Color a, Color b, float t)
    {
        t = Math.Clamp(t, 0, 1);
        return new Color(
            (byte)(a.R + (b.R - a.R) * t),
            (byte)(a.G + (b.G - a.G) * t),
            (byte)(a.B + (b.B - a.B) * t),
            (byte)(a.A + (b.A - a.A) * t)
        );
    }

    /// <summary>
    /// Adjust color brightness (HSL lightness).
    /// Factor > 1 brightens, < 1 darkens.
    /// </summary>
    public static Color AdjustBrightness(Color color, float factor)
    {
        int r = Math.Clamp((int)(color.R * factor), 0, 255);
        int g = Math.Clamp((int)(color.G * factor), 0, 255);
        int b = Math.Clamp((int)(color.B * factor), 0, 255);
        return new Color((byte)r, (byte)g, (byte)b, color.A);
    }

    /// <summary>
    /// Ease-out cubic for smooth animations.
    /// </summary>
    public static float EaseOutCubic(float t)
    {
        return 1 - MathF.Pow(1 - t, 3);
    }

    /// <summary>
    /// Ease-in-out cubic for smooth animations.
    /// </summary>
    public static float EaseInOutCubic(float t)
    {
        return t < 0.5f
            ? 4 * t * t * t
            : 1 - MathF.Pow(-2 * t + 2, 3) / 2;
    }
}

/// <summary>
/// Terrain color palette matching the JavaScript version.
/// </summary>
public static class TerrainColors
{
    public static readonly Color Forest = RenderUtils.ParseHex("#2a4038");
    public static readonly Color Clearing = RenderUtils.ParseHex("#d8d8d8");
    public static readonly Color Plain = RenderUtils.ParseHex("#d8d8d8");
    public static readonly Color Hills = RenderUtils.ParseHex("#8090a0");
    public static readonly Color Water = RenderUtils.ParseHex("#90b0c8");
    public static readonly Color Marsh = RenderUtils.ParseHex("#607068");
    public static readonly Color Rock = RenderUtils.ParseHex("#606068");
    public static readonly Color Mountain = RenderUtils.ParseHex("#404048");
    public static readonly Color DeepWater = RenderUtils.ParseHex("#6090b0");
    public static readonly Color Unexplored = RenderUtils.ParseHex("#080a0c");

    /// <summary>
    /// Get terrain color by name.
    /// </summary>
    public static Color GetColor(string terrain)
    {
        return terrain switch
        {
            "Forest" => Forest,
            "Clearing" => Clearing,
            "Plain" => Plain,
            "Hills" => Hills,
            "Water" => Water,
            "Marsh" => Marsh,
            "Rock" => Rock,
            "Mountain" => Mountain,
            "DeepWater" => DeepWater,
            _ => Unexplored
        };
    }
}

/// <summary>
/// UI color palette for the game.
/// </summary>
public static class UIColors
{
    public static readonly Color Midnight = RenderUtils.ParseHex("#0d1114");
    public static readonly Color Panel = RenderUtils.ParseHex("#141a1f");
    public static readonly Color Surface = RenderUtils.ParseHex("#1f2833");
    public static readonly Color BorderDim = new(255, 255, 255, 25);
    public static readonly Color TextDim = new(255, 255, 255, 128);
    public static readonly Color TextPrimary = new(255, 255, 255, 230);
    public static readonly Color FireOrange = RenderUtils.ParseHex("#e08830");
    public static readonly Color TechCyan = RenderUtils.ParseHex("#60a0b0");
    public static readonly Color VitalRed = RenderUtils.ParseHex("#a05050");
    public static readonly Color Danger = RenderUtils.ParseHex("#d84315");
    public static readonly Color Success = RenderUtils.ParseHex("#4caf50");
    public static readonly Color Warning = RenderUtils.ParseHex("#ff9800");
}
