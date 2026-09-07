using text_survival.Environments.Grid;

namespace text_survival.Environments.Factories;

/// <summary>
/// Builds geography before biomes: a west-to-east graph of basins, narrow winding
/// connections, local drainages and mountain tunnels. No simulation or locations here.
/// </summary>
internal sealed class ValleyLayout
{
    private readonly int _width, _height, _east;
    private readonly Random _rng;
    public TerrainType[,] Terrain { get; }
    public TileStructure[,] Structures { get; }
    public int?[,] CaveIds { get; }
    public Dictionary<(GridPosition A, GridPosition B), EdgeType> Barriers { get; } = [];
    public HashSet<GridPosition> WaterCrossings { get; } = [];
    private static (GridPosition A, GridPosition B) EdgeKey(GridPosition a, GridPosition b) =>
        a.X < b.X || a.X == b.X && a.Y < b.Y ? (a, b) : (b, a);
    public List<List<GridPosition>> Routes { get; } = [];
    public List<GridPosition> Pass { get; } = [];
    public GridPosition Camp { get; private set; }
    public HashSet<GridPosition> Reachable { get; private set; } = [];
    private readonly List<Basin> _basins = [];
    private sealed record Basin(GridPosition Center, double Rx, double Ry, double Angle);

    public ValleyLayout(int width, int height, Random rng)
    {
        if (width < 20 || height < 20)
            throw new ArgumentOutOfRangeException(nameof(width), "Valley worlds need at least 20 × 20 tiles.");
        _width = width; _height = height; _rng = rng;
        _east = width - Math.Clamp(width / 12, 6, 18) - 1;
        Terrain = new TerrainType[width, height];
        Structures = new TileStructure[width, height];
        CaveIds = new int?[width, height];
        foreach (var p in Positions()) Terrain[p.X, p.Y] = TerrainType.Mountain;
        Generate();
    }

    private IEnumerable<GridPosition> Positions()
    {
        for (int x = 0; x < _width; x++)
            for (int y = 0; y < _height; y++) yield return new(x, y);
    }
    private bool Inside(GridPosition p) => p.X >= 2 && p.X < _east && p.Y >= 2 && p.Y < _height - 2;
    private IEnumerable<GridPosition> Neighbors(GridPosition p) => p.GetCardinalNeighbors()
        .Where(n => n.X >= 0 && n.X < _width && n.Y >= 0 && n.Y < _height);
    private GridPosition Clamp(double x, double y) => new(
        (int)Math.Clamp(Math.Round(x), 2, _east - 1), (int)Math.Clamp(Math.Round(y), 2, _height - 3));
    private double Jitter(double amount) => (_rng.NextDouble() * 2 - 1) * amount;
    private T Pick<T>(IReadOnlyList<T> list) => list[_rng.Next(list.Count)];
    private bool Open(GridPosition p) => Terrain[p.X, p.Y].IsPassable();
    private void Ground(GridPosition p)
    {
        if (!Inside(p)) return;
        Terrain[p.X, p.Y] = TerrainType.Plain;
        Structures[p.X, p.Y] = TileStructure.None;
    }
    private void Disk(GridPosition center, double radius)
    {
        for (int dx = -(int)Math.Ceiling(radius); dx <= Math.Ceiling(radius); dx++)
            for (int dy = -(int)Math.Ceiling(radius); dy <= Math.Ceiling(radius); dy++)
                if (dx * dx + dy * dy <= radius * radius) Ground(new(center.X + dx, center.Y + dy));
    }

    private void Generate()
    {
        int count = Math.Clamp(_width / 28 + _rng.Next(-1, 2), 3, 9);
        double spacing = (_east - 10.0) / (count - 1);
        var stages = new List<List<Basin>>();
        for (int stage = 0; stage < count; stage++)
        {
            bool split = stage > 0 && stage < count - 1 &&
                (stage == 1 || stage != count / 2 && _rng.NextDouble() < .62);
            var group = new List<Basin>();
            for (int lane = 0; lane < (split ? 2 : 1); lane++)
            {
                var center = Clamp(5 + spacing * stage + Jitter(Math.Min(3, spacing * .12)),
                    _height * (split ? lane == 0 ? .28 : .72 : .5) + Jitter(_height * .09));
                double radius = Math.Min(spacing * .43, _height * .12);
                var basin = new Basin(center, Math.Max(2, radius * (.8 + _rng.NextDouble() * .4)),
                    Math.Max(2, radius * (.8 + _rng.NextDouble() * .4)), Jitter(.8));
                group.Add(basin); _basins.Add(basin);
                PaintBasin(basin, false);
            }
            stages.Add(group);
        }
        for (int j = 1; j < stages.Count; j++)
        {
            var left = stages[j - 1]; var right = stages[j];
            if (left.Count == 1 || right.Count == 1)
                foreach (var a in left) foreach (var b in right) Valley(a.Center, b.Center);
            else
            {
                Valley(left[0].Center, right[0].Center); Valley(left[1].Center, right[1].Center);
                if (_rng.NextDouble() < .3) { int lane = _rng.Next(2); Valley(left[lane].Center, right[1 - lane].Center); }
            }
        }
        foreach (var group in stages.Where(s => s.Count == 2))
            if (_rng.NextDouble() < .4) Valley(group[0].Center, group[1].Center);
        for (int j = 0; j < _rng.Next(6, 11); j++)
        {
            var a = Pick(Pick(Routes));
            for (int attempt = 0; attempt < 30; attempt++)
            {
                double angle = _rng.NextDouble() * Math.Tau, length = _rng.Next(8, 25);
                var b = Clamp(a.X + Math.Cos(angle) * length, a.Y + Math.Sin(angle) * length);
                if (Terrain[b.X, b.Y] != TerrainType.Mountain) continue;
                Valley(a, b); break;
            }
        }
        // Each lake drains locally, toward the nearest north/south boundary.
        // The starting basin never floods: camp opens in woods, not on a lake shore.
        var lakes = _basins.Skip(1).OrderBy(_ => _rng.Next()).Take(Math.Min(_basins.Count - 1, _rng.Next(2, 5))).ToList();
        foreach (var basin in lakes)
        {
            var river = Curve(basin.Center, Clamp(basin.Center.X + Jitter(7), basin.Center.Y < _height / 2 ? 2 : _height - 3), 4);
            foreach (var p in river) Disk(p, 2.5);
            PaintBasin(basin, true);
            foreach (var p in river)
                Terrain[p.X, p.Y] = TerrainType.DeepWater;
        }
        foreach (var basin in _basins.Skip(1).Except(lakes).OrderBy(_ => _rng.Next()).Take(5))
        {
            double angle = _rng.NextDouble() * Math.Tau, length = Math.Min(basin.Rx, basin.Ry) * .7;
            var cut = Curve(Clamp(basin.Center.X - Math.Cos(angle) * length, basin.Center.Y - Math.Sin(angle) * length),
                Clamp(basin.Center.X + Math.Cos(angle) * length, basin.Center.Y + Math.Sin(angle) * length), 2);
            var type = _rng.NextDouble() < .65 ? EdgeType.Ravine : EdgeType.Cliff;
            // The curve follows grid vertices. Each segment separates two ordinary tiles.
            for (int j = 1; j < cut.Count; j++)
            {
                var p = cut[j - 1]; var q = cut[j];
                GridPosition a, b;
                if (p.Y == q.Y)
                {
                    int x = Math.Min(p.X, q.X);
                    a = new(x, p.Y - 1); b = new(x, p.Y);
                }
                else
                {
                    int y = Math.Min(p.Y, q.Y);
                    a = new(p.X - 1, y); b = new(p.X, y);
                }
                if (Inside(a) && Inside(b) && Open(a) && Open(b) &&
                    !Neighbors(a).Concat(Neighbors(b)).Any(n => Terrain[n.X, n.Y] == TerrainType.DeepWater))
                    Barriers[EdgeKey(a, b)] = type;
            }
        }
        // Restore every intended route. Water gets rock stepping ground; ravines and
        // cliffs get passable gaps in their edge chains, without a marker or tile type.
        foreach (var route in Routes)
        {
            for (int j = 0; j < route.Count; j++)
            {
                var p = route[j];
                if (Terrain[p.X, p.Y] == TerrainType.DeepWater) WaterCrossings.Add(p);
                Terrain[p.X, p.Y] = WaterCrossings.Contains(p) ? TerrainType.Rock : TerrainType.Plain;
                if (j > 0) Barriers.Remove(EdgeKey(route[j - 1], p));
            }
        }
        var start = stages[0][0].Center;
        Camp = Neighbors(start).Where(p => Open(p) && !Barriers.ContainsKey(EdgeKey(start, p))).OrderBy(_ => _rng.Next()).FirstOrDefault(start);
        Reachable = Flood(Camp);
        // Remove accidental isolated pockets so resource and wildlife placement cannot strand content.
        foreach (var p in Positions())
            if (Open(p) && !Reachable.Contains(p)) Terrain[p.X, p.Y] = TerrainType.Mountain;
        foreach (var key in Barriers.Keys.Where(k => !Open(k.A) || !Open(k.B)).ToList())
            Barriers.Remove(key);
        GeneratePass(stages[^1][0].Center);
        Reachable = Flood(Camp);
        GenerateCaves();
        Reachable = Flood(Camp);
    }

    private void PaintBasin(Basin b, bool lake)
    {
        double rx = b.Rx * (lake ? .48 : 1), ry = b.Ry * (lake ? .48 : 1);
        double c = Math.Cos(b.Angle), s = Math.Sin(b.Angle);
        foreach (var p in Positions().Where(Inside))
        {
            double dx = p.X - b.Center.X, dy = p.Y - b.Center.Y;
            double value = Math.Pow((dx * c + dy * s) / rx, 2) + Math.Pow((-dx * s + dy * c) / ry, 2);
            if (value >= 1 + Math.Sin(p.X * .8 + p.Y * .6) * .12) continue;
            Terrain[p.X, p.Y] = lake ? TerrainType.DeepWater : TerrainType.Plain;
        }
    }

    private void Valley(GridPosition a, GridPosition b)
    {
        var cells = Curve(a, b, Math.Min(11, a.DistanceTo(b) * .3));
        for (int j = 0; j < cells.Count; j++)
        {
            var p = cells[j]; Ground(p);
            int width = 1 + (j / 9 + _rng.Next(2)) % 3;
            var next = cells[Math.Min(j + 1, cells.Count - 1)];
            for (int k = 1; k < width; k++)
            {
                int side = k == 1 ? 1 : -1;
                Ground(new(p.X + (next.X == p.X ? side : 0), p.Y + (next.X == p.X ? 0 : side)));
            }
        }
        Routes.Add(cells);
    }

    // Catmull-Rom bends rasterized into cardinal steps, with no diagonal cracks.
    private List<GridPosition> Curve(GridPosition a, GridPosition b, double amplitude)
    {
        double dx = b.X - a.X, dy = b.Y - a.Y, length = Math.Max(1, a.DistanceTo(b));
        double sign = _rng.Next(2) == 0 ? -1 : 1;
        GridPosition Bend(double t, double off) => Clamp(a.X + dx * t - dy / length * off, a.Y + dy * t + dx / length * off);
        GridPosition[] points = [a, Bend(.28, sign * amplitude), Bend(.62, -sign * amplitude * .65), b];
        var result = new List<GridPosition> { a };
        for (int segment = 0; segment < points.Length - 1; segment++)
        {
            var p0 = points[Math.Max(0, segment - 1)]; var p1 = points[segment];
            var p2 = points[segment + 1]; var p3 = points[Math.Min(points.Length - 1, segment + 2)];
            int samples = Math.Max(4, p1.ManhattanDistance(p2) * 4);
            for (int k = 1; k <= samples; k++)
            {
                double t = (double)k / samples;
                double S(double v0, double v1, double v2, double v3) => .5 * (2 * v1 + (-v0 + v2) * t +
                    (2 * v0 - 5 * v1 + 4 * v2 - v3) * t * t + (-v0 + 3 * v1 - 3 * v2 + v3) * t * t * t);
                var target = Clamp(S(p0.X, p1.X, p2.X, p3.X), S(p0.Y, p1.Y, p2.Y, p3.Y));
                while (result[^1] != target)
                {
                    var p = result[^1];
                    result.Add(Math.Abs(target.X - p.X) >= Math.Abs(target.Y - p.Y)
                        ? new(p.X + Math.Sign(target.X - p.X), p.Y) : new(p.X, p.Y + Math.Sign(target.Y - p.Y)));
                }
            }
        }
        return result;
    }

    private HashSet<GridPosition> Flood(GridPosition start)
    {
        var seen = new HashSet<GridPosition> { start }; var queue = new Queue<GridPosition>(); queue.Enqueue(start);
        while (queue.TryDequeue(out var p))
            foreach (var n in Neighbors(p)) if (Open(n) && !Barriers.ContainsKey(EdgeKey(p, n)) && seen.Add(n)) queue.Enqueue(n);
        return seen;
    }

    private List<GridPosition> Path(GridPosition a, GridPosition b, Func<GridPosition, bool> allowed, int limit)
    {
        var queue = new Queue<GridPosition>(); queue.Enqueue(a);
        var previous = new Dictionary<GridPosition, GridPosition> { [a] = a };
        var depth = new Dictionary<GridPosition, int> { [a] = 0 };
        while (queue.TryDequeue(out var p))
        {
            if (p == b)
            {
                var path = new List<GridPosition> { b };
                while (path[^1] != a) path.Add(previous[path[^1]]);
                path.Reverse(); return path;
            }
            if (depth[p] >= limit) continue;
            foreach (var n in Neighbors(p))
                if (allowed(n) && !Barriers.ContainsKey(EdgeKey(p, n)) && previous.TryAdd(n, p)) { depth[n] = depth[p] + 1; queue.Enqueue(n); }
        }
        return [];
    }

    private void GenerateCaves()
    {
        var boundary = Positions().Where(p => Inside(p) && p.X < _east - 5 && Terrain[p.X, p.Y] == TerrainType.Mountain && Neighbors(p).Any(Reachable.Contains)).ToList();
        if (boundary.Count < 2) return;
        int id = 0;
        for (int attempt = 0; attempt < 250 && id < 3; attempt++)
        {
            var a = Pick(boundary);
            var options = boundary.Where(p => p.ManhattanDistance(a) is >= 10 and <= 30).ToList();
            if (options.Count == 0) continue;
            var b = Pick(options);
            bool Rock(GridPosition p) => Inside(p) && p.X < _east - 5 && Terrain[p.X, p.Y] == TerrainType.Mountain &&
                !Neighbors(p).Any(n => CaveIds[n.X, n.Y].HasValue);
            if (!Rock(a) || !Rock(b)) continue;
            var tunnel = Path(a, b, Rock, 42);
            if (tunnel.Count < 11) continue;
            var aa = Neighbors(a).First(Reachable.Contains); var bb = Neighbors(b).First(Reachable.Contains);
            var outside = Path(aa, bb, Reachable.Contains, _width * _height);
            if (outside.Count < tunnel.Count + 8) continue;
            foreach (var p in tunnel)
            { Terrain[p.X, p.Y] = TerrainType.Rock; Structures[p.X, p.Y] = TileStructure.CaveFloor; CaveIds[p.X, p.Y] = id; }
            Structures[a.X, a.Y] = Structures[b.X, b.Y] = TileStructure.CaveEntrance;
            var tip = tunnel[tunnel.Count / 2];
            for (int step = 0; step < _rng.Next(4, 9); step++)
            {
                var choices = Neighbors(tip).Where(p => Inside(p) && p.X < _east - 5 && Terrain[p.X, p.Y] == TerrainType.Mountain &&
                    Neighbors(p).All(n => n == tip || !Open(n) && !CaveIds[n.X, n.Y].HasValue)).ToList();
                if (choices.Count == 0) break;
                tip = Pick(choices); Terrain[tip.X, tip.Y] = TerrainType.Rock;
                Structures[tip.X, tip.Y] = TileStructure.CaveFloor; CaveIds[tip.X, tip.Y] = id;
            }
            id++;
        }
        // A blind cave still offers shelter and a reason to investigate a dead end.
        for (int attempt = 0; attempt < 100 && id < 2; attempt++)
        {
            var mouth = Pick(boundary);
            if (Terrain[mouth.X, mouth.Y] != TerrainType.Mountain || CaveIds[mouth.X, mouth.Y].HasValue) continue;
            var candidates = Positions().Where(p => Inside(p) && p.ManhattanDistance(mouth) is >= 8 and <= 16 &&
                Terrain[p.X, p.Y] == TerrainType.Mountain && Neighbors(p).All(n => !Open(n))).ToList();
            if (candidates.Count == 0) continue;
            var path = Path(mouth, Pick(candidates), p => Inside(p) && p.X < _east - 5 && Terrain[p.X, p.Y] == TerrainType.Mountain &&
                !Neighbors(p).Any(n => CaveIds[n.X, n.Y].HasValue), 22);
            if (path.Count < 8 || Neighbors(mouth).Any(n => CaveIds[n.X, n.Y].HasValue)) continue;
            foreach (var p in path)
            {
                Terrain[p.X, p.Y] = TerrainType.Rock;
                Structures[p.X, p.Y] = p == mouth ? TileStructure.CaveEntrance : TileStructure.CaveFloor;
                CaveIds[p.X, p.Y] = id;
            }
            id++;
        }
    }

    private void GeneratePass(GridPosition last)
    {
        // The reserved eastern strip has no other openings. All six stages must be crossed.
        var approach = new GridPosition(_east, last.Y);
        for (int x = last.X; x <= approach.X; x++)
        {
            var p = new GridPosition(x, last.Y);
            Terrain[x, last.Y] = TerrainType.Rock; Structures[x, last.Y] = TileStructure.None;
            CaveIds[x, last.Y] = null;
            if (x > last.X) Barriers.Remove(EdgeKey(new(x - 1, last.Y), p));
        }
        for (int x = _east; x < _width; x++)
        {
            var p = new GridPosition(x, last.Y);
            Terrain[x, last.Y] = TerrainType.Rock; Pass.Add(p);
        }
    }
}
