namespace Survival.Tests;

/// Scent (spec "Scent"): a source's scent level x the animal's sense of smell x the wind sets how many
/// tiles it is smelled from: Scent.FullReachTiles in calm air for a full-strength source to a nose of 1,
/// about twice that straight downwind and half straight upwind. Sight and scent are one sensing
/// function (Senses): what either finds, the animal finds.
public class ScentTests
{
    static readonly TileId Source = new(20, 20);

    /// A nose of the given sense of smell standing dx, dy off the source, blind, in this wind.
    static bool Smells(int dx, int dy, double scentLevel = 1, double smell = 1, WindDirection wind = WindDirection.Calm) =>
        new Senses(new TileId(Source.X + dx, Source.Y + dy), SightBudgetTiles: 0, smell, wind).Smells(Source, scentLevel);

    [Fact]
    public void InCalmAir_AFullScent_ReachesSixTiles_TheSameInEveryDirection()
    {
        Assert.Equal(6, Scent.FullReachTiles);
        foreach (var (dx, dy) in new[] { (6, 0), (-6, 0), (0, 6), (0, -6), (3, 3), (-3, 3) })
            Assert.True(Smells(dx, dy), $"{dx},{dy}");
        foreach (var (dx, dy) in new[] { (7, 0), (-7, 0), (0, 7), (0, -7), (4, 3), (-3, -4) })
            Assert.False(Smells(dx, dy), $"{dx},{dy}");
    }

    /// A north wind comes from the north and carries scent south (North is up, y - 1).
    [Fact]
    public void Downwind_ItReachesTwiceAsFar_AndUpwindHalfAsFar()
    {
        var north = WindDirection.North;
        Assert.True(Smells(0, 12, wind: north));   // south of the source: downwind
        Assert.False(Smells(0, 13, wind: north));
        Assert.True(Smells(0, -3, wind: north));   // north of it: upwind
        Assert.False(Smells(0, -4, wind: north));
        Assert.True(Smells(6, 0, wind: north));    // across the wind: as in calm air
        Assert.False(Smells(7, 0, wind: north));
        Assert.Equal(2, Scent.WindFactor(WindDirection.West, Source, new TileId(30, 20)), 6);
        Assert.Equal(0.5, Scent.WindFactor(WindDirection.West, Source, new TileId(10, 20)), 6);
        Assert.Equal(1, Scent.WindFactor(WindDirection.Calm, Source, new TileId(10, 20)), 6);
    }

    /// Diagonal winds and lines fall between: the factor runs smoothly from x0.5 to x2 with the angle.
    [Fact]
    public void TheWindFactor_RunsWithTheAngle()
    {
        var se = new TileId(25, 25);
        Assert.Equal(2, Scent.WindFactor(WindDirection.Northwest, Source, se), 6);
        double half = Scent.WindFactor(WindDirection.North, Source, se);
        Assert.InRange(half, 1.5, 1.7); // 2^cos 45
        Assert.Equal(1, Scent.WindFactor(WindDirection.North, Source, Source), 6);
    }

    [Fact]
    public void AStrongerScent_AndAKeenerNose_ReachFurther()
    {
        Assert.False(Smells(4, 0, scentLevel: 0.5));
        Assert.True(Smells(3, 0, scentLevel: 0.5));
        Assert.True(Smells(12, 0, smell: 2));
        Assert.False(Smells(13, 0, smell: 2));
        Assert.False(Smells(1, 0, scentLevel: 0));
    }

    [Fact]
    public void WhatIsSeen_IsSensed_ThoughItGivesNoScent()
    {
        var w = TestWorlds.Bare(17);
        var at = w.Agent(AgentId.Player).Tile;
        var senses = new Senses(at, SightBudgetTiles: 11, SmellFactor: 1, WindDirection.Calm);
        var near = new TileId(at.X + 1, at.Y);
        Assert.Equal(Sight.Sees(w, at, near, 11), senses.Finds(w, near, scentLevel: 0));
        Assert.True(senses.Finds(w, at, scentLevel: 0));
        Assert.False(new Senses(at, 0, 1, WindDirection.Calm).Finds(w, near, scentLevel: 0));
    }

    /// A person's scent: their species' base, then blood and the food they carry (spec "Scent").
    [Fact]
    public void APerson_SmellsOfTheirBase_TheirBlood_AndTheFoodTheyCarry()
    {
        var w = TestWorlds.Bare(17);
        var me = w.Agent(AgentId.Player);
        foreach (var r in Items.Pack(w, me.Id).Kg.Keys.ToList()) Items.Take(Items.Pack(w, me.Id), r, double.MaxValue);
        double clean = Scent.Of(w, me);
        Assert.Equal(SpeciesTable.Of(Species.Human).ScentLevel, clean, 6);
        Assert.InRange(clean, 0.01, 0.2); // low for people

        Items.Add(Items.Pack(w, me.Id), Resource.CookedMeat, 1);
        double meat = Scent.Of(w, me);
        Assert.True(meat > clean + 0.3, $"{meat} carrying cooked meat, {clean} with nothing");

        Items.Take(Items.Pack(w, me.Id), Resource.CookedMeat, 1);
        Effects.Add(me.Body, EffectKind.Bleeding, 0.5);
        Assert.True(Scent.Of(w, me) >= 0.9, "bleeding is among the strongest");
    }

    /// The spec's order: meat and fish strong, dried meat less, berries low, nuts and roots near nothing.
    [Fact]
    public void Foods_SmellInTheSpecsOrder()
    {
        double Of(Resource r) => Scent.OfFood(r);
        Assert.True(Of(Resource.RawMeat) >= 0.5 && Of(Resource.CookedMeat) >= 0.5 && Of(Resource.RawFish) >= 0.5 && Of(Resource.CookedFish) >= 0.5);
        Assert.True(Of(Resource.DriedMeat) < Of(Resource.CookedMeat) && Of(Resource.DriedMeat) > Of(Resource.Berries));
        Assert.True(Of(Resource.Berries) > Of(Resource.Nuts) && Of(Resource.Nuts) <= 0.05 && Of(Resource.Roots) <= 0.05);
        Assert.Equal(0, Of(Resource.Stone));
    }

    // ---- through the world (seam 1) ----

    /// 22:00 (world minute 0 is 09:00): on the pack's stalking clock, a wolf's active dusk long gone.
    const long Night = 13 * 60;

    /// The strip's middle row, the person a test is about in the middle of it.
    static TileId T(int x) => new(x, 2);

    /// A fresh wind of about 13 mph from the west, so scent carries east; or calm air.
    static WeatherSample Sky(WindDirection from, WeatherCondition condition = WeatherCondition.Clear, double tempC = -5) =>
        new(0, condition, tempC, from == WindDirection.Calm ? 0 : 0.3, 0, 0.2, from == WindDirection.Calm ? WindDirection.North : from);

    /// PlannerTests.Strip's corridor, three rows of this ground between mountains, nothing growing.
    static World Strip(WeatherSample sky, Terrain ground = Terrain.Plain)
    {
        int width = PlannerTests.Width, height = 5;
        var terrain = new Terrain[width, height];
        for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++) terrain[x, y] = y == 0 || y == height - 1 ? Terrain.Mountain : ground;
        var map = new GeneratedMap(width, height, terrain, new Cave[width, height], new int?[width, height], [], [], new(0, 0),
            new() { [new(width - 1, 0)] = 5 });
        var w = new World { Seed = 1, GeneratedMap = map, FixedWeather = sky, PoolCache = [] };
        for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++) w.PoolCache[new TileId(x, y)] = [];
        w.Minute = Night;
        return w;
    }

    static Agent StarvingPack(World w, TileId at)
    {
        var pack = Agents.SpawnGroup(w, Species.Wolf, at, 3);
        foreach (var wolf in pack)
        {
            CombatTests.Normalize(w, wolf);
            TestKit.SetHunger(w.Agent(wolf), 0.85);
        }
        return w.Agent(pack[0]);
    }

    static Agent Bleeding(World w, TileId at)
    {
        var person = w.Agent(Npc.Spawn(w, at));
        Effects.Add(person.Body, EffectKind.Bleeding, 0.5);
        return person;
    }

    /// Fourteen tiles off, past a wolf's eleven of sight: downwind of a bleeding person it smells them (6 x 1 x 2.5 x 2 = 30
    /// tiles) and heads for them; upwind it smells them only 7.5 tiles off, and does not.
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void AStarvingPack_DownwindOfABleedingPerson_BeyondItsSight_ComesForThem_AndUpwindDoesNot(bool downwind)
    {
        var w = Strip(Sky(WindDirection.West));
        var person = Bleeding(w, T(20));
        var pack = StarvingPack(w, T(downwind ? 34 : 6));
        Assert.False(Senses.Of(w, pack).Sees(w, person.Tile));
        var (activity, _) = Fauna.Decide(w, pack, []);
        var toward = downwind ? Direction.West : Direction.East;
        Assert.Equal(downwind, activity is Travel t && t.Dir == toward);
        Assert.Equal(downwind, Senses.Of(w, pack).Finds(w, person.Tile, Scent.Of(w, person)));
    }

    /// In calm air the same bleeding person is smelled fourteen tiles off from either side (15 tiles).
    [Theory]
    [InlineData(34)]
    [InlineData(6)]
    public void InCalmAir_APackSmellsTheSameDistance_EitherSide(int packAt)
    {
        var w = Strip(Sky(WindDirection.Calm));
        Assert.Equal(WindDirection.Calm, Weather.At(w, w.Minute).WindDirection);
        var person = Bleeding(w, T(20));
        var pack = StarvingPack(w, T(packAt));
        var (activity, _) = Fauna.Decide(w, pack, []);
        Assert.True(activity is Travel t && t.Dir == (packAt > 20 ? Direction.West : Direction.East), $"it chose {activity}");
    }

    /// Fourteen tiles downwind: a person with a kilo of cooked meat reaches a wolf (6 x 0.6 x 2.5 x 2 = 18 tiles), one
    /// carrying nothing only 3 tiles.
    [Fact]
    public void APersonCarryingCookedMeat_IsSmelledFromFurther_ThanOneCarryingNothing()
    {
        var w = Strip(Sky(WindDirection.West));
        var person = w.Agent(Npc.Spawn(w, T(20)));
        var pack = StarvingPack(w, T(34));
        var pack0 = Items.PackOf(person)!;
        foreach (var r in pack0.Kg.Keys.ToList()) Items.Take(pack0, r, double.MaxValue);
        Assert.False(Senses.Of(w, pack).Finds(w, person.Tile, Scent.Of(w, person)));
        Items.Add(pack0, Resource.CookedMeat, 1);
        Assert.True(Senses.Of(w, pack).Finds(w, person.Tile, Scent.Of(w, person)));
    }

    /// In a whiteout among trees a caribou cannot see the next tile (11 x 0.1 = 1.1 tiles of budget, the half of its own
    /// forest tile a ray crosses to leave it costs 2), so it has only its nose: a person a tile upwind it smells (6 x 0.1 x 1.5 x 2 = 1.8 tiles) and
    /// runs from; one a tile downwind (0.45 tiles) it does not know of until they are on its tile. With nothing to eat
    /// anywhere it has no plan holding it, so it gives way from a threat it senses beside it (Fauna.GiveWay).
    [Theory]
    [InlineData(true, 1)]
    [InlineData(false, 0)]
    public void Caribou_FleeAPersonApproachingFromUpwind_SoonerThanFromDownwind(bool fromUpwind, int fleesAtTiles)
    {
        int? fledAt = null;
        for (int d = 3; d >= 0 && fledAt == null; d--)
        {
            var w = Strip(Sky(WindDirection.West, WeatherCondition.Whiteout, -15), Terrain.Forest);
            w.Minute = Night + 5 * 60; // 03:00, on its sensing clock
            var caribou = PlannerTests.Animal(w, Species.Caribou, T(20));
            TestKit.SetHunger(caribou, 0.85); // hungry enough to weigh food it cannot find
            w.Agent(Npc.Spawn(w, T(fromUpwind ? 20 - d : 20 + d)));
            Assert.False(Senses.Of(w, caribou).Sees(w, T(21)));
            if (Fauna.Decide(w, caribou, []).Activity is Travel) fledAt = d;
        }
        Assert.Equal(fleesAtTiles, fledAt);
    }
}

