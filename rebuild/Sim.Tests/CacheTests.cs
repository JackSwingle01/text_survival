namespace Survival.Tests;

/// Caches (#125): a cache lets in the abilities it lists (one access check, Access.GetsInto), an animal without hands breaks in
/// once and leaves it torn open, and a person takes from it with no extra time.
public class CacheTests
{
    static readonly AgentId P = AgentId.Player;

    static TileId T(int x) => new(x, 2);

    static readonly WeatherSample Calm = new(0, WeatherCondition.Clear, -5, 0, 0, 0.2, WindDirection.Calm);

    /// The strip under half a metre of snow, deep enough to bury in (shelters.json buryDepthM 0.3), with nobody on it.
    static World SnowyStrip()
    {
        var w = PlannerTests.Strip(Calm);
        TestKit.EditGround(w, T(20), (g, _) => g.Layers.Add(new SurfaceLayer { Material = SurfaceMaterial.Snow, ThicknessM = 0.5, CompactionPct = 0.3, FrozenFractionPct = 1 }));
        return w;
    }

    static CacheId CacheOfMeat(World w, TileId tile, CacheKind kind, int pieces = 3)
    {
        var id = Caches.Place(w, tile, kind);
        TestKit.GivePieces(w.Caches[id].Contents, Resource.RawMeat, pieces);
        return id;
    }

    static double Meat(World w, CacheId id) => w.Caches.TryGetValue(id, out var c) ? Items.Have(c.Contents, Resource.RawMeat) : 0;

    static Agent Hungry(World w, Species species, TileId at, double hunger = 0.7)
    {
        var a = PlannerTests.Animal(w, species, at);
        TestKit.SetHunger(a, hunger);
        return a;
    }

    static LocationFeature Feature(World w, CacheId id) => new(LocationFeatureKind.Cache, w.Caches[id].Tile, id.Value);

    [Theory]
    [InlineData(Species.Bear, true)]
    [InlineData(Species.Wolf, false)]
    public void ABear_DigsUpMeatBuriedInSnow_AWolfDoesNot(Species species, bool digs)
    {
        var w = SnowyStrip();
        var id = CacheOfMeat(w, T(20), CacheKind.BuriedInSnow, pieces: 30);
        double meat = Meat(w, id);
        Hungry(w, species, T(20));
        Sim.Advance(w, 60);
        Assert.Equal(digs, Meat(w, id) < meat);
        Assert.Equal(digs, w.Caches[id].TornOpen);
    }

    [Theory]
    [InlineData(CacheKind.IceCrevasse)]
    [InlineData(CacheKind.RockCleft)]
    public void NothingGetsIntoAFoundCache_ButAPerson(CacheKind kind)
    {
        foreach (var species in new[] { Species.Bear, Species.CaveBear, Species.Wolf, Species.Fox, Species.Raven, Species.Hyena })
        {
            var w = SnowyStrip();
            var id = CacheOfMeat(w, T(20), kind);
            double meat = Meat(w, id);
            // The same meat in a pile beside it: the animal is feeding, so the cache's meat is kept out, not passed over.
            var pile = Items.PileAt(w, T(20), new Cause(P, "put down by the player"));
            TestKit.GivePieces(pile, Resource.RawMeat, 3);
            double piled = Items.Have(pile, Resource.RawMeat);
            var animal = Hungry(w, species, T(20), hunger: 0.9);
            Assert.False(Access.GetsInto(w, animal.Id, Feature(w, id)), $"{species} gets into the {kind}");
            Sim.Advance(w, 60);
            Assert.True((w.Tiles.GetValueOrDefault(T(20))?.Pile is { } left ? Items.Have(left, Resource.RawMeat) : 0) < piled, $"{species} did not feed");
            Assert.Equal(meat, Meat(w, id));
            Assert.False(w.Caches[id].TornOpen);
        }
        var person = SnowyStrip();
        var mine = CacheOfMeat(person, T(20), kind);
        PlayerAt(person, T(20));
        Assert.True(Access.GetsInto(person, P, Feature(person, mine)));
    }

    /// Each site cache stands where locations.json puts it: the Ice Crevasse at its own location, the Rock Cleft at Sheltered Valley.
    [Fact]
    public void TheFoundCaches_StandAtTheirSites()
    {
        var w = TestWorlds.Bare(3); // seed 3 lays a site with a cache
        var placed = Map.Of(w).Sites.SelectMany(s => s.Value.All.OfType<SiteCache>().Select(c => (Tile: s.Key, c.Cache))).ToList();
        Assert.NotEmpty(placed);
        foreach (var (tile, kind) in placed) Assert.Contains(Caches.At(w, tile), c => w.Caches[c].Kind == kind);
        Assert.Equal(placed.Count, w.Caches.Count);
        var sites = Content.Tables.Locations.ToDictionary(l => l.Id, l => l.All.OfType<SiteCache>().Select(c => c.Cache).ToList());
        Assert.Equal([CacheKind.IceCrevasse], sites["ice_crevasse"]);
        Assert.Equal([CacheKind.RockCleft], sites["sheltered_valley"]);
    }

    /// A pile and a buried cache of the same meat, five tiles either side, the cache on the side a tie goes to: the break-in is
    /// counted in the forecast, so the bear goes to the pile; with the cache torn open, nothing to break, it goes to the cache.
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void WithAPileAndACacheEquallyFar_AnAnimalTakesThePileFirst(bool tornOpen)
    {
        var w = SnowyStrip();
        TestKit.EditGround(w, T(15), (g, _) => g.Layers.Add(new SurfaceLayer { Material = SurfaceMaterial.Snow, ThicknessM = 0.5, CompactionPct = 0.3, FrozenFractionPct = 1 }));
        TestKit.GivePieces(Items.PileAt(w, T(25), new Cause(P, "put down by the player")), Resource.RawMeat, 3);
        var id = CacheOfMeat(w, T(15), CacheKind.BuriedInSnow);
        w.Caches[id].TornOpen = tornOpen;
        var bear = Hungry(w, Species.Bear, T(20), hunger: 0.85);
        Fauna.MindOf(bear).Sightings = [new Sensed(null, T(25), 0, Pile: true), new Sensed(null, T(15), 0, Cache: id)]; // both found before
        var pile = Assert.IsType<Source>(Fauna.PileOrCacheSource(w, bear, T(25)));
        var cache = Assert.IsType<Source>(Fauna.PileOrCacheSource(w, bear, T(15)));
        Assert.Equal(pile.PerMinute, cache.PerMinute, 9);
        Assert.Equal(tornOpen ? 0 : Caches.Row(CacheKind.BuriedInSnow).BreakInMinutes, cache.SetupMinutes);
        Fauna.Decide(w, bear, []);
        Assert.Equal(tornOpen ? T(15) : T(25), Fauna.MindOf(bear).Plan?.At);
        if (tornOpen) return;
        double PileMeat() => w.Tiles.GetValueOrDefault(T(25))?.Pile is { } p ? Items.Have(p, Resource.RawMeat) : 0;
        double piled = PileMeat(), cached = Meat(w, id);
        Sim.Advance(w, 60);
        Assert.True(PileMeat() < piled, "the bear did not eat at the pile");
        Assert.Equal(cached, Meat(w, id));
        Assert.False(w.Caches[id].TornOpen);
    }

    /// A fox digs for five minutes (breakInMinutes), eating nothing, then the cache lies torn open and it eats; a wolf then gets
    /// in too, until a person repairs it.
    [Fact]
    public void ABreakIn_TakesItsTime_ThenLeavesTheCacheTornOpen_UntilAPersonRepairsIt()
    {
        var w = SnowyStrip();
        var id = CacheOfMeat(w, T(20), CacheKind.BuriedInSnow, pieces: 6);
        double meat = Meat(w, id);
        var fox = Hungry(w, Species.Fox, T(20), hunger: 0.9);
        int breakIn = Caches.Row(CacheKind.BuriedInSnow).BreakInMinutes;
        Sim.Advance(w, breakIn - 1);
        Assert.Equal(meat, Meat(w, id));
        Assert.False(w.Caches[id].TornOpen);
        Sim.Advance(w, 1);
        Assert.True(w.Caches[id].TornOpen);
        Sim.Advance(w, 1);
        Assert.True(Meat(w, id) < meat);

        Agents.Remove(w, fox.Id);
        var wolf = Hungry(w, Species.Wolf, T(20), hunger: 0.9);
        Assert.True(Access.GetsInto(w, wolf.Id, Feature(w, id)));
        PlayerAt(w, T(20));
        var repair = Assert.Single(Verbs.At(w, P).LocationFeatures.Single(t => t.LocationFeature == Feature(w, id)).Menu, v => v.Activity is RepairCache);
        Sim.Step(w, repair.Activity, 1);
        Assert.False(w.Caches[id].TornOpen);
        Assert.False(Access.GetsInto(w, wolf.Id, Feature(w, id)));
        w.Agents.Remove(P);
        double left = Meat(w, id);
        Sim.Advance(w, 30);
        Assert.Equal(left, Meat(w, id));
    }

    /// Through Verbs and Sim.Step: the take is the menu's, done in its one minute, with no break-in; burying puts it back.
    [Fact]
    public void APerson_TakesFromACache_WithNoBreakIn_AndBuriesFoodInSnow()
    {
        var w = SnowyStrip();
        var id = CacheOfMeat(w, T(20), CacheKind.BuriedInSnow);
        double meat = Meat(w, id);
        var me = PlayerAt(w, T(20));
        var pack = Items.PackOf(me)!;
        double carried = Items.Have(pack, Resource.RawMeat);
        var take = Assert.Single(Verbs.At(w, P).LocationFeatures.Single(t => t.LocationFeature == Feature(w, id)).Menu, v => v.Activity is PickUp);
        long before = w.Minute;
        var events = Sim.Step(w, take.Activity, 1).Events;
        Assert.Equal(before + 1, w.Minute);
        Assert.Contains(events, e => e is PickedUp { Kg: > 0 });
        Assert.Equal(carried + meat, Items.Have(pack, Resource.RawMeat), 9);
        Assert.False(w.Caches.ContainsKey(id)); // emptied, the snow is only snow

        Items.Add(pack, Resource.Water, Items.CapacityKg(pack)); // a full pack: the take is refused, not spilled
        TestKit.GivePieces(w.Caches[Caches.Place(w, T(20), CacheKind.RockCleft)].Contents, Resource.RawMeat, 1);
        var cleft = Assert.Single(Caches.At(w, T(20)));
        Assert.Equal("your pack is full", Work.Refusal(w, new PickUp(P, Resource.RawMeat, 0.5, cleft))?.Reason);
        Items.Take(pack, Resource.Water, Items.CapacityKg(pack));

        var bury = Assert.Single(Verbs.At(w, P).Pack, v => v.Activity is Bury { Resource: Resource.RawMeat });
        Sim.Step(w, bury.Activity, 1);
        var buried = Assert.IsType<CacheId>(Caches.BuriedAt(w, T(20)));
        Assert.Equal(Math.Min(carried + meat, Caches.Row(CacheKind.BuriedInSnow).CapacityKg), Meat(w, buried), 9);
    }

    /// A cache with room left, but less than the least piece carried, refuses the put-in and the bury: Items.Move
    /// moves only whole pieces, so the minute would be spent moving nothing.
    [Fact]
    public void ACacheWithRoomForLessThanAWholePiece_RefusesThePutIn_AndTheBury()
    {
        var w = SnowyStrip();
        var me = PlayerAt(w, T(20));
        var pack = Items.PackOf(me)!;
        double pieceKg = Items.Resources[Resource.RawMeat].MaxPieceKg;
        Items.AddPiece(pack, Resource.RawMeat, pieceKg);
        foreach (var kind in new[] { CacheKind.RockCleft, CacheKind.BuriedInSnow })
            Items.Add(w.Caches[Caches.Place(w, T(20), kind)].Contents, Resource.Water, Caches.Row(kind).CapacityKg - pieceKg / 2);
        var cleft = Assert.Single(Caches.At(w, T(20)), c => w.Caches[c].Kind == CacheKind.RockCleft);
        string piece = Names.PieceWord(Resource.RawMeat);
        Assert.Equal($"no room in the {Names.Of(CacheKind.RockCleft)} for a whole {piece}", Work.Refusal(w, new PutDown(P, Resource.RawMeat, pieceKg, cleft))?.Reason);
        Assert.Equal($"no room in the snow cache here for a whole {piece}", Work.Refusal(w, new Bury(P, Resource.RawMeat, pieceKg))?.Reason);
    }

    /// Calm air, six tiles: a bear smells 3 kg of raw meat in a pile (6 x 0.6 x 3 = 10.8 tiles), but buried in snow the scent
    /// is x0.2 (2.2 tiles), and it does not.
    [Fact]
    public void BuriedFood_IsSmelledThroughItsScentPct()
    {
        var w = SnowyStrip();
        var id = CacheOfMeat(w, T(26), CacheKind.BuriedInSnow);
        TestKit.GivePieces(Items.PileAt(w, T(14), new Cause(P, "put down by the player")), Resource.RawMeat, 3);
        var bear = Hungry(w, Species.Bear, T(20), hunger: 0.85);
        var senses = Senses.Now(w, bear);
        Assert.True(senses.Smells(T(14), Scent.OfFoodIn(w.Tiles[T(14)].Pile!)));
        Assert.Equal(Scent.OfFoodIn(w.Caches[id].Contents) * Caches.Row(CacheKind.BuriedInSnow).ScentPct, Scent.OfCache(w.Caches[id]), 9);
        Assert.False(senses.Smells(T(26), Scent.OfCache(w.Caches[id])));
        Fauna.Decide(w, bear, []);
        var seen = Fauna.MindOf(bear).Sightings ?? [];
        Assert.Contains(seen, s => s.Pile && s.Tile == T(14));
        Assert.DoesNotContain(seen, s => s.Cache == id);
    }

    /// In a windy snowfall, meat in the Rock Cleft (wind 0.7, overhead 0.8) feels less wind than in the open and stays dry.
    [Fact]
    public void ACachesCover_AddsToTheTiles()
    {
        var w = PlannerTests.Strip(new WeatherSample(0, WeatherCondition.LightSnow, -5, 0.8, 0.8, 1, WindDirection.West));
        var id = Caches.Place(w, T(20), CacheKind.RockCleft);
        var open = Exposure.Standing(w, T(20), w.Minute, null, default);
        var held = Exposure.InCache(w, w.Caches[id], w.Minute);
        Assert.True(held.WindLevel < open.WindLevel, $"wind {held.WindLevel} in the cleft, {open.WindLevel} in the open");
        Assert.Equal(open.AirF, held.AirF);
        Assert.Equal(Precipitation.Snow, open.Falling);
        Assert.Equal(Precipitation.None, held.Falling);
    }

    static Agent PlayerAt(World w, TileId tile) => w.Agents[P] = new Agent { Id = P, Tile = tile };
}
