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
        double weight = body.WeightKG;

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

}
