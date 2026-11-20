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
                         $"trees felled {log.OfType<TreeFelled>().Count()}, places known {((NpcMind)w.Agent(npc).Mind).Places.Count}, camp wood {CampWoodKg(w, npc):0} kg");
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

        var (activity, _) = Npc.Decide(w, w.Agent(npc));
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

        Npc.Decide(w, w.Agent(npc));
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
            var (activity, minutes) = Npc.Decide(w, w.Agent(npc));
            Assert.IsType<Gather>(activity);
            cut = Sim.Step(w, activity, minutes);
        }
        Assert.Equal("tree felled", cut!.InterruptedReason);
        Assert.True(cut.MinutesElapsed < 60, "the step the tree fell in should have been cut short");

        var (next, _) = Npc.Decide(w, w.Agent(npc));
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
        Assert.Equal(stoodOn.OrderBy(t => t), mind.Places.Where(p => p.Value.Visited).Select(p => p.Key));
    }
}
