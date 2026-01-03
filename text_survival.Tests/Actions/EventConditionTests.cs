using text_survival.Actions;
using text_survival.Actions.Expeditions;
using text_survival.Actions.Tensions;
using text_survival.Actors.Player;
using text_survival.Environments;
using text_survival.Environments.Features;
using text_survival.Environments.Grid;
using text_survival.Items;

namespace text_survival.Tests.Actions;

public class EventConditionTests
{
    private static GameContext CreateTestContext()
    {
        var player = new Player();
        var weather = new Weather(-10);
        var camp = new Location("Test Camp", "[test]", weather, 5);

        player.CurrentLocation = camp;

        var ctx = new GameContext(player, camp, weather);
        return ctx;
    }

    private static GameContext CreateTestContextWithMap()
    {
        var player = new Player();
        var weather = new Weather(-10);
        var camp = new Location("Test Camp", "[camp]", weather, 5);
        var awayLocation = new Location("Away Location", "[away]", weather, 5);

        // Create a simple 2x2 map
        var map = new GameMap(2, 2);
        map.Weather = weather;
        map.SetLocation(0, 0, camp);
        map.SetLocation(1, 0, awayLocation);
        map.CurrentPosition = new GridPosition(0, 0);

        player.CurrentLocation = camp;
        player.Map = map;

        var ctx = new GameContext(player, camp, weather);
        ctx.Map = map;
        return ctx;
    }

    // Camp/Expedition State Conditions

    [Fact]
    public void Check_AtCamp_WhenAtCamp_ReturnsTrue()
    {
        // Arrange
        var ctx = CreateTestContext();
        // No expedition active = at camp

        // Act & Assert
        Assert.True(ctx.Check(EventCondition.AtCamp));
    }

    [Fact]
    public void Check_AtCamp_WhenOnExpedition_ReturnsFalse()
    {
        // Arrange
        var ctx = CreateTestContextWithMap();
        var awayLocation = ctx.Map!.GetLocationAt(1, 0)!;
        ctx.Map!.MoveTo(awayLocation, ctx.player); // Travel away from camp

        // Act & Assert
        Assert.False(ctx.Check(EventCondition.AtCamp));
    }

    [Fact]
    public void Check_OnExpedition_WhenOnExpedition_ReturnsTrue()
    {
        // Arrange
        var ctx = CreateTestContextWithMap();
        var awayLocation = ctx.Map!.GetLocationAt(1, 0)!;
        ctx.Map!.MoveTo(awayLocation, ctx.player); // Travel away from camp

        // Act & Assert
        Assert.True(ctx.Check(EventCondition.OnExpedition));
    }

    [Fact]
    public void Check_OnExpedition_WhenAtCamp_ReturnsFalse()
    {
        // Arrange
        var ctx = CreateTestContext();
        // No expedition active

        // Act & Assert
        Assert.False(ctx.Check(EventCondition.OnExpedition));
    }

    [Fact]
    public void Check_NearFire_WhenFireActive_ReturnsTrue()
    {
        // Arrange
        var ctx = CreateTestContext();
        var fire = new HeatSourceFeature();
        // Tinder has minFireTemperature: 0, so it can ignite from cold
        fire.AddFuel(0.5, FuelType.Tinder);
        fire.IgniteAll();
        ctx.CurrentLocation.Features.Add(fire);

        // Act & Assert
        Assert.True(ctx.Check(EventCondition.NearFire));
    }

    [Fact]
    public void Check_NearFire_WhenNoFire_ReturnsFalse()
    {
        // Arrange
        var ctx = CreateTestContext();
        // No heat source feature

        // Act & Assert
        Assert.False(ctx.Check(EventCondition.NearFire));
    }

    [Fact]
    public void Check_HasShelter_WhenShelterExists_ReturnsTrue()
    {
        // Arrange
        var ctx = CreateTestContext();
        ctx.CurrentLocation.Features.Add(new ShelterFeature(
            "test_shelter", 0.8, 0.5, 0.6));

        // Act & Assert
        Assert.True(ctx.Check(EventCondition.HasShelter));
    }

    [Fact]
    public void Check_HasShelter_WhenNoShelter_ReturnsFalse()
    {
        // Arrange
        var ctx = CreateTestContext();
        // No shelter feature

        // Act & Assert
        Assert.False(ctx.Check(EventCondition.HasShelter));
    }

    // Tension Conditions

    [Fact]
    public void Check_Stalked_WhenStalked_ReturnsTrue()
    {
        // Arrange
        var ctx = CreateTestContext();
        ctx.Tensions.AddTension(ActiveTension.Stalked(0.5));

        // Act & Assert
        Assert.True(ctx.Check(EventCondition.Stalked));
    }

    [Fact]
    public void Check_Stalked_WhenNotStalked_ReturnsFalse()
    {
        // Arrange
        var ctx = CreateTestContext();
        // No tension

        // Act & Assert
        Assert.False(ctx.Check(EventCondition.Stalked));
    }

    [Fact]
    public void Check_StalkedHigh_WhenAboveThreshold_ReturnsTrue()
    {
        // Arrange
        var ctx = CreateTestContext();
        ctx.Tensions.AddTension(ActiveTension.Stalked(0.7));

        // Act & Assert
        Assert.True(ctx.Check(EventCondition.StalkedHigh));
    }

    [Fact]
    public void Check_StalkedHigh_WhenBelowThreshold_ReturnsFalse()
    {
        // Arrange
        var ctx = CreateTestContext();
        ctx.Tensions.AddTension(ActiveTension.Stalked(0.3));

        // Act & Assert
        Assert.False(ctx.Check(EventCondition.StalkedHigh));
    }

    [Fact]
    public void Check_StalkedCritical_WhenAboveThreshold_ReturnsTrue()
    {
        // Arrange
        var ctx = CreateTestContext();
        ctx.Tensions.AddTension(ActiveTension.Stalked(0.9));

        // Act & Assert
        Assert.True(ctx.Check(EventCondition.StalkedCritical));
    }

    [Fact]
    public void Check_StalkedCritical_WhenBelowThreshold_ReturnsFalse()
    {
        // Arrange
        var ctx = CreateTestContext();
        ctx.Tensions.AddTension(ActiveTension.Stalked(0.5));

        // Act & Assert
        Assert.False(ctx.Check(EventCondition.StalkedCritical));
    }

    [Fact]
    public void Check_SmokeSpotted_WhenTensionExists_ReturnsTrue()
    {
        // Arrange
        var ctx = CreateTestContext();
        ctx.Tensions.AddTension(ActiveTension.SmokeSpotted(0.5, "north"));

        // Act & Assert
        Assert.True(ctx.Check(EventCondition.SmokeSpotted));
    }

    [Fact]
    public void Check_Infested_WhenTensionExists_ReturnsTrue()
    {
        // Arrange
        var ctx = CreateTestContext();
        ctx.Tensions.AddTension(ActiveTension.Infested(0.5));

        // Act & Assert
        Assert.True(ctx.Check(EventCondition.Infested));
    }

    // Resource Conditions

    [Fact]
    public void Check_HasFuel_WhenHasFuel_ReturnsTrue()
    {
        // Arrange
        var ctx = CreateTestContext();
        ctx.Inventory[Resource.Stick].Push(0.5);

        // Act & Assert
        Assert.True(ctx.Check(EventCondition.HasFuel));
    }

    [Fact]
    public void Check_NoFuel_WhenEmpty_ReturnsTrue()
    {
        // Arrange
        var ctx = CreateTestContext();
        // No fuel

        // Act & Assert
        Assert.True(ctx.Check(EventCondition.NoFuel));
    }

    [Fact]
    public void Check_HasFood_WhenHasFood_ReturnsTrue()
    {
        // Arrange
        var ctx = CreateTestContext();
        ctx.Inventory[Resource.CookedMeat].Push(0.3);

        // Act & Assert
        Assert.True(ctx.Check(EventCondition.HasFood));
    }

    [Fact]
    public void Check_HasMeat_WhenHasRawMeat_ReturnsTrue()
    {
        // Arrange
        var ctx = CreateTestContext();
        ctx.Inventory[Resource.RawMeat].Push(0.5);

        // Act & Assert
        Assert.True(ctx.Check(EventCondition.HasMeat));
    }

    [Fact]
    public void Check_HasMeat_WhenHasCookedMeat_ReturnsTrue()
    {
        // Arrange
        var ctx = CreateTestContext();
        ctx.Inventory[Resource.CookedMeat].Push(0.5);

        // Act & Assert
        Assert.True(ctx.Check(EventCondition.HasMeat));
    }

    // Location Conditions

    [Fact]
    public void Check_InAnimalTerritory_WhenHasAnimalTerritory_ReturnsTrue()
    {
        // Arrange
        var ctx = CreateTestContext();
        ctx.CurrentLocation.Features.Add(AnimalTerritoryFeature.CreateMixedTerritory());

        // Act & Assert
        Assert.True(ctx.Check(EventCondition.InAnimalTerritory));
    }

    [Fact]
    public void Check_HasPredators_WhenTerritoryHasPredators_ReturnsTrue()
    {
        // Arrange
        var ctx = CreateTestContext();
        var territory = AnimalTerritoryFeature.CreatePredatorTerritory();
        ctx.CurrentLocation.Features.Add(territory);

        // Act & Assert
        Assert.True(ctx.Check(EventCondition.HasPredators));
    }

    [Fact]
    public void Check_HasPredators_WhenSmallGameOnly_ReturnsFalse()
    {
        // Arrange
        var ctx = CreateTestContext();
        var territory = AnimalTerritoryFeature.CreateSmallGameTerritory();
        ctx.CurrentLocation.Features.Add(territory);

        // Act & Assert
        Assert.False(ctx.Check(EventCondition.HasPredators));
    }
}
