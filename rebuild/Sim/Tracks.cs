namespace Survival;

/// The kind of print left on the ground: by shape, not identity. The player's boots and an
/// NPC's are the same mark; what a print tells you is two feet, pads or hooves.
public enum TrackMaker { Human, Paw, Hoof }

/// How far a route has been beaten in. Derived from the wear scalar, never stored.
public enum TrailTier { None, Trace, Path, Trail }

/// How traffic through a tile reads: mostly one way (Heading), back and forth along one axis, or mixed.
public enum TrafficPattern { Heading, NorthSouth, EastWest, Mixed }

/// The sign one kind of maker has left on one tile, as of the last passage (Minute).
/// Traffic and the direction totals are what had come through by then, faded passage to
/// passage; reading them now fades them again by Freshness.
public sealed record Track(long Minute, Direction Heading, double Traffic, double HeaviestIndividual,
    double DirectionX, double DirectionY, double NorthSouthTraffic, double EastWestTraffic);

/// Desire-path wear on one edge as of Minute. Decay since then is derived on read.
public sealed record TrailWear(double Wear, long Minute);

/// What a tile's ground says about one kind of maker right now. FreshnessPct 1 = just made.
public readonly record struct TrackSign(TrackMaker Maker, double FreshnessPct, int Traffic, TrafficPattern Pattern,
    Direction Heading, long AgeMinutes, double Depth, int NorthSouthTraffic, int EastWestTraffic);

/// Footprints and desire paths: the ground remembers who walked on it, on two clocks. A port of
/// the old TrackRegistry, TrailWear and GameMap.RecordMove / AdvanceGround / GetEdgeTraversalModifier.
///
/// Both age against erosion. The old game kept one accumulator advanced every minute; here the sky
/// is a pure function of (seed, minute), so erosion at any minute is derived from it (ErosionAt)
/// and nothing is ticked or saved. Tracks and wear store the minute they were last added to and
/// catch up on read (plan decision 3).
public static class Tracks
{
    // ---- erosion (old TrackRegistry.ErosionPerMinute) ----

    /// Erosion units a Depth-1 track survives.
    public const double BaseLifespanUnits = 2880;
    // Roughly 4 days in dead calm cold, 17 hours in light snow, 6 hours in a blizzard for one person.
    const double StillAirUnits = 0.5, PrecipitationUnits = 6.0, WindUnits = 3.0, ThawUnits = 2.0, ThawAboveF = 34;

    /// How fast the ground is wiped clean: falling snow buries, wind drifts, a thaw slumps, still air fills slowly.
    public static double ErosionPerMinute(WeatherSample s) =>
        StillAirUnits + s.PrecipitationPct * PrecipitationUnits + s.WindLevel * WindUnits + (s.TemperatureF > ThawAboveF ? ThawUnits : 0);

    /// Total erosion over the minutes lived up to and including minute (minutes 1..minute, each
    /// under its own sky, as Step lives them). Monotonic; an age is a difference of two of these.
    public static double ErosionAt(World world, long minute)
    {
        if (minute < 0) throw new ArgumentOutOfRangeException(nameof(minute), minute, "Erosion starts at minute 0");
        // A pinned sky has been the sky since minute 0: World refuses to change it once the clock moves.
        if (world.FixedWeather is WeatherSample sky) return ErosionPerMinute(sky) * minute;
        return (world.ErosionCache ??= new ErosionTimeline()).At(world, minute);
    }

    // ---- footprints (old TrackRegistry) ----

    /// The single seam for a completed crossing, whoever made it: prints on both tiles, wear on the
    /// edge between them. individuals is head count (a herd agent's Members.Count); individualDepth
    /// is how heavily one of them marks the ground, an adult human at 1.0.
    public static void RecordMove(World world, TileId from, TileId to, TrackMaker maker, double individuals = 1, double individualDepth = 1.0)
    {
        if (from == to) return;
        if (individuals <= 0 || individualDepth <= 0)
            throw new ArgumentOutOfRangeException(nameof(individuals), $"{individuals} x {individualDepth}: a move needs feet");
        long now = world.Minute;
        double erosion = ErosionAt(world, now);
        Sweep(world, now, erosion);

        var heading = HeadingOf(from, to);
        Stamp(world, from, maker, heading, individuals, individualDepth, now, erosion);
        Stamp(world, to, maker, heading, individuals, individualDepth, now, erosion);

        // Wear belongs to an edge, and an edge exists only between neighbours: crediting one
        // arbitrary edge for a multi-tile hop would wear in a route nothing walked.
        if (Math.Abs(from.X - to.X) + Math.Abs(from.Y - to.Y) != 1) return;
        // Linear in head count: every set of feet treads the ground.
        var edge = new EdgeId(from, to);
        world.Trails[edge] = new TrailWear(Math.Min(MaxWear, WearAt(world, edge, now, erosion) + individuals * individualDepth), now);
    }

    /// Readable signs on a tile, freshest first. Empty when the ground is clean.
    public static IReadOnlyList<TrackSign> Read(World world, TileId tile)
    {
        if (!world.Tiles.TryGetValue(tile, out var state) || state.Tracks == null) return [];
        double erosion = ErosionAt(world, world.Minute);
        var signs = new List<TrackSign>();
        foreach (var (maker, t) in state.Tracks)
        {
            double f = Freshness(world, t, erosion);
            if (f > 0) signs.Add(Sign(maker, t, f, world.Minute));
        }
        signs.Sort((a, b) => b.FreshnessPct != a.FreshnessPct ? b.FreshnessPct.CompareTo(a.FreshnessPct) : a.Maker.CompareTo(b.Maker));
        return signs;
    }

    /// How deeply the ground is marked, and so how long the sign lasts: linear in the heaviest
    /// thing through, sublinear in how many came.
    public static double Depth(Track t) => Math.Clamp(t.HeaviestIndividual * (0.6 + 0.4 * Math.Sqrt(Math.Max(1, t.Traffic))), 0.5, 3.0);

    static double Freshness(World world, Track t, double erosionNow) =>
        Math.Clamp(1 - (erosionNow - ErosionAt(world, t.Minute)) / (BaseLifespanUnits * Depth(t)), 0, 1);

    static void Stamp(World world, TileId tile, TrackMaker maker, Direction heading, double individuals, double individualDepth, long now, double erosion)
    {
        if (!world.Tiles.TryGetValue(tile, out var state)) world.Tiles[tile] = state = new TileState();
        state.Tracks ??= [];
        double traffic = individuals, heaviest = individualDepth, dx = 0, dy = 0, ns = 0, ew = 0;
        // Whatever the weather has left of the earlier sign carries over, so a route in daily use
        // reads busier than one crossed once - and nothing piles up forever, because it has faded.
        // old game: a dead but not-yet-pruned sign still passed on its heaviest walker, so Depth
        // depended on when the prune last ran (bug, fixed: a dead sign is no sign).
        if (state.Tracks.TryGetValue(maker, out var old) && Freshness(world, old, erosion) is var f && f > 0)
        {
            traffic += old.Traffic * f;
            heaviest = Math.Max(heaviest, old.HeaviestIndividual);
            dx = old.DirectionX * f; dy = old.DirectionY * f;
            ns = old.NorthSouthTraffic * f; ew = old.EastWestTraffic * f;
        }
        switch (heading)
        {
            case Direction.North: dy -= individuals; ns += individuals; break;
            case Direction.South: dy += individuals; ns += individuals; break;
            case Direction.East: dx += individuals; ew += individuals; break;
            case Direction.West: dx -= individuals; ew += individuals; break;
        }
        state.Tracks[maker] = new Track(now, heading, traffic, heaviest, dx, dy, ns, ew);
    }

    /// Which way something was heading; a multi-tile hop reads by its larger axis.
    static Direction HeadingOf(TileId from, TileId to)
    {
        int dx = to.X - from.X, dy = to.Y - from.Y;
        if (Math.Abs(dx) >= Math.Abs(dy)) return dx >= 0 ? Direction.East : Direction.West;
        return dy >= 0 ? Direction.South : Direction.North;
    }

    /// Old Track.DominantHeading: strongest net movement; the last heading when it cancels out.
    static Direction DominantHeading(Track t)
    {
        if (Math.Abs(t.DirectionX) < double.Epsilon && Math.Abs(t.DirectionY) < double.Epsilon) return t.Heading;
        if (Math.Abs(t.DirectionX) >= Math.Abs(t.DirectionY)) return t.DirectionX >= 0 ? Direction.East : Direction.West;
        return t.DirectionY >= 0 ? Direction.South : Direction.North;
    }

    static int Readable(double traffic, double freshness) => traffic * freshness <= 0 ? 0 : Math.Max(1, (int)Math.Round(traffic * freshness));

    /// Old LocationInspector.DirectionSummary, as data: an axis dominates at 1.5x the other, and
    /// within it the leading way dominates at 1.5x the opposing.
    static TrackSign Sign(TrackMaker maker, Track t, double f, long now)
    {
        double ns = t.NorthSouthTraffic * f, ew = t.EastWestTraffic * f;
        bool nsDominant = ns >= ew * 1.5, ewDominant = ew >= ns * 1.5;
        var pattern = TrafficPattern.Mixed;
        if (nsDominant || ewDominant)
        {
            double axis = nsDominant ? ns : ew;
            double net = Math.Abs((nsDominant ? t.DirectionY : t.DirectionX) * f);
            double opposing = Math.Max(0, axis - net) / 2, leading = (axis + net) / 2;
            pattern = opposing <= 0 || leading >= opposing * 1.5 ? TrafficPattern.Heading
                : nsDominant ? TrafficPattern.NorthSouth : TrafficPattern.EastWest;
        }
        return new TrackSign(maker, f, Readable(t.Traffic, f), pattern, DominantHeading(t), now - t.Minute, Depth(t),
            Readable(t.NorthSouthTraffic, f), Readable(t.EastWestTraffic, f));
    }

    /// Drop signs and wear the weather has finished with, so the save holds only what is there.
    /// Erosion only rises, so dead stays dead and dropping it changes no reading.
    // ponytail: a full sweep per crossing, O(tiles + edges). Sweep on a clock if phase-3 herds make moves frequent.
    static void Sweep(World world, long now, double erosion)
    {
        foreach (var (tile, state) in world.Tiles)
        {
            if (state.Tracks == null) continue;
            foreach (var (maker, t) in state.Tracks)
                if (Freshness(world, t, erosion) <= 0) state.Tracks.Remove(maker);
            if (state.Tracks.Count > 0) continue;
            state.Tracks = null;
            // The only place a tile can become clean: Ground never lets go of a tile it has observed.
            if (state.Ground == null) world.Tiles.Remove(tile);
        }
        foreach (var (edge, _) in world.Trails)
            if (WearAt(world, edge, now, erosion) <= 0) world.Trails.Remove(edge);
    }

    // ---- desire paths (old TrailWear) ----

    /// Wear for each tier. One adult human crossing adds 1, so these read roughly as net crossings.
    public const double TraceAt = 5, PathAt = 14, TrailAt = 35;
    /// Wear stops accumulating here, so an abandoned trail fades in a bounded time.
    public const double MaxWear = 50;
    /// Settling and regrowth, whatever the sky: about 0.44 wear a day, so an abandoned Path is gone in a month.
    const double SettlingPerMinute = 0.000306;
    /// Snow filling the trench, per erosion unit. Deliberately the smaller term: burying a path is not undoing it.
    const double BuryingPerErosionUnit = 0.0000837;
    /// Below this the route is gone.
    const double MinimumWear = 0.5;

    /// How beaten-in the edge between two tiles is right now.
    public static double WearAt(World world, TileId a, TileId b) => WearAt(world, new EdgeId(a, b), world.Minute, ErosionAt(world, world.Minute));

    static double WearAt(World world, EdgeId edge, long now, double erosionNow)
    {
        if (!world.Trails.TryGetValue(edge, out var w)) return 0;
        double wear = w.Wear - (now - w.Minute) * SettlingPerMinute - (erosionNow - ErosionAt(world, w.Minute)) * BuryingPerErosionUnit;
        return wear < MinimumWear ? 0 : wear;
    }

    public static TrailTier TierFor(double wear) => wear switch
    {
        >= TrailAt => TrailTier.Trail,
        >= PathAt => TrailTier.Path,
        >= TraceAt => TrailTier.Trace,
        _ => TrailTier.None,
    };

    public static TrailTier TierOf(World world, TileId a, TileId b) => TierFor(WearAt(world, a, b));

    /// Minutes a beaten route saves. A trace is only visible; you have to beat a route properly first.
    public static int TrailMinutes(World world, TileId a, TileId b) => TierOf(world, a, b) switch
    {
        TrailTier.Trail => -2,
        TrailTier.Path => -1,
        _ => 0,
    };

    // ---- who leaves what ----

    // ponytail: humans only until phase 3 species. Then Paw for predators, Hoof for the rest, and depth
    // Math.Clamp(Math.Sqrt(weightKg / 70), 0.4, 3.0) per member (old AnimalType.Tracks / IndividualTrackDepth).
    public static TrackMaker MakerOf(Species species) => species switch
    {
        Species.Human => TrackMaker.Human,
        _ => throw new ArgumentOutOfRangeException(nameof(species), species, "No track maker"),
    };

    public static double IndividualDepth(Species species) => species switch
    {
        Species.Human => 1.0,
        _ => throw new ArgumentOutOfRangeException(nameof(species), species, "No track depth"),
    };
}

/// Prefix sums of erosion over the seed's front states, which hold the sky constant from one
/// start to the next. Pure derived data, rebuilt from the seed after a load.
internal sealed class ErosionTimeline
{
    readonly List<long> starts = [];
    // Erosion from the start of the weather pre-roll to each start. Only differences are ever read,
    // so the origin (Weather.PreRollMinutes before minute 0) cancels out.
    readonly List<double> before = [];
    readonly List<double> rates = [];
    long coveredThrough = -1;

    public double At(World world, long minute) => Before(world, minute + 1) - Before(world, 1);

    /// Erosion over minutes [pre-roll start, n).
    double Before(World world, long n)
    {
        long last = n - 1;
        if (last > coveredThrough)
        {
            foreach (var s in Weather.SpellsUntil(world, last).Skip(starts.Count))
            {
                before.Add(starts.Count == 0 ? 0 : before[^1] + rates[^1] * (s.Start - starts[^1]));
                starts.Add(s.Start);
                rates.Add(Tracks.ErosionPerMinute(new WeatherSample(s.Start, s.Condition, s.TemperatureC, s.WindLevel,
                    s.PrecipitationPct, s.CloudCoverPct, s.Heading, s.Front)));
            }
            coveredThrough = last;
        }
        int i = starts.BinarySearch(last);
        if (i < 0) i = ~i - 1;
        return before[i] + rates[i] * (n - starts[i]);
    }
}
