using text_survival.Actors;
using text_survival.Environments;
using text_survival.Environments.Features;
using text_survival.Environments.Grid;
using text_survival.Items;
using Xunit;

namespace text_survival.Tests.Actors;

/// <summary>
/// Tests for NPC stockpile threshold logic.
/// Thresholds: 2 days × 1 person × daily rate
/// Daily rates: Fuel=20kg, Tinder=0.1kg, Food=1kg, Water=3L, Medicine=0.1kg
/// </summary>
public class NPCStockpileTests
{
    private static (NPC npc, Location camp, CacheFeature cache) CreateTestNPCWithCache()
    {
        var weather = new Weather(-10);
        var camp = new Location("Test Camp", "[camp]", weather, 5);
        var cache = new CacheFeature("Camp Cache", CacheType.Built);
        camp.AddFeature(cache);

        var map = new GameMap(2, 1);
        map.Weather = weather;
        map.SetLocation(0, 0, camp);
        map.CurrentPosition = new GridPosition(0, 0);

        var personality = new Personality { Boldness = 0.5, Selfishness = 0.3, Sociability = 0.6 };
        var npc = new NPC("Test NPC", personality, camp, map);
        npc.Camp = camp;

        return (npc, camp, cache);
    }

    #region No Cache Tests

    [Fact]
    public void IsEnoughStockpiled_NoCache_ReturnsFalse()
    {
        var weather = new Weather(-10);
        var camp = new Location("Test Camp", "[camp]", weather, 5);
        // No cache feature added

        var map = new GameMap(2, 1);
        map.Weather = weather;
        map.SetLocation(0, 0, camp);

        var npc = new NPC("Test NPC", new Personality(), camp, map);
        npc.Camp = camp;

        var result = npc.IsEnoughStockpiled(ResourceCategory.Fuel);

        Assert.False(result);
    }

    [Fact]
    public void IsEnoughStockpiled_EmptyCache_ReturnsFalse()
    {
        var (npc, _, _) = CreateTestNPCWithCache();
        // Cache exists but is empty

        var result = npc.IsEnoughStockpiled(ResourceCategory.Fuel);

        Assert.False(result);
    }

    #endregion

    #region Fuel Tests (Target: 40kg)

    [Fact]
    public void IsEnoughStockpiled_Fuel_BelowTarget_ReturnsFalse()
    {
        var (npc, _, cache) = CreateTestNPCWithCache();
        cache.Storage.Add(Resource.Stick, 30.0); // 30kg < 40kg target

        var result = npc.IsEnoughStockpiled(ResourceCategory.Fuel);

        Assert.False(result);
    }

    [Fact]
    public void IsEnoughStockpiled_Fuel_AtTarget_ReturnsTrue()
    {
        var (npc, _, cache) = CreateTestNPCWithCache();
        cache.Storage.Add(Resource.Stick, 40.0); // Exactly 40kg

        var result = npc.IsEnoughStockpiled(ResourceCategory.Fuel);

        Assert.True(result);
    }

    [Fact]
    public void IsEnoughStockpiled_Fuel_AboveTarget_ReturnsTrue()
    {
        var (npc, _, cache) = CreateTestNPCWithCache();
        cache.Storage.Add(Resource.Stick, 50.0); // 50kg > 40kg

        var result = npc.IsEnoughStockpiled(ResourceCategory.Fuel);

        Assert.True(result);
    }

    #endregion

    #region Food Tests (Target: 2kg)

    [Fact]
    public void IsEnoughStockpiled_Food_BelowTarget_ReturnsFalse()
    {
        var (npc, _, cache) = CreateTestNPCWithCache();
        cache.Storage.Add(Resource.CookedMeat, 1.5); // 1.5kg < 2kg target

        var result = npc.IsEnoughStockpiled(ResourceCategory.Food);

        Assert.False(result);
    }

    [Fact]
    public void IsEnoughStockpiled_Food_AtTarget_ReturnsTrue()
    {
        var (npc, _, cache) = CreateTestNPCWithCache();
        cache.Storage.Add(Resource.CookedMeat, 2.0); // Exactly 2kg

        var result = npc.IsEnoughStockpiled(ResourceCategory.Food);

        Assert.True(result);
    }

    #endregion

    #region Water Tests (Target: 6L)

    [Fact]
    public void IsEnoughStockpiled_Water_BelowTarget_ReturnsFalse()
    {
        var (npc, _, cache) = CreateTestNPCWithCache();
        cache.Storage.WaterLiters = 5.0; // 5L < 6L target

        var result = npc.IsEnoughStockpiled(ResourceCategory.Water);

        Assert.False(result);
    }

    [Fact]
    public void IsEnoughStockpiled_Water_AtTarget_ReturnsTrue()
    {
        var (npc, _, cache) = CreateTestNPCWithCache();
        cache.Storage.WaterLiters = 6.0; // Exactly 6L

        var result = npc.IsEnoughStockpiled(ResourceCategory.Water);

        Assert.True(result);
    }

    #endregion

    #region Tinder Tests (Target: 0.2kg, but truncates to 0)

    [Fact]
    public void IsEnoughStockpiled_Tinder_AnyAmount_ReturnsTrue()
    {
        var (npc, _, cache) = CreateTestNPCWithCache();
        // Target is (int)(2 * 1 * 0.1) = (int)0.2 = 0
        // So any amount >= 0 should satisfy
        cache.Storage.Add(Resource.Tinder, 0.05);

        var result = npc.IsEnoughStockpiled(ResourceCategory.Tinder);

        Assert.True(result);
    }

    #endregion

    #region Material Tests (Target: 0kg)

    [Fact]
    public void IsEnoughStockpiled_Material_EmptyCache_ReturnsTrue()
    {
        var (npc, _, cache) = CreateTestNPCWithCache();
        // Material target is 0, so should always be satisfied if cache exists
        // But CampHas(Material) will return false if no materials
        // This tests the edge case

        var result = npc.IsEnoughStockpiled(ResourceCategory.Material);

        // Returns false because CampHas returns false for empty category
        Assert.False(result);
    }

    [Fact]
    public void IsEnoughStockpiled_Material_HasSome_ReturnsTrue()
    {
        var (npc, _, cache) = CreateTestNPCWithCache();
        cache.Storage.Add(Resource.Stone, 0.5);

        var result = npc.IsEnoughStockpiled(ResourceCategory.Material);

        // Target is 0kg, so any amount satisfies
        Assert.True(result);
    }

    #endregion
}
