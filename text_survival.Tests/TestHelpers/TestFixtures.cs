using text_survival.Actors;
using text_survival.Environments;
using text_survival.Environments.Factories;
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

    /// <summary>
    /// Creates a test fire with specified fuel mixture
    /// </summary>
    public static HeatSourceFeature CreateTestFire(
        double initialFuelKg = 0,
        FuelType? fuelType = null,
        double maxCapacity = 12.0,
        bool isActive = false)
    {
        // Use factory to create zone with locations
        var zone = ZoneFactory.MakeForestZone("TestZone", "A test zone", baseTemp: 0); // 0°C = 32°F
        var location = zone.Locations[0]; // Get first generated location

        var fire = new HeatSourceFeature(location, maxCapacity);

        // Add fuel if specified
        if (initialFuelKg > 0 && fuelType.HasValue)
        {
            // Create a test fuel item
            var fuelItem = new Item($"Test {fuelType.Value}", initialFuelKg)
            {
                FuelMassKg = initialFuelKg,
                CraftingProperties = new List<ItemProperty> { GetFuelProperty(fuelType.Value) }
            };

            fire.AddFuel(fuelItem, initialFuelKg);

            if (isActive)
            {
                fire.SetActive(true);
            }
        }

        return fire;
    }

    /// <summary>
    /// Helper to convert FuelType enum to ItemProperty
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
