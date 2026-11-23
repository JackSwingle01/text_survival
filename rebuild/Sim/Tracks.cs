namespace Survival;

/// By shape, not identity: the player's boots and an NPC's are the same mark.
public enum TrackMaker { Human, Paw, Hoof }

/// Derived from the edge's trodden amount. The world never stores it; a person's memory keeps the tier they saw (GroundMemory).
public enum TrailTier { None, Trace, Path, Trail }

public enum TrafficPattern { Heading, NorthSouth, EastWest, Mixed }

/// Totals are as of the last passage (Minute), faded passage to passage; reading fades them again by Freshness.
public sealed record Track(long Minute, Direction Heading, double Traffic, double HeaviestIndividual,
    double DirectionX, double DirectionY, double NorthSouthTraffic, double EastWestTraffic);

/// How packed an edge's snow is, 0 to 1, as of its last stamp (Minute); the settling since is derived on read.
public sealed record EdgeTrodden(double Amount, long Minute);

/// FreshnessPct 1 = just made.
public readonly record struct TrackSign(TrackMaker Maker, double FreshnessPct, int Traffic, TrafficPattern Pattern,
    Direction Heading, long AgeMinutes, double Depth, int NorthSouthTraffic, int EastWestTraffic);

/// Footprints, aged against erosion, and trodden edges, settling and buried by snowfall. The sky is a pure function
/// of (seed, minute), so erosion and snowfall are derived (ErosionAt, SnowfallWeAt), never ticked or saved; prints
/// and edges store the minute they were last added to and catch up on read.
public static class Tracks
{
    // ---- erosion ----

    /// Erosion units a Depth-1 track survives.
    public const double BaseLifespanUnits = 2880;
    // One person's prints last ~4 days in dead calm cold, 17 hours in light snow, 6 hours in a blizzard.
    const double StillAirUnits = 0.5, PrecipitationUnits = 6.0, WindUnits = 3.0, ThawUnits = 2.0, ThawAboveF = 34;

    public static double ErosionPerMinute(WeatherSample s) =>
        StillAirUnits + s.PrecipitationPct * PrecipitationUnits + s.WindLevel * WindUnits + (s.TemperatureF > ThawAboveF ? ThawUnits : 0);

    /// Total erosion over minutes 1..minute, each under its own sky. Monotonic; an age is a difference of two.
    public static double ErosionAt(World world, long minute)
    {
        if (minute < 0) throw new ArgumentOutOfRangeException(nameof(minute), minute, "Erosion starts at minute 0");
        // A pinned sky has been the sky since minute 0: World refuses to change it once the clock moves.
        if (world.FixedWeather is WeatherSample sky) return ErosionPerMinute(sky) * minute;
        return (world.ErosionCache ??= new SkyTimeline(ErosionPerMinute)).At(world, minute);
    }

    /// Snow fallen on open ground over minutes 1..minute, metres water-equivalent. Monotonic, like ErosionAt.
    public static double SnowfallWeAt(World world, long minute)
    {
        if (minute < 0) throw new ArgumentOutOfRangeException(nameof(minute), minute, "Snowfall is counted from minute 0");
        if (world.FixedWeather is WeatherSample sky) return sky.SnowfallWeMPerMinute * minute;
        return (world.SnowfallCache ??= new SkyTimeline(s => s.SnowfallWeMPerMinute)).At(world, minute);
    }

    // ---- footprints ----

    /// An agent's completed crossing: each walker marks the ground by its own weight; a flyer touches nothing on the way.
    public static void RecordMove(World world, Agent agent, TileId from, TileId to)
    {
        if (Traversal.FlightMinutesPerTile(agent) != null) return;
        foreach (var _ in agent.Members) RecordMove(world, from, to, MakerOf(agent.Species), IndividualDepth(agent.Species));
    }

    /// The single seam for a completed crossing, one walker at a time; an adult human's individualDepth is 1.0.
    public static void RecordMove(World world, TileId from, TileId to, TrackMaker maker, double individualDepth = 1.0)
    {
        if (from == to) return;
        if (individualDepth <= 0) throw new ArgumentOutOfRangeException(nameof(individualDepth), individualDepth, "a move needs feet");
        long now = world.Minute;
        double erosion = ErosionAt(world, now);
        // At most hourly: with herds walking, a sweep per crossing dominates a long run's cost.
        if (now / 60 != world.TracksSweptHour)
        {
            world.TracksSweptHour = now / 60;
            Sweep(world, now, erosion);
        }

        var heading = HeadingOf(from, to);
        Stamp(world, from, maker, heading, individualDepth, now, erosion);
        Stamp(world, to, maker, heading, individualDepth, now, erosion);

        // Only neighbours share an edge: crediting one arbitrary edge for a multi-tile hop would pack a route nothing walked.
        if (Math.Abs(from.X - to.X) + Math.Abs(from.Y - to.Y) != 1) return;
        // Saturating, as grazing does: the first crossings pack the most, and each packs a share of what is still loose.
        var edge = new EdgeId(from, to);
        double settled = TroddenAt(world, edge, now);
        world.Trails[edge] = new EdgeTrodden(settled + (1 - settled) * Math.Min(1, StampPct * individualDepth), now);
        // The minute's forecasts over this edge read it loose; every other crossing's price stands.
        if (world.StepMinutesCache is { } cache)
            foreach (var key in cache.Minutes.Keys.Where(k => (k.From == from && k.To == to) || (k.From == to && k.To == from)).ToList())
                cache.Minutes.Remove(key);
    }

    /// Freshest first.
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

    /// Sets how long the sign lasts: linear in the heaviest thing through, sublinear in how many came.
    public static double Depth(Track t) => Math.Clamp(t.HeaviestIndividual * (0.6 + 0.4 * Math.Sqrt(Math.Max(1, t.Traffic))), 0.5, 3.0);

    static double Freshness(World world, Track t, double erosionNow) =>
        Math.Clamp(1 - (erosionNow - ErosionAt(world, t.Minute)) / (BaseLifespanUnits * Depth(t)), 0, 1);

    static void Stamp(World world, TileId tile, TrackMaker maker, Direction heading, double individualDepth, long now, double erosion)
    {
        if (!world.Tiles.TryGetValue(tile, out var state)) world.Tiles[tile] = state = new TileState();
        state.Tracks ??= [];
        double traffic = 1, heaviest = individualDepth, dx = 0, dy = 0, ns = 0, ew = 0;
        // What the weather left of the earlier sign carries over: daily routes read busier, yet nothing piles up forever.
        // A dead sign is no sign, whether or not the sweep has dropped it yet.
        if (state.Tracks.TryGetValue(maker, out var old) && Freshness(world, old, erosion) is var f && f > 0)
        {
            traffic += old.Traffic * f;
            heaviest = Math.Max(heaviest, old.HeaviestIndividual);
            dx = old.DirectionX * f; dy = old.DirectionY * f;
            ns = old.NorthSouthTraffic * f; ew = old.EastWestTraffic * f;
        }
        switch (heading)
        {
            case Direction.North: dy -= 1; ns += 1; break;
            case Direction.South: dy += 1; ns += 1; break;
            case Direction.East: dx += 1; ew += 1; break;
            case Direction.West: dx -= 1; ew += 1; break;
        }
        state.Tracks[maker] = new Track(now, heading, traffic, heaviest, dx, dy, ns, ew);
    }

    // A multi-tile hop reads by its larger axis.
    static Direction HeadingOf(TileId from, TileId to)
    {
        int dx = to.X - from.X, dy = to.Y - from.Y;
        if (Math.Abs(dx) >= Math.Abs(dy)) return dx >= 0 ? Direction.East : Direction.West;
        return dy >= 0 ? Direction.South : Direction.North;
    }

    static Direction DominantHeading(Track t)
    {
        if (Math.Abs(t.DirectionX) < double.Epsilon && Math.Abs(t.DirectionY) < double.Epsilon) return t.Heading;
        if (Math.Abs(t.DirectionX) >= Math.Abs(t.DirectionY)) return t.DirectionX >= 0 ? Direction.East : Direction.West;
        return t.DirectionY >= 0 ? Direction.South : Direction.North;
    }

    static int Readable(double traffic, double freshness) => traffic * freshness <= 0 ? 0 : Math.Max(1, (int)Math.Round(traffic * freshness));

    /// An axis dominates at 1.5x the other, and within it the leading way dominates at 1.5x the opposing.
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

    /// Drop what the weather has finished with so the save holds only what is there; erosion only
    /// rises, so dropping a dead sign changes no reading. O(tiles + edges), hence the hourly cap.
    static void Sweep(World world, long now, double erosion)
    {
        foreach (var (tile, state) in world.Tiles.ToList())
        {
            if (state.Tracks == null) continue;
            foreach (var (maker, t) in state.Tracks.ToList())
                if (Freshness(world, t, erosion) <= 0) state.Tracks.Remove(maker);
            if (state.Tracks.Count > 0) continue;
            state.Tracks = null;
            // Ground never lets go of a tile it has observed; pools, stashes and carcasses are kept too.
            Map.Tidy(world, tile);
        }
        foreach (var (edge, _) in world.Trails.ToList())
            if (TroddenAt(world, edge, now) <= 0) world.Trails.Remove(edge);
    }

    // ---- trodden edges ----

    /// Trodden amount for each tier: about 5, 14 and 32 adult human crossings in a row.
    public const double TraceAt = 0.2, PathAt = 0.5, TrailAt = 0.8;
    /// The share of an edge's still-loose snow one adult human's crossing packs; IndividualDepth scales it.
    public const double StampPct = 0.05;
    /// Settling and regrowth, whatever the sky: a saturated edge is gone in 35 days, a Trail in four weeks.
    const double SettlingPerMinute = 1.0 / (35 * 1440);
    const double MinimumTrodden = 0.01;

    /// Settled since its last stamp; snow fallen on it does not lower it.
    public static double TroddenAt(World world, TileId a, TileId b) => TroddenAt(world, new EdgeId(a, b), world.Minute);

    static double TroddenAt(World world, EdgeId edge, long now)
    {
        if (!world.Trails.TryGetValue(edge, out var t)) return 0;
        double amount = t.Amount - (now - t.Minute) * SettlingPerMinute;
        return amount < MinimumTrodden ? 0 : amount;
    }

    /// At tile's half: the trodden amount, less the share of tile's snow fallen since the last stamp.
    public static double TroddenLevel(World world, TileId tile, TileId other, double snowDepthM)
    {
        var edge = new EdgeId(tile, other);
        if (snowDepthM <= 0 || !world.Trails.TryGetValue(edge, out var t)) return 0;
        double amount = TroddenAt(world, edge, world.Minute);
        if (amount <= 0) return 0;
        double fallenM = Ground.FreshSnowDepthM(Exposure.SnowfallWeSince(world, tile, t.Minute));
        return amount * (1 - Math.Min(1, fallenM / snowDepthM));
    }

    public static TrailTier TierFor(double trodden) => trodden switch
    {
        >= TrailAt => TrailTier.Trail,
        >= PathAt => TrailTier.Path,
        >= TraceAt => TrailTier.Trace,
        _ => TrailTier.None,
    };

    public static TrailTier TierOf(World world, TileId a, TileId b) => TierFor(TroddenAt(world, a, b));

    // ---- who leaves what ----

    public static TrackMaker MakerOf(Species species) => SpeciesTable.Of(species).Role switch
    {
        Role.Human => TrackMaker.Human,
        Role.Predator or Role.Scavenger => TrackMaker.Paw,
        _ => TrackMaker.Hoof,
    };

    /// Square root of body mass, so a mammoth prints far deeper than a fox, not seventy times.
    public static double IndividualDepth(Species species) =>
        species == Species.Human ? 1.0 : Math.Clamp(Math.Sqrt(SpeciesTable.Of(species).WeightKg / 70.0), 0.4, 3.0);
}

/// Prefix sums of a per-minute rate of the sky over the seed's front states. Derived; rebuilt after a load.
internal sealed class SkyTimeline(Func<WeatherSample, double> ratePerMinute)
{
    readonly List<long> starts = [];
    // From the weather pre-roll start; only differences are read, so the origin cancels.
    readonly List<double> before = [];
    readonly List<double> rates = [];
    long coveredThrough = -1;

    public double At(World world, long minute) => Before(world, minute + 1) - Before(world, 1);

    double Before(World world, long n)
    {
        long last = n - 1;
        if (last > coveredThrough)
        {
            foreach (var s in Weather.SpellsUntil(world, last).Skip(starts.Count))
            {
                before.Add(starts.Count == 0 ? 0 : before[^1] + rates[^1] * (s.Start - starts[^1]));
                starts.Add(s.Start);
                rates.Add(ratePerMinute(new WeatherSample(s.Start, s.Condition, s.TemperatureC, s.WindLevel,
                    s.PrecipitationPct, s.CloudCoverPct, s.Heading, s.Front)));
            }
            coveredThrough = last;
        }
        int i = starts.BinarySearch(last);
        if (i < 0) i = ~i - 1;
        return before[i] + rates[i] * (n - starts[i]);
    }
}
