namespace Survival.Tests;

/// Work activities through Sim.Step: one code path for anyone with a pack. Old WorkRunner,
/// ConsumptionHandler and CookingHandler rules, and the old bugs the port fixes.
public class WorkTests
{
    static readonly AgentId P = AgentId.Player;
    static readonly WeatherSample Mild = new(0, WeatherCondition.Cloudy, 5, 0.1, 0, 0.8, WindDirection.North);

    static World WithPools(params PoolDef[] pools)
    {
        var w = TestWorlds.Bare(17);
        w.FixedWeather = Mild;
        w.PoolCache = new() { [Map.Of(w).Camp] = pools };
        return w;
    }

    static readonly ForagePool Forage = new("forage", 1.0, Pools.MixedForest);
    static readonly TreePool Trees = new("Trees", Resource.Birch, 100);
    static StockPool Bush() => new("Bush", StockKind.Harvest, 5, [new StockItem(Resource.Berries, 5, 0.4, 168)], RevealAtHours: 0);

    static void Know(World w, int pool) => ((PersonMind)w.Agent(P).Mind).Memory.Discovered.Add(new PoolId(Map.Of(w).Camp, pool));
    static void Axe(World w) => Items.Pack(w, P).Gear.Add(Items.Make("stone_axe"));
    static void FastForwardTo(World w, int hour) { while (Weather.MinuteOfDay(w.Minute) / 60 != hour) w.Minute++; }
    static void LightAPit(World w) =>
        w.Fires[new FireId(w.NextFireId++)] = new FireState { Tile = Map.Of(w).Camp, LastUpdatedMinute = w.Minute, BurningKg = { [Resource.Birch] = 5 } };

    // ---- refusals cost nothing ----

    [Theory]
    [InlineData("pool")]
    [InlineData("axe")]
    [InlineData("eat")]
    [InlineData("cook")]
    [InlineData("melt")]
    [InlineData("butcher")]
    public void Refusals_CostNoTime_AndChangeNothing(string what)
    {
        var w = WithPools(Forage, Trees);
        string before = Save.ToJson(w);
        Activity a = what switch
        {
            "pool" => new Gather(P, 7),
            "axe" => new Gather(P, 1),
            "eat" => new Eat(P, Resource.Berries, 1),
            "cook" => new Cook(P, Resource.RawMeat, 1),
            "melt" => new MeltSnow(P),
            _ => new Butcher(P, 3),
        };
        var r = Sim.Step(w, a, 30);
        Assert.Equal(0, r.MinutesElapsed);
        Assert.NotNull(r.InterruptedReason);
        Assert.Equal(before, Save.ToJson(w));
    }

    /// The axe wears by the hour of chopping, not by the step: felling a tree in one go or in
    /// quarter hours takes the same time, gives the same wood and costs the axe the same.
    /// Old ChoppingStrategy charged one use a session, however short.
    [Fact]
    public void Chopping_InOneStepOrInQuarterHours_CostsTheAxeTheSame()
    {
        var one = WithPools(Trees);
        var quarters = WithPools(Trees);
        Items.Pack(one, P).Gear.Add(Items.Make("stone_axe", durability: 10)); // stone axes never wear; this one does
        Items.Pack(quarters, P).Gear.Add(Items.Make("stone_axe", durability: 10));
        var r = Sim.Step(one, new Gather(P, 0), 300);
        int chunked = 0;
        string? last = null;
        while (last != "tree felled") { var q = Sim.Step(quarters, new Gather(P, 0), 15); chunked += q.MinutesElapsed; last = q.InterruptedReason; }
        Assert.Equal(("tree felled", 100, 100), (r.InterruptedReason, r.MinutesElapsed, chunked));
        var axe1 = Items.Tool(Items.Pack(one, P), ToolType.Axe)!;
        var axe2 = Items.Tool(Items.Pack(quarters, P), ToolType.Axe)!;
        Assert.Equal(axe1.Durability, axe2.Durability);
        Assert.Equal(axe1.MaxDurability - 1, axe1.Durability); // one use for 100 minutes: one per hour
        Assert.Equal(Items.Have(Items.Pack(one, P), Resource.Birch), Items.Have(Items.Pack(quarters, P), Resource.Birch), 9);
    }

    /// An hour's search that turns up nothing says so: the session's line is never only what it taught.
    [Fact]
    public void ForagingThatFindsNothing_SaysSo()
    {
        var w = WithPools(new ForagePool("bare", 0.0, []));
        var r = Sim.Step(w, new Gather(P, 0), 60);
        Assert.Equal(60, r.MinutesElapsed);
        var said = r.Events.Select(e => Narration.ForPlayer(w, e)?.Text).Where(t => t != null).ToList();
        // The old failure lines, from the band of the ground's word: bare ground is the lowest band.
        Assert.Contains(said, t => Clues.Table.Failures[^1].Lines.Contains(t));
    }

    /// A pile emptied goes back to null, so a tile with nothing else on it can leave World.Tiles.
    [Fact]
    public void AnEmptiedPile_IsGone()
    {
        var w = WithPools(Forage);
        var camp = Map.Of(w).Camp;
        Sim.Step(w, new PutDown(P, Resource.Kindling, 99), 1);
        Assert.NotNull(w.Tiles[camp].Pile);
        Sim.Step(w, new PickUp(P, Resource.Kindling, 99), 1);
        Assert.False(w.Tiles.ContainsKey(camp)); // nothing else differs here, so the tile goes too
    }

    // ---- gathering ----

    [Fact]
    public void Foraging_FillsThePack_ThinsTheGround_AndBurnsCaloriesLikeWalking()
    {
        var w = WithPools(Forage);
        var rest = WithPools(Forage);
        double before = Items.WeightKg(Items.Pack(w, P));
        var r = Sim.Step(w, new Gather(P, 0), 60);
        Sim.Step(rest, new Rest(P), 60);
        Assert.Equal(60, r.MinutesElapsed);
        Assert.Single(r.Events.OfType<Gathered>());
        Assert.True(Items.WeightKg(Items.Pack(w, P)) > before);
        Assert.True(Pools.ForageDensity(w, new PoolId(Map.Of(w).Camp, 0)) < 1.0);
        Assert.True(w.Agent(P).Body.CaloriesKcal < rest.Agent(P).Body.CaloriesKcal, "foraging (1.5) should burn more than resting");
    }

    /// old game: axe and shovel bonuses were applied only when the total multiplier came out below 1,
    /// so a healthy forager never got the +10% the tutorial promised (bug, fixed).
    [Fact]
    public void AnAxe_AddsTenPercentToForaging_EvenForAHealthyForager()
    {
        var plain = WithPools(Forage);
        var axed = WithPools(Forage);
        Axe(axed);
        double Got(World w) { Sim.Step(w, new Gather(P, 0), 60); return Items.Pack(w, P).Kg.Values.Sum() - 1.08; } // less the starting sticks and tinder
        double a = Got(axed), p = Got(plain);
        Assert.True(p > 0);
        Assert.Equal(p * 1.1, a, 6); // same draws, every find a tenth heavier
    }

    [Fact]
    public void AFocusedSearch_KeepsItsFocus_AndDropsAboutHalfTheRest()
    {
        double Fuel(ForageFocus focus)
        {
            double sum = 0;
            for (int i = 0; i < 20; i++)
            {
                var w = WithPools(new ForagePool("forage", 1.0, Pools.MixedForest));
                w.Rngs[StreamName.Of("work", P)] = Rng.ForSystem(i, "work");
                Sim.Step(w, new Gather(P, 0, focus), 60);
                sum += Items.Pack(w, P).Kg.Where(kv => Items.Resources[kv.Key].Category == ResourceCategory.Fuel).Sum(kv => kv.Value);
            }
            return sum;
        }
        double fuelFocused = Fuel(ForageFocus.Fuel), foodFocused = Fuel(ForageFocus.Food);
        Assert.True(foodFocused < fuelFocused * 0.75, $"fuel kept {fuelFocused:0.0} kg under a fuel focus, {foodFocused:0.0} under a food focus");
    }

    /// The old filter (FocusProcessor.ApplyFocus): a focus keeps its whole category and buys nothing more of it.
    [Fact]
    public void AFocusedSearch_KeepsExactlyWhatAGeneralSearchFindsOfItsCategory_AndNoMoreOfAnything()
    {
        IReadOnlyDictionary<Resource, double> Haul(ForageFocus focus, int seed)
        {
            var w = WithPools(new ForagePool("forage", 1.0, Pools.MixedForest));
            w.Rngs[StreamName.Of("work", P)] = Rng.ForSystem(seed, "work");
            Sim.Step(w, new Gather(P, 0, focus), 60);
            return Items.Pack(w, P).Kg;
        }
        for (int seed = 0; seed < 6; seed++)
        {
            var general = Haul(ForageFocus.General, seed);
            var food = Haul(ForageFocus.Food, seed);
            foreach (var r in general.Keys.Union(food.Keys))
            {
                double g = general.GetValueOrDefault(r), f = food.GetValueOrDefault(r);
                if (Items.Resources[r].Category == ResourceCategory.Food) Assert.Equal(g, f, 9);
                else Assert.True(f <= g + 1e-9, $"{r}: {f} under a food focus, {g} in a general search");
            }
        }
    }

    [Fact]
    public void Chopping_NeedsAnAxe_PersistsAcrossSteps_AndStopsWhenTheTreeFalls()
    {
        var w = WithPools(Forage, Trees);
        Assert.Equal("you need an axe to fell trees", Sim.Step(w, new Gather(P, 1), 60).InterruptedReason);
        Axe(w);
        Assert.Null(Sim.Step(w, new Gather(P, 1), 60).InterruptedReason);
        var r = Sim.Step(w, new Gather(P, 1), 60);
        Assert.Equal(("tree felled", 40), (r.InterruptedReason, r.MinutesElapsed));
        Assert.Single(r.Events.OfType<TreeFelled>());
        Assert.True(Items.Have(Items.Pack(w, P), Resource.Birch) > 0);
    }

    /// old game: interrupted work lost everything it had done, and the impairment factor lengthened
    /// the session but credited the inflated minutes as progress, so bad legs never slowed a chop (bug, fixed).
    [Fact]
    public void ImpairedChopping_IsSlower_AndInterruptedChoppingKeepsItsProgress()
    {
        var healthy = WithPools(Forage, Trees);
        var lame = WithPools(Forage, Trees);
        Axe(healthy); Axe(lame);
        foreach (var leg in lame.Agent(P).Body.Regions.Where(r => r.Name.Contains("Leg"))) foreach (var t in leg.Layers) t.Condition = 0.3;
        Assert.True(Body.GetCapacities(lame.Agent(P).Body).Moving < 0.5);
        Assert.Equal("tree felled", Sim.Step(healthy, new Gather(P, 1), 100).InterruptedReason);
        Assert.Null(Sim.Step(lame, new Gather(P, 1), 100).InterruptedReason);
        Assert.Equal(100 / 1.2 / 100, Pools.ChopProgressPct(lame, new PoolId(Map.Of(lame).Camp, 1)), 9);

        var tired = WithPools(Forage, Trees);
        Axe(tired);
        tired.Agent(P).Body.EnergyMinutes = 30;
        Assert.Equal("exhausted", Sim.Step(tired, new Gather(P, 1), 100).InterruptedReason);
        Assert.Equal(0.30, Pools.ChopProgressPct(tired, new PoolId(Map.Of(tired).Camp, 1)), 9);
    }

    [Fact]
    public void Harvesting_TakesAUnitACycle_UntilPickedClean()
    {
        var w = WithPools(Forage, Bush());
        Know(w, 1);
        var r = Sim.Step(w, new Gather(P, 1), 60);
        Assert.Equal(("the Bush is picked clean", 25), (r.InterruptedReason, r.MinutesElapsed));
        Assert.Equal(2.0, Items.Have(Items.Pack(w, P), Resource.Berries), 9);
        Assert.Equal("the Bush is picked clean", Sim.Step(w, new Gather(P, 1), 60).InterruptedReason);
    }

    /// old game: the player's harvest never checked the tool a harvestable required; only NPCs did (bug, fixed).
    [Fact]
    public void AHarvestThatNeedsATool_IsRefusedWithoutIt()
    {
        var w = WithPools(Forage, Bush() with { RequiresTool = ToolType.Axe });
        Know(w, 1);
        Assert.Equal("you need an axe", Sim.Step(w, new Gather(P, 1), 60).InterruptedReason);
        Axe(w);
        Assert.Equal("the Bush is picked clean", Sim.Step(w, new Gather(P, 1), 60).InterruptedReason);
    }

    [Fact]
    public void Salvage_ComesAllAtOnce_GearIncluded()
    {
        var camp = new StockPool("Camp", StockKind.Salvage, 45, [new StockItem(Resource.Kindling, 1, 0.6)], 0, Gear: [Items.Make("worn_knife")]);
        var w = WithPools(Forage, camp);
        Know(w, 1);
        var r = Sim.Step(w, new Gather(P, 1), 120);
        Assert.Equal(("the Camp is picked clean", 45), (r.InterruptedReason, r.MinutesElapsed));
        Assert.Contains(Items.Pack(w, P).Gear, g => g.Kind == "worn_knife");
    }

    // ---- darkness (old WorkRunner.CheckDarknessBlocking) ----

    [Fact]
    public void AtNight_OnlyForagingWorksWithoutLight()
    {
        var w = WithPools(Forage, Bush());
        Know(w, 1);
        FastForwardTo(w, 22);
        Assert.Equal("too dark to work", Sim.Step(w, new Gather(P, 1), 10).InterruptedReason);
        Assert.Null(Sim.Step(w, new Gather(P, 0), 5).InterruptedReason);
        LightAPit(w);
        Assert.NotEqual("too dark to work", Sim.Step(w, new Gather(P, 1), 10).InterruptedReason);
    }

    /// Darkness is checked every minute of the work, not only when it begins: one long step from
    /// 20:30 stops at 21:00 as half-hour steps do (old: a long session chopped on into the night).
    [Fact]
    public void ChoppingIntoTheNight_StopsAtDark_HoweverTheStepsAreCut()
    {
        World Start()
        {
            var w = WithPools(Trees);
            Axe(w);
            while (Weather.MinuteOfDay(w.Minute) != 20 * 60 + 30) w.Minute++;
            return w;
        }
        var one = Start();
        var r = Sim.Step(one, new Gather(P, 0), 300);
        Assert.Equal(("too dark to work", 30), (r.InterruptedReason, r.MinutesElapsed));
        foreach (int chunk in new[] { 1, 7, 30 })
        {
            var w = Start();
            int minutes = 0;
            string? last = null;
            for (int i = 0; i < 400 && last != "too dark to work"; i++)
            {
                var q = Sim.Step(w, new Gather(P, 0), chunk);
                (minutes, last) = (minutes + q.MinutesElapsed, q.InterruptedReason);
            }
            Assert.Equal(("too dark to work", 30), (last, minutes));
            Assert.Equal(Save.ToJson(one), Save.ToJson(w));
        }
    }

    /// Perception, light and tools are read every minute of foraging, as impairment is: two hours
    /// from 16:00 or into the dusk from 19:00 come out the same in one step, four, or a hundred and twenty,
    /// the search held between its steps so it is one search with one luck.
    [Theory, InlineData(16), InlineData(19)]
    public void Foraging_InOneStepOrMany_ComesOutTheSame(int startHour)
    {
        World Start()
        {
            var w = WithPools(Forage);
            while (Weather.MinuteOfDay(w.Minute) != startHour * 60) w.Minute++;
            return w;
        }
        var one = Start();
        Sim.Step(one, new Gather(P, 0), 120);
        foreach (int chunk in new[] { 30, 1 })
        {
            var w = Start();
            for (int i = 0; i < 120 / chunk; i++) Sim.Step(w, new Gather(P, 0), chunk, more: i < 120 / chunk - 1);
            Assert.Equal(Save.ToJson(one), Save.ToJson(w));
        }
    }

    /// A tile whose last carcass goes, with nothing else on it, leaves World.Tiles (old: only a
    /// track sweep removed it, so it stayed as an empty {} in the save).
    [Fact]
    public void ATileWhoseLastCarcassGoes_IsGone()
    {
        var w = TestWorlds.New(17);
        var tile = new TileId(3, 3);
        Assert.False(w.Tiles.ContainsKey(tile));
        var c = Carcasses.Create(w, tile, "Hare", BodyState.Create(BodyKind.Quadruped, 2, 0.1, 0.5), 0);
        Carcasses.Remove(w, tile, c);
        Assert.False(w.Tiles.ContainsKey(tile));
    }

    // ---- eating, drinking, cooking ----

    [Fact]
    public void Eating_TakesTenMinutes_AndPutsItsCaloriesAndWaterIntoTheBody()
    {
        var w = WithPools(Forage);
        var body = w.Agent(P).Body;
        Items.Add(Items.Pack(w, P), Resource.Berries, 1.0);
        body.CaloriesKcal = 500;
        body.HydrationMl = 1000;
        var r = Sim.Step(w, new Eat(P, Resource.Berries, 0.6), 60);
        Assert.Equal(("ate", 10), (r.InterruptedReason, r.MinutesElapsed));
        var ate = Assert.Single(r.Events.OfType<Ate>());
        Assert.Equal((0.6, 300.0), (ate.Kg, ate.Kcal));
        Assert.Equal(0.4, Items.Have(Items.Pack(w, P), Resource.Berries), 9);
        Assert.True(body.HydrationMl > 1000 + 120 - 30, "berries carry water");
        Assert.Equal("Stone is not food", Sim.Step(w, new Eat(P, Resource.Stone, 1), 10).InterruptedReason);
    }

    [Fact]
    public void Drinking_UsesThePacksWaterFirst_AtMostALitre_AndRefusesWhenThereIsNone()
    {
        var w = WithPools(Forage);
        w.Agent(P).Body.HydrationMl = 1000;
        Assert.Equal("no water here", Sim.Step(w, new Drink(P), 5).InterruptedReason); // camp has no river
        Items.Add(Items.Pack(w, P), Resource.Water, 3);
        var r = Sim.Step(w, new Drink(P, 5), 5);
        Assert.Equal(("drank", 2), (r.InterruptedReason, r.MinutesElapsed));
        Assert.Equal(2, Items.Have(Items.Pack(w, P), Resource.Water), 9);
        Assert.True(w.Agent(P).Body.HydrationMl > 1900);
    }

    [Fact]
    public void ARiverOrLake_GivesWaterWithoutAPack()
    {
        var w = TestWorlds.New(17);
        w.FixedWeather = Mild;
        var m = Map.Of(w);
        var river = Enumerable.Range(0, m.Width).SelectMany(x => Enumerable.Range(0, m.Height).Select(y => new TileId(x, y))).First(m.BesideRiver);
        w.Agent(P).Tile = river;
        w.Agent(P).Body.HydrationMl = 1000;
        Assert.Equal("drank", Sim.Step(w, new Drink(P), 5).InterruptedReason);
        Assert.True(w.Agent(P).Body.HydrationMl > 1900);
    }

    [Fact]
    public void Cooking_TurnsRawToCooked_AtAKiloAQuarterHour_OnABurningFire()
    {
        var w = WithPools(Forage);
        Items.Add(Items.Pack(w, P), Resource.RawMeat, 2);
        Assert.Equal("no fire to cook on", Sim.Step(w, new Cook(P, Resource.RawMeat, 1), 30).InterruptedReason);
        LightAPit(w);
        var r = Sim.Step(w, new Cook(P, Resource.RawMeat, 1), 60);
        Assert.Equal(("cooked", 15), (r.InterruptedReason, r.MinutesElapsed));
        Assert.Equal(1, Items.Have(Items.Pack(w, P), Resource.CookedMeat), 9);
        Assert.Equal(1, Items.Have(Items.Pack(w, P), Resource.RawMeat), 9);
        Assert.Equal("Berries is not something to cook", Sim.Step(w, new Cook(P, Resource.Berries, 1), 30).InterruptedReason);
    }

    [Fact]
    public void MeltingSnow_MakesALitreInTenMinutes()
    {
        var w = WithPools(Forage);
        LightAPit(w);
        var r = Sim.Step(w, new MeltSnow(P), 30);
        Assert.Equal(("melted", 10), (r.InterruptedReason, r.MinutesElapsed));
        Assert.Equal(1, Items.Have(Items.Pack(w, P), Resource.Water), 9);
    }

    [Fact]
    public void Work_IsDeterministic_AndResumesFromASave()
    {
        static World Play(World? from = null)
        {
            var w = from ?? WithPools(Forage, Trees);
            if (from == null) Axe(w);
            Sim.Step(w, new Gather(P, 0), 45);
            Sim.Step(w, new Gather(P, 1), 120);
            return w;
        }
        Assert.Equal(Save.ToJson(Play()), Save.ToJson(Play()));
        var a = WithPools(Forage, Trees);
        Axe(a);
        Sim.Step(a, new Gather(P, 0), 30);
        var b = TestWorlds.Load(Save.ToJson(a));
        b.PoolCache = a.PoolCache;
        Sim.Step(a, new Gather(P, 0), 30);
        Sim.Step(b, new Gather(P, 0), 30);
        Assert.Equal(Save.ToJson(a), Save.ToJson(b));
    }
}
