using text_survival.Tests.TestHelpers;

namespace text_survival.Tests.Bodies;

public class BodyTests
{
    [Fact]
    public void Weight_SumOfComponents_CalculatesCorrectly()
    {
        // Arrange
        var body = TestFixtures.CreateBaselineHumanBody();

        // Act
        double weight = body.Weight;

        // Assert
        // Weight = _baseWeight + BodyFat + Muscle
        // For baseline: 75kg total = baseWeight + 11.25 + 22.5
        // So baseWeight should be 41.25
        Assert.Equal(TestConstants.BaselineHuman.Weight, weight, precision: 1);
    }

    [Fact]
    public void BodyFatPercentage_CalculatedCorrectly()
    {
        // Arrange
        var body = TestFixtures.CreateBaselineHumanBody();

        // Act
        double fatPercent = body.BodyFatPercentage;

        // Assert
        // BodyFat / Weight = 11.25 / 75 = 0.15
        Assert.Equal(TestConstants.BaselineHuman.FatPercent, fatPercent, precision: 2);
    }

    [Fact]
    public void MusclePercentage_CalculatedCorrectly()
    {
        // Arrange
        var body = TestFixtures.CreateBaselineHumanBody();

        // Act
        double musclePercent = body.MusclePercentage;

        // Assert
        // Muscle / Weight = 22.5 / 75 = 0.30
        Assert.Equal(TestConstants.BaselineHuman.MusclePercent, musclePercent, precision: 2);
    }

    [Fact]
    public void Health_AverageCondition_CalculatesFromParts()
    {
        // Arrange
        var body = TestFixtures.CreateBaselineHumanBody();

        // Act
        double health = body.Health;

        // Assert
        // Healthy body should have health close to 1.0
        Assert.True(health > 0.9 && health <= 1.0,
            $"Healthy body should have health near 1.0. Actual: {health}");
    }

    [Fact]
    public void Health_DamagedOrgan_ClampedByWorstOrgan()
    {
        // Arrange
        var body = TestFixtures.CreateBaselineHumanBody();
        var chest = body.Parts.First(p => p.Name == BodyRegionNames.Chest);
        var heart = chest.Organs.First(o => o.Name == OrganNames.Heart);
        heart.Condition = 0.3; // Severely damaged heart

        // Act
        double health = body.Health;

        // Assert
        // Health is clamped by worst organ condition
        Assert.True(health <= 0.3,
            $"Overall health should be clamped by worst organ. Actual: {health}");
    }
}
