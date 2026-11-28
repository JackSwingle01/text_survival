namespace Survival.Tests;

/// Animals eat from fires and racks (#192): what sits at a station, working or finished, is food in the tile's pile by the
/// pile's rule (PileFeedingTests): smelled as what it is now, found, eaten through the one take, and gone for among
/// people only as engagement is.
public class StationFeedingTests
{
    static readonly AgentId P = AgentId.Player;

    /// The strip's middle row.
    static TileId T(int x) => new(x, 2);

    /// A fresh wind from the west, so scent carries east.
    static readonly WeatherSample Westerly = new(0, WeatherCondition.Clear, -5, 0.3, 0, 0.2, WindDirection.West);

    static World Strip() => PlannerTests.Strip(Westerly);

    static Agent Hungry(World w, Species species, TileId at, double hunger = 0.6)
    {
        var a = PlannerTests.Animal(w, species, at);
        TestKit.SetHunger(a, hunger);
        return a;
    }

    /// A rack on the tile with these set on it by the player, each a place, nothing done yet.
    static RackId Rack(World w, TileId tile, params (string Recipe, Resource Input, double Kg)[] on)
    {
        var id = Stations.Build(w, tile);
        foreach (var (recipe, input, kg) in on)
            w.Racks[id].Work.Working.Add(new Transformation { Recipe = recipe, Input = input, InputKg = kg, SetBy = P });
        return id;
    }

    static double Working(World w, RackId rack, Resource r) => w.Racks[rack].Work.Working.Where(t => t.Input == r).Sum(t => t.InputKg);

    static readonly (string, Resource, double) Meat = ("dry_meat", Resource.RawMeat, 1);

    /// Fifteen tiles off, past a wolf's sight: downwind it smells 3 kg of raw meat drying on a rack (18 tiles) and comes and
    /// eats it; upwind only 4.5 tiles off, and does not.
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void AWolf_DownwindOfMeatOnARack_ComesAndEatsIt_AndUpwindDoesNot(bool downwind)
    {
        var w = Strip();
        var tile = T(20);
        var rack = Rack(w, tile, Meat, Meat, Meat);
        var wolf = Hungry(w, Species.Wolf, T(downwind ? 35 : 5), hunger: 0.85);
        Assert.False(Senses.Now(w, wolf).Sees(w, tile));
        Fauna.Decide(w, wolf, []);
        Assert.Equal(downwind, Fauna.MindOf(wolf).Plan?.At == tile);
        Assert.Equal(downwind, Fauna.MindOf(wolf).Sightings?.Exists(s => s.Pile && s.Tile == tile) == true);
        if (!downwind) return;
        Sim.Advance(w, 240);
        Assert.True(Working(w, rack, Resource.RawMeat) < 3, "the wolf smelled the meat on the rack and did not eat it");
    }

    static Agent PlayerAt(World w, TileId tile) => w.Agents[P] = new Agent { Id = P, Tile = tile };

    /// The wolf downwind, a sleeper at the rack: with no fire it comes onto the tile (for the sleeper, as a starving pack
    /// does); beside a strong fire it keeps off, though it smelled the meat, and the meat stays. A fire deters only as
    /// engagement has it, through the people there.
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void TheWolf_DoesNotTakeMeatFromARack_BesideAStrongFire(bool fire)
    {
        var w = Strip();
        w.Minute = 13 * 60;
        var tile = T(20);
        PlayerAt(w, tile);
        w.Agent(P).Body.SleepDebtMinutes = 0.7 * 16 * 60;
        var rack = Rack(w, tile, Meat, Meat, Meat);
        if (fire) w.Fires[new FireId(w.NextFireId++)] = FireDeterrenceTests.Pit(tile, w.Minute, pineKg: 5);
        var wolf = Hungry(w, Species.Wolf, T(35), hunger: 0.85);
        Sim.Step(w, new Sleep(P), 240);
        Assert.Equal(!fire, wolf.Tile == tile);
        if (!fire) return;
        Assert.Equal(3, Working(w, rack, Resource.RawMeat));
        Assert.Contains(Fauna.MindOf(wolf).Sightings ?? [], s => s.Pile && s.Tile == tile);
        Assert.Null(Combat.EncounterOf(w, P));
    }

    static readonly (string, Resource, double) Berries = ("dry_berries", Resource.Berries, 0.5);

    /// Berries drying on a rack: a wolf on the tile leaves them; a bear eats them.
    [Theory]
    [InlineData(Species.Wolf, false)]
    [InlineData(Species.Bear, true)]
    public void AWolf_LeavesBerriesOnARack_ABearEatsThem(Species species, bool eats)
    {
        var w = Strip();
        var tile = T(20);
        var rack = Rack(w, tile, Berries, Berries);
        Hungry(w, species, tile);
        Sim.Advance(w, 60);
        Assert.Equal(eats, Working(w, rack, Resource.Berries) < 1);
    }

    /// A cut cooked at a fire since gone cold waits there: a wolf downwind, past its sight, smells it (1 kg of cooked meat,
    /// 6 x 0.5 x 2.5 x 2 = 15 tiles), comes and eats from it until sated.
    [Fact]
    public void CookedMeat_WaitingAtAColdFire_IsEatenByAWolfThatFindsIt()
    {
        var w = Strip();
        var tile = T(20);
        var pit = new FireId(w.NextFireId++);
        w.Fires[pit] = new FireState { Tile = tile, LastUpdatedMinute = w.Minute };
        var ready = new Inventory();
        Items.AddPiece(ready, Resource.CookedMeat, 1);
        w.Stations[pit] = new StationState { Ready = ready };
        var wolf = Hungry(w, Species.Wolf, T(32), hunger: 0.85);
        Assert.False(Senses.Now(w, wolf).Sees(w, tile));
        Fauna.Decide(w, wolf, []);
        Assert.Equal(tile, Fauna.MindOf(wolf).Plan?.At);
        Sim.Advance(w, 240);
        Assert.True(Items.Have(w.Stations[pit].Ready, Resource.CookedMeat) < 1, "the wolf found the cooked meat and did not eat it");
    }

    /// Fully dried, 1 kg of raw meat is 0.4 kg of dried meat (0.3 x 0.4 = 0.12, 3.6 tiles downwind, against raw meat's 18):
    /// a wolf ten tiles downwind smells the meat drying, and not the meat dried.
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FinishedDriedMeat_IsSmelledFromLessFar_ThanTheRawMeatWas(bool dried)
    {
        var w = Strip();
        var tile = T(20);
        var rack = Rack(w, tile, Meat);
        if (dried)
        {
            w.Racks[rack].Work.Working[0].DonePct = 1 - 1e-6;
            Assert.Contains(Sim.Advance(w, 1), e => e is TransformationFinished { Made: Resource.DriedMeat });
        }
        var wolf = Hungry(w, Species.Wolf, T(30), hunger: 0.85);
        Fauna.Decide(w, wolf, []);
        Assert.Equal(!dried, Fauna.MindOf(wolf).Sightings?.Exists(s => s.Pile && s.Tile == tile) == true);
    }

    /// A bare world's camp, with nothing in the player's pack, which the player has stood on and remembers.
    static World Camp(out TileId camp)
    {
        var w = TestWorlds.FreshPlayer(TestWorlds.Bare(17));
        w.FixedWeather = Westerly;
        camp = Map.Of(w).Camp;
        TestKit.ClearResources(Items.Pack(w, P));
        Sim.Step(w, new Wait(P), 1);
        return w;
    }

    /// The player walks off the camp for a while (the world advanced: a wait would end at the bear in sight), comes back
    /// and stands a minute: the receipt of that minute.
    static Receipt TripAway(World w, TileId camp, int minutes)
    {
        w.Agent(P).Tile = Map.Step(Map.Step(Map.Step(camp, Direction.East), Direction.East), Direction.East);
        Sim.Advance(w, minutes);
        w.Agent(P).Tile = camp;
        return Assert.Single(Sim.Step(w, new Wait(P), 1).Events.OfType<Noticed>()).Receipt;
    }

    /// A bear eats the meat drying on the camp's rack while the player is away: what it took carries "taken by a bear",
    /// and the receipt on return says a bear took it from the rack.
    [Fact]
    public void MeatTakenFromARack_CarriesItsCause_AndTheReceiptSaysWhatTookIt()
    {
        var w = Camp(out var camp);
        Rack(w, camp, ("dry_meat", Resource.RawMeat, 0.5), ("dry_meat", Resource.RawMeat, 0.5));
        var bear = Hungry(w, Species.Bear, camp);
        var r = TripAway(w, camp, 120);
        var took = Assert.Single(r.Happened.OfType<PickedUp>());
        Assert.Equal((new Cause(bear.Id, "taken by a bear"), StationKind.Rack, true), (took.By, took.From, took.Taken));
        Assert.Matches("^A bear takes .*raw meat from the rack\\.$", Narration.Of(w, took)!.Text);
    }

    /// A full rack, two pieces of meat drying and the dried berries of two places waiting: a bear eats it all while the
    /// player is away. The rack still stands, and the player puts four things on it.
    [Fact]
    public void AfterARaid_TheRackStands_AndItsEmptiedPlacesCanBeUsed()
    {
        var w = Camp(out var camp);
        var rack = Rack(w, camp, ("dry_meat", Resource.RawMeat, 0.5), ("dry_meat", Resource.RawMeat, 0.5));
        var work = w.Racks[rack].Work;
        Items.Add(work.Ready, Resource.DriedBerries, 0.3);
        work.ReadyAmounts = 2;
        TestKit.GivePieces(Items.Pack(w, P), Resource.RawMeat, 4);
        Assert.NotNull(Stations.Refusal(w, new PutOn(P, "dry_meat", rack))); // full
        Hungry(w, Species.Bear, camp, hunger: 0.9);
        TripAway(w, camp, 240);
        Assert.True(w.Racks.ContainsKey(rack));
        Assert.Empty(work.Working);
        Assert.False(Items.HasResources(work.Ready), "the bear left food on the rack");
        for (int i = 0; i < Stations.RackRoom; i++)
            Assert.Equal(Stations.HandlingMinutes, Sim.Step(w, new PutOn(P, "dry_meat", rack), 1).MinutesElapsed);
        Assert.Equal(Stations.RackRoom, work.Working.Count);
    }
}
