using System.Text.Json;
using text_survival.Actions;
using text_survival.Actions.Tensions;
using text_survival.Actors;
using text_survival.Actors.Player;
using text_survival.Environments;
using text_survival.Environments.Features;
using text_survival.Items;
using text_survival.Persistence;

namespace text_survival.Tests.Persistence;

public class StatefulSerializationTests
{
    [Fact]
    public void GroundItems_PreserveStoredItems()
    {
        var groundItems = new GroundItemsFeature();
        groundItems.Storage.Add(Resource.Stone, 1.25);
        groundItems.Storage.Tools.Add(Gear.Axe("Dropped Axe"));

        var loaded = Assert.IsType<GroundItemsFeature>(RoundTrip<LocationFeature>(groundItems));

        Assert.Equal(1, loaded.Storage.Count(Resource.Stone));
        Assert.Contains(loaded.Storage.Tools, tool => tool.Name == "Dropped Axe");
    }

    [Fact]
    public void SnareLine_PreservesSnaresAndTheirState()
    {
        var territory = new SmallGameFeature(0.8).AddRabbit();
        var snareLine = new SnareLineFeature(territory);
        snareLine.PlaceSnareWithBait(8, BaitType.Berries, reinforced: true);

        var loaded = Assert.IsType<SnareLineFeature>(RoundTrip<LocationFeature>(snareLine));
        var snare = Assert.Single(loaded._snares);

        Assert.Equal(8, snare.DurabilityRemaining);
        Assert.Equal(BaitType.Berries, snare.Bait);
        Assert.True(snare.IsReinforced);
        Assert.NotNull(loaded._territory);
    }

    [Fact]
    public void FishingNets_PreserveFeatureAndNetState()
    {
        var water = new WaterFeature("river", "River");
        var netFishing = new NetFishingFeature(water);
        netFishing.PlaceNet(9);
        netFishing._nets[0].Update(60, fishAbundance: 0, isFlowingWater: false,
            stalkedTensionActive: false);

        var loaded = Assert.IsType<NetFishingFeature>(RoundTrip<LocationFeature>(netFishing));
        var net = Assert.Single(loaded._nets);

        Assert.Equal(NetState.Soaking, net.State);
        Assert.Equal(60, net.SoakDurationMinutes);
        Assert.Equal(9, net.DurabilityRemaining);
        Assert.Equal("River", loaded._water.DisplayName);
    }

    [Fact]
    public void TensionRegistry_PreservesActiveTensions()
    {
        var registry = new TensionRegistry();
        registry.AddTension(ActiveTension.SmokeSpotted(0.65, "north"));

        var loaded = RoundTrip(registry);
        var tension = Assert.Single(loaded.GetAllTensions());

        Assert.Equal("SmokeSpotted", tension.Type);
        Assert.Equal(0.65, tension.Severity, precision: 2);
        Assert.Equal("north", tension.Direction);
        Assert.Equal(TensionStage.Escalating, tension.Stage);
        Assert.False(tension.DecaysAtCamp);
    }

    [Fact]
    public void Weather_PreservesItsClock()
    {
        var time = new DateTime(2025, 7, 14, 16, 30, 0);
        var weather = new Weather(-10, time);

        var loaded = RoundTrip(weather);

        Assert.Equal(time, loaded.Time);
        Assert.Equal(Weather.Season.Summer, loaded.CurrentSeason);
    }

    [Fact]
    public void InventoryCapacity_DoesNotGrowAcrossReloads()
    {
        var inventory = Inventory.CreatePlayerInventory(15);
        inventory.Accessories.Add(Gear.LargeBag());

        for (var reload = 0; reload < 3; reload++)
            inventory = RoundTrip(inventory);

        Assert.Equal(15, inventory.BaseMaxWeightKg);
        Assert.Equal(25, inventory.MaxWeightKg);
    }

    [Fact]
    public void RelationshipMemory_PreservesEvents()
    {
        var relationships = new RelationshipMemory();
        relationships.MemoryEvents.Add(new MemoryEvent
        {
            Type = MemoryType.SavedMe,
            Count = 3
        });

        var loaded = RoundTrip(relationships);
        var memory = Assert.Single(loaded.MemoryEvents);

        Assert.Equal(MemoryType.SavedMe, memory.Type);
        Assert.Equal(3, memory.Count);
    }

    [Fact]
    public void PlayerSkills_PreserveLevelsAndExperience()
    {
        var player = new Player();
        player.Skills.Fighting.LevelUp();
        player.Skills.Fighting.LevelUp();
        player.Skills.Fighting.GainExperience(7);

        var loaded = RoundTrip(player);

        Assert.Equal(2, loaded.Skills.Fighting.Level);
        Assert.Equal(7, loaded.Skills.Fighting.Xp);
    }

    [Fact]
    public void FullGame_PreservesStatefulSystemsAndSharedReferences()
    {
        var game = GameContext.CreateNewGame(seed: 1234);

        var groundItems = new GroundItemsFeature();
        groundItems.Storage.Add(Resource.Bone, 0.4);
        game.Camp.Features.Add(groundItems);

        var territory = new SmallGameFeature(0.8).AddRabbit();
        var snareLine = new SnareLineFeature(territory);
        snareLine.PlaceSnare(6);
        game.Camp.Features.Add(territory);
        game.Camp.Features.Add(snareLine);

        var water = new WaterFeature("river", "River");
        var netFishing = new NetFishingFeature(water);
        netFishing.PlaceNet(7);
        game.Camp.Features.Add(water);
        game.Camp.Features.Add(netFishing);

        game.Tensions.AddTension(ActiveTension.Infested(0.55, game.Camp));
        game.player.Skills.Foraging.LevelUp();

        var npc = Assert.Single(game.NPCs);
        npc.Relationships.AddMemory(MemoryType.SavedMe, game.player);

        var loaded = RoundTrip(game);
        var loadedTerritory = loaded.Camp.Features.OfType<SmallGameFeature>().Last();
        var loadedSnareLine = Assert.IsType<SnareLineFeature>(loaded.Camp.GetFeature<SnareLineFeature>());
        var loadedWater = loaded.Camp.Features.OfType<WaterFeature>().Last();
        var loadedNetFishing = Assert.IsType<NetFishingFeature>(loaded.Camp.GetFeature<NetFishingFeature>());

        Assert.Equal(1, loaded.Camp.GetFeature<GroundItemsFeature>()!.Storage.Count(Resource.Bone));
        Assert.Same(loadedTerritory, loadedSnareLine._territory);
        Assert.Same(loadedWater, loadedNetFishing._water);
        Assert.Same(loaded.Camp, loaded.Tensions.GetTension("Infested")!.RelevantLocation);
        Assert.Equal(1, loaded.player.Skills.Foraging.Level);
        Assert.Same(loaded.player, Assert.Single(Assert.Single(loaded.NPCs).Relationships.MemoryEvents).Subject);
    }

    private static T RoundTrip<T>(T value)
    {
        var json = JsonSerializer.Serialize(value, SaveManager.Options);
        return Assert.IsAssignableFrom<T>(JsonSerializer.Deserialize<T>(json, SaveManager.Options));
    }
}
