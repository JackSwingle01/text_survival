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
    static (World World, AgentId Npc) AtCamp(int seed)
    {
        var w = World.New(seed);
        return (w, Npc.Spawn(w, Map.Of(w).Camp));
    }

    static double CampWoodKg(World w, AgentId npc)
    {
        var camp = ((NpcMind)w.Agent(npc).Mind).Camp;
        var stash = w.Tiles.GetValueOrDefault(camp)?.Stash;
        return new[] { Resource.Pine, Resource.Birch, Resource.Oak, Resource.Kindling }.Sum(r => stash != null ? Items.Have(stash, r) : 0);
    }

    /// Phase 3 gate: an NPC survives 14 days alone - no hand-feeding, nothing given past its spawn kit.
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

    /// Hungry, with wood enough at camp and a caribou on its tile in daylight: it goes after it.
    [Fact]
    public void HungryWithPreyHere_Hunts()
    {
        var w = World.New(17);
        var camp = Map.Of(w).Camp;
        var npc = Npc.Spawn(w, camp);
        Items.Add(Items.StashAt(w, camp), Resource.Birch, 40); // so firewood is not what it worries about
        w.Agent(npc).Body.CaloriesKcal = 100;
        w.Agent(npc).Body.FatKg -= 0.6; // short of the fat it is used to: food is wanted (Npc.TargetFatKg)
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
        output.WriteLine($"{a}, need {mind.Need}");
        Assert.Equal(Need.Food, mind.Need);
    }

    [Fact]
    public void TonightsFuelNotBanked_WantedFood_GoesForWood()
    {
        var (w, mind) = ShortOfFat(0.5, 0);
        var a = DecideFor(w, mind);
        output.WriteLine($"{a}, need {mind.Need}");
        Assert.Null(mind.Need); // the wood plan sees to no need
        Assert.False(a is Gather { Focus: ForageFocus.Food });
    }

    [Fact]
    public void CriticalShortfall_GoesForFood_EvenShortOfFuel_ButNotOverCriticalCold()
    {
        var (w, mind) = ShortOfFat(1.5, 0);
        DecideFor(w, mind);
        Assert.Equal(Need.Food, mind.Need);

        var (cold, coldMind) = ShortOfFat(1.5, 0);
        cold.Agents.Values.Single(a => a.Mind == coldMind).Body.CoreTempF = 95.0;
        DecideFor(cold, coldMind);
        Assert.Equal(Need.Warmth, coldMind.Need);
    }

    /// A lean week: it gets used to the leaner body, but never below the floor where leanness costs.
    [Fact]
    public void TargetFat_AdaptsDownOverALeanWeek_NeverBelowTheFloor()
    {
        var body = BodyState.BaselineHuman();
        var mind = new NpcMind { TargetFatKg = body.FatKg };
        body.FatKg = 9.0;
        Npc.AdaptTargetFat(mind, body, 7 * 1440);
        output.WriteLine($"after a week at 9 kg: target {mind.TargetFatKg:0.000}");
        Assert.InRange(mind.TargetFatKg, 9.0, 9.6); // 9 + 2.25 * 2^(-7/3)

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
        output.WriteLine($"{a}, need {mind.Need}");
        Assert.Equal(Need.Warmth, mind.Need);
    }

    /// Old: cold by a fire with no wood left to feed it, it rested until the fire was nearly out, then
    /// groped the bare camp ground for sticks (bug, fixed). By day it goes for wood while the fire lasts.
    [Fact]
    public void ColdByAFireWithNoWoodLeft_GoesForWoodWhileItLasts()
    {
        var w = World.New(17);
        var camp = Map.Of(w).Camp;
        var npc = Npc.Spawn(w, camp);
        var pit = new FireState { Tile = camp, LastUpdatedMinute = w.Minute, Pit = PitType.Open };
        Fire.AddFuel(pit, Resource.Kindling, 2.5);
        Fire.IgniteAll(pit);
        w.Fires[new FireId(w.NextFireId++)] = pit;
        ((NpcMind)w.Agent(npc).Mind).Pack.Kg[Resource.Kindling] = 0.6; // its reserve: nothing to feed the fire
        w.Agent(npc).Body.CoreTempF = 96.5;
        var (a, _) = Npc.Decide(w, w.Agent(npc), []);
        output.WriteLine($"{a}");
        Assert.True(a is Travel or Gather { Focus: not ForageFocus.Food } && a is not Gather { Pool: 0 }, $"chose {a}");
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
        Assert.Equal(Need.Rest, mind.Need);
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
        output.WriteLine($"{a}, need {mind.Need}");
        Assert.Single(w.Encounters);
        Assert.DoesNotContain(w.Encounters[0].Units, u => u.Agent == npc);
    }
}
