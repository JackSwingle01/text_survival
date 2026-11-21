using Survival;
using Xunit;
using Xunit.Abstractions;

/// The NPC review after the trip budget (round four): making camp where it stands only when it truly cannot get
/// home, walks that warm judged by the warmth they reach rather than the trip budget, warming at a lit fire
/// rather than groping beside it, and which herds a lone NPC hunts.
public class NpcWarmingWalkTests(ITestOutputHelper output)
{
    static readonly WeatherSample FrostyNight = new(0, WeatherCondition.Clear, -12, 0.2, 0, 0, WindDirection.North);
    static readonly WeatherSample FrostyDay = new(0, WeatherCondition.Clear, -12, 0.2, 0, 1, WindDirection.North);
    static readonly WeatherSample BitterDay = new(0, WeatherCondition.Clear, -30, 0.6, 0, 1, WindDirection.North);
    static readonly WeatherSample Mild = new(0, WeatherCondition.Clear, 20, 0.0, 0, 1, WindDirection.North);

    /// Minute of a day this many days after the start, at this hour.
    static long At(int day, int hour, int minute = 0) => day * 1440L + (hour - 9) * 60 + minute;

    static void NoHerds(World w)
    {
        foreach (var h in w.Agents.Values.Where(a => a.Mind is HerdMind).Select(a => a.Id).ToList()) w.Agents.Remove(h);
    }

    static FireState LitPit(World w, TileId tile, double kindlingKg)
    {
        var pit = new FireState { Tile = tile, LastUpdatedMinute = w.Minute, Pit = PitType.Open };
        Fire.AddFuel(pit, Resource.Kindling, kindlingKg);
        Fire.IgniteAll(pit);
        w.Fires[new FireId(w.NextFireId++)] = pit;
        return pit;
    }

    // ---- 1. making camp where it stands ----

    /// Seed 7, minute 31793: one tile from its camp's roaring fire, shelter and stash, the walk home reckoned at 63
    /// minutes (every crossing off the tile averaged; the step home is 36) and its core judged against the critical
    /// line, so it made camp where it stood and died making a fire there. Out to day 23, it never moves camp while
    /// the camp it leaves, two tiles off or less, has a fire burning or in embers.
    [Fact]
    public void Seed7_NeverMakesCampBesideTheFireItLeft()
    {
        var w = World.New(7);
        var npc = Npc.Spawn(w, Map.Of(w).Camp);
        var mind = (NpcMind)w.Agent(npc).Mind;
        var camp = mind.Camp;
        var moves = new List<string>();
        while (w.Minute < 23 * 1440L && Body.IsAlive(w.Agent(npc).Body))
        {
            Sim.Advance(w, 1);
            if (mind.Camp == camp) continue;
            var pit = Fire.PitAt(w, camp) is FireId p ? w.Fires[p] : null;
            if (MapGen.Manhattan(camp, mind.Camp) <= 2 && pit != null && (Fire.IsActive(pit) || pit.HasEmbers))
                moves.Add($"{w.Minute}: {camp} -> {mind.Camp}, the old pit {Fire.Phase(pit)}");
            camp = mind.Camp;
        }
        output.WriteLine($"alive {Body.IsAlive(w.Agent(npc).Body)} at minute {w.Minute}; {string.Join("; ", moves)}");
        Assert.Empty(moves);
    }

    /// Eight tiles out in a bitter wind with no furs, already chilled (94.5) and carrying the makings of a fire: the
    /// walk home would have its core under the lethal line - but the fire at camp is burning, so it goes home to it.
    [Fact]
    public void FarOutAndCold_CampFireBurning_WalksHome()
    {
        var (w, npc, mind, far) = FarOut(BitterDay, campFire: true);
        var (a, _) = Npc.Decide(w, w.Agent(npc), []);
        output.WriteLine($"{a}, camp {mind.Camp}");
        Assert.NotEqual(far, mind.Camp);
        var step = Assert.IsType<Travel>(a);
        Assert.True(MapGen.Manhattan(Map.Step(far, step.Dir), mind.Camp) < MapGen.Manhattan(far, mind.Camp), "the step should be toward camp");
    }

    /// The same with a cold camp, no shelter: home is here, where it can make a fire.
    [Fact]
    public void FarOutAndCold_CampCold_MakesCampWhereItCanMakeAFire()
    {
        var (w, npc, mind, far) = FarOut(BitterDay, campFire: false);
        var (a, _) = Npc.Decide(w, w.Agent(npc), []);
        output.WriteLine($"{a}, camp {mind.Camp}");
        Assert.Equal(far, mind.Camp);
        Assert.IsNotType<Travel>(a);
    }

    /// And without the fuel to make one: it does not make camp where it cannot keep a fire.
    [Fact]
    public void FarOutAndCold_NothingToBurn_DoesNotMakeCampThere()
    {
        var (w, npc, mind, far) = FarOut(BitterDay, campFire: false, carriesFuel: false);
        var (a, _) = Npc.Decide(w, w.Agent(npc), []);
        output.WriteLine($"{a}, camp {mind.Camp}");
        Assert.NotEqual(far, mind.Camp);
    }

    /// Two tiles from a cold camp, spent: it walks home all the same.
    [Fact]
    public void TwoTilesOut_Spent_NeverMakesCampThere()
    {
        var (w, npc, mind, far) = FarOut(Mild, campFire: false, tiles: 2);
        w.Agent(npc).Body.EnergyMinutes = 1;
        var (a, _) = Npc.Decide(w, w.Agent(npc), []);
        output.WriteLine($"{a}, camp {mind.Camp}");
        Assert.NotEqual(far, mind.Camp);
    }

    static (World World, AgentId Npc, NpcMind Mind, TileId Far) FarOut(WeatherSample sky, bool campFire, bool carriesFuel = true, int tiles = 8)
    {
        var w = World.New(17);
        w.FixedWeather = sky;
        NoHerds(w);
        w.Minute = At(2, 11);
        var camp = Map.Of(w).Camp;
        var m = Map.Of(w);
        var far = Enumerable.Range(-tiles, 2 * tiles + 1).Select(dx => new TileId(camp.X + dx, camp.Y + (tiles - Math.Abs(dx))))
            .First(t => m.InBounds(t) && m.TerrainAt(t).IsPassable() && !Traversal.IsHazardous(w, t));
        var npc = Npc.Spawn(w, far, camp, boldness: 0.5);
        var mind = (NpcMind)w.Agent(npc).Mind;
        w.Agent(npc).Body.Worn.Clear(); // no furs: the walk home is a cold one
        w.Agent(npc).Body.CoreTempF = 94.5;
        if (carriesFuel) Items.Add(mind.Pack, Resource.Birch, 4);
        if (campFire)
        {
            LitPit(w, camp, 3);
            mind.Knows.Places[camp] = new TileMemory { SeenMinute = w.Minute, VisitedMinute = w.Minute, Fire = FirePhase.Roaring, FireHours = 3 };
        }
        else mind.Knows.Places[camp] = new TileMemory { SeenMinute = w.Minute, VisitedMinute = w.Minute };
        return (w, npc, mind, far);
    }

    // ---- 2. walks that warm ----

    /// Seed 11's real state at a dead camp (the reviewer's trace): spent (energy 0.10), core 96.3, a frosty night,
    /// a pinch of tinder too small to strike, and the camp it moved from - tinder and nine kilos of birch on its
    /// stash - next door. The trip budget refused the walk (core under the wanted line, energy under the rested
    /// one) and it groped the bare camp ground. It goes back to its makings, and home is there.
    [Theory]
    [InlineData(0.10, 96.3, false)]
    [InlineData(0.45, 96.5, false)]
    [InlineData(0.45, 96.5, true)]
    [InlineData(0.20, 96.5, true)]
    public void DeadCamp_MakingsNextDoor_GoesToThem(double energyPct, double coreF, bool day)
    {
        var (w, npc, mind, camp) = DeadCamp(day ? FrostyDay : FrostyNight, day ? At(30, 11) : At(30, 0, 3), energyPct, coreF);
        var m = Map.Of(w);
        var dir = Enum.GetValues<Direction>().First(d => m.InBounds(Map.Step(camp, d)) && m.TerrainAt(Map.Step(camp, d)).IsPassable()
                                                          && Traversal.Refusal(w, new Travel(npc, d)) == null);
        var next = Map.Step(camp, dir);
        var stash = new Inventory();
        Items.Add(stash, Resource.Tinder, 2);
        Items.Add(stash, Resource.Birch, 9);
        (w.Tiles.TryGetValue(next, out var there) ? there : w.Tiles[next] = new TileState()).Stash = stash;
        mind.Knows.Places[next] = new TileMemory { SeenMinute = w.Minute, VisitedMinute = w.Minute, Stash = new() { [Resource.Tinder] = 2, [Resource.Birch] = 9 } };
        var (a, n) = Npc.Decide(w, w.Agent(npc), []);
        output.WriteLine($"energy {energyPct} core {coreF} {(day ? "day" : "night")}: {a} x{n}, camp {mind.Camp}");
        var step = Assert.IsType<Travel>(a);
        Assert.Equal(next, Map.Step(camp, step.Dir));
        Assert.Equal(next, mind.Camp);
        // And it gets there and has a fire going within the hour.
        bool lit = false;
        for (int i = 0; i < 60 && !lit; i++) lit = Sim.Advance(w, 1).OfType<FireLit>().Any(f => f.Tile == next);
        Assert.True(lit, "no fire lit at the makings within the hour");
    }

    /// By day at a dead camp, tired (energy 0.20, under the rested line every trip wanted) and a little cold, with
    /// nothing to light and no makings anywhere, but timber three tiles off: the walk to the trees is the warming
    /// move (felling gives tinder and logs), held to lasting harm and the legs it has, not the trip budget. Old: no
    /// walk at all, so it groped the camp ground and napped in fifteen-minute stretches.
    [Fact]
    public void DeadCamp_Tired_ByDay_WalksToTheTrees()
    {
        var (w, npc, mind, camp) = DeadCamp(FrostyDay, At(30, 11), 0.20, 96.5);
        var m = Map.Of(w);
        var tree = Enumerable.Range(-3, 7).Select(dx => new TileId(camp.X + dx, camp.Y + (3 - Math.Abs(dx))))
            .First(t => m.InBounds(t) && m.TerrainAt(t).IsPassable());
        mind.Knows.Places[tree] = new TileMemory { SeenMinute = w.Minute, VisitedMinute = w.Minute, Trees = true };
        var (a, n) = Npc.Decide(w, w.Agent(npc), []);
        output.WriteLine($"{a} x{n}, need {mind.Need}");
        var step = Assert.IsType<Travel>(a);
        Assert.True(MapGen.Manhattan(Map.Step(camp, step.Dir), tree) < MapGen.Manhattan(camp, tree), "the step should be toward the trees");
    }

    /// Seed 11, day 30: at camp beside a cold pit with a hand drill, a pinch of tinder too small to strike with and
    /// nothing on the stash. Herds removed; the weather fixed.
    static (World World, AgentId Npc, NpcMind Mind, TileId Camp) DeadCamp(WeatherSample sky, long minute, double energyPct, double coreF)
    {
        var w = World.New(11);
        w.FixedWeather = sky;
        NoHerds(w);
        w.Minute = minute;
        var camp = Map.Of(w).Camp;
        var npc = Npc.Spawn(w, camp);
        var mind = (NpcMind)w.Agent(npc).Mind;
        mind.Pack.Kg.Clear();
        mind.Pack.Kg[Resource.Tinder] = 0.01;
        mind.Pack.Kg[Resource.Water] = 2;
        var body = w.Agent(npc).Body;
        body.EnergyMinutes = energyPct * Body.MaxEnergyMinutes;
        body.CoreTempF = coreF;
        w.Fires[new FireId(w.NextFireId++)] = new FireState { Tile = camp, LastUpdatedMinute = w.Minute };
        return (w, npc, mind, camp);
    }

    // ---- 3. warming at a lit fire ----

    /// Seed 17's camp by day: 0.8 kg of kindling burning, the NPC's reserve in hand and no other wood, a little cold
    /// (96.7) and tired (0.35). Too cold to walk out for wood, it took the wood plan's fallback instead - half an
    /// hour groping the camp ground for fuel, again and again, ahead of feeding the fire and warming at it. The wood
    /// plan counts here only as the walk: what it does at the fire is the fire's own business, a short grope to keep
    /// a small fire fed (feed it or lose it) and warming at it, never the half-hour fuel forage.
    [Fact]
    public void LowFire_ColdAndTired_DoesNotTakeTheWoodPlansGropeForAWalk()
    {
        var w = World.New(17);
        w.FixedWeather = FrostyDay;
        NoHerds(w);
        w.Minute = At(3, 11);
        var camp = Map.Of(w).Camp;
        var npc = Npc.Spawn(w, camp);
        var pit = new FireState { Tile = camp, LastUpdatedMinute = w.Minute, BurningKg = { [Resource.Kindling] = 0.8 } };
        Fire.IgniteAll(pit);
        w.Fires[new FireId(w.NextFireId++)] = pit;
        var mind = (NpcMind)w.Agent(npc).Mind;
        mind.Pack.Kg[Resource.Kindling] = 0.6;
        foreach (var r in new[] { Resource.Pine, Resource.Birch, Resource.Oak }) mind.Pack.Kg.Remove(r);
        var body = w.Agent(npc).Body;
        body.EnergyMinutes = 0.35 * Body.MaxEnergyMinutes;
        body.CoreTempF = 96.7;
        var longGropes = new List<string>();
        Doing? seen = null;
        for (int i = 0; i < 240; i++)
        {
            Sim.Advance(w, 1);
            if (w.Agent(npc).Doing is not { } d || ReferenceEquals(d, seen)) continue;
            seen = d;
            var now = Fire.PitAt(w, camp) is FireId q ? w.Fires[q] : null;
            output.WriteLine($"  {i}: {d.Activity} x{d.MinutesLeft} core {body.CoreTempF:0.00} en {body.EnergyPct:0.00} pit {(now == null ? "-" : $"{Fire.Phase(now)} {Fire.TotalKg(now):0.00}")}");
            if (w.Agent(npc).Tile == camp && d.Activity is Gather { Pool: 0, Focus: ForageFocus.Fuel } && d.MinutesLeft > 15 && now != null && Fire.IsActive(now))
                longGropes.Add($"{i}: x{d.MinutesLeft}");
        }
        output.WriteLine($"core now {body.CoreTempF:0.00}; half-hour gropes beside the lit fire: {string.Join(", ", longGropes)}");
        Assert.Empty(longGropes);
    }

    // ---- 6. which herds it hunts ----

    /// A lone NPC with an axe, the herd next door, firewood enough at camp. Prey that runs (caribou) is worth a stalk
    /// whenever food is wanted; prey that fights back (bison) only when starving; a mammoth never, alone. Old: by
    /// FightOrFlight's size rule every herd animal outsized it (a caribou 4 to its 2), so it hunted nothing until
    /// starving and then anything, and a mammoth herd killed it (seed 123, day 34.9 at 6.9 kg fat).
    [Theory]
    [InlineData(Species.Caribou, 0.5, true)]
    [InlineData(Species.Caribou, 1.5, true)]
    [InlineData(Species.Bison, 0.5, false)]
    [InlineData(Species.Bison, 1.5, true)]
    [InlineData(Species.Mammoth, 0.5, false)]
    [InlineData(Species.Mammoth, 1.5, false)]
    public void WhichHerdsALoneNpcHunts(Species species, double shortKg, bool hunts)
    {
        var w = World.New(17);
        w.FixedWeather = Mild;
        NoHerds(w);
        w.Minute = At(0, 11);
        var camp = Map.Of(w).Camp;
        var npc = Npc.Spawn(w, camp, camp, boldness: 0.5);
        var mind = (NpcMind)w.Agent(npc).Mind;
        Items.Add(Items.StashAt(w, camp), Resource.Oak, 40); // firewood is not what it worries about
        mind.Knows.Places[camp] = new TileMemory { SeenMinute = w.Minute, VisitedMinute = w.Minute, Stash = new() { [Resource.Oak] = 40 } };
        var body = w.Agent(npc).Body;
        body.FatKg = mind.TargetFatKg - shortKg;
        body.CaloriesKcal = Body.MaxCaloriesKcal; // not hungry for what it carries: it is the fat it is short of
        var next = MapGen.Neighbors(camp).Where(t => Map.Of(w).TerrainAt(t).IsPassable() && Map.Blocked(Map.Of(w), camp, t) == null).OrderBy(t => t).First();
        Agents.Spawn(w, species, next, species == Species.Mammoth ? 6 : 3);
        var (a, _) = Npc.Decide(w, w.Agent(npc), []);
        output.WriteLine($"{species} short {shortKg} kg: {a}, need {mind.Need}");
        Assert.Equal(hunts, a is Travel t && Map.Step(camp, t.Dir) == next);
    }
}
