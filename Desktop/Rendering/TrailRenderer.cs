using System.Numerics;
using Raylib_cs;
using text_survival.Actions;
using text_survival.Environments.Grid;

namespace text_survival.Desktop.Rendering;

/// <summary>Connected worn earth, with shared boundary anchors and world-seeded surface detail.</summary>
public static class TrailRenderer
{
    private static readonly (int X, int Y)[] Directions = [(0, -1), (1, 0), (0, 1), (-1, 0)];
    private readonly record struct Exit(Vector2 Point, Vector2 Outward, TrailTier Tier);
    internal readonly record struct Strip(Vector2[] Points, float[] Widths, TrailTier Tier, int Seed);

    public static void Render(GameContext ctx, Camera camera, float timeFactor)
    {
        var map = ctx.Map!;
        var strips = new List<Strip>();
        float pitch = camera.TileSize + camera.TileGap;
        foreach (var (x, y) in camera.GetVisibleTiles())
        {
            if (!map.IsValidPosition(x, y) || map.GetVisibility(x, y) == TileVisibility.Unexplored)
                continue;

            var tiers = new TrailTier[4];
            for (int d = 0; d < Directions.Length; d++)
            {
                var (dx, dy) = Directions[d];
                int nx = x + dx, ny = y + dy;
                if (map.IsValidPosition(nx, ny) && map.GetVisibility(nx, ny) != TileVisibility.Unexplored)
                    tiers[d] = map.GetTrailTier(new GridPosition(x, y), new GridPosition(nx, ny));
            }
            var center = camera.WorldToScreen(x, y) + new Vector2(camera.TileSize / 2f);
            strips.AddRange(BuildTile(x, y, center, pitch, tiers));
        }
        Draw(strips, pitch, timeFactor);
    }

    // Each boundary is owned by a canonical world coordinate. Its position and width
    // agree on both tiles, including the gap between tiles and mixed-tier routes.
    internal static List<Strip> BuildTile(int x, int y, Vector2 center, float pitch, TrailTier[] tiers)
    {
        var exits = new List<Exit>(4);
        for (int d = 0; d < Directions.Length; d++)
        {
            if (tiers[d] == TrailTier.None) continue;
            var (dx, dy) = Directions[d];
            bool horizontal = dx != 0;
            float offset = (Noise(x + Math.Max(dx, 0), y + Math.Max(dy, 0), horizontal ? 11 : 29) - 0.5f) * 0.14f;
            var outward = new Vector2(dx, dy);
            var across = horizontal ? Vector2.UnitY : Vector2.UnitX;
            exits.Add(new Exit(center + (outward * 0.5f + across * offset) * pitch, outward, tiers[d]));
        }

        var result = new List<Strip>();
        if (exits.Count == 0) return result;
        var hub = center + new Vector2(Noise(x, y, 41) - 0.5f, Noise(x, y, 53) - 0.5f) * pitch * 0.12f;
        if (exits.Count == 2)
        {
            var a = exits[0];
            var b = exits[1];
            // Inward control handles give both sides of every tile boundary the same
            // tangent, and round a corner instead of making a right-angle elbow.
            result.Add(MakeStrip(a.Point, a.Point - a.Outward * pitch * 0.32f,
                b.Point - b.Outward * pitch * 0.32f, b.Point,
                Width(a.Tier), Width(b.Tier), (TrailTier)Math.Max((int)a.Tier, (int)b.Tier), x, y, pitch, 0));
        }
        else
        {
            float hubWidth = exits.Max(e => Width(e.Tier));
            for (int i = 0; i < exits.Count; i++)
            {
                var e = exits[i];
                result.Add(MakeStrip(e.Point, e.Point - e.Outward * pitch * 0.23f,
                    hub + e.Outward * pitch * 0.10f, hub,
                    Width(e.Tier), exits.Count == 1 ? Width(e.Tier) * 0.22f : hubWidth,
                    e.Tier, x, y, pitch, i));
            }
        }
        return result;
    }

    private static float Width(TrailTier tier) => tier switch
    {
        TrailTier.Trail => 0.115f,
        TrailTier.Path => 0.073f,
        _ => 0.035f
    };

    private static Strip MakeStrip(Vector2 a, Vector2 b, Vector2 c, Vector2 d,
        float startWidth, float endWidth, TrailTier tier, int x, int y, float pitch, int branch)
    {
        const int steps = 24;
        var points = new Vector2[steps + 1];
        var widths = new float[steps + 1];
        int seed = unchecked(x * 73856093 ^ y * 19349663 ^ branch * 83492791);
        for (int i = 0; i <= steps; i++)
        {
            float t = (float)i / steps, u = 1 - t;
            points[i] = u * u * u * a + 3 * u * u * t * b + 3 * u * t * t * c + t * t * t * d;
            // Width variation dies out at shared endpoints so seams stay invisible.
            float roughness = (Noise(seed, i, 71) - 0.5f) * 0.24f * MathF.Sin(t * MathF.PI);
            widths[i] = (startWidth * u + endWidth * t) * pitch * (1 + roughness);
        }
        return new Strip(points, widths, tier, seed);
    }

    internal static void Draw(IReadOnlyList<Strip> strips, float pitch, float timeFactor)
    {
        float light = 0.4f + Math.Clamp(timeFactor, 0, 1) * 0.6f;
        Color Lit(int r, int g, int b) => new((int)(r * light), (int)(g * light), (int)(b * light), 255);
        // Complete each layer over the whole network before the next. Opaque earth
        // avoids dark overlapping caps at joins and branched intersections.
        for (int layer = 0; layer < 3; layer++)
        {
            foreach (var strip in strips)
            {
                var color = layer switch
                {
                    0 => Lit(100, 94, 69), // irregular trampled vegetation / soil margin
                    1 => Lit(139, 119, 86),
                    _ => Lit(162, 141, 103)
                };
                if (strip.Tier == TrailTier.Trace)
                    color = layer == 0 ? Lit(104, 105, 77) : Lit(125, 120, 88);
                float scale = layer switch { 0 => 1.30f, 1 => 1f, _ => 0.65f };
                for (int i = 0; i < strip.Points.Length - 1; i++)
                {
                    var a = strip.Points[i];
                    var b = strip.Points[i + 1];
                    float width = (strip.Widths[i] + strip.Widths[i + 1]) * 0.5f * scale;
                    Raylib.DrawLineEx(a, b, width, color);
                    Raylib.DrawCircleV(a, strip.Widths[i] * scale * 0.5f, color);
                }
                Raylib.DrawCircleV(strip.Points[^1], strip.Widths[^1] * scale * 0.5f, color);
            }
        }

        foreach (var strip in strips)
        {
            // Small, uneven flecks follow the route; no repeated dotted centerline.
            for (int i = 2; i < strip.Points.Length - 2; i += 2)
            {
                float n = Noise(strip.Seed, i, 97);
                var tangent = Vector2.Normalize(strip.Points[i + 1] - strip.Points[i - 1]);
                var normal = new Vector2(-tangent.Y, tangent.X);
                var p = strip.Points[i] + normal * ((n - 0.5f) * strip.Widths[i] * 0.85f);
                var color = n > 0.55f ? Lit(183, 162, 121) : Lit(119, 103, 77);
                Raylib.DrawLineEx(p, p + tangent * pitch * (0.008f + n * 0.014f),
                    pitch * 0.009f, color);
                if (n > 0.58f)
                {
                    float side = Noise(strip.Seed, i, 113) > 0.5f ? 1 : -1;
                    var verge = strip.Points[i] + normal * strip.Widths[i] * side * (0.62f + n * 0.14f);
                    Raylib.DrawCircleV(verge, pitch * 0.006f, Lit(139, 126, 96));
                }
            }
        }
    }

    private static float Noise(int x, int y, int salt)
    {
        uint h = unchecked((uint)(x * 73856093 ^ y * 19349663 ^ salt * 83492791));
        h ^= h >> 16;
        h *= 0x7feb352d;
        h ^= h >> 15;
        return (h & 0xffff) / 65535f;
    }
}
