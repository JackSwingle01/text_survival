namespace text_survival.Tests.TestHelpers;

/// <summary>
/// Factory methods for creating test objects
/// </summary>
public static class TestFixtures
{
    /// <summary>
    /// Creates a baseline human body with default stats
    /// </summary>
    public static Body CreateBaselineHumanBody(string ownerName = "TestHuman")
    {
        var creationInfo = new BodyCreationInfo
        {
            type = BodyTypes.Human,
            overallWeight = TestConstants.BaselineHuman.Weight,
            fatPercent = TestConstants.BaselineHuman.FatPercent,
            musclePercent = TestConstants.BaselineHuman.MusclePercent,
            IsPlayer = false
        };

        // Create a temporary null owner - tests don't need the full actor
        var effectRegistry = new EffectRegistry(null!);
        return new Body(ownerName, creationInfo, effectRegistry);
    }

    /// <summary>
    /// Creates a human body with custom body composition
    /// </summary>
    public static Body CreateCustomHumanBody(
        double weight = 75.0,
        double fatPercent = 0.15,
        double musclePercent = 0.30,
        string ownerName = "TestHuman",
        bool isPlayer = false)
    {
        var creationInfo = new BodyCreationInfo
        {
            type = BodyTypes.Human,
            overallWeight = weight,
            fatPercent = fatPercent,
            musclePercent = musclePercent,
            IsPlayer = isPlayer
        };

        var effectRegistry = new EffectRegistry(null!);
        return new Body(ownerName, creationInfo, effectRegistry);
    }

    /// <summary>
    /// Creates baseline survival data for testing
    /// </summary>
    public static SurvivalData CreateBaselineSurvivalData()
    {
        return new SurvivalData
        {
            Calories = TestConstants.Survival.MaxCalories / 2, // Half full
            Hydration = TestConstants.Survival.MaxHydration / 2, // Half full
            Energy = TestConstants.Survival.MaxEnergyMinutes / 2, // Half rested
            Temperature = TestConstants.Temperature.NormalBodyTemp,
            ColdResistance = 0.5,
            BodyStats = CreateBaselineBodyStats(),
            equipmentInsulation = 0.0,
            environmentalTemp = TestConstants.Temperature.RoomTemp,
            activityLevel = 1.0,
            IsPlayer = false
        };
    }

    /// <summary>
    /// Creates custom survival data with specified parameters
    /// </summary>
    public static SurvivalData CreateCustomSurvivalData(
        double? calories = null,
        double? hydration = null,
        double? energy = null,
        double? temperature = null,
        double? coldResistance = null,
        BodyStats? bodyStats = null,
        double? equipmentInsulation = null,
        double? environmentalTemp = null,
        double? activityLevel = null,
        bool isPlayer = false)
    {
        var baseline = CreateBaselineSurvivalData();

        return new SurvivalData
        {
            Calories = calories ?? baseline.Calories,
            Hydration = hydration ?? baseline.Hydration,
            Energy = energy ?? baseline.Energy,
            Temperature = temperature ?? baseline.Temperature,
            ColdResistance = coldResistance ?? baseline.ColdResistance,
            BodyStats = bodyStats ?? baseline.BodyStats,
            equipmentInsulation = equipmentInsulation ?? baseline.equipmentInsulation,
            environmentalTemp = environmentalTemp ?? baseline.environmentalTemp,
            activityLevel = activityLevel ?? baseline.activityLevel,
            IsPlayer = isPlayer
        };
    }

    /// <summary>
    /// Creates baseline body stats
    /// </summary>
    public static BodyStats CreateBaselineBodyStats()
    {
        return new BodyStats
        {
            BodyWeight = TestConstants.BaselineHuman.Weight,
            MuscleWeight = TestConstants.BaselineHuman.MuscleWeight,
            FatWeight = TestConstants.BaselineHuman.FatWeight,
            HealthPercent = 1.0
        };
    }

    /// <summary>
    /// Creates custom body stats
    /// </summary>
    public static BodyStats CreateCustomBodyStats(
        double bodyWeight,
        double muscleWeight,
        double fatWeight,
        double healthPercent = 1.0)
    {
        return new BodyStats
        {
            BodyWeight = bodyWeight,
            MuscleWeight = muscleWeight,
            FatWeight = fatWeight,
            HealthPercent = healthPercent
        };
    }

    /// <summary>
    /// Creates a capacity container with all capacities set to a value
    /// </summary>
    public static CapacityContainer CreateUniformCapacities(double value = 1.0)
    {
        return new CapacityContainer
        {
            Moving = value,
            Manipulation = value,
            Breathing = value,
            BloodPumping = value,
            Consciousness = value,
            Sight = value,
            Hearing = value,
            Digestion = value
        };
    }

    /// <summary>
    /// Creates damage info for testing
    /// </summary>
    public static DamageInfo CreateDamageInfo(
        double amount,
        DamageType damageType = DamageType.Blunt,
        string? targetPartName = null)
    {
        return new DamageInfo
        {
            Amount = amount,
            Type = damageType,
            TargetPartName = targetPartName
        };
    }
}
