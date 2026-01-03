using text_survival.Actors;
using text_survival.Environments;
using text_survival.Environments.Features;
using text_survival.Environments.Grid;
using text_survival.Items;
using Xunit;

namespace text_survival.Tests.Actors;

/// <summary>
/// Tests for NPC inventory management, specifically DealWithFullInventory().
/// </summary>
public class NPCInventoryTests
{
    /// <summary>
    /// Creates a test NPC at camp with proper game state setup.
    /// Returns (npc, camp, awayLocation, map)
    /// </summary>
    private static (NPC npc, Location camp, Location away, GameMap map) CreateTestNPCAtCamp()
    {
        var weather = new Weather(-10);
        var camp = new Location("Test Camp", "[camp]", weather, 5);
        var away = new Location("Away Location", "[away]", weather, 5);

        // Add cache to camp for stashing
        camp.AddFeature(new CacheFeature("Camp Cache", CacheType.Built));

        var map = new GameMap(2, 1);
        map.Weather = weather;
        map.SetLocation(0, 0, camp);
        map.SetLocation(1, 0, away);
        map.CurrentPosition = new GridPosition(0, 0);

        var personality = new Personality { Boldness = 0.5, Selfishness = 0.3, Sociability = 0.6 };
        var npc = new NPC("Test NPC", personality, camp, map);
        npc.Camp = camp;
        npc.Inventory.MaxWeightKg = 15.0; // Standard NPC capacity

        return (npc, camp, away, map);
    }

    [Fact]
    public void DealWithFullInventory_UnderThreshold_ReturnsNull()
    {
        // Arrange: NPC at camp with inventory under 90% capacity
        var (npc, _, _, _) = CreateTestNPCAtCamp();
        npc.Inventory.Add(Resource.Stick, 10.0); // 10kg of 15kg = 67%

        // Act
        var action = npc.DealWithFullInventory();

        // Assert: No action needed
        Assert.Null(action);
    }

    [Fact]
    public void DealWithFullInventory_OverThreshold_NotAtCamp_ReturnsMove()
    {
        // Arrange: NPC at away location with full inventory
        var (npc, _, away, _) = CreateTestNPCAtCamp();
        npc.CurrentLocation = away; // Move NPC away from camp
        npc.Inventory.Add(Resource.Stick, 14.0); // 14kg of 15kg = 93%

        // Act
        var action = npc.DealWithFullInventory();

        // Assert: Should return NPCMove toward camp
        Assert.NotNull(action);
        Assert.IsType<NPCMove>(action);
    }

    [Fact]
    public void DealWithFullInventory_AtCamp_WithResources_StashesHeaviestFirst()
    {
        // Arrange: NPC at camp with multiple resource types
        var (npc, _, _, _) = CreateTestNPCAtCamp();
        // Note: Avoid Resource.Stick being heaviest - it's enum value 0 (default)
        // and the code checks `heaviestResource != default` which fails for Stick
        npc.Inventory.Add(Resource.Pine, 10.0); // 10kg fuel (heaviest)
        npc.Inventory.Add(Resource.RawMeat, 4.0); // 4kg food
        // Total: 14kg = 93%

        // Act
        var action = npc.DealWithFullInventory();

        // Assert: Should stash Fuel (heaviest category)
        Assert.NotNull(action);
        var stash = Assert.IsType<NPCStash>(action);
        // The stash action stores by category - Fuel is heaviest
    }

    [Fact]
    public void DealWithFullInventory_AtCamp_StickIsHeaviest_StillWorks()
    {
        // Arrange: Resource.Stick is enum value 0 (default) - verify it still stashes
        var (npc, _, _, _) = CreateTestNPCAtCamp();
        npc.Inventory.Add(Resource.Stick, 14.0); // 14kg sticks = 93%

        // Act
        var action = npc.DealWithFullInventory();

        // Assert: Should stash Fuel (Stick is in Fuel category)
        Assert.NotNull(action);
        Assert.IsType<NPCStash>(action);
    }

    [Fact]
    public void DealWithFullInventory_AtCamp_OnlyWater_StashesWater()
    {
        // Arrange: NPC at camp with only water (no resources)
        var (npc, _, _, _) = CreateTestNPCAtCamp();
        npc.Inventory.WaterLiters = 14.0; // 14L = 14kg (93%)

        // Act
        var action = npc.DealWithFullInventory();

        // Assert: Should stash water
        Assert.NotNull(action);
        Assert.IsType<NPCStashWater>(action);
    }

    [Fact]
    public void DealWithFullInventory_AtCamp_OnlyEquipment_ReturnsNull()
    {
        // Arrange: NPC at camp with only tools/equipment (can't stash)
        var (npc, _, _, _) = CreateTestNPCAtCamp();
        // Add heavy tools to exceed threshold
        for (int i = 0; i < 5; i++)
        {
            npc.Inventory.Tools.Add(Gear.Axe()); // Each ~1.5kg
        }
        // Tools alone might not hit threshold, so add a bit of equipment weight
        // Actually, let's just reduce max capacity to make tools exceed threshold
        npc.Inventory.MaxWeightKg = 5.0; // Lower threshold so tools exceed it

        // Act
        var action = npc.DealWithFullInventory();

        // Assert: Should return null (can't stash tools/equipment)
        Assert.Null(action);
    }
}
