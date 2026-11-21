namespace Survival.Tests;

/// Walking between tiles. Minutes and risks pinned from old TravelProcessor.PreviewCrossing on
/// the old seed-17 map (golden harness), which the rebuild's map reproduces tile for tile.
public class TravelTests
{
    static readonly AgentId P = AgentId.Player;
    static readonly GeneratedMap Seed17 = Map.Generate(17);

    static readonly WeatherSample Calm = new(0, WeatherCondition.Clear, -10, 0, 0, 0, WindDirection.North);
    static readonly WeatherSample Blizzard = new(0, WeatherCondition.Blizzard, -10, 0.8, 0.7, 1, WindDirection.North);
    static readonly WeatherSample LightSnow = new(0, WeatherCondition.LightSnow, -10, 0.2, 0.2, 1, WindDirection.North);

    /// A seed-17 world with the player placed at a tile and the sky pinned. Shares one generated map.
    static World At(int x, int y, WeatherSample? sky = null, string? travelRngSeed = null)
    {
        var world = new World { Seed = 17, GeneratedMap = Seed17, FixedWeather = sky ?? Calm };
        world.Agents[P] = new Agent { Id = P, Tile = new TileId(x, y) };
        if (travelRngSeed != null) world.Rngs[StreamName.Of("travel", P)] = Rng.ForSystem(17, travelRngSeed);
        return world;
    }

    // from, to, sky, old quick minutes, old careful minutes, old risk (max of the two segments)
    public static readonly TheoryData<int, int, int, int, string, int, int, double> OldCrossings = new()
    {
        { 2, 33, 2, 34, "calm", 7, 10, 0.3 },          // Forest -> Rock
        { 2, 51, 2, 52, "calm", 5, 5, 0 },             // Plain -> Plain: the 5-minute floor
        { 26, 2, 26, 3, "calm", 8, 12, 0.5 },          // Marsh beside a river -> Forest beside a river: capped
        { 7, 42, 8, 42, "calm", 5, 5, 0 },             // camp -> Forest
        { 211, 43, 212, 43, "calm", 22, 34, 0.5 },     // Rock -> The Pass Proper
        { 222, 43, 223, 43, "calm", 10, 16, 0.3 },     // Rock -> Far Side
        { 2, 33, 2, 34, "blizzard", 8, 11, 0.5 },
        { 26, 2, 26, 3, "blizzard", 9, 14, 0.5 },
        { 211, 43, 212, 43, "blizzard", 29, 44, 0.5 },
        { 222, 43, 223, 43, "blizzard", 11, 17, 0.5 },
        { 2, 33, 2, 34, "lightsnow", 7, 10, 0.44999999999999996 },
        { 222, 43, 223, 43, "lightsnow", 10, 16, 0.44999999999999996 },
    };

    [Theory, MemberData(nameof(OldCrossings))]
    public void CrossingMinutesAndRisk_MatchOld(int ax, int ay, int bx, int by, string sky, int quick, int careful, double risk)
    {
        var s = sky switch { "blizzard" => Blizzard, "lightsnow" => LightSnow, _ => Calm };
        var world = At(ax, ay, s);
        var body = TestKit.OldBaselineHuman(); // the old game's traveller
        TileId a = new(ax, ay), b = new(bx, by);
        Assert.Equal(quick, Traversal.CrossingMinutes(world, body, a, b, Pace.Quick, s));
        Assert.Equal(careful, Traversal.CrossingMinutes(world, body, a, b, Pace.Careful, s));
        Assert.Equal(risk, Math.Max(Traversal.InjuryRiskPct(world, a, body, s), Traversal.InjuryRiskPct(world, b, body, s)), 12);
    }

    [Fact]
    public void InjuryRisk_RisesWithImpairedMovement()
    {
        var world = At(2, 33);
        var body = world.Agent(P).Body;
        TileId forestOnRiver = new(26, 3); // effective hazard 0.45
        double healthy = Traversal.InjuryRiskPct(world, forestOnRiver, body, Calm);
        Body.Damage(body, new DamageInfo(0.6, DamageType.Blunt, BodyTarget.LeftLeg), world.Rng("body"));
        double moving = Body.GetCapacities(body).Moving;
        Assert.True(moving < 1);
        Assert.Equal(Math.Min(0.5, healthy + (1 - moving) * 0.3), Traversal.InjuryRiskPct(world, forestOnRiver, body, Calm), 12);
        Assert.Equal(0, Traversal.InjuryRiskPct(world, new TileId(2, 33), body, Calm)); // forest stays below the threshold
    }

    [Fact]
    public void Travel_ArrivesOnTheLastMinute_AndLivesEveryMinute()
    {
        var world = At(2, 51);
        double kcal = world.Agent(P).Body.CaloriesKcal;
        var r = Sim.Step(world, new Travel(P, Direction.South), 600);

        Assert.Equal(("arrived", 5), (r.InterruptedReason, r.MinutesElapsed));
        Assert.Equal(5, world.Minute);
        Assert.Equal(new TileId(2, 52), world.Agent(P).Tile);
        Assert.Null(world.Agent(P).Crossing);
        var moved = Assert.Single(r.Events.OfType<Moved>());
        Assert.Equal((new TileId(2, 51), new TileId(2, 52), 5L), (moved.From, moved.To, moved.Minute));

        // Walking (activity 1.5) burns more than waiting the same minutes.
        var waited = At(2, 51);
        Sim.Step(waited, new Wait(P), 5);
        Assert.True(kcal - world.Agent(P).Body.CaloriesKcal > kcal - waited.Agent(P).Body.CaloriesKcal);
    }

    [Fact]
    public void Travel_StaysOnTheOriginUntilTheCrossingCompletes_AndCanBeContinued()
    {
        var world = At(2, 33);
        var r = Sim.Step(world, new Travel(P, Direction.South, Pace.Careful), 4);
        Assert.Equal((null, 4), (r.InterruptedReason, r.MinutesElapsed));
        Assert.Equal(new TileId(2, 33), world.Agent(P).Tile);
        Assert.Equal(4, world.Agent(P).Crossing!.ElapsedMinutes);

        r = Sim.Step(world, new Travel(P, Direction.South, Pace.Careful), 600);
        // 8 in all for the 68 kg man (species round); 10 for the old game's 75 kg traveller.
        Assert.Equal(("arrived", 4), (r.InterruptedReason, r.MinutesElapsed));
        Assert.Equal(new TileId(2, 34), world.Agent(P).Tile);
    }

    [Fact]
    public void AnotherActivity_AbandonsAHalfCrossing()
    {
        var world = At(2, 33);
        Sim.Step(world, new Travel(P, Direction.South, Pace.Careful), 4);
        Sim.Step(world, new Wait(P), 1);
        Assert.Null(world.Agent(P).Crossing);
        Assert.Equal(new TileId(2, 33), world.Agent(P).Tile);
        Assert.Equal(("arrived", 8), Done(Sim.Step(world, new Travel(P, Direction.South, Pace.Careful), 600))); // old 75 kg traveller: 10

        static (string?, int) Done(StepResult r) => (r.InterruptedReason, r.MinutesElapsed);
    }

    [Fact]
    public void ACrossingStartedElsewhere_IsNotResumed()
    {
        var world = At(2, 33);
        Sim.Step(world, new Travel(P, Direction.South, Pace.Careful), 4);
        var agent = world.Agent(P);
        // Something else moved the agent (a fall, a drag, a load): the half-walk belonged to another tile.
        TileId elsewhere = new(2, 51), beyond = new(2, 52);
        agent.Tile = elsewhere;
        var r = Sim.Step(world, new Travel(P, Direction.South, Pace.Careful), 600);
        int fresh = Traversal.CrossingMinutes(world, agent.Body, elsewhere, beyond, Pace.Careful, Calm);
        Assert.Equal(("arrived", fresh), (r.InterruptedReason, r.MinutesElapsed));
        Assert.Equal(beyond, agent.Tile);
        Assert.Equal((elsewhere, beyond), (Assert.Single(r.Events.OfType<Moved>()).From, agent.Tile));
    }

    /// old game: InjuryVariant.HasIce read the destination's river even for the origin's injury
    /// (bug, fixed): each segment draws from its own tile's pools.
    [Fact]
    public void EachSegmentsInjury_ComesFromItsOwnTilesPools()
    {
        var m = Seed17;
        // A river tile beside a dry one whose pools hold sharp hazards; river pools are blunt only.
        var (river, dry, dir) = (from x in Enumerable.Range(0, m.Width)
                                 from y in Enumerable.Range(0, m.Height)
                                 let t = new TileId(x, y)
                                 where m.BesideRiver(t) && m.CaveAt(t) == Cave.None && t != m.Camp
                                 from d in Enum.GetValues<Direction>()
                                 let n = Map.Step(t, d)
                                 where m.InBounds(n) && Map.Blocked(m, t, n) == null && !m.BesideRiver(n) && m.StageAt(n) == null
                                     && n != m.Camp && m.CaveAt(n) == Cave.None && Map.TerrainHazardLevel(m, n) < 0.5
                                 select (t, n, d)).First();
        var opposite = (Direction)(((int)dir + 2) % 4);
        foreach (var (from, to, way, originRisk) in new[] { (river, dry, dir, 1.0), (dry, river, opposite, 0.0) })
        {
            var world = At(from.X, from.Y);
            var hits = new List<Injured>();
            for (int i = 0; i < 200; i++)
            {
                var agent = world.Agent(P);
                agent.Tile = from;
                agent.Members[0] = TestKit.OldBaselineHuman();
                agent.Crossing = new Crossing { From = from, Dir = way, Pace = Pace.Quick, TotalMinutes = 1, OriginRiskPct = originRisk, DestRiskPct = 1 - originRisk };
                var events = new List<WorldEvent>();
                Traversal.Advance(world, agent, events);
                hits.AddRange(events.OfType<Injured>());
            }
            Assert.Equal(200, hits.Count);
            Assert.All(hits, h => Assert.Equal(river, h.Tile));
            Assert.All(hits, h => Assert.Equal(DamageType.Blunt, h.Type)); // ice slips and stumbles, never the dry tile's sharp hazards
        }
    }

    [Fact]
    public void ImpassableMove_IsRefusedAtNoCost()
    {
        var m = Seed17;
        var (tile, dir) = (from x in Enumerable.Range(0, m.Width)
                           from y in Enumerable.Range(0, m.Height)
                           let t = new TileId(x, y)
                           where m.TerrainAt(t).IsPassable()
                           from d in Enum.GetValues<Direction>()
                           where m.InBounds(Map.Step(t, d)) && m.TerrainAt(Map.Step(t, d)) == Terrain.Mountain
                           select (t, d)).First();
        var world = At(tile.X, tile.Y);
        string before = Save.ToJson(world);
        var r = Sim.Step(world, new Travel(P, dir), 60);
        Assert.Equal(("mountains block the way", 0), (r.InterruptedReason, r.MinutesElapsed));
        Assert.Empty(r.Events);
        Assert.Equal(before, Save.ToJson(world));
    }

    [Fact]
    public void Travel_IsRefusedWhenTheBodyCannotWalk()
    {
        var world = At(2, 51);
        var body = world.Agent(P).Body;
        foreach (var leg in new[] { BodyTarget.LeftLeg, BodyTarget.RightLeg })
            for (int i = 0; i < 6 && Body.GetCapacities(body).Moving > 0.1; i++)
                Body.Damage(body, new DamageInfo(1.0, DamageType.Blunt, leg), world.Rng("body"));
        Assert.True(Body.GetCapacities(body).Moving <= 0.1, $"moving {Body.GetCapacities(body).Moving}");
        Assert.True(Body.IsAlive(body));
        var r = Sim.Step(world, new Travel(P, Direction.South), 60);
        Assert.Equal(("you can barely move", 0), (r.InterruptedReason, r.MinutesElapsed));
    }

    /// A walker bled nearly white and still bleeding, setting out on the 34-minute careful crossing onto The Pass Proper.
    static World Failing()
    {
        var world = At(211, 43);
        var body = world.Agent(P).Body;
        body.BloodPct = 0.57; // circulation 0.14: Moving falls to 0.1 at 0.55
        Assert.InRange(Body.GetCapacities(body).Moving, 0.1001, 0.2);
        Effects.Add(body, EffectKind.Bleeding, 1.0);
        return world;
    }

    /// Found by ActionSequenceTests: "you can barely move" was checked only when a step started, so a walker
    /// worn down mid-crossing finished it in one long step but was stopped short when the minutes were
    /// cut up. Now every minute asks: the walk stops at the same minute, on the origin with the crossing kept.
    [Fact]
    public void WornDownMidCrossing_StopsAtTheSameMinute_HoweverStepped()
    {
        var whole = Failing();
        var walk = new Travel(P, Direction.East, Pace.Careful);
        var r = Sim.Step(whole, walk, 600);
        Assert.Equal("you can barely move", r.InterruptedReason);
        Assert.InRange(r.MinutesElapsed, 1, 33); // stopped part way across, not refused at the start
        Assert.Equal(new TileId(211, 43), whole.Agent(P).Tile);
        Assert.Equal(r.MinutesElapsed, whole.Agent(P).Crossing!.ElapsedMinutes);

        var chunked = Failing();
        string? reason = null;
        while (reason == null) reason = Sim.Step(chunked, walk, 7).InterruptedReason;
        Assert.Equal("you can barely move", reason);
        Assert.Equal(Save.ToJson(whole), Save.ToJson(chunked));
    }

    [Fact]
    public void QuickHazardousCrossings_InjureAtTheOldRate_CarefulNever()
    {
        // Forest (safe) -> Rock (0.3): only the entry rolls, so about 30% of quick crossings hurt.
        const int Trials = 400;
        int injured = 0;
        for (int i = 0; i < Trials; i++)
        {
            var world = At(2, 33, travelRngSeed: $"trial{i}");
            var r = Sim.Step(world, new Travel(P, Direction.South), 600);
            var hurt = r.Events.OfType<Injured>().ToList();
            Assert.True(hurt.Count <= 1);
            if (hurt.Count == 0) continue;
            injured++;
            Assert.Equal(new TileId(2, 34), hurt[0].Tile);
            Assert.True(world.Agent(P).Body.Regions.Any(reg => reg.Condition < 1), "injury left no damage");
        }
        Assert.InRange(injured / (double)Trials, 0.24, 0.36);

        for (int i = 0; i < 100; i++)
        {
            var careful = Sim.Step(At(2, 33, travelRngSeed: $"trial{i}"), new Travel(P, Direction.South, Pace.Careful), 600);
            Assert.Empty(careful.Events.OfType<Injured>());
        }
    }

    [Fact]
    public void ReachingTheFarSide_CrossesThePass()
    {
        var world = At(222, 43);
        var r = Sim.Step(world, new Travel(P, Direction.East, Pace.Careful), 600);
        Assert.Equal("crossed the pass", r.InterruptedReason);
        Assert.Equal(Seed17.Exit, world.Agent(P).Tile);
        Assert.IsType<CrossedThePass>(r.Events[^1]);
    }

    // ---- saves and determinism ----

    /// A day on foot from camp, with a night's sleep: walk a fixed pattern of headings, taking the first open one, resting between legs.
    static List<Activity> Route(World world)
    {
        var m = Map.Of(world);
        var tile = world.Agent(P).Tile;
        var plan = new List<Activity>();
        Direction[] pattern = [Direction.East, Direction.East, Direction.North, Direction.East, Direction.South, Direction.East];
        for (int leg = 0; leg < 120; leg++)
        {
            var dir = Enumerable.Range(0, 4).Select(k => (Direction)(((int)pattern[leg % pattern.Length] + k) % 4))
                .First(d => Map.Blocked(m, tile, Map.Step(tile, d)) == null);
            tile = Map.Step(tile, dir);
            plan.Add(new Travel(P, dir, leg % 3 == 0 ? Pace.Careful : Pace.Quick));
            if (leg == 59) plan.Add(new Sleep(P));
            else if (leg % 10 == 9) plan.Add(new Rest(P));
        }
        return plan;
    }

    static string RunRoute(int seed, int? saveAtStep = null, int splitMinutes = 3)
    {
        var world = World.New(seed);
        world.FixedWeather = Calm with { TemperatureC = 5 }; // walking all day, not freezing to death
        int step = 0;
        foreach (var activity in Route(world))
        {
            if (!Body.IsAlive(world.Agent(P).Body)) break;
            int budget = activity switch { Rest => 60, Sleep => 480, _ => 600 };
            if (step++ == saveAtStep && activity is Travel)
            {
                Sim.Step(world, activity, splitMinutes); // part way across, then save and load mid-crossing
                Assert.NotNull(world.Agent(P).Crossing);
                world = Save.FromJson(Save.ToJson(world));
            }
            Sim.Step(world, activity, budget);
        }
        Assert.True(world.Minute >= 24 * 60, $"route lasted only {world.Minute} minutes");
        return Save.ToJson(world);
    }

    [Fact]
    public void ADayOfTravel_IsDeterministic()
    {
        Assert.Equal(RunRoute(17), RunRoute(17));
        Assert.NotEqual(RunRoute(17), RunRoute(18));
    }

    [Fact]
    public void SaveLoadMidCrossing_GivesTheSameWorldAsAStraightRun()
    {
        string straight = RunRoute(17);
        Assert.Equal(straight, RunRoute(17, saveAtStep: 7));
        Assert.Equal(straight, RunRoute(17, saveAtStep: 30, splitMinutes: 1));
        Assert.True(straight.Length < 1_000_000);
    }
}
