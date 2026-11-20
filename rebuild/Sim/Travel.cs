namespace Survival;

/// How to take hazardous ground: quick at full injury risk, or careful at half again the time
/// and no risk. Only hazardous segments care.
public enum Pace { Quick, Careful }

/// A crossing under way. The agent stays on its tile until ElapsedMinutes reaches TotalMinutes.
/// Risks are fixed when the crossing starts, as the old TravelRunner fixed them before the walk.
public sealed class Crossing
{
    public required Direction Dir { get; init; }
    public required Pace Pace { get; init; }
    public required int TotalMinutes { get; init; }
    public int ElapsedMinutes { get; set; }
    public double OriginRiskPct { get; init; }
    public double DestRiskPct { get; init; }
}

/// Walking one tile north, east, south or west. Port of the old TravelProcessor and TravelRunner.
public static class Traversal
{
    /// Effective hazard at or above which a segment is hazardous: pace matters and quick risks injury.
    public const double HazardousLevel = 0.3;
    public const double CarefulFactor = 1.5;
    public const double MaxInjuryRiskPct = 0.5;
    /// Shortest a crossing can take, however many trail bonuses stack on it.
    public const int MinimumCrossingMinutes = 5;

    /// Why this move can't be made, or null. Checked before any time passes.
    public static string? Refusal(World world, Travel t)
    {
        var agent = world.Agent(t.Who);
        if (Body.GetCapacities(agent.Body).Moving <= 0.1) return "you can barely move";
        return Map.Blocked(Map.Of(world), agent.Tile, Map.Step(agent.Tile, t.Dir));
    }

    /// The crossing this activity continues, or a fresh one. A crossing in another direction, or
    /// at another pace, is abandoned: the agent never left its tile.
    public static Crossing Begin(World world, Travel t)
    {
        var agent = world.Agent(t.Who);
        if (agent.Crossing is { } c && c.Dir == t.Dir && c.Pace == t.Pace) return c;
        var from = agent.Tile;
        var to = Map.Step(from, t.Dir);
        var sky = Weather.At(world, world.Minute);
        bool quick = t.Pace == Pace.Quick;
        return agent.Crossing = new Crossing
        {
            Dir = t.Dir,
            Pace = t.Pace,
            TotalMinutes = CrossingMinutes(world, agent.Body, from, to, t.Pace, sky),
            OriginRiskPct = quick ? InjuryRiskPct(world, from, agent.Body, sky) : 0,
            DestRiskPct = quick ? InjuryRiskPct(world, to, agent.Body, sky) : 0,
        };
    }

    /// One minute of walking lived. On the last one the agent arrives, and quick hazardous
    /// segments roll for injury, origin first. Returns why to stop, if anything.
    public static string? Advance(World world, Agent agent, List<WorldEvent> events)
    {
        var c = agent.Crossing ?? throw new InvalidOperationException($"{agent.Id} is not crossing");
        if (++c.ElapsedMinutes < c.TotalMinutes) return null;

        var from = agent.Tile;
        var to = Map.Step(from, c.Dir);
        agent.Tile = to;
        agent.Crossing = null;
        events.Add(new Moved(world.Minute, agent.Id, from, to));
        // ponytail: Ground/Tracks hook here - this is the old GameMap.RecordMove seam (prints, trail wear).

        foreach (var (tile, riskPct) in new[] { (from, c.OriginRiskPct), (to, c.DestRiskPct) })
        {
            if (riskPct <= 0 || world.Rng("travel").NextDouble() >= riskPct) continue;
            if (Injure(world, agent, tile, events) is string stop) return stop;
        }
        if (to == Map.Of(world).Exit)
        {
            events.Add(new CrossedThePass(world.Minute, agent.Id));
            return "crossed the pass";
        }
        return "arrived";
    }

    // ---- cost ----

    /// Old TravelProcessor.GetTraversalMinutes with the pace applied as TravelRunner applied it.
    public static int CrossingMinutes(World world, BodyState body, TileId from, TileId to, Pace pace, WeatherSample sky)
    {
        var m = Map.Of(world);
        double speed = Body.Speed(body); // ponytail: no encumbrance until inventory exists
        int exit = SegmentMinutes(m, from, speed, sky);
        int entry = SegmentMinutes(m, to, speed, sky);
        double surface = SurfaceExcessMinutes(world, from, exit) + SurfaceExcessMinutes(world, to, entry);
        if (pace == Pace.Careful)
        {
            if (IsHazardous(world, from)) exit = (int)Math.Ceiling(exit * CarefulFactor);
            if (IsHazardous(world, to)) entry = (int)Math.Ceiling(entry * CarefulFactor);
        }
        return Math.Max(MinimumCrossingMinutes, (int)Math.Ceiling(exit + entry + EdgeMinutes(world, from, to) + surface));
    }

    /// Leaving or entering one tile: its base minutes, slowed by the shape of the ground, by a
    /// strong wind or heavy precipitation, and by how slowly this body moves.
    public static int SegmentMinutes(GeneratedMap m, TileId tile, double speed, WeatherSample sky)
    {
        double multiplier = Map.TravelHazardLevel(m, tile);
        if (sky.WindLevel > 0.5) multiplier *= 1 + sky.WindLevel * 0.3 * Map.WindFactor(m, tile);
        if (sky.PrecipitationPct > 0.5) multiplier *= 1 + sky.PrecipitationPct * 0.2;
        return (int)Math.Ceiling(Map.BaseTraversalMinutes(m, tile) * (1 + multiplier) / Math.Max(0.1, speed));
    }

    /// Minutes the edge between two tiles adds. Generated edges only ever block, so this is 0 until
    /// trail wear and authored edges arrive. Tracks (desire paths) add their tier bonus here.
    public static int EdgeMinutes(World world, TileId from, TileId to) =>
        Map.Of(world).EdgeBetween(from, to)?.TraversalMinutes() ?? 0;

    /// Hook for Ground (phase 2c): what today's snow and mud add to a segment of segmentMinutes,
    /// old segmentMinutes * (Surface.TraversalFactor - 1). Added after the edge bonus, from the
    /// baseline segment: a worn path is bare earth under the snow, not a cleared lane through it.
    public static double SurfaceExcessMinutes(World world, TileId tile, int segmentMinutes) => 0;

    /// Hook for Ground (phase 2c): old Location.GetSurfaceHazardDelta. Zero beside a river, whose
    /// ice already owns the hazard.
    public static double SurfaceHazardDelta(World world, TileId tile) => 0;

    // ---- hazard ----

    public static double EffectiveHazardLevel(World world, TileId tile) =>
        Math.Min(1.0, Map.TravelHazardLevel(Map.Of(world), tile) + SurfaceHazardDelta(world, tile));

    public static bool IsHazardous(World world, TileId tile) => EffectiveHazardLevel(world, tile) >= HazardousLevel;

    /// Old TravelProcessor.GetInjuryRisk: chance a quick crossing of this tile injures you.
    public static double InjuryRiskPct(World world, TileId tile, BodyState body, WeatherSample sky)
    {
        double baseRisk = EffectiveHazardLevel(world, tile);
        if (baseRisk < HazardousLevel) return 0;
        double weather = sky.Condition is WeatherCondition.Blizzard or WeatherCondition.Stormy ? 0.25
            : sky.PrecipitationPct > 0.3 || sky.Condition == WeatherCondition.LightSnow ? 0.15 : 0;
        double impaired = (1 - Body.GetCapacities(body).Moving) * 0.3;
        return Math.Min(MaxInjuryRiskPct, baseRisk + weather + impaired);
    }

    /// One accident's mechanics, from the old AccidentVariants (text is phase 4 content).
    readonly record struct Accident(BodyTarget Target, DamageType Type, double Amount, EffectKind? Effect = null, double Severity = 0);

    static readonly Accident[] TripStumble =
    [
        new(BodyTarget.AnyLeg, DamageType.Blunt, 0.08), new(BodyTarget.AnyLeg, DamageType.Blunt, 0.08),
        new(BodyTarget.AnyLeg, DamageType.Blunt, 0.08), new(BodyTarget.AnyArm, DamageType.Blunt, 0.08),
        new(BodyTarget.AnyArm, DamageType.Blunt, 0.05), new(BodyTarget.Abdomen, DamageType.Blunt, 0.10),
        new(BodyTarget.Chest, DamageType.Blunt, 0.10),
    ];
    static readonly Accident[] SharpHazards =
    [
        new(BodyTarget.AnyLeg, DamageType.Sharp, 0.08), new(BodyTarget.AnyArm, DamageType.Sharp, 0.05),
        new(BodyTarget.AnyArm, DamageType.Sharp, 0.05), new(BodyTarget.Chest, DamageType.Sharp, 0.08),
        new(BodyTarget.AnyLeg, DamageType.Sharp, 0.10),
    ];
    static readonly Accident[] IceSlip =
    [
        new(BodyTarget.AnyLeg, DamageType.Blunt, 0.10), new(BodyTarget.AnyLeg, DamageType.Blunt, 0.08),
        new(BodyTarget.Abdomen, DamageType.Blunt, 0.12), new(BodyTarget.AnyArm, DamageType.Blunt, 0.10),
        new(BodyTarget.Head, DamageType.Blunt, 0.10, EffectKind.Dazed, 0.2),
    ];
    static readonly Accident[] RockyTerrain =
    [
        new(BodyTarget.AnyLeg, DamageType.Blunt, 0.10), new(BodyTarget.AnyArm, DamageType.Sharp, 0.08),
        new(BodyTarget.AnyLeg, DamageType.Blunt, 0.08), new(BodyTarget.Chest, DamageType.Sharp, 0.08),
        new(BodyTarget.Head, DamageType.Blunt, 0.12, EffectKind.Dazed, 0.3),
    ];
    static readonly Accident[] FallImpact =
    [
        new(BodyTarget.AnyLeg, DamageType.Blunt, 0.12), new(BodyTarget.Abdomen, DamageType.Blunt, 0.15),
        new(BodyTarget.AnyArm, DamageType.Blunt, 0.12), new(BodyTarget.Chest, DamageType.Blunt, 0.15),
        new(BodyTarget.Head, DamageType.Blunt, 0.12, EffectKind.Dazed, 0.4),
    ];
    // ponytail: the old sprains also added SprainedAnkle / Clumsy effects, which the rebuild has no kinds for yet.
    static readonly Accident[] Sprains =
    [
        new(BodyTarget.AnyLeg, DamageType.Blunt, 0.08), new(BodyTarget.AnyLeg, DamageType.Blunt, 0.10),
        new(BodyTarget.AnyArm, DamageType.Blunt, 0.08),
    ];

    /// Old VariantSelector.SelectTravelInjuryVariant: ice slips beside a river, otherwise worse
    /// pools as the ground gets worse.
    static (Accident[] Pool, double Weight)[] AccidentPools(GeneratedMap m, TileId tile)
    {
        double severity = Map.TerrainHazardLevel(m, tile);
        if (m.BesideRiver(tile))
            return severity >= 0.5 ? [(IceSlip, 2.0), (FallImpact, 1.0)] : [(IceSlip, 2.0), (TripStumble, 0.8)];
        if (severity >= 0.7) return [(FallImpact, 1.5), (Sprains, 1.5), (RockyTerrain, 1.0)];
        if (severity >= 0.5) return [(RockyTerrain, 1.5), (Sprains, 1.0), (SharpHazards, 0.8)];
        return [(TripStumble, 1.5), (SharpHazards, 0.8), (RockyTerrain, 0.6)];
    }

    static string? Injure(World world, Agent agent, TileId tile, List<WorldEvent> events)
    {
        var weighted = AccidentPools(Map.Of(world), tile).SelectMany(p => p.Pool.Select(a => (a, p.Weight))).ToList();
        double roll = world.Rng("travel").NextDouble() * weighted.Sum(x => x.Weight);
        var accident = weighted[^1].a;
        foreach (var (a, weight) in weighted)
            if ((roll -= weight) < 0) { accident = a; break; }

        var body = agent.Body;
        var kindsBefore = body.Effects.Select(e => e.Kind).ToList();
        var hit = Body.Damage(body, new DamageInfo(accident.Amount, accident.Type, accident.Target), world.Rng("body"));
        if (accident.Effect is EffectKind kind) Effects.Add(body, kind, accident.Severity);
        events.Add(new Injured(world.Minute, agent.Id, tile, hit.HitRegion, accident.Type, accident.Amount));
        foreach (var k in body.Effects.Select(e => e.Kind).Except(kindsBefore)) events.Add(new EffectStarted(world.Minute, agent.Id, k));

        if (Body.IsAlive(body)) return null;
        events.Add(new Died(world.Minute, agent.Id));
        return "died";
    }
}
