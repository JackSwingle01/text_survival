namespace Survival.Tests;

/// Walking between tiles. Minutes and risks pinned from old TravelProcessor.PreviewCrossing on
/// the old seed-17 map (golden harness), which the rebuild's map reproduces tile for tile.
public class TravelTests
{
    static readonly AgentId P = AgentId.Player;
    static readonly GeneratedMap Seed17 = TestWorlds.Map(17);

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

    /// January, when the river is ice: the old game's never thawed. These tiles keep their ground as
    /// generated, as at minute 0 under a pinned sky (which has no history), so only the ice differs.
    static World InJanuary(World world, params TileId[] tiles)
    {
        world.Minute = TestKit.JanuaryMinute;
        foreach (var t in tiles)
        {
            var ground = Ground.Baseline(Ground.Substrate(Seed17, t));
            ground.LastUpdatedMinute = world.Minute;
            Ground.SetClassState(world, t, ground);
        }
        return world;
    }

    // from, to, sky, old quick minutes, old careful minutes, risk: the footing curve on the tile walked onto.
    public static readonly TheoryData<int, int, int, int, string, int, int, double> OldCrossings = new()
    {
        { 2, 33, 2, 34, "calm", 7, 10, 0.018951265351475146 },     // Forest -> Rock (0.3)
        { 2, 51, 2, 52, "calm", 5, 5, 0 },                         // Plain -> Plain: the 5-minute floor, no risk
        { 26, 2, 26, 3, "calm", 8, 12, 0.05705633641617645 },      // Marsh beside a river -> Forest beside a river (0.45)
        { 7, 42, 8, 42, "calm", 5, 5, 0.0009565070511121593 },     // camp -> Forest (0.1)
        { 211, 43, 212, 43, "calm", 22, 34, 0.5 },                 // Rock -> The Pass Proper (1.0)
        { 222, 43, 223, 43, "calm", 10, 16, 0.018951265351475146 }, // Rock -> Far Side (0.3)
        { 2, 33, 2, 34, "blizzard", 8, 11, 0.03475897330008071 },  // 0.3 x 1.25
        { 26, 2, 26, 3, "blizzard", 9, 14, 0.10464840406743224 },  // 0.45 x 1.25
        { 211, 43, 212, 43, "blizzard", 29, 44, 0.5 },
        { 222, 43, 223, 43, "blizzard", 11, 17, 0.03475897330008071 },
        { 2, 33, 2, 34, "lightsnow", 7, 10, 0.027709714362850982 }, // 0.3 x 1.15
        { 222, 43, 223, 43, "lightsnow", 10, 16, 0.027709714362850982 },
    };

    [Theory, MemberData(nameof(OldCrossings))]
    public void CrossingMinutesAndRisk_MatchOld(int ax, int ay, int bx, int by, string sky, int quick, int careful, double risk)
    {
        var s = (sky switch { "blizzard" => Blizzard, "lightsnow" => LightSnow, _ => Calm }) with { Minute = TestKit.JanuaryMinute };
        TileId a = new(ax, ay), b = new(bx, by);
        var world = InJanuary(At(ax, ay, s), a, b);
        var body = TestKit.OldBaselineHuman(); // the old game's traveller
        Assert.Equal(quick, Traversal.CrossingMinutes(world, body, a, b, Pace.Quick, s));
        Assert.Equal(careful, Traversal.CrossingMinutes(world, body, a, b, Pace.Careful, s));
        Assert.Equal(risk, Traversal.InjuryRiskPct(world, b, body, s), 12);
        Assert.Equal(risk / 4, Traversal.InjuryRiskPct(world, b, body, s, Pace.Careful), 12);
    }

    /// A December afternoon of falling snow on seed 5: deep snow over puddles that froze in autumn.
    static World SnowyDecemberAfternoon()
    {
        const int december15 = 349, afternoonMinuteOfDay = 15 * 60;
        var world = TestWorlds.New(5);
        long first = Enumerable.Range(0, 400).Select(d => d * 1440L).First(t => Weather.DayOfYear(t) == december15);
        long afternoon = first + afternoonMinuteOfDay - Weather.MinuteOfDay(first);
        world.Minute = Enumerable.Range(0, 60).Select(d => afternoon + d * 1440L)
            .First(t => Weather.At(world, t) is { Condition: WeatherCondition.LightSnow or WeatherCondition.HeavySnow, TemperatureF: < 25 });
        return world;
    }

    [Fact]
    public void QuickCrossingInDecemberSnow_IsRiskyEnoughToWeighOnlyWhereTheGroundIs()
    {
        var world = SnowyDecemberAfternoon();
        var m = Map.Of(world);
        var me = world.Agent(P);
        bool Open(TileId t) => m.SiteAt(t) == null && m.CaveAt(t) == Cave.None && t != m.Camp && m.TerrainAt(t).Row().Passable
            && Map.Blocked(m, t, Map.Step(t, Direction.North)) == null;
        var valley = Enumerable.Range(30, m.Height - 35).SelectMany(y => Enumerable.Range(5, m.Width - 10).Select(x => new TileId(x, y))).Where(Open).ToList();

        var plain = valley.First(t => m.TerrainAt(t) == Terrain.Plain && m.TerrainAt(Map.Step(t, Direction.North)) == Terrain.Plain
            && Ground.Underfoot(world, t) is { Iced: true, SnowDepthM: > 0.3 });
        me.Tile = plain;
        Assert.False(Trip.PaceMatters(world, P, Direction.North), $"quick risk {Trip.Risk(world, P, Direction.North, Pace.Quick).TotalPct:P2} on buried ice");

        var rock = valley.First(t => m.TerrainAt(t) == Terrain.Rock && m.TerrainAt(Map.Step(t, Direction.North)) == Terrain.Rock);
        me.Tile = rock;
        var quick = Trip.Risk(world, P, Direction.North, Pace.Quick);
        Assert.True(Trip.PaceMatters(world, P, Direction.North), "rock is hazardous at a quick pace");
        Assert.Equal(quick.TotalPct / 4, Trip.Risk(world, P, Direction.North, Pace.Careful).TotalPct, 12);
        Assert.True(Trip.Time(world, P, Direction.North, Pace.Careful).TotalMinutes > Trip.Time(world, P, Direction.North, Pace.Quick).TotalMinutes,
            "careful pays for its care in minutes");

        bool Gentle(TileId t) => Map.TravelHazardLevel(m, t, world.Minute) <= 0.1 && Ground.Underfoot(world, t).SnowDepthM > 0.3;
        var gentle = valley.Where(t => Gentle(t) && Gentle(Map.Step(t, Direction.North))).ToList();
        Assert.True(gentle.Count > 500, $"only {gentle.Count} gentle snowy crossings");
        var risky = gentle.Where(t => { me.Tile = t; return Trip.PaceMatters(world, P, Direction.North); }).ToList();
        Assert.True(risky.Count == 0, $"{risky.Count} of {gentle.Count} gentle snowy crossings risk an injury worth weighing, first {risky.FirstOrDefault()}");
    }

    [Fact]
    public void InjuryRisk_RisesWithImpairedMovement()
    {
        TileId forestOnRiver = new(26, 3); // effective hazard 0.45 while the river is ice
        var world = InJanuary(At(2, 33), forestOnRiver, new TileId(2, 33));
        var body = world.Agent(P).Body;
        double healthy = Traversal.InjuryRiskPct(world, forestOnRiver, body, Calm);
        Body.Damage(body, new DamageInfo(0.6, DamageType.Blunt, BodyTarget.LeftLeg), world.Rng("body"));
        double moving = Body.GetCapacities(body).Moving;
        Assert.True(moving < 1);
        Assert.Equal(Math.Pow(0.45 * (1 + (1 - moving) * 0.3), Math.E) / 2, Traversal.InjuryRiskPct(world, forestOnRiver, body, Calm), 12);
        Assert.True(Traversal.InjuryRiskPct(world, forestOnRiver, body, Calm) > healthy);
        Assert.False(Traversal.PaceMatters(Traversal.InjuryRiskPct(world, new TileId(2, 33), body, Calm)), "easy ground stays easy on a limp");
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
    /// (bug, fixed). The one roll is the tile walked onto's, and draws from that tile's pools.
    [Fact]
    public void ACrossingsInjury_ComesFromThePoolsOfTheTileWalkedOnto()
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
        foreach (var (from, to, way) in new[] { (river, dry, dir), (dry, river, opposite) })
        {
            var world = InJanuary(At(from.X, from.Y), from, to);
            var hits = new List<Injured>();
            for (int i = 0; i < 200; i++)
            {
                var agent = world.Agent(P);
                agent.Tile = from;
                agent.Members[0] = TestKit.OldBaselineHuman();
                agent.Crossing = new Crossing { From = from, Dir = way, Pace = Pace.Quick, TotalMinutes = 1, RiskPct = 1 };
                var events = new List<WorldEvent>();
                Traversal.Advance(world, agent, events);
                hits.AddRange(events.OfType<Injured>());
            }
            Assert.Equal(200, hits.Count);
            Assert.All(hits, h => Assert.Equal(to, h.Tile));
            if (to == river) Assert.All(hits, h => Assert.Equal(DamageType.Blunt, h.Type)); // ice slips and stumbles, never the dry tile's sharp hazards
            else Assert.Contains(hits, h => h.Type == DamageType.Sharp);
        }
    }

    /// Hills (0.25) are a choice of pace no more: their fall chance sits just under the line. Rock (0.3) is.
    [Fact]
    public void Hills_AreQuickOnly_RockOffersACarefulPace()
    {
        TileId Bare(Terrain t) => Enumerable.Range(0, Seed17.Width).SelectMany(x => Enumerable.Range(0, Seed17.Height).Select(y => new TileId(x, y)))
            .First(p => Seed17.TerrainAt(p) == t && Seed17.SiteAt(p) == null && Seed17.CaveAt(p) == Cave.None && !Seed17.BesideRiver(p) && p != Seed17.Camp);
        var (hills, rock) = (Bare(Terrain.Hills), Bare(Terrain.Rock));
        var world = At(hills.X, hills.Y);
        Assert.False(Traversal.IsHazardous(world, hills));
        Assert.True(Traversal.IsHazardous(world, rock));
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

    /// Rock -> The Pass Proper (hazard 1): the crossing rolls once, for the tile walked onto, so half of
    /// quick crossings hurt (two rolls, leaving and entering, hurt about two in three), and an eighth of careful ones.
    [Fact]
    public void ACrossingRollsOnce_ForTheTileWalkedOnto_CarefulAQuarterAsOften()
    {
        const int Trials = 400;
        double Rate(Pace pace)
        {
            int injured = 0;
            for (int i = 0; i < Trials; i++)
            {
                var world = At(211, 43, travelRngSeed: $"trial{i}");
                var r = Sim.Step(world, new Travel(P, Direction.East, pace), 600);
                var hurt = r.Events.OfType<Injured>().ToList();
                Assert.True(hurt.Count <= 1);
                if (hurt.Count == 0) continue;
                injured++;
                Assert.Equal(new TileId(212, 43), hurt[0].Tile);
                Assert.True(world.Agent(P).Body.Regions.Any(reg => reg.Condition < 1), "injury left no damage");
            }
            return injured / (double)Trials;
        }
        Assert.InRange(Rate(Pace.Quick), 0.43, 0.57);
        Assert.InRange(Rate(Pace.Careful), 0.08, 0.17);
    }

    /// Careful takes a quarter of the quick risk and pays for it in minutes; on easy ground there is
    /// next to nothing to be careful of.
    [Fact]
    public void CarefulCarriesAQuarterOfTheQuickRisk_AndIsSlower()
    {
        foreach (var sky in new[] { Calm, LightSnow, Blizzard })
        {
            var world = At(2, 33, sky);
            var body = world.Agent(P).Body;
            TileId forest = new(2, 33), rock = new(2, 34);
            double quick = Traversal.InjuryRiskPct(world, rock, body, sky);
            Assert.True(quick > 0.015, $"rock, quick: {quick:P2}");
            Assert.Equal(quick * 0.25, Traversal.InjuryRiskPct(world, rock, body, sky, Pace.Careful), 12);
            Assert.True(Traversal.CrossingMinutes(world, body, forest, rock, Pace.Careful, sky) > Traversal.CrossingMinutes(world, body, forest, rock, Pace.Quick, sky));
            Assert.Equal(quick * 0.25, Trip.Risk(world, P, Direction.South, Pace.Careful).TotalPct, 12);
            Assert.InRange(Traversal.InjuryRiskPct(world, forest, body, sky, Pace.Careful), 0, 0.001);
        }
        // A short crossing onto rock: quick, it is the five-minute floor; careful, more.
        var shortWay = At(19, 46);
        var walker = shortWay.Agent(P).Body;
        TileId shortFrom = new(19, 46), rockBeyond = new(19, 47);
        Assert.Equal(Traversal.MinimumCrossingMinutes, Traversal.CrossingMinutes(shortWay, walker, shortFrom, rockBeyond, Pace.Quick, Calm));
        Assert.True(Traversal.CrossingMinutes(shortWay, walker, shortFrom, rockBeyond, Pace.Careful, Calm) > Traversal.MinimumCrossingMinutes,
            "the five-minute floor swallowed the careful minutes");

        var plain = At(2, 51);
        Assert.Equal(0, Trip.Risk(plain, P, Direction.South, Pace.Careful).TotalPct);
        Assert.Equal(Trip.Time(plain, P, Direction.South, Pace.Quick), Trip.Time(plain, P, Direction.South, Pace.Careful)); // nothing to be careful of
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
        var world = TestWorlds.New(seed);
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
    [Trait("Category", TestCategories.LongRun)]
    public void ADayOfTravel_IsDeterministic()
    {
        Assert.Equal(RunRoute(17), RunRoute(17));
        Assert.NotEqual(RunRoute(17), RunRoute(18));
    }

    [Fact]
    [Trait("Category", TestCategories.LongRun)]
    public void SaveLoadMidCrossing_GivesTheSameWorldAsAStraightRun()
    {
        string straight = RunRoute(17);
        Assert.Equal(straight, RunRoute(17, saveAtStep: 7));
        Assert.Equal(straight, RunRoute(17, saveAtStep: 30, splitMinutes: 1));
        Assert.True(straight.Length < 1_000_000);
    }

    // ---- open water ----

    /// Seed 17's first unnamed lake tile with land west and east of it.
    internal static (TileId West, TileId Lake, TileId East) LakeBetweenLand(GeneratedMap m)
    {
        bool Land(TileId t) => m.InBounds(t) && m.TerrainAt(t).IsPassable() && m.TerrainAt(t) != Terrain.Water && m.CaveAt(t) == Cave.None
            && m.SiteAt(t) == null && t != m.Camp;
        for (int y = 3; y < m.Height - 3; y++)
            for (int x = 3; x < m.Width - 3; x++)
            {
                var (lake, w, e) = (new TileId(x, y), new TileId(x - 1, y), new TileId(x + 1, y));
                if (m.TerrainAt(lake) == Terrain.Water && m.SiteAt(lake) == null && m.CaveAt(lake) == Cave.None
                    && Land(w) && Land(e) && Map.Blocked(m, w, lake) == null && Map.Blocked(m, lake, e) == null)
                    return (w, lake, e);
            }
        throw new InvalidOperationException("seed 17 has no lake tile between land");
    }

    /// In July a lake is a foot of water to wade: slower than its ice, and it soaks you on the way through.
    /// In January it is ice, with no water on it to wade or to soak you.
    [Fact]
    public void ALake_IsWadedInJuly_AndWalkedOnAsIceInJanuary()
    {
        var w = TestWorlds.New(17);
        var m = Map.Of(w);
        var (west, lake, east) = LakeBetweenLand(m);
        var me = w.Agent(P);
        me.Tile = west;
        me.Crossing = null;
        var onto = new Travel(P, Direction.East, Pace.Careful);
        Assert.False(Map.IsFrozen(m, lake, w.Minute));
        Assert.Null(Verbs.At(w, P).Walk.Single(v => v.Activity == new Travel(P, Direction.East)).Refused);
        Assert.Equal(new SeenSurface(0, false, 0, Ground.MaxStandingWaterDepthM, false), SeenSurface.Of(Ground.Underfoot(w, lake)));
        Assert.True(Trip.Time(w, P, Direction.East, Pace.Careful).GroundMinutes > 0, "wading is slower than the walk");
        Assert.Equal("arrived", Sim.Step(w, onto, 600).InterruptedReason);
        Assert.Equal(lake, me.Tile);
        double dry = Effects.Severity(me.Body, EffectKind.Wet);
        Assert.Equal("arrived", Sim.Step(w, onto, 600).InterruptedReason);
        Assert.Equal(east, me.Tile);
        Assert.True(Effects.Severity(me.Body, EffectKind.Wet) > dry + 0.1, $"waded out at {Effects.Severity(me.Body, EffectKind.Wet):F2} wet, from {dry:F2}");

        w.Minute = TestKit.JanuaryMinute;
        Assert.True(Map.IsFrozen(m, lake, w.Minute));
        Assert.Equal(0, Ground.Underfoot(w, lake).WaterDepthM);
        Assert.Equal(0, Ground.Underfoot(w, lake).ContactWettingRate(SurfaceContact.Walking, 0));
    }

    /// A shore drinks from the open lake beside it in July; in January the lake is ice and the shore is dry.
    [Fact]
    public void AShore_HasWaterToDrinkInJuly_NotInJanuary()
    {
        var w = TestWorlds.New(17);
        var m = Map.Of(w);
        var shore = Enumerable.Range(0, m.Width).SelectMany(x => Enumerable.Range(0, m.Height).Select(y => new TileId(x, y)))
            .First(t => m.TerrainAt(t) is Terrain.Forest or Terrain.Plain && m.SiteAt(t) == null && !m.BesideRiver(t) && m.CaveAt(t) == Cave.None
                && Enum.GetValues<Direction>().Any(d => Map.Step(t, d) is var n && m.InBounds(n) && m.TerrainAt(n) == Terrain.Water && Map.Blocked(m, t, n) == null));
        Assert.True(Work.HasOpenWater(w, shore));
        w.Minute = TestKit.JanuaryMinute;
        Assert.False(Work.HasOpenWater(w, shore));
    }

    /// Lakes and rivers freeze over and break up on the days terrain.json names, every year alike.
    [Fact]
    public void WaterIsIce_FromFreezeUpToBreakUp()
    {
        var m = TestWorlds.Map(17);
        var lake = LakeBetweenLand(m).Lake;
        var ice = Content.Tables.WaterIce;
        // The first time each day of the year comes round after 1 July.
        long MinuteOf(int dayOfYear) => Enumerable.Range(0, 365).Select(d => d * 1440L).First(t => Weather.DayOfYear(t) == dayOfYear);
        Assert.False(Map.IsFrozen(m, lake, MinuteOf(ice.FreezeDay - 1)));
        Assert.True(Map.IsFrozen(m, lake, MinuteOf(ice.FreezeDay)));
        Assert.True(Map.IsFrozen(m, lake, MinuteOf(1)));
        Assert.True(Map.IsFrozen(m, lake, MinuteOf(ice.ThawDay - 1)));
        Assert.False(Map.IsFrozen(m, lake, MinuteOf(ice.ThawDay)));
        Assert.True(Map.IsFrozen(m, lake, MinuteOf(ice.FreezeDay) + 365 * 1440L));
        Assert.Throws<ArgumentException>(() => Map.IsFrozen(m, m.Camp, 0)); // no water there to freeze
    }
}
