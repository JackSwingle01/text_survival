namespace Survival;

/// A crossing's minutes as terms that add up to the total exactly (ui-design: every time in the dock equals the preview).
/// Each slowing is the extra minutes over the stage before it, per segment, rounded as the crossing rounds them.
/// Terrain is the ground's shape, Weather strong wind and heavy precipitation, Pace the body's own speed (negative
/// for a quick one), Careful half again where Traversal.PaceMatters. Edge is an authored edge (a ravine, a game
/// trail). Ground is today's snow, water and mud, with the sum's rounding up; Shortest is what the five-minute floor
/// adds. Those are the crossing over the bare tiles; Trail is what the edge's trodden snow changes of it (negative).
public sealed record TripTime(int WalkingMinutes, int TerrainMinutes, int WeatherMinutes, int PaceMinutes, int LoadMinutes,
    int CarefulMinutes, int EdgeMinutes, int TrailMinutes, int GroundMinutes, int ShortestMinutes)
{
    public int TotalMinutes => WalkingMinutes + TerrainMinutes + WeatherMinutes + PaceMinutes + LoadMinutes + CarefulMinutes
        + EdgeMinutes + TrailMinutes + GroundMinutes + ShortestMinutes;

    public IReadOnlyList<(string Factor, int Minutes)> Terms =>
    [
        ("Walking", WalkingMinutes), ("Terrain", TerrainMinutes), ("Weather", WeatherMinutes), ("Pace", PaceMinutes),
        ("Load", LoadMinutes), ("Careful", CarefulMinutes), ("Edge", EdgeMinutes), ("Trail", TrailMinutes),
        ("Snow and ground", GroundMinutes), ("Shortest crossing", ShortestMinutes),
    ];
}

/// A crossing as seen before it is made, and the routes made of crossings. Reading the ground writes nothing, so a
/// preview and the crossing itself read the same ground.
// The one copy of the crossing formula: Traversal.CrossingMinutes is Terms(...).TotalMinutes.
public static class Trip
{
    public static TripTime Time(World world, AgentId who, Direction dir, Pace pace)
    {
        var agent = world.Agent(who);
        return Terms(world, Traversal.PaceBody(agent), agent.Tile, Map.Step(agent.Tile, dir), pace, Weather.At(world, world.Minute), Items.EncumbrancePct(agent));
    }

    public static TripTime Terms(World world, BodyState body, TileId from, TileId to, Pace pace, WeatherSample sky,
        double encumbrancePct = 0)
    {
        var m = Map.Of(world);
        double unloaded = Body.Speed(body), speed = Body.Speed(body, new AbilityContext(EncumbrancePct: encumbrancePct));
        var still = sky with { WindLevel = 0, PrecipitationPct = 0 }; // no wind or precipitation to slow a walk
        var (exit, entry) = (Segment(from), Segment(to));
        int edge = m.EdgeBetween(from, to)?.TraversalMinutes() ?? 0;
        var (rounded, careful) = Join(HalfOf(world, from, null, body, speed, sky, pace), HalfOf(world, to, null, body, speed, sky, pace), edge);
        int bare = Total((rounded, careful));
        int trail = Tracks.IsTrodden(world, from, to) ? TroddenTotal(world, from, to, body, speed, sky, pace, edge) - bare : 0;
        int whole = exit.Minutes + entry.Minutes + edge;
        return new TripTime(exit.Walking + entry.Walking, exit.Terrain + entry.Terrain, exit.Weather + entry.Weather,
            exit.Pace + entry.Pace, exit.Load + entry.Load, careful, edge, trail,
            GroundMinutes: rounded - whole, ShortestMinutes: Math.Max(0, Traversal.MinimumCrossingMinutes - rounded));

        // Minutes is Traversal.SegmentMinutes itself.
        (int Walking, int Terrain, int Weather, int Pace, int Load, int Minutes) Segment(TileId tile)
        {
            int walking = Map.BaseTraversalMinutes(m, tile);
            int shaped = Traversal.SegmentMinutes(m, tile, 1, still);
            int weathered = Traversal.SegmentMinutes(m, tile, 1, sky);
            int paced = Traversal.SegmentMinutes(m, tile, unloaded, sky);
            int loaded = Traversal.SegmentMinutes(m, tile, speed, sky);
            return (walking, shaped - walking, weathered - shaped, paced - weathered, loaded - paced, loaded);
        }
    }

    /// One tile's half of a crossing: its segment minutes at this speed under this sky, how much its ground slows
    /// that, and whether care slows it.
    internal readonly record struct Half(int Minutes, double GroundFactor, bool Slow);

    /// other: the tile across the edge, for the edge's half of tile; null for the bare tile.
    static Half HalfOf(World world, TileId tile, TileId? other, BodyState body, double speed, WeatherSample sky, Pace pace)
    {
        double factor = Ground.Underfoot(world, tile, other).TraversalFactor;
        bool slow = pace == Pace.Careful
            && Traversal.PaceMatters(Traversal.InjuryOdds(Traversal.EffectiveHazardLevel(world, tile, other), body, sky, Pace.Quick).TotalPct);
        return new Half(Traversal.SegmentMinutes(Map.Of(world), tile, speed, sky), factor, slow);
    }

    /// The crossing from its two halves: the walk with the ground, rounded up; care slows both halves of risky
    /// ground on top of the five-minute floor (only the tile walked onto is rolled for).
    static (int Rounded, int Careful) Join(Half exit, Half entry, int edgeMinutes)
    {
        double surface = exit.Minutes * (exit.GroundFactor - 1) + entry.Minutes * (entry.GroundFactor - 1);
        int rounded = (int)Math.Ceiling(exit.Minutes + entry.Minutes + edgeMinutes + surface);
        return (rounded, (exit.Slow ? Careful(exit.Minutes) : 0) + (entry.Slow ? Careful(entry.Minutes) : 0));
    }

    static int Total((int Rounded, int Careful) joined) => Math.Max(joined.Rounded, Traversal.MinimumCrossingMinutes) + joined.Careful;

    static int TroddenTotal(World world, TileId from, TileId to, BodyState body, double speed, WeatherSample sky, Pace pace, int edge) =>
        Total(Join(HalfOf(world, from, to, body, speed, sky, pace), HalfOf(world, to, from, body, speed, sky, pace), edge));

    /// Prices one agent's crossings under one sky: the same total Terms gives (Join), without its breakdown. Given a
    /// search's scratch, each bare tile's half is priced once however many crossings share it; a trodden edge's
    /// halves are priced each time.
    sealed class Pricer(World world, Agent agent, Pace pace, WeatherSample sky, RouteScratch? scratch = null)
    {
        readonly BodyState body = Traversal.PaceBody(agent);
        double? speed;
        double Speed => speed ??= Body.Speed(body, new AbilityContext(EncumbrancePct: Items.EncumbrancePct(agent)));
        readonly int? flight = Traversal.FlightMinutesPerTile(agent);

        public int Minutes(TileId from, TileId to)
        {
            if (flight is int flat) return flat;
            int edge = Traversal.EdgeMinutes(world, from, to);
            if (Tracks.IsTrodden(world, from, to)) return TroddenTotal(world, from, to, body, Speed, sky, pace, edge);
            return Total(Join(HalfAt(from), HalfAt(to), edge));
        }

        Half HalfAt(TileId tile)
        {
            if (scratch == null) return HalfOf(world, tile, null, body, Speed, sky, pace);
            int i = scratch.Index(tile);
            if (scratch.HalfStamp[i] != scratch.Generation)
                (scratch.Halves[i], scratch.HalfStamp[i]) = (HalfOf(world, tile, null, body, Speed, sky, pace), scratch.Generation);
            return scratch.Halves[i];
        }
    }

    /// One search's working state, flat by tile and kept on World between searches (World.RouteScratch, never saved).
    /// A cell counts for this search only where its stamp is this search's Generation, so starting a search clears nothing.
    internal sealed class RouteScratch(int width, int height)
    {
        public readonly int Width = width;
        public int Generation;
        public readonly int[] Minutes = new int[width * height], Previous = new int[width * height];
        public readonly int[] ReachedStamp = new int[width * height], SettledStamp = new int[width * height], HalfStamp = new int[width * height];
        public readonly Half[] Halves = new Half[width * height];
        public readonly List<int> Settled = [];
        public readonly PriorityQueue<int, (int Minutes, TileId Tile)> Queue = new();

        public bool Fits(GeneratedMap m) => Width == m.Width && Minutes.Length == m.Width * m.Height;
        public int Index(TileId t) => t.Y * Width + t.X;
        public TileId Tile(int i) => new(i % Width, i / Width);

        public void Begin()
        {
            if (Generation == int.MaxValue)
            {
                Array.Clear(ReachedStamp); Array.Clear(SettledStamp); Array.Clear(HalfStamp);
                Generation = 0;
            }
            Generation++;
            Settled.Clear();
            Queue.Clear();
        }
    }

    /// A forecast for planners: the minutes who's crossing from a tile to its neighbour takes now, by the rule
    /// Traversal.Begin crosses by (Terms, or a flyer's flat minutes). Kept for the minute, so a route search pays
    /// for each crossing once however often it asks, and the answer is the same after a load.
    public static int StepMinutes(World world, AgentId who, TileId from, TileId to, Pace pace)
    {
        var agent = world.Agent(who);
        var body = Traversal.PaceBody(agent);
        double encumbrancePct = Items.EncumbrancePct(agent), speed = Body.Speed(body);
        if (world.StepMinutesCache is not { } cache || cache.Minute != world.Minute) world.StepMinutesCache = cache = (world.Minute, []);
        if (cache.Minutes.TryGetValue((who, from, to, pace), out var kept) && kept.EncumbrancePct == encumbrancePct && kept.Speed == speed)
            return kept.Minutes;
        if (Traversal.Blocked(world, agent, from, to) is string blocked) throw new ArgumentException($"No crossing from {from} to {to}: {blocked}");
        int minutes = new Pricer(world, agent, pace, Weather.At(world, world.Minute)).Minutes(from, to);
        cache.Minutes[(who, from, to, pace)] = (minutes, encumbrancePct, speed);
        return minutes;
    }

    /// A crossing of this edge priced this minute was priced before it was trodden (Tracks.RecordMove): only its prices go.
    internal static void Forget(World world, TileId a, TileId b)
    {
        if (world.StepMinutesCache is not { } cache) return;
        foreach (var key in cache.Minutes.Keys.Where(k => (k.From == a && k.To == b) || (k.From == b && k.To == a)).ToList())
            cache.Minutes.Remove(key);
    }

    /// The one route search, for people and animals alike: a Dijkstra from who's tile on StepMinutes' prices at a
    /// quick pace, so it agrees with the walk Travel then makes. A person routes over the ground it has explored, an
    /// animal over any. Tiles settle in order of minutes, then tile id, and of two equal ways in the one through
    /// the tile settled first is kept, so the answer never depends on the order anything was looked at.
    /// maxMinutes: nothing farther is reached. until: told each tile as it settles, with its minutes; true ends the
    /// search there (the nearest of some goals, or travel past the best total a planner has found so far).
    /// refused: tiles the caller won't route through (never the origin). into: route into this tile from
    /// everywhere instead, each crossing priced in the direction it would be walked.
    public static RouteMap Routes(World world, AgentId who, int maxMinutes = int.MaxValue, Func<TileId, int, bool>? until = null,
        Func<TileId, bool>? refused = null, TileId? into = null)
    {
        var agent = world.Agent(who);
        var m = Map.Of(world);
        // Taken off World while in use: a search started from inside this one's callbacks gets its own.
        var scratch = world.RouteScratch is { } kept && kept.Fits(m) ? kept : new RouteScratch(m.Width, m.Height);
        world.RouteScratch = null;
        scratch.Begin();
        int search = scratch.Generation;
        var pricer = new Pricer(world, agent, Pace.Quick, Weather.At(world, world.Minute), scratch);
        var origin = into ?? agent.Tile;
        // A person with no explored ground yet reaches only where it stands.
        bool walksSeenGround = agent.Mind is PersonMind;
        var explored = (agent.Mind as PersonMind)?.Memory.Explored;
        bool Open(TileId t) => !walksSeenGround || t == agent.Tile || explored?.Contains(t) == true;
        int start = scratch.Index(origin);
        (scratch.Minutes[start], scratch.ReachedStamp[start]) = (0, search);
        scratch.Queue.Enqueue(start, (0, origin));
        while (scratch.Queue.TryDequeue(out var i, out var key))
        {
            if (scratch.SettledStamp[i] == search) continue;
            scratch.SettledStamp[i] = search;
            scratch.Settled.Add(i);
            var t = key.Tile;
            if (until?.Invoke(t, key.Minutes) == true) break;
            foreach (var (dx, dy) in MapGen.NeighborSteps) // MapGen.Neighbors' order, without an array a tile
            {
                var n = new TileId(t.X + dx, t.Y + dy);
                if (!m.InBounds(n)) continue;
                int ni = scratch.Index(n);
                if (scratch.SettledStamp[ni] == search || !Open(n) || refused?.Invoke(n) == true) continue;
                var (from, to) = into == null ? (t, n) : (n, t);
                if (Traversal.Blocked(world, agent, from, to) != null) continue;
                long reach = (long)key.Minutes + pricer.Minutes(from, to);
                if (reach > maxMinutes) continue;
                int nd = (int)reach;
                if (scratch.ReachedStamp[ni] == search && nd >= scratch.Minutes[ni]) continue;
                (scratch.Minutes[ni], scratch.Previous[ni], scratch.ReachedStamp[ni]) = (nd, i, search);
                scratch.Queue.Enqueue(ni, (nd, n));
            }
        }
        // The map owns a copy of what it reads, in settle order, so it stays true through later searches.
        var minutes = new Dictionary<TileId, int>(scratch.Settled.Count);
        var previous = new Dictionary<TileId, TileId>(scratch.Settled.Count);
        foreach (int i in scratch.Settled)
        {
            var t = scratch.Tile(i);
            minutes[t] = scratch.Minutes[i];
            if (i != start) previous[t] = scratch.Tile(scratch.Previous[i]);
        }
        world.RouteScratch = scratch;
        return new RouteMap(origin, into != null, minutes, previous, Traversal.KcalPerMinute(world, agent));
    }

    /// The odds of the one roll, on the tile walked onto, that Traversal.Begin would fix now, from the ground looked at.
    public static Odds Risk(World world, AgentId who, Direction dir, Pace pace)
    {
        var agent = world.Agent(who);
        // A crossing under way this way keeps the risk fixed when it began (Traversal.Begin resumes it).
        if (agent.Crossing is { } c && c.Dir == dir && c.Pace == pace && c.From == agent.Tile)
            return new Odds("As you set out", c.RiskPct, []);
        return Traversal.InjuryOdds(Traversal.EffectiveHazardLevel(world, Map.Step(agent.Tile, dir), agent.Tile), Traversal.PaceBody(agent), Weather.At(world, world.Minute), pace);
    }

    public static bool PaceMatters(World world, AgentId who, Direction dir) => Traversal.PaceMatters(Risk(world, who, dir, Pace.Quick).TotalPct);

    static int Careful(int segmentMinutes) => (int)Math.Ceiling(segmentMinutes * Traversal.CarefulFactor) - segmentMinutes;

}

/// What Trip.Routes found: the minutes to every tile it settled, the calories the walk burns, and the way.
/// Routes into a site (Into) run backward: each leads from a tile to the site.
public sealed class RouteMap(TileId origin, bool into, IReadOnlyDictionary<TileId, int> minutes,
    IReadOnlyDictionary<TileId, TileId> previous, double kcalPerMinute)
{
    public bool Into => into;
    /// Settled tiles only: each is the least minutes there is.
    public IReadOnlyDictionary<TileId, int> Minutes => minutes;
    public IEnumerable<TileId> Reached => minutes.Keys;
    public bool Reaches(TileId t) => minutes.ContainsKey(t);
    /// Otherwise (infinite unless given) where the search did not reach.
    public double MinutesTo(TileId t, double otherwise = double.PositiveInfinity) => minutes.TryGetValue(t, out var m) ? m : otherwise;
    /// The walk's burn at the body's rate now (Traversal.KcalPerMinute).
    public double? Kcal(TileId t) => minutes.TryGetValue(t, out var m) ? m * kcalPerMinute : null;

    /// The tiles walked onto, in order: from the origin to t, or from t into the site. Empty at the origin.
    public IReadOnlyList<TileId> RouteTo(TileId t)
    {
        if (into) return RouteBack(t);
        return RouteBack(t).Prepend(t).SkipLast(1).Reverse().ToList();
    }

    /// The tiles walked onto going back the way the search came, from t to the origin: a forecast for the way
    /// back to where the routes start from, near enough on ground that costs much the same both ways.
    public IReadOnlyList<TileId> RouteBack(TileId t)
    {
        if (!minutes.ContainsKey(t)) throw new ArgumentException($"{t} was not reached from {origin}");
        var chain = new List<TileId>();
        for (var at = t; at != origin; at = previous[at]) chain.Add(previous[at]);
        return chain;
    }

    /// The first step out of the origin toward t; null at the origin.
    public Direction? FirstStep(TileId t)
    {
        if (into) throw new InvalidOperationException("Routes into a site have no first step out of it");
        var route = RouteTo(t);
        return route.Count == 0 ? null : Enum.GetValues<Direction>().First(d => Map.Step(origin, d) == route[0]);
    }
}
