using System.Numerics;
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

    /// <summary>
    /// Draw a polygon from a list of points.
    /// Renders as a triangle fan from the first vertex.
    /// </summary>
    public static void DrawPolygon(Vector2[] points, Color color)
    {
        if (points.Length < 3) return;

        // Draw as triangle fan from first vertex
        for (int i = 1; i < points.Length - 1; i++)
        {
            Raylib.DrawTriangle(points[0], points[i], points[i + 1], color);
        }
    }

    /// <summary>
    /// Draw a quadrilateral (4-sided polygon).
    /// Points should be in order (clockwise or counter-clockwise).
    /// </summary>
    public static void DrawQuadrilateral(Vector2 p1, Vector2 p2, Vector2 p3, Vector2 p4, Color color)
    {
        Raylib.DrawTriangle(p1, p2, p3, color);
        Raylib.DrawTriangle(p1, p3, p4, color);
    }

    /// <summary>
    /// Draw a rotated ellipse.
    /// Approximates rotation by drawing a polygon with rotated vertices.
    /// </summary>
    public static void DrawRotatedEllipse(float cx, float cy, float radiusX, float radiusY, float rotationRadians, Color color, int segments = 16)
    {
        Vector2[] points = new Vector2[segments];
        float angleStep = MathF.PI * 2 / segments;

        for (int i = 0; i < segments; i++)
        {
            float angle = i * angleStep;
            // Point on unrotated ellipse
            float x = radiusX * MathF.Cos(angle);
            float y = radiusY * MathF.Sin(angle);

            // Rotate the point
            float cos = MathF.Cos(rotationRadians);
            float sin = MathF.Sin(rotationRadians);
            float rotatedX = x * cos - y * sin;
            float rotatedY = x * sin + y * cos;

            points[i] = new Vector2(cx + rotatedX, cy + rotatedY);
        }

        DrawPolygon(points, color);
    }

    /// <summary>
    /// Draw a cubic bezier curve approximated by line segments.
    /// P0 = start, P1/P2 = control points, P3 = end.
    /// </summary>
    public static void DrawBezierCurve(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, Color color, int thickness, int segments = 12)
    {
        Vector2 prev = p0;

        for (int i = 1; i <= segments; i++)
        {
            float t = (float)i / segments;
            float t2 = t * t;
            float t3 = t2 * t;
            float mt = 1 - t;
            float mt2 = mt * mt;
            float mt3 = mt2 * mt;

            // Cubic bezier formula: B(t) = (1-t)³P₀ + 3(1-t)²tP₁ + 3(1-t)t²P₂ + t³P₃
            float x = mt3 * p0.X + 3 * mt2 * t * p1.X + 3 * mt * t2 * p2.X + t3 * p3.X;
            float y = mt3 * p0.Y + 3 * mt2 * t * p1.Y + 3 * mt * t2 * p2.Y + t3 * p3.Y;

            Vector2 current = new(x, y);
            Raylib.DrawLineEx(prev, current, thickness, color);
            prev = current;
        }
    }

    /// <summary>
    /// Draw a quadratic mound shape (smooth hill curve).
    /// Creates a parabolic arc from startX to endX at baseY, peaking at peakY.
    /// </summary>
    public static void DrawQuadraticMound(float startX, float endX, float baseY, float peakY, Color color, int segments = 16)
    {
        Vector2[] points = new Vector2[segments + 2];

        // Start at base left
        points[0] = new Vector2(startX, baseY);

        // Build curve points
        for (int i = 0; i <= segments; i++)
        {
            float t = (float)i / segments;
            float x = startX + (endX - startX) * t;

            // Quadratic curve: y = baseY - (peakY - baseY) * (4 * t * (1 - t))
            // This gives smooth arc peaking at t=0.5
            float height = (peakY - baseY) * 4 * t * (1 - t);
            float y = baseY - height;

            points[i + 1] = new Vector2(x, y);
        }

        // End at base right (close the shape)
        points[segments + 1] = new Vector2(endX, baseY);

        DrawPolygon(points, color);
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
