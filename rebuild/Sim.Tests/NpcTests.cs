using Survival;
using Xunit;
using Xunit.Abstractions;

/// Drives NPCs by their own minds, on the world's scheduler: the world lives on and every NPC
/// chooses its own activities (Minds.Decide) as each one ends.
static class NpcDriver
{
    /// Lives the world on until the minute. Returns what happened. A choice refused more than a
    /// few times running is a bug in the mind, so it throws.
    public static List<WorldEvent> Live(World w, AgentId npc, long untilMinute)
    {
        var log = new List<WorldEvent>();
        int refusedInARow = 0;
        while (w.Minute < untilMinute && Body.IsAlive(w.Agent(npc).Body))
        {
            var events = Sim.Advance(w, 1);
            log.AddRange(events);
            var refused = events.OfType<PlanRefused>().FirstOrDefault(r => r.Who == npc);
            if (refused == null) { if (w.Agent(npc).Doing != null) refusedInARow = 0; }
            else if (++refusedInARow > 3) throw new InvalidOperationException($"{npc} keeps choosing a refused {refused.Activity}: {refused.Reason}");
        }
        return log;
    }
}

public class NpcTests(ITestOutputHelper output)
{
    /// The NPC benches' NPC: at camp, at a fixed bench id (NpcBench), so its numbers do not move when generation
    /// populates another herd kind.
    static (World World, AgentId Npc) AtCamp(int seed)
    {
        var w = World.New(seed);
        return (w, NpcBench.Spawn(w, Map.Of(w).Camp));
    }

    static double CampWoodKg(World w, AgentId npc)
    {
        var camp = ((NpcMind)w.Agent(npc).Mind).Camp;
        var stash = w.Tiles.GetValueOrDefault(camp)?.Stash;
        return new[] { Resource.Pine, Resource.Birch, Resource.Oak, Resource.Kindling }.Sum(r => stash != null ? Items.Have(stash, r) : 0);
    }

    /// Phase 3 gate: an NPC survives 14 days alone - no hand-feeding, nothing given past its spawn kit.
    [Trait("Category", TestCategories.Slow)]
    [Theory]
    [InlineData(17)]
    [InlineData(1)]
    [InlineData(42)]
    [InlineData(2024)]
    [InlineData(3)]
    [InlineData(777)] // no timber within sight of camp: it finds some and moves home to it
    public void FourteenDaysAlone_Survives(int seed)
    {
        var (w, npc) = AtCamp(seed);
        var log = NpcDriver.Live(w, npc, 14 * 1440L);
        var b = w.Agent(npc).Body;
        double ate = log.OfType<Ate>().Where(a => a.Who == npc).Sum(a => a.Kcal);
        output.WriteLine($"seed {seed}: alive {Body.IsAlive(b)} at minute {w.Minute}; vitality {Body.Vitality(b):0.00}, fat {b.FatKg:0.0} kg " +
                         $"(from {Body.BaselineWeightKg * Body.BaselineFatPct:0.0}), calories {b.CaloriesKcal:0}, hydration {b.HydrationPct:0.00}, " +
                         $"energy {b.EnergyPct:0.00}, core {b.CoreTempF:0.0}F; ate {ate:0} kcal, fires lit {log.OfType<FireLit>().Count(f => f.By == npc)}, " +
                         $"trees felled {log.OfType<TreeFelled>().Count()}, places known {((NpcMind)w.Agent(npc).Mind).Knows.Places.Count}, camp wood {CampWoodKg(w, npc):0} kg");
        Assert.True(Body.IsAlive(b), $"seed {seed}: the NPC died at minute {w.Minute}");
        Assert.True(w.Minute >= 14 * 1440L);
        Assert.True(ate > 0, "two weeks and it never ate");
        Assert.True(log.OfType<TreeFelled>().Any(), "two weeks of nights and it never felled a tree");
    }

    /// The same gate with the world's scheduler choosing when the NPC decides, as the game will run it.
    [Trait("Category", TestCategories.Slow)]
    [Fact]
    public void FourteenDaysAlone_OnTheWorldScheduler()
    {
        var (w, npc) = AtCamp(17);
        var log = new List<WorldEvent>();
        while (w.Minute < 14 * 1440L && Body.IsAlive(w.Agent(npc).Body)) log.AddRange(Sim.Advance(w, 60));
        var b = w.Agent(npc).Body;
        output.WriteLine($"scheduled: alive {Body.IsAlive(b)}, fat {b.FatKg:0.0} kg, vitality {Body.Vitality(b):0.00}, refused {log.OfType<PlanRefused>().Count(r => r.Who == npc)} choices");
        Assert.True(Body.IsAlive(b), $"died at minute {w.Minute}");
        // A refused choice costs a minute; it is a stale read now and then, never a habit.
        Assert.True(log.OfType<PlanRefused>().Count(r => r.Who == npc) < 20);
    }

    /// What an NPC does, minute by minute: its activity, its need, its camp and its moves. With look,
    /// every fire in the world is caught up after each minute: a pure observation.
    static List<string> Lived(World w, AgentId npc, long untilMinute, bool look)
    {
        var did = new List<string>();
        while (w.Minute < untilMinute && Body.IsAlive(w.Agent(npc).Body))
        {
            foreach (var e in Sim.Advance(w, 1))
                if (e is Moved { } m && m.Who == npc) did.Add($"{m.Minute} moved {m.To}");
            var mind = (NpcMind)w.Agent(npc).Mind;
            did.Add($"{w.Minute} {w.Agent(npc).Doing?.Activity} {mind.SeeingTo} camp {mind.Camp}");
            if (look) foreach (var id in w.Fires.Keys.ToList()) if (w.Fires.ContainsKey(id)) Fire.CatchUp(w, id, []);
        }
        return did;
    }

    static void AssertSameLives(List<string> unseen, List<string> seen)
    {
        int first = Enumerable.Range(0, Math.Min(unseen.Count, seen.Count)).FirstOrDefault(i => unseen[i] != seen[i], -1);
        Assert.True(first < 0 && unseen.Count == seen.Count, first < 0 ? "one run lived longer" : $"first difference: {unseen[first]} | {seen[first]}");
    }

    /// Eager and lazy fire catch-up agree: catching up every fire in the world after each minute, rather
    /// than when someone looks, changes nothing the NPC does. The seed-17 NPC never goes out of sight of
    /// camp in these ten days, so this does not reach a read of a fire out of sight; that guard is
    /// LookingAtAnAbandonedCampFire_ChangesNothingAnNpcDoes.
    [Trait("Category", TestCategories.Slow)]
    [Fact]
    public void EagerAndLazyFireCatchUp_Agree()
    {
        List<string> Run(bool look) { var (w, npc) = AtCamp(17); return Lived(w, npc, 10 * 1440L, look); }
        AssertSameLives(Run(false), Run(true));
    }

    /// The same, where it used to matter (review: the camp's fuel and the fled-camp check read the camp
    /// pit from any distance, as whoever last looked left it). It fled a bear that settled beside camp,
    /// leaving the fire burning; out of sight, the fire died. Whether anyone looks at that fire must not
    /// decide whether it gives up its camp: it goes by the fire it saw.
    [Fact]
    public void LookingAtAnAbandonedCampFire_ChangesNothingAnNpcDoes()
    {
        List<string> Run(bool look)
        {
            var w = World.New(17);
            w.FixedWeather = new WeatherSample(0, WeatherCondition.Cloudy, -5, 0.1, 0, 0.8, WindDirection.North);
            w.Agents.Remove(AgentId.Player); // nobody at camp to keep its fire caught up
            var camp = Map.Of(w).Camp;
            var pit = new FireId(w.NextFireId++);
            w.Fires[pit] = new FireState { Tile = camp, LastUpdatedMinute = 0, BurningKg = { [Resource.Birch] = 1 } };
            var phase = Fire.Phase(w.Fires[pit]);
            Sim.Advance(w, 5 * 60); // the fire burns out with nobody to see it
            var far = Enumerable.Range(Sight.MaxTiles + 1, 10).Select(d => new TileId(camp.X + d, camp.Y))
                .First(t => Map.Of(w).TerrainAt(t).IsPassable());
            var npc = Npc.Spawn(w, far, camp);
            var mind = (NpcMind)w.Agent(npc).Mind;
            // It stood in camp at minute 0 with the fire burning, and fled the bear beside it a few minutes ago.
            mind.Knows.Places[camp] = new TileMemory { SeenMinute = 0, VisitedMinute = 0, Fire = phase, FireHours = 1.0 };
            (mind.ThreatTile, mind.ThreatMinute) = (new TileId(camp.X - 1, camp.Y), w.Minute - 5);
            return Lived(w, npc, w.Minute + 3 * 60, look);
        }
        AssertSameLives(Run(false), Run(true));
    }

    /// Review: on seed 17 (dying on day 14.8), 832 of 1,182 moves were there-and-back within ten minutes,
    /// the starving NPC walking between camp and the next tile because from each, the walk toward some far
    /// ground looked better than the step it had just taken. Walking out for food and straight back again,
    /// having done nothing there, never happens now. (Out for food and home because the walk tired or
    /// chilled it is another need doing its job, and still can: so is the trip budget, when the walk out
    /// proved colder than reckoned and the ground no longer fits it - Npc.ForageTripFits.)
    [Trait("Category", TestCategories.Slow)]
    [Fact]
    public void LookingForFood_ItNeverTurnsStraightBack()
    {
        var (w, npc) = AtCamp(17);
        var back = new List<string>();
        (long Minute, TileId From, Stat? SeeingTo, bool Unaffordable)? last = null;
        bool did = false;
        int budgeted = 0;
        while (w.Minute < 16 * 1440L && Body.IsAlive(w.Agent(npc).Body))
        {
            var need = ((NpcMind)w.Agent(npc).Mind).SeeingTo;
            var goal = ((NpcMind)w.Agent(npc).Mind).ForageGoal;
            foreach (var e in Sim.Advance(w, 1))
            {
                if (e is Moved m && m.Who == npc)
                {
                    if (last is { } l && m.To == l.From && m.Minute - l.Minute <= 60 && !did && l.SeeingTo == Stat.Food && need == Stat.Food)
                    {
                        if (l.Unaffordable) budgeted++;
                        else back.Add($"{l.From}->{m.From}->{m.To} at {l.Minute}-{m.Minute}");
                    }
                    // Arrived with ground in mind: does the stint there still fit, from here?
                    (last, did) = ((m.Minute, m.From, need, goal is TileId g && g != m.To && !Npc.ForageTripFits(w, w.Agent(npc), g)), false);
                }
                else if (e is Stashed or Unstashed or Gathered or Ate or Drank or FuelAdded or FireLit or FireStartFailed or Butchered or TreeFelled or Dropped
                         && e.ToString()!.Contains(npc.ToString() + ",")) did = true;
            }
        }
        output.WriteLine($"turned back by the trip budget: {budgeted}");
        Assert.True(back.Count == 0, $"{back.Count} trips out for food and straight back: {string.Join("; ", back.Take(5))}");
        Assert.True(budgeted <= 3, $"turned back by the trip budget {budgeted} times: its reckoning setting out is too far off");
    }

    /// Old behavior: banking the fire before going out took wood off the stash, and the next choice
    /// put it back on the stash before it went on the fire, two minutes a round, for most of an hour
    /// (seed 17: 236 rounds in a run).
    [Fact]
    public void BankingTheFire_PutsTheWoodOnIt_NotBackOnTheStash()
    {
        var (w, npc) = AtCamp(17);
        var log = NpcDriver.Live(w, npc, 8 * 1440L);
        var handled = log.Where(e => e is Stashed s && s.Who == npc || e is Unstashed u && u.Who == npc).ToList();
        var backAgain = handled.Zip(handled.Skip(1)).Where(p => p.First is Unstashed u && p.Second is Stashed s
                                                               && u.Resource == s.Resource && s.Minute - u.Minute <= 1).ToList();
        Assert.True(backAgain.Count == 0, $"{backAgain.Count} times off the stash and straight back on, first {backAgain.FirstOrDefault().First}");
    }

    [Trait("Category", TestCategories.Slow)]
    [Fact]
    public void SameSeed_SameWorld()
    {
        string Run(int seed) { var (w, npc) = AtCamp(seed); NpcDriver.Live(w, npc, 2 * 1440L); return Save.ToJson(w); }
        Assert.Equal(Run(17), Run(17));
        Assert.NotEqual(Run(17), Run(18));
    }

    [Fact]
    public void SaveAndLoadMidRun_MatchesAStraightRun()
    {
        // Both stop at the same minute: where a step is cut short changes what the NPC does next.
        var (straight, npc) = AtCamp(17);
        NpcDriver.Live(straight, npc, 1440L + 437);
        NpcDriver.Live(straight, npc, 2 * 1440L);

        var (half, _) = AtCamp(17);
        NpcDriver.Live(half, npc, 1440L + 437); // mid-afternoon, mid-whatever it is doing
        var resumed = Save.FromJson(Save.ToJson(half));
        Assert.Equal(Save.ToJson(half), Save.ToJson(resumed));
        NpcDriver.Live(resumed, npc, 2 * 1440L);
        Assert.Equal(Save.ToJson(straight), Save.ToJson(resumed));
    }

    /// Wolves next door: it does not wait to be eaten. One tile off, it always runs; on its own tile
    /// it fights or runs.
    [Fact]
    public void WolvesNextDoor_Flees()
    {
        var w = World.New(17);
        var camp = Map.Of(w).Camp;
        var start = new TileId(camp.X + 1, camp.Y); // forest east of camp: away from its fire
        var npc = Npc.Spawn(w, start, camp);
        var wolfTile = new TileId(start.X + 1, start.Y);
        Agents.Spawn(w, Species.Wolf, wolfTile, 3);

        var (activity, _) = Npc.Decide(w, w.Agent(npc), []);
        var travel = Assert.IsType<Travel>(activity);
        Assert.Equal(Pace.Quick, travel.Pace);
        Assert.True(MapGen.Manhattan(Map.Step(start, travel.Dir), wolfTile) > 1, $"ran {travel.Dir}, toward the wolves");
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(1.0)]
    public void WolvesOnItsTile_FightsOrFlees(double boldness)
    {
        var w = World.New(17);
        var camp = Map.Of(w).Camp;
        var start = new TileId(camp.X + 1, camp.Y);
        var npc = Npc.Spawn(w, start, camp, boldness);
        Agents.Spawn(w, Species.Wolf, start, 2);

        var events = new List<WorldEvent>();
        var (activity, _) = Npc.Decide(w, w.Agent(npc), events);
        bool fought = events.OfType<FightEnded>().Any(f => f.Agent == npc);
        bool fled = activity is Travel { Pace: Pace.Quick };
        output.WriteLine($"boldness {boldness}: {(fought ? "fought: " + events.OfType<FightEnded>().First(f => f.Agent == npc).Result : fled ? "fled" : activity.ToString())}");
        Assert.True(fought || fled, $"with wolves on its tile it chose {activity}");
    }

    /// The player is fighting wolves; a bold, armed NPC on the same tile joins the player's side.
    [Fact]
    public void PlayerFightingWolves_BoldNpcJoinsIn()
    {
        var w = World.New(17);
        var tile = w.Agent(AgentId.Player).Tile;
        var npc = Npc.Spawn(w, tile, boldness: 1.0);
        var wolves = Agents.Spawn(w, Species.Wolf, tile, 1);
        var enc = Combat.Create(w, [new Fighter(AgentId.Player)], [new Fighter(wolves)], tile, 8, Awareness.Engaged, Awareness.Engaged);

        Npc.Decide(w, w.Agent(npc), []);
        var unit = Assert.Single(enc.Units, u => u.Agent == npc);
        Assert.Equal(0, unit.Team);
        Assert.NotNull(unit.Weapon); // it brought the axe
    }

    /// An activity cut short is not finished: the tree came down an hour in, and with its pack full of
    /// logs it re-plans, and heads home.
    [Fact]
    public void TreeFelledMidChop_ReplansAndCarriesWoodHome()
    {
        var w = World.New(17);
        var camp = Map.Of(w).Camp;
        var wood = new TileId(camp.X + 1, camp.Y); // forest
        var npc = Npc.Spawn(w, wood, camp);

        StepResult? cut = null;
        for (int i = 0; i < 6 && cut?.InterruptedReason != "tree felled"; i++)
        {
            var (activity, minutes) = Npc.Decide(w, w.Agent(npc), []);
            Assert.IsType<Gather>(activity);
            cut = Sim.Step(w, activity, minutes);
        }
        Assert.Equal("tree felled", cut!.InterruptedReason);
        Assert.True(cut.MinutesElapsed < 60, "the step the tree fell in should have been cut short");

        var (next, _) = Npc.Decide(w, w.Agent(npc), []);
        var home = Assert.IsType<Travel>(next);
        Assert.Equal(camp, Map.Step(wood, home.Dir));
    }

    /// Starving, with wood enough at camp and a caribou on its tile in daylight: it goes after it. (Which herds
    /// it takes on, wanting food or starving: NpcWarmingWalkTests.WhichHerdsALoneNpcHunts.)
    [Fact]
    public void HungryWithPreyHere_Hunts()
    {
        var w = World.New(17);
        var camp = Map.Of(w).Camp;
        var npc = Npc.Spawn(w, camp);
        Items.Add(Items.StashAt(w, camp), Resource.Birch, 40); // so firewood is not what it worries about
        w.Agent(npc).Body.CaloriesKcal = 100;
        w.Agent(npc).Body.FatKg -= 1.5; // well short of the fat it is used to: food is critical (Npc.TargetFatKg)
        var caribou = Agents.Spawn(w, Species.Caribou, camp, 1);

        var events = new List<WorldEvent>();
        Npc.Decide(w, w.Agent(npc), events);
        var ended = events.OfType<FightEnded>().FirstOrDefault(f => f.Agent == npc);
        Assert.NotNull(ended);
        output.WriteLine($"hunt: {ended.Result} after {ended.Rounds} rounds; carcass {(w.Tiles.GetValueOrDefault(camp)?.Carcasses?.Count ?? 0)}");
    }

    /// Old ResourceMemory appended a camp's fire to its fire list on every visit, so the list grew
    /// without bound (bug, fixed): memory is one entry per tile, however often it is visited.
    [Fact]
    public void Memory_IsOnePlacePerTile()
    {
        var (w, npc) = AtCamp(17);
        var log = NpcDriver.Live(w, npc, 1440L);
        var mind = (NpcMind)w.Agent(npc).Mind;
        var stoodOn = log.OfType<Moved>().Where(m => m.Who == npc).Select(m => m.To).Append(Map.Of(w).Camp).ToHashSet();
        Assert.True(log.OfType<Moved>().Count(m => m.Who == npc && m.To == Map.Of(w).Camp) > 1, "it should have come home more than once");
        Assert.Equal(stoodOn.OrderBy(t => t), mind.Knows.Places.Where(p => p.Value.Visited).Select(p => p.Key));
    }

    // ---- food by fat (Npc.TargetFatKg) ----

    /// An NPC at camp in the morning, short of the fat it is used to by about shortfallKg, with this
    /// much birch on the stash.
    static (World World, NpcMind Mind) ShortOfFat(double shortfallKg, double stashBirchKg)
    {
        var w = World.New(17);
        var camp = Map.Of(w).Camp;
        var npc = Npc.Spawn(w, camp);
        TestKit.Lean(w, npc);
        if (stashBirchKg > 0) Items.Add(Items.StashAt(w, camp), Resource.Birch, stashBirchKg);
        var mind = (NpcMind)w.Agent(npc).Mind;
        w.Agent(npc).Body.FatKg = mind.TargetFatKg - shortfallKg;
        return (w, mind);
    }

    static Activity DecideFor(World w, NpcMind mind) =>
        Npc.Decide(w, w.Agents.Values.Single(a => a.Mind == mind), []).Activity;

    [Fact]
    public void TonightsFuelBanked_WantedFood_GoesForFood()
    {
        var (w, mind) = ShortOfFat(0.5, 40);
        var a = DecideFor(w, mind);
        output.WriteLine($"{a}, need {mind.SeeingTo}");
        Assert.Equal(Stat.Food, mind.SeeingTo);
    }

    [Fact]
    public void TonightsFuelNotBanked_WantedFood_GoesForWood()
    {
        var (w, mind) = ShortOfFat(0.5, 0);
        var a = DecideFor(w, mind);
        output.WriteLine($"{a}, need {mind.SeeingTo}");
        Assert.Null(mind.SeeingTo); // the wood plan sees to no need
        Assert.False(a is Gather { Focus: ForageFocus.Food });
    }

    /// A lean week: it gets used to the leaner body, but never below the floor where leanness costs.
    [Fact]
    public void TargetFat_AdaptsDownOverALeanWeek_NeverBelowTheFloor()
    {
        var body = BodyState.BaselineHuman();
        var mind = new NpcMind { TargetFatKg = body.FatKg };
        body.FatKg = 7.0;
        Npc.AdaptTargetFat(mind, body, 7 * 1440);
        output.WriteLine($"after a week at 7 kg: target {mind.TargetFatKg:0.000}");
        Assert.InRange(mind.TargetFatKg, 7.0, 7.3); // 7 + 1.16 * 2^(-7/3), from the 68 kg man's 8.16

        body.FatKg = 5.0;
        Npc.AdaptTargetFat(mind, body, 14 * 1440);
        Assert.Equal(Npc.FatFloorPct * body.WeightKg, mind.TargetFatKg, 9);
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

    // ---- predators by camp ----

    static (World World, AgentId Npc, AgentId Bear) BearNextToCamp()
    {
        var w = World.New(17);
        var camp = Map.Of(w).Camp;
        var npc = Npc.Spawn(w, camp);
        var next = MapGen.Neighbors(camp).First(t => Map.Blocked(Map.Of(w), camp, t) == null);
        return (w, npc, Agents.Spawn(w, Species.CaveBear, next, 1, [next]));
    }

    /// Old: a predator next to camp sent it one tile off, then home, then off again - 76 times, until it
    /// froze at minute 537 (bug, fixed). It runs clear and makes its camp where it ran to.
    [Fact]
    public void BearNextToColdCamp_RunsClearAndLives()
    {
        var (w, npc, bear) = BearNextToCamp();
        var log = NpcDriver.Live(w, npc, 1440);
        int moves = log.OfType<Moved>().Count(m => m.Who == npc);
        var mind = (NpcMind)w.Agent(npc).Mind;
        output.WriteLine($"alive {Body.IsAlive(w.Agent(npc).Body)}, moved {moves} times, camp {mind.Camp}, bear {w.Agents.GetValueOrDefault(bear)?.Tile}");
        Assert.True(Body.IsAlive(w.Agent(npc).Body), $"died at minute {w.Minute}");
        Assert.True(moves < 40, $"moved {moves} times in a day");
    }

    /// Old: by a lit fire with a predator next door it fed the fire and rested ten minutes, forever,
    /// never sleeping (bug, fixed). The fire is a place to hold; it sleeps there.
    [Fact]
    public void BearNextToLitCamp_HoldsTheFireAndSleeps()
    {
        var (w, npc, _) = BearNextToCamp();
        var camp = Map.Of(w).Camp;
        Items.Add(Items.StashAt(w, camp), Resource.Birch, 40);
        var pit = new FireState { Tile = camp, LastUpdatedMinute = w.Minute, Pit = PitType.Open };
        Fire.AddFuel(pit, Resource.Kindling, 4);
        Fire.IgniteAll(pit);
        w.Fires[new FireId(w.NextFireId++)] = pit;
        bool slept = false;
        while (w.Minute < 1440 && Body.IsAlive(w.Agent(npc).Body))
        {
            Sim.Advance(w, 1);
            slept |= w.Agent(npc).Doing?.Activity is Sleep;
        }
        Assert.True(Body.IsAlive(w.Agent(npc).Body), $"died at minute {w.Minute}");
        Assert.True(slept, "a day and a night by its fire and it never slept");
    }

    // ---- cold, fuel and water (found running the gate to 30 days) ----

    /// Old: away from camp it turned for home at a fixed 97.5F, however long and cold the walk; a
    /// two-tile trip through deep snow came home hypothermic (bug, fixed). It judges the core it will
    /// have when it gets there.
    [Fact]
    public void AwayInTheCold_TurnsHomeByTheCoreItWillHaveThere()
    {
        var w = World.New(17);
        var camp = Map.Of(w).Camp;
        var npc = Npc.Spawn(w, new TileId(camp.X, camp.Y - 3), camp);
        var b = w.Agent(npc).Body;
        b.Worn.Clear(); // no furs: the open air takes heat fast
        b.CoreTempF = 97.7;
        var mind = (NpcMind)w.Agent(npc).Mind;
        var (a, _) = Npc.Decide(w, w.Agent(npc), []);
        output.WriteLine($"{a}, need {mind.SeeingTo}");
        Assert.Equal(Stat.Warmth, mind.SeeingTo);
    }

    /// Old: cold by a fire with no wood left to feed it, it rested until the fire was nearly out, then
    /// groped the bare camp ground for sticks (bug, fixed). By day it goes for wood while the fire lasts:
    /// once warm enough to come home warm (the trip budget), not while it still wants warming, when a
    /// walk out would bring it home colder than it wants to be (seed 7, day 22: out at 98.1, home at 93.4).
    /// Then it warms at the fire, not groping the camp ground (old, this rebuild: the wood plan's fallback,
    /// half an hour's fuel forage here, was taken for the walk it could not make).
    [Theory]
    // 97.6: warm enough for a trip that brings it home above Npc.TripHomeCoreF (96.8F, inside warmth's Need: a wood
    // trip is what it takes to get through the day). Held to the Need line itself, 97.5F, it was 98.4 - and seed 17
    // froze in a blizzard beside a dying fire, no wood trip ever fitting.
    [InlineData(97.6, true)]
    [InlineData(96.5, false)]
    public void ColdByAFireWithNoWoodLeft_GoesForWoodWhileItLasts(double coreF, bool goes)
    {
        var w = World.New(17);
        var camp = Map.Of(w).Camp;
        var npc = Npc.Spawn(w, camp);
        var pit = new FireState { Tile = camp, LastUpdatedMinute = w.Minute, Pit = PitType.Open };
        Fire.AddFuel(pit, Resource.Kindling, 2.5);
        Fire.IgniteAll(pit);
        w.Fires[new FireId(w.NextFireId++)] = pit;
        ((NpcMind)w.Agent(npc).Mind).Pack.Kg[Resource.Kindling] = 0.6; // its reserve: nothing to feed the fire
        w.Agent(npc).Body.CoreTempF = coreF;
        var (a, _) = Npc.Decide(w, w.Agent(npc), []);
        output.WriteLine($"{a}");
        if (goes) Assert.True(a is Travel or Gather { Focus: not ForageFocus.Food } && a is not Gather { Pool: 0 }, $"chose {a}");
        else Assert.True(a is Rest or MeltSnow or Cook, $"chose {a}, not warming at the fire");
    }

    /// Old: freezing and parched, it fussed with its fire and died of thirst carrying three litres
    /// (bug, fixed). A drink from the pack is never the wrong thing when water is critical.
    [Fact]
    public void FreezingAndParched_DrinksWhatItCarries()
    {
        var w = World.New(17);
        var npc = Npc.Spawn(w, Map.Of(w).Camp);
        var b = w.Agent(npc).Body;
        Items.Add(((NpcMind)w.Agent(npc).Mind).Pack, Resource.Water, 1);
        b.HydrationMl = 0.1 * Body.MaxHydrationMl;
        b.CoreTempF = 95.0;
        var (a, _) = Npc.Decide(w, w.Agent(npc), []);
        Assert.IsType<Drink>(a);
    }

    /// Critical food does not outrank critical rest: foraging on at no energy killed it (seed 1, day 28).
    [Fact]
    public void Starving_AndSpent_Sleeps()
    {
        var (w, mind) = ShortOfFat(3.0, 40);
        var agent = w.Agents.Values.Single(a => a.Mind == mind);
        agent.Body.EnergyMinutes = 0.05 * Body.MaxEnergyMinutes;
        var (a, _) = Npc.Decide(w, agent, []);
        Assert.True(a is Sleep or AddFuel or Unstash or StartFire, $"chose {a}"); // bed: the fire banked, then sleep
        Assert.Equal(Stat.Rest, mind.SeeingTo);
    }

    // ---- foraging ----

    /// A food search weighs fresh ground against the walk at today's going. On a forest tile worked to
    /// about two thirds of the best fresh ground next door, it moves on over bare ground; with
    /// everything buried in drifts the walk costs more than the difference and it works what is here
    /// (old: two minutes a tile whatever the snow, so it set off through drifts for fresh ground and
    /// froze walking home).
    [Fact]
    public void FoodSearch_PricesTheWalkToFreshGround_AtTodaysGoing()
    {
        (Activity Activity, double WalkMinutes) Decide(bool drifts)
        {
            var w = World.New(17);
            w.FixedWeather = new WeatherSample(0, WeatherCondition.Cloudy, -5, 0.1, 0, 0.8, WindDirection.North);
            var m = Map.Of(w);
            double Fresh(TileId t) => ((ForagePool)Pools.At(w, t)[0]).Density;
            // A forest tile among forest, richer than 0.7 of its best neighbour, so it can be worked down to 0.65 of it.
            var here = Enumerable.Range(-15, 31).SelectMany(dx => Enumerable.Range(-15, 31).Select(dy => new TileId(m.Camp.X + dx, m.Camp.Y + dy)))
                .Where(t => t != m.Camp && m.InBounds(t) && m.TerrainAt(t) == Terrain.Forest
                            && MapGen.Neighbors(t).All(n => n != m.Camp && m.InBounds(n) && m.TerrainAt(n) == Terrain.Forest && Map.Blocked(m, t, n) == null)
                            && Fresh(t) > 0.7 * MapGen.Neighbors(t).Max(Fresh))
                .OrderBy(t => MapGen.Manhattan(t, m.Camp)).ThenBy(t => t).First();
            double target = 0.65 * MapGen.Neighbors(here).Max(Fresh);
            w.Tiles[here] = new TileState { Pools = new() { [0] = new ForageState { HoursForaged = (Fresh(here) / target - 1) / 2 } } };
            if (drifts)
                foreach (var t in MapGen.Neighbors(here).Append(here))
                {
                    var sub = Ground.Substrate(m, t);
                    var g = Ground.Baseline(sub);
                    Ground.Advance(g, sub, 60 * 48, new SurfaceWeather(15, 0.2, 0, 5e-3 / 60, 0));
                    g.LastUpdatedMinute = 0;
                    (w.Tiles.TryGetValue(t, out var ts) ? ts : w.Tiles[t] = new TileState()).Ground = g;
                }
            var npc = Npc.Spawn(w, here);
            TestKit.Lean(w, npc);
            Items.Add(Items.StashAt(w, here), Resource.Birch, 40);
            var agent = w.Agent(npc);
            agent.Body.FatKg = ((NpcMind)agent.Mind).TargetFatKg - 0.5;
            var (a, _) = Npc.Decide(w, agent, []);
            return (a, Traversal.CrossingMinutes(w, agent.Body, here, MapGen.Neighbors(here).First(), Pace.Quick, Weather.At(w, 0)));
        }
        var bare = Decide(drifts: false);
        var deep = Decide(drifts: true);
        output.WriteLine($"bare: {bare}; drifts: {deep}");
        Assert.True(deep.WalkMinutes > bare.WalkMinutes * 3, "the drifts should make the walk slow");
        Assert.IsType<Travel>(bare.Activity);
        Assert.Equal(new Gather(deep.Activity.Who, 0, ForageFocus.Food), deep.Activity);
    }

    /// Review finding H1: a hungry, armed NPC on the tile where the player is fighting a bison took
    /// that bison for prey and called BeginHunt on it, which threw "already in another fight".
    [Fact]
    public void HungryNpc_DoesNotHuntPreyAlreadyInAFight()
    {
        var w = World.New(17);
        var camp = Map.Of(w).Camp;
        var npc = Npc.Spawn(w, camp);
        Items.Pack(w, npc).Gear.Add(Items.Make("wooden_spear"));
        var mind = (NpcMind)w.Agent(npc).Mind;
        Items.Add(Items.StashAt(w, camp), Resource.Birch, 60);
        Sim.Advance(w, 60);
        var tile = w.Agent(npc).Tile;
        var bison = Agents.Spawn(w, Species.Bison, tile, 3);
        w.Agent(AgentId.Player).Tile = tile;
        w.Agent(AgentId.Player).Members[0] = BodyState.BaselineHuman();
        Combat.Create(w, [new Fighter(AgentId.Player)], [new Fighter(bison, 0)], tile, 20, Awareness.Engaged, Awareness.Unaware);
        var body = w.Agent(npc).Body;
        w.Agent(npc).Doing = null;
        (body.CoreTempF, body.EnergyMinutes, body.HydrationMl, body.CaloriesKcal) = (98.6, Body.MaxEnergyMinutes, Body.MaxHydrationMl, 100);
        body.FatKg = mind.TargetFatKg - 1.5;
        var (a, _) = Npc.Decide(w, w.Agent(npc), []);
        output.WriteLine($"{a}, need {mind.SeeingTo}");
        Assert.Single(w.Encounters);
        Assert.DoesNotContain(w.Encounters[0].Units, u => u.Agent == npc);
    }

    // ---- asks review, second round ----

    static readonly WeatherSample Mild = new(0, WeatherCondition.Clear, 20, 0.0, 0, 1, WindDirection.North);

    static void NoHerds(World w)
    {
        foreach (var h in w.Agents.Values.Where(a => a.Mind is HerdMind).Select(a => a.Id).ToList()) w.Agents.Remove(h);
    }

    /// Review: in sight of camp but not on it, it read the camp pit's fuel exactly, from up to five
    /// tiles away. From a distance a fire's flame shows, not how much is on it: it reckons the hours
    /// from when it last stood beside it (TileMemory.FireHours), as it does out of sight.
    [Fact]
    public void InSightOfCamp_ItReckonsTheFireFromMemory_NotThePit()
    {
        var w = World.New(17);
        w.FixedWeather = Mild;
        var camp = Map.Of(w).Camp;
        var pit = new FireId(w.NextFireId++);
        w.Fires[pit] = new FireState { Tile = camp, LastUpdatedMinute = w.Minute, BurningKg = { [Resource.Birch] = 5 }, UnburnedKg = { [Resource.Birch] = 5 } };
        var near = MapGen.Neighbors(camp).SelectMany(MapGen.Neighbors).Where(t => MapGen.Manhattan(t, camp) == 2 && Map.Of(w).TerrainAt(t).IsPassable())
            .OrderBy(t => t).First();
        var npc = Npc.Spawn(w, near, camp);
        var agent = w.Agent(npc);
        var mind = (NpcMind)agent.Mind;
        mind.Pack.Kg.Remove(Resource.Kindling); // only the pit counts
        // It stood by the fire an hour ago, when it had two hours on it; someone has fed it since.
        Sim.Advance(w, 60);
        agent.Tile = near;
        mind.Knows.Places[camp] = new TileMemory { SeenMinute = w.Minute - 60, VisitedMinute = w.Minute - 60, Fire = FirePhase.Steady, FireHours = 2.0 };
        Knowledge.Observe(w, agent, []);
        Assert.True(Sight.InSight(w, agent, camp) && agent.Tile != camp);
        Assert.True(Fire.FuelHours(w.Fires[pit]) > 5, "the pit should hold far more than it remembers");
        Assert.Equal(1.0, Npc.BelievedCampFuelHours(w, agent), 6);
        // Standing beside it, it judges the pit as it is.
        agent.Tile = camp;
        Knowledge.Observe(w, agent, []);
        Assert.Equal(Fire.FuelHours(w.Fires[pit]), Npc.BelievedCampFuelHours(w, agent), 6);
    }

    /// An NPC at camp at this minute, short of food (wanted, not critical), with wood enough on the stash,
    /// no herds about, and a memory of the country in which every tile in its range is picked bare but
    /// one, five tiles out: a forage trip of the walk there, 45 minutes of work, and the walk home.
    static (World World, AgentId Npc, TileId Ground) ForagerAtCamp(long minute, WeatherSample? sky = null)
    {
        var w = World.New(17);
        w.FixedWeather = sky ?? Mild;
        NoHerds(w);
        w.Minute = minute;
        var m = Map.Of(w);
        var camp = m.Camp;
        var npc = Npc.Spawn(w, camp, boldness: 0.5);
        var mind = (NpcMind)w.Agent(npc).Mind;
        Items.Add(Items.StashAt(w, camp), Resource.Oak, 40);
        Items.Add(Items.StashAt(w, camp), Resource.Kindling, 2); // and the kindling to light it (Npc.Lit)
        w.Agent(npc).Body.FatKg = mind.TargetFatKg - 0.5;
        (w.Tiles.TryGetValue(camp, out var ts) ? ts : w.Tiles[camp] = new TileState()).Pools = new() { [0] = new ForageState { HoursForaged = 1000, LastForagedMinute = minute } };
        var near = Enumerable.Range(-6, 13).SelectMany(dx => Enumerable.Range(-6, 13).Select(dy => new TileId(camp.X + dx, camp.Y + dy)))
            .Where(t => m.InBounds(t) && MapGen.Manhattan(t, camp) <= 6).ToList();
        var ground = near.Where(t => MapGen.Manhattan(t, camp) == 5 && m.TerrainAt(t).IsPassable() && Pools.At(w, t) is [ForagePool { Density: > 0.2 }, ..])
            .OrderBy(t => t).First();
        foreach (var t in near)
            mind.Knows.Places[t] = new TileMemory
            {
                SeenMinute = minute, VisitedMinute = minute, ForageDensity = t == ground ? ((ForagePool)Pools.At(w, t)[0]).Density : 0,
                Stash = t == camp ? new() { [Resource.Oak] = 40, [Resource.Kindling] = 2 } : null,
            };
        return (w, npc, ground);
    }

    /// Minute of a day this many days after the start, at this hour.
    static long At(int day, int hour, int minute = 0) => day * 1440L + (hour - 9) * 60 + minute;

    /// Review: setting out was checked by its first crossing alone, against 19:00 whatever the season.
    /// The whole trip - out, the work, home - must be in by evening, or by sunset if that is sooner.
    [Fact]
    public void ATripOut_MustBeHomeByEvening_OrSunsetInWinter()
    {
        Activity Decided(long minute)
        {
            var (w, npc, ground) = ForagerAtCamp(minute);
            var (a, _) = Npc.Decide(w, w.Agent(npc), []);
            output.WriteLine($"{Weather.SeasonAt(minute)} {Weather.MinuteOfDay(minute) / 60.0:0.00}h (sunset {Weather.SunsetHour(minute)}): {a}");
            return a;
        }
        // Summer: at three the trip is in long before seven; a minute before six it would not be, though its first step would.
        Assert.IsType<Travel>(Decided(At(0, 15)));
        Assert.IsNotType<Travel>(Decided(At(0, 17, 59)));
        // Winter: the sun is down at four, so at three an hour's trip waits for the morning.
        int winterDay = 180; // late December
        Assert.Equal(Season.Winter, Weather.SeasonAt(At(winterDay, 15)));
        Assert.IsType<Travel>(Decided(At(winterDay, 11)));
        Assert.IsNotType<Travel>(Decided(At(winterDay, 15)));
    }

    /// Review: a plan the decision refused still left its mark. A forage trip turned down as too late
    /// kept the ground it would have walked to as ForageGoal, so the next choice walked on toward it.
    /// Weighing a plan changes nothing; only the plan started is kept.
    [Fact]
    public void ARefusedTrip_LeavesNoGoalBehind()
    {
        var (w, npc, _) = ForagerAtCamp(At(0, 17, 59));
        var mind = (NpcMind)w.Agent(npc).Mind;
        var (a, _) = Npc.Decide(w, w.Agent(npc), []);
        Assert.IsNotType<Travel>(a);
        Assert.Null(mind.ForageGoal);
        Assert.Null(mind.Heading);
    }

    /// Review: FindFood scored foraging by working out where it would go, which set ForageGoal even
    /// when hunting won. Hunting chosen, it has no forage ground in mind.
    [Fact]
    public void HuntingChosen_LeavesNoForageGoal()
    {
        var (w, npc, _) = ForagerAtCamp(At(0, 11));
        var mind = (NpcMind)w.Agent(npc).Mind;
        w.Agent(npc).Body.FatKg = mind.TargetFatKg - 1.5; // well short of fat: a caribou is worth a stalk (Npc.WouldHunt)
        var camp = mind.Camp;
        var next = MapGen.Neighbors(camp).Where(t => Map.Of(w).TerrainAt(t).IsPassable() && Map.Blocked(Map.Of(w), camp, t) == null).OrderBy(t => t).First();
        Agents.Spawn(w, Species.Caribou, next, 3);
        var events = new List<WorldEvent>();
        var (a, _) = Npc.Decide(w, w.Agent(npc), events);
        output.WriteLine($"{a}");
        var step = Assert.IsType<Travel>(a);
        Assert.Equal(next, Map.Step(camp, step.Dir)); // after the caribou, not the forage ground
        Assert.Null(mind.ForageGoal);
    }

    /// Review: ground already being walked to skipped the cold check, so a starving NPC walked on until
    /// its warmth turned critical. The trip's chill is weighed again at every step: too cold to go on,
    /// it gives the ground up.
    [Fact]
    public void WalkingToForage_TooColdToGoOn_GivesTheGroundUp()
    {
        (Activity Activity, TileId? Goal, TileId At, TileId Ground) Decide(double airC)
        {
            var (w, npc, ground) = ForagerAtCamp(At(0, 11), new WeatherSample(0, WeatherCondition.Cloudy, airC, 0.6, 0, 0.8, WindDirection.North));
            var agent = w.Agent(npc);
            var mind = (NpcMind)agent.Mind;
            var camp = mind.Camp;
            // One tile out along the way, starving, the ground still four tiles off.
            var at = MapGen.Neighbors(camp).Where(t => MapGen.Manhattan(t, ground) == 4 && Map.Blocked(Map.Of(w), camp, t) == null).OrderBy(t => t).First();
            agent.Tile = at;
            mind.ForageGoal = ground;
            agent.Body.FatKg = mind.TargetFatKg - 1.5;
            agent.Body.CoreTempF = 98.2;
            var (a, _) = Npc.Decide(w, agent, []);
            output.WriteLine($"{airC}C: {a}, need {mind.SeeingTo}, goal {mind.ForageGoal}");
            return (a, mind.ForageGoal, at, ground);
        }
        var mild = Decide(10);
        Assert.Equal(mild.Ground, mild.Goal);
        Assert.IsType<Travel>(mild.Activity); // on its way
        // At -20C the nine tiles out and back would bring it home under the trip line (Npc.TripHomeCoreF), though it
        // is not cold yet: it gives the ground up rather than walk on until warmth is critical. (At freezing, before
        // the trip budget counted the heat its clothes had stored, this was already too cold; at -10C, while trips
        // were held to warmth's Need line, 97.5F.)
        var cold = Decide(-20);
        Assert.Null(cold.Goal);
        Assert.NotEqual(mild.Activity, cold.Activity); // not the step on toward it
    }

    // ---- fire keeping (traced: seed 5 died on day 4.8, seeds 42 and 2024 late, all of cold) ----

    /// An NPC at camp at this minute, by a pit holding this fire, with nothing to burn in its pack or
    /// on the stash (its reserve of kindling at most), tired.
    static (World World, AgentId Npc, FireState Pit) ByAFire(long minute, FireState pit, double kindlingKg = 0, double tinderKg = 0)
    {
        var w = World.New(17);
        w.FixedWeather = Mild;
        NoHerds(w);
        w.Minute = minute;
        var camp = Map.Of(w).Camp;
        var npc = Npc.Spawn(w, camp);
        var mind = (NpcMind)w.Agent(npc).Mind;
        mind.Pack.Kg.Clear();
        if (kindlingKg > 0) mind.Pack.Kg[Resource.Kindling] = kindlingKg;
        if (tinderKg > 0) mind.Pack.Kg[Resource.Tinder] = tinderKg;
        mind.Pack.Kg[Resource.Water] = 3;
        w.Agent(npc).Body.EnergyMinutes = 0.4 * Body.MaxEnergyMinutes;
        pit.Tile = camp;
        pit.LastUpdatedMinute = minute;
        w.Fires[new FireId(w.NextFireId++)] = pit;
        return (w, npc, pit);
    }

    /// Traced (seed 5, day 4): with nothing to bank its fire, it slept two hours at a time beside it, and
    /// woke to a cold pit and a hand drill. It sleeps only until the fire is nearly out. (The camp ground picked
    /// bare: with anything to gather there, the fire short of tonight has it gathering first - warmth's fuel tier
    /// runs through the night, NightFireShortOfTonight_TheNpcAnswersIt.)
    [Fact]
    public void NothingToBankTheFire_SleepsOnlyUntilItIsNearlyOut()
    {
        var pit = new FireState { Tile = default, LastUpdatedMinute = 0, BurningKg = { [Resource.Birch] = 0.8 } };
        var (w, npc, _) = ByAFire(At(3, 23), pit);
        PickedBare(w, w.Agent(npc).Tile);
        double minutesLeft = Fire.FuelHours(pit) * 60;
        var (a, minutes) = Npc.Decide(w, w.Agent(npc), []);
        output.WriteLine($"{a} for {minutes} min; the fire has {minutesLeft:0} min");
        Assert.IsType<Sleep>(a);
        Assert.True(minutes < minutesLeft, $"slept {minutes} minutes beside a fire with {minutesLeft:0} left");
    }

    /// The same, woken with the fire in embers and nothing to feed them: it gropes about the camp for
    /// something while they will still take it, rather than sleep on.
    [Fact]
    public void WokenToEmbers_WithNothingToFeedThem_LooksForSomething()
    {
        var pit = new FireState { Tile = default, LastUpdatedMinute = 0, HasEmbers = true, EmberDurationHours = 0.5, EmberHoursRemaining = 0.4, EmberStartTempF = 700 };
        var (w, npc, _) = ByAFire(At(3, 23), pit);
        var (a, _) = Npc.Decide(w, w.Agent(npc), []);
        output.WriteLine($"{a}");
        Assert.Equal(new Gather(npc, 0, ForageFocus.Fuel), a);
    }

    /// Home by evening with an hour of fire and timber next door: there is light to fetch some before
    /// bed, and it does (old: to bed with what it had). After the hour it must be home by
    /// (Npc.HomeMinuteOfDay), it gets what it can without leaving camp (see WinterEvening_NoTripForWoodAfterSunset).
    [Theory]
    [InlineData(17, 30, true)]
    [InlineData(19, 0, false)]
    public void ShortOfFuelAtDusk_FetchesSomeWhileThereIsLight(int hour, int minute, bool walks)
    {
        var pit = new FireState { Tile = default, LastUpdatedMinute = 0, BurningKg = { [Resource.Kindling] = 1.0 } };
        var (w, npc, _) = ByAFire(At(0, hour, minute), pit, kindlingKg: 0.9, tinderKg: 0.2);
        var chose = new List<Activity>();
        for (int i = 0; i < 4; i++)
        {
            var (a, minutes) = Npc.Decide(w, w.Agent(npc), []);
            chose.Add(a);
            if (a is not (AddFuel or Stash or Unstash)) break; // a minute's tidying first: the fire fed, the pack unloaded
            Sim.Step(w, a, minutes);
        }
        output.WriteLine(string.Join(", ", chose));
        if (walks) Assert.True(chose[^1] is Travel || chose[^1] is Gather g && Pools.At(w, w.Agent(npc).Tile)[g.Pool] is TreePool, $"chose {chose[^1]}");
        else Assert.DoesNotContain(chose, a => a is Travel);
    }

    /// Every forage pool on a tile picked over to nothing, for now.
    static void PickedBare(World w, TileId tile)
    {
        var ts = w.Tiles.TryGetValue(tile, out var t) ? t : w.Tiles[tile] = new TileState();
        ts.Pools = Enumerable.Range(0, Pools.At(w, tile).Count).Where(i => Pools.At(w, tile)[i] is ForagePool)
            .ToDictionary(i => i, i => (PoolState)new ForageState { HoursForaged = 1000, LastForagedMinute = w.Minute });
    }

    /// Traced (seed 5): it would not strike under 20% until its core was critical, by when its cold hands
    /// had the drill at 13%, then at the 5% floor. Wanting warmth, a strike at poor odds is worth the tinder.
    [Fact]
    public void WantingWarmth_StrikesAtOddsItWouldWaitOnWhenWarm()
    {
        (Activity Activity, double Odds) Decide(double coreF)
        {
            var pit = new FireState { Tile = default, LastUpdatedMinute = 0 };
            var (w, npc, _) = ByAFire(At(3, 23, 30), pit, kindlingKg: 1.0, tinderKg: 0.3);
            var agent = w.Agent(npc);
            agent.Body.CoreTempF = coreF;
            agent.Body.EnergyMinutes = Body.MaxEnergyMinutes; // awake: the night is for keeping warm, not sleep
            Effects.Set(agent.Body, EffectKind.Wet, 1.0); // wet, shaking hands in the dark: a poor strike
            Effects.Set(agent.Body, EffectKind.Shivering, 0.6);
            Fire.CatchUpAround(w, agent, []);
            double odds = Fire.StartOdds(w, agent, ToolType.HandDrill, Resource.Tinder).TotalPct;
            var (a, _) = Npc.Decide(w, agent, []);
            output.WriteLine($"core {coreF}: odds {odds:0.00}, {a}");
            return (a, odds);
        }
        var warm = Decide(98.4);
        Assert.InRange(warm.Odds, 0.07, 0.19);
        Assert.IsNotType<StartFire>(warm.Activity);
        Assert.IsType<StartFire>(Decide(96.6).Activity); // wanting warmth, not yet freezing
    }

    /// Traced (seed 5): it lit kindling, but the only logs it had were oak, which a kindling fire is too
    /// cool to take; its last kindling was its reserve, so the fire died with the oak beside it. Logs
    /// waiting on a fire too cool for them, the reserve goes on to build it up, and the oak goes on after.
    [Fact]
    public void AKindlingFire_WithOnlyOak_IsBuiltUpUntilTheOakGoesOn()
    {
        var pit = new FireState { Tile = default, LastUpdatedMinute = 0, BurningKg = { [Resource.Tinder] = 0.05, [Resource.Kindling] = 0.3 } };
        var (w, npc, _) = ByAFire(At(0, 11), pit, kindlingKg: 0.6);
        var agent = w.Agent(npc);
        Items.Add(((NpcMind)agent.Mind).Pack, Resource.Oak, 6);
        Items.Add(Items.StashAt(w, agent.Tile), Resource.Kindling, 0.8);
        agent.Body.CoreTempF = 96.6; // it wants the fire
        agent.Body.EnergyMinutes = Body.MaxEnergyMinutes;
        Assert.False(Fire.CanAddFuel(pit, Resource.Oak));
        var log = NpcDriver.Live(w, npc, w.Minute + 90);
        var fed = log.OfType<FuelAdded>().Where(f => f.By == npc).Select(f => $"{f.Fuel} {f.Kg:0.00}").ToList();
        output.WriteLine(string.Join(", ", fed));
        Assert.Contains(log, e => e is FuelAdded { Fuel: Resource.Oak } f && f.By == npc);
    }

    /// Traced (seed 5): no standing timber within 15 tiles of camp, only deadfall; it had an axe and never used it,
    /// and froze on day 4.8. A camp with no timber in reach is a reason to move camp to timber. Seed 5's own camp and
    /// its nearest stand (24 tiles east, past MaxWoodTiles), at the bench id, in mild weather with no herds about, the
    /// stand known to it: it sets out for the stand with home moved there, gets there within the day, and keeps
    /// house there - timber in reach, alive. (Once this ran seed 5's real kit and weather from spawn and passed or
    /// not on the trajectory's luck; that outcome is the bench's, reported, not asserted.)
    [Fact]
    public void NoTimberInReach_MovesCampToTimber()
    {
        var w = World.New(5);
        w.FixedWeather = Mild;
        NoHerds(w);
        var m = Map.Of(w);
        var camp = m.Camp;
        var npc = NpcBench.Spawn(w, camp);
        var agent = w.Agent(npc);
        var mind = (NpcMind)agent.Mind;
        bool Timber(TileId t) => m.InBounds(t) && Pools.At(w, t).Select((p, i) => (p, i)).Any(x => x.p is TreePool && Pools.HasTrees(w, new PoolId(t, x.i)));
        var box = Enumerable.Range(-30, 61).SelectMany(dx => Enumerable.Range(-30, 61).Select(dy => new TileId(camp.X + dx, camp.Y + dy)));
        Assert.DoesNotContain(box, t => MapGen.Manhattan(t, camp) <= 15 && Timber(t)); // nothing in carrying reach
        var stand = box.Where(t => MapGen.Manhattan(t, camp) <= 30 && Timber(t)).OrderBy(t => MapGen.Manhattan(t, camp)).ThenBy(t => t).First();
        mind.Knows.Places[stand] = new TileMemory { SeenMinute = w.Minute, Trees = true };
        var (first, _) = Npc.Decide(w, agent, []);
        output.WriteLine($"camp {camp}, stand {stand} ({MapGen.Manhattan(stand, camp)} tiles): first {first}, home now {mind.Camp}");
        NpcDriver.Live(w, npc, w.Minute + 1440);
        var trees = mind.Knows.Places.Where(p => p.Value.Trees).Select(p => p.Key).ToList();
        output.WriteLine($"a day on: at {agent.Tile}, home {mind.Camp}, nearest timber {trees.Min(t => MapGen.Manhattan(t, mind.Camp))} tiles; alive {Body.IsAlive(agent.Body)}");
        Assert.NotEqual(camp, mind.Camp);
        Assert.True(trees.Min(t => MapGen.Manhattan(t, mind.Camp)) <= 2);
        Assert.True(MapGen.Manhattan(agent.Tile, mind.Camp) <= 2, "never got there");
        Assert.True(Body.IsAlive(agent.Body));
    }
}
