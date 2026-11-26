namespace Survival.Tests;

/// Piles feed animals (#124): food left in a pile is food for every animal whose diet takes it, found by scent or on its
/// tile, eaten until full through the one take operation, and gone for among people as engagement is (Combat.WouldEnter).
public class PileFeedingTests
{
    static readonly AgentId P = AgentId.Player;

    /// The strip's middle row.
    static TileId T(int x) => new(x, 2);

    /// A fresh wind from the west, so scent carries east.
    static readonly WeatherSample Westerly = new(0, WeatherCondition.Clear, -5, 0.3, 0, 0.2, WindDirection.West);

    /// PlannerTests.Strip with nobody on it: a person is put there by the tests about people.
    static World Strip(WeatherSample? sky = null) => PlannerTests.Strip(sky ?? Westerly);

    static Agent PlayerAt(World w, TileId tile) => w.Agents[P] = new Agent { Id = P, Tile = tile };

    static Inventory Pile(World w, TileId tile) => Items.PileAt(w, tile, new Cause(P, "put down by the player"));

    static double Have(World w, TileId tile, Resource r) => w.Tiles.GetValueOrDefault(tile)?.Pile is { } pile ? Items.Have(pile, r) : 0;

    static Agent Hungry(World w, Species species, TileId at, double hunger = 0.6)
    {
        var a = PlannerTests.Animal(w, species, at);
        TestKit.SetHunger(a, hunger);
        return a;
    }

    [Theory]
    [InlineData(Species.Wolf, true, false)]
    [InlineData(Species.Bear, true, true)]
    [InlineData(Species.Caribou, false, false)]
    public void OnAPile_AnAnimalEats_WhatItsDietTakes_AndLeavesTheRest(Species species, bool eatsMeat, bool eatsBerries)
    {
        var w = Strip();
        var tile = T(20);
        TestKit.GivePieces(Pile(w, tile), Resource.RawMeat, 3);
        Items.Add(Pile(w, tile), Resource.Berries, 2);
        double meat = Have(w, tile, Resource.RawMeat);
        Hungry(w, species, tile);
        Sim.Advance(w, 60);
        Assert.Equal(eatsMeat, Have(w, tile, Resource.RawMeat) < meat);
        if (!eatsMeat) Assert.Equal(eatsBerries, Have(w, tile, Resource.Berries) < 2);
        // Richest first, so the berries' own pile shows whether it takes them too.
        var berriesOnly = Strip();
        Items.Add(Pile(berriesOnly, tile), Resource.Berries, 2);
        Hungry(berriesOnly, species, tile);
        Sim.Advance(berriesOnly, 60);
        Assert.Equal(eatsBerries, Have(berriesOnly, tile, Resource.Berries) < 2);
    }

    /// Roots hardly smell (0.02): a bear three tiles off neither senses the pile nor heads for it; on its tile, it eats.
    [Fact]
    public void ABear_DoesNotHeadForRootsItCannotSmell_ButEatsThemOnTheirTile()
    {
        var w = Strip();
        var tile = T(20);
        Items.Add(Pile(w, tile), Resource.Roots, 3);
        var bear = Hungry(w, Species.Bear, T(17));
        Fauna.Decide(w, bear, []);
        Assert.DoesNotContain(Fauna.MindOf(bear).Sightings ?? [], s => s.Pile);
        Assert.NotEqual(tile, Fauna.MindOf(bear).Plan?.At);
        Assert.Null(Fauna.PileSource(w, bear, tile));

        bear.Tile = tile;
        Fauna.MindOf(bear).Plan = null;
        var (activity, _) = Fauna.Decide(w, bear, []);
        Assert.IsType<Feed>(activity);
        Sim.Advance(w, 5);
        Assert.True(Have(w, tile, Resource.Roots) < 3);
    }

    /// Fifteen tiles off, past a wolf's sight: straight downwind it smells 3 kg of raw meat (6 x 0.6 x 2.5 x 2 = 18 tiles) and
    /// heads for it; upwind only 4.5 tiles off, and does not.
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void AWolf_DownwindOfAMeatPile_ComesToIt_AndUpwindDoesNot(bool downwind)
    {
        var w = Strip();
        var tile = T(20);
        TestKit.GivePieces(Pile(w, tile), Resource.RawMeat, 3);
        var wolf = Hungry(w, Species.Wolf, T(downwind ? 35 : 5), hunger: 0.85);
        Assert.False(Senses.Now(w, wolf).Sees(w, tile));
        Fauna.Decide(w, wolf, []);
        Assert.Equal(downwind, Fauna.MindOf(wolf).Plan?.At == tile);
        Assert.Equal(downwind, Fauna.MindOf(wolf).Sightings?.Exists(s => s.Pile && s.Tile == tile) == true);
        if (!downwind) return;
        Sim.Advance(w, 240);
        Assert.True(Have(w, tile, Resource.RawMeat) < 3, "the wolf smelled the meat and did not eat it");
    }

    /// A hungry wolf on 10 kg of meat eats until it is sated and leaves the rest: what left the pile is what it gained, less
    /// its burn, never more than it had room for.
    [Fact]
    public void AnAnimal_EatsToFull_AndTheRestStaysInThePile()
    {
        var w = Strip();
        var tile = T(20);
        TestKit.GivePieces(Pile(w, tile), Resource.RawMeat, 20);
        double meat = Have(w, tile, Resource.RawMeat);
        var wolf = Hungry(w, Species.Wolf, tile, hunger: 0.5);
        var b = wolf.Body;
        double room = Body.CaloriesCapacityKcal(b) - b.CaloriesKcal + Math.Max(0, SpeciesTable.Of(Species.Wolf).FatPct * b.WeightKg - b.FatKg) * Body.KcalPerKgFat;
        double held = b.CaloriesKcal + b.FatKg * Body.KcalPerKgFat, burn = Fauna.BurnKcalPerMinute(b);
        const int minutes = 6 * 60;
        Sim.Advance(w, minutes);
        double eatenKcal = (meat - Have(w, tile, Resource.RawMeat)) * Items.Resources[Resource.RawMeat].KcalPerKg;
        Assert.True(Have(w, tile, Resource.RawMeat) > 0, "the wolf ate the whole pile");
        Assert.True(Fauna.Hunger(wolf) <= 0.15, $"the wolf stopped at hunger {Fauna.Hunger(wolf):0.00}");
        Assert.True(eatenKcal <= room + burn * minutes * 1.5, $"ate {eatenKcal:0} kcal with room for {room:0}");
        Assert.Equal(held + eatenKcal - burn * minutes, b.CaloriesKcal + b.FatKg * Body.KcalPerKgFat, room * 0.1);
    }

    /// What a bear takes carries its cause, "taken by a bear"; a meal of many minutes is one entry in the pile's journal.
    [Fact]
    public void FoodTaken_CarriesItsCause_AndAMealIsOneJournalEntry()
    {
        var w = Strip();
        var tile = T(20);
        TestKit.GivePieces(Pile(w, tile), Resource.RawMeat, 3);
        var bear = Hungry(w, Species.Bear, tile);
        var events = Sim.Advance(w, 30);
        var takes = events.OfType<PickedUp>().ToList();
        Assert.NotEmpty(takes);
        Assert.All(takes, t => Assert.Equal(new Cause(bear.Id, "taken by a bear"), t.By));
        Assert.All(takes, t => Assert.True(t.Taken));
        Assert.Single(w.Journal[tile].OfType<PickedUp>());
    }

    /// Beside an awake person, ravens (no nerve at all) leave a pile alone.
    [Fact]
    public void Ravens_DoNotFeedAtAPile_BesideAnAwakePerson()
    {
        var w = Strip();
        var tile = T(20);
        PlayerAt(w, tile);
        TestKit.GivePieces(Pile(w, tile), Resource.RawMeat, 3);
        double meat = Have(w, tile, Resource.RawMeat);
        foreach (var raven in Agents.SpawnGroup(w, Species.Raven, tile, 4)) TestKit.SetHunger(w.Agent(raven), 0.7);
        Sim.Step(w, new Wait(P), 60);
        Assert.Equal(meat, Have(w, tile, Resource.RawMeat));
    }

    /// A starving bear beside a sleeper's pile at night: with no fire it comes and eats; beside a strong fire it does not.
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AStarvingBear_ComesForAPileBesideASleeper_UnlessAStrongFireBurns(bool fire)
    {
        var w = Strip();
        w.Minute = 13 * 60;
        var tile = T(20);
        PlayerAt(w, tile);
        w.Agent(P).Body.EnergyMinutes = 0.3 * Body.MaxEnergyMinutes;
        TestKit.GivePieces(Pile(w, tile), Resource.RawMeat, 3);
        double meat = Have(w, tile, Resource.RawMeat);
        if (fire) w.Fires[new FireId(w.NextFireId++)] = FireDeterrenceTests.Pit(tile, w.Minute, pineKg: 5);
        Hungry(w, Species.Bear, T(22), hunger: 0.95);
        Sim.Step(w, new Sleep(P), 120);
        Assert.Equal(!fire, Have(w, tile, Resource.RawMeat) < meat);
    }
}
