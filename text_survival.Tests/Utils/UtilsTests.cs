namespace text_survival.Tests.Utils;

public class UtilsTests
{
    [Fact]
    public void DetermineSuccess_ChanceZero_AlwaysFails()
    {
        // Arrange
        double chance = 0.0;
        int trials = 100;
        int successes = 0;

        // Act
        for (int i = 0; i < trials; i++)
        {
            if (text_survival.Utils.DetermineSuccess(chance))
                successes++;
        }

        // Assert
        Assert.Equal(0, successes);
    }

    [Fact]
    public void DetermineSuccess_ChanceOne_AlwaysSucceeds()
    {
        // Arrange
        double chance = 1.0;
        int trials = 100;
        int successes = 0;

        // Act
        for (int i = 0; i < trials; i++)
        {
            if (text_survival.Utils.DetermineSuccess(chance))
                successes++;
        }

        // Assert
        Assert.Equal(trials, successes);
    }

    [Fact]
    public void RandDouble_Distribution_WithinRange()
    {
        // Arrange
        double low = 5.0;
        double high = 10.0;
        int trials = 1000;

        // Act
        for (int i = 0; i < trials; i++)
        {
            double result = text_survival.Utils.RandDouble(low, high);

            // Assert
            Assert.True(result >= low && result <= high,
                $"Random value {result} should be between {low} and {high}");
        }
    }

    [Fact]
    public void RandInt_Distribution_InclusiveRange()
    {
        // Arrange
        int low = 1;
        int high = 5;
        int trials = 1000;
        var results = new HashSet<int>();

        // Act
        for (int i = 0; i < trials; i++)
        {
            int result = text_survival.Utils.RandInt(low, high);
            results.Add(result);

            // Assert range
            Assert.True(result >= low && result <= high,
                $"Random integer {result} should be between {low} and {high} inclusive");
        }

        // After many trials, we should have seen all values in the range
        Assert.True(results.Count >= 4, "Should generate varied values across the range");
    }

    [Fact]
    public void GetRandomWeighted_SingleItem_AlwaysReturns()
    {
        // Arrange
        var choices = new Dictionary<string, double>
        {
            { "OnlyChoice", 1.0 }
        };

        // Act & Assert
        for (int i = 0; i < 10; i++)
        {
            string result = text_survival.Utils.GetRandomWeighted(choices);
            Assert.Equal("OnlyChoice", result);
        }
    }

    [Fact]
    public void GetRandomWeighted_Distribution_RespectsWeights()
    {
        // Arrange
        var choices = new Dictionary<string, double>
        {
            { "Common", 80.0 },
            { "Rare", 20.0 }
        };

        int trials = 1000;
        int commonCount = 0;
        int rareCount = 0;

        // Act
        for (int i = 0; i < trials; i++)
        {
            string result = text_survival.Utils.GetRandomWeighted(choices);
            if (result == "Common") commonCount++;
            else if (result == "Rare") rareCount++;
        }

        // Assert
        // With 80/20 weights, we expect roughly 800/200 distribution
        // Allow for statistical variation
        Assert.True(commonCount > rareCount,
            $"Common item should appear more frequently. Common: {commonCount}, Rare: {rareCount}");
        Assert.True(commonCount > 600,
            $"Common item should appear ~80% of the time. Actual: {commonCount}/{trials}");
    }

    [Theory]
    [InlineData(0, "0 minutes")]
    [InlineData(1, "1 minutes")]
    [InlineData(30, "30 minutes")]
    [InlineData(59, "59 minutes")]
    [InlineData(60, "1.0 hours")]
    [InlineData(90, "1.5 hours")]
    [InlineData(120, "2.0 hours")]
    [InlineData(125, "2.1 hours")]
    [InlineData(180, "3.0 hours")]
    public void FormatFireTime_FormatsCorrectly(int minutes, string expected)
    {
        // Act
        string result = text_survival.Utils.FormatFireTime(minutes);

        // Assert
        Assert.Equal(expected, result);
    }
}
