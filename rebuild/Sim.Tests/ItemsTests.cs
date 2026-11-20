namespace Survival.Tests;

/// Items, inventory, clothing and what fire now takes from the pack. Ported from the old
/// AccessoryTests, WarmthTests.Coverage_IsAreaWeighted and FireHandler rules; goldens from the old
/// Inventory/Gear/AbilityCalculator (harness golden3a).
public class ItemsTests
{
    static readonly AgentId P = AgentId.Player;
    static readonly WeatherSample Mild = new(0, WeatherCondition.Cloudy, 5, 0.1, 0, 0.8, WindDirection.North);

    static World New(int seed = 17)
    {
        var w = World.New(seed);
        w.FixedWeather = Mild;
        return w;
    }

    [Fact]
    public void EveryResource_HasARow_AndFuelsAreResources()
    {
        foreach (var r in Enum.GetValues<Resource>()) Assert.True(Items.Resources.ContainsKey(r), $"{r} has no row");
        Assert.All(Fire.Props.Keys, r => Assert.True(Items.Resources.ContainsKey(r)));
        Assert.True(Fire.IsFuel(Resource.Birch) && Fire.IsFuel(Resource.Bone) && !Fire.IsFuel(Resource.Berries));
    }

    // ---- golden: the old starting kit (old GameContext.CreateNewGame) ----

    [Fact]
    public void StartingKit_MatchesOld_CloWeightAndLoad()
    {
        var w = World.New(17);
        var player = w.Agent(P);
        Assert.Equal(1.33, player.Body.ClothingClo, 9);        // old Inventory.ClothingClo
        Assert.Equal(3.05, player.Body.ClothingWeightKg, 9);   // old TotalEquipmentWeightKg
        Assert.Equal(4.43, Items.CarriedKg(player), 9);        // old CurrentWeightKg
        Assert.Equal(0.29533333333333334, Items.EncumbrancePct(player), 9);
    }

    [Fact]
    public void WornGear_LosesWeightAndWarmthWithCondition_AsOld()
    {
        var g = Items.Make("worn_fur_chest_wrap");
        g.Durability = 25;
        Assert.Equal(1.3125, Items.WeightKg(g), 12); // old Gear.Weight at half condition
        Assert.Equal(1.0, Items.CloValue(g), 12);
        Assert.False(Items.Use(Items.Make("torch")));
        var unbreakable = Items.Make("stone_axe");
        Assert.True(Items.Use(unbreakable));
        Assert.Equal(-1, unbreakable.Durability);
    }

    /// Old WarmthTests.Coverage_IsAreaWeighted_NotSummed.
    [Fact]
    public void Clothing_IsAreaWeighted_NotSummed()
    {
        double chest = Items.ClothingClo([Items.Make("fur_chest_wrap")]);
        double hands = Items.ClothingClo([Items.Make("hide_handwraps")]);
        Assert.True(chest > hands * 2, $"chest {chest} vs hands {hands}");
        Assert.Equal(0.4 * 3.0, chest, 12);
    }

    /// Old AccessoryTests: capacity stacks, accessories weigh what they weigh.
    [Fact]
    public void Accessories_StackCapacity_AndTheirWeightCounts()
    {
        var w = World.New(17);
        var pack = Items.Pack(w, P);
        pack.Gear.Add(Items.Make("large_bag"));
        pack.Gear.Add(Items.Make("rope_belt"));
        Assert.Equal(28, Items.CapacityKg(pack), 12);                // old: max 28
        Assert.Equal(5.63, Items.CarriedKg(w.Agent(P)), 9);         // old: carried 5.63
    }

    // ---- golden: encumbrance reaches speed exactly as the old AbilityCalculator ----

    [Theory]
    [InlineData(0.0, 0.8491860359604892)]
    [InlineData(0.5, 0.8452777831208177)]
    [InlineData(0.8, 0.6391281827864973)]
    [InlineData(1.0, 0.5016951158969504)]
    [InlineData(1.5, 0.15811244867308313)]
    public void Speed_UnderLoad_MatchesOld(double encumbrancePct, double oldSpeed) =>
        Assert.Equal(oldSpeed, Body.Speed(BodyState.BaselineHuman(), new AbilityContext(EncumbrancePct: encumbrancePct)), 9);

    [Fact]
    public void AHeavyPack_SlowsTheCrossing()
    {
        var light = New();
        var heavy = New();
        Items.Add(Items.Pack(heavy, P), Resource.Stone, 15);
        int Minutes(World w) => Sim.Step(w, new Travel(P, Direction.East, Pace.Careful), 600).MinutesElapsed;
        Assert.True(Minutes(heavy) > Minutes(light), "carrying twice your capacity should slow you");
    }

    // ---- the pack ----

    [Fact]
    public void Receive_FillsThePack_ThenDropsTheRestOnTheTile()
    {
        var w = World.New(17);
        var agent = w.Agent(P);
        var gained = new Inventory();
        Items.Add(gained, Resource.Stone, 20);
        var events = new List<WorldEvent>();
        var dropped = Items.Receive(w, agent, gained, events);
        Assert.Equal(Items.CapacityKg(Items.Pack(w, P)), Items.CarriedKg(agent), 9);
        Assert.Equal(20 - (15 - 4.43), Items.Have(dropped, Resource.Stone), 9);
        Assert.Equal(Items.Have(dropped, Resource.Stone), Items.Have(w.Tiles[agent.Tile].Stash!, Resource.Stone), 9);
        Assert.Single(events.OfType<Dropped>());
    }

    [Fact]
    public void StashAndUnstash_MoveThingsBetweenPackAndTile_AndSurviveASave()
    {
        var w = New();
        Assert.Null(Sim.Step(w, new Stash(P, Resource.Kindling, 0.5), 1).InterruptedReason);
        Assert.Equal(0.4, Items.Have(Items.Pack(w, P), Resource.Kindling), 9);
        var loaded = Save.FromJson(Save.ToJson(w));
        Assert.Equal(0.5, Items.Have(loaded.Tiles[Map.Of(loaded).Camp].Stash!, Resource.Kindling), 9);
        Sim.Step(loaded, new Unstash(P, Resource.Kindling, 99), 1);
        Assert.Equal(0.9, Items.Have(Items.Pack(loaded, P), Resource.Kindling), 9);
        var r = Sim.Step(loaded, new Unstash(P, Resource.Kindling, 1), 5);
        Assert.Equal(("no Kindling stashed here", 0), (r.InterruptedReason, r.MinutesElapsed));
    }

    [Fact]
    public void Wear_PutsOnAGarment_AndThePackTakesWhatItReplaced()
    {
        var w = New();
        Items.Pack(w, P).Gear.Add(Items.Make("bear_hide_chest"));
        double before = w.Agent(P).Body.ClothingClo;
        Assert.Null(Sim.Step(w, new Wear(P, "bear_hide_chest"), 1).InterruptedReason);
        Assert.True(w.Agent(P).Body.ClothingClo > before);
        Assert.Contains(Items.Pack(w, P).Gear, g => g.Kind == "worn_fur_chest_wrap");
        Assert.Single(w.Agent(P).Body.Worn, g => Items.Def(g).Slot == EquipSlot.Chest);
        Assert.Equal("Hand Drill is not worn", Sim.Step(w, new Wear(P, "hand_drill"), 1).InterruptedReason);
    }

    [Fact]
    public void AHerd_CarriesNothing_AndCannotWork()
    {
        var w = New();
        var herd = new Agent { Id = new AgentId(7), Tile = Map.Of(w).Camp, Mind = new HerdMind(), Members = [BodyState.Create(BodyKind.Quadruped, 120, 0.15, 0.6)] };
        w.Agents[herd.Id] = herd;
        Assert.Null(Items.PackOf(herd));
        Assert.Equal(0, Items.EncumbrancePct(herd));
        Assert.Equal("carries nothing to work with", Sim.Step(w, new Gather(herd.Id, 0), 10).InterruptedReason);
    }

    // ---- fire takes from the pack (the phase-1 ponytails) ----

    [Fact]
    public void StartFire_WithoutTheTool_OrTheTinder_IsRefused_AndCostsNothing()
    {
        var w = New();
        string before = Save.ToJson(w);
        var r = Sim.Step(w, new StartFire(P, ToolType.FireStriker, Resource.Tinder, 0.05, 0.3), 5);
        Assert.Equal(("no working FireStriker", 0), (r.InterruptedReason, r.MinutesElapsed));
        r = Sim.Step(w, new StartFire(P, ToolType.HandDrill, Resource.BirchBark, 0.05, 0.3), 5);
        Assert.Equal(("not enough BirchBark", 0), (r.InterruptedReason, r.MinutesElapsed));
        r = Sim.Step(w, new StartFire(P, ToolType.HandDrill, Resource.Tinder, 0.05, 5), 5);
        Assert.Equal(("not enough kindling", 0), (r.InterruptedReason, r.MinutesElapsed));
        Assert.Equal(before, Save.ToJson(w));
    }

    /// Old FireHandler.AttemptStartFire: tinder spent on every try, kindling only when it catches.
    [Fact]
    public void StartFire_SpendsTinderEachTry_AndKindlingOnlyWhenItCatches()
    {
        var w = New();
        var pack = Items.Pack(w, P);
        Items.Add(pack, Resource.Tinder, 2);
        int fails = 0;
        for (int i = 0; i < 30 && Fire.PitAt(w, Map.Of(w).Camp) == null; i++)
        {
            double tinder = Items.Have(pack, Resource.Tinder), kindling = Items.Have(pack, Resource.Kindling);
            var r = Sim.Step(w, new StartFire(P, ToolType.HandDrill, Resource.Tinder, 0.05, 0.3), 1);
            Assert.Equal(tinder - 0.05, Items.Have(pack, Resource.Tinder), 9);
            if (r.InterruptedReason == "fire did not catch") { fails++; Assert.Equal(kindling, Items.Have(pack, Resource.Kindling), 9); }
            else Assert.Equal(kindling - 0.3, Items.Have(pack, Resource.Kindling), 9);
        }
        Assert.NotNull(Fire.PitAt(w, Map.Of(w).Camp));
        Assert.True(fails > 0, "a 50% hand drill never failed in a row of tries");
    }

    [Fact]
    public void AddFuel_TakesWhatThePackHas_AndKeepsWhatThePitCannotHold()
    {
        var w = TestKit.Supply(New());
        for (int i = 0; i < 20 && Fire.PitAt(w, Map.Of(w).Camp) == null; i++)
            Sim.Step(w, new StartFire(P, ToolType.FireStriker, Resource.BirchBark, 0.05, 1.0), 1);
        var pack = Items.Pack(w, P);
        double birch = Items.Have(pack, Resource.Birch);
        Sim.Step(w, new AddFuel(P, Resource.Kindling, 2), 30);
        Sim.Step(w, new AddFuel(P, Resource.Birch, 99), 1); // more than the pack and more than the pit
        var pit = w.Fires[Fire.PitAt(w, Map.Of(w).Camp)!.Value];
        Assert.True(Items.Have(pack, Resource.Birch) < birch);
        Assert.Equal(birch - Items.Have(pack, Resource.Birch), pit.UnburnedKg.GetValueOrDefault(Resource.Birch) + pit.BurningKg.GetValueOrDefault(Resource.Birch), 1);
        Assert.Equal("Berries does not burn", Sim.Step(w, new AddFuel(P, Resource.Berries, 1), 1).InterruptedReason);
        Assert.Equal("no Oak to add", Sim.Step(w, new AddFuel(P, Resource.Oak, 1), 1).InterruptedReason);
    }

    [Fact]
    public void LightTorch_UsesUpATorchFromThePack()
    {
        var w = TestKit.Supply(New());
        for (int i = 0; i < 20 && Fire.PitAt(w, Map.Of(w).Camp) == null; i++)
            Sim.Step(w, new StartFire(P, ToolType.FireStriker, Resource.BirchBark, 0.05, 1.0), 1);
        var pack = Items.Pack(w, P);
        int torches = pack.Gear.Count(g => g.Kind == "torch");
        Sim.Step(w, new LightTorch(P), 1);
        Assert.Equal(torches - 1, pack.Gear.Count(g => g.Kind == "torch"));
        pack.Gear.RemoveAll(g => g.Kind == "torch");
        Assert.Equal("no torch to light", Sim.Step(w, new LightTorch(P), 1).InterruptedReason);
    }

    [Fact]
    public void GatherCharcoal_PutsWhatADeadFireLeftInThePack()
    {
        var w = New();
        Assert.Equal("no charcoal here", Sim.Step(w, new GatherCharcoal(P), 1).InterruptedReason);
        var id = new FireId(w.NextFireId++);
        w.Fires[id] = new FireState { Tile = Map.Of(w).Camp, LastUpdatedMinute = 0, CharcoalKg = 0.4 };
        Assert.Null(Sim.Step(w, new GatherCharcoal(P), 1).InterruptedReason);
        Assert.Equal(0.4, Items.Have(Items.Pack(w, P), Resource.Charcoal), 12);
        Assert.Equal(0, w.Fires[id].CharcoalKg);
    }
}
