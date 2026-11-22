namespace Survival;

/// Careful: half again the time where care matters, for a fraction of the risk (Footing).
public enum Pace { Quick, Careful }

public sealed record Footing(double InjuryExponent, double InjuryDivisor, double CarefulRiskFraction, double PaceMattersAbovePct);

/// The agent stays on its tile until ElapsedMinutes reaches TotalMinutes. The risk is fixed when the crossing starts.
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

/// A record, not a struct, so the reader requires Target, Type and Amount (a struct row would default them).
public sealed record Accident(BodyTarget Target, DamageType Type, double Amount, EffectKind? Effect = null, double Severity = 0);

public static class Traversal
{
    public const double CarefulFactor = 1.5;
    static readonly Footing FootingRules = Content.Tables.Footing;
    public const int MinimumCrossingMinutes = 5;

    public static Refusal? Refusal(World world, Travel t)
    {
        var agent = world.Agent(t.Who);
        if (CannotWalk(agent) is string lame) return new(lame, new MissingState(Lack.MovingPct, LameMovingPct, Moving(agent)));
        return Blocked(world, agent, agent.Tile, Map.Step(agent.Tile, t.Dir)) is string blocked ? new(blocked) : null;
    }

    public static string? Blocked(World world, Agent agent, TileId from, TileId to) =>
        FlightMinutesPerTile(agent) == null ? Map.Blocked(Map.Of(world), from, to) : Map.BlockedInFlight(Map.Of(world), from, to);

    public static int? FlightMinutesPerTile(Agent agent) => SpeciesTable.Of(agent.Species).FlightMinutesPerTile;

    public static BodyState PaceBody(Agent agent) =>
        agent.Members.Count == 1 ? agent.Members[0] : agent.Members.Where(Body.IsAlive).MinBy(b => Body.Speed(b))
            ?? throw new InvalidOperationException($"{agent.Id} has no living member to walk");

    /// A minute on the move, all members: a person walking, an animal at the field metabolism it always burns.
    public static double KcalPerMinute(World world, Agent agent) => agent.Mind is HerdMind
        ? agent.Members.Where(Body.IsAlive).Sum(Fauna.BurnKcalPerMinute)
        : Body.BurnKcalPerMinute(agent.Body, Sim.ActivityFactor(world, agent.Tile, new Travel(agent.Id, Direction.North)));

    /// At or under this Moving, a person can barely move.
    public const double LameMovingPct = 0.1;

    static double Moving(Agent agent) => agent.Members.Where(Body.IsAlive).Min(m => Body.GetCapacities(m).Moving);

    /// Checked at the start and every minute (Advance), so a crossing stops at the same minute however its minutes are cut into steps.
    static string? CannotWalk(Agent agent) => Moving(agent) <= LameMovingPct ? "you can barely move" : null;

    /// A crossing in another direction, at another pace, or from a tile the agent is no longer on, is abandoned.
    public static Crossing Begin(World world, Travel t)
    {
        var agent = world.Agent(t.Who);
        if (agent.Crossing is { } c && c.Dir == t.Dir && c.Pace == t.Pace && c.From == agent.Tile) return c;
        var from = agent.Tile;
        var to = Map.Step(from, t.Dir);
        var sky = Weather.At(world, world.Minute);
        bool stumbles = agent.Mind is PersonMind && FlightMinutesPerTile(agent) == null;
        return agent.Crossing = new Crossing
        {
            From = from,
            Dir = t.Dir,
            Pace = t.Pace,
            TotalMinutes = FlightMinutesPerTile(agent)
                ?? CrossingMinutes(world, PaceBody(agent), from, to, t.Pace, sky, Items.EncumbrancePct(agent), GroundReadOf(agent, GroundRead.Observe)),
            RiskPct = stumbles ? InjuryRiskPct(world, to, agent.Body, sky, t.Pace) : 0,
        };
    }

    /// On the last minute the agent arrives and rolls for injury on the tile walked onto. A minute that leaves it
    /// too lame to go on is walked and stops it short of the far side, crossing kept.
    public static string? Advance(World world, Agent agent, List<WorldEvent> events)
    {
        var c = agent.Crossing ?? throw new InvalidOperationException($"{agent.Id} is not crossing");
        if (agent.Mind is PersonMind && agent.Members.Count == 1)
        {
            // A crossing walks out of one tile and into the next: half its searching is of each.
            double hours = TravelSearchPct * Body.Perception(agent.Body, Fire.AbilityContextFor(world, agent)) / 60.0;
            Pools.Search(world, agent, c.From, hours / 2, events);
            Pools.Search(world, agent, Map.Step(c.From, c.Dir), hours / 2, events);
        }
        if (++c.ElapsedMinutes < c.TotalMinutes) return CannotWalk(agent);

        if (c.From != agent.Tile) throw new InvalidOperationException($"{agent.Id} is crossing from {c.From} but stands on {agent.Tile}");
        var from = c.From;
        var to = Map.Step(from, c.Dir);
        agent.Tile = to;
        agent.Crossing = null;
        events.Add(new Moved(world.Minute, agent.Id, from, to));
        // A herd marks the ground with all its feet; the carrion flock is mostly on the wing, the fox in it too light.
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

    public const string Arrived = "arrived", CrossedThePassReason = "crossed the pass";

    public const double TravelSearchPct = 0.25;

    /// encumbrancePct is carried weight over capacity (Items.EncumbrancePct): past half, a load slows you.
    public static int CrossingMinutes(World world, BodyState body, TileId from, TileId to, Pace pace, WeatherSample sky, double encumbrancePct = 0,
        GroundRead ground = GroundRead.Observe) =>
        Trip.Terms(world, body, from, to, pace, sky, encumbrancePct, ground).TotalMinutes;

    /// Animals read the weather-only estimate (decision 2026-11-22-animals-read-the-ground-estimate).
    public static GroundRead GroundReadOf(Agent agent, GroundRead person) => agent.Mind is PersonMind ? person : GroundRead.Estimate;

    public static int SegmentMinutes(GeneratedMap m, TileId tile, double speed, WeatherSample sky)
    {
        double multiplier = Map.TravelHazardLevel(m, tile, sky.Minute);
        if (sky.WindLevel > 0.5) multiplier *= 1 + Exposure.OpenWindLevel(m, tile, sky) * 0.3;
        if (sky.PrecipitationPct > 0.5) multiplier *= 1 + sky.PrecipitationPct * 0.2;
        return (int)Math.Ceiling(Map.BaseTraversalMinutes(m, tile) * (1 + multiplier) / Math.Max(0.1, speed));
    }

    public static int EdgeMinutes(World world, TileId from, TileId to) =>
        (Map.Of(world).EdgeBetween(from, to)?.TraversalMinutes() ?? 0) + Tracks.TrailMinutes(world, from, to);

    /// Added after the edge bonus, from the baseline segment: a worn path is bare earth under the snow, not a cleared lane.
    public static double SurfaceExcessMinutes(World world, TileId tile, int segmentMinutes) =>
        segmentMinutes * (Ground.TraversalFactor(world, tile) - 1);

    /// Zero beside a frozen river, whose ice already owns the hazard.
    public static double SurfaceHazardDelta(World world, TileId tile) => Ground.HazardDelta(world, tile);

    public static double EffectiveHazardLevel(World world, TileId tile) => HazardLevel(Map.Of(world), tile, world.Minute, SurfaceHazardDelta(world, tile));

    /// One rule whether the surface was observed (EffectiveHazardLevel) or only looked at (Trip.Risk).
    internal static double HazardLevel(GeneratedMap m, TileId tile, long minute, double surfaceDelta) => Math.Min(1.0, Map.TravelHazardLevel(m, tile, minute) + surfaceDelta);

    public static bool PaceMatters(double quickRiskPct) => quickRiskPct > FootingRules.PaceMattersAbovePct;

    public static bool IsHazardous(World world, TileId tile) => PaceMatters(FallPct(EffectiveHazardLevel(world, tile)));

    /// The one curve every injury risk is read from, so easy ground is nearly safe and there is no step in it.
    internal static double FallPct(double footing) => Math.Pow(Math.Clamp(footing, 0, 1), FootingRules.InjuryExponent) / FootingRules.InjuryDivisor;

    public static double InjuryRiskPct(World world, TileId tile, BodyState body, WeatherSample sky, Pace pace = Pace.Quick) =>
        InjuryOdds(world, tile, body, sky, pace).TotalPct;

    public static Odds InjuryOdds(World world, TileId tile, BodyState body, WeatherSample sky, Pace pace = Pace.Quick) =>
        InjuryOdds(EffectiveHazardLevel(world, tile), body, sky, pace);

    /// Weather and bad legs worsen footing in proportion, so easy ground stays easy in a storm.
    internal static Odds InjuryOdds(double hazardLevel, BodyState body, WeatherSample sky, Pace pace)
    {
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

    static readonly IReadOnlyList<Accident> TripStumble = Accidents("TripStumble"), SharpHazards = Accidents("SharpHazards"),
        IceSlip = Accidents("IceSlip"), RockyTerrain = Accidents("RockyTerrain"), FallImpact = Accidents("FallImpact"), Sprains = Accidents("Sprains");

    static IReadOnlyList<Accident> Accidents(string pool) =>
        Content.Tables.Accidents.TryGetValue(pool, out var a) ? a : throw new KeyNotFoundException($"No accident pool '{pool}'");

    static (IReadOnlyList<Accident> Pool, double Weight)[] AccidentPools(GeneratedMap m, TileId tile, long minute)
    {
        double severity = Map.TerrainHazardLevel(m, tile);
        if (m.BesideRiver(tile) && Map.IsFrozen(m, tile, minute))
            return severity >= 0.5 ? [(IceSlip, 2.0), (FallImpact, 1.0)] : [(IceSlip, 2.0), (TripStumble, 0.8)];
        if (severity >= 0.7) return [(FallImpact, 1.5), (Sprains, 1.5), (RockyTerrain, 1.0)];
        if (severity >= 0.5) return [(RockyTerrain, 1.5), (Sprains, 1.0), (SharpHazards, 0.8)];
        return [(TripStumble, 1.5), (SharpHazards, 0.8), (RockyTerrain, 0.6)];
    }

    static readonly Cause Fall = new(null, "a fall");

    static string? Injure(World world, Agent agent, TileId tile, List<WorldEvent> events)
    {
        var weighted = AccidentPools(Map.Of(world), tile, world.Minute).SelectMany(p => p.Pool.Select(a => (a, p.Weight))).ToList();
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
