using text_survival.Actors;
using text_survival.Environments;
using text_survival.Environments.Features;
using text_survival.Environments.Grid;
using text_survival.Items;
using text_survival.Survival;
using text_survival.Tests.TestHelpers;
using Xunit;

namespace text_survival.Tests.Actors;

/// <summary>
/// Tests for NPC need determination and sleep eligibility logic.
/// </summary>
public class NPCDecisionTests
{
    private static (NPC npc, Location camp) CreateTestNPC()
    {
        var weather = new Weather(-10);
        var camp = new Location("Test Camp", "[camp]", weather, 5);

        var map = new GameMap(2, 1);
        map.Weather = weather;
        map.SetLocation(0, 0, camp);
        map.CurrentPosition = new GridPosition(0, 0);

        var personality = new Personality { Boldness = 0.5, Selfishness = 0.3, Sociability = 0.6 };
        var npc = new NPC("Test NPC", personality, camp, map);
        npc.Camp = camp;

        return (npc, camp);
    }

    // Helper to set body stats to specific percentages
    private static void SetBodyStats(NPC npc, double warmPct, double hydratedPct, double energyPct, double fullPct)
    {
        // WarmPct = (BodyTemp - 95) / 3.6 → BodyTemp = WarmPct * 3.6 + 95
        npc.Body.BodyTemperature = warmPct * 3.6 + 95.0;
        npc.Body.Hydration = hydratedPct * SurvivalProcessor.MAX_HYDRATION;
        npc.Body.Energy = energyPct * SurvivalProcessor.MAX_ENERGY_MINUTES;
        npc.Body.CalorieStore = fullPct * SurvivalProcessor.MAX_CALORIES;
    }

    #region GetCriticalNeed Tests

    [Fact]
    public void GetCriticalNeed_AllStatsHealthy_ReturnsNull()
    {
        var (npc, _) = CreateTestNPC();
        SetBodyStats(npc, warmPct: 0.5, hydratedPct: 0.5, energyPct: 0.5, fullPct: 0.5);

        var result = npc.GetCriticalNeed();

        Assert.Null(result);
    }

    [Fact]
    public void GetCriticalNeed_WarmthCritical_ReturnsWarmth()
    {
        var (npc, _) = CreateTestNPC();
        SetBodyStats(npc, warmPct: 0.20, hydratedPct: 0.5, energyPct: 0.5, fullPct: 0.5);

        var result = npc.GetCriticalNeed();

        Assert.Equal(NeedType.Warmth, result);
    }

    [Fact]
    public void GetCriticalNeed_WaterCritical_ReturnsWater()
    {
        var (npc, _) = CreateTestNPC();
        SetBodyStats(npc, warmPct: 0.5, hydratedPct: 0.15, energyPct: 0.5, fullPct: 0.5);

        var result = npc.GetCriticalNeed();

        Assert.Equal(NeedType.Water, result);
    }

    [Fact]
    public void GetCriticalNeed_RestCritical_ReturnsRest()
    {
        var (npc, _) = CreateTestNPC();
        SetBodyStats(npc, warmPct: 0.5, hydratedPct: 0.5, energyPct: 0.05, fullPct: 0.5);

        var result = npc.GetCriticalNeed();

        Assert.Equal(NeedType.Rest, result);
    }

    [Fact]
    public void GetCriticalNeed_FoodCritical_ReturnsFood()
    {
        var (npc, _) = CreateTestNPC();
        SetBodyStats(npc, warmPct: 0.5, hydratedPct: 0.5, energyPct: 0.5, fullPct: 0.03);

        var result = npc.GetCriticalNeed();

        Assert.Equal(NeedType.Food, result);
    }

    [Fact]
    public void GetCriticalNeed_MultipleNeedsCritical_ReturnsWarmthFirst()
    {
        var (npc, _) = CreateTestNPC();
        // All stats critical
        SetBodyStats(npc, warmPct: 0.20, hydratedPct: 0.15, energyPct: 0.05, fullPct: 0.03);

        var result = npc.GetCriticalNeed();

        // Warmth has highest priority
        Assert.Equal(NeedType.Warmth, result);
    }

    [Fact]
    public void GetCriticalNeed_WaterAndFoodCritical_ReturnsWaterFirst()
    {
        var (npc, _) = CreateTestNPC();
        // Warmth OK, but water and food critical
        SetBodyStats(npc, warmPct: 0.5, hydratedPct: 0.15, energyPct: 0.5, fullPct: 0.03);

        var result = npc.GetCriticalNeed();

        // Water has higher priority than Food
        Assert.Equal(NeedType.Water, result);
    }

    #endregion

    #region IsCriticalNeedSatisfied Tests

    [Fact]
    public void IsCriticalNeedSatisfied_NullCurrentNeed_ReturnsTrue()
    {
        var (npc, _) = CreateTestNPC();
        npc.CurrentNeed = null;

        var result = npc.IsCriticalNeedSatisfied();

        Assert.True(result);
    }

    [Fact]
    public void IsCriticalNeedSatisfied_WarmthNeed_AboveThreshold_ReturnsTrue()
    {
        var (npc, _) = CreateTestNPC();
        npc.CurrentNeed = NeedType.Warmth;
        SetBodyStats(npc, warmPct: 0.75, hydratedPct: 0.5, energyPct: 0.5, fullPct: 0.5);

        var result = npc.IsCriticalNeedSatisfied();

        Assert.True(result);
    }

    [Fact]
    public void IsCriticalNeedSatisfied_WarmthNeed_BelowThreshold_ReturnsFalse()
    {
        var (npc, _) = CreateTestNPC();
        npc.CurrentNeed = NeedType.Warmth;
        SetBodyStats(npc, warmPct: 0.65, hydratedPct: 0.5, energyPct: 0.5, fullPct: 0.5);

        var result = npc.IsCriticalNeedSatisfied();

        Assert.False(result);
    }

    [Fact]
    public void IsCriticalNeedSatisfied_WaterNeed_AboveThreshold_ReturnsTrue()
    {
        var (npc, _) = CreateTestNPC();
        npc.CurrentNeed = NeedType.Water;
        SetBodyStats(npc, warmPct: 0.5, hydratedPct: 0.55, energyPct: 0.5, fullPct: 0.5);

        var result = npc.IsCriticalNeedSatisfied();

        Assert.True(result);
    }

    [Fact]
    public void IsCriticalNeedSatisfied_FoodNeed_AboveThreshold_ReturnsTrue()
    {
        var (npc, _) = CreateTestNPC();
        npc.CurrentNeed = NeedType.Food;
        SetBodyStats(npc, warmPct: 0.5, hydratedPct: 0.5, energyPct: 0.5, fullPct: 0.35);

        var result = npc.IsCriticalNeedSatisfied();

        Assert.True(result);
    }

    [Fact]
    public void IsCriticalNeedSatisfied_Hysteresis_BetweenCriticalAndSatisfied_ReturnsFalse()
    {
        var (npc, _) = CreateTestNPC();
        // Warmth at 0.5 - above critical (0.25) but below satisfied (0.7)
        npc.CurrentNeed = NeedType.Warmth;
        SetBodyStats(npc, warmPct: 0.50, hydratedPct: 0.5, energyPct: 0.5, fullPct: 0.5);

        var result = npc.IsCriticalNeedSatisfied();

        // Should NOT be satisfied - hysteresis prevents oscillation
        Assert.False(result);
    }

    #endregion

    #region CanSleep Tests

    [Fact]
    public void CanSleep_AllConditionsMet_ReturnsTrue()
    {
        var (npc, camp) = CreateTestNPC();
        npc.CurrentLocation = camp;
        SetBodyStats(npc, warmPct: 0.5, hydratedPct: 0.5, energyPct: 0.5, fullPct: 0.5);

        // Add fire with sufficient runway (>= 2 hours)
        var fire = TestFixtures.CreateTestFire(initialFuelKg: 10.0, fuelType: FuelType.Softwood);
        camp.AddFeature(fire);

        var result = npc.CanSleep();

        Assert.True(result);
    }

    [Fact]
    public void CanSleep_AwayFromCamp_ReturnsFalse()
    {
        var (npc, camp) = CreateTestNPC();
        var away = new Location("Away", "[away]", new Weather(-10), 5);
        npc.CurrentLocation = away;
        SetBodyStats(npc, warmPct: 0.5, hydratedPct: 0.5, energyPct: 0.5, fullPct: 0.5);

        var result = npc.CanSleep();

        Assert.False(result);
    }

    [Fact]
    public void CanSleep_Freezing_ReturnsFalse()
    {
        var (npc, camp) = CreateTestNPC();
        npc.CurrentLocation = camp;
        SetBodyStats(npc, warmPct: 0.15, hydratedPct: 0.5, energyPct: 0.5, fullPct: 0.5);

        var result = npc.CanSleep();

        Assert.False(result);
    }

    [Fact]
    public void CanSleep_FireDyingSoon_ReturnsFalse()
    {
        var (npc, camp) = CreateTestNPC();
        npc.CurrentLocation = camp;
        SetBodyStats(npc, warmPct: 0.5, hydratedPct: 0.5, energyPct: 0.5, fullPct: 0.5);

        // Add fire with insufficient runway (< 2 hours)
        var fire = TestFixtures.CreateTestFire(initialFuelKg: 0.3, fuelType: FuelType.Kindling);
        camp.AddFeature(fire);

        var result = npc.CanSleep();

        Assert.False(result);
    }

    [Fact]
    public void CanSleep_NoFire_ReturnsTrue()
    {
        var (npc, camp) = CreateTestNPC();
        npc.CurrentLocation = camp;
        SetBodyStats(npc, warmPct: 0.5, hydratedPct: 0.5, energyPct: 0.5, fullPct: 0.5);
        // No fire feature at all

        var result = npc.CanSleep();

        // No fire means fire check passes (only fails if fire exists AND has < 2hr)
        Assert.True(result);
    }

    #endregion
}
