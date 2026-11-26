using CsCheck;
using Survival;
using Survival.Tests.Verification;
using Xunit.Abstractions;

/// The NPC's fire (#270): it plans, strikes and tends by Fire.Forecast alone. Each world is hand-built, with no animals.
public class NpcFireTests(ITestOutputHelper output)
{
    static (World World, Agent Npc) Alone(double tempC)
    {
        var w = ConservationWorlds.Bare(tempC);
        w.Agents.Remove(AgentId.Player);
        var npc = w.Agent(Npc.Spawn(w, Map.Of(w).Camp));
        TestKit.ClearResources(MindOf(npc).Pack);
        return (w, npc);
    }

    static NpcMind MindOf(Agent a) => (NpcMind)a.Mind;

    static FireState Pit(World w, TileId tile)
    {
        var pit = new FireState { Tile = tile, LastUpdatedMinute = w.Minute, Pit = PitType.Open };
        w.Fires[new FireId(w.NextFireId++)] = pit;
        return pit;
    }

    static FireState Burning(World w, TileId tile, Resource fuel, double kg)
    {
        var pit = Pit(w, tile);
        Fire.AddFuel(pit, fuel, kg);
        Fire.IgniteAll(pit);
        return pit;
    }

    static FireForecast ForecastAtCamp(World w, Agent npc, double hours)
    {
        var camp = Map.Of(w).Camp;
        var fuel = Items.CopyResources(MindOf(npc).Pack);
        if (w.Tiles.GetValueOrDefault(camp)?.Pile is { } pile) Items.AddAll(fuel, Items.CopyResources(pile));
        var starter = Fire.Starters.Where(t => Items.Tool(MindOf(npc).Pack, t) != null).Cast<ToolType?>().FirstOrDefault();
        return Fire.Forecast(new FireAsk(Fire.CurrentPitAt(w, camp), PitType.Open, fuel, starter, hours));
    }

    static void Nightfall(World w)
    {
        while (Weather.IsDaytime(w.Minute)) w.Minute += 30;
    }

    static bool Struck(WorldEvent e, AgentId who) => e is FireLit l && l.By == who || e is FireStartFailed f && f.Who == who;

    /// Struck, or out fetching one of the fuels the forecast named (a step taken, or the fuel gathered or picked up).
    static void StruckOrFetching(List<WorldEvent> log, AgentId who, FireForecast before)
    {
        var named = before.Missing.SelectMany(m => m.AnyOf).ToHashSet();
        bool struck = log.Any(e => Struck(e, who));
        bool fetching = before.Missing.Count > 0 && log.Any(e => e is Moved m && m.Who == who || e is Gathered g && g.Who == who);
        Assert.True(struck || fetching, $"neither struck nor fetched ({string.Join(", ", before.Missing)}): {string.Join("; ", log.Where(e => e is Moved or Gathered or FireLit or FireStartFailed or PlanRefused).Take(20))}");
    }

    // ---- the two states the NPCs died in (#258) ----

    [Fact]
    public void SticksOnly_WaterWanted_ItStrikesOrFetches_ThenDrinks()
    {
        var (w, npc) = Alone(-5);
        var pack = MindOf(npc).Pack;
        TestKit.Give(pack, Resource.Tinder, 0.2);
        TestKit.Give(pack, Resource.Stick, 4.4);
        npc.Body.HydrationMl = 0.1 * Body.MaxHydrationMl;
        Assert.False(Work.CanDrinkHere(w, npc.Tile));
        var before = ForecastAtCamp(w, npc, 1);
        var first = NpcDriver.Live(w, npc.Id, w.Minute + 10);
        StruckOrFetching(first, npc.Id, before);
        var rest = NpcDriver.Live(w, npc.Id, w.Minute + 230);
        output.WriteLine(string.Join("\n", first.Concat(rest).Where(e => e is FireLit or FireStartFailed or FuelAdded or Drank or TransformationFinished)));
        Assert.Contains(first.Concat(rest), e => e is Drank d && d.Who == npc.Id);
    }

    [Fact]
    public void OakOnThePile_WarmthWanted_ItStrikesOrFetches_ThenBurnsTheOak()
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
        var before = ForecastAtCamp(w, npc, 8);
        var first = NpcDriver.Live(w, npc.Id, w.Minute + 10);
        StruckOrFetching(first, npc.Id, before);
        var rest = NpcDriver.Live(w, npc.Id, w.Minute + 230);
        var log = first.Concat(rest).ToList();
        output.WriteLine(string.Join("\n", log.Where(e => e is FireLit or FireStartFailed or FuelAdded or Gathered)));
        Assert.Contains(log, e => e is FuelAdded f && f.By == npc.Id && f.Fuel == Resource.OakBranch);
        Assert.True(Fire.CurrentPitAt(w, camp) is { } pit && Fire.IsActive(pit), "no fire burns at camp at the end");
    }

    // ---- each use of the fire: the forecast names a missing fuel, and it goes for it ----

    /// No forage anywhere near, and a pile it remembers two tiles east holding the fuel: the only source of it.
    static TileId RememberedPileEast(World w, Agent npc, Resource fuel, double kg)
    {
        var camp = Map.Of(w).Camp;
        w.PoolCache = [];
        for (int dx = -13; dx <= 13; dx++)
            for (int dy = -13; dy <= 13; dy++)
                if (Map.Of(w).InBounds(new TileId(camp.X + dx, camp.Y + dy))) w.PoolCache[new TileId(camp.X + dx, camp.Y + dy)] = [];
        var at = new TileId(camp.X + 2, camp.Y);
        Assert.True(Map.Of(w).TerrainAt(at).IsPassable());
        TestKit.Give(Items.PileAt(w, at), fuel, kg);
        Memories.Of(npc).Places[at] = new TileMemory { SeenMinute = w.Minute, VisitedMinute = w.Minute, Pile = new() { [fuel] = kg } };
        return at;
    }

    void StepsToward(World w, Agent npc, TileId at, Resource fuel, FireForecast forecast, Stat? plan = null)
    {
        Assert.Contains(forecast.Missing, m => m.AnyOf.Contains(fuel));
        var (a, _) = Npc.Decide(w, npc, []);
        output.WriteLine($"{a}; missing {string.Join(", ", forecast.Missing)}");
        var travel = Assert.IsType<Travel>(a);
        Assert.True(MapGen.Manhattan(Map.Step(npc.Tile, travel.Dir), at) < MapGen.Manhattan(npc.Tile, at), $"walked {travel.Dir}, away from {at}");
        if (plan != null) Assert.Equal(plan, MindOf(npc).Plan?.Stat);
    }

    /// Cold at camp with no tinder: the forecast names tinder, and it goes for the tinder.
    [Fact]
    public void Warmth_ForTheTinderTheForecastNames()
    {
        var (w, npc) = Alone(-10);
        TestKit.Give(MindOf(npc).Pack, Resource.Stick, 3);
        TestKit.Give(MindOf(npc).Pack, Resource.Water, 2);
        var at = RememberedPileEast(w, npc, Resource.Tinder, 1);
        npc.Body.CoreTempF = 96.0;
        StepsToward(w, npc, at, Resource.Tinder, ForecastAtCamp(w, npc, 1), Stat.Warmth);
    }

    /// Warm, by night, with too few sticks for the night: the forecast names more, and it goes for them.
    [Fact]
    public void TonightsFire_ForTheFuelTheForecastNames()
    {
        var (w, npc) = Alone(-10);
        Nightfall(w);
        TestKit.Give(MindOf(npc).Pack, Resource.Tinder, 0.3);
        TestKit.Give(MindOf(npc).Pack, Resource.Stick, 0.5);
        TestKit.Give(MindOf(npc).Pack, Resource.Water, 2);
        var at = RememberedPileEast(w, npc, Resource.Stick, 20);
        Assert.False(Weather.IsDaytime(w.Minute));
        StepsToward(w, npc, at, Resource.Stick, ForecastAtCamp(w, npc, 8), Stat.Warmth);
    }

    /// Two tiles from camp by night, warm, with nothing to strike with: the camp fire it remembers outlasting the night
    /// is fire enough, as it is when it stands beside it.
    [Fact]
    public void FromAway_TheCampFireItRemembers_CountsTowardTonight()
    {
        var (w, npc) = Alone(15);
        Nightfall(w);
        TestKit.Give(MindOf(npc).Pack, Resource.Water, 2);
        var camp = Map.Of(w).Camp;
        var away = new TileId(camp.X + 2, camp.Y);
        Assert.True(Map.Of(w).TerrainAt(away).IsPassable());
        Burning(w, camp, Resource.Stick, 10); // seen burning from where it stands, so the hours it remembers are kept
        Memories.Of(npc).Places[camp] = new TileMemory { SeenMinute = w.Minute, VisitedMinute = w.Minute, FireHours = 48 };
        npc.Tile = away;
        Npc.Decide(w, npc, []);
        Assert.NotEqual(Stat.Warmth, MindOf(npc).Plan?.Stat);
    }

    /// Parched by day with no water near and too few sticks for a melt: it goes for the sticks the forecast names.
    [Fact]
    public void Melting_ForTheFuelTheForecastNames()
    {
        var (w, npc) = Alone(-5);
        TestKit.Give(MindOf(npc).Pack, Resource.Tinder, 0.3);
        TestKit.Give(MindOf(npc).Pack, Resource.Stick, 0.2);
        var at = RememberedPileEast(w, npc, Resource.Stick, 20);
        npc.Body.HydrationMl = 0.1 * Body.MaxHydrationMl;
        Assert.True(Weather.IsDaytime(w.Minute));
        StepsToward(w, npc, at, Resource.Stick, ForecastAtCamp(w, npc, 1), Stat.Water);
    }

    /// Hungry, with meat on a fire that will die before it cooks and nothing to feed it: it goes for fuel.
    [Fact]
    public void Cooking_ForTheFuelTheForecastNames()
    {
        var (w, npc) = Alone(-5);
        var camp = Map.Of(w).Camp;
        Burning(w, camp, Resource.Stick, 0.3);
        var pack = MindOf(npc).Pack;
        TestKit.Give(pack, Resource.Water, 2);
        Items.AddPiece(pack, Resource.RawMeat, 0.4);
        Assert.NotEqual(StepOutcome.Refused, Sim.Step(w, new PutOn(npc.Id, "cook_meat"), 1).Outcome);
        var at = RememberedPileEast(w, npc, Resource.Stick, 20);
        npc.Body.CaloriesKcal = 0;
        StepsToward(w, npc, at, Resource.Stick, ForecastAtCamp(w, npc, 1));
    }

    // ---- listed equals stepped ----

    public enum PitKind { None, Cold, Glowing, Burning }

    public sealed record Case(PitKind Pit, (Resource Fuel, double Kg)[] Pack, (Resource Fuel, double Kg)[] Pile, bool Starter, bool Night, bool Cold)
    {
        public override string ToString() =>
            $"pit {Pit}, pack [{string.Join(", ", Pack.Select(p => $"{p.Fuel} {p.Kg:0.00}"))}], pile [{string.Join(", ", Pile.Select(p => $"{p.Fuel} {p.Kg:0.00}"))}], starter {Starter}, night {Night}, cold {Cold}";
    }

    static readonly Resource[] Fuels = [.. Fire.Props.Keys.Order()];

    static readonly Gen<(Resource, double)> FuelGen = Gen.Select(Gen.Int[0, Fuels.Length - 1], Gen.Double[0.05, 6], (i, kg) => (Fuels[i], kg));

    static readonly Gen<Case> Cases = Gen.Select(Gen.Enum<PitKind>(), FuelGen.Array[0, 4], FuelGen.Array[0, 3], Gen.Bool, Gen.Bool, Gen.Bool,
        (pit, pack, pile, starter, night, cold) => new Case(pit, pack, pile, starter, night, cold));

    /// Random packs, piles and pits at camp: a StartFire or AddFuel it chooses is never refused, and holding a fire plan
    /// with no fire burning, it strikes or goes to fetch.
    [Fact]
    public void ListedEqualsStepped()
    {
        Cases.Check(c =>
        {
            var (w, npc) = Alone(-10);
            var camp = Map.Of(w).Camp;
            if (c.Night) Nightfall(w);
            var pack = MindOf(npc).Pack;
            if (!c.Starter) pack.Gear.RemoveAll(g => Fire.Starters.Any(t => Items.Def(g).Tool == t));
            foreach (var (r, kg) in c.Pack) Items.AddKg(pack, r, kg);
            foreach (var (r, kg) in c.Pile) Items.AddKg(Items.PileAt(w, camp), r, kg);
            TestKit.Give(pack, Resource.Water, 2);
            if (c.Pit == PitKind.Cold) Fire.AddFuel(Pit(w, camp), Resource.BirchBranch, 2);
            if (c.Pit == PitKind.Glowing)
            {
                var embers = Pit(w, camp);
                (embers.HasEmbers, embers.EmberDurationHours, embers.EmberHoursRemaining, embers.EmberStartTempF) = (true, 1, 1, 600);
            }
            if (c.Pit == PitKind.Burning) Burning(w, camp, Resource.Stick, 0.5);
            npc.Body.CoreTempF = c.Cold ? 96.0 : 98.6;
            var (a, _) = Npc.Decide(w, npc, []);
            var why = $"{c}: chose {a}";
            if (a is StartFire or AddFuel) Assert.True(Sim.Refusal(w, a) == null, $"{why}, refused: {Sim.Refusal(w, a)}");
            if (MindOf(npc).Plan?.Stat == Stat.Warmth && !(Fire.CurrentPitAt(w, camp) is { } p && (Fire.IsActive(p) || p.HasEmbers)))
                Assert.True(a is StartFire or Travel or Gather or PickUp or PutDown, $"{why}, holding a warmth plan with no fire");
        }, iter: 150);
    }
}
