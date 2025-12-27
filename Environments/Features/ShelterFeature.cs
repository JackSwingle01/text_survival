namespace text_survival.Environments.Features;

public class ShelterFeature : LocationFeature
{
    public override string? MapIcon => !IsDestroyed ? (IsSnowShelter ? "ac_unit" : "cabin") : null;
    public override int IconPriority => 5; // Shelter is important

    public double TemperatureInsulation { get; set; } = 0; // ambient temp protection 0-1
    public double OverheadCoverage { get; set; } = 0; // rain / snow / sun protection 0-1
    public double WindCoverage { get; set; } = 0; // wind protection 0-1

    /// <summary>
    /// Whether this is a snow shelter that degrades in warm temperatures.
    /// </summary>
    public bool IsSnowShelter { get; init; } = false;

    public ShelterFeature(string name, double tempInsulation, double overheadCoverage, double windCoverage, bool isSnowShelter = false) : base(name)
    {
        TemperatureInsulation = tempInsulation;
        OverheadCoverage = overheadCoverage;
        WindCoverage = windCoverage;
        IsSnowShelter = isSnowShelter;
    }

    [System.Text.Json.Serialization.JsonConstructor]
    public ShelterFeature() : base() { }

    /// <summary>
    /// Update shelter state. Snow shelters degrade in warm temperatures.
    /// </summary>
    public override void Update(int minutes)
    {
        // Snow shelters melt when temperature rises above freezing
        if (IsSnowShelter)
        {
            // Note: We don't have direct access to location temperature here.
            // This will need to be called from Location.Update() with temp info,
            // or we check later during gameplay.
            // For now, we'll add the logic structure and integrate it properly later.
        }
    }

    /// <summary>
    /// Degrade snow shelter when temperature is above freezing.
    /// Called from location with temperature context.
    /// </summary>
    public void DegradeFromWarmth(double locationTempF, int minutes)
    {
        if (!IsSnowShelter) return;
        if (locationTempF <= 40) return;  // Snow stable below 40°F

        // Degrade based on how warm it is and time elapsed
        double warmthDelta = locationTempF - 40;
        double degradeRate = 0.01 * (warmthDelta / 20.0);  // Faster degradation in warmer temps
        double damageAmount = degradeRate * (minutes / 60.0);

        Damage(damageAmount);
    }

    /// <summary>
    /// Get the overall quality of the shelter (average of all coverages).
    /// </summary>
    public double Quality => (TemperatureInsulation + OverheadCoverage + WindCoverage) / 3.0;

    /// <summary>
    /// Damage the shelter, reducing all coverage values proportionally.
    /// </summary>
    /// <param name="amount">Amount to reduce quality (0-1)</param>
    public void Damage(double amount)
    {
        TemperatureInsulation = Math.Max(0, TemperatureInsulation - amount);
        OverheadCoverage = Math.Max(0, OverheadCoverage - amount);
        WindCoverage = Math.Max(0, WindCoverage - amount);
    }

    /// <summary>
    /// Repair the shelter, increasing coverage values.
    /// </summary>
    /// <param name="amount">Amount to increase quality (0-1)</param>
    public void Repair(double amount)
    {
        TemperatureInsulation = Math.Min(1, TemperatureInsulation + amount);
        OverheadCoverage = Math.Min(1, OverheadCoverage + amount);
        WindCoverage = Math.Min(1, WindCoverage + amount);
    }

    /// <summary>
    /// Check if the shelter is still functional (any protection remaining).
    /// </summary>
    public bool IsDestroyed => Quality <= 0.05;

    /// <summary>
    /// Check if shelter is compromised (quality below safe threshold).
    /// Used for events about shelter needing repair.
    /// </summary>
    public bool IsCompromised => Quality <= 0.3;

    /// <summary>
    /// Check if shelter is weakened but still functional.
    /// Used for warning events before shelter fails.
    /// </summary>
    public bool IsWeakened => Quality > 0.3 && Quality <= 0.5;

    /// <summary>
    /// Check if shelter needs repair (any damage).
    /// </summary>
    public bool NeedsRepair => Quality < 1.0 && !IsDestroyed;

    #region Save/Load Support

    /// <summary>
    /// Restore shelter state from save data.
    /// </summary>
    internal void RestoreState(double tempInsulation, double overheadCoverage, double windCoverage)
    {
        TemperatureInsulation = tempInsulation;
        OverheadCoverage = overheadCoverage;
        WindCoverage = windCoverage;
    }

    #endregion

    #region Factory Methods

    /// <summary>
    /// Create a basic lean-to shelter.
    /// Quick to build, provides basic protection from rain and some wind.
    /// </summary>
    public static ShelterFeature CreateLeanTo() => new("Lean-to",
        tempInsulation: 0.3,
        overheadCoverage: 0.7,  // Good rain protection
        windCoverage: 0.4,
        isSnowShelter: false
    );

    /// <summary>
    /// Create a snow shelter dug into deep snow.
    /// Excellent insulation and protection, but melts in warm temperatures.
    /// </summary>
    public static ShelterFeature CreateSnowShelter() => new("Snow shelter",
        tempInsulation: 0.6,  // Snow is excellent insulator
        overheadCoverage: 0.9,
        windCoverage: 0.8,
        isSnowShelter: true   // Will degrade if temp > 40°F
    );

    /// <summary>
    /// Create a hide tent supported by wooden poles.
    /// Good all-around protection, portable.
    /// </summary>
    public static ShelterFeature CreateHideTent() => new("Hide tent",
        tempInsulation: 0.5,
        overheadCoverage: 0.9,
        windCoverage: 0.7,
        isSnowShelter: false
    );

    /// <summary>
    /// Create a permanent cabin structure.
    /// Best protection from all weather conditions.
    /// </summary>
    public static ShelterFeature CreateCabin() => new("Cabin",
        tempInsulation: 0.8,
        overheadCoverage: 0.95,
        windCoverage: 0.9,
        isSnowShelter: false
    );

    #endregion
}