namespace text_survival;

public class SurvivalStatsUpdate
{
    public double Temperature;
    public double Calories;
    public double Hydration;
    public double Exhaustion;

    /// <summary>
    /// Returns a new SurvivalStatsUpdate with the update added
    /// </summary>
    /// <param name="update"></param>
    /// <returns></returns>
    public SurvivalStatsUpdate Add(SurvivalStatsUpdate update)
    {
        return new SurvivalStatsUpdate
        {
            Calories = this.Calories + update.Calories,
            Temperature = this.Temperature + update.Temperature,
            Hydration = this.Hydration + update.Hydration,
            Exhaustion = this.Exhaustion + update.Exhaustion,
        };
    }

    /// <summary>
    /// returns a new Survival stats update with the multiplier applied
    /// </summary>
    /// <param name="multiplier"></param>
    /// <returns></returns>
    public SurvivalStatsUpdate ApplyMultiplier(double multiplier)
    {
        return new SurvivalStatsUpdate
        {
            Calories = this.Calories * multiplier,
            Temperature = this.Temperature * multiplier,
            Hydration = this.Hydration * multiplier,
            Exhaustion = this.Exhaustion * multiplier
        };
    }
}