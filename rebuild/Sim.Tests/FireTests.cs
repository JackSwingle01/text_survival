namespace Survival.Tests;

/// Ported from text_survival.Tests/Environments/HeatSourceFeatureTests.cs, plus the world-level wiring.
public class FireTests
{
    const double AmbientF = 32.0;
    static readonly AgentId P = AgentId.Player;

    static FireState Lit(double kg = 0, Resource? fuel = null, PitType pit = PitType.Open)
    {
        var f = new FireState { Tile = new TileId(0, 0), LastUpdatedMinute = 0, Pit = pit };
        if (kg > 0 && fuel is Resource type) { Fire.AddFuel(f, type, kg); Fire.IgniteAll(f); }
        return f;
    }

    static List<WorldEvent> Burn(FireState f, int minutes)
    {
        var events = new List<WorldEvent>();
        for (int i = 0; i < minutes; i++) Fire.Tick(f, new FireId(0), ++f.LastUpdatedMinute, events);
        return events;
    }

    // ---- temperature ----

    [Fact]
    public void ColdFire_HasZeroTemperature() => Assert.Equal(0.0, Fire.TemperatureF(Lit()));

    [Fact]
    public void ActiveFire_HasRealisticTemperature()
    {
        var f = Lit(3.0, Resource.Kindling);
        Burn(f, 15);
        double t = Fire.TemperatureF(f);
        Assert.InRange(t, 1, 999);
    }

    [Fact]
    public void Hardwood_BurnsHotterThanTinder()
    {
        var tinder = Lit(3.0, Resource.Tinder);
        var oak = Lit(4.0, Resource.Kindling);
        Burn(oak, 10);
        Assert.True(Fire.AddFuel(oak, Resource.Oak, 5.0) > 0, "kindling fire should be hot enough for oak");
        Burn(tinder, 10);
        Burn(oak, 30);
        Assert.True(Fire.TemperatureF(oak) > Fire.TemperatureF(tinder));
    }

    [Fact]
    public void SmallFire_IsCoolerThanIdeal()
    {
        var small = Lit(0.5, Resource.Kindling);
        var ideal = Lit(3.0, Resource.Kindling);
        Burn(small, 15); Burn(ideal, 15);
        Assert.True(Fire.TemperatureF(small) < Fire.TemperatureF(ideal));
    }

    [Fact]
    public void LargeFire_IsHotterThanIdeal()
    {
        var ideal = Lit(3.0, Resource.Kindling);
        var large = Lit(9.0, Resource.Kindling);
        Burn(ideal, 15); Burn(large, 15);
        Assert.True(Fire.TemperatureF(large) > Fire.TemperatureF(ideal));
    }

    [Fact]
    public void LowFuel_IsCooler()
    {
        var full = Lit(6.0, Resource.Kindling);
        var low = Lit(2.0, Resource.Kindling);
        Burn(full, 15); Burn(low, 15);
        Assert.True(Fire.TemperatureF(low) < Fire.TemperatureF(full));
    }

    // ---- embers ----

    [Fact]
    public void FuelDepleted_TransitionsToEmbers()
    {
        var f = Lit(1.0, Resource.Tinder);
        var events = Burn(f, 25); // tinder 3kg/h x 0.9 small-fire factor: ~22 min
        Assert.False(Fire.IsActive(f));
        Assert.True(f.HasEmbers);
        Assert.Equal(0.0, Fire.BurningKg(f));
        Assert.Equal(FirePhase.Embers, Fire.Phase(f));
        Assert.IsType<FireBurnedToEmbers>(Assert.Single(events));
    }

    [Fact]
    public void Embers_AreCoolerThanActiveFire_ButWarm()
    {
        var f = Lit(3.0, Resource.Kindling);
        Burn(f, 15);
        double active = Fire.TemperatureF(f);
        Burn(f, 110);
        Burn(f, 15);
        Assert.True(f.HasEmbers);
        Assert.InRange(Fire.TemperatureF(f), double.Epsilon, active - double.Epsilon);
    }

    [Fact]
    public void Embers_DecayThenDie_AndLeaveCharcoal()
    {
        var f = Lit(3.0, Resource.Kindling);
        while (!f.HasEmbers) Burn(f, 1);
        double before = f.EmberHoursRemaining;
        Burn(f, 5);
        Assert.True(f.EmberHoursRemaining < before);

        var events = Burn(f, 30);
        Assert.False(f.HasEmbers);
        Assert.Equal(0.0, f.EmberHoursRemaining);
        Assert.Equal(FirePhase.Cold, Fire.Phase(f));
        Assert.IsType<FireDied>(Assert.Single(events));
        Assert.Equal(3.0 * 0.04, f.CharcoalKg, 2); // 4% of what burned
        Assert.Equal(f.CharcoalKg, Fire.CollectCharcoal(f));
        Assert.Equal(0.0, f.CharcoalKg);
    }

    [Fact]
    public void AddFuel_ToEmbers_Relights()
    {
        var f = Lit(1.0, Resource.Tinder);
        Burn(f, 25);
        Assert.True(f.HasEmbers);
        Fire.AddFuel(f, Resource.Kindling, 2.0);
        Assert.True(Fire.IsActive(f));
        Assert.False(f.HasEmbers);
    }

    // ---- heat ----

    [Fact]
    public void ColdFire_GivesNoHeat() => Assert.Equal(0.0, Fire.HeatOutputF(Lit(), AmbientF));

    [Fact]
    public void ActiveFire_GivesHeat_AndBiggerGivesMore()
    {
        var small = Lit(1.0, Resource.Kindling);
        var large = Lit(5.0, Resource.Kindling);
        Burn(small, 15); Burn(large, 15);
        Assert.True(Fire.HeatOutputF(small, AmbientF) > 0);
        Assert.True(Fire.HeatOutputF(large, AmbientF) > Fire.HeatOutputF(small, AmbientF));
    }

    // ---- consumption ----

    [Fact]
    public void ConsumesFuelOverTime()
    {
        var f = Lit(3.0, Resource.Kindling);
        double before = Fire.BurningKg(f);
        Burn(f, 30);
        Assert.InRange(Fire.BurningKg(f), double.Epsilon, before - double.Epsilon);
    }

    [Fact]
    public void BurningHoursRemaining_MatchesBurnRate()
    {
        // kindling 1.5 kg/h; 1.5 kg sits in the 1.0 size band
        Assert.InRange(Fire.BurningHoursRemaining(Lit(1.5, Resource.Kindling)), 0.9, 1.1);
    }

    [Fact]
    public void StonePit_BurnsSlowerThanOpen()
    {
        var open = Lit(3.0, Resource.Kindling);
        var stone = Lit(3.0, Resource.Kindling, PitType.Stone);
        Burn(open, 30); Burn(stone, 30);
        Assert.True(Fire.BurningKg(stone) > Fire.BurningKg(open));
    }

    // ---- fuel gating ----

    [Fact]
    public void Hardwood_NeedsAHotFire()
    {
        var f = Lit(0.5, Resource.Tinder);
        Assert.False(Fire.CanAddFuel(f, Resource.Oak));
        Assert.Equal(0.0, Fire.AddFuel(f, Resource.Oak, 1.0));
        Assert.True(Fire.CanAddFuel(f, Resource.Kindling));
    }

    [Fact]
    public void AddFuel_IsCappedByPitCapacity()
    {
        var f = Lit(10.0, Resource.Kindling);
        Assert.Equal(2.0, Fire.AddFuel(f, Resource.Kindling, 5.0), 6);
        Assert.False(Fire.CanAddFuel(f, Resource.Kindling));
    }

    [Fact]
    public void UnburnedFuel_CatchesOverTime()
    {
        var f = Lit(2.0, Resource.Kindling);
        Burn(f, 10);
        Fire.AddFuel(f, Resource.Pine, 3.0);
        Assert.Equal(FirePhase.Building, Fire.Phase(f));
        double unburned = Fire.UnburnedKg(f);
        Burn(f, 10);
        Assert.True(Fire.UnburnedKg(f) < unburned);
    }

    [Fact]
    public void StartChance_MatchesOldTable()
    {
        Assert.Equal(0.35 + 0.30, Fire.StartChancePct(ToolType.HandDrill, Resource.BirchBark), 9); // birch bark +30% (#144)
        Assert.Equal(0.95, Fire.StartChancePct(ToolType.FireStriker, Resource.BirchBark));
        Assert.Equal(0.05, Fire.StartChancePct(ToolType.HandDrill, Resource.Tinder, consciousnessImpaired: true, dexterityPct: 0));
    }

    // ---- world wiring ----

    static World LitCamp(int seed = 1)
    {
        var world = TestWorlds.Bare(seed);
        TestKit.Supply(world);
        // Striker with birch bark is 95%; retry so the test never depends on one roll.
        for (int i = 0; i < 20 && Fire.PitAt(world, Map.Of(world).Camp) == null; i++)
            Sim.Step(world, new StartFire(P, ToolType.FireStriker, Resource.BirchBark, 0.05, 1.0), 1);
        Assert.NotNull(Fire.PitAt(world, Map.Of(world).Camp));
        return world;
    }

    /// A pit with little room left takes what it can hold; the rest stays in the pack, as AddFuel does.
    [Fact]
    public void StartFire_KeepsWhatThePitHasNoRoomFor()
    {
        var world = TestWorlds.Bare(1);
        TestKit.Supply(world);
        var pit = new FireState { Tile = Map.Of(world).Camp, LastUpdatedMinute = 0, Pit = PitType.Open };
        Fire.AddFuel(pit, Resource.Kindling, Fire.MaxFuelKg(PitType.Open) - 0.03); // cold, and nearly full
        world.Fires[new FireId(world.NextFireId++)] = pit;
        var pack = Items.Pack(world, P);
        double kindling = Items.Have(pack, Resource.Kindling), bark = 0;
        for (int i = 0; i < 20 && !Fire.IsActive(pit); i++)
        {
            bark = Items.Have(pack, Resource.BirchBark);
            Sim.Step(world, new StartFire(P, ToolType.FireStriker, Resource.BirchBark, 0.05, 1.0), 1);
        }
        Assert.True(Fire.IsActive(pit));
        Assert.Equal(bark - 0.03, Items.Have(pack, Resource.BirchBark), 9); // 0.03 of the 0.05 fit
        Assert.Equal(kindling, Items.Have(pack, Resource.Kindling), 9);     // none of it did
    }

    [Fact]
    public void StartFire_ThenAddFuel_ThenTheFireWarmsTheTile()
    {
        var world = LitCamp();
        var r = Sim.Step(world, new AddFuel(P, Resource.Kindling, 2.0), 15);
        Assert.Null(r.InterruptedReason);
        Assert.Contains(r.Events, e => e is FuelAdded { Fuel: Resource.Kindling });
        Assert.True(Fire.WarmthFor(world, P).PitHeatF > 0);
        Assert.True(Fire.HasLight(world, P));
        world.Agent(P).Tile = new TileId(0, 0);
        Assert.Equal(default, Fire.WarmthFor(world, P));
    }

    [Fact]
    public void AddFuel_RefusesBoneOnASmallFire_AndNoFireAtAll()
    {
        var world = LitCamp();
        Assert.Equal("the fire is not hot enough for bone", Sim.Step(world, new AddFuel(P, Resource.Bone, 1), 1).InterruptedReason);
        var empty = TestWorlds.Bare(1);
        Assert.Equal("no fire here", Sim.Step(empty, new AddFuel(P, Resource.Kindling, 1), 1).InterruptedReason);
    }

    [Fact]
    public void UnattendedFire_CatchesUpLazily_WithEventsAtTheRightMinute()
    {
        var world = LitCamp();
        var id = Fire.PitAt(world, Map.Of(world).Camp)!.Value;
        world.Agent(P).Tile = new TileId(0, 0); // walk away; Step no longer touches the fire
        Sim.Step(world, new Wait(P), 180);
        Assert.True(world.Fires[id].LastUpdatedMinute < world.Minute);
        world.Agent(P).Tile = Map.Of(world).Camp; // back, but nothing has caught the fire up
        Assert.Throws<InvalidOperationException>(() => Fire.WarmthFor(world, P));

        var events = new List<WorldEvent>();
        Fire.CatchUp(world, id, events);
        Assert.Equal(world.Minute, world.Fires[id].LastUpdatedMinute);
        var embers = Assert.IsType<FireBurnedToEmbers>(events[0]);
        var died = Assert.IsType<FireDied>(events[1]);
        Assert.True(embers.Minute < died.Minute && died.Minute < world.Minute);
        Assert.Equal(FirePhase.Cold, Fire.Phase(world.Fires[id]));
    }

    [Fact]
    public void Torch_LightsFromFire_WarmsItsCarrier_AndBurnsOutInAnHour()
    {
        var world = LitCamp();
        Sim.Step(world, new AddFuel(P, Resource.Kindling, 2.0), 5);
        Assert.Equal(0.0, Fire.WarmthFor(world, P).TorchHeatF);
        Assert.Null(Sim.Step(world, new LightTorch(P), 1).InterruptedReason);
        // A fresh torch adds 5°F to its carrier only, not to the tile. (One minute burned since lighting.)
        Assert.Equal(3.0 + 2.0 * 59 / 60, Fire.WarmthFor(world, P).TorchHeatF, 6);

        world.Agent(P).Tile = new TileId(0, 0);
        Assert.True(Fire.HasLight(world, P));
        var r = Sim.Step(world, new Wait(P), 61);
        Assert.Contains(r.Events, e => e is FireDied);
        Assert.False(Fire.HasLight(world, P));
        Assert.DoesNotContain(world.Fires.Values, f => f.CarriedBy == P);
        Assert.Equal("no flame to light a torch from", Sim.Step(world, new LightTorch(P), 1).InterruptedReason);
    }

    [Fact]
    public void FireState_SurvivesSaveRoundTrip_AndIsDeterministic()
    {
        static string Play()
        {
            var world = LitCamp(9);
            Sim.Step(world, new AddFuel(P, Resource.Kindling, 3.0), 20);
            Sim.Step(world, new AddFuel(P, Resource.Pine, 2.0), 30);
            Sim.Step(world, new LightTorch(P), 10);
            return Save.ToJson(world);
        }
        string json = Play();
        Assert.Equal(json, Play());
        Assert.Equal(json, Save.ToJson(TestWorlds.Load(json)));
    }
}
