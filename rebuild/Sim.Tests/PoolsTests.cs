namespace Survival.Tests;

/// The merged resource pool (old ForageFeature, HarvestableFeature, WoodedAreaFeature, SalvageFeature,
/// EnvironmentalDetail) and discovery (old HiddenFeature, DiscoveryGenerator). Old WoodedAreaFeatureTests
/// ported; statistical goldens measured by running the old code (harness golden3a).
public class PoolsTests
{
    static readonly AgentId P = AgentId.Player;
    static readonly WeatherSample Mild = new(0, WeatherCondition.Cloudy, 5, 0.1, 0, 0.8, WindDirection.North);

    static World New(int seed = 17)
    {
        var w = World.New(seed);
        w.FixedWeather = Mild;
        return w;
    }

    /// A world whose camp tile holds exactly these pools.
    static World WithPools(params PoolDef[] pools)
    {
        var w = New();
        w.PoolCache = new() { [Map.Of(w).Camp] = pools };
        return w;
    }

    static PoolId Camp(World w, int i = 0) => new(Map.Of(w).Camp, i);

    // ---- generation ----

    [Fact]
    public void Pools_AreAPureFunctionOfSeedAndTile_AndCostTheSaveNothingUntilDrawnFrom()
    {
        var a = New();
        var b = New();
        var tile = new TileId(Map.Of(a).Camp.X + 1, Map.Of(a).Camp.Y);
        Assert.Equal(Pools.At(a, tile), Pools.At(b, tile)); // records: value equality
        // old game: drew terrain forage density from the process-wide RNG, so the same seed gave different tiles (bug, fixed).
        Assert.Equal(((ForagePool)Pools.At(a, tile)[0]).Density, ((ForagePool)Pools.At(Save.FromJson(Save.ToJson(a)), tile)[0]).Density);
        Assert.DoesNotContain("Pools", Save.ToJson(a));
        Assert.NotEqual(Pools.At(a, tile), Pools.At(World.New(18), tile));
    }

    [Fact]
    public void Camp_HasThinMixedForage_AndNoTimberOrHiddenFinds()
    {
        var w = New();
        var pools = Pools.At(w, Map.Of(w).Camp);
        var forage = Assert.IsType<ForagePool>(Assert.Single(pools));
        Assert.Equal(0.38, forage.Density);
        var forest = Enumerable.Range(0, Map.DefaultWidth).Select(x => new TileId(x, Map.Of(w).Camp.Y))
            .First(t => t != Map.Of(w).Camp && Map.Of(w).TerrainAt(t) == Terrain.Forest && Map.Of(w).StageAt(t) == null && Map.Of(w).CaveAt(t) == Cave.None);
        Assert.IsType<TreePool>(Pools.At(w, forest)[1]);
    }

    /// Statistical golden: old DiscoveryGenerator over 20000 forest seeds spawned a berry bush 7.995%
    /// of the time, revealed at a mean 1.0002 h (exponential, capped at ten times the expected).
    [Fact]
    public void Discoveries_SpawnAndRevealLikeTheOldPools()
    {
        var m = Map.Generate(17);
        var forest = Enumerable.Range(0, m.Width).SelectMany(x => Enumerable.Range(0, m.Height).Select(y => new TileId(x, y)))
            .Where(t => m.TerrainAt(t) == Terrain.Forest && t != m.Camp && m.StageAt(t) == null && m.CaveAt(t) == Cave.None).ToList();
        var bushes = forest.SelectMany(t => Pools.Generate(17, m, t)).OfType<StockPool>().Where(p => p.Name == "Wild Berry Bush").ToList();
        double rate = bushes.Count / (double)forest.Count;
        Assert.InRange(rate, 0.08 * 0.85, 0.08 * 1.15);
        Assert.InRange(bushes.Average(b => b.RevealAtHours), 0.85, 1.15);
        Assert.All(bushes, b => Assert.InRange(b.RevealAtHours, 0, 10.0));
    }

    /// old game: the hunter's blind spear rolled durability as a share of an unlimited maximum (-1),
    /// which always came to 0: every one was found broken (bug, fixed).
    [Fact]
    public void AHuntersBlindSpear_IsFoundWhole()
    {
        var m = Map.Generate(17);
        var spears = Enumerable.Range(0, m.Width).SelectMany(x => Enumerable.Range(0, m.Height).Select(y => new TileId(x, y)))
            .SelectMany(t => Pools.Generate(17, m, t)).OfType<StockPool>().Where(p => p.Gear?.Any(g => g.Kind == "wooden_spear") == true).ToList();
        Assert.NotEmpty(spears);
        Assert.All(spears.SelectMany(p => p.Gear!), g => Assert.True(Items.Works(g)));
    }

    // ---- forage ----

    /// Statistical golden: the old ForageFeature at density 1.0 (mixed forest), one call a minute,
    /// over 4000 trials: 5.2956 kg in the first hour (3.5905 wood, 0.2575 food), 8.1236 kg in hours 2-3.
    [Fact]
    public void Forage_YieldsAndThinsLikeTheOld()
    {
        int trials = 2000;
        double hour1 = 0, wood = 0, food = 0, later = 0;
        var rng = Rng.ForSystem(5, "test");
        var w = WithPools(new ForagePool("test", 1.0, Pools.MixedForest));
        for (int t = 0; t < trials; t++)
        {
            w.Tiles.Remove(Map.Of(w).Camp); // fresh ground each trial
            for (int m = 0; m < 180; m++)
                foreach (var (r, kg) in Pools.Forage(w, Camp(w), 1.0 / 60, rng))
                {
                    if (m >= 60) { later += kg; continue; }
                    hour1 += kg;
                    if (r is Resource.Pine or Resource.Birch or Resource.Oak) wood += kg;
                    if (Items.Resources[r].Category == ResourceCategory.Food) food += kg;
                }
        }
        Assert.InRange(hour1 / trials, 5.2956 * 0.95, 5.2956 * 1.05);
        Assert.InRange(wood / trials, 3.5905 * 0.93, 3.5905 * 1.07);
        Assert.InRange(food / trials, 0.2575 * 0.85, 0.2575 * 1.15);
        Assert.InRange(later / trials, 8.1236 * 0.95, 8.1236 * 1.05);
    }

    [Fact]
    public void ForageDensity_ThinsAsWorked_AndRegrowsOverFourWeeks()
    {
        // Abundant enough that every minute finds something: the old ground only thinned when a
        // search turned something up (kept quirk), so a sparse table thins far slower than 1/(1 + 2h).
        var w = WithPools(new ForagePool("test", 1.0, [new ForageItem(Resource.Kindling, 600, 0.1, 0.1)]));
        var id = Camp(w);
        var rng = Rng.ForSystem(1, "test");
        for (int i = 0; i < 60; i++) Pools.Forage(w, id, 1.0 / 60, rng);
        double thinned = Pools.ForageDensity(w, id);
        Assert.Equal(1.0 / 3, thinned, 9);
        w.Minute += 336 * 60;
        Assert.Equal(thinned + 0.5 * (1 - thinned), Pools.ForageDensity(w, id), 9);
        w.Minute += 400 * 60;
        Assert.Equal(1.0, Pools.ForageDensity(w, id), 9);
    }

    /// old game: a search kept the raw hours-foraged total and reset the regrowth clock, so one
    /// minute's find on a fully regrown tile thinned it straight back to where it was (bug, fixed).
    [Fact]
    public void ASearchAfterRegrowth_StartsFromTheRegrownGround()
    {
        var w = WithPools(new ForagePool("test", 1.0, [new ForageItem(Resource.Kindling, 600, 0.1, 0.1)]));
        var id = Camp(w);
        var rng = Rng.ForSystem(1, "test");
        for (int i = 0; i < 180; i++) Pools.Forage(w, id, 1.0 / 60, rng);
        Assert.True(Pools.ForageDensity(w, id) < 0.2);
        w.Minute += 700 * 60; // past full regrowth
        Pools.Forage(w, id, 1.0 / 60, rng);
        Assert.True(Pools.ForageDensity(w, id) > 0.95, $"one minute after regrowth the density is {Pools.ForageDensity(w, id)}");
    }

    [Fact]
    public void Grazing_ThinsOneResource_AndRegrows()
    {
        var w = WithPools(new ForagePool("test", 1.0, Pools.MixedForest));
        Pools.SetGrazed(w, Camp(w), Resource.Berries, 0.5);
        Assert.Equal(0.5, Pools.GrazedLevel(w, Camp(w), Resource.Berries));
        w.Minute += 336 * 60 / 2;
        Assert.Equal(0.25, Pools.GrazedLevel(w, Camp(w), Resource.Berries), 9);
        Assert.Equal(0, Pools.GrazedLevel(w, Camp(w), Resource.Roots));
    }

    // ---- stock (harvestables, salvage, details) ----

    static StockPool Bush(double respawnHours = 168, ToolType? tool = null) =>
        new("Bush", StockKind.Harvest, 5, [new StockItem(Resource.Berries, 5, 0.4, respawnHours)], RevealAtHours: 0, RequiresTool: tool);

    [Fact]
    public void Stock_GivesAUnitACycle_AndRegrowsAUnitPerRespawnTime()
    {
        var w = WithPools(Bush());
        var id = Camp(w);
        var got = Pools.WorkStock(w, id, 12);
        Assert.Equal(0.8, Items.Have(got, Resource.Berries), 9); // two whole cycles of five minutes
        Assert.Equal(3, Pools.UnitsNow(w, id)[0]);
        Pools.WorkStock(w, id, 15);
        Assert.False(Pools.HasStock(w, id));
        w.Minute += 168 * 60 * 2;
        Assert.Equal(2, Pools.UnitsNow(w, id)[0]);
        w.Minute += 168 * 60 * 10;
        Assert.Equal(5, Pools.UnitsNow(w, id)[0]); // never above its max
    }

    /// Regrowth is counted once: worked a minute at a time a week after it was picked clean, the
    /// bush gives the one unit that grew back, not that unit again every minute it is worked.
    [Fact]
    public void Stock_RegrowthIsCountedOnce_HoweverManyMinutesItIsWorked()
    {
        var w = WithPools(Bush());
        var id = Camp(w);
        Pools.WorkStock(w, id, 25);
        Assert.False(Pools.HasStock(w, id));
        w.Minute += 168 * 60;
        Assert.Equal(1, Pools.UnitsNow(w, id)[0]);
        var got = new Inventory();
        for (int minute = 0; minute < 30; minute++) Items.AddAll(got, Pools.WorkStock(w, id, 1));
        Assert.Equal(0.4, Items.Have(got, Resource.Berries), 9);
        Assert.False(Pools.HasStock(w, id));
        // And the clock still runs: two more weeks, two more units.
        w.Minute += 2 * 168 * 60;
        Assert.Equal(2, Pools.UnitsNow(w, id)[0]);
    }

    /// old game: a respawn time of 0 was meant as "never" but refilled the stock every tick (bug, fixed).
    [Fact]
    public void Stock_ThatNeverRegrows_StaysEmpty()
    {
        var w = WithPools(Bush(respawnHours: 0));
        Pools.WorkStock(w, Camp(w), 30);
        w.Minute += 10_000;
        Assert.Equal(0, Pools.UnitsNow(w, Camp(w))[0]);
    }

    // ---- trees (old WoodedAreaFeatureTests) ----

    [Fact]
    public void Chopping_AccumulatesAndPersists_ThenFellsAndResets()
    {
        var w = WithPools(new TreePool("Trees", Resource.Birch, 100));
        var id = Camp(w);
        var rng = Rng.ForSystem(1, "test");
        Assert.Null(Pools.Chop(w, id, 40, rng));
        Assert.Equal(0.4, Pools.ChopProgressPct(w, id), 9);
        var loaded = Save.FromJson(Save.ToJson(w));
        loaded.PoolCache = w.PoolCache;
        Assert.Equal(0.4, Pools.ChopProgressPct(loaded, id), 9);
        var tree = Pools.Chop(w, id, 60, rng)!;
        Assert.Equal(0, Pools.ChopProgressPct(w, id));
        Assert.InRange(Items.Have(tree, Resource.Birch), 8 * 2.0 * 0.9, 10 * 2.0 * 1.1); // 8-10 logs, all birch
        Assert.True(Items.Have(tree, Resource.Kindling) > 0 && Items.Have(tree, Resource.Tinder) > 0);
        Assert.True(Pools.HasTrees(w, id)); // unlimited
    }

    /// Old WoodedAreaFeature: a limited stand counts down, and regrows only once cleared, a week a tree,
    /// and only to one tree (Update stops once a tree stands). Kept quirk.
    [Fact]
    public void ALimitedStand_ClearsAndRegrowsOneTreeAWeek()
    {
        var w = WithPools(new TreePool("Grove", null, 10, Trees: 2));
        var id = Camp(w);
        var rng = Rng.ForSystem(1, "test");
        Pools.Chop(w, id, 10, rng);
        Assert.True(Pools.HasTrees(w, id));
        Pools.Chop(w, id, 10, rng);
        Assert.False(Pools.HasTrees(w, id));
        w.Minute += 167 * 60;
        Assert.False(Pools.HasTrees(w, id));
        w.Minute += 60;
        Assert.True(Pools.HasTrees(w, id));
        Pools.Chop(w, id, 10, rng);
        Assert.False(Pools.HasTrees(w, id));
    }

    /// Statistical golden: old WoodedAreaFeature.FellTree over 20000 mixed trees: 8.9956 logs,
    /// 24.307 kg of logs, 1.751 kg of sticks, 0.1376 kg of tinder; pine 39.7%, birch 35.1%, oak 25.2%.
    [Fact]
    public void FelledTrees_YieldLikeTheOld()
    {
        var w = WithPools(new TreePool("Trees", null, 1));
        var rng = Rng.ForSystem(2, "test");
        int n = 8000;
        double logs = 0, sticks = 0, tinder = 0;
        var types = new Dictionary<Resource, int>();
        for (int i = 0; i < n; i++)
        {
            var y = Pools.Chop(w, Camp(w), 1, rng)!;
            foreach (var r in new[] { Resource.Pine, Resource.Birch, Resource.Oak })
                if (Items.Have(y, r) > 0) { logs += Items.Have(y, r); types[r] = types.GetValueOrDefault(r) + 1; }
            sticks += Items.Have(y, Resource.Kindling);
            tinder += Items.Have(y, Resource.Tinder);
        }
        Assert.InRange(logs / n, 24.307 * 0.97, 24.307 * 1.03);
        Assert.InRange(sticks / n, 1.751 * 0.97, 1.751 * 1.03);
        Assert.InRange(tinder / n, 0.1376 * 0.97, 0.1376 * 1.03);
        Assert.InRange(types[Resource.Pine] / (double)n, 0.37, 0.43);
        Assert.InRange(types[Resource.Oak] / (double)n, 0.22, 0.28);
    }

    // ---- discovery is the agent's knowledge ----

    [Fact]
    public void AHiddenPool_CannotBeWorkedUntilFound_AndOnlyItsFinderKnowsIt()
    {
        var bush = Bush() with { RevealAtHours = 0.5 };
        var w = WithPools(new ForagePool("forage", 0.5, Pools.MixedForest), bush);
        var r = Sim.Step(w, new Gather(P, 1), 10);
        Assert.Equal(("nothing like that here", 0), (r.InterruptedReason, r.MinutesElapsed));
        var found = Sim.Step(w, new Gather(P, 0), 60);
        var discovered = Assert.Single(found.Events.OfType<PoolDiscovered>());
        Assert.Equal(Camp(w, 1), discovered.Pool);
        Assert.InRange(discovered.Minute, 25, 40); // half an hour of perception-weighted search, roughly
        Assert.True(Pools.Knows(w, P, Camp(w, 1)));
        Assert.Equal("the Bush is picked clean", Sim.Step(w, new Gather(P, 1), 60).InterruptedReason);

        var other = new Agent { Id = new AgentId(3), Tile = Map.Of(w).Camp, Mind = new PlayerMind() };
        w.Agents[other.Id] = other;
        Assert.False(Pools.Knows(w, other.Id, Camp(w, 1)));
        Assert.Contains("\"Discovered\":[\"", Save.ToJson(w));
    }

    /// Old BuriedDiscoveryTests.DeeperBurialSlowsNewSearchingButNeverRevaluesEarnedEffort: snow over a
    /// find slows the search for it, and effort already earned stays earned.
    [Fact]
    public void Snow_SlowsTheSearch_ButKeepsEffortAlreadySpent()
    {
        var bush = Bush() with { RevealAtHours = 100 };
        var bare = WithPools(new ForagePool("forage", 0.5, Pools.MixedForest), bush);
        var snowy = WithPools(new ForagePool("forage", 0.5, Pools.MixedForest), bush);
        var camp = Map.Of(snowy).Camp;
        var sub = Ground.Substrate(Map.Of(snowy), camp);
        var ground = Ground.Baseline(sub);
        snowy.Tiles[camp] = new TileState { Ground = ground }; // observed: the ground now lives on the tile
        Ground.AddSnow(ground, sub, 0.1); // about a metre of fresh snow
        Pools.Search(bare, bare.Agent(P), 1.0, []);
        Pools.Search(snowy, snowy.Agent(P), 1.0, []);
        double Hours(World w) => ((PersonMind)w.Agent(P).Mind).Knows.SearchHours[Camp(w, 1)];
        Assert.Equal(1.0, Hours(bare), 9);
        Assert.InRange(Hours(snowy), 0.099, 0.99); // buried: down to the 0.1 floor
        double earned = Hours(snowy);
        Ground.AddSnow(ground, sub, 0.1);
        Assert.Equal(earned, Hours(snowy), 12);
    }
}

/// Named sites (Content/locations.json) as pools: the old site's features on the old tile.
public class SitePoolsTests
{
    static readonly AgentId P = AgentId.Player;

    [Fact]
    public void EverySite_PutsItsForageFirst_ItsVisibleStockInSight_AndKeepsTheTerrainsHiddenFinds()
    {
        var w = World.New(17);
        var m = Map.Of(w);
        foreach (var (t, site) in m.Sites)
        {
            var pools = Pools.At(w, t);
            var features = Map.SiteFeatures(w.Seed, m, t);
            if (features.FirstOrDefault() is SiteForage f)
            {
                var forage = Assert.IsType<ForagePool>(pools[0]);
                Assert.Equal(f.Density, forage.Density);
                Assert.Equal(f.Items, forage.Items);
            }
            var inSight = features.OfType<SiteStock>().Where(s => s.RevealAtHours == 0).Select(s => s.Name).ToList();
            for (int i = 0; i < pools.Count; i++)
                if (pools[i] is StockPool s && inSight.Contains(s.Name))
                    Assert.True(Pools.Knows(w, P, new PoolId(t, i)), $"{site.Name}: {s.Name} should be in plain sight");
            Assert.Equal(inSight.Count, pools.OfType<StockPool>().Count(s => s.RevealAtHours == 0 && s.Name != "Cave Seep"
                && features.OfType<SiteWater>().All(x => x.Name != s.Name)));
        }
    }

    [Fact]
    public void SearchingASite_FindsOnlyWhatWasHidden()
    {
        var w = World.New(17);
        var m = Map.Of(w);
        var (tile, _) = m.Sites.First(s => Pools.At(w, s.Key).Any(p => p is StockPool { RevealAtHours: 0 }));
        w.Agent(P).Tile = tile;
        var events = new List<WorldEvent>();
        Pools.Search(w, w.Agent(P), 1000, events);
        var found = events.OfType<PoolDiscovered>().Select(e => Pools.Def(w, e.Pool)).ToList();
        Assert.All(found, d => Assert.True(Pools.IsHidden(d)));
        Assert.Equal(Pools.At(w, tile).Count(Pools.IsHidden), found.Count);
    }

    [Fact]
    public void ASalvage_IsRolledOnce_FromTheSeedAndTile()
    {
        for (int seed = 1; seed <= 40; seed++)
        {
            var m = Map.Generate(seed);
            if (m.Sites.FirstOrDefault(s => s.Value.All.Any(f => f is SiteStock { Work: StockKind.Salvage })) is not { Value: not null } hit) continue;
            var a = Pools.At(World.New(seed), hit.Key).OfType<StockPool>().First(p => p.Kind == StockKind.Salvage);
            var b = Pools.At(World.New(seed), hit.Key).OfType<StockPool>().First(p => p.Kind == StockKind.Salvage);
            Assert.Equal(a.Items, b.Items);
            Assert.Equal(a.Gear?.Select(g => g.Kind), b.Gear?.Select(g => g.Kind));
            Assert.Equal(0, a.RevealAtHours);
            return;
        }
        Assert.Fail("no salvage site in 40 seeds");
    }

    [Fact]
    public void OpenWaterAtASite_IsASourceToDrawFrom()
    {
        for (int seed = 1; seed <= 40; seed++)
        {
            var m = Map.Generate(seed);
            foreach (var (t, site) in m.Sites)
                if (Map.SiteFeatures(seed, m, t).OfType<SiteWater>().FirstOrDefault(x => !x.Frozen) is { } water)
                {
                    var pool = Pools.At(World.New(seed), t).OfType<StockPool>().Single(p => p.Name == water.Name);
                    Assert.Equal(Resource.Water, Assert.Single(pool.Items).Resource);
                    Assert.False(Pools.IsHidden(pool));
                    return;
                }
        }
        Assert.Fail("no open water site in 40 seeds");
    }

    [Fact]
    public void CavePassages_HaveTheOldCaveForage_AMouthCatchesSticks()
    {
        var w = World.New(17);
        var m = Map.Of(w);
        var tiles = Enumerable.Range(0, m.Width).SelectMany(x => Enumerable.Range(0, m.Height).Select(y => new TileId(x, y))).ToList();
        var floor = tiles.First(t => m.CaveAt(t) == Cave.Floor && m.SiteAt(t) == null);
        var mouth = tiles.First(t => m.CaveAt(t) == Cave.Entrance);
        var f = Assert.IsType<ForagePool>(Pools.At(w, floor)[0]);
        Assert.Equal(0.52, f.Density);
        Assert.Contains(f.Items, i => i.Resource == Resource.Shale);
        Assert.DoesNotContain(f.Items, i => i.Resource == Resource.Kindling);
        Assert.Contains(((ForagePool)Pools.At(w, mouth)[0]).Items, i => i.Resource == Resource.Kindling);
        Assert.DoesNotContain(Pools.At(w, floor), p => p is TreePool);
        Assert.True(Map.IsDark(m, floor));
        Assert.False(Map.IsDark(m, mouth));
    }

    [Fact]
    public void SiteShelters_StandFromTheStart_CampKeepsOneOverhang()
    {
        var w = World.New(17);
        var m = Map.Of(w);
        foreach (var (t, site) in m.Sites)
            if (site.All.OfType<SiteShelter>().FirstOrDefault() is { } s)
            {
                var placed = Shelters.At(w, t);
                Assert.NotNull(placed);
                Assert.Equal((s.Name, s.Type, s.Insulation), (placed!.Name, placed.Kind, placed.InsulationLevel));
            }
            else Assert.Null(Shelters.At(w, t));
        Assert.Equal("Overhang", Shelters.At(w, m.Camp)!.Name);
    }
}
