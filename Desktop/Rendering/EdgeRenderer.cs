using Raylib_cs;
using System.Numerics;
using text_survival.Actions;
using text_survival.Environments.Grid;

namespace text_survival.Desktop.Rendering;

/// <summary>Continuous river channels and rock escarpments along tile boundaries.</summary>
public static class EdgeRenderer
{
    private static readonly (int X, int Y)[] Directions = [(0, -1), (1, 0), (0, 1), (-1, 0)];
    internal readonly record struct Boundary(Vector2[] Points, float[] Widths, Vector2 Downhill, int Seed,
        (int X, int Y) StartCorner, (int X, int Y) EndCorner);

    public static void RenderEdges(GameContext ctx, Camera camera, float timeFactor)
    {
        var map = ctx.Map ?? throw new InvalidOperationException("Cannot render without an initialized map.");
        var entrances = new List<(Vector2 Center, Vector2 Inward, int Seed)>();
        var ravines = new List<Boundary>();
        var rivers = new List<Boundary>();
        var cliffs = new List<Boundary>();
        var drawn = new HashSet<(int X, int Y, bool Vertical)>();
        float pitch = camera.TileSize + camera.TileGap;

        foreach (var (x, y) in camera.GetVisibleTiles())
        {
            if (!map.IsValidPosition(x, y) || map.GetVisibility(x, y) == TileVisibility.Unexplored)
                continue;
            foreach (var (dx, dy) in Directions)
            {
                int nx = x + dx, ny = y + dy;
                if (!map.IsValidPosition(nx, ny) || map.GetVisibility(nx, ny) == TileVisibility.Unexplored)
                    continue;
                var a = map.GetLocationAt(x, y);
                var b = map.GetLocationAt(nx, ny);
                if (a == null || b == null) continue;

                // The shared boundary is keyed by its top/left grid corner, regardless
                // of which tile sees it first. This also covers viewport fringe edges.
                bool vertical = dx != 0;
                int cornerX = x + Math.Max(dx, 0), cornerY = y + Math.Max(dy, 0);
                if (!drawn.Add((cornerX, cornerY, vertical))) continue;
                var origin = camera.WorldToScreen(cornerX, cornerY) - new Vector2(camera.TileGap / 2f);
                if (map.HasEdgeType(new GridPosition(x, y), new GridPosition(nx, ny), EdgeType.River))
                    rivers.Add(BuildBoundary(cornerX, cornerY, vertical, origin, pitch, true, Vector2.Zero));

                bool mouthA = a.Structure == TileStructure.CaveEntrance && !b.CaveId.HasValue && b.IsPassable;
                bool mouthB = b.Structure == TileStructure.CaveEntrance && !a.CaveId.HasValue && a.IsPassable;
                if (mouthA || mouthB)
                {
                    var center = origin + (vertical ? Vector2.UnitY : Vector2.UnitX) * pitch * .5f;
                    entrances.Add((center, new Vector2(dx, dy) * (mouthB ? 1 : -1),
                        unchecked(cornerX * 73856093 ^ cornerY * 19349663)));
                }
                bool ravine = map.HasEdgeType(new GridPosition(x, y), new GridPosition(nx, ny), EdgeType.Ravine);
                bool cliff = map.HasEdgeType(new GridPosition(x, y), new GridPosition(nx, ny), EdgeType.Cliff);
                if (ravine)
                    ravines.Add(BuildBoundary(cornerX, cornerY, vertical, origin, pitch, false, Vector2.Zero));
                bool highA = IsHigh(map.DisplayTerrain(a)), highB = IsHigh(map.DisplayTerrain(b));
                if (!ravine && (cliff || highA != highB))
                {
                    var downhill = new Vector2(dx, dy) * (highA ? -1 : 1);
                    cliffs.Add(BuildBoundary(cornerX, cornerY, vertical, origin, pitch, false, downhill));
                }
            }
        }

        // Water remains legible where a channel follows the foot of a cliff.
        DrawCliffs(cliffs, pitch, timeFactor);
        DrawRavines(ravines, pitch, timeFactor);
        DrawRivers(RoundRiverBends(rivers), pitch, timeFactor);
        foreach (var entrance in entrances)
            CaveEntranceRenderer.Draw(entrance.Center, entrance.Inward, pitch, timeFactor);
    }

    // Rock is rough ground, not high ground - a boulder field does not sit on a
    // plateau. Only ground that actually stands above its neighbours gets a scarp.
    private static bool IsHigh(TerrainType terrain) =>
        terrain is TerrainType.Mountain or TerrainType.Hills;

    internal static Boundary BuildBoundary(int x, int y, bool vertical, Vector2 origin,
        float pitch, bool river, Vector2 downhill)
    {
        const int steps = 24;
        var direction = vertical ? Vector2.UnitY : Vector2.UnitX;
        var normal = new Vector2(-direction.Y, direction.X);
        int ex = x + (vertical ? 0 : 1), ey = y + (vertical ? 1 : 0);
        Vector2 Corner(int cx, int cy) => new(Noise(cx, cy, 13) - 0.5f, Noise(cx, cy, 31) - 0.5f);
        var start = origin + Corner(x, y) * pitch * 0.045f;
        var end = origin + direction * pitch + Corner(ex, ey) * pitch * 0.045f;
        var points = new Vector2[steps + 1];
        var widths = new float[steps + 1];
        int seed = unchecked(x * 73856093 ^ y * 19349663 ^ (vertical ? 193 : 397));
        float bend = (Noise(x, y, vertical ? 61 : 79) - 0.5f) * 0.12f;
        float baseWidth = river ? 0.135f : 0.12f;
        float startWidth = baseWidth * (0.9f + Noise(x, y, 103) * 0.2f);
        float endWidth = baseWidth * (0.9f + Noise(ex, ey, 103) * 0.2f);
        for (int i = 0; i <= steps; i++)
        {
            float t = (float)i / steps;
            float envelope = MathF.Sin(MathF.PI * t);
            // Position, width and local noise converge at the same world corner for
            // straight runs, turns, and junctions. Detail never depends on the camera.
            float offset = river
                ? bend * envelope * envelope + MathF.Sin(t * MathF.PI * 4) * envelope * 0.009f
                : (Noise(seed, i / 2, 127) - 0.5f) * envelope * 0.04f;
            points[i] = Vector2.Lerp(start, end, t) + normal * offset * pitch;
            float roughness = (Noise(seed, i, 149) - 0.5f) * envelope * (river ? 0.10f : 0.22f);
            widths[i] = (startWidth * (1 - t) + endWidth * t) * pitch * (1 + roughness);
        }
        // Avoid floating-point sine residue at joins.
        points[0] = start;
        points[^1] = end;
        widths[0] = startWidth * pitch;
        widths[^1] = endWidth * pitch;
        return new Boundary(points, widths, downhill, seed, (x, y), (ex, ey));
    }

    /// <summary>Replace two-way corner elbows with curved channel sections.</summary>
    internal static List<Boundary> RoundRiverBends(IReadOnlyList<Boundary> edges)
    {
        var junctions = new Dictionary<(int X, int Y), List<(int Edge, bool Start)>>();
        for (int i = 0; i < edges.Count; i++)
        {
            foreach (bool start in new[] { true, false })
            {
                // Topology uses world corners so subpixel camera movement cannot
                // change whether two channels are recognized as connected.
                var key = start ? edges[i].StartCorner : edges[i].EndCorner;
                if (!junctions.TryGetValue(key, out var connections))
                    junctions[key] = connections = [];
                connections.Add((i, start));
            }
        }
        var first = new int[edges.Count];
        var last = edges.Select(e => e.Points.Length - 1).ToArray();
        var bends = new List<Boundary>();
        foreach (var connections in junctions.Values)
        {
            if (connections.Count != 2) continue;
            var a = connections[0];
            var b = connections[1];
            var edgeA = edges[a.Edge];
            var edgeB = edges[b.Edge];
            int ai = a.Start ? 4 : edgeA.Points.Length - 5;
            int bi = b.Start ? 4 : edgeB.Points.Length - 5;
            var corner = a.Start ? edgeA.Points[0] : edgeA.Points[^1];
            var start = edgeA.Points[ai];
            var end = edgeB.Points[bi];
            if (Vector2.Dot(Vector2.Normalize(start - corner), Vector2.Normalize(end - corner)) < -0.8f)
                continue;
            if (a.Start) first[a.Edge] = ai; else last[a.Edge] = ai;
            if (b.Start) first[b.Edge] = bi; else last[b.Edge] = bi;
            var points = new Vector2[13];
            var widths = new float[13];
            for (int i = 0; i < points.Length; i++)
            {
                float t = i / 12f, u = 1 - t;
                points[i] = u * u * start + 2 * u * t * corner + t * t * end;
                widths[i] = edgeA.Widths[ai] * u + edgeB.Widths[bi] * t;
            }
            var cornerKey = a.Start ? edgeA.StartCorner : edgeA.EndCorner;
            bends.Add(new Boundary(points, widths, Vector2.Zero, edgeA.Seed ^ edgeB.Seed, cornerKey, cornerKey));
        }
        for (int i = 0; i < edges.Count; i++)
            bends.Add(edges[i] with
            {
                Points = edges[i].Points[first[i]..(last[i] + 1)],
                Widths = edges[i].Widths[first[i]..(last[i] + 1)]
            });
        return bends;
    }

    private static Color Lit(int r, int g, int b, float timeFactor)
    {
        float light = 0.4f + Math.Clamp(timeFactor, 0, 1) * 0.6f;
        return new Color((int)(r * light), (int)(g * light), (int)(b * light), 255);
    }

    private static void Ribbon(Boundary edge, float scale, Vector2 offset, Color color)
    {
        for (int i = 0; i < edge.Points.Length - 1; i++)
        {
            var a = edge.Points[i] + offset;
            var b = edge.Points[i + 1] + offset;
            Raylib.DrawLineEx(a, b, (edge.Widths[i] + edge.Widths[i + 1]) * 0.5f * scale, color);
            Raylib.DrawCircleV(a, edge.Widths[i] * scale * 0.5f, color);
        }
        Raylib.DrawCircleV(edge.Points[^1] + offset, edge.Widths[^1] * scale * 0.5f, color);
    }

    internal static void DrawRivers(IReadOnlyList<Boundary> rivers, float pitch, float timeFactor)
    {
        // Draw each layer across the entire network before adding the next so caps
        // merge into a single channel instead of stamping a bank over flowing water.
        (float Scale, int R, int G, int B)[] layers =
        [
            (1.48f, 109, 112, 99), // damp, stony bank
            (1.22f, 169, 188, 186), // pale gravel / ice at the margin
            (1.00f, 87, 145, 165),  // shallow water
            (0.64f, 48, 106, 136),  // deeper central channel
            (0.28f, 58, 121, 149)
        ];
        foreach (var layer in layers)
            foreach (var edge in rivers)
                Ribbon(edge, layer.Scale, Vector2.Zero, Lit(layer.R, layer.G, layer.B, timeFactor));

        foreach (var edge in rivers)
        {
            for (int i = 2; i < edge.Points.Length - 3; i += 3)
            {
                float n = Noise(edge.Seed, i, 173);
                var tangent = Vector2.Normalize(edge.Points[i + 1] - edge.Points[i - 1]);
                var normal = new Vector2(-tangent.Y, tangent.X);
                float side = n > 0.5f ? 1 : -1;
                var offset = normal * edge.Widths[i] * (n - 0.5f) * 0.55f;
                // Short curved glints suggest current, with no repetitive crossbars.
                var glint = Lit(158, 204, 214, timeFactor);
                Raylib.DrawLineEx(edge.Points[i] + offset, edge.Points[i + 1] + offset, pitch * 0.009f, glint);
                if (n > 0.35f)
                    Raylib.DrawLineEx(edge.Points[i + 1] + offset, edge.Points[i + 2] + offset,
                        pitch * 0.006f, glint);
                var bank = edge.Points[i] + normal * side * edge.Widths[i] * 0.64f;
                Raylib.DrawCircleV(bank, pitch * (0.009f + n * 0.008f), Lit(123, 139, 135, timeFactor));
                Raylib.DrawLineEx(bank - tangent * pitch * 0.006f, bank + tangent * pitch * 0.012f,
                    pitch * 0.009f, Lit(202, 215, 205, timeFactor));
            }
        }
    }

    /// <summary>Two opposing rock banks and a recessed fissure along the shared boundary.</summary>
    private static void DrawRavines(IReadOnlyList<Boundary> ravines, float pitch, float timeFactor)
    {
        var banks = new List<Boundary>();
        foreach (var edge in ravines)
        {
            var tangent = Vector2.Normalize(edge.Points[^1] - edge.Points[0]);
            var normal = new Vector2(-tangent.Y, tangent.X);
            foreach (float side in new[] { -1f, 1f })
                banks.Add(edge with
                {
                    Points = edge.Points.Select(p => p + normal * (side * pitch * .065f)).ToArray(),
                    Widths = edge.Widths.Select(w => w * .55f).ToArray(),
                    Downhill = -normal * side
                });
        }
        DrawCliffs(banks, pitch, timeFactor);
        foreach (var edge in ravines)
        {
            Ribbon(edge, .55f, Vector2.Zero, Lit(40, 45, 44, timeFactor));
            Ribbon(edge, .24f, Vector2.Zero, Lit(24, 30, 31, timeFactor));
        }
    }

    internal static void DrawCliffs(IReadOnlyList<Boundary> cliffs, float pitch, float timeFactor)
    {
        for (int layer = 0; layer < 4; layer++)
        {
            foreach (var edge in cliffs)
            {
                var (scale, displacement, color) = layer switch
                {
                    0 => (1.30f, 0.033f, Lit(48, 59, 61, timeFactor)),
                    1 => (1.12f, 0.012f, Lit(69, 83, 85, timeFactor)),
                    2 => (0.68f, -0.012f, Lit(109, 125, 127, timeFactor)),
                    _ => (0.25f, -0.044f, Lit(162, 174, 171, timeFactor))
                };
                Ribbon(edge, scale, edge.Downhill * pitch * displacement, color);
            }
        }
        foreach (var edge in cliffs)
        {
            for (int i = 1; i < edge.Points.Length - 2; i += 3 + (int)(Noise(edge.Seed, i, 211) * 3))
            {
                float n = Noise(edge.Seed, i, 197);
                var tangent = Vector2.Normalize(edge.Points[i + 1] - edge.Points[i - 1]);
                var p = edge.Points[i];
                var lip = p - edge.Downhill * pitch * (0.01f + n * 0.023f);
                var foot = p + edge.Downhill * pitch * (0.045f + n * 0.023f);
                var kink = Vector2.Lerp(lip, foot, 0.5f) + tangent * pitch * (n - 0.5f) * 0.065f;
                // Broken vertical seams and ledge shelves describe exposed rock faces.
                var crack = Lit(48, 59, 61, timeFactor);
                Raylib.DrawLineEx(lip, kink, pitch * (0.009f + n * 0.012f), crack);
                Raylib.DrawLineEx(kink, foot, pitch * 0.014f, crack);
                Raylib.DrawLineEx(kink + tangent * pitch * 0.014f,
                    kink + tangent * pitch * (0.033f + n * 0.025f), pitch * 0.012f,
                    Lit(135, 150, 151, timeFactor));
                if (n > 0.4f)
                {
                    var chip = foot + edge.Downhill * pitch * 0.044f;
                    Raylib.DrawLineEx(chip, chip + tangent * pitch * (0.013f + n * 0.014f),
                        pitch * 0.022f, Lit(89, 105, 108, timeFactor));
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
