using text_survival.Environments;

public class HeatSourceFeature : LocationFeature
{
    public bool IsActive { get; private set; }
    public double HeatOutput { get; private set; } // In Fahrenheit
    public double FuelRemaining { get; private set; } 
    public double FuelConsumptionRate { get; private set; } // Per hour
    
    public HeatSourceFeature(Location location, double heatOutput = 15.0) 
        : base("heatSource", location)
    {
        IsActive = false;
        HeatOutput = heatOutput; // Default 15°F increase
        FuelRemaining = 0;
        FuelConsumptionRate = 1; // default
    }
    
    /// <summary>
    /// Note, max 10 hours of burn time
    /// </summary>
    /// <param name="hours"></param>
    public void AddFuel(double hours)
    {
        FuelRemaining = Math.Min(1.0, FuelRemaining + hours);

        // Activate if adding fuel to inactive source
        if (!IsActive && FuelRemaining > 0)
            IsActive = true;
    }
    
    // Update for fuel consumption
    public void Update(TimeSpan elapsed)
    {
        if (!IsActive || FuelRemaining <= 0)
            return;
            
        // Calculate fuel consumption
        double hoursElapsed = elapsed.TotalHours;
        double fuelUsed = FuelConsumptionRate * hoursElapsed;
        
        FuelRemaining = Math.Max(0, FuelRemaining - fuelUsed);
        
        // Deactivate if out of fuel
        if (FuelRemaining <= 0)
            IsActive = false;
    }
    
    // Manually activate/deactivate
    public void SetActive(bool active)
    {
        // Can only activate if there's fuel
        if (active && FuelRemaining > 0)
            IsActive = true;
        else if (!active)
            IsActive = false;
    }
}