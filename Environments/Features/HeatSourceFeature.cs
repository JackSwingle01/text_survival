using text_survival.Items;

namespace text_survival.Environments.Features;

public class HeatSourceFeature : LocationFeature
{
    // Fire State
    public bool IsActive => BurningMassKg > 0;
    public bool HasEmbers { get; private set; }

    // Two-mass fuel model: unburned + burning = total
    public double UnburnedMassKg { get; private set; }
    public double BurningMassKg { get; private set; }
    public double TotalMassKg => UnburnedMassKg + BurningMassKg;
    public double MaxFuelCapacityKg { get; private set; }

    private Dictionary<FuelType, double> _unburnedMixture = [];
    private Dictionary<FuelType, double> _burningMixture = [];

    // Time remaining calculations
    public double HoursRemaining => BurningHoursRemaining; // Backwards compatibility

    public double BurningHoursRemaining
    {
        get
        {
            if (BurningMassKg <= 0) return 0;
            double burnRate = EffectiveBurnRateKgPerHour;
            return burnRate > 0 ? BurningMassKg / burnRate : 0;
        }
    }

    public double TotalHoursRemaining
    {
        get
        {
            if (TotalMassKg <= 0) return 0;
            double burnRate = EffectiveBurnRateKgPerHour;
            if (burnRate <= 0) burnRate = 1.0; // Fallback
            return TotalMassKg / burnRate;
        }
    }

    public double EffectiveBurnRateKgPerHour =>
        BurningMassKg > 0 ? GetWeightedBurnRate() * GetFireSizeBurnMultiplier() : 0;

    // Ember tracking
    private double _emberDuration;
    private double _emberStartTemperature;
    private double _lastBurningTemperature; // Captured before consumption for ember transition
    public double EmberTimeRemaining { get; private set; }

    // Catching mechanic constants
    private const double BaseCatchRate = 0.05; // kg/min baseline

    public HeatSourceFeature(double maxCapacityKg = 12.0)
        : base("Campfire")
    {
        HasEmbers = false;
        UnburnedMassKg = 0;
        BurningMassKg = 0;
        MaxFuelCapacityKg = maxCapacityKg;
        EmberTimeRemaining = 0;
        _emberDuration = 0;
        _emberStartTemperature = 0;
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
    /// Calculate active fire temperature based on burning mass.
    /// No startup curve - temperature is real combustion temp.
    /// </summary>
    private double GetActiveFireTemperature()
    {
        if (BurningMassKg <= 0) return 0;

        double peakTemp = GetWeightedPeakTemperature();
        double sizeMultiplier = GetFireSizeMultiplier();

        return peakTemp * sizeMultiplier;
    }

    /// <summary>
    /// Calculate ember phase temperature with exponential decay.
    /// Starts at the temperature the fire was before transitioning to embers.
    /// </summary>
    private double GetEmberTemperature()
    {
        if (_emberDuration <= 0) return Math.Max(200, _emberStartTemperature * 0.3);

        double progress = EmberTimeRemaining / _emberDuration;
        // Square root decay: embers cool slowly at first, then faster
        return _emberStartTemperature * Math.Pow(progress, 0.5);
    }

    /// <summary>
    /// Get weighted average peak temperature from burning fuel mixture
    /// </summary>
    private double GetWeightedPeakTemperature()
    {
        if (_burningMixture.Count == 0 || BurningMassKg <= 0) return 450; // Default to tinder temp

        double totalWeightedTemp = 0;
        double totalMass = 0;

        foreach (var (fuelType, mass) in _burningMixture)
        {
            var props = FuelDatabase.Get(fuelType);
            totalWeightedTemp += props.PeakTemperature * mass;
            totalMass += mass;
        }

        return totalMass > 0 ? totalWeightedTemp / totalMass : 450;
    }

    /// <summary>
    /// Fire size affects temperature (larger fires burn hotter due to better oxygen flow)
    /// Based on burning mass, not total fuel
    /// </summary>
    private double GetFireSizeMultiplier()
    {
        if (BurningMassKg < 0.5) return 0.5;   // Tiny
        if (BurningMassKg < 1.0) return 0.7;   // Small
        if (BurningMassKg < 2.0) return 0.85;  // Medium
        if (BurningMassKg < 4.0) return 1.0;   // Good
        return 1.1;                             // Large
    }

    /// <summary>
    /// Fire size affects burn rate (larger fires consume fuel faster)
    /// </summary>
    private double GetFireSizeBurnMultiplier()
    {
        if (BurningMassKg < 1.5) return 0.9;   // Small - burns slower
        if (BurningMassKg < 4.0) return 1.0;   // Sweet spot
        if (BurningMassKg < 7.0) return 1.1;   // Large - burns faster
        return 1.2;                             // Huge - hungry fire
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

        // Heat output scales with temperature differential and burning fire size
        double fireSizeMultiplier = Math.Sqrt(Math.Max(BurningMassKg, 0.5));
        double heatOutput = (tempDifferential / 60.0) * fireSizeMultiplier;

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
        if (TotalMassKg >= MaxFuelCapacityKg) return false;

        // Check minimum temperature requirement
        var props = FuelDatabase.Get(fuelType);
        double currentTemp = GetCurrentFireTemperature();

        return currentTemp >= props.MinFireTemperature;
    }

    /// <summary>
    /// Add fuel to the fire. Fuel goes to unburned pile and catches over time.
    /// </summary>
    public bool AddFuel(double massKg, FuelType fuelType)
    {
        // Validate temperature requirement
        if (!CanAddFuel(fuelType)) return false;

        // Add to unburned fuel (capped at max capacity)
        double spaceAvailable = MaxFuelCapacityKg - TotalMassKg;
        double actualMassAdded = Math.Min(massKg, spaceAvailable);

        UnburnedMassKg += actualMassAdded;

        // Track fuel type in unburned mixture
        if (_unburnedMixture.ContainsKey(fuelType))
            _unburnedMixture[fuelType] += actualMassAdded;
        else
            _unburnedMixture[fuelType] = actualMassAdded;

        // Auto-relight from embers: immediately ignite added fuel
        if (HasEmbers && GetEmberTemperature() >= FuelDatabase.Get(fuelType).MinFireTemperature)
        {
            // Transfer from unburned to burning (relight)
            TransferToBurning(fuelType, actualMassAdded);
            HasEmbers = false;
            EmberTimeRemaining = 0;
            _emberDuration = 0;
        }

        return true;
    }

    /// <summary>
    /// Ignite fuel - transfers from unburned to burning.
    /// Used when starting a fire or relighting from embers.
    /// </summary>
    public void IgniteFuel(FuelType fuelType, double massKg)
    {
        double available = _unburnedMixture.GetValueOrDefault(fuelType, 0);
        double toIgnite = Math.Min(massKg, available);

        if (toIgnite > 0)
        {
            TransferToBurning(fuelType, toIgnite);
        }
    }

    /// <summary>
    /// Ignite all unburned fuel of types that can ignite from cold (tinder/kindling)
    /// </summary>
    public void IgniteAll()
    {
        foreach (var (fuelType, mass) in _unburnedMixture.ToList())
        {
            var props = FuelDatabase.Get(fuelType);
            if (props.MinFireTemperature == 0 && mass > 0)
            {
                TransferToBurning(fuelType, mass);
            }
        }
    }

    private void TransferToBurning(FuelType fuelType, double massKg)
    {
        // Remove from unburned
        if (_unburnedMixture.ContainsKey(fuelType))
        {
            _unburnedMixture[fuelType] -= massKg;
            if (_unburnedMixture[fuelType] <= 0.001)
                _unburnedMixture.Remove(fuelType);
        }
        UnburnedMassKg = Math.Max(0, UnburnedMassKg - massKg);

        // Add to burning
        if (_burningMixture.ContainsKey(fuelType))
            _burningMixture[fuelType] += massKg;
        else
            _burningMixture[fuelType] = massKg;
        BurningMassKg += massKg;
    }

    /// <summary>
    /// Get weighted average burn rate from burning fuel mixture (kg/hour)
    /// </summary>
    private double GetWeightedBurnRate()
    {
        if (_burningMixture.Count == 0 || BurningMassKg <= 0) return 1.0;

        double totalWeightedRate = 0;
        double totalMass = 0;

        foreach (var (fuelType, mass) in _burningMixture)
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

        if (BurningMassKg > 0)
        {
            // 1. Process catching: unburned fuel catches fire
            ProcessCatching(minutesElapsed);

            // 2. Consume burning fuel
            ProcessConsumption(minutesElapsed);

            // 3. Check for transition to embers
            if (BurningMassKg <= 0)
            {
                TransitionToEmbers();
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
                _emberStartTemperature = 0;
            }
        }
    }

    /// <summary>
    /// Process the catching mechanic: unburned fuel gradually catches fire
    /// </summary>
    private void ProcessCatching(double minutesElapsed)
    {
        if (UnburnedMassKg <= 0 || BurningMassKg <= 0) return;

        double currentTemp = GetCurrentFireTemperature();
        double tempMultiplier = Math.Max(0.5, currentTemp / 400.0);
        double massMultiplier = 1.0 + (BurningMassKg * 0.15); // Exponential feedback

        foreach (var (fuelType, unburnedMass) in _unburnedMixture.ToList())
        {
            if (unburnedMass <= 0) continue;

            var props = FuelDatabase.Get(fuelType);

            // Check if fire is hot enough to ignite this fuel type
            if (currentTemp < props.MinFireTemperature) continue;

            // Use burn rate as fuel-type multiplier (fast-burning = fast-catching)
            double fuelMultiplier = props.BurnRateKgPerHour;
            double catchRate = BaseCatchRate * tempMultiplier * massMultiplier * fuelMultiplier;
            double catching = Math.Min(unburnedMass, catchRate * minutesElapsed);

            if (catching > 0)
            {
                TransferToBurning(fuelType, catching);
            }
        }
    }

    /// <summary>
    /// Process fuel consumption: burning fuel is consumed
    /// </summary>
    private void ProcessConsumption(double minutesElapsed)
    {
        if (BurningMassKg <= 0) return;

        // Capture temperature BEFORE consumption for ember transition
        _lastBurningTemperature = GetActiveFireTemperature();

        double burnRate = GetWeightedBurnRate() * GetFireSizeBurnMultiplier();
        double consumed = burnRate * (minutesElapsed / 60.0);

        // Cap consumption at available burning mass
        consumed = Math.Min(consumed, BurningMassKg);

        // Proportionally remove from burning mixture
        double ratio = consumed / BurningMassKg;
        foreach (var (fuelType, mass) in _burningMixture.ToList())
        {
            _burningMixture[fuelType] *= (1.0 - ratio);

            // Remove depleted fuel types
            if (_burningMixture[fuelType] < 0.001)
                _burningMixture.Remove(fuelType);
        }

        BurningMassKg = Math.Max(0, BurningMassKg - consumed);
    }

    /// <summary>
    /// Transition from active fire to embers
    /// </summary>
    private void TransitionToEmbers()
    {
        // Use temperature captured before consumption (mixture is now empty)
        _emberStartTemperature = _lastBurningTemperature;

        HasEmbers = true;

        // Embers last based on how much fuel burned (rough estimate)
        // More fuel burned = more embers = longer duration
        _emberDuration = Math.Max(0.25, _emberStartTemperature / 600.0 * 0.5); // 0.25 to 0.5 hours
        EmberTimeRemaining = _emberDuration;

        BurningMassKg = 0;
        _burningMixture.Clear();
    }

    /// <summary>
    /// Get current fire phase for UI display
    /// </summary>
    public string GetFirePhase()
    {
        if (!IsActive && !HasEmbers) return "Cold";

        if (HasEmbers) return "Embers";

        // Phases based on burning mass and catching state
        if (BurningMassKg < 0.5) return "Igniting";
        if (UnburnedMassKg > BurningMassKg * 0.5) return "Building";
        if (BurningMassKg > 4.0) return "Roaring";
        if (BurningMassKg > 1.5) return "Steady";
        return "Dying";
    }

    #endregion
}
