namespace text_survival.Items;

/// <summary>
/// Types of fuel for fires, each with distinct burning characteristics
/// </summary>
public enum FuelType
{
    Tinder,    // Grass, leaves, dry bark - ignites easily, burns fast
    Kindling,  // Small branches, twigs - builds fire
    Softwood,  // Pine, spruce - moderate heat, standard burn rate
    Hardwood,  // Oak, ash - high heat, slow burn
    Bone,      // Animal bones - moderate heat, very slow burn
    Peat       // Dried peat - moderate heat, slow burn, smoky
}

/// <summary>
/// Physical and combustion properties for a fuel type
/// </summary>
public readonly struct FuelProperties
{
    /// <summary>
    /// Maximum combustion temperature in Fahrenheit
    /// </summary>
    public double PeakTemperature { get; init; }

    /// <summary>
    /// Rate at which fuel is consumed (kg per hour)
    /// </summary>
    public double BurnRateKgPerHour { get; init; }

    /// <summary>
    /// Minimum fire temperature (°F) required to add this fuel type
    /// 0 = can ignite from cold fire
    /// </summary>
    public double MinFireTemperature { get; init; }

    /// <summary>
    /// Bonus to fire-starting skill check (as decimal, e.g., 0.15 = +15%)
    /// </summary>
    public double IgnitionBonus { get; init; }

    /// <summary>
    /// Time in minutes for fuel to reach peak temperature after being added
    /// </summary>
    public double StartupTimeMinutes { get; init; }

    public FuelProperties(
        double peakTemperature,
        double burnRateKgPerHour,
        double minFireTemperature = 0,
        double ignitionBonus = 0,
        double startupTimeMinutes = 10)
    {
        PeakTemperature = peakTemperature;
        BurnRateKgPerHour = burnRateKgPerHour;
        MinFireTemperature = minFireTemperature;
        IgnitionBonus = ignitionBonus;
        StartupTimeMinutes = startupTimeMinutes;
    }
}

/// <summary>
/// Database of fuel type characteristics for realistic fire physics
/// </summary>
public static class FuelDatabase
{
    private static readonly Dictionary<FuelType, FuelProperties> _properties = new()
    {
        [FuelType.Tinder] = new FuelProperties(
            peakTemperature: 450,        // Low heat, just gets fire going
            burnRateKgPerHour: 3.0,      // Burns very fast (3kg/hour)
            minFireTemperature: 0,       // Can ignite from cold
            ignitionBonus: 0.15,         // +15% to fire-starting success
            startupTimeMinutes: 3        // Quick ignition
        ),

        [FuelType.Kindling] = new FuelProperties(
            peakTemperature: 600,        // Moderate heat, builds fire
            burnRateKgPerHour: 1.5,      // Burns moderately fast (1.5kg/hour)
            minFireTemperature: 0,       // Can ignite from cold
            ignitionBonus: 0,            // No ignition bonus
            startupTimeMinutes: 8        // Takes time to catch fully
        ),

        [FuelType.Softwood] = new FuelProperties(
            peakTemperature: 750,        // Good heat output
            burnRateKgPerHour: 1.0,      // Baseline burn rate (1kg/hour)
            minFireTemperature: 200,     // Need some fire establishment, but not roaring
            ignitionBonus: 0,
            startupTimeMinutes: 15       // Takes time to reach full burn
        ),

        [FuelType.Hardwood] = new FuelProperties(
            peakTemperature: 900,        // Highest heat output
            burnRateKgPerHour: 0.7,      // Slow, efficient burn (0.7kg/hour)
            minFireTemperature: 500,     // Need hot fire to ignite
            ignitionBonus: 0,
            startupTimeMinutes: 20       // Slow to reach full combustion
        ),

        [FuelType.Bone] = new FuelProperties(
            peakTemperature: 650,        // Moderate heat, cooler than wood
            burnRateKgPerHour: 0.5,      // Very slow burn (0.5kg/hour) - excellent efficiency
            minFireTemperature: 600,     // Need very hot fire to burn bone
            ignitionBonus: 0,
            startupTimeMinutes: 25       // Very slow to ignite fully
        ),

        [FuelType.Peat] = new FuelProperties(
            peakTemperature: 700,        // Moderate-high heat
            burnRateKgPerHour: 0.8,      // Slow burn (0.8kg/hour)
            minFireTemperature: 400,     // Need established fire
            ignitionBonus: 0,
            startupTimeMinutes: 18       // Moderate startup
        )
    };

    /// <summary>
    /// Get the fuel properties for a given fuel type
    /// </summary>
    public static FuelProperties Get(FuelType fuelType)
    {
        return _properties[fuelType];
    }

    /// <summary>
    /// Check if a fuel type exists in the database
    /// </summary>
    public static bool Has(FuelType fuelType)
    {
        return _properties.ContainsKey(fuelType);
    }

    /// <summary>
    /// Get all defined fuel types
    /// </summary>
    public static IEnumerable<FuelType> GetAllFuelTypes()
    {
        return _properties.Keys;
    }
}
