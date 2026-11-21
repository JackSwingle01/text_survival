namespace Survival;

/// A crossing's minutes as the terms that make them, in the order Traversal.CrossingMinutes builds
/// them, so they add up to its total exactly (ui-design: every time in the dock equals the preview).
/// Each slowing is the extra minutes over the stage before it, per segment, rounded as the crossing
/// rounds them:
/// - Walking: the two tiles' base minutes; Terrain: the shape of the ground; Weather: strong wind and
///   heavy precipitation; Pace: this body's own speed, legs, wounds and build (negative for a quick one);
///   Load: what it carries; Careful: half again on hazardous ground, taken carefully.
/// - Edge: an authored edge (a ravine, a game trail); Trail: a path beaten in by use (negative).
/// - Ground: today's snow, water and mud, with the whole sum's rounding up; Shortest: what the
///   five-minute floor adds to a crossing shorter than that.
public sealed record TripTime(int WalkingMinutes, int TerrainMinutes, int WeatherMinutes, int PaceMinutes, int LoadMinutes,
    int CarefulMinutes, int EdgeMinutes, int TrailMinutes, int GroundMinutes, int ShortestMinutes)
{
    public int TotalMinutes => WalkingMinutes + TerrainMinutes + WeatherMinutes + PaceMinutes + LoadMinutes + CarefulMinutes
        + EdgeMinutes + TrailMinutes + GroundMinutes + ShortestMinutes;

    /// Every term, named, zeros included, in order: what the preview lists (leaving out the zeros).
    public IReadOnlyList<(string Factor, int Minutes)> Terms =>
    [
        ("Walking", WalkingMinutes), ("Terrain", TerrainMinutes), ("Weather", WeatherMinutes), ("Pace", PaceMinutes),
        ("Load", LoadMinutes), ("Careful", CarefulMinutes), ("Edge", EdgeMinutes), ("Trail", TrailMinutes),
        ("Snow and ground", GroundMinutes), ("Shortest crossing", ShortestMinutes),
    ];
}

/// A crossing as seen before it is made: for verb lists and the trip preview. Reads only: the
/// ground is looked at (Ground.Peek), never observed, so asking changes nothing in the world.
// Traversal.CrossingMinutes has its own copy of the formula, and TripTests holds the two equal across
// the valley. When Travel.cs is free, CrossingMinutes becomes Terms(..., observe: true).TotalMinutes.
public static class Trip
{
    /// The crossing this agent would make now, one tile this way at this pace.
    public static TripTime Time(World world, AgentId who, Direction dir, Pace pace)
    {
        var agent = world.Agent(who);
        return Terms(world, agent.Body, agent.Tile, Map.Step(agent.Tile, dir), pace, Weather.At(world, world.Minute), Items.EncumbrancePct(agent));
    }

    /// Traversal.CrossingMinutes as its terms. observe: catch the ground up as the crossing itself
    /// does (writing it to the world), rather than look at it.
    public static TripTime Terms(World world, BodyState body, TileId from, TileId to, Pace pace, WeatherSample sky,
        double encumbrancePct = 0, bool observe = false)
    {
        var m = Map.Of(world);
        double unloaded = Body.Speed(body), speed = Body.Speed(body, new AbilityContext(EncumbrancePct: encumbrancePct));
        var still = sky with { WindLevel = 0, PrecipitationPct = 0 }; // no wind or precipitation to slow a walk
        var (exit, entry) = (Segment(from), Segment(to));
        Func<World, TileId, double> ground = observe ? Ground.TraversalFactor : Ground.TraversalFactorNow;
        Func<World, TileId, bool> hazardous = observe ? Traversal.IsHazardous : IsHazardousNow;
        // Traversal.SurfaceExcessMinutes, from the segment before a careful pace stretches it.
        double surface = exit.Minutes * (ground(world, from) - 1) + entry.Minutes * (ground(world, to) - 1);
        int careful = pace == Pace.Careful
            ? (hazardous(world, from) ? Careful(exit.Minutes) : 0) + (hazardous(world, to) ? Careful(entry.Minutes) : 0)
            : 0;
        int edge = m.EdgeBetween(from, to)?.TraversalMinutes() ?? 0, trail = Tracks.TrailMinutes(world, from, to);
        int whole = exit.Minutes + entry.Minutes + careful + edge + trail;
        int rounded = (int)Math.Ceiling(whole + surface);
        return new TripTime(exit.Walking + entry.Walking, exit.Terrain + entry.Terrain, exit.Weather + entry.Weather,
            exit.Pace + entry.Pace, exit.Load + entry.Load, careful, edge, trail,
            GroundMinutes: rounded - whole, ShortestMinutes: Math.Max(0, Traversal.MinimumCrossingMinutes - rounded));

        // One tile's segment, built up stage by stage; Minutes is Traversal.SegmentMinutes itself.
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

    static int Careful(int segmentMinutes) => (int)Math.Ceiling(segmentMinutes * Traversal.CarefulFactor) - segmentMinutes;

    /// Traversal.IsHazardous, without observing the ground.
    public static bool IsHazardousNow(World world, TileId tile) =>
        Math.Min(1.0, Map.TravelHazardLevel(Map.Of(world), tile) + Ground.HazardDeltaNow(world, tile)) >= Traversal.HazardousLevel;
}
