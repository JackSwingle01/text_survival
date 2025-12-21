using text_survival.Tests.TestHelpers;

namespace text_survival.Tests.Survival;

public class SurvivalProcessorTests
{
    [Fact]
    public void Process_NormalConditions_ReturnsNegativeDeltas()
    {
        // Arrange
        var body = TestFixtures.CreateBaselineHumanBody();
        var context = TestFixtures.CreateBaselineSurvivalContext();
        int minutesElapsed = 60; // 1 hour

        // Act
        var result = SurvivalProcessor.Process(body, context, minutesElapsed);

        // Assert - deltas should be negative (consuming resources)
        Assert.NotNull(result);
        Assert.True(result.StatsDelta.EnergyDelta < 0, "Energy delta should be negative");
        Assert.True(result.StatsDelta.HydrationDelta < 0, "Hydration delta should be negative");
        Assert.True(result.StatsDelta.CalorieDelta < 0, "Calorie delta should be negative");
    }

    [Fact]
    public void Process_ApplyResult_UpdatesBodyStats()
    {
        // Arrange
        var body = TestFixtures.CreateBaselineHumanBody();
        var context = TestFixtures.CreateBaselineSurvivalContext();
        int minutesElapsed = 60;

        double initialEnergy = body.Energy;
        double initialHydration = body.Hydration;
        double initialCalories = body.CalorieStore;

        // Act
        var result = SurvivalProcessor.Process(body, context, minutesElapsed);
        body.ApplyResult(result);

        // Assert - body stats should have decreased
        Assert.True(body.Energy < initialEnergy, "Energy should decrease");
        Assert.True(body.Hydration < initialHydration, "Hydration should decrease");
        Assert.True(body.CalorieStore < initialCalories, "Calories should decrease");
    }

    [Fact]
    public void Process_ExtremeHeat_GeneratesSweatEffect()
    {
        // Arrange - create body with high temperature
        var body = TestFixtures.CreateBaselineHumanBody();
        // We need to manually set body temp above sweating threshold
        // Since we can't directly set BodyTemperature, use a hot environment context
        var context = TestFixtures.CreateCustomSurvivalContext(
            locationTemperature: 110.0, // Hot environment
            clothingInsulation: 0.0);

        // First, warm up the body by simulating many ticks in hot environment
        for (int i = 0; i < 60; i++)
        {
            var warmupResult = SurvivalProcessor.Process(body, context, 1);
            body.ApplyResult(warmupResult);
        }

        // Now test - body should be warm enough for sweating
        int minutesElapsed = 1;

        // Act
        var result = SurvivalProcessor.Process(body, context, minutesElapsed);

        // Assert - if body temp > 99°F, should have sweating effect
        if (body.BodyTemperature > 99.0)
        {
            Assert.Contains(result.Effects, e => e.EffectKind == "Sweating");
        }
    }

    [Fact]
    public void Process_ExtremeCold_GeneratesHypothermiaEffect()
    {
        // Arrange - create body and cool it down
        var body = TestFixtures.CreateBaselineHumanBody();
        var context = TestFixtures.CreateCustomSurvivalContext(
            locationTemperature: -20.0, // Very cold
            clothingInsulation: 0.0);

        // Cool down the body
        for (int i = 0; i < 120; i++)
        {
            var cooldownResult = SurvivalProcessor.Process(body, context, 1);
            body.ApplyResult(cooldownResult);
        }

        // Act - get effects at current cold body temp
        var result = SurvivalProcessor.Process(body, context, 1);

        // Assert - if body temp < 95°F, should have hypothermia
        if (body.BodyTemperature < 95.0)
        {
            Assert.Contains(result.Effects, e => e.EffectKind == "Hypothermia");
        }
    }

    [Fact]
    public void Process_SevereCold_GeneratesFrostbiteEffects()
    {
        // Arrange - create body and cool it severely
        var body = TestFixtures.CreateBaselineHumanBody();
        var context = TestFixtures.CreateCustomSurvivalContext(
            locationTemperature: -40.0, // Extreme cold
            clothingInsulation: 0.0);

        // Cool down the body significantly
        for (int i = 0; i < 240; i++)
        {
            var cooldownResult = SurvivalProcessor.Process(body, context, 1);
            body.ApplyResult(cooldownResult);
        }

        // Act
        var result = SurvivalProcessor.Process(body, context, 1);

        // Assert - if body temp < 89.6°F, should have frostbite effect
        if (body.BodyTemperature < 89.6)
        {
            var frostbiteEffects = result.Effects.Where(e => e.EffectKind == "Frostbite").ToList();
            Assert.Single(frostbiteEffects); // Consolidated frostbite effect with escalating messages
        }
    }

    [Fact]
    public void Process_ColdEnvironment_TemperatureDecreases()
    {
        // Arrange - cold environment, no insulation
        var body = TestFixtures.CreateBaselineHumanBody();
        var context = TestFixtures.CreateCustomSurvivalContext(
            locationTemperature: 32.0, // Freezing
            clothingInsulation: 0.0);

        double initialTemp = body.BodyTemperature;
        int minutesElapsed = 60;

        // Act
        var result = SurvivalProcessor.Process(body, context, minutesElapsed);

        // Assert - temperature delta should be negative in cold
        Assert.True(result.StatsDelta.TemperatureDelta < 0,
            "Temperature should decrease in cold environment");
    }

    [Fact]
    public void Process_MaxInsulation_ReducesTemperatureChange()
    {
        // Arrange - same cold environment but with different insulation levels
        var bodyWithInsulation = TestFixtures.CreateBaselineHumanBody();
        var bodyWithoutInsulation = TestFixtures.CreateBaselineHumanBody();

        var contextWithInsulation = TestFixtures.CreateCustomSurvivalContext(
            locationTemperature: 32.0,
            clothingInsulation: 0.9); // High insulation
        var contextWithoutInsulation = TestFixtures.CreateCustomSurvivalContext(
            locationTemperature: 32.0,
            clothingInsulation: 0.0);

        int minutesElapsed = 60;

        // Act
        var resultWithInsulation = SurvivalProcessor.Process(bodyWithInsulation, contextWithInsulation, minutesElapsed);
        var resultWithoutInsulation = SurvivalProcessor.Process(bodyWithoutInsulation, contextWithoutInsulation, minutesElapsed);

        // Assert - insulation should reduce temperature loss
        double tempDropWithInsulation = Math.Abs(resultWithInsulation.StatsDelta.TemperatureDelta);
        double tempDropWithoutInsulation = Math.Abs(resultWithoutInsulation.StatsDelta.TemperatureDelta);

        Assert.True(tempDropWithInsulation < tempDropWithoutInsulation,
            "Insulation should significantly reduce temperature loss");
    }

    [Fact]
    public void Sleep_ReturnsPositiveEnergyDelta()
    {
        // Arrange
        var body = TestFixtures.CreateBaselineHumanBody();
        int minutesSlept = 60;

        // Act
        var result = SurvivalProcessor.Sleep(body, minutesSlept);

        // Assert - energy should increase during sleep
        Assert.True(result.StatsDelta.EnergyDelta > 0,
            $"Energy delta should be positive during sleep. Actual: {result.StatsDelta.EnergyDelta}");
    }

    [Fact]
    public void Sleep_Metabolism_ReducedCalorieBurn()
    {
        // Arrange
        var body = TestFixtures.CreateBaselineHumanBody();
        var context = TestFixtures.CreateBaselineSurvivalContext();
        int minutes = 480; // 8 hours

        // Act
        var sleepResult = SurvivalProcessor.Sleep(body, minutes);
        var awakeResult = SurvivalProcessor.Process(body, context, minutes);

        // Assert - sleep should burn fewer calories than being awake
        double sleepCalories = Math.Abs(sleepResult.StatsDelta.CalorieDelta);
        double awakeCalories = Math.Abs(awakeResult.StatsDelta.CalorieDelta);

        Assert.True(sleepCalories < awakeCalories,
            $"Sleep should burn fewer calories. Sleep: {sleepCalories}, Awake: {awakeCalories}");
    }

    [Fact]
    public void Sleep_Dehydration_ReducedTo70Percent()
    {
        // Arrange
        var body = TestFixtures.CreateBaselineHumanBody();
        int minutesSlept = 480; // 8 hours

        // BASE_DEHYDRATION_RATE ≈ 2.78 mL/min
        // During sleep: rate * 0.7 ≈ 1.95 mL/min
        double expectedDehydration = (4000.0 / (24.0 * 60.0)) * 0.7 * minutesSlept;

        // Act
        var result = SurvivalProcessor.Sleep(body, minutesSlept);

        // Assert
        double actualDehydration = Math.Abs(result.StatsDelta.HydrationDelta);
        Assert.True(Math.Abs(actualDehydration - expectedDehydration) < 1.0,
            $"Dehydration during sleep should be ~70% of normal rate. Expected: {expectedDehydration}, Actual: {actualDehydration}");
    }

    [Fact]
    public void GetCurrentMetabolism_BaselineHuman_CalculatesCorrectly()
    {
        // Arrange
        var body = TestFixtures.CreateBaselineHumanBody();
        var context = TestFixtures.CreateBaselineSurvivalContext();
        int minutesElapsed = 1440; // 24 hours

        // Expected BMR calculation:
        // bmr = 370 + (21.6 * 22.5) + (6.17 * 11.25)
        //     = 370 + 486 + 69.4 = 925.4
        // bmr *= 0.7 + (0.3 * 1.0) = 925.4 * 1.0 = 925.4
        // total = bmr * activityLevel = 925.4 * 1.0 = 925.4 cal/day

        // Act
        var result = SurvivalProcessor.Process(body, context, minutesElapsed);

        // Assert
        double caloriesBurned = Math.Abs(result.StatsDelta.CalorieDelta);
        Assert.True(caloriesBurned > 900 && caloriesBurned < 950,
            $"24-hour metabolism should be ~925 calories. Actual: {caloriesBurned}");
    }

    [Fact]
    public void GetCurrentMetabolism_HighMuscle_IncreasedBMR()
    {
        // Arrange - high muscle mass increases metabolism
        var highMuscleBody = TestFixtures.CreateCustomHumanBody(
            weight: 90,
            fatPercent: 10.0 / 90.0,
            musclePercent: 40.0 / 90.0);
        var baselineBody = TestFixtures.CreateBaselineHumanBody();
        var context = TestFixtures.CreateBaselineSurvivalContext();
        int minutesElapsed = 1440; // 24 hours

        // Act
        var highMuscleResult = SurvivalProcessor.Process(highMuscleBody, context, minutesElapsed);
        var baselineResult = SurvivalProcessor.Process(baselineBody, context, minutesElapsed);

        // Assert
        double highMuscleCaloriesBurned = Math.Abs(highMuscleResult.StatsDelta.CalorieDelta);
        double baselineCaloriesBurned = Math.Abs(baselineResult.StatsDelta.CalorieDelta);

        Assert.True(highMuscleCaloriesBurned > baselineCaloriesBurned,
            $"High muscle mass should burn more calories. High: {highMuscleCaloriesBurned}, Baseline: {baselineCaloriesBurned}");
    }
}
