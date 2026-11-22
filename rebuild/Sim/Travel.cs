namespace Survival;

/// Careful: half again the time where care matters, for a fraction of the risk (Footing).
public enum Pace { Quick, Careful }

/// How bad footing turns into a fall (terrain.json footing).
public sealed record Footing(double InjuryExponent, double InjuryDivisor, double CarefulRiskFraction, double PaceMattersAbovePct);

/// A crossing under way. The agent stays on its tile until ElapsedMinutes reaches TotalMinutes.
/// The risk is fixed when the crossing starts, before the walk.
public sealed class Crossing
{
    /// Where the crossing started. Only resumed from this tile: an agent moved by anything else starts afresh.
    public required TileId From { get; init; }
    public required Direction Dir { get; init; }
    public required Pace Pace { get; init; }
    public required int TotalMinutes { get; init; }
    public int ElapsedMinutes { get; set; }
    public double RiskPct { get; init; }
}

/// One accident's mechanics (Content/accidents.json): a hit of Amount and Type on Target, and an
/// effect it may start at Severity. A class, not a struct, so the reader requires Target, Type and
/// Amount (a struct row would default them).
public sealed record Accident(BodyTarget Target, DamageType Type, double Amount, EffectKind? Effect = null, double Severity = 0);

/// Walking one tile north, east, south or west.
public static class Traversal
{
    public const double CarefulFactor = 1.5;
    static readonly Footing FootingRules = Content.Tables.Footing;
    /// Shortest a crossing can take, however many trail bonuses stack on it.
    public const int MinimumCrossingMinutes = 5;

    /// Why this move can't be made, or null. Checked before any time passes.
    public static string? Refusal(World world, Travel t)
    {
        var agent = world.Agent(t.Who);
        return CannotWalk(agent) ?? Map.Blocked(Map.Of(world), agent.Tile, Map.Step(agent.Tile, t.Dir));
    }

    /// Why this agent cannot walk on, or null: checked when a walk starts and every minute of it
    /// (Advance), so a crossing stops at the same minute however its minutes are cut into steps.
    /// The map's blocks are fixed at generation and need no re-check.
    static string? CannotWalk(Agent agent) =>
        agent.Members.Where(Body.IsAlive).Min(m => Body.GetCapacities(m).Moving) <= 0.1 ? "you can barely move" : null;

    /// The crossing this activity continues, or a fresh one. A crossing in another direction, at
    /// another pace, or from a tile the agent is no longer on, is abandoned.
    public static Crossing Begin(World world, Travel t)
    {
        var agent = world.Agent(t.Who);
        if (agent.Crossing is { } c && c.Dir == t.Dir && c.Pace == t.Pace && c.From == agent.Tile) return c;
        var from = agent.Tile;
        var to = Map.Step(from, t.Dir);
        var sky = Weather.At(world, world.Minute);
        if (agent.Mind is HerdMind)
            return agent.Crossing = new Crossing { From = from, Dir = t.Dir, Pace = t.Pace, TotalMinutes = HerdCrossingMinutes(world, agent, from, to, sky) };
        return agent.Crossing = new Crossing
        {
            From = from,
            Dir = t.Dir,
            Pace = t.Pace,
            TotalMinutes = CrossingMinutes(world, agent.Body, from, to, t.Pace, sky, Items.EncumbrancePct(agent)),
            RiskPct = InjuryRiskPct(world, to, agent.Body, sky, t.Pace),
        };
    }

    /// One minute of walking lived. On the last one the agent arrives and rolls for injury on the tile
    /// it walked onto. Returns why to stop, if anything. A minute that leaves
    /// the agent too lame to go on (CannotWalk) is walked, and stops it short of the far side, on its
    /// tile with the crossing kept, as a step that ends there and a refused next Start would.
    public static string? Advance(World world, Agent agent, List<WorldEvent> events)
    {
        var c = agent.Crossing ?? throw new InvalidOperationException($"{agent.Id} is not crossing");
        if (++c.ElapsedMinutes < c.TotalMinutes) return CannotWalk(agent);

        if (c.From != agent.Tile) throw new InvalidOperationException($"{agent.Id} is crossing from {c.From} but stands on {agent.Tile}");
        var from = c.From;
        var to = Map.Step(from, c.Dir);
        agent.Tile = to;
        agent.Crossing = null;
        events.Add(new Moved(world.Minute, agent.Id, from, to));
        // Every agent's completed crossing marks the ground, a herd with all its feet. The carrion flock
        // is mostly on the wing, and the fox in it too light to mark the ground (judgment).
        if (Fauna.BehaviorOf(agent.Species) != HerdBehavior.Carrion)
            Tracks.RecordMove(world, from, to, Tracks.MakerOf(agent.Species), agent.Members.Count, Tracks.IndividualDepth(agent.Species));

        if (c.RiskPct > 0 && world.Rng("travel", agent.Id).NextDouble() < c.RiskPct && Injure(world, agent, to, events) is string stop) return stop;
        if (to == Map.Of(world).Exit && agent.Mind is PersonMind)
        {
            events.Add(new CrossedThePass(world.Minute, agent.Id, to));
            return CrossedThePassReason;
        }
        return Arrived;
    }

    /// The crossing's own ends: on the far tile, or on the far side of the pass.
    public const string Arrived = "arrived", CrossedThePassReason = "crossed the pass";

    // ---- cost ----

    /// The time the crossing actually lasts: the ground, the pace and the surface excess.
    /// encumbrancePct is carried weight over capacity (Items.EncumbrancePct): past half, a load slows you.
    /// The formula and its terms live in one place, Trip.Terms, which the trip preview reads too.
    public static int CrossingMinutes(World world, BodyState body, TileId from, TileId to, Pace pace, WeatherSample sky, double encumbrancePct = 0) =>
        Trip.Terms(world, body, from, to, pace, sky, encumbrancePct, observe: true).TotalMinutes;

    /// A herd moves at its slowest member's pace over the shape of the ground and the edge, and
    /// does not stumble (no injury roll).
    // Herds ignore today's snow and mud. Reading it would catch up the ground on every tile
    // a herd crosses (a month of history each, and a ground state in the save); add it if herds in
    // deep snow should visibly slow.
    public static int HerdCrossingMinutes(World world, Agent herd, TileId from, TileId to, WeatherSample sky)
    {
        var m = Map.Of(world);
        double speed = herd.Members.Where(Body.IsAlive).Min(b => Body.Speed(b));
        return Math.Max(MinimumCrossingMinutes, SegmentMinutes(m, from, speed, sky) + SegmentMinutes(m, to, speed, sky) + EdgeMinutes(world, from, to));
    }

    /// Leaving or entering one tile: its base minutes, slowed by the shape of the ground, by a
    /// strong wind or heavy precipitation, and by how slowly this body moves.
    public static int SegmentMinutes(GeneratedMap m, TileId tile, double speed, WeatherSample sky)
    {
        double multiplier = Map.TravelHazardLevel(m, tile);
        if (sky.WindLevel > 0.5) multiplier *= 1 + Exposure.OpenWindLevel(m, tile, sky) * 0.3;
        if (sky.PrecipitationPct > 0.5) multiplier *= 1 + sky.PrecipitationPct * 0.2;
        return (int)Math.Ceiling(Map.BaseTraversalMinutes(m, tile) * (1 + multiplier) / Math.Max(0.1, speed));
    }

    /// Minutes the edge between two tiles adds: authored edges, plus however far the route has been
    /// beaten in by use.
    public static int EdgeMinutes(World world, TileId from, TileId to) =>
        (Map.Of(world).EdgeBetween(from, to)?.TraversalMinutes() ?? 0) + Tracks.TrailMinutes(world, from, to);

    /// What today's snow, water and mud add to a segment of segmentMinutes: segmentMinutes x
    /// (TraversalFactor - 1). Added after the edge bonus, from the baseline segment: a worn path is
    /// bare earth under the snow, not a cleared lane through it.
    public static double SurfaceExcessMinutes(World world, TileId tile, int segmentMinutes) =>
        segmentMinutes * (Ground.TraversalFactor(world, tile) - 1);

    /// Zero beside a river, whose ice already owns the hazard.
    public static double SurfaceHazardDelta(World world, TileId tile) => Ground.HazardDelta(world, tile);

    // ---- hazard ----

    public static double EffectiveHazardLevel(World world, TileId tile) => HazardLevel(Map.Of(world), tile, SurfaceHazardDelta(world, tile));

    /// The ground's shape plus what its surface adds, at most 1: one rule whether the surface was
    /// observed (EffectiveHazardLevel) or only looked at (Trip.Risk).
    internal static double HazardLevel(GeneratedMap m, TileId tile, double surfaceDelta) => Math.Min(1.0, Map.TravelHazardLevel(m, tile) + surfaceDelta);

    public static bool PaceMatters(double quickRiskPct) => quickRiskPct > FootingRules.PaceMattersAbovePct;

    /// The ground alone, for a sound body under a calm sky: for agents that choose a pace by the map.
    public static bool IsHazardous(World world, TileId tile) => PaceMatters(FallPct(EffectiveHazardLevel(world, tile)));

    /// The one curve every injury risk is read from, so easy ground is nearly safe and there is no step in it.
    internal static double FallPct(double footing) => Math.Pow(Math.Clamp(footing, 0, 1), FootingRules.InjuryExponent) / FootingRules.InjuryDivisor;

    public static double InjuryRiskPct(World world, TileId tile, BodyState body, WeatherSample sky, Pace pace = Pace.Quick) =>
        InjuryOdds(world, tile, body, sky, pace).TotalPct;

    /// The risk of the one roll a crossing onto this tile makes, as its terms for the trip preview.
    public static Odds InjuryOdds(World world, TileId tile, BodyState body, WeatherSample sky, Pace pace = Pace.Quick) =>
        InjuryOdds(EffectiveHazardLevel(world, tile), body, sky, pace);

    /// Weather and bad legs worsen footing in proportion, so easy ground stays easy in a storm.
    internal static Odds InjuryOdds(double hazardLevel, BodyState body, WeatherSample sky, Pace pace)
    {
        // Labelled by what earned it: a storm, light snow, or (whatever the sky is called) heavy precipitation.
        var (weatherWhy, weather) = sky.Condition is WeatherCondition.Blizzard or WeatherCondition.Stormy ? (Names.Title(sky.Condition), 0.25)
            : sky.Condition == WeatherCondition.LightSnow ? (Names.Title(sky.Condition), 0.15)
            : sky.PrecipitationPct > 0.3 ? (sky.Precipitation == Precipitation.None ? "Precipitation" : $"Heavy {Names.Of(sky.Precipitation)}", 0.15)
            : (Names.Title(sky.Condition), 0.0);
        double impaired = (1 - Body.GetCapacities(body).Moving) * 0.3;
        double ground = Math.Clamp(hazardLevel, 0, 1), weathered = Math.Min(1, ground * (1 + weather)), lame = Math.Min(1, ground * (1 + weather + impaired));
        return new Odds("Ground", FallPct(ground), Odds.Moving(
            OddsTerm.Add(weatherWhy, FallPct(weathered) - FallPct(ground)),
            OddsTerm.Add("Bad legs", FallPct(lame) - FallPct(weathered)),
            OddsTerm.Scale("Careful", pace == Pace.Careful ? FootingRules.CarefulRiskFraction : 1)));
    }

    // Accident mechanics, pool by pool (Content/accidents.json).
    static readonly IReadOnlyList<Accident> TripStumble = Accidents("TripStumble"), SharpHazards = Accidents("SharpHazards"),
        IceSlip = Accidents("IceSlip"), RockyTerrain = Accidents("RockyTerrain"), FallImpact = Accidents("FallImpact"), Sprains = Accidents("Sprains");

    static IReadOnlyList<Accident> Accidents(string pool) =>
        Content.Tables.Accidents.TryGetValue(pool, out var a) ? a : throw new KeyNotFoundException($"No accident pool '{pool}'");

    /// Ice slips beside a river, otherwise worse pools as the ground gets worse. Every question is
    /// asked of the tile walked onto.
    static (IReadOnlyList<Accident> Pool, double Weight)[] AccidentPools(GeneratedMap m, TileId tile)
    {
        double severity = Map.TerrainHazardLevel(m, tile);
        if (m.BesideRiver(tile))
            return severity >= 0.5 ? [(IceSlip, 2.0), (FallImpact, 1.0)] : [(IceSlip, 2.0), (TripStumble, 0.8)];
        if (severity >= 0.7) return [(FallImpact, 1.5), (Sprains, 1.5), (RockyTerrain, 1.0)];
        if (severity >= 0.5) return [(RockyTerrain, 1.5), (Sprains, 1.0), (SharpHazards, 0.8)];
        return [(TripStumble, 1.5), (SharpHazards, 0.8), (RockyTerrain, 0.6)];
    }

    static readonly Cause Fall = new(null, "a fall");

    static string? Injure(World world, Agent agent, TileId tile, List<WorldEvent> events)
    {
        var weighted = AccidentPools(Map.Of(world), tile).SelectMany(p => p.Pool.Select(a => (a, p.Weight))).ToList();
        double roll = world.Rng("travel", agent.Id).NextDouble() * weighted.Sum(x => x.Weight);
        var accident = weighted[^1].a;
        foreach (var (a, weight) in weighted)
            if ((roll -= weight) < 0) { accident = a; break; }

        var body = agent.Body;
        var kindsBefore = body.Effects.Select(e => e.Kind).ToList();
        var hit = Body.Damage(body, new DamageInfo(accident.Amount, accident.Type, accident.Target, By: Fall), world.Rng("body", agent.Id));
        if (accident.Effect is EffectKind kind) Effects.Add(body, kind, accident.Severity);
        events.Add(new Injured(world.Minute, agent.Id, tile, hit.HitRegion, accident.Type, accident.Amount));
        foreach (var k in body.Effects.Select(e => e.Kind).Except(kindsBefore)) events.Add(new EffectStarted(world.Minute, agent.Id, agent.Tile, k));

        if (Body.IsAlive(body)) return null;
        Body.MarkDead(body, world.Minute, Fall);
        events.Add(new Died(world.Minute, agent.Id, agent.Tile));
        return "died";
    }
}
