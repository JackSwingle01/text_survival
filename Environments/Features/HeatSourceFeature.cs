using text_survival.Items;

namespace text_survival.Environments.Features;

public class HeatSourceFeature : LocationFeature
{
    // Fire State
    public bool IsActive => FuelMassKg > 0;
    public bool HasEmbers { get; private set; }

    // Fuel Management (mass-based, not time-based)
    public double FuelMassKg { get; private set; }
    public double MaxFuelCapacityKg { get; private set; }
    private Dictionary<FuelType, double> _fuelMixture = []; // Track fuel types and their masses

    public double HoursRemaining
    {
        get
        {
            if (FuelMassKg <= 0) return 0;
            double burnRate = GetWeightedBurnRate();
            return burnRate > 0 ? FuelMassKg / burnRate : 0;
        }
    }

    // Fire Physics
    public double FireAgeMinutes { get; private set; } // Time since fire started/rekindled
    private double _emberDuration; // Total ember duration when embers started
    public double EmberTimeRemaining { get; private set; } // Time remaining for embers

    public HeatSourceFeature(double maxCapacityKg = 12.0)
        : base("Campfire")
    {
        HasEmbers = false;
        FuelMassKg = 0;
        MaxFuelCapacityKg = maxCapacityKg;
        FireAgeMinutes = 0;
        EmberTimeRemaining = 0;
        _emberDuration = 0;
    }

    #region Temperature Calculations

    /// <summary>
    /// Get the current internal fire temperature (°F).
    /// This is the combustion temperature, not the ambient heat output.
    /// </summary>
    public double GetCurrentFireTemperature()
    {
        if (!IsActive && !HasEmbers)
            return 0;

        if (HasEmbers)
            return GetEmberTemperature();

        return GetActiveFireTemperature();
    }

    /// <summary>
    /// Calculate active fire temperature based on physics model
    /// </summary>
    private double GetActiveFireTemperature()
    {
        double peakTemp = GetWeightedPeakTemperature();
        double fireSizeMultiplier = GetFireSizeMultiplier();
        double ageMultiplier = GetStartupMultiplier();
        double declineMultiplier = GetDeclineMultiplier();

        return peakTemp * fireSizeMultiplier * ageMultiplier * declineMultiplier;
    }

    /// <summary>
    /// Calculate ember phase temperature with exponential decay
    /// </summary>
    private double GetEmberTemperature()
    {
        if (_emberDuration <= 0) return 300; // Minimum ember temp

        double progress = EmberTimeRemaining / _emberDuration;
        // Square root decay: embers cool slowly at first, then faster
        return 600.0 * Math.Pow(progress, 0.5);
    }

    /// <summary>
    /// Get weighted average peak temperature from fuel mixture
    /// </summary>
    private double GetWeightedPeakTemperature()
    {
        if (_fuelMixture.Count == 0 || FuelMassKg <= 0) return 450; // Default to tinder temp

        double totalWeightedTemp = 0;
        double totalMass = 0;

        foreach (var (fuelType, mass) in _fuelMixture)
        {
            var props = FuelDatabase.Get(fuelType);
            totalWeightedTemp += props.PeakTemperature * mass;
            totalMass += mass;
        }

        return totalMass > 0 ? totalWeightedTemp / totalMass : 450;
    }

    /// <summary>
    /// Fire size affects temperature (larger fires burn hotter due to better oxygen flow)
    /// </summary>
    private double GetFireSizeMultiplier()
    {
        if (FuelMassKg < 1.0) return 0.7;  // Small fire: inefficient
        if (FuelMassKg < 2.0) return 0.85; // Small-medium
        if (FuelMassKg < 5.0) return 1.0;  // Ideal size
        if (FuelMassKg < 8.0) return 1.05; // Large
        return 1.1; // Very large: maximum efficiency
    }

    /// <summary>
    /// Startup curve: fires take time to reach peak temperature
    /// Sigmoid function: slow start, rapid middle, slow approach to peak
    /// </summary>
    private double GetStartupMultiplier()
    {
        // Get weighted average startup time
        double avgStartupTime = GetWeightedStartupTime();

        // Sigmoid parameters
        double inflectionPoint = avgStartupTime * 0.6; // 60% of startup time
        double steepness = 0.4; // Controls curve sharpness

        // Sigmoid: 1 / (1 + e^(-k * (x - inflection)))
        double exponent = -steepness * (FireAgeMinutes - inflectionPoint);
        double sigmoid = 1.0 / (1.0 + Math.Exp(exponent));

        // Scale from 0.4 to 1.0 (fires start at 40% of peak temp)
        return 0.4 + (sigmoid * 0.6);
    }

    /// <summary>
    /// Get weighted average startup time from fuel mixture
    /// </summary>
    private double GetWeightedStartupTime()
    {
        if (_fuelMixture.Count == 0) return 5; // Default fast startup

        double totalWeightedTime = 0;
        double totalMass = 0;

        foreach (var (fuelType, mass) in _fuelMixture)
        {
            var props = FuelDatabase.Get(fuelType);
            totalWeightedTime += props.StartupTimeMinutes * mass;
            totalMass += mass;
        }

        return totalMass > 0 ? totalWeightedTime / totalMass : 5;
    }

    /// <summary>
    /// Decline curve: temperature drops as fuel depletes below 30%
    /// </summary>
    private double GetDeclineMultiplier()
    {
        double fuelPercent = FuelMassKg / MaxFuelCapacityKg;
        double declineThreshold = 0.3; // 30% fuel remaining

        if (fuelPercent >= declineThreshold) return 1.0; // Full temperature

        // Power function for gradual decline
        return Math.Pow(fuelPercent / declineThreshold, 0.6);
    }

    /// <summary>
    /// Convert internal fire temperature to ambient heat output (°F added to location temp).
    /// Uses physics: heat radiation scales with temperature differential and fire mass.
    /// </summary>
    public double GetEffectiveHeatOutput(double ambientTemp)
    {
        double fireTemp = GetCurrentFireTemperature();
        double tempDifferential = fireTemp - ambientTemp;

        // Negative differential (fire cooler than ambient) produces no heat
        if (tempDifferential <= 0) return 0;

        // Heat output scales with temperature differential and fire size
        // Formula calibrated: 800°F fire with 3kg fuel ~15°F output (current system)
        double fireSizeMultiplier = Math.Sqrt(Math.Max(FuelMassKg, 0.5));
        double heatOutput = (tempDifferential / 90.0) * fireSizeMultiplier;

        return Math.Max(0, heatOutput);
    }

    #endregion

    #region Fuel Management

    /// <summary>
    /// Check if fuel of a given type can be added to the fire based on current temperature
    /// </summary>
    public bool CanAddFuel(FuelType fuelType)
    {
        // Check if fire is at capacity
        if (FuelMassKg >= MaxFuelCapacityKg) return false;

        // Check minimum temperature requirement
        var props = FuelDatabase.Get(fuelType);
        double currentTemp = GetCurrentFireTemperature();

        return currentTemp >= props.MinFireTemperature;
    }

    /// <summary>
    /// Add fuel to the fire by mass and type.
    /// </summary>
    public bool AddFuel(double massKg, FuelType fuelType)
    {
        // Validate temperature requirement
        if (!CanAddFuel(fuelType)) return false;

        // Add to fuel mass (capped at max capacity)
        double spaceAvailable = MaxFuelCapacityKg - FuelMassKg;
        double actualMassAdded = Math.Min(massKg, spaceAvailable);

        FuelMassKg += actualMassAdded;

        // Track fuel type in mixture
        if (_fuelMixture.ContainsKey(fuelType))
            _fuelMixture[fuelType] += actualMassAdded;
        else
            _fuelMixture[fuelType] = actualMassAdded;

        // Auto-relight from embers (embers still have heat to ignite fuel)
        if (HasEmbers && FuelMassKg > 0)
        {
            HasEmbers = false;
            EmberTimeRemaining = 0;
            FireAgeMinutes = 0; // Reset fire age when relighting
        }

        // If adding to cold fire and fuel type can ignite from cold (tinder/kindling)
        var props = FuelDatabase.Get(fuelType);
        if (!IsActive && !HasEmbers && props.MinFireTemperature == 0)
        {
            // This fuel can start a fire from cold (used with Start Fire action)
            FireAgeMinutes = 0;
        }

        return true;
    }

    /// <summary>
    /// Get weighted average burn rate from current fuel mixture (kg/hour)
    /// </summary>
    private double GetWeightedBurnRate()
    {
        if (_fuelMixture.Count == 0 || FuelMassKg <= 0) return 1.0; // Default burn rate

        double totalWeightedRate = 0;
        double totalMass = 0;

        foreach (var (fuelType, mass) in _fuelMixture)
        {
            var props = FuelDatabase.Get(fuelType);
            totalWeightedRate += props.BurnRateKgPerHour * mass;
            totalMass += mass;
        }

        return totalMass > 0 ? totalWeightedRate / totalMass : 1.0;
    }

    #endregion

    #region Fire State Management

    /// <summary>
    /// Update fire state based on time elapsed
    /// </summary>
    public override void Update(int minutes)
    {
        double minutesElapsed = minutes;
        FireAgeMinutes += minutesElapsed;

        if (FuelMassKg > 0)
        {
            // Calculate fuel consumption
            double burnRateKgPerHour = GetWeightedBurnRate();
            double fuelConsumed = burnRateKgPerHour * (minutesElapsed / 60.0);

            // Consume fuel proportionally from each fuel type in mixture
            if (FuelMassKg > 0)
            {
                double consumptionRatio = fuelConsumed / FuelMassKg;
                var fuelTypes = _fuelMixture.Keys.ToList();

                foreach (var fuelType in fuelTypes)
                {
                    _fuelMixture[fuelType] *= (1.0 - consumptionRatio);

                    // Remove fuel types that are depleted
                    if (_fuelMixture[fuelType] < 0.001)
                        _fuelMixture.Remove(fuelType);
                }
            }

            FuelMassKg = Math.Max(0, FuelMassKg - fuelConsumed);

            // Transition to embers when fuel depleted
            if (FuelMassKg <= 0)
            {
                HasEmbers = true;

                // Embers last 25% of total burn time
                _emberDuration = FireAgeMinutes / 60 * 0.25;
                EmberTimeRemaining = _emberDuration;

                FuelMassKg = 0;
                _fuelMixture.Clear();
            }
        }
        else if (HasEmbers)
        {
            // Ember decay
            EmberTimeRemaining = Math.Max(0, EmberTimeRemaining - (minutesElapsed / 60.0));

            if (EmberTimeRemaining <= 0)
            {
                HasEmbers = false;
                _emberDuration = 0;
            }
        }
    }

    /// <summary>
    /// Get current fire phase for UI display
    /// </summary>
    public string GetFirePhase()
    {
        if (!IsActive && !HasEmbers) return "Cold";

        double temp = GetCurrentFireTemperature();
        double fuelPercent = FuelMassKg / MaxFuelCapacityKg;
        double peakTemp = GetWeightedPeakTemperature();

        if (HasEmbers) return "Embers";
        if (FireAgeMinutes < 5) return "Igniting";
        if (temp < peakTemp * 0.9) return "Building";
        if (fuelPercent > 0.5) return "Roaring";
        if (fuelPercent > 0.3) return "Steady";
        return "Dying";
    }

    #endregion
}
