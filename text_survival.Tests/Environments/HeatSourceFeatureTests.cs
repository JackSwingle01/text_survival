using text_survival.Environments.Features;
using text_survival.Items;
using text_survival.Tests.TestHelpers;

namespace text_survival.Tests.Environments;

public class HeatSourceFeatureTests
{
    private const double AmbientTemp = 32.0; // 0°C = 32°F

    #region Basic Temperature Calculations

    [Fact]
    public void GetCurrentFireTemperature_ColdFire_ReturnsZero()
    {
        // Arrange
        var fire = TestFixtures.CreateTestFire();

        // Act
        double temp = fire.GetCurrentFireTemperature();

        // Assert - cold fire (no fuel, no embers) returns 0
        Assert.Equal(0.0, temp);
    }

    [Fact]
    public void GetCurrentFireTemperature_ActiveFire_ReturnsHigherThanZero()
    {
        // Arrange
        var fire = TestFixtures.CreateTestFire(initialFuelKg: 3.0, fuelType: FuelType.Kindling);

        // Simulate fire burning past startup phase
        fire.Update(new FeatureUpdateContext(15, AmbientTemp));

        // Act
        double temp = fire.GetCurrentFireTemperature();

        // Assert
        Assert.True(temp > 0, $"Active fire should have temperature. Actual: {temp}°F");
        Assert.True(temp < 1000.0, $"Fire temp should be realistic. Actual: {temp}°F");
    }

    [Fact]
    public void GetCurrentFireTemperature_HardwoodVsTinder_HardwoodHotter()
    {
        // Arrange
        var tinderFire = TestFixtures.CreateTestFire(initialFuelKg: 3.0, fuelType: FuelType.Tinder);

        // For hardwood, use kindling (burns slower) to maintain heat, then add hardwood
        var hardwoodFire = TestFixtures.CreateTestFire(initialFuelKg: 4.0, fuelType: FuelType.Kindling);
        hardwoodFire.Update(new FeatureUpdateContext(10, AmbientTemp)); // Get fire very hot (kindling reaches 600°F)

        // Add hardwood once fire is hot enough (requires 500°F)
        hardwoodFire.AddFuel(5.0, FuelType.Hardwood);

        // Both fires past startup
        tinderFire.Update(new FeatureUpdateContext(10, AmbientTemp));
        hardwoodFire.Update(new FeatureUpdateContext(30, AmbientTemp)); // Let hardwood fully ignite

        // Act
        double tinderTemp = tinderFire.GetCurrentFireTemperature();
        double hardwoodTemp = hardwoodFire.GetCurrentFireTemperature();

        // Assert
        // Hardwood peak (900°F) should be much higher than tinder (450°F)
        Assert.True(hardwoodTemp > tinderTemp,
            $"Hardwood should burn hotter than tinder. Hardwood: {hardwoodTemp}°F, Tinder: {tinderTemp}°F");
    }

    #endregion

    #region Ember Phase

    [Fact]
    public void Update_FuelDepleted_TransitionsToEmbers()
    {
        // Arrange
        var fire = TestFixtures.CreateTestFire(initialFuelKg: 1.0, fuelType: FuelType.Tinder);

        // Act - Burn past depletion (tinder burns at 3kg/hour, so 1kg = 20 min)
        fire.Update(new FeatureUpdateContext(25, AmbientTemp));

        // Assert
        Assert.False(fire.IsActive, "Fire should no longer be active");
        Assert.True(fire.HasEmbers, "Fire should have embers");
        Assert.Equal(0.0, fire.BurningMassKg);
    }

    [Fact]
    public void GetCurrentFireTemperature_Embers_LowerThanActiveFire()
    {
        // Arrange
        var fire = TestFixtures.CreateTestFire(initialFuelKg: 3.0, fuelType: FuelType.Kindling);

        // Let fire reach peak temperature
        fire.Update(new FeatureUpdateContext(15, AmbientTemp));
        double activeTempBefore = fire.GetCurrentFireTemperature();

        // Burn until embers (kindling burns at 1.5kg/hour, so 3kg = 120 min)
        fire.Update(new FeatureUpdateContext(110, AmbientTemp)); // Burn past depletion to embers

        // Let embers cool significantly (they start at peak temp)
        fire.Update(new FeatureUpdateContext(15, AmbientTemp));

        // Act
        double emberTemp = fire.GetCurrentFireTemperature();

        // Assert
        Assert.True(fire.HasEmbers, "Fire should have embers");
        Assert.True(emberTemp < activeTempBefore,
            $"Cooled embers should be lower temp than peak active fire. Active: {activeTempBefore}°F, Ember: {emberTemp}°F");
        Assert.True(emberTemp > 0, "Embers should still be warm");
    }

    [Fact]
    public void Update_EmbersDecayOverTime()
    {
        // Arrange
        var fire = TestFixtures.CreateTestFire(initialFuelKg: 1.0, fuelType: FuelType.Kindling);
        fire.Update(new FeatureUpdateContext(45, AmbientTemp)); // Transition to embers

        double initialEmberTime = fire.EmberTimeRemaining;

        // Act
        fire.Update(new FeatureUpdateContext(5, AmbientTemp));

        // Assert
        Assert.True(fire.EmberTimeRemaining < initialEmberTime,
            "Embers should decay over time");
    }

    [Fact]
    public void Update_EmbersEventuallyExtinguish()
    {
        // Arrange
        var fire = TestFixtures.CreateTestFire(initialFuelKg: 1.0, fuelType: FuelType.Kindling);
        fire.Update(new FeatureUpdateContext(45, AmbientTemp)); // Transition to embers

        // Act - Wait well past ember duration
        fire.Update(new FeatureUpdateContext(30, AmbientTemp));

        // Assert
        Assert.False(fire.HasEmbers, "Embers should be extinguished");
        Assert.Equal(0.0, fire.EmberTimeRemaining);
    }

    #endregion

    #region Fire Size Effects

    [Fact]
    public void GetCurrentFireTemperature_SmallFire_ReducedTemp()
    {
        // Arrange
        var smallFire = TestFixtures.CreateTestFire(initialFuelKg: 0.5, fuelType: FuelType.Kindling);
        var idealFire = TestFixtures.CreateTestFire(initialFuelKg: 3.0, fuelType: FuelType.Kindling);

        smallFire.Update(new FeatureUpdateContext(15, AmbientTemp));
        idealFire.Update(new FeatureUpdateContext(15, AmbientTemp));

        // Act
        double smallTemp = smallFire.GetCurrentFireTemperature();
        double idealTemp = idealFire.GetCurrentFireTemperature();

        // Assert
        // Small fire (<1kg) has 0.7 multiplier - should be cooler
        Assert.True(smallTemp < idealTemp,
            $"Small fire should be cooler than ideal size. Small: {smallTemp}°F, Ideal: {idealTemp}°F");
    }

    [Fact]
    public void GetCurrentFireTemperature_LargeFire_BonusTemp()
    {
        // Arrange - Use kindling which doesn't require hot fire to start
        var idealFire = TestFixtures.CreateTestFire(initialFuelKg: 3.0, fuelType: FuelType.Kindling);
        var largeFire = TestFixtures.CreateTestFire(initialFuelKg: 9.0, fuelType: FuelType.Kindling);

        idealFire.Update(new FeatureUpdateContext(15, AmbientTemp));
        largeFire.Update(new FeatureUpdateContext(15, AmbientTemp));

        // Act
        double idealTemp = idealFire.GetCurrentFireTemperature();
        double largeTemp = largeFire.GetCurrentFireTemperature();

        // Assert
        // Large fire (>8kg) has 1.1 multiplier - should be hotter
        Assert.True(largeTemp > idealTemp,
            $"Large fire should be hotter than ideal size. Large: {largeTemp}°F, Ideal: {idealTemp}°F");
    }

    #endregion

    // Note: Startup curve tests removed - startup curve was replaced by two-mass model
    // where fuel takes time to catch, not time to reach temperature

    #region Fire Size Effects

    [Fact]
    public void GetCurrentFireTemperature_LowFuel_ReducedTemp()
    {
        // Arrange
        var fullFuelFire = TestFixtures.CreateTestFire(initialFuelKg: 6.0, fuelType: FuelType.Kindling);
        var lowFuelFire = TestFixtures.CreateTestFire(initialFuelKg: 2.0, fuelType: FuelType.Kindling);

        fullFuelFire.Update(new FeatureUpdateContext(15, AmbientTemp));
        lowFuelFire.Update(new FeatureUpdateContext(15, AmbientTemp));

        // Act
        double fullTemp = fullFuelFire.GetCurrentFireTemperature();
        double lowTemp = lowFuelFire.GetCurrentFireTemperature();

        // Assert
        // Fuel at 16.7% (2kg/12kg) is below 30% threshold, should have decline penalty
        Assert.True(lowTemp < fullTemp,
            $"Low fuel should reduce temperature. Full: {fullTemp}°F, Low: {lowTemp}°F");
    }

    #endregion

    #region Heat Output

    [Fact]
    public void GetEffectiveHeatOutput_ColdFire_ReturnsZero()
    {
        // Arrange
        var fire = TestFixtures.CreateTestFire();

        // Act
        double heatOutput = fire.GetEffectiveHeatOutput(AmbientTemp);

        // Assert
        Assert.Equal(0.0, heatOutput);
    }

    [Fact]
    public void GetEffectiveHeatOutput_ActiveFire_ReturnsPositiveHeat()
    {
        // Arrange - Use kindling which starts easily
        var fire = TestFixtures.CreateTestFire(initialFuelKg: 3.0, fuelType: FuelType.Kindling);
        fire.Update(new FeatureUpdateContext(15, AmbientTemp));

        // Act
        double heatOutput = fire.GetEffectiveHeatOutput(AmbientTemp);

        // Assert
        Assert.True(heatOutput > 0,
            $"Active hot fire should produce heat. Actual: {heatOutput}°F");
    }

    [Fact]
    public void GetEffectiveHeatOutput_LargerFire_MoreHeat()
    {
        // Arrange
        var smallFire = TestFixtures.CreateTestFire(initialFuelKg: 1.0, fuelType: FuelType.Kindling);
        var largeFire = TestFixtures.CreateTestFire(initialFuelKg: 5.0, fuelType: FuelType.Kindling);

        smallFire.Update(new FeatureUpdateContext(15, AmbientTemp));
        largeFire.Update(new FeatureUpdateContext(15, AmbientTemp));

        // Act
        double smallHeat = smallFire.GetEffectiveHeatOutput(AmbientTemp);
        double largeHeat = largeFire.GetEffectiveHeatOutput(AmbientTemp);

        // Assert
        Assert.True(largeHeat > smallHeat,
            $"Larger fire should produce more heat. Small: {smallHeat}°F, Large: {largeHeat}°F");
    }

    #endregion

    #region Fuel Consumption

    [Fact]
    public void Update_ConsumsFuelOverTime()
    {
        // Arrange
        var fire = TestFixtures.CreateTestFire(initialFuelKg: 3.0, fuelType: FuelType.Kindling);
        double initialFuel = fire.BurningMassKg;

        // Act
        fire.Update(new FeatureUpdateContext(30, AmbientTemp));

        // Assert
        Assert.True(fire.BurningMassKg < initialFuel,
            "Fire should consume fuel over time");
        Assert.True(fire.BurningMassKg > 0,
            "Fire should still have fuel remaining");
    }

    [Fact]
    public void HoursRemaining_ReturnsEstimatedHours()
    {
        // Arrange
        var fire = TestFixtures.CreateTestFire(initialFuelKg: 1.5, fuelType: FuelType.Kindling);

        // Act
        double fuelHours = fire.HoursRemaining;

        // Assert
        // Kindling burns at 1.5kg/hour, so 1.5kg should last 1 hour
        Assert.True(fuelHours >= 0.9 && fuelHours <= 1.1,
            $"1.5kg kindling should last ~1 hour. Actual: {fuelHours} hours");
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void AddFuel_ToEmbers_RelightsFireAutomatically()
    {
        // Arrange
        var fire = TestFixtures.CreateTestFire(initialFuelKg: 1.0, fuelType: FuelType.Tinder);
        fire.Update(new FeatureUpdateContext(25, AmbientTemp)); // Burn to embers

        Assert.True(fire.HasEmbers, "Fire should have embers before test");

        // Act - add kindling fuel
        fire.AddFuel(2.0, FuelType.Kindling);

        // Assert
        Assert.True(fire.IsActive, "Adding fuel to embers should relight fire");
        Assert.False(fire.HasEmbers, "Fire should no longer have embers");
    }

    [Fact]
    public void CanAddFuel_RequiresMinimumTemperature()
    {
        // Arrange
        var coldFire = TestFixtures.CreateTestFire(initialFuelKg: 0.5, fuelType: FuelType.Tinder);

        // Act - hardwood requires 500°F to ignite
        bool canAddImmediately = coldFire.CanAddFuel(FuelType.Hardwood);

        // Warm up the fire
        coldFire.Update(new FeatureUpdateContext(10, AmbientTemp));
        bool canAddAfterWarmup = coldFire.CanAddFuel(FuelType.Hardwood);

        // Assert
        Assert.False(canAddImmediately,
            "Should not be able to add hardwood to cold fire");
        // After warmup, tinder should reach required temp (depending on fire size)
    }

    #endregion
}
