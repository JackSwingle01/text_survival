using text_survival.Environments;

public class HeatSourceFeature : LocationFeature
{
    public bool IsActive { get; private set; }
    public bool HasEmbers { get; private set; }
    public double HeatOutput { get; private set; } // In Fahrenheit
    public double FuelRemaining { get; private set; }
    public double EmberTimeRemaining { get; private set; } // In hours
    public double FuelConsumptionRate { get; private set; } // Per hour
    private double _lastFuelAmount; // Track fuel for ember calculation

    public HeatSourceFeature(Location location, double heatOutput = 15.0)
        : base("Campfire", location)
    {
        IsActive = false;
        HasEmbers = false;
        HeatOutput = heatOutput; // Default 15°F increase
        FuelRemaining = 0;
        EmberTimeRemaining = 0;
        FuelConsumptionRate = 1; // default
        _lastFuelAmount = 0;
    }
    
    /// <summary>
    /// Adds fuel to the fire (max 8 hours).
    /// Auto-relights from embers. Otherwise requires fire-making materials via SetActive() or Start Fire action.
    /// </summary>
    /// <param name="hours"></param>
    public void AddFuel(double hours)
    {
        FuelRemaining = Math.Min(8.0, FuelRemaining + hours);
        _lastFuelAmount = FuelRemaining;

        // Auto-relight from embers
        if (HasEmbers && FuelRemaining > 0)
        {
            IsActive = true;
            HasEmbers = false;
            EmberTimeRemaining = 0;
        }
    }
    
    // Update for fuel consumption and ember transitions
    public void Update(TimeSpan elapsed)
    {
        double hoursElapsed = elapsed.TotalHours;
        double consumptionAmount = FuelConsumptionRate * hoursElapsed;

        // Active fire: burn fuel
        if (IsActive && FuelRemaining > 0)
        {
            // Track fuel amount for ember calculation
            _lastFuelAmount = FuelRemaining;

            FuelRemaining = Math.Max(0, FuelRemaining - consumptionAmount);

            // Transition to embers when fuel runs out
            if (FuelRemaining <= 0)
            {
                IsActive = false;
                HasEmbers = true;
                // Embers last 25% of the time the fire burned
                EmberTimeRemaining = _lastFuelAmount * 0.25;
            }
        }
        // Ember state: decay embers
        else if (HasEmbers && EmberTimeRemaining > 0)
        {
            EmberTimeRemaining = Math.Max(0, EmberTimeRemaining - consumptionAmount);

            // Fully extinguish when embers run out
            if (EmberTimeRemaining <= 0)
            {
                HasEmbers = false;
            }
        }
    }
    
    // Manually activate/deactivate
    public void SetActive(bool active)
    {
        // Can only activate if there's fuel
        if (active && FuelRemaining > 0)
        {
            IsActive = true;
            HasEmbers = false;
            EmberTimeRemaining = 0;
            _lastFuelAmount = FuelRemaining; // Track for ember calculation
        }
        else if (!active)
        {
            IsActive = false;
        }
    }

    /// <summary>
    /// Gets the effective heat output based on fire state.
    /// Active fire: full heat output
    /// Embers: 35% heat output
    /// Cold: no heat
    /// </summary>
    public double GetEffectiveHeatOutput()
    {
        if (IsActive)
            return HeatOutput;
        else if (HasEmbers)
            return HeatOutput * 0.35;
        else
            return 0;
    }
}