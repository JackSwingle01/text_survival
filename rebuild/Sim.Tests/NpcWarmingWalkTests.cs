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

    /// Review probe P2b: ten tiles out at -35 in a strong wind, clothed, core 94.6, carrying the makings of a fire. The
    /// camp is cold, but it remembers leaving the fire roaring two days ago, with three hours on it (and, in the
    /// second case, a shelter there). Old (this rebuild): any remembered phase but Cold, or a remembered shelter,
    /// counted as warmth waiting, so it walked the ten tiles home through -35 to a cold pit. Neither is: the fire
    /// burned out days ago, and a shelter without one does not stop a lethal walk. It makes camp where it stands and
    /// has a fire going.
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FarOutAndCold_FireRememberedLongAgo_MakesCampAndAFireWhereItStands(bool shelter)
    {
        var w = World.New(17);
        w.FixedWeather = new WeatherSample(0, WeatherCondition.Clear, -35, 0.8, 0, 1, WindDirection.North);
        NoHerds(w);
        w.Minute = At(2, 11);
        var camp = Map.Of(w).Camp;
        var m = Map.Of(w);
        const int tiles = 10;
        var far = Enumerable.Range(-tiles, 2 * tiles + 1).Select(dx => new TileId(camp.X + dx, camp.Y + (tiles - Math.Abs(dx))))
            .First(t => m.InBounds(t) && m.TerrainAt(t).IsPassable() && !Traversal.IsHazardous(w, t));
        var npc = Npc.Spawn(w, far, camp, boldness: 0.5);
        var mind = (NpcMind)w.Agent(npc).Mind;
        var body = w.Agent(npc).Body;
        body.CoreTempF = 94.6;
        Items.Add(mind.Pack, Resource.Birch, 4);
        mind.Knows.Places[camp] = new TileMemory
        {
            SeenMinute = w.Minute - 2 * 1440, VisitedMinute = w.Minute - 2 * 1440, Fire = FirePhase.Roaring, FireHours = 3,
            Shelter = shelter ? "a lean-to" : null,
        };
        var (a, _) = Npc.Decide(w, w.Agent(npc), []);
        output.WriteLine($"shelter {shelter}: {a}, camp {(mind.Camp == far ? "here" : mind.Camp.ToString())}");
        Assert.Equal(far, mind.Camp);
        Assert.IsNotType<Travel>(a);
        bool lit = false;
        for (int i = 0; i < 120 && !lit && Body.IsAlive(body); i++) lit = Sim.Advance(w, 1).OfType<FireLit>().Any(f => f.Tile == far);
        Assert.True(lit, "no fire made where it stands within two hours");
    }

    /// The camp it remembers: a fire seen burning an hour ago with three hours on it still counts as warmth waiting,
    /// so the far-out NPC walks home to it (the ageing above does not make it strand beside a live fire).
    [Fact]
    public void FarOutAndCold_FireLeftAnHourAgo_WalksHome()
    {
        var (w, npc, mind, far) = FarOut(BitterDay, campFire: true);
        mind.Knows.Places[mind.Camp] = new TileMemory { SeenMinute = w.Minute - 60, VisitedMinute = w.Minute - 60, Fire = FirePhase.Roaring, FireHours = 3 };
        var (a, _) = Npc.Decide(w, w.Agent(npc), []);
        output.WriteLine($"{a}, camp {mind.Camp}");
        Assert.NotEqual(far, mind.Camp);
        Assert.IsType<Travel>(a);
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
    /// one) and it groped the bare camp ground. It goes back to its makings, and home is there. (Kindling on that
    /// stash since the Light fix: tinder and birch alone are not makings, a tinder fire never takes birch.)
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
        Items.Add(stash, Resource.Kindling, 1);
        Items.Add(stash, Resource.Birch, 9);
        (w.Tiles.TryGetValue(next, out var there) ? there : w.Tiles[next] = new TileState()).Stash = stash;
        mind.Knows.Places[next] = new TileMemory { SeenMinute = w.Minute, VisitedMinute = w.Minute, Stash = new() { [Resource.Tinder] = 2, [Resource.Kindling] = 1, [Resource.Birch] = 9 } };
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

    /// Seed 7, day 29 (the reviewer's trace): at a cold camp at night with a hand drill, 0.21 kg of tinder of its own
    /// and no kindling, 5.33 kg of tinder on the camp's stash but no wood, and a woodpile one tile off - 31 kg of oak,
    /// kindling, and a pinch of tinder. Makings were judged by the woodpile's stash alone, so it walked off with its
    /// 0.21, struck at the odds' floor until that was gone, and froze. It takes the camp's tinder (and kindling) for
    /// the walk first: it reaches the woodpile with a kilo of tinder counting the pinch it remembers there.
    [Fact]
    public void WalkToMakings_TakesTheCampsTinderFirst()
    {
        var (w, npc, mind, camp) = DeadCamp(FrostyNight, At(29, 0, 3), 0.4, 96.3);
        Items.Add(Items.StashAt(w, camp), Resource.Tinder, 5.33);
        mind.Pack.Kg[Resource.Tinder] = 0.21;
        mind.Knows.Places[camp] = new TileMemory { SeenMinute = w.Minute, VisitedMinute = w.Minute, Stash = new() { [Resource.Tinder] = 5.3 } };
        var m = Map.Of(w);
        var dir = Enum.GetValues<Direction>().First(d => m.InBounds(Map.Step(camp, d)) && m.TerrainAt(Map.Step(camp, d)).IsPassable()
                                                          && Traversal.Refusal(w, new Travel(npc, d)) == null);
        var next = Map.Step(camp, dir);
        var pile = new Inventory();
        Items.Add(pile, Resource.Oak, 31);
        Items.Add(pile, Resource.Kindling, 1.5);
        Items.Add(pile, Resource.Tinder, 0.05);
        (w.Tiles.TryGetValue(next, out var there) ? there : w.Tiles[next] = new TileState()).Stash = pile;
        mind.Knows.Places[next] = new TileMemory { SeenMinute = w.Minute, VisitedMinute = w.Minute, Stash = new() { [Resource.Oak] = 31, [Resource.Kindling] = 1.5, [Resource.Tinder] = 0.1 } };
        double carried = 0;
        for (int i = 0; i < 60 && Body.IsAlive(w.Agent(npc).Body); i++)
        {
            Sim.Advance(w, 1);
            if (w.Agent(npc).Tile != next) continue;
            carried = Items.Have(mind.Pack, Resource.Tinder);
            break;
        }
        output.WriteLine($"reached the woodpile with {carried:0.00} kg of tinder; camp stash has {Items.Have(Items.StashAt(w, camp), Resource.Tinder):0.00} left; camp now {mind.Camp}");
        Assert.Equal(next, mind.Camp);
        Assert.True(carried >= 0.85, $"reached the woodpile with only {carried:0.00} kg of tinder");
    }

    /// By day at a dead camp, tired (under the rested line every trip wanted) and a little cold, with nothing to light
    /// and no makings anywhere, but timber a few tiles off: the walk to the trees is the warming move (felling gives
    /// tinder and logs), and it lasts until it gets there. Old (this rebuild, review probe P5): the first step was a
    /// warming walk, the second was judged by the ordinary budget, which refused it, so rest won and took it home -
    /// 32 walks of one tile and back in five hours, never reaching the trees, until it lit a fire back at the dead
    /// camp at last; on a bitter day (probe P4) it died within three hours. The timber it walks to is the nearest
    /// it knows, a tile off (it sees it from camp), whatever stand further off it is told of. Followed for five hours:
    /// it reaches standing timber without once turning back to the dead camp, has a fire going there, and is alive.
    [Theory]
    [InlineData(false, 0.20, 96.5, 3)]
    [InlineData(false, 0.45, 96.5, 3)]
    [InlineData(false, 0.20, 96.5, 6)]
    [InlineData(true, 0.60, 96.0, 12)]
    [InlineData(true, 0.25, 95.5, 12)]
    public void DeadCamp_Tired_ByDay_WalksToTheTreesAndMakesAFireThere(bool bitter, double energyPct, double coreF, int tiles)
    {
        var (w, npc, mind, camp) = DeadCamp(bitter ? BitterDay : FrostyDay, At(30, 11), energyPct, coreF);
        var m = Map.Of(w);
        var tree = Enumerable.Range(-tiles, 2 * tiles + 1).Select(dx => new TileId(camp.X + dx, camp.Y + (tiles - Math.Abs(dx))))
            .First(t => m.InBounds(t) && m.TerrainAt(t).IsPassable());
        mind.Knows.Places[tree] = new TileMemory { SeenMinute = w.Minute, VisitedMinute = w.Minute, Trees = true };
        var (left, litAt, backAtCamp) = Follow(w, npc, camp, 300);
        output.WriteLine($"left {left}, lit at {litAt?.ToString() ?? "-"} (trees there {(litAt is TileId l && HasTrees(w, l))}), back at the dead camp {backAtCamp} times, camp now {mind.Camp}");
        Assert.True(left, "it never set out");
        Assert.Equal(0, backAtCamp);
        Assert.NotNull(litAt);
        Assert.True(HasTrees(w, litAt!.Value), $"the fire at {litAt} is not at the trees");
        Assert.True(Body.IsAlive(w.Agent(npc).Body));
    }

    /// A dead camp with no standing timber within seven tiles and some eight to twelve off, by day: frosty, and bitter
    /// (-30, a strong wind). Old (this rebuild, review probe P4): a walk that set out and was judged afresh a tile on,
    /// turned back. If the warming trip fits its own reckoning setting out - there short of the lethal line, with
    /// legs to spare - it goes, never turns back to the camp it left, gets there, lights a fire at the trees and is
    /// alive at the end. If it does not fit, it does not set out. On the bitter day it does not: twelve tiles of
    /// snow take 70 minutes, and the core is at the lethal line in 52.
    [Theory]
    [InlineData(false, 96.5, 0.6)]
    [InlineData(false, 96.5, 0.25)]
    [InlineData(true, 96.0, 0.6)]
    [InlineData(true, 95.5, 0.25)]
    public void DeadCamp_FarTrees_GetsThereOrDoesNotSetOut(bool bitter, double coreF, double energyPct)
    {
        var (w, start, tree) = FarFromTrees();
        w.FixedWeather = bitter ? BitterDay : FrostyDay;
        NoHerds(w);
        w.Minute = At(30, 10);
        var npc = Npc.Spawn(w, start);
        var mind = (NpcMind)w.Agent(npc).Mind;
        mind.Pack.Kg.Clear();
        mind.Pack.Kg[Resource.Tinder] = 0.01;
        mind.Pack.Kg[Resource.Water] = 2;
        var body = w.Agent(npc).Body;
        body.EnergyMinutes = energyPct * Body.MaxEnergyMinutes;
        body.CoreTempF = coreF;
        w.Fires[new FireId(w.NextFireId++)] = new FireState { Tile = start, LastUpdatedMinute = w.Minute };
        mind.Knows.Places[tree] = new TileMemory { SeenMinute = w.Minute, VisitedMinute = w.Minute, Trees = true };
        bool fits = Npc.WarmingTripFits(w, w.Agent(npc), tree);
        var (left, litAt, backAtCamp) = Follow(w, npc, start, 720);
        output.WriteLine($"trees {MapGen.Manhattan(start, tree)} tiles off, {(bitter ? "bitter" : "frosty")}: fits {fits}, left {left}, lit at {litAt?.ToString() ?? "-"}, back {backAtCamp}, alive {Body.IsAlive(body)} core {body.CoreTempF:0.0} en {body.EnergyPct:0.00}");
        if (bitter) Assert.False(fits, "the bitter-day walk was expected not to fit");
        Assert.Equal(fits, left);
        if (!fits) return;
        Assert.Equal(0, backAtCamp);
        Assert.True(Body.IsAlive(body), "it died after setting out");
        Assert.NotNull(litAt);
        Assert.True(HasTrees(w, litAt!.Value), $"the fire at {litAt} is not at the trees");
    }

    static bool HasTrees(World w, TileId t) =>
        Pools.At(w, t).Select((p, i) => (p, i)).Any(x => x.p is TreePool && Pools.HasTrees(w, new PoolId(t, x.i)));

    /// Lives the NPC on for this long or until it has a fire going away from the camp it started at: whether it left
    /// that camp, where its fire is (null if none), and how many times it walked back onto the camp before then.
    static (bool Left, TileId? LitAt, int BackAtCamp) Follow(World w, AgentId npc, TileId camp, int minutes)
    {
        long end = w.Minute + minutes;
        bool left = false;
        int back = 0;
        var was = w.Agent(npc).Tile;
        while (w.Minute < end && Body.IsAlive(w.Agent(npc).Body))
        {
            var lit = Sim.Advance(w, 1).OfType<FireLit>().FirstOrDefault(f => f.By == npc);
            var at = w.Agent(npc).Tile;
            if (at != camp) left = true;
            else if (was != camp) back++;
            was = at;
            if (lit != null && lit.Tile != camp) return (left, lit.Tile, back);
        }
        return (left, null, back);
    }

    /// A passable, safe tile on the first of these worlds with no standing timber within seven tiles and some eight
    /// to twelve off, and the nearest such stand.
    static (World World, TileId Start, TileId Tree) FarFromTrees()
    {
        foreach (int seed in new[] { 42, 777, 5, 11 })
        {
            var w = World.New(seed);
            var m = Map.Of(w);
            var trees = new HashSet<TileId>();
            for (int x = 0; x < m.Width; x++)
                for (int y = 0; y < m.Height; y++)
                    if (HasTrees(w, new TileId(x, y))) trees.Add(new TileId(x, y));
            TileId? Nearest(TileId t, int within)
            {
                for (int d = 0; d <= within; d++)
                    foreach (var c in Enumerable.Range(-d, 2 * d + 1).SelectMany(dx => new[] { new TileId(t.X + dx, t.Y + (d - Math.Abs(dx))), new TileId(t.X + dx, t.Y - (d - Math.Abs(dx))) }).OrderBy(c => c))
                        if (trees.Contains(c)) return c;
                return null;
            }
            for (int x = 0; x < m.Width; x++)
                for (int y = 0; y < m.Height; y++)
                {
                    var t = new TileId(x, y);
                    if (!m.TerrainAt(t).IsPassable() || Traversal.IsHazardous(w, t)) continue;
                    if (Nearest(t, 12) is TileId nearest && MapGen.Manhattan(nearest, t) >= 8) return (w, t, nearest);
                }
        }
        throw new InvalidOperationException("no world has a tile eight to twelve tiles from timber");
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
