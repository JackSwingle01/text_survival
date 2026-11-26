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
        var w = TestWorlds.Bare(seed);
        w.FixedWeather = Mild;
        return w;
    }

    [Fact]
    public void EveryResource_HasARow_AndFuelsAreResources()
    {
        foreach (var r in Enum.GetValues<Resource>()) Assert.True(Items.Resources.ContainsKey(r), $"{r} has no row");
        Assert.All(Fire.Props.Keys, r => Assert.True(Items.Resources.ContainsKey(r)));
        Assert.True(Fire.IsFuel(Resource.BirchBranch) && Fire.IsFuel(Resource.Bone) && !Fire.IsFuel(Resource.Berries));
    }

    // ---- golden: the old starting kit (old GameContext.CreateNewGame) ----

    [Fact]
    public void StartingKit_MatchesOld_CloWeightAndLoad()
    {
        var w = TestWorlds.Bare(17);
        var player = w.Agent(P);
        // The old kit, less what its leg wraps lose for being worn, 60 of their kind's 100 (#265): the old game
        // made them 60 of 60, whole. Their 0.45 clo is 0.27 at 60%, and their 1 kg is 0.9.
        Assert.Equal(1.33 - 0.18, player.Body.ClothingClo, 9);        // old Inventory.ClothingClo
        Assert.Equal(3.05 - 0.1, player.Body.ClothingWeightKg, 9);    // old TotalEquipmentWeightKg
        Assert.Equal(4.43 - 0.2 - 0.1, Items.CarriedKg(player), 9);   // old CurrentWeightKg, less the 0.2 kg the hand drill lost to its lightest inputs (#142)
        Assert.Equal(4.13 / 15, Items.EncumbrancePct(player), 9);
    }

    [Fact]
    public void WornGear_LosesWeightAndWarmthWithCondition_AsOld()
    {
        var g = Items.Make("worn_fur_chest_wrap");
        g.Durability = 25;
        Assert.Equal(1.3125, Items.WeightKg(g), 12); // old Gear.Weight at half condition
        Assert.Equal(1.0, Items.CloValue(g), 12);
        Assert.False(Items.Use(Items.Make("torch")));
        var unbreakable = Items.Make("ground_stone_axe");
        Assert.True(Items.Use(unbreakable));
        Assert.Equal(-1, unbreakable.Durability);
    }

    /// A piece made worn (#265) is worn of its kind's own maximum; one made with no durability is whole, as before.
    [Fact]
    public void Make_WithADurability_IsWornOfItsKindsMaximum()
    {
        var whole = Items.Make("fur_leg_wraps");
        Assert.Equal((100, 100, 1.0), (whole.Durability, whole.MaxDurability, Items.ConditionPct(whole)));
        var axe = Items.Make("ground_stone_axe");
        Assert.Equal((-1, -1), (axe.Durability, axe.MaxDurability));
        var worn = Items.Make("fur_leg_wraps", 60);
        Assert.Equal((60, 100, 0.6), (worn.Durability, worn.MaxDurability, Items.ConditionPct(worn)));
        Assert.Throws<ArgumentOutOfRangeException>(() => Items.Make("fur_leg_wraps", 101)); // more than whole
        Assert.Throws<ArgumentOutOfRangeException>(() => Items.Make("fur_leg_wraps", -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => Items.Make("ground_stone_axe", 10)); // never wears
    }

    [Fact]
    public void StartingLegWraps_AreWorn_SixtyOfTheirKindsMaximum()
    {
        var wraps = TestWorlds.Bare(17).Agent(P).Body.Worn.Single(g => g.Kind == "fur_leg_wraps");
        Assert.Equal((60, 100), (wraps.Durability, wraps.MaxDurability));
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
        var w = TestWorlds.Bare(17);
        var pack = Items.Pack(w, P);
        pack.Gear.Add(Items.Make("large_bag"));
        pack.Gear.Add(Items.Make("rope_belt"));
        Assert.Equal(28, Items.CapacityKg(pack), 12);                // old: max 28
        // old: carried 5.63, less the hand drill's 0.2 kg (#142), the bag's 0.29 and the belt's 0.2 (#150: no heavier than their inputs),
        // and the 0.1 kg the leg wraps lose for being worn (#265)
        Assert.Equal(5.63 - 0.2 - 0.29 - 0.2 - 0.1, Items.CarriedKg(w.Agent(P)), 9);
    }

    /// Each made carrying thing adds what the spec says it does, and all of them together add up (#150).
    [Theory]
    [InlineData("small_pouch", 0.5)]
    [InlineData("rope_belt", 3)]
    [InlineData("hide_belt", 4)]
    [InlineData("large_bag", 10)]
    [InlineData("mammoth_hide_pack", 15)]
    public void EachCarryingThing_AddsItsCapacity(string kind, double kg)
    {
        var pack = new Inventory { Gear = [Items.Make(kind)] };
        Assert.Equal(Items.BaseCapacityKg + kg, Items.CapacityKg(pack), 12);
    }

    [Fact]
    public void TheCarryingThings_Stack()
    {
        var pack = new Inventory { Gear = [.. new[] { "small_pouch", "rope_belt", "hide_belt", "large_bag", "mammoth_hide_pack" }.Select(k => Items.Make(k))] };
        Assert.Equal(Items.BaseCapacityKg + 0.5 + 3 + 4 + 10 + 15, Items.CapacityKg(pack), 12);
    }

    // ---- golden: encumbrance reaches speed exactly as the old AbilityCalculator ----

    [Theory]
    [InlineData(0.0, 0.8491860359604892)]
    [InlineData(0.5, 0.8452777831208177)]
    [InlineData(0.8, 0.6391281827864973)]
    [InlineData(1.0, 0.5016951158969504)]
    [InlineData(1.5, 0.15811244867308313)]
    public void Speed_UnderLoad_MatchesOld(double encumbrancePct, double oldSpeed) =>
        Assert.Equal(oldSpeed, Body.Speed(TestKit.OldBaselineHuman(), new AbilityContext(EncumbrancePct: encumbrancePct)), 9);

    [Fact]
    public void AHeavyPack_SlowsTheCrossing()
    {
        var light = New();
        var heavy = New();
        TestKit.Give(Items.Pack(heavy, P), Resource.Stone, 15);
        int Minutes(World w) => Sim.Step(w, new Travel(P, Direction.East, Pace.Careful), 600).MinutesElapsed;
        Assert.True(Minutes(heavy) > Minutes(light), "carrying twice your capacity should slow you");
    }

    // ---- the pack ----

    [Fact]
    public void Receive_FillsThePack_ThenDropsTheRestOnTheTile()
    {
        var w = TestWorlds.Bare(17);
        var agent = w.Agent(P);
        var gained = new Inventory();
        TestKit.Give(gained, Resource.Stone, 20);
        var events = new List<WorldEvent>();
        var dropped = Items.Receive(w, agent, gained, events);
        // Whole stones: the pack fills to within one stone of full, and the rest go down.
        Assert.True(Items.CarriedKg(agent) <= Items.CapacityKg(Items.Pack(w, P)) + 1e-9);
        Assert.True(Items.CapacityKg(Items.Pack(w, P)) - Items.CarriedKg(agent) < Items.LightestKg(dropped, Resource.Stone));
        Assert.Equal(20, Items.Have(dropped, Resource.Stone) + Items.Have(Items.Pack(w, P), Resource.Stone), 9);
        Assert.Equal(Items.Have(dropped, Resource.Stone), Items.Have(w.Tiles[agent.Tile].Pile!, Resource.Stone), 9);
        Assert.Single(events.OfType<Dropped>());
    }

    [Fact]
    public void PutDownAndPickUp_MoveThingsBetweenPackAndTile_AndSurviveASave()
    {
        var w = New();
        // The sticks are 0.25, 0.3 and 0.35 kg: half a kilo puts down the lightest, since two would weigh more.
        Assert.Null(Sim.Step(w, new PutDown(P, Resource.Stick, 0.5), 1).InterruptedReason);
        Assert.Equal([0.3, 0.35], Items.Pack(w, P).Pieces[Resource.Stick]);
        var loaded = TestWorlds.Load(Save.ToJson(w));
        Assert.Equal([0.25], loaded.Tiles[Map.Of(loaded).Camp].Pile!.Pieces[Resource.Stick]);
        Sim.Step(loaded, new PickUp(P, Resource.Stick, 99), 1);
        Assert.Equal(0.9, Items.Have(Items.Pack(loaded, P), Resource.Stick), 9);
        var r = Sim.Step(loaded, new PickUp(P, Resource.Stick, 1), 5);
        Assert.Equal(("no sticks in the pile here", 0), (r.InterruptedReason, r.MinutesElapsed));
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
        Assert.Equal("Hand Drill is not something to wear", Sim.Step(w, new Wear(P, "hand_drill"), 1).InterruptedReason);
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

    // ---- fire takes from the pack ----

    [Fact]
    public void StartFire_WithoutTheTool_OrTheTinder_IsRefused_AndCostsNothing()
    {
        var w = New();
        string before = Save.ToJson(w);
        var r = Sim.Step(w, new StartFire(P, ToolType.FireStriker, Resource.Tinder, 0.05, 0.3), 5);
        Assert.Equal(("no working fire striker", 0), (r.InterruptedReason, r.MinutesElapsed));
        r = Sim.Step(w, new StartFire(P, ToolType.HandDrill, Resource.BirchBark, 0.05, 0.3), 5);
        Assert.Equal(("not enough birch bark", 0), (r.InterruptedReason, r.MinutesElapsed));
        r = Sim.Step(w, new StartFire(P, ToolType.HandDrill, Resource.Tinder, 0.05, 5), 5);
        Assert.Equal(("not enough sticks", 0), (r.InterruptedReason, r.MinutesElapsed));
        Assert.Equal(before, Save.ToJson(w));
    }

    /// Old FireHandler.AttemptStartFire: tinder spent on every try, kindling only when it catches.
    [Fact]
    public void StartFire_SpendsTinderEachTry_AndKindlingOnlyWhenItCatches()
    {
        var w = New();
        var pack = Items.Pack(w, P);
        TestKit.Give(pack, Resource.Tinder, 2);
        int fails = 0;
        for (int i = 0; i < 30 && Fire.PitAt(w, Map.Of(w).Camp) == null; i++)
        {
            double tinder = Items.Have(pack, Resource.Tinder), kindling = Items.Have(pack, Resource.Stick), stick = Items.LightestKg(pack, Resource.Stick)!.Value;
            var r = Sim.Step(w, new StartFire(P, ToolType.HandDrill, Resource.Tinder, 0.05, 0.3), 1);
            Assert.Equal(tinder - 0.05, Items.Have(pack, Resource.Tinder), 9);
            if (r.InterruptedReason == "fire did not catch") { fails++; Assert.Equal(kindling, Items.Have(pack, Resource.Stick), 9); }
            else Assert.Equal(kindling - stick, Items.Have(pack, Resource.Stick), 9); // a whole stick, the lightest
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
        double birch = Items.Have(pack, Resource.BirchBranch);
        Sim.Step(w, new AddFuel(P, Resource.Stick, 2), 30);
        Sim.Step(w, new AddFuel(P, Resource.BirchBranch, 99), 1); // more than the pack and more than the pit
        var pit = w.Fires[Fire.PitAt(w, Map.Of(w).Camp)!.Value];
        Assert.True(Items.Have(pack, Resource.BirchBranch) < birch);
        Assert.Equal(birch - Items.Have(pack, Resource.BirchBranch), pit.UnburnedKg.GetValueOrDefault(Resource.BirchBranch) + pit.BurningKg.GetValueOrDefault(Resource.BirchBranch), 1);
        Assert.Equal("Berries does not burn", Sim.Step(w, new AddFuel(P, Resource.Berries, 1), 1).InterruptedReason);
        Assert.Equal("no oak branches to add", Sim.Step(w, new AddFuel(P, Resource.OakBranch, 1), 1).InterruptedReason);
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
