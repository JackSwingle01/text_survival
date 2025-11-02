namespace text_survival.Tests.Utils;

public class SkillCheckCalculatorTests
{
    [Fact]
    public void CalculateSuccessChance_BaseOnly_ReturnsBaseChance()
    {
        // Arrange
        double baseChance = 0.50;
        int skillLevel = 0;
        int skillDC = 0;

        // Act
        double result = SkillCheckCalculator.CalculateSuccessChance(baseChance, skillLevel, skillDC);

        // Assert
        // With no skill and no DC, should return clamped base chance
        Assert.Equal(0.50, result, precision: 2);
    }

    [Fact]
    public void CalculateSuccessChance_SkillAboveDC_PositiveModifier()
    {
        // Arrange
        double baseChance = 0.40;
        int skillLevel = 5;
        int skillDC = 3;

        // Act
        double result = SkillCheckCalculator.CalculateSuccessChance(baseChance, skillLevel, skillDC);

        // Assert
        // Skill modifier = (5 - 3) * 0.1 = +0.2
        // Result = 0.40 + 0.2 = 0.60
        Assert.Equal(0.60, result, precision: 2);
    }

    [Fact]
    public void CalculateSuccessChance_SkillBelowDC_NegativeModifier()
    {
        // Arrange
        double baseChance = 0.50;
        int skillLevel = 1;
        int skillDC = 4;

        // Act
        double result = SkillCheckCalculator.CalculateSuccessChance(baseChance, skillLevel, skillDC);

        // Assert
        // Skill modifier = (1 - 4) * 0.1 = -0.3
        // Result = 0.50 - 0.3 = 0.20
        Assert.Equal(0.20, result, precision: 2);
    }

    [Fact]
    public void CalculateSuccessChance_NoDC_FlatBonus()
    {
        // Arrange
        double baseChance = 0.30;
        int skillLevel = 3;
        int skillDC = 0; // No DC

        // Act
        double result = SkillCheckCalculator.CalculateSuccessChance(baseChance, skillLevel, skillDC);

        // Assert
        // Skill modifier = 3 * 0.1 = +0.3
        // Result = 0.30 + 0.3 = 0.60
        Assert.Equal(0.60, result, precision: 2);
    }

    [Fact]
    public void CalculateSuccessChance_ClampedAt5Percent_ExtremeLow()
    {
        // Arrange
        double baseChance = 0.10;
        int skillLevel = 0;
        int skillDC = 10; // Impossible DC

        // Act
        double result = SkillCheckCalculator.CalculateSuccessChance(baseChance, skillLevel, skillDC);

        // Assert
        // Skill modifier = (0 - 10) * 0.1 = -1.0
        // Result = 0.10 - 1.0 = -0.90, clamped to 0.05
        Assert.Equal(0.05, result, precision: 2);
    }

    [Fact]
    public void CalculateSuccessChance_ClampedAt95Percent_ExtremeHigh()
    {
        // Arrange
        double baseChance = 0.80;
        int skillLevel = 10;
        int skillDC = 0; // No DC

        // Act
        double result = SkillCheckCalculator.CalculateSuccessChance(baseChance, skillLevel, skillDC);

        // Assert
        // Skill modifier = 10 * 0.1 = +1.0
        // Result = 0.80 + 1.0 = 1.80, clamped to 0.95
        Assert.Equal(0.95, result, precision: 2);
    }

    [Fact]
    public void CalculateXPReward_Success_ReturnsSuccessXP()
    {
        // Arrange
        bool success = true;
        int successXP = 5;
        int failureXP = 1;

        // Act
        int result = SkillCheckCalculator.CalculateXPReward(success, successXP, failureXP);

        // Assert
        Assert.Equal(5, result);
    }

    [Fact]
    public void CalculateXPReward_Failure_ReturnsFailureXP()
    {
        // Arrange
        bool success = false;
        int successXP = 5;
        int failureXP = 1;

        // Act
        int result = SkillCheckCalculator.CalculateXPReward(success, successXP, failureXP);

        // Assert
        Assert.Equal(1, result);
    }

    [Fact]
    public void CalculateXPReward_Failure_DefaultsToOne()
    {
        // Arrange
        bool success = false;
        int successXP = 10;

        // Act
        int result = SkillCheckCalculator.CalculateXPReward(success, successXP); // Use default failureXP

        // Assert
        Assert.Equal(1, result);
    }
}
