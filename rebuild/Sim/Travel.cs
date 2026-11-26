namespace Survival;

/// Careful: half again the time where care matters, for a fraction of the risk (Footing).
public enum Pace { Quick, Careful }

public sealed record Footing(double InjuryExponent, double InjuryDivisor, double CarefulRiskFraction, double PaceMattersAbovePct);

/// On its tile until Covered reaches Whole, a minute at that minute's price (Traversal.Advance). Risk is fixed at the start.
public sealed class Crossing
{
    public const long Whole = 1L << 40;

    /// Where the crossing started. Only resumed from this tile: an agent moved by anything else starts afresh.
    public required TileId From { get; init; }
    public required Direction Dir { get; init; }
    public required Pace Pace { get; init; }
    public long Covered { get; set; }
    public int ElapsedMinutes { get; set; }
    public double RiskPct { get; init; }
}

public readonly record struct Departure(TileId From, Direction Dir, long Minute);

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
        if (CannotWalk(agent) is { } lame) return lame;
        return Blocked(world, agent, agent.Tile, Map.Step(agent.Tile, t.Dir)) is string blocked ? new(blocked) : null;
    }

    /// A walker sets out only from ground it could stand on: a route searched into a site asks from every neighbour.
    public static string? Blocked(World world, Agent agent, TileId from, TileId to) =>
        FlightMinutesPerTile(agent) != null ? Map.BlockedInFlight(Map.Of(world), from, to)
            : Map.Blocked(Map.Of(world), from, to) ?? (Map.Of(world).TerrainAt(from).IsPassable() ? null : NowhereToSetOutFrom);

    public const string NowhereToSetOutFrom = "no ground to set out from";

    public static int? FlightMinutesPerTile(Agent agent) => SpeciesTable.Of(agent.Species).FlightMinutesPerTile;

    public static BodyState PaceBody(Agent agent) =>
        agent.Members.Count == 1 ? agent.Members[0] : agent.Members.Where(Body.IsAlive).MinBy(b => Body.Speed(b))
            ?? throw new InvalidOperationException($"{agent.Id} has no living member to walk");

    /// A minute on the move, all members: a person walking, an animal at the field metabolism it always burns.
    public static double KcalPerMinute(World world, Agent agent) => agent.Mind is HerdMind
        ? agent.Members.Where(Body.IsAlive).Sum(Fauna.BurnKcalPerMinute)
        : Body.BurnKcalPerMinute(agent.Body, Sim.ActivityFactor(world, agent.Tile, new Travel(agent.Id, Direction.North)));

    /// Lame (GLOSSARY), under the load: asked before any crossing time, and every minute (Advance), so a crossing stops at the same minute however stepped.
    public static Refusal? CannotWalk(Agent agent) =>
        Crippled.Refusal(PaceBody(agent), Faculty.Speed, new AbilityContext(EncumbrancePct: Items.EncumbrancePct(agent)));

    public const string CannotWalkPrefix = "you cannot walk: ";

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
            RiskPct = stumbles ? InjuryRiskPct(world, to, agent.Body, sky, t.Pace, from) : 0,
        };
    }

    /// A minute's share, priced for this minute's body, load and sky (Trip.MinutesNow); covered, the agent arrives and
    /// rolls for injury on the tile walked onto. A minute it cannot walk covers nothing and stops it, crossing kept.
    public static string? Advance(World world, Agent agent, List<WorldEvent> events)
    {
        var c = agent.Crossing ?? throw new InvalidOperationException($"{agent.Id} is not crossing");
        if (CannotWalk(agent) is { } lame) return lame.Reason;
        if (agent.Mind is PersonMind && agent.Members.Count == 1)
        {
            // A crossing walks out of one tile and into the next: half its searching is of each.
            double hours = TravelSearchPct * Body.Perception(agent.Body, Fire.AbilityContextFor(world, agent)) / 60.0;
            Pools.Search(world, agent, c.From, hours / 2, events);
            Pools.Search(world, agent, Map.Step(c.From, c.Dir), hours / 2, events);
        }
        c.ElapsedMinutes++;
        c.Covered += ShareNow(world, agent, c);
        if (c.Covered < Crossing.Whole) return null;

        if (c.From != agent.Tile) throw new InvalidOperationException($"{agent.Id} is crossing from {c.From} but stands on {agent.Tile}");
        var from = c.From;
        var to = Map.Step(from, c.Dir);
        agent.Tile = to;
        agent.Crossing = null;
        if (agent.Mind is HerdMind) agent.LastLeft = new Departure(from, c.Dir, world.Minute);
        events.Add(new Moved(world.Minute, agent.Id, from, to));
        Tracks.RecordMove(world, agent, from, to);

        if (c.RiskPct > 0 && world.Rng("travel", agent.Id).NextDouble() < c.RiskPct && Injure(world, agent, to, events) is string stop) return stop;
        if (to == Map.Of(world).Exit && agent.Mind is PersonMind)
        {
            events.Add(new CrossedThePass(world.Minute, agent.Id, to));
            return CrossedThePassReason;
        }
        return Arrived;
    }

    /// The minutes left at this minute's price; null for a walker who cannot walk now.
    public static int? MinutesLeft(World world, Agent agent)
    {
        var c = agent.Crossing ?? throw new InvalidOperationException($"{agent.Id} is not crossing");
        if (CannotWalk(agent) != null) return null;
        long perMinute = ShareNow(world, agent, c);
        return (int)Math.Max(0, (Crossing.Whole - c.Covered + perMinute - 1) / perMinute);
    }

    public static double CoveredPct(Crossing c) => (double)c.Covered / Crossing.Whole;

    static long ShareNow(World world, Agent agent, Crossing c)
    {
        int minutes = Trip.MinutesNow(world, agent, c.From, Map.Step(c.From, c.Dir), c.Pace);
        return (Crossing.Whole + minutes - 1) / minutes;
    }

    public const string Arrived = "arrived", CrossedThePassReason = "crossed the pass";

    public const double TravelSearchPct = 0.25;

    /// encumbrancePct is carried weight over capacity (Items.EncumbrancePct): past half, a load slows you.
    public static int CrossingMinutes(World world, BodyState body, TileId from, TileId to, Pace pace, WeatherSample sky, double encumbrancePct = 0,
        Species species = Species.Human) =>
        Trip.Terms(world, body, from, to, pace, sky, encumbrancePct, species).TotalMinutes;

    public static int SegmentMinutes(GeneratedMap m, TileId tile, double speed, WeatherSample sky)
    {
        if (!(speed > 0)) throw new ArgumentOutOfRangeException(nameof(speed), speed, "a body at speed zero cannot walk: ask CannotWalk first");
        double multiplier = Map.TravelHazardLevel(m, tile, sky.Minute);
        if (sky.WindLevel > 0.5) multiplier *= 1 + Exposure.OpenWindLevel(m, tile, sky) * 0.3;
        if (sky.PrecipitationPct > 0.5) multiplier *= 1 + sky.PrecipitationPct * 0.2;
        return checked((int)Math.Ceiling(Map.BaseTraversalMinutes(m, tile) * (1 + multiplier) / speed));
    }

    public static int EdgeMinutes(World world, TileId from, TileId to) => Map.Of(world).EdgeBetween(from, to)?.TraversalMinutes() ?? 0;

    /// Added after the edge's minutes, from the baseline segment.
    public static double SurfaceExcessMinutes(World world, TileId tile, int segmentMinutes) =>
        segmentMinutes * (Ground.Underfoot(world, tile).TraversalFactor - 1);

    /// Zero beside a frozen river, whose ice already owns the hazard. from: for the edge's half of tile.
    public static double SurfaceHazardDelta(World world, TileId tile, TileId? from = null) =>
        Map.Of(world) is var m && m.BesideRiver(tile) && Map.IsFrozen(m, tile, world.Minute) ? 0
            : Ground.Underfoot(world, tile, from).HazardDelta;

    public static double EffectiveHazardLevel(World world, TileId tile, TileId? from = null) =>
        HazardLevel(Map.Of(world), tile, world.Minute, SurfaceHazardDelta(world, tile, from));

    internal static double HazardLevel(GeneratedMap m, TileId tile, long minute, double surfaceDelta) => Math.Min(1.0, Map.TravelHazardLevel(m, tile, minute) + surfaceDelta);

    public static bool PaceMatters(double quickRiskPct) => quickRiskPct > FootingRules.PaceMattersAbovePct;

    /// Whether care slows a walk and is offered: the quick risk to sound legs, so care is never free and a crossing at
    /// the floor stays within ten times a fresh one at the same pace. Bad legs raise the risk itself (InjuryOdds).
    public static bool CareMatters(World world, TileId tile, TileId? from, WeatherSample sky) =>
        PaceMatters(InjuryOdds(EffectiveHazardLevel(world, tile, from), SoundLegsPct, sky, Pace.Quick).TotalPct);

    const double SoundLegsPct = 1;

    public static bool IsHazardous(World world, TileId tile) => PaceMatters(FallPct(EffectiveHazardLevel(world, tile)));

    /// The one curve every injury risk is read from, so easy ground is nearly safe and there is no step in it.
    internal static double FallPct(double footing) => Math.Pow(Math.Clamp(footing, 0, 1), FootingRules.InjuryExponent) / FootingRules.InjuryDivisor;

    public static double InjuryRiskPct(World world, TileId tile, BodyState body, WeatherSample sky, Pace pace = Pace.Quick, TileId? from = null) =>
        InjuryOdds(world, tile, body, sky, pace, from).TotalPct;

    public static Odds InjuryOdds(World world, TileId tile, BodyState body, WeatherSample sky, Pace pace = Pace.Quick, TileId? from = null) =>
        InjuryOdds(EffectiveHazardLevel(world, tile, from), body, sky, pace);

    /// Weather and bad legs (speed short of fresh) worsen footing in proportion, so easy ground stays easy in a storm.
    internal static Odds InjuryOdds(double hazardLevel, BodyState body, WeatherSample sky, Pace pace) =>
        InjuryOdds(hazardLevel, SpeciesTable.SpeedPct(Species.Human, body), sky, pace);

    static Odds InjuryOdds(double hazardLevel, double speedPct, WeatherSample sky, Pace pace)
    {
        var (weatherWhy, weather) = sky.Condition is WeatherCondition.Blizzard or WeatherCondition.Stormy ? (Names.Title(sky.Condition), 0.25)
            : sky.Condition == WeatherCondition.LightSnow ? (Names.Title(sky.Condition), 0.15)
            : sky.PrecipitationPct > 0.3 ? (sky.Precipitation == Precipitation.None ? "Precipitation" : $"Heavy {Names.Of(sky.Precipitation)}", 0.15)
            : (Names.Title(sky.Condition), 0.0);
        double impaired = (1 - speedPct) * 0.3;
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
