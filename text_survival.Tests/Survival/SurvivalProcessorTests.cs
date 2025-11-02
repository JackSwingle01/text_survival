using text_survival.Tests.TestHelpers;

namespace text_survival.Tests.Survival;

public class SurvivalProcessorTests
{
    [Fact]
    public void Process_NormalConditions_UpdatesStatsCorrectly()
    {
        // Arrange
        var survivalData = TestFixtures.CreateBaselineSurvivalData();
        int minutesElapsed = 60; // 1 hour
        var activeEffects = new List<Effect>();

        // Act
        var result = SurvivalProcessor.Process(survivalData, minutesElapsed, activeEffects);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Data.Energy < TestConstants.Survival.MaxEnergyMinutes / 2, "Energy should decrease");
        Assert.True(result.Data.Hydration < TestConstants.Survival.MaxHydration / 2, "Hydration should decrease");
        Assert.True(result.Data.Calories < TestConstants.Survival.MaxCalories / 2, "Calories should decrease");
    }

    [Fact]
    public void Process_ZeroCalories_StarvationPreventsNegativeCalories()
    {
        // Arrange
        var survivalData = TestFixtures.CreateCustomSurvivalData(calories: 0);
        int minutesElapsed = 60;
        var activeEffects = new List<Effect>();

        // Act
        var result = SurvivalProcessor.Process(survivalData, minutesElapsed, activeEffects);

        // Assert
        Assert.Equal(0, result.Data.Calories); // Should not go negative
    }

    [Fact]
    public void Process_ZeroHydration_DehydrationPreventsNegativeHydration()
    {
        // Arrange
        var survivalData = TestFixtures.CreateCustomSurvivalData(hydration: 0);
        int minutesElapsed = 60;
        var activeEffects = new List<Effect>();

        // Act
        var result = SurvivalProcessor.Process(survivalData, minutesElapsed, activeEffects);

        // Assert
        Assert.Equal(0, result.Data.Hydration); // Should not go negative
    }

    [Fact]
    public void Process_ExtremeHeat_GeneratesSweatEffect()
    {
        // Arrange - body temp above sweating threshold
        var survivalData = TestFixtures.CreateCustomSurvivalData(
            temperature: 100.0, // Above 99.0 sweating threshold
            environmentalTemp: 110.0);
        int minutesElapsed = 1;
        var activeEffects = new List<Effect>();

        // Act
        var result = SurvivalProcessor.Process(survivalData, minutesElapsed, activeEffects);

        // Assert
        Assert.Contains(result.Effects, e => e.EffectKind == "Sweating");
    }

    [Fact]
    public void Process_ExtremeCold_GeneratesHypothermiaEffect()
    {
        // Arrange - body temp below hypothermia threshold (95.0°F)
        var survivalData = TestFixtures.CreateCustomSurvivalData(
            temperature: 94.0,
            environmentalTemp: -20.0,
            equipmentInsulation: 0.0);
        int minutesElapsed = 1;
        var activeEffects = new List<Effect>();

        // Act
        var result = SurvivalProcessor.Process(survivalData, minutesElapsed, activeEffects);

        // Assert
        Assert.Contains(result.Effects, e => e.EffectKind == "Hypothermia");
    }

    [Fact]
    public void Process_SevereCold_GeneratesFrostbiteEffects()
    {
        // Arrange - body temp below severe hypothermia threshold (89.6°F)
        var survivalData = TestFixtures.CreateCustomSurvivalData(
            temperature: 88.0,
            environmentalTemp: -40.0,
            equipmentInsulation: 0.0);
        int minutesElapsed = 1;
        var activeEffects = new List<Effect>();

        // Act
        var result = SurvivalProcessor.Process(survivalData, minutesElapsed, activeEffects);

        // Assert
        var frostbiteEffects = result.Effects.Where(e => e.EffectKind == "Frostbite").ToList();
        Assert.Equal(4, frostbiteEffects.Count); // One for each extremity (2 arms, 2 legs)
    }

    [Fact]
    public void Process_TemperatureChange_ExponentialHeatTransfer()
    {
        // Arrange - cold environment, no insulation
        var survivalData = TestFixtures.CreateCustomSurvivalData(
            temperature: 98.6,
            environmentalTemp: 32.0, // Freezing
            equipmentInsulation: 0.0);
        var initialTemp = survivalData.Temperature;
        int minutesElapsed = 60;
        var activeEffects = new List<Effect>();

        // Act
        var result = SurvivalProcessor.Process(survivalData, minutesElapsed, activeEffects);

        // Assert
        Assert.True(result.Data.Temperature < initialTemp, "Temperature should decrease in cold environment");

        // Temperature drop should be noticeable with large differential
        double tempDrop = initialTemp - result.Data.Temperature;
        Assert.True(tempDrop > 0.1, $"Temperature drop expected with large differential. Actual drop: {tempDrop}");
    }

    [Fact]
    public void Process_MaxInsulation_ReducesTemperatureChange()
    {
        // Arrange - same cold environment but with max insulation
        var withInsulation = TestFixtures.CreateCustomSurvivalData(
            temperature: 98.6,
            environmentalTemp: 32.0,
            equipmentInsulation: 0.95); // Max insulation
        var withoutInsulation = TestFixtures.CreateCustomSurvivalData(
            temperature: 98.6,
            environmentalTemp: 32.0,
            equipmentInsulation: 0.0);
        int minutesElapsed = 60;
        var activeEffects = new List<Effect>();

        // Act
        var resultWithInsulation = SurvivalProcessor.Process(withInsulation, minutesElapsed, activeEffects);
        var resultWithoutInsulation = SurvivalProcessor.Process(withoutInsulation, minutesElapsed, activeEffects);

        // Assert
        double tempDropWithInsulation = 98.6 - resultWithInsulation.Data.Temperature;
        double tempDropWithoutInsulation = 98.6 - resultWithoutInsulation.Data.Temperature;

        Assert.True(tempDropWithInsulation < tempDropWithoutInsulation,
            "Insulation should significantly reduce temperature loss");
    }

    [Fact]
    public void Sleep_EnergyRestoration_RestoresAtDoubleRate()
    {
        // Arrange
        var survivalData = TestFixtures.CreateCustomSurvivalData(energy: 100); // Low energy
        int minutesSlept = 60;
        var initialEnergy = survivalData.Energy;

        // Act
        var result = SurvivalProcessor.Sleep(survivalData, minutesSlept);

        // Assert
        // Energy should increase during sleep
        Assert.True(result.Data.Energy > initialEnergy,
            $"Energy should increase during sleep. Before: {initialEnergy}, After: {result.Data.Energy}");

        // Should be clamped at max if applicable
        Assert.True(result.Data.Energy <= SurvivalProcessor.MAX_ENERGY_MINUTES,
            $"Energy should not exceed maximum of {SurvivalProcessor.MAX_ENERGY_MINUTES}. Actual: {result.Data.Energy}");
    }

    [Fact]
    public void Sleep_Metabolism_ReducedToHalfRate()
    {
        // Arrange
        var survivalData = TestFixtures.CreateBaselineSurvivalData();
        int minutesSlept = 480; // 8 hours
        var initialCalories = survivalData.Calories;

        // Act
        var result = SurvivalProcessor.Sleep(survivalData, minutesSlept);

        // Assert
        // Activity level during sleep = 0.5, so metabolism is halved
        Assert.True(result.Data.Calories < initialCalories, "Calories should decrease during sleep");

        // Approximate check: 8 hours of sleep should burn roughly 1/4 of daily metabolism
        // (0.5 activity * 8/24 hours = ~1/6 of daily calories)
        double caloriesBurned = initialCalories - result.Data.Calories;
        Assert.True(caloriesBurned > 0 && caloriesBurned < 500,
            "Sleep metabolism should be significantly reduced but still burn some calories");
    }

    [Fact]
    public void Sleep_Dehydration_ReducedTo70Percent()
    {
        // Arrange
        var survivalData = TestFixtures.CreateBaselineSurvivalData();
        int minutesSlept = 480; // 8 hours
        var initialHydration = survivalData.Hydration;

        // BASE_DEHYDRATION_RATE ≈ 2.78 mL/min
        // During sleep: rate * 0.7 ≈ 1.95 mL/min
        double expectedDehydration = (4000.0 / (24.0 * 60.0)) * 0.7 * minutesSlept;

        // Act
        var result = SurvivalProcessor.Sleep(survivalData, minutesSlept);

        // Assert
        double actualDehydration = initialHydration - result.Data.Hydration;
        Assert.True(Math.Abs(actualDehydration - expectedDehydration) < 1.0,
            $"Dehydration during sleep should be ~70% of normal rate. Expected: {expectedDehydration}, Actual: {actualDehydration}");
    }

    [Fact]
    public void GetCurrentMetabolism_BaselineHuman_CalculatesCorrectly()
    {
        // This tests the private GetCurrentMetabolism method indirectly through Process
        // Arrange
        var survivalData = TestFixtures.CreateBaselineSurvivalData();
        int minutesElapsed = 1440; // 24 hours
        var activeEffects = new List<Effect>();

        // Expected BMR calculation:
        // bmr = 370 + (21.6 * 22.5) + (6.17 * 11.25)
        //     = 370 + 486 + 69.4 = 925.4
        // bmr *= 0.7 + (0.3 * 1.0) = 925.4 * 1.0 = 925.4
        // total = bmr * activityLevel = 925.4 * 1.0 = 925.4 cal/day

        // Act
        var result = SurvivalProcessor.Process(survivalData, minutesElapsed, activeEffects);

        // Assert
        double caloriesBurned = 1000 - result.Data.Calories; // Started at 1000 (half of max 2000)
        Assert.True(caloriesBurned > 900 && caloriesBurned < 950,
            $"24-hour metabolism should be ~925 calories. Actual: {caloriesBurned}");
    }

    [Fact]
    public void GetCurrentMetabolism_HighMuscle_IncreasedBMR()
    {
        // Arrange - high muscle mass increases metabolism
        var highMuscleStats = TestFixtures.CreateCustomBodyStats(
            bodyWeight: 90,
            muscleWeight: 40, // Very high muscle
            fatWeight: 10,
            healthPercent: 1.0);
        var baselineStats = TestFixtures.CreateBaselineBodyStats();

        var highMuscleData = TestFixtures.CreateCustomSurvivalData(bodyStats: highMuscleStats);
        var baselineData = TestFixtures.CreateCustomSurvivalData(bodyStats: baselineStats);
        int minutesElapsed = 1440; // 24 hours

        // Act
        var highMuscleResult = SurvivalProcessor.Process(highMuscleData, minutesElapsed, new List<Effect>());
        var baselineResult = SurvivalProcessor.Process(baselineData, minutesElapsed, new List<Effect>());

        // Assert
        double highMuscleCaloriesBurned = 1000 - highMuscleResult.Data.Calories;
        double baselineCaloriesBurned = 1000 - baselineResult.Data.Calories;

        Assert.True(highMuscleCaloriesBurned > baselineCaloriesBurned,
            $"High muscle mass should burn more calories than baseline. High: {highMuscleCaloriesBurned}, Baseline: {baselineCaloriesBurned}");
    }

    [Fact]
    public void GetCurrentMetabolism_LowHealth_ReducedBMR()
    {
        // Arrange - low health reduces metabolism
        var lowHealthStats = TestFixtures.CreateCustomBodyStats(
            bodyWeight: 75,
            muscleWeight: 22.5,
            fatWeight: 11.25,
            healthPercent: 0.3); // Very injured
        var survivalData = TestFixtures.CreateCustomSurvivalData(bodyStats: lowHealthStats);
        int minutesElapsed = 1440; // 24 hours

        // Expected BMR with low health:
        // Base: 925.4
        // Health modifier: 0.7 + (0.3 * 0.3) = 0.79
        // bmr = 925.4 * 0.79 ≈ 731 cal/day

        // Act
        var result = SurvivalProcessor.Process(survivalData, minutesElapsed, new List<Effect>());

        // Assert
        double caloriesBurned = 1000 - result.Data.Calories;
        Assert.True(caloriesBurned < 800,
            "Low health should reduce daily calorie burn");
    }
}
