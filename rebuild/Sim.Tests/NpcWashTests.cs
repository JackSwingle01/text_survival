using CsCheck;
using Survival;
using Survival.Tests.Verification;
using Xunit.Abstractions;

/// The NPC washes (#266): a Want above half Dirty, last on the ladder, and a wash before it idles when dirty at all.
/// Each world is hand-built, with no animals.
public class NpcWashTests(ITestOutputHelper output)
{
    static NpcMind MindOf(Agent a) => (NpcMind)a.Mind;

    /// Warm, by day, at camp: water and a winter's fire on the pile, rested, fed, and no food it knows of, so food's
    /// Want (its least, FoodPriority) has nothing to pursue. No snow or water on the camp tile, nor carried water.
    static (World World, Agent Npc) Calm()
    {
        var w = ConservationWorlds.Bare(15);
        w.Agents.Remove(AgentId.Player);
        var camp = Map.Of(w).Camp;
        var npc = w.Agent(Npc.Spawn(w, camp));
        TestKit.ClearResources(MindOf(npc).Pack);
        w.PoolCache = [];
        for (int dx = -13; dx <= 13; dx++)
            for (int dy = -13; dy <= 13; dy++)
                if (Map.Of(w).InBounds(new TileId(camp.X + dx, camp.Y + dy))) w.PoolCache[new TileId(camp.X + dx, camp.Y + dy)] = [];
        var pile = Items.PileAt(w, camp);
        TestKit.Give(pile, Resource.Water, 4);
        TestKit.Give(pile, Resource.OakBranch, 60);
        TestKit.Give(pile, Resource.Stick, 30);
        TestKit.Give(MindOf(npc).Pack, Resource.Tinder, 0.5);
        Assert.True(Sim.Refusal(w, new Wash(npc.Id)) != null, "the camp tile has washing water");
        Assert.True(Weather.IsDaytime(w.Minute));
        return (w, npc);
    }

    static void Dirty(Agent npc, double severity) => Effects.Set(npc.Body, EffectKind.Dirty, severity);

    static void SnowOn(World w, TileId t) => TestKit.EditGround(w, t, (g, sub) => Ground.AddSnow(g, sub, 0.1));

    static TileId EastOfCamp(World w, int tiles)
    {
        var camp = Map.Of(w).Camp;
        var at = new TileId(camp.X + tiles, camp.Y);
        Assert.True(Map.Of(w).TerrainAt(at).IsPassable());
        return at;
    }

    [Fact]
    public void AboveHalfDirty_NothingElseToDo_ItWashesWhereItStands()
    {
        var (w, npc) = Calm();
        SnowOn(w, npc.Tile);
        Dirty(npc, 0.7);
        var (a, _) = Npc.Decide(w, npc, []);
        Assert.IsType<Wash>(a);
        Assert.Equal(Stat.Dirty, MindOf(npc).Plan?.Stat);
    }

    [Fact]
    public void AboveHalfDirty_NothingElseToDo_ItWalksToSnowAndWashes()
    {
        var (w, npc) = Calm();
        var snow = EastOfCamp(w, 2);
        SnowOn(w, snow);
        Memories.Glance(w, npc, []);
        Dirty(npc, 0.7);
        var (first, _) = Npc.Decide(w, npc, []);
        output.WriteLine($"{first}; plan {MindOf(npc).Plan}");
        Assert.IsType<Travel>(first);
        Assert.Equal(Stat.Dirty, MindOf(npc).Plan?.Stat);
        var log = NpcDriver.Live(w, npc.Id, w.Minute + 120);
        output.WriteLine(string.Join("\n", log.Where(e => e is Moved or Washed)));
        var washed = log.OfType<Washed>().First(e => e.Who == npc.Id);
        Assert.NotEqual(Map.Of(w).Camp, washed.Tile);
        Assert.Equal(WashedIn.Snow, washed.In);
    }

    [Fact]
    public void AboveHalfDirty_AnotherStatAtWant_ThatStatComesFirst()
    {
        var (w, npc) = Calm();
        SnowOn(w, npc.Tile);
        Dirty(npc, 0.7);
        Items.Take(Items.PileAt(w, npc.Tile), Resource.Water, double.MaxValue);
        var water = EastOfCamp(w, 2);
        Memories.Of(npc).Places[water] = new TileMemory { SeenMinute = w.Minute, VisitedMinute = w.Minute, Water = true };
        var (a, _) = Npc.Decide(w, npc, []);
        output.WriteLine($"{a}; plan {MindOf(npc).Plan}");
        Assert.NotEqual(Stat.Dirty, MindOf(npc).Plan?.Stat);
        Assert.IsNotType<Wash>(a);
    }

    [Fact]
    public void ALittleDirty_NothingToDo_WashesBeforeIdling_WhereItCan()
    {
        var (w, npc) = Calm();
        SnowOn(w, npc.Tile);
        Dirty(npc, 0.3);
        var (a, _) = Npc.Decide(w, npc, []);
        Assert.IsType<Wash>(a);
        Assert.Null(MindOf(npc).Plan);
    }

    [Fact]
    public void ALittleDirty_NothingToDo_WhereItCannotWash_ItIdlesAndMakesNoTrip()
    {
        var (w, npc) = Calm();
        var snow = EastOfCamp(w, 2);
        SnowOn(w, snow);
        Memories.Glance(w, npc, []);
        Dirty(npc, 0.3);
        var (a, _) = Npc.Decide(w, npc, []);
        output.WriteLine($"{a}; plan {MindOf(npc).Plan}");
        Assert.IsType<Rest>(a);
        Assert.Null(MindOf(npc).Plan);
    }

    [Fact]
    public void Clean_ItNeverWashes()
    {
        var (w, npc) = Calm();
        SnowOn(w, npc.Tile);
        Dirty(npc, 0);
        var (a, _) = Npc.Decide(w, npc, []);
        Assert.IsNotType<Wash>(a);
        Assert.NotEqual(Stat.Dirty, MindOf(npc).Plan?.Stat);
    }

    [Fact]
    public void ANeed_InterruptsAWashTrip()
    {
        var (w, npc) = Calm();
        var snow = EastOfCamp(w, 3);
        SnowOn(w, snow);
        Memories.Glance(w, npc, []);
        Dirty(npc, 0.7);
        Assert.IsType<Travel>(Npc.Decide(w, npc, []).Activity);
        Assert.Equal(Stat.Dirty, MindOf(npc).Plan?.Stat);
        npc.Body.HydrationMl = 0.3 * Body.MaxHydrationMl; // water at Need
        var (a, _) = Npc.Decide(w, npc, []);
        output.WriteLine($"{a}; plan {MindOf(npc).Plan}");
        Assert.NotEqual(Stat.Dirty, MindOf(npc).Plan?.Stat);
        Assert.Equal(Resource.Water, Assert.IsType<PickUp>(a).Resource); // the water on the camp pile, in hand
    }

    public sealed record Case(double Dirty, bool SnowHere, bool SnowAway, double CarriedWaterL, int Hour);

    static readonly Gen<Case> Cases = Gen.Select(Gen.Double[0, 1], Gen.Bool, Gen.Bool, Gen.Double[0, 1], Gen.Int[0, 23],
        (d, here, away, water, hour) => new Case(d, here, away, water, hour));

    /// Random dirt, snow, carried water and hours: a Wash it chooses is never refused.
    [Fact]
    public void ListedEqualsStepped()
    {
        Cases.Check(c =>
        {
            var (w, npc) = Calm();
            w.Minute += c.Hour * 60;
            if (c.SnowHere) SnowOn(w, npc.Tile);
            if (c.SnowAway) SnowOn(w, EastOfCamp(w, 2));
            if (c.CarriedWaterL > 0) TestKit.Give(MindOf(npc).Pack, Resource.Water, c.CarriedWaterL);
            Memories.Glance(w, npc, []);
            Dirty(npc, c.Dirty);
            var (a, _) = Npc.Decide(w, npc, []);
            if (a is Wash) Assert.True(Sim.Refusal(w, a) == null, $"{c}: chose a refused wash: {Sim.Refusal(w, a)}");
            if (c.Dirty == 0) Assert.IsNotType<Wash>(a);
        }, iter: 100);
    }
}
