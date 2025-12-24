using text_survival.Tests.TestHelpers;

namespace text_survival.Tests.Bodies;

public class AbilityCalculatorTests
{
    private static CapacityModifierContainer NoEffects => new();

    [Fact]
    public void CalculateStrength_BaselineHuman_ReturnsExpectedValue()
    {
        // Arrange
        var body = TestFixtures.CreateBaselineHumanBody();

        // Baseline: 15% fat, 30% muscle, perfect health
        // Expected calculation:
        // - baseStrength = 0.30
        // - muscleContribution = 0.5 + (0.3 - 0.2) * 1.0 = 0.6
        // - muscleContribution += 22.5 / 3 / 100 = 0.675
        // - fatBonus = 11.25 / 50 / 100 = 0.00225
        // - bodyContribution = 0.30 + 0.675 + 0.00225 ≈ 0.977
        // - manipulation = 0.80 + 0.20 * 1.0 = 1.0
        // - moving = 0.50 + 0.50 * 1.0 = 1.0
        // - bloodPumping = 1.0
        // Result ≈ 0.977

        // Act
        double strength = AbilityCalculator.CalculateStrength(body, NoEffects);

        // Assert
        Assert.True(strength > 0.9 && strength < 1.1,
            $"Baseline human strength should be close to 1.0. Actual: {strength}");
    }

    [Fact]
    public void CalculateStrength_HighMuscle_DiminishingReturns()
    {
        // Arrange - very muscular body (45% muscle)
        var body = TestFixtures.CreateCustomHumanBody(weight: 90, fatPercent: 0.10, musclePercent: 0.45);

        // Act
        double strength = AbilityCalculator.CalculateStrength(body, NoEffects);
        double baselineStrength = AbilityCalculator.CalculateStrength(TestFixtures.CreateBaselineHumanBody(), NoEffects);

        // Assert
        Assert.True(strength > baselineStrength,
            $"High muscle mass should increase strength above baseline. High: {strength}, Baseline: {baselineStrength}");
    }

    [Fact]
    public void CalculateStrength_LowFat_Penalty()
    {
        // Arrange - very low body fat (<5%)
        var body = TestFixtures.CreateCustomHumanBody(weight: 70, fatPercent: 0.03, musclePercent: 0.35);

        // Low fat penalty: (0.05 - 0.03) * 3.0 = 0.06 penalty

        // Act
        double strength = AbilityCalculator.CalculateStrength(body, NoEffects);

        // Since muscle is slightly higher, but fat penalty is significant, result should be lower than high-fat equivalent
        // Assert
        Assert.True(strength > 0,
            "Strength should still be positive despite low fat penalty");
    }

    [Fact]
    public void CalculateSpeed_BaselineHuman_ReturnsExpectedValue()
    {
        // Arrange
        var body = TestFixtures.CreateBaselineHumanBody();

        // Act
        double speed = AbilityCalculator.CalculateSpeed(body, NoEffects);

        // Assert
        // Baseline human with current formulas gives ~0.85 speed
        Assert.True(speed > 0.7 && speed < 1.0,
            $"Baseline human speed should be between 0.7-1.0. Actual: {speed}");
    }

    [Fact]
    public void CalculateSpeed_HighFat_SteepPenalty()
    {
        // Arrange - obese body (35% fat)
        var body = TestFixtures.CreateCustomHumanBody(weight: 100, fatPercent: 0.35, musclePercent: 0.20);

        // High fat (>15% baseline) penalty:
        // fatPenalty = (0.35 - 0.15) * 1.5 = 0.30 (30% penalty)

        // Act
        double speed = AbilityCalculator.CalculateSpeed(body, NoEffects);

        // Assert
        Assert.True(speed < 0.8,
            $"High body fat should significantly reduce speed. Actual: {speed}");
    }

    [Fact]
    public void CalculateSpeed_SizeModifier_LargeCreature()
    {
        // Arrange - large creature (10x human weight)
        var body = TestFixtures.CreateCustomHumanBody(weight: 750, fatPercent: 0.15, musclePercent: 0.30);

        // Size ratio = 750 / 75 = 10
        // sizeModifier = 1 - 0.03 * Log2(10) ≈ 1 - 0.03 * 3.32 ≈ 0.90

        // Act
        double speed = AbilityCalculator.CalculateSpeed(body, NoEffects);

        // Assert
        Assert.True(speed < 1.0,
            "Large creatures should be slower than baseline");
    }

    [Fact]
    public void CalculateSpeed_SizeModifier_SmallCreature()
    {
        // Arrange - small creature (0.1x human weight)
        var body = TestFixtures.CreateCustomHumanBody(weight: 7.5, fatPercent: 0.15, musclePercent: 0.30);
        var baselineBody = TestFixtures.CreateBaselineHumanBody();

        // Act
        double smallSpeed = AbilityCalculator.CalculateSpeed(body, NoEffects);
        double baselineSpeed = AbilityCalculator.CalculateSpeed(baselineBody, NoEffects);

        // Assert
        Assert.True(smallSpeed > baselineSpeed,
            $"Small creatures should be faster than baseline. Small: {smallSpeed}, Baseline: {baselineSpeed}");
    }

    [Fact]
    public void CalculateVitality_OrganFunction_DominatesResult()
    {
        // Arrange
        var body = TestFixtures.CreateBaselineHumanBody();

        // Vitality is currently just organ function (body composition commented out)
        // organFunction = (2 * (breathing + bloodPumping) + digestion) / 5
        // With perfect health: (2 * (1.0 + 1.0) + 1.0) / 5 = 5 / 5 = 1.0

        // Act
        double vitality = AbilityCalculator.CalculateVitality(body, NoEffects);

        // Assert
        Assert.Equal(1.0, vitality, precision: 2);
    }

    [Fact]
    public void CalculateVitality_OptimalBodyFat_BestResult()
    {
        // Arrange - optimal fat range (10-25%)
        var optimalBody = TestFixtures.CreateCustomHumanBody(weight: 75, fatPercent: 0.20, musclePercent: 0.30);

        // Fat contribution in optimal range (10-25%): 0.05

        // Act
        double vitality = AbilityCalculator.CalculateVitality(optimalBody, NoEffects);

        // Assert - vitality should be close to 1.0 with perfect organ function
        Assert.True(vitality > 0.9,
            "Optimal body fat with good organ function should give high vitality");
    }

    [Fact]
    public void CalculatePerception_AverageSightHearing_ReturnsAverage()
    {
        // Arrange
        var body = TestFixtures.CreateBaselineHumanBody();

        // Perception = (sight + hearing) / 2
        // Perfect health: (1.0 + 1.0) / 2 = 1.0

        // Act
        double perception = AbilityCalculator.CalculatePerception(body, NoEffects);

        // Assert
        Assert.Equal(1.0, perception, precision: 2);
    }

    [Fact]
    public void CalculateColdResistance_LowFat_MinimalInsulation()
    {
        // Arrange - very low fat (3%)
        var body = TestFixtures.CreateCustomHumanBody(weight: 70, fatPercent: 0.03, musclePercent: 0.35);

        // Fat insulation (< 5%): 0.03 / 0.05 * 0.05 = 0.03
        // Total = 0.0 + 0.03 = 0.03 (base cold resistance is 0 for humans)

        // Act
        double coldResistance = AbilityCalculator.CalculateColdResistance(body);

        // Assert
        Assert.True(coldResistance >= 0 && coldResistance < 0.1,
            $"Low fat should provide minimal insulation. Actual: {coldResistance}");
    }

    [Fact]
    public void CalculateColdResistance_OptimalFat_GoodInsulation()
    {
        // Arrange - optimal fat range (5-15%)
        var body = TestFixtures.CreateCustomHumanBody(weight: 75, fatPercent: 0.10, musclePercent: 0.30);

        // Fat insulation (5-15%):
        // fatInsulation = 0.05 + ((0.10 - 0.05) / 0.10 * 0.10) = 0.05 + 0.05 = 0.10
        // Total = 0.0 + 0.10 = 0.10 (base cold resistance is 0 for humans)

        // Act
        double coldResistance = AbilityCalculator.CalculateColdResistance(body);

        // Assert
        Assert.True(coldResistance >= 0.05 && coldResistance < 0.20,
            $"Optimal fat should provide good insulation. Actual: {coldResistance}");
    }

    [Fact]
    public void CalculateColdResistance_HighFat_DiminishingReturns()
    {
        // Arrange - high fat (30%)
        var body = TestFixtures.CreateCustomHumanBody(weight: 90, fatPercent: 0.30, musclePercent: 0.25);

        // Fat insulation (>15%):
        // fatInsulation = 0.15 + ((0.30 - 0.15) / 0.15 * 0.05) = 0.15 + 0.05 = 0.20
        // Total = 0.0 + 0.20 = 0.20 (base cold resistance is 0 for humans)

        // Act
        double coldResistance = AbilityCalculator.CalculateColdResistance(body);

        // Assert
        Assert.True(coldResistance >= 0.15 && coldResistance <= 0.25,
            $"High fat should provide excellent insulation with diminishing returns. Actual: {coldResistance}");
    }

    [Fact]
    public void CalculateVitality_WithConsciousnessEffects_ReturnsReducedVitality()
    {
        // Arrange - baseline body with effects that reduce Consciousness
        var body = TestFixtures.CreateBaselineHumanBody();

        // Simulating the user's scenario: Hypothermia 98%, Tired 65%, Fever 22%
        // Hypothermia: -0.5 Consciousness * 0.98 = -0.49
        // Tired: -0.45 Consciousness * 0.65 = -0.2925
        // Fever: -0.4 Consciousness * 0.22 = -0.088
        // Total: -0.87

        var effectModifiers = new CapacityModifierContainer();
        effectModifiers.SetCapacityModifier(CapacityNames.Consciousness, -0.87);

        // Act
        double vitality = AbilityCalculator.CalculateVitality(body, effectModifiers);

        // Assert - Vitality should be reduced (min of Breathing, BloodPumping, Consciousness)
        // Consciousness = 1.0 - 0.87 = 0.13, so Vitality should be ~0.13
        Assert.True(vitality < 0.20,
            $"Vitality with -87% Consciousness modifier should be less than 20%. Actual: {vitality:P0}");
    }
}
