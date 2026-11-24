using Survival;
using Survival.Tests.Verification;
using Xunit.Abstractions;

/// The NPC mind's capabilities and orderings (Npc.cs): what it chooses, never how long it survives. The bench
/// (NpcBenchReport) reports survival and never gates.
public class NpcTests(ITestOutputHelper output)
{
    static (World World, AgentId Npc) AtCamp(int seed)
    {
        var w = TestWorlds.New(seed);
        return (w, NpcBench.Spawn(w, Map.Of(w).Camp));
    }

    /// A bare world with only the NPC in it, at camp, under a pinned sky.
    static (World World, Agent Npc) Alone(double tempC)
    {
        var w = ConservationWorlds.Bare(tempC);
        w.Agents.Remove(AgentId.Player);
        var npc = Npc.Spawn(w, Map.Of(w).Camp);
        return (w, w.Agent(npc));
    }

    static NpcMind MindOf(Agent a) => (NpcMind)a.Mind;

    static (Activity Activity, int Minutes) Decide(World w, Agent a) => Npc.Decide(w, a, []);

    static FireState Pit(World w, TileId tile, Resource fuel, double kg)
    {
        var pit = new FireState { Tile = tile, LastUpdatedMinute = w.Minute, Pit = PitType.Open };
        Fire.AddFuel(pit, Resource.Kindling, 1);
        Fire.IgniteAll(pit);
        Fire.AddFuel(pit, fuel, kg);
        w.Fires[new FireId(w.NextFireId++)] = pit;
        return pit;
    }

    // ---- the ladder ----

    /// Freezing beside a fire that is burning low, and parched with nothing to drink: warmth comes first, so it feeds
    /// the fire before it sees to water.
    [Fact]
    public void Freezing_WithAFireOnHand_TendsItBeforeFetchingWater()
    {
        var (w, npc) = Alone(-15);
        var camp = Map.Of(w).Camp;
        Pit(w, camp, Resource.Kindling, 0.2);
        var pack = MindOf(npc).Pack;
        TestKit.Remove(pack, Resource.Water);
        TestKit.Give(pack, Resource.Birch, 4);
        npc.Body.CoreTempF = 96.0;
        npc.Body.HydrationMl = 0.4 * Body.MaxHydrationMl;
        var (a, _) = Decide(w, npc);
        output.WriteLine($"{a}");
        Assert.IsType<AddFuel>(a);
    }

    /// Water at Critical outranks a food plan at Want, with no water to hand: the plan gives way to the water.
    [Fact]
    public void ACommittedPlan_IsDropped_WhenAnotherStatReachesAWorsePriority()
    {
        var (w, npc) = Alone(5);
        var mind = MindOf(npc);
        mind.Plan = new NpcPlan(Stat.Food, Priority.Want, w.Minute, 600);
        npc.Body.HydrationMl = 0.1 * Body.MaxHydrationMl;
        var (a, _) = Decide(w, npc);
        output.WriteLine($"{a}, plan {mind.Plan}");
        Assert.NotNull(mind.Plan);
        Assert.Equal((Stat.Water, Priority.Critical), (mind.Plan!.Stat, mind.Plan.Priority));
    }

    /// A plan whose own stat worsens is held at the worse priority.
    [Fact]
    public void APlansOwnStatWorsening_RaisesItsPriority()
    {
        var (w, npc) = Alone(5);
        var mind = MindOf(npc);
        mind.Plan = new NpcPlan(Stat.Water, Priority.Want, w.Minute, 600);
        npc.Body.HydrationMl = 0.1 * Body.MaxHydrationMl;
        Decide(w, npc);
        Assert.Equal(Priority.Critical, mind.Plan?.Priority);
    }

    /// Out on a trip for food when thirst turns Critical, it gives the trip up for the water it carries.
    [Fact]
    public void OutForFood_ItAbandonsTheTrip_WhenItsThirstWorsens()
    {
        var (w, npc) = AtCamp(17);
        var a = w.Agent(npc);
        var mind = MindOf(a);
        var out1 = MapGen.Neighbors(mind.Camp).First(t => Map.Blocked(Map.Of(w), mind.Camp, t) == null);
        a.Tile = out1;
        mind.Plan = new NpcPlan(Stat.Food, Priority.Need, w.Minute, 600);
        a.Body.HydrationMl = 0.12 * Body.MaxHydrationMl;
        var (act, _) = Decide(w, a);
        Assert.False(act is Gather { Focus: ForageFocus.Food } || mind.Plan?.Stat == Stat.Food, $"kept to the food trip: {act}");
        Assert.False(act is Gather { Focus: ForageFocus.Food }, $"kept foraging: {act}");
    }

    /// A plan outlives its forecast only so long: past its budget it is dropped and planned afresh.
    [Fact]
    public void APlanPastItsTimeBudget_IsPlannedAfresh()
    {
        var (w, npc) = Alone(5);
        var mind = MindOf(npc);
        var stale = new NpcPlan(Stat.Food, Priority.Want, w.Minute - 500, 60);
        mind.Plan = stale;
        Decide(w, npc);
        Assert.True(mind.Plan == null || mind.Plan.StartedMinute == w.Minute, $"the stale plan ran on: {mind.Plan}");
    }

    // ---- the tree ----

    /// No tinder, and tinder to be found by foraging nearby: it gathers before it strikes, and strikes once it has
    /// what it went for, never walking back and forth between two places.
    [Fact]
    public void NoTinder_ItGathersThenLights_WithoutPingPonging()
    {
        var (w, npc) = Alone(-10);
        var camp = Map.Of(w).Camp;
        var pack = MindOf(npc).Pack;
        foreach (var t in Fire.Tinders) TestKit.Remove(pack, t);
        TestKit.Give(Items.PileAt(w, camp), Resource.Birch, 20);
        w.Minute = 9 * 60; // evening: a fire is wanted tonight
        var moves = new List<(TileId To, bool Worked)>(); // where it stepped, and whether it did anything there before moving on
        bool lit = false;
        for (int step = 0; step < 60 && !lit; step++)
        {
            var (a, minutes) = Decide(w, npc);
            if (a is StartFire) { lit = true; break; }
            Assert.False(a is StartFire, "struck with no tinder");
            var r = Sim.Step(w, a, Math.Min(minutes, 60));
            if (r.Events.OfType<Moved>().LastOrDefault(m => m.Who == npc.Id) is { } moved) moves.Add((moved.To, false));
            else if (moves.Count > 0) moves[^1] = (moves[^1].To, true);
        }
        output.WriteLine($"moves: {string.Join(" ", moves.Select(m => m.To))}; tinder {Fire.Tinders.Sum(t => Items.Have(pack, t)):0.00}");
        Assert.True(lit, "it never came to light the fire");
        Assert.True(Fire.Tinders.Sum(t => Items.Have(pack, t)) >= Verbs.TinderKg);
        for (int i = 2; i < moves.Count; i++)
            Assert.False(moves[i].To == moves[i - 2].To && !moves[i - 1].Worked, $"turned straight back at {moves[i - 1].To}");
    }

    /// Two sources alike but for the walk: it goes to the nearer.
    [Fact]
    public void OfTwoEqualSources_ItGoesToTheNearer()
    {
        var (w, npc) = Alone(5);
        var m = Map.Of(w);
        var camp = m.Camp;
        var forage = new ForagePool("forage", 1.0, Pools.MixedForest);
        var near = Enumerable.Range(1, 6).Select(d => new TileId(camp.X + d, camp.Y)).ToList();
        Assert.All(near, t => Assert.True(m.TerrainAt(t).IsPassable()));
        w.PoolCache = [];
        for (int dx = -13; dx <= 13; dx++)
            for (int dy = -13; dy <= 13; dy++)
                if (m.InBounds(new TileId(camp.X + dx, camp.Y + dy))) w.PoolCache[new TileId(camp.X + dx, camp.Y + dy)] = [];
        w.PoolCache[near[1]] = [forage];
        w.PoolCache[near[4]] = [forage];
        TestKit.Give(Items.PileAt(w, camp), Resource.Birch, 40); // warmth seen to: food is the Need, and its sources are the forage
        TestKit.Give(Items.PileAt(w, camp), Resource.Tinder, 1);
        npc.Body.CaloriesKcal = 0;
        var (a, _) = Decide(w, npc);
        output.WriteLine($"{a}");
        Assert.Equal(Direction.East, Assert.IsType<Travel>(a).Dir);
        Assert.Equal(Stat.Food, MindOf(npc).Plan?.Stat);
        Assert.Equal(near[1], MindOf(npc).Plan?.At);
    }

    // ---- water from the shared planner ----

    /// After two days' rain on the plain, a thirsty NPC with nothing in its pack drinks from the pond it stands in.
    [Fact]
    public void AfterRain_ItDrinksFromAnOpenPond()
    {
        var w = PlannerTests.Strip(sky: new WeatherSample(0, WeatherCondition.Rainy, 12, 0.1, 0.8, 0.8, WindDirection.North));
        Sim.Advance(w, 2 * 1440);
        var at = new TileId(20, 1);
        Assert.True(Ground.Underfoot(w, at).DrinkableWaterM >= Work.MinStandingWaterM, "no pond stood on the plain");
        var npc = w.Agent(Npc.Spawn(w, at));
        TestKit.Remove(MindOf(npc).Pack, Resource.Water);
        npc.Body.HydrationMl = 0.3 * Body.MaxHydrationMl;
        double before = npc.Body.HydrationPct;
        Sim.Advance(w, 60);
        Assert.True(npc.Body.HydrationPct > before + 0.2, $"hydration {before:F2} -> {npc.Body.HydrationPct:F2}");
    }

    /// Remembering a pond fourteen tiles east, out of sight, that the world does not hold, a thirsty NPC sets out for it:
    /// it acts on its memory, not on the world.
    [Fact]
    public void AWrongMemoryOfAPond_IsWhatItActsOn()
    {
        var w = PlannerTests.Strip();
        var (start, camp, pond) = (new TileId(2, 2), new TileId(12, 2), new TileId(16, 2));
        var npc = w.Agent(Npc.Spawn(w, start, camp));
        var mind = MindOf(npc);
        TestKit.Remove(mind.Pack, Resource.Water);
        npc.Body.HydrationMl = 0.3 * Body.MaxHydrationMl;
        Assert.True(MapGen.Manhattan(start, pond) > Sight.OpenGroundTiles, "the pond is in sight");
        Assert.Equal(0, Ground.Underfoot(w, pond).DrinkableWaterM);
        var explored = mind.Memory.Explored = new GroundMemory(PlannerTests.Width, 5);
        for (int x = 0; x < PlannerTests.Width; x++)
            for (int y = 1; y <= 3; y++)
            {
                var t = new TileId(x, y);
                var s = SeenSurface.Of(Ground.Underfoot(w, t));
                explored.Remember(t, t == pond ? s with { WaterDepthM = 0.1, Iced = false } : s, w.Minute);
            }
        var (a, _) = Npc.Decide(w, npc, []);
        output.WriteLine($"{a}, plan {mind.Plan}");
        Assert.Equal(Direction.East, Assert.IsType<Travel>(a).Dir);
        Assert.Equal(pond, mind.Plan?.At);
    }

    // ---- interrupts and danger ----

    /// A predator it has just seen next to it, and no fire to hold: it steps away from it.
    [Fact]
    public void APredatorSeenNextToIt_ItStepsAway()
    {
        var w = TestWorlds.New(17);
        var camp = Map.Of(w).Camp;
        var start = new TileId(camp.X + 1, camp.Y);
        var npc = Npc.Spawn(w, start, camp);
        var wolfTile = new TileId(start.X + 1, start.Y);
        Agents.SpawnGroup(w, Species.Wolf, wolfTile, 3);
        var (a, _) = Npc.Decide(w, w.Agent(npc), []);
        var travel = Assert.IsType<Travel>(a);
        Assert.True(MapGen.Manhattan(Map.Step(start, travel.Dir), wolfTile) > 1, $"ran {travel.Dir}, toward the wolves");
    }

    /// Away from camp and cold, it judges warmth by the core it will have on reaching camp, not the core it has now.
    [Fact]
    public void AwayInTheCold_ItJudgesWarmthByTheCoreItWillHaveAtCamp()
    {
        var w = TestWorlds.New(17);
        var camp = Map.Of(w).Camp;
        var npc = Npc.Spawn(w, new TileId(camp.X, camp.Y - 3), camp);
        var b = w.Agent(npc).Body;
        b.Worn.Clear();
        b.CoreTempF = 97.7;
        Npc.Decide(w, w.Agent(npc), []);
        Assert.Equal(Stat.Warmth, MindOf(w.Agent(npc)).Plan?.Stat);
    }

    // ---- a day lived on the scheduler ----

    /// A day and a night by a lit fire with a predator next door: it holds the fire and sleeps there.
    [Fact]
    [Trait("Category", TestCategories.LongRun)]
    public void BesideALitFire_WithAPredatorNextDoor_ItSleeps()
    {
        var w = TestWorlds.New(17);
        var camp = Map.Of(w).Camp;
        var npc = Npc.Spawn(w, camp);
        var next = MapGen.Neighbors(camp).First(t => Map.Blocked(Map.Of(w), camp, t) == null);
        Agents.Spawn(w, Species.CaveBear, next);
        TestKit.Give(Items.PileAt(w, camp), Resource.Birch, 40);
        Pit(w, camp, Resource.Kindling, 3);
        bool slept = false;
        while (w.Minute < 1440 && Body.IsAlive(w.Agent(npc).Body))
        {
            Sim.Advance(w, 1);
            slept |= w.Agent(npc).Doing?.Activity is Sleep;
        }
        Assert.True(slept, "a day and a night by its fire and it never slept");
    }

    /// Memory is one entry per tile, however often it comes back.
    [Fact]
    [Trait("Category", TestCategories.LongRun)]
    public void Memory_IsOnePlacePerTile()
    {
        var (w, npc) = AtCamp(17);
        var log = NpcDriver.Live(w, npc, 1440L);
        var mind = MindOf(w.Agent(npc));
        var stoodOn = log.OfType<Moved>().Where(m => m.Who == npc).Select(m => m.To).Append(Map.Of(w).Camp).ToHashSet();
        Assert.Equal(stoodOn.OrderBy(t => t), mind.Memory.Places.Where(p => p.Value.Visited).Select(p => p.Key));
    }

    /// An NPC asleep where it dropped from exhaustion sleeps it out, its mind not asked, then decides again.
    [Fact]
    public void AnNpcAsleepWhereItDropped_SleepsItOut_ThenDecidesAgain()
    {
        var (w, npc) = AtCamp(17);
        Sim.Advance(w, 60);
        var agent = w.Agent(npc);
        agent.Doing = null;
        agent.Body.EnergyMinutes = 0;
        Effects.Set(agent.Body, EffectKind.Asleep, 40 / 90.0);
        var events = new List<WorldEvent>();
        for (int m = 0; m < 40; m++)
        {
            events.AddRange(Sim.Advance(w, 1));
            Assert.True(agent.Doing is null or { Activity: Sleep }, $"minute {m}: {agent.Doing?.Activity}");
        }
        Assert.False(Body.IsAsleep(agent.Body));
        Assert.Equal(80, agent.Body.EnergyMinutes, 9);
        Assert.DoesNotContain(events, e => e is PlanRefused r && r.Who == npc);
    }

    /// An NPC asleep with a waking activity still under way does not walk on asleep: it stops before any of it runs.
    [Fact]
    public void AnNpcAsleepMidActivity_StopsBeforeTheActivityRuns()
    {
        var (w, npc) = AtCamp(17);
        var agent = w.Agent(npc);
        var dir = Enum.GetValues<Direction>().First(d => Sim.Refusal(w, new Travel(npc, d)) == null);
        Sim.Start(w, new Travel(npc, dir), 600, [], out var doing);
        agent.Doing = doing;
        var tile = agent.Tile;
        agent.Body.EnergyMinutes = 0;
        Effects.Set(agent.Body, EffectKind.Asleep, 40 / 90.0);
        int elapsed = agent.Crossing!.ElapsedMinutes;
        var crossing = agent.Crossing;
        Sim.Advance(w, 1);
        Assert.Equal(tile, agent.Tile);
        Assert.Equal(elapsed, crossing.ElapsedMinutes);
        Assert.Null(agent.Doing);
    }

    /// Woken from an exhaustion sleep by a blow, it gets up and decides again.
    [Fact]
    public void AnNpcWokenByABlow_DecidesAgain()
    {
        var (w, npc) = AtCamp(17);
        Sim.Advance(w, 60);
        var agent = w.Agent(npc);
        agent.Doing = null;
        agent.Body.EnergyMinutes = 800;
        Effects.Set(agent.Body, EffectKind.Asleep, 60 / 90.0);
        Sim.Advance(w, 2);
        Body.Damage(agent.Body, new DamageInfo(0.3, DamageType.Blunt, BodyTarget.LeftArm), Rng.ForSystem(1, "test"));
        Sim.Advance(w, 2);
        Assert.False(Body.IsAsleep(agent.Body));
    }

    // ---- determinism ----

    [Trait("Category", TestCategories.Slow)]
    [Fact]
    public void SameSeed_SameWorld()
    {
        string Run(int seed) { var (w, npc) = AtCamp(seed); NpcDriver.Live(w, npc, 2 * 1440L); return Save.ToJson(w); }
        Assert.Equal(Run(17), Run(17));
        Assert.NotEqual(Run(17), Run(18));
    }

    [Fact]
    [Trait("Category", TestCategories.LongRun)]
    public void SaveAndLoadMidRun_MatchesAStraightRun()
    {
        var (straight, npc) = AtCamp(17);
        NpcDriver.Live(straight, npc, 1440L + 437);
        NpcDriver.Live(straight, npc, 2 * 1440L);

        var (half, _) = AtCamp(17);
        NpcDriver.Live(half, npc, 1440L + 437);
        var resumed = Save.FromJson(Save.ToJson(half));
        Assert.Equal(Save.ToJson(half), Save.ToJson(resumed));
        NpcDriver.Live(resumed, npc, 2 * 1440L);
        Assert.Equal(Save.ToJson(straight), Save.ToJson(resumed));
    }

    /// Catching every fire up after each minute, rather than when someone looks, changes nothing the NPC does.
    [Trait("Category", TestCategories.Slow)]
    [Fact]
    public void EagerAndLazyFireCatchUp_Agree()
    {
        List<string> Run(bool look)
        {
            var (w, npc) = AtCamp(17);
            var did = new List<string>();
            while (w.Minute < 4 * 1440L && Body.IsAlive(w.Agent(npc).Body))
            {
                Sim.Advance(w, 1);
                var a = w.Agent(npc);
                did.Add($"{w.Minute} {a.Tile} {a.Doing?.Activity} {MindOf(a).Plan?.Stat} {MindOf(a).Camp}");
                if (look) foreach (var id in w.Fires.Keys.ToList()) if (w.Fires.ContainsKey(id)) Fire.CatchUp(w, id, []);
            }
            return did;
        }
        var (unseen, seen) = (Run(false), Run(true));
        int first = Enumerable.Range(0, Math.Min(unseen.Count, seen.Count)).FirstOrDefault(i => unseen[i] != seen[i], -1);
        Assert.True(first < 0 && unseen.Count == seen.Count, first < 0 ? "one run lived longer" : $"first difference: {unseen[first]} | {seen[first]}");
    }

    // ---- the fat it is used to ----

    [Fact]
    public void TargetFat_AdaptsDownOverALeanWeek_NeverBelowTheFloor()
    {
        var body = BodyState.BaselineHuman();
        var mind = new NpcMind { TargetFatKg = body.FatKg };
        body.FatKg = 7.0;
        Npc.AdaptTargetFat(mind, body, 7 * 1440);
        Assert.InRange(mind.TargetFatKg, 7.0, 7.3);
        body.FatKg = 5.0;
        Npc.AdaptTargetFat(mind, body, 14 * 1440);
        Assert.Equal(Priorities.FatFloorKg(body), mind.TargetFatKg, 9);
    }

    [Fact]
    public void TargetFat_OneLongUpdate_IsManyShortOnes()
    {
        var body = BodyState.BaselineHuman();
        body.FatKg = 9.5;
        var once = new NpcMind { TargetFatKg = 10.5 };
        var often = new NpcMind { TargetFatKg = 10.5 };
        Npc.AdaptTargetFat(once, body, 3000);
        for (long m = 1; m <= 3000; m++) Npc.AdaptTargetFat(often, body, m);
        Assert.Equal(once.TargetFatKg, often.TargetFatKg, 9);
    }
}
