using text_survival.Bodies;
using text_survival.Environments.Features;
using text_survival.Items;

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

        return new Body(ownerName, creationInfo);
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

        return new Body(ownerName, creationInfo);
    }

    /// <summary>
    /// Creates a baseline survival context for testing
    /// </summary>
    public static SurvivalContext CreateBaselineSurvivalContext()
    {
        return new SurvivalContext
        {
            LocationTemperature = TestConstants.Temperature.RoomTemp,
            ClothingInsulation = 0.0,
            ActivityLevel = 1.0
        };
    }

    /// <summary>
    /// Creates a custom survival context with specified parameters
    /// </summary>
    public static SurvivalContext CreateCustomSurvivalContext(
        double? locationTemperature = null,
        double? clothingInsulation = null,
        double? activityLevel = null)
    {
        return new SurvivalContext
        {
            LocationTemperature = locationTemperature ?? TestConstants.Temperature.RoomTemp,
            ClothingInsulation = clothingInsulation ?? 0.0,
            ActivityLevel = activityLevel ?? 1.0
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

    /// <summary>
    /// Creates a test fire with specified fuel mixture.
    /// Fuel is added and ignited so the fire is actively burning.
    /// </summary>
    public static HeatSourceFeature CreateTestFire(
        double initialFuelKg = 0,
        FuelType? fuelType = null,
        double maxCapacity = 12.0)
    {
        var fire = new HeatSourceFeature(maxCapacity);

        // Add fuel if specified
        if (initialFuelKg > 0 && fuelType.HasValue)
        {
            fire.AddFuel(initialFuelKg, fuelType.Value);
            // Ignite the fuel so the fire is actively burning
            fire.IgniteAll();
        }

        return fire;
    }

    /// <summary>
    /// Helper to convert FuelType enum to ItemProperty (legacy, may not be needed)
    /// </summary>
    private static ItemProperty GetFuelProperty(FuelType fuelType)
    {
        return fuelType switch
        {
            FuelType.Tinder => ItemProperty.Fuel_Tinder,
            FuelType.Kindling => ItemProperty.Fuel_Kindling,
            FuelType.Softwood => ItemProperty.Fuel_Softwood,
            FuelType.Hardwood => ItemProperty.Fuel_Hardwood,
            FuelType.Bone => ItemProperty.Fuel_Bone,
            FuelType.Peat => ItemProperty.Fuel_Peat,
            _ => ItemProperty.Fuel_Tinder
        };
    }
}
