namespace text_survival;

public class SurvivalStatsDelta
{
    public double TemperatureDelta;
    public double CalorieDelta;
    public double HydrationDelta;
    public double EnergyDelta;

    /// <summary>
    /// Returns a new SurvivalStatsUpdate with the update added
    /// </summary>
    /// <param name="update"></param>
    /// <returns></returns>
    public SurvivalStatsDelta Add(SurvivalStatsDelta update)
    {
        return new SurvivalStatsDelta
        {
            CalorieDelta = this.CalorieDelta + update.CalorieDelta,
            TemperatureDelta = this.TemperatureDelta + update.TemperatureDelta,
            HydrationDelta = this.HydrationDelta + update.HydrationDelta,
            EnergyDelta = this.EnergyDelta + update.EnergyDelta,
        };
    }

    /// <summary>
    /// returns a new Survival stats update with the multiplier applied
    /// </summary>
    /// <param name="multiplier"></param>
    /// <returns></returns>
    public SurvivalStatsDelta ApplyMultiplier(double multiplier)
    {
        return new SurvivalStatsDelta
        {
            CalorieDelta = this.CalorieDelta * multiplier,
            TemperatureDelta = this.TemperatureDelta * multiplier,
            HydrationDelta = this.HydrationDelta * multiplier,
            EnergyDelta = this.EnergyDelta * multiplier
        };
    }

    public void Combine(SurvivalStatsDelta other)
    {
        CalorieDelta += other.CalorieDelta;
        TemperatureDelta += other.TemperatureDelta;
        HydrationDelta += other.HydrationDelta;
        EnergyDelta += other.EnergyDelta;
    }
}