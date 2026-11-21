namespace Survival;

/// The old game's world generator (ValleyLayout + GridWorldGenerator's terrain layers), ported
/// draw for draw on LegacyRng so a seed gives the same terrain it gave the old game. Geography
/// first - basins, winding valleys, lakes and their drainages, ravines, caves, the pass - then
/// biomes painted on the open land, then the named places dealt onto it (old PlaceNamedLocations and
/// PlaceCaveLocations), still on the same stream, so a seed names the same places the old game named.
// Math.Sin/Cos/Pow are libm calls, so seed -> terrain is exact per platform, not across
// platforms. A save holds no terrain; if saves must move between OSes, swap these for a portable sin.
static class MapGen
{
    static readonly (int dx, int dy)[][] SmallShapes = [[(0, 0)], [(0, 0), (1, 0)], [(0, 0), (0, 1)]];
    static readonly (int dx, int dy)[][] MediumShapes =
        [[(0, 0)], [(0, 0), (1, 0)], [(0, 0), (0, 1)], [(0, 0), (1, 0), (0, 1)], [(0, 0), (1, 0), (2, 0)]];

    public static GeneratedMap Generate(int seed, int width, int height)
    {
        var rng = new LegacyRng(seed);
        var layout = new ValleyLayout(width, height, rng);
        var terrain = layout.Terrain;
        var land = layout.Reachable.Where(p => layout.Structures[p.X, p.Y] == Cave.None
            && !layout.Pass.Contains(p) && !layout.WaterCrossings.Contains(p)).ToList();
        var paintable = land.ToHashSet();
        double density = land.Count / (96.0 * (96 - 18)); // the old constant, kept: it scales every layer
        PaintBiomes(terrain, width, height, rng, land, paintable, density);

        var river = new HashSet<TileId>();
        foreach (var p in layout.Reachable)
            if (Neighbors(p).Any(n => In(n, width, height) && terrain[n.X, n.Y] == Terrain.DeepWater)
                && terrain[p.X, p.Y] != Terrain.Water && layout.Structures[p.X, p.Y] != Cave.Floor) // a lake has its own ice
                river.Add(p);

        var barriers = new Dictionary<(TileId, TileId), EdgeType>();
        foreach (var (key, type) in layout.Barriers)
            if (terrain[key.A.X, key.A.Y].IsPassable() && terrain[key.B.X, key.B.Y].IsPassable()) barriers[key] = type;

        var camp = FindCampSite(layout, terrain, paintable);
        terrain[camp.X, camp.Y] = Terrain.Forest; // the camp is woods, whatever stood there

        var stages = new Dictionary<TileId, int>();
        for (int j = 0; j < Map.PassStages.Length; j++)
        {
            var p = layout.Pass[j * (layout.Pass.Count - 1) / (Map.PassStages.Length - 1)];
            terrain[p.X, p.Y] = Terrain.Rock;
            stages[p] = j;
        }
        var sites = new Dictionary<TileId, LocationDef>();
        PlaceNamedLocations(rng, land, terrain, camp, width, sites);
        PlaceCaveSites(rng, layout.Structures, layout.CaveIds, width, height, sites);
        return new GeneratedMap(width, height, terrain, layout.Structures, layout.CaveIds, barriers, river, camp, stages, sites, layout.Pass);
    }

    const int TargetNamedLocations = 300, MinLocationSpacing = 10;

    /// Old PlaceNamedLocations: random open tiles at least ten apart (Manhattan) and from camp, each
    /// given a site weighted among those that prefer its terrain; elite ones only past 65% of the
    /// map's width from camp. Draw for draw the old loop, so the old sites land on the old tiles.
    static void PlaceNamedLocations(LegacyRng rng, List<TileId> land, Terrain[,] terrain, TileId camp, int width,
        Dictionary<TileId, LocationDef> sites)
    {
        var placed = new List<TileId> { camp };
        double minEliteDistance = width * 0.65;
        var named = Content.Locations.Where(l => l.Placement == Placement.Named).ToList();
        for (int attempts = 0; placed.Count <= TargetNamedLocations && attempts < TargetNamedLocations * 10; attempts++)
        {
            var p = land[rng.Next(land.Count)];
            if (placed.Any(q => Manhattan(q, p) < MinLocationSpacing)) continue; // also keeps camp and placed sites
            bool allowElite = Distance(p, camp) >= minEliteDistance;
            var valid = named.Where(l => l.Terrain!.Contains(terrain[p.X, p.Y]) && (allowElite || !l.Elite)).ToList();
            if (valid.Count == 0) continue;
            double total = 0;
            foreach (var l in valid) total += l.Weight;
            double roll = rng.NextDouble() * total, cumulative = 0;
            var site = valid[0];
            foreach (var l in valid)
            {
                cumulative += l.Weight;
                if (roll <= cumulative) { site = l; break; }
            }
            sites[p] = site;
            placed.Add(p);
        }
    }

    /// Old PlaceCaveLocations: the four cave sites in a shuffled order, dealt round-robin onto one
    /// floor tile of each cave system, two for a system of sixteen floors or more.
    static void PlaceCaveSites(LegacyRng rng, Cave[,] caves, int?[,] caveIds, int width, int height,
        Dictionary<TileId, LocationDef> sites)
    {
        var order = Shuffled(Content.CaveSites.ToList(), rng);
        var systems = new List<(int Id, List<TileId> Floors)>(); // in order of first floor, x-major, as old AllLocations
        for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
                if (caves[x, y] == Cave.Floor && caveIds[x, y] is int id)
                {
                    int i = systems.FindIndex(s => s.Id == id);
                    if (i < 0) systems.Add((id, [new(x, y)])); else systems[i].Floors.Add(new(x, y));
                }
        int next = 0;
        foreach (var (_, floors) in systems)
        {
            var shuffled = Shuffled(floors, rng);
            int count = Math.Min(2, Math.Max(1, shuffled.Count / 8));
            foreach (var floor in shuffled.Take(count)) sites[floor] = order[next++ % order.Count];
        }
    }

    /// Old OrderBy(_ => rng.Next()): one key per item in order, then a stable sort on the keys.
    static List<T> Shuffled<T>(List<T> items, LegacyRng rng)
    {
        var keys = items.Select(_ => rng.Next()).ToList();
        return Enumerable.Range(0, items.Count).OrderBy(i => keys[i]).Select(i => items[i]).ToList();
    }

    public static bool In(TileId p, int width, int height) => p.X >= 0 && p.X < width && p.Y >= 0 && p.Y < height;

    /// North, east, south, west: the old GetCardinalNeighbors order, which the draw order depends on.
    public static TileId[] Neighbors(TileId p) => [new(p.X, p.Y - 1), new(p.X + 1, p.Y), new(p.X, p.Y + 1), new(p.X - 1, p.Y)];
    public static int Manhattan(TileId a, TileId b) => Math.Abs(a.X - b.X) + Math.Abs(a.Y - b.Y);
    /// Single precision, as the old GridPosition.DistanceTo (Vector2) computed it.
    public static double Distance(TileId a, TileId b) { float dx = a.X - b.X, dy = a.Y - b.Y; return MathF.Sqrt(dx * dx + dy * dy); }
    public static (TileId A, TileId B) EdgeKey(TileId a, TileId b) => a.X < b.X || a.X == b.X && a.Y < b.Y ? (a, b) : (b, a);

    /// Base Forest/Plain by octave noise split at the median, then rock, clearings, hills, water
    /// and marsh. Rerolls (up to 20 times) until every passable terrain has a minimum share.
    static void PaintBiomes(Terrain[,] terrain, int width, int height, LegacyRng rng,
        List<TileId> land, HashSet<TileId> paintable, double density)
    {
        int minTiles = Math.Max(1, (int)Math.Round(40 * density));
        for (int attempt = 0; attempt < 20; attempt++)
        {
            int seed = rng.Next();
            var noise = new double[width, height];
            var values = new List<double>();
            for (int x = 0; x < width; x++)
                for (int y = 0; y < height; y++)
                {
                    noise[x, y] = OctaveNoise(x, y, seed, 16);
                    if (paintable.Contains(new(x, y))) values.Add(noise[x, y]);
                }
            values.Sort();
            double median = values[values.Count / 2];
            for (int x = 0; x < width; x++)
                for (int y = 0; y < height; y++)
                    if (paintable.Contains(new(x, y)))
                        terrain[x, y] = noise[x, y] > median ? Terrain.Forest : Terrain.Plain;

            int rocks = Math.Max(1, (int)Math.Round(rng.Next(224, 372) * density));
            for (int i = 0; i < rocks; i++)
            {
                var p = land[rng.Next(land.Count)];
                if (terrain[p.X, p.Y] is Terrain.Forest or Terrain.Plain) terrain[p.X, p.Y] = Terrain.Rock;
            }
            Clusters((int)Math.Round(rng.Next(108, 192) * density), Terrain.Clearing, Terrain.Forest, MediumShapes, null);
            Clusters((int)Math.Round(rng.Next(92, 156) * density), Terrain.Hills, Terrain.Plain, MediumShapes, null);
            Clusters((int)Math.Round(rng.Next(92, 156) * density), Terrain.Water, null, SmallShapes,
                [Terrain.Forest, Terrain.Plain, Terrain.Clearing]);
            Marsh();
            if (EveryTerrainHas(minTiles)) return;
        }

        void Clusters(int rounded, Terrain place, Terrain? required, (int dx, int dy)[][] shapes, Terrain[]? allowed)
        {
            int count = Math.Max(1, rounded);
            for (int i = 0; i < count; i++)
            {
                var (x, y) = land[rng.Next(land.Count)];
                if (required.HasValue && terrain[x, y] != required.Value) continue;
                if (allowed != null && !allowed.Contains(terrain[x, y])) continue;
                var shape = RotateAndFlip(shapes[rng.Next(shapes.Length)], rng);
                bool valid = shape.All(s =>
                {
                    int nx = x + s.dx, ny = y + s.dy;
                    if (nx < 0 || nx >= width || ny < 0 || ny >= height || !paintable.Contains(new(nx, ny))) return false;
                    if (required.HasValue && terrain[nx, ny] != required.Value) return false;
                    return allowed == null || allowed.Contains(terrain[nx, ny]);
                });
                if (!valid) continue;
                foreach (var (dx, dy) in shape) terrain[x + dx, y + dy] = place;
            }
        }

        void Marsh()
        {
            var candidates = new List<TileId>();
            for (int x = 0; x < width; x++)
                for (int y = 0; y < height; y++)
                {
                    if (!paintable.Contains(new(x, y))) continue;
                    if (terrain[x, y] is not (Terrain.Forest or Terrain.Plain or Terrain.Clearing)) continue;
                    foreach (var (dx, dy) in new[] { (-1, 0), (1, 0), (0, -1), (0, 1) })
                    {
                        int nx = x + dx, ny = y + dy;
                        if (nx >= 0 && nx < width && ny >= 0 && ny < height && terrain[nx, ny] is Terrain.Water or Terrain.DeepWater)
                        { candidates.Add(new(x, y)); break; }
                    }
                }
            foreach (var p in candidates)
                if (rng.NextDouble() < 0.5) terrain[p.X, p.Y] = Terrain.Marsh;
        }

        bool EveryTerrainHas(int min)
        {
            var counts = new Dictionary<Terrain, int>();
            foreach (var p in paintable) counts[terrain[p.X, p.Y]] = counts.GetValueOrDefault(terrain[p.X, p.Y]) + 1;
            Terrain[] required = [Terrain.Forest, Terrain.Plain, Terrain.Clearing, Terrain.Hills, Terrain.Rock, Terrain.Water, Terrain.Marsh];
            return required.All(t => counts.GetValueOrDefault(t) >= min);
        }
    }

    static (int dx, int dy)[] RotateAndFlip((int dx, int dy)[] shape, LegacyRng rng)
    {
        int rotations = rng.Next(0, 4);
        bool flip = rng.Next(0, 2) == 1;
        return shape.Select(s =>
        {
            var (dx, dy) = s;
            for (int r = 0; r < rotations; r++) (dx, dy) = (-dy, dx);
            return (flip ? -dx : dx, dy);
        }).ToArray();
    }

    static double OctaveNoise(int x, int y, int seed, double scale)
    {
        double value = 0, amplitude = 1, frequency = 1, max = 0;
        for (int octave = 0; octave < 3; octave++)
        {
            value += InterpolatedNoise(x * frequency / scale, y * frequency / scale, seed + octave * 1000) * amplitude;
            max += amplitude;
            amplitude *= 0.5;
            frequency *= 2;
        }
        return value / max;
    }

    static double InterpolatedNoise(double x, double y, int seed)
    {
        int x0 = (int)Math.Floor(x), y0 = (int)Math.Floor(y);
        double fx = x - x0, fy = y - y0;
        fx = fx * fx * (3 - 2 * fx);
        fy = fy * fy * (3 - 2 * fy);
        double n00 = HashNoise(x0, y0, seed), n10 = HashNoise(x0 + 1, y0, seed);
        double n01 = HashNoise(x0, y0 + 1, seed), n11 = HashNoise(x0 + 1, y0 + 1, seed);
        double nx0 = n00 + fx * (n10 - n00), nx1 = n01 + fx * (n11 - n01);
        return nx0 + fy * (nx1 - nx0);
    }

    static double HashNoise(int x, int y, int seed)
    {
        unchecked
        {
            int n = x + y * 57 + seed * 131;
            n = (n << 13) ^ n;
            int m = (n * (n * n * 15731 + 789221) + 1376312589) & 0x7fffffff;
            return 1.0 - m / 1073741824.0;
        }
    }

    /// Camp opens in woods with room to move: the nearest forest tile to the starting basin's
    /// middle, within 12 tiles, not hemmed in by water, marsh or a barrier on any side.
    static TileId FindCampSite(ValleyLayout layout, Terrain[,] terrain, HashSet<TileId> paintable)
    {
        var start = layout.Camp;
        bool Open(TileId p) => paintable.Contains(p) && terrain[p.X, p.Y] is not (Terrain.Water or Terrain.Marsh);
        int Room(TileId p) => Neighbors(p).Count(n => Open(n) && !layout.Barriers.ContainsKey(EdgeKey(p, n)));
        var sites = paintable.Where(p => Manhattan(p, start) <= 12 && Open(p) && Room(p) == 4).ToList();
        var forest = sites.Where(p => terrain[p.X, p.Y] == Terrain.Forest).ToList();
        return (forest.Count > 0 ? forest : sites).OrderBy(p => Manhattan(p, start)).Cast<TileId?>().FirstOrDefault() ?? start;
    }

    /// Geography before biomes: a west-to-east chain of basins, narrow winding valleys between
    /// them, lakes draining north or south, ravines and cliffs, caves, and the eastern pass.
    sealed class ValleyLayout
    {
        readonly int _width, _height, _east;
        readonly LegacyRng _rng;
        public Terrain[,] Terrain { get; }
        public Cave[,] Structures { get; }
        public int?[,] CaveIds { get; }
        public Dictionary<(TileId A, TileId B), EdgeType> Barriers { get; } = [];
        public HashSet<TileId> WaterCrossings { get; } = [];
        public List<List<TileId>> Routes { get; } = [];
        public List<TileId> Pass { get; } = [];
        public TileId Camp { get; private set; }
        public HashSet<TileId> Reachable { get; private set; } = [];
        readonly List<Basin> _basins = [];
        sealed record Basin(TileId Center, double Rx, double Ry, double Angle);

        public ValleyLayout(int width, int height, LegacyRng rng)
        {
            if (width < 20 || height < 20)
                throw new ArgumentOutOfRangeException(nameof(width), "Valley worlds need at least 20 x 20 tiles.");
            _width = width; _height = height; _rng = rng;
            _east = width - Math.Clamp(width / 12, 6, 18) - 1;
            Terrain = new Terrain[width, height];
            Structures = new Cave[width, height];
            CaveIds = new int?[width, height];
            foreach (var p in Positions()) Terrain[p.X, p.Y] = Survival.Terrain.Mountain;
            Generate();
        }

        IEnumerable<TileId> Positions()
        {
            for (int x = 0; x < _width; x++)
                for (int y = 0; y < _height; y++) yield return new(x, y);
        }
        bool Inside(TileId p) => p.X >= 2 && p.X < _east && p.Y >= 2 && p.Y < _height - 2;
        IEnumerable<TileId> Neighbors(TileId p) => MapGen.Neighbors(p).Where(n => In(n, _width, _height));
        TileId Clamp(double x, double y) => new(
            (int)Math.Clamp(Math.Round(x), 2, _east - 1), (int)Math.Clamp(Math.Round(y), 2, _height - 3));
        double Jitter(double amount) => (_rng.NextDouble() * 2 - 1) * amount;
        T Pick<T>(IReadOnlyList<T> list) => list[_rng.Next(list.Count)];
        bool Open(TileId p) => Terrain[p.X, p.Y].IsPassable();
        void Ground(TileId p)
        {
            if (!Inside(p)) return;
            Terrain[p.X, p.Y] = Survival.Terrain.Plain;
            Structures[p.X, p.Y] = Cave.None;
        }
        void Disk(TileId center, double radius)
        {
            for (int dx = -(int)Math.Ceiling(radius); dx <= Math.Ceiling(radius); dx++)
                for (int dy = -(int)Math.Ceiling(radius); dy <= Math.Ceiling(radius); dy++)
                    if (dx * dx + dy * dy <= radius * radius) Ground(new(center.X + dx, center.Y + dy));
        }

        void Generate()
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
            for (int j = 0; j < _rng.Next(6, 11); j++) // the bound is redrawn every pass, as in the old code
            {
                var a = Pick(Pick(Routes));
                for (int attempt = 0; attempt < 30; attempt++)
                {
                    double angle = _rng.NextDouble() * Math.Tau, length = _rng.Next(8, 25);
                    var b = Clamp(a.X + Math.Cos(angle) * length, a.Y + Math.Sin(angle) * length);
                    if (Terrain[b.X, b.Y] != Survival.Terrain.Mountain) continue;
                    Valley(a, b); break;
                }
            }
            // Each lake drains locally, toward the nearest north/south boundary. The starting basin never floods.
            var lakes = _basins.Skip(1).OrderBy(_ => _rng.Next()).Take(Math.Min(_basins.Count - 1, _rng.Next(2, 5))).ToList();
            foreach (var basin in lakes)
            {
                var river = Curve(basin.Center, Clamp(basin.Center.X + Jitter(7), basin.Center.Y < _height / 2 ? 2 : _height - 3), 4);
                foreach (var p in river) Disk(p, 2.5);
                PaintBasin(basin, true);
                foreach (var p in river) Terrain[p.X, p.Y] = Survival.Terrain.DeepWater;
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
                    TileId a, b;
                    if (p.Y == q.Y) { int x = Math.Min(p.X, q.X); a = new(x, p.Y - 1); b = new(x, p.Y); }
                    else { int y = Math.Min(p.Y, q.Y); a = new(p.X - 1, y); b = new(p.X, y); }
                    if (Inside(a) && Inside(b) && Open(a) && Open(b) &&
                        !Neighbors(a).Concat(Neighbors(b)).Any(n => Terrain[n.X, n.Y] == Survival.Terrain.DeepWater))
                        Barriers[EdgeKey(a, b)] = type;
                }
            }
            // Restore every intended route: water gets rock stepping ground, barriers get gaps.
            foreach (var route in Routes)
                for (int j = 0; j < route.Count; j++)
                {
                    var p = route[j];
                    if (Terrain[p.X, p.Y] == Survival.Terrain.DeepWater) WaterCrossings.Add(p);
                    Terrain[p.X, p.Y] = WaterCrossings.Contains(p) ? Survival.Terrain.Rock : Survival.Terrain.Plain;
                    if (j > 0) Barriers.Remove(EdgeKey(route[j - 1], p));
                }
            var start = stages[0][0].Center;
            Camp = Neighbors(start).Where(p => Open(p) && !Barriers.ContainsKey(EdgeKey(start, p))).OrderBy(_ => _rng.Next()).FirstOrDefault(start);
            Reachable = Flood(Camp);
            // Remove isolated pockets so nothing placed later can be stranded.
            foreach (var p in Positions())
                if (Open(p) && !Reachable.Contains(p)) Terrain[p.X, p.Y] = Survival.Terrain.Mountain;
            foreach (var key in Barriers.Keys.Where(k => !Open(k.A) || !Open(k.B)).ToList())
                Barriers.Remove(key);
            GeneratePass(stages[^1][0].Center);
            Reachable = Flood(Camp);
            GenerateCaves();
            Reachable = Flood(Camp);
        }

        void PaintBasin(Basin b, bool lake)
        {
            double rx = b.Rx * (lake ? .48 : 1), ry = b.Ry * (lake ? .48 : 1);
            double c = Math.Cos(b.Angle), s = Math.Sin(b.Angle);
            foreach (var p in Positions().Where(Inside))
            {
                double dx = p.X - b.Center.X, dy = p.Y - b.Center.Y;
                double value = Math.Pow((dx * c + dy * s) / rx, 2) + Math.Pow((-dx * s + dy * c) / ry, 2);
                if (value >= 1 + Math.Sin(p.X * .8 + p.Y * .6) * .12) continue;
                Terrain[p.X, p.Y] = lake ? Survival.Terrain.DeepWater : Survival.Terrain.Plain;
            }
        }

        void Valley(TileId a, TileId b)
        {
            var cells = Curve(a, b, Math.Min(11, Distance(a, b) * .3));
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
        List<TileId> Curve(TileId a, TileId b, double amplitude)
        {
            double dx = b.X - a.X, dy = b.Y - a.Y, length = Math.Max(1, Distance(a, b));
            double sign = _rng.Next(2) == 0 ? -1 : 1;
            TileId Bend(double t, double off) => Clamp(a.X + dx * t - dy / length * off, a.Y + dy * t + dx / length * off);
            TileId[] points = [a, Bend(.28, sign * amplitude), Bend(.62, -sign * amplitude * .65), b];
            var result = new List<TileId> { a };
            for (int segment = 0; segment < points.Length - 1; segment++)
            {
                var p0 = points[Math.Max(0, segment - 1)]; var p1 = points[segment];
                var p2 = points[segment + 1]; var p3 = points[Math.Min(points.Length - 1, segment + 2)];
                int samples = Math.Max(4, Manhattan(p1, p2) * 4);
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

        HashSet<TileId> Flood(TileId start)
        {
            var seen = new HashSet<TileId> { start }; var queue = new Queue<TileId>(); queue.Enqueue(start);
            while (queue.TryDequeue(out var p))
                foreach (var n in Neighbors(p)) if (Open(n) && !Barriers.ContainsKey(EdgeKey(p, n)) && seen.Add(n)) queue.Enqueue(n);
            return seen;
        }

        List<TileId> Path(TileId a, TileId b, Func<TileId, bool> allowed, int limit)
        {
            var queue = new Queue<TileId>(); queue.Enqueue(a);
            var previous = new Dictionary<TileId, TileId> { [a] = a };
            var depth = new Dictionary<TileId, int> { [a] = 0 };
            while (queue.TryDequeue(out var p))
            {
                if (p == b)
                {
                    var path = new List<TileId> { b };
                    while (path[^1] != a) path.Add(previous[path[^1]]);
                    path.Reverse(); return path;
                }
                if (depth[p] >= limit) continue;
                foreach (var n in Neighbors(p))
                    if (allowed(n) && !Barriers.ContainsKey(EdgeKey(p, n)) && previous.TryAdd(n, p)) { depth[n] = depth[p] + 1; queue.Enqueue(n); }
            }
            return [];
        }

        // Three to six cave systems: mostly two-mouth tunnels, often a blind shelter, occasionally a
        // three-mouth warren. Mouths always open onto reachable ground.
        void GenerateCaves()
        {
            var boundary = Positions().Where(p => Inside(p) && p.X < _east - 5 && Terrain[p.X, p.Y] == Survival.Terrain.Mountain
                && Neighbors(p).Any(Reachable.Contains)).ToList();
            if (boundary.Count < 2) return;
            int target = _rng.Next(3, 7), id = 0;
            for (int cave = 0; cave < target; cave++)
            {
                double roll = _rng.NextDouble();
                int mouths = roll < .40 ? 1 : roll < .90 ? 2 : 3;
                for (int attempt = 0; attempt < 120; attempt++)
                    if (mouths == 1 ? CarveBlind(boundary, id) : CarveTunnel(boundary, id, mouths == 3)) { id++; break; }
            }
        }

        bool Diggable(TileId p) => Inside(p) && p.X < _east - 5 &&
            Terrain[p.X, p.Y] == Survival.Terrain.Mountain && !Neighbors(p).Any(n => CaveIds[n.X, n.Y].HasValue);

        void Carve(IEnumerable<TileId> cells, int id)
        {
            foreach (var p in cells)
            { Terrain[p.X, p.Y] = Survival.Terrain.Rock; Structures[p.X, p.Y] = Cave.Floor; CaveIds[p.X, p.Y] = id; }
        }

        bool CarveTunnel(List<TileId> boundary, int id, bool thirdMouth)
        {
            var a = Pick(boundary);
            var options = boundary.Where(p => Manhattan(p, a) is >= 10 and <= 30).ToList();
            if (options.Count == 0) return false;
            var b = Pick(options);
            if (!Diggable(a) || !Diggable(b)) return false;
            var tunnel = Path(a, b, Diggable, 42);
            if (tunnel.Count < 11) return false;
            var aa = Neighbors(a).First(Reachable.Contains); var bb = Neighbors(b).First(Reachable.Contains);
            // A shortcut is only worth digging if the way around is meaningfully longer.
            var outside = Path(aa, bb, Reachable.Contains, _width * _height);
            if (outside.Count < tunnel.Count + 8) return false;
            Carve(tunnel, id);
            Structures[a.X, a.Y] = Structures[b.X, b.Y] = Cave.Entrance;
            if (thirdMouth) CarveThirdMouth(boundary, tunnel, id);
            var tip = tunnel[tunnel.Count / 2];
            for (int step = 0; step < _rng.Next(4, 9); step++)
            {
                var choices = Neighbors(tip).Where(p => Inside(p) && p.X < _east - 5 && Terrain[p.X, p.Y] == Survival.Terrain.Mountain &&
                    Neighbors(p).All(n => n == tip || !Open(n) && !CaveIds[n.X, n.Y].HasValue)).ToList();
                if (choices.Count == 0) break;
                tip = Pick(choices); Carve([tip], id);
            }
            return true;
        }

        void CarveThirdMouth(List<TileId> boundary, List<TileId> tunnel, int id)
        {
            for (int attempt = 0; attempt < 40; attempt++)
            {
                var mouth = Pick(boundary);
                if (!Diggable(mouth)) continue;
                var junction = tunnel[_rng.Next(2, tunnel.Count - 2)];
                bool Allowed(TileId p) => p == junction || Inside(p) && p.X < _east - 5 &&
                    Terrain[p.X, p.Y] == Survival.Terrain.Mountain && Neighbors(p).All(n => n == junction || !CaveIds[n.X, n.Y].HasValue);
                var spur = Path(mouth, junction, Allowed, 30);
                if (spur.Count < 5) continue;
                Carve(spur[..^1], id);
                Structures[mouth.X, mouth.Y] = Cave.Entrance;
                return;
            }
        }

        bool CarveBlind(List<TileId> boundary, int id)
        {
            var mouth = Pick(boundary);
            if (!Diggable(mouth)) return false;
            var candidates = Positions().Where(p => Inside(p) && Manhattan(p, mouth) is >= 8 and <= 16 &&
                Terrain[p.X, p.Y] == Survival.Terrain.Mountain && Neighbors(p).All(n => !Open(n))).ToList();
            if (candidates.Count == 0) return false;
            var path = Path(mouth, Pick(candidates), Diggable, 22);
            if (path.Count < 8) return false;
            Carve(path, id);
            Structures[mouth.X, mouth.Y] = Cave.Entrance;
            return true;
        }

        void GeneratePass(TileId last)
        {
            // The reserved eastern strip has no other openings. All six stages must be crossed.
            for (int x = last.X; x <= _east; x++)
            {
                Terrain[x, last.Y] = Survival.Terrain.Rock; Structures[x, last.Y] = Cave.None; CaveIds[x, last.Y] = null;
                if (x > last.X) Barriers.Remove(EdgeKey(new(x - 1, last.Y), new(x, last.Y)));
            }
            for (int x = _east; x < _width; x++) { Terrain[x, last.Y] = Survival.Terrain.Rock; Pass.Add(new(x, last.Y)); }
        }
    }
}

/// The BCL Random(seed)'s legacy algorithm (Knuth's subtractive generator, as .NET's
/// Net5CompatSeedImpl), so the map draws exactly what the old game drew. Generation only: it is
/// rebuilt from the seed every time, never saved, and never used by the running simulation.
sealed class LegacyRng
{
    readonly int[] _seed = new int[56];
    int _inext, _inextp = 21;

    public LegacyRng(int seed)
    {
        int subtraction = seed == int.MinValue ? int.MaxValue : Math.Abs(seed);
        int mj = 161803398 - subtraction;
        _seed[55] = mj;
        int mk = 1, ii = 0;
        for (int i = 1; i < 55; i++)
        {
            if ((ii += 21) >= 55) ii -= 55;
            _seed[ii] = mk;
            mk = mj - mk;
            if (mk < 0) mk += int.MaxValue;
            mj = _seed[ii];
        }
        for (int k = 1; k < 5; k++)
            for (int i = 1; i < 56; i++)
            {
                int n = i + 30;
                if (n >= 55) n -= 55;
                _seed[i] = unchecked(_seed[i] - _seed[1 + n]);
                if (_seed[i] < 0) _seed[i] += int.MaxValue;
            }
    }

    int InternalSample()
    {
        if (++_inext >= 56) _inext = 1;
        if (++_inextp >= 56) _inextp = 1;
        int ret = _seed[_inext] - _seed[_inextp];
        if (ret == int.MaxValue) ret--;
        if (ret < 0) ret += int.MaxValue;
        _seed[_inext] = ret;
        return ret;
    }

    public double NextDouble() => InternalSample() * (1.0 / int.MaxValue);
    public int Next() => InternalSample();
    public int Next(int maxExclusive) => (int)(NextDouble() * maxExclusive);
    public int Next(int min, int maxExclusive) => (int)(NextDouble() * ((long)maxExclusive - min)) + min;
}
