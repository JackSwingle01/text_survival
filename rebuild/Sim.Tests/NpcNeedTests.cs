using CsCheck;
using Survival;
using Survival.Tests.Verification;
using Xunit.Abstractions;

/// No Need dropped without a reason, and no pacing (#271): every stat at Need or Critical the NPC passes over is told
/// (NeedDropped) with why, none is left with no answer (NeedStuck), and it does not walk back and forth. Each world is
/// hand-built, with no animals.
public class NpcNeedTests(ITestOutputHelper output)
{
    static (World World, Agent Npc) Alone(double tempC, TileId? at = null, TileId? camp = null)
    {
        var w = ConservationWorlds.Bare(tempC);
        w.Agents.Remove(AgentId.Player);
        var npc = w.Agent(Npc.Spawn(w, at ?? Map.Of(w).Camp, camp));
        TestKit.ClearResources(MindOf(npc).Pack);
        return (w, npc);
    }

    static NpcMind MindOf(Agent a) => (NpcMind)a.Mind;

    static readonly bool Tracing = Environment.GetEnvironmentVariable("NPC_TRACE") is { Length: > 0 };
    [ThreadStatic] static List<string>? trace;
    static List<string> Trace => trace ??= [];

    static FireState Pit(World w, TileId tile)
    {
        var pit = new FireState { Tile = tile, LastUpdatedMinute = w.Minute, Pit = PitType.Open };
        w.Fires[new FireId(w.NextFireId++)] = pit;
        return pit;
    }

    static void RemoveStarters(Inventory pack) => pack.Gear.RemoveAll(g => Fire.Starters.Any(t => Items.Def(g).Tool == t));

    /// No forage anywhere near camp: what it needs is only where the scene puts it.
    static void NoForage(World w, TileId around)
    {
        w.PoolCache = [];
        for (int dx = -13; dx <= 13; dx++)
            for (int dy = -13; dy <= 13; dy++)
                if (Map.Of(w).InBounds(new TileId(around.X + dx, around.Y + dy))) w.PoolCache[new TileId(around.X + dx, around.Y + dy)] = [];
    }

    /// A pile it remembers, east of camp.
    static TileId RememberedPile(World w, Agent npc, TileId camp, int tiles, Resource r, double kg)
    {
        var at = new TileId(camp.X + tiles, camp.Y);
        Assert.True(Map.Of(w).TerrainAt(at).IsPassable());
        TestKit.Give(Items.PileAt(w, at), r, kg);
        Memories.Of(npc).Places[at] = new TileMemory { SeenMinute = w.Minute, VisitedMinute = w.Minute, Pile = new() { [r] = kg } };
        return at;
    }

    /// Lives the NPC on, minute by minute: what happened, and each decision it made.
    static (List<WorldEvent> Log, List<NpcDecision> Decisions) Live(World w, Agent npc, int minutes)
    {
        var log = new List<WorldEvent>();
        var decisions = new List<NpcDecision>();
        long until = w.Minute + minutes;
        while (w.Minute < until && Sim.IsAlive(npc))
        {
            log.AddRange(Sim.Advance(w, 1));
            if (MindOf(npc).LastDecision is { } d && (decisions.Count == 0 || !ReferenceEquals(decisions[^1], d)))
            {
                decisions.Add(d);
                if (Tracing) Trace.Add($"{d.Minute} at {npc.Tile} serves {d.Serves} [{string.Join(",", d.Ranked)}] plan {MindOf(npc).Plan} doing {Sim.ActivityOf(npc)}");
            }
        }
        return (log, decisions);
    }

    // ---- an impossible need is passed over, and why is told ----

    /// Cold at camp with no fire starter, and berries it remembers two tiles east: warmth is dropped naming the fire
    /// starter, and it goes for the food.
    [Fact]
    public void NoFireStarter_WarmthAtNeed_IsDropped_NamingTheStarter_AndItGoesOn()
    {
        var (w, npc) = Alone(-10);
        var camp = Map.Of(w).Camp;
        var pack = MindOf(npc).Pack;
        RemoveStarters(pack);
        TestKit.Give(pack, Resource.Tinder, 0.3);
        TestKit.Give(pack, Resource.Stick, 3);
        TestKit.Give(pack, Resource.Water, 2);
        NoForage(w, camp);
        var berries = RememberedPile(w, npc, camp, 2, Resource.Berries, 3);
        npc.Body.CoreTempF = 96.5;
        npc.Body.CaloriesKcal = 0.1 * Body.CaloriesCapacityKcal(npc.Body);
        var (log, _) = Live(w, npc, 1);
        output.WriteLine(string.Join("\n", log.Where(e => e is NeedDropped or NeedStuck or Moved)));
        var dropped = Assert.Single(log.OfType<NeedDropped>(), d => d.Who == npc.Id && d.Stat == Stat.Warmth);
        Assert.Contains("fire starter", dropped.Reason);
        Assert.Equal(Stat.Food, MindOf(npc).Plan?.Stat);
        Assert.Equal(berries, MindOf(npc).Plan?.At);
        Assert.DoesNotContain(log, e => e is NeedStuck);
    }

    // ---- no need is stuck, and a dropped Need says why ----

    public enum PitKind { None, Cold, Glowing, Burning }

    static readonly Resource[] Fuels = [.. Fire.Props.Keys.Order()];
    static readonly Gen<(Resource, double)> FuelGen = Gen.Select(Gen.Int[0, Fuels.Length - 1], Gen.Double[0.05, 12], (i, kg) => (Fuels[i], kg));

    public sealed record State(PitKind Pit, (Resource Fuel, double Kg)[] Pack, (Resource Fuel, double Kg)[] Pile, bool Starter, double WaterKg,
        double BerriesKg, bool NoForage, int Hour, double TempC, Priority Warmth, Priority Water, Priority Food, Priority Rest)
    {
        public override string ToString() =>
            $"pit {Pit}, pack [{string.Join(", ", Pack.Select(p => $"{p.Fuel} {p.Kg:0.00}"))}], pile [{string.Join(", ", Pile.Select(p => $"{p.Fuel} {p.Kg:0.00}"))}], " +
            $"starter {Starter}, water {WaterKg:0.0}, berries {BerriesKg:0.0}, no forage {NoForage}, {Hour}:00, {TempC:0}C, warmth {Warmth}, water {Water}, food {Food}, rest {Rest}";
    }

    static readonly Gen<State> States = Gen.Select(Gen.Select(Gen.Enum<PitKind>(), FuelGen.Array[0, 4], FuelGen.Array[0, 3], Gen.Bool, Gen.Bool.Select(b => b ? 2.0 : 0.0), Gen.Bool.Select(b => b ? 1.0 : 0.0), Gen.Bool),
        Gen.Select(Gen.Int[0, 23], Gen.Double[-25, 5], Gen.Enum<Priority>(), Gen.Enum<Priority>(), Gen.Enum<Priority>(), Gen.Enum<Priority>()),
        (a, b) => new State(a.Item1, a.Item2, a.Item3, a.Item4, a.Item5, a.Item6, a.Item7, b.Item1, b.Item2, b.Item3, b.Item4, b.Item5, b.Item6));

    /// A body set to roughly these levels: warmth by the core (a Want comes from the fire's hours), water, food and rest by their stores.
    static void SetBody(BodyState b, State s)
    {
        b.CoreTempF = s.Warmth switch { Priority.Critical => 94.8, Priority.Need => 97.0, _ => 98.6 };
        b.HydrationMl = Body.MaxHydrationMl * s.Water switch { Priority.Critical => 0.1, Priority.Need => 0.4, Priority.Want => 0.8, _ => 0.95 };
        b.EnergyMinutes = Body.MaxEnergyMinutes * s.Rest switch { Priority.Critical => 0.05, Priority.Need => 0.25, Priority.Want => 0.6, _ => 0.9 };
        b.CaloriesKcal = Body.CaloriesCapacityKcal(b) * s.Food switch { Priority.Critical or Priority.Need => 0.0, Priority.Want => 0.5, _ => 0.9 };
        if (s.Food == Priority.Critical) b.FatKg = StarvingFatPct * b.WeightKg;
    }

    const double StarvingFatPct = Priorities.StarvingFatPct;

    static World WorldFor(State s, out Agent npc)
    {
        var (w, a) = Alone(s.TempC);
        npc = a;
        var camp = Map.Of(w).Camp;
        while (Weather.MinuteOfDay(w.Minute) / 60 != s.Hour) w.Minute += 60;
        var pack = MindOf(npc).Pack;
        if (!s.Starter) RemoveStarters(pack);
        foreach (var (r, kg) in s.Pack) Items.AddKg(pack, r, kg);
        foreach (var (r, kg) in s.Pile) Items.AddKg(Items.PileAt(w, camp), r, kg);
        if (s.WaterKg > 0) TestKit.Give(pack, Resource.Water, s.WaterKg);
        if (s.BerriesKg > 0) TestKit.Give(pack, Resource.Berries, s.BerriesKg);
        if (s.NoForage) NoForage(w, camp);
        if (s.Pit == PitKind.Cold) Fire.AddFuel(Pit(w, camp), Resource.BirchBranch, 2);
        if (s.Pit == PitKind.Glowing)
        {
            var embers = Pit(w, camp);
            (embers.HasEmbers, embers.EmberDurationHours, embers.EmberHoursRemaining, embers.EmberStartTempF) = (true, 1, 1, 600);
        }
        if (s.Pit == PitKind.Burning)
        {
            var pit = Pit(w, camp);
            Fire.AddFuel(pit, Resource.Stick, 0.5);
            Fire.IgniteAll(pit);
        }
        SetBody(npc.Body, s);
        return w;
    }

    /// Random NPC states at camp, each lived 120 minutes: no NeedStuck, and every decision that serves a stat ranked below
    /// one at Need or Critical (or serves none) has a NeedDropped for the higher one, with a reason. A decision holds for
    /// all its minutes: the ranking checked is the one it was made on.
    [Fact]
    [Trait("Category", TestCategories.LongRun)]
    public void NoNeedIsStuck_AndEveryDroppedNeedSaysWhy()
    {
        States.Check(s =>
        {
            var w = WorldFor(s, out var npc);
            var (log, decisions) = Live(w, npc, 120);
            var stuck = log.OfType<NeedStuck>().Where(e => e.Who == npc.Id).ToList();
            Assert.True(stuck.Count == 0, $"{s}: stuck {string.Join("; ", stuck)}");
            var dropped = log.OfType<NeedDropped>().Where(e => e.Who == npc.Id).ToList();
            Assert.All(dropped, d => Assert.False(string.IsNullOrWhiteSpace(d.Reason), $"{s}: {d} has no reason"));
            foreach (var d in decisions)
                foreach (var (stat, priority) in d.Ranked.TakeWhile(x => x.Stat != d.Serves).Where(x => x.Priority >= Priority.Need))
                    Assert.True(dropped.Any(x => x.Minute == d.Minute && x.Stat == stat),
                        $"{s}: at minute {d.Minute} it served {d.Serves?.ToString() ?? "nothing"} over {stat} at {priority} with no NeedDropped");
        }, iter: 60);
    }

    // ---- no pacing ----

    public enum Where { OneTile, TwoTiles, Nowhere }

    const int PacingMinutes = 600, MostReversals = 3;

    /// A tile beside open water with passable ground east of it: the water scenes make their camp that far off.
    static TileId WaterEdge(World w, int tiles)
    {
        var m = Map.Of(w);
        var camp = m.Camp;
        for (int r = 0; r < 40; r++)
            for (int dx = -r; dx <= r; dx++)
                foreach (int dy in new[] { r - Math.Abs(dx), Math.Abs(dx) - r }.Distinct())
                {
                    var t = new TileId(camp.X + dx, camp.Y + dy);
                    if (!m.InBounds(t) || !m.TerrainAt(t).IsPassable() || !Work.HasOpenWater(w, t)) continue;
                    var ok = Enumerable.Range(1, tiles).Select(i => new TileId(t.X + i, t.Y))
                        .All(x => m.InBounds(x) && m.TerrainAt(x).IsPassable() && !Work.CanDrinkHere(w, x));
                    if (ok) return t;
                }
        throw new InvalidOperationException("no open water with dry ground east of it near camp");
    }

    static (World, Agent) Scene(Stat stat, Where where)
    {
        int tiles = where == Where.OneTile ? 1 : 2;
        if (stat == Stat.Water && where != Where.Nowhere)
        {
            var probe = ConservationWorlds.Bare(-5);
            var water = WaterEdge(probe, tiles);
            var camp = new TileId(water.X + tiles, water.Y);
            var (ww, wn) = Alone(-5, camp, camp);
            Memories.Of(wn).Places[water] = new TileMemory { SeenMinute = ww.Minute, VisitedMinute = ww.Minute, Water = true };
            TestKit.Give(MindOf(wn).Pack, Resource.Tinder, 0.3);
            TestKit.Give(MindOf(wn).Pack, Resource.Berries, 1);
            wn.Body.HydrationMl = 0.4 * Body.MaxHydrationMl;
            return (ww, wn);
        }
        var (w, npc) = Alone(-5);
        var home = MindOf(npc).Camp;
        var pack = MindOf(npc).Pack;
        TestKit.Give(pack, Resource.Tinder, 0.3);
        if (stat != Stat.Water) TestKit.Give(pack, Resource.Water, 2);
        if (stat != Stat.Food) TestKit.Give(pack, Resource.Berries, 1);
        NoForage(w, home);
        switch (stat)
        {
            case Stat.Warmth:
                npc.Body.CoreTempF = 97.0;
                if (where != Where.Nowhere) RememberedPile(w, npc, home, tiles, Resource.Stick, 20);
                break;
            case Stat.Water:
                npc.Body.HydrationMl = 0.4 * Body.MaxHydrationMl;
                break;
            case Stat.Food:
                npc.Body.CaloriesKcal = 0;
                if (where != Where.Nowhere) RememberedPile(w, npc, home, tiles, Resource.Berries, 3);
                break;
            case Stat.Rest:
                // Its bed is camp, where it stands away from; nowhere it remembers: no ground seen between.
                npc.Body.EnergyMinutes = 0.25 * Body.MaxEnergyMinutes;
                npc.Tile = new TileId(home.X + tiles, home.Y);
                if (where == Where.Nowhere) MindOf(npc).Memory.Explored = null;
                break;
        }
        return (w, npc);
    }

    public static TheoryData<Stat, Where> Scenes()
    {
        var data = new TheoryData<Stat, Where>();
        foreach (var stat in new[] { Stat.Warmth, Stat.Water, Stat.Food, Stat.Rest })
            foreach (var where in Enum.GetValues<Where>()) data.Add(stat, where);
        return data;
    }

    /// Each stat at Need, with what it needs one tile from camp, two tiles, or nowhere it remembers: it does not pace.
    [Theory]
    [Trait("Category", TestCategories.LongRun)]
    [MemberData(nameof(Scenes))]
    public void AtNeed_ItDoesNotPace(Stat stat, Where where)
    {
        var (w, npc) = Scene(stat, where);
        AssertNoPacing(w, npc);
    }

    /// #270's first scene: sticks only, water at Critical.
    [Fact]
    [Trait("Category", TestCategories.LongRun)]
    public void SticksOnly_WaterCritical_ItDoesNotPace()
    {
        var (w, npc) = Alone(-5);
        var pack = MindOf(npc).Pack;
        TestKit.Give(pack, Resource.Tinder, 0.2);
        TestKit.Give(pack, Resource.Stick, 4.4);
        npc.Body.HydrationMl = 0.1 * Body.MaxHydrationMl;
        AssertNoPacing(w, npc);
    }

    /// #270's second scene: oak on the camp pile, warmth at Need.
    [Fact]
    [Trait("Category", TestCategories.LongRun)]
    public void OakOnThePile_WarmthAtNeed_ItDoesNotPace()
    {
        var (w, npc) = Alone(-10);
        var camp = Map.Of(w).Camp;
        Pit(w, camp);
        var pack = MindOf(npc).Pack;
        TestKit.Give(pack, Resource.Tinder, 0.2);
        TestKit.Give(pack, Resource.Stick, 4.7);
        TestKit.Give(pack, Resource.Water, 2);
        TestKit.Give(Items.PileAt(w, camp), Resource.OakBranch, 16);
        npc.Body.CoreTempF = 96.0;
        AssertNoPacing(w, npc);
    }

    void AssertNoPacing(World w, Agent npc)
    {
        var (log, _) = Live(w, npc, PacingMinutes);
        int reversals = Reversals.In(log, npc.Id);
        var told = log.Where(e => e is NeedDropped d && d.Who == npc.Id || e is NeedStuck s && s.Who == npc.Id).ToList();
        output.WriteLine($"reversals {reversals}, alive {Sim.IsAlive(npc)}, dropped {told.Count(e => e is NeedDropped)}, stuck {told.Count(e => e is NeedStuck)}");
        output.WriteLine(string.Join("\n", told.Take(10)));
        if (Tracing)
            output.WriteLine(string.Join("\n", Trace.Take(60)));
        Assert.True(reversals <= MostReversals, $"{reversals} reversals in {PacingMinutes} minutes");
        Assert.DoesNotContain(log, e => e is NeedStuck s && s.Who == npc.Id);
    }
}
