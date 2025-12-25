namespace text_survival.Environments.Features;

public class ShelterFeature : LocationFeature
{
    public double TemperatureInsulation { get; set; } = 0; // ambient temp protection 0-1
    public double OverheadCoverage { get; set; } = 0; // rain / snow / sun protection 0-1
    public double WindCoverage { get; set; } = 0; // wind protection 0-1

    public ShelterFeature(string name, double tempInsulation, double overheadCoverage, double windCoverage) : base(name)
    {
        TemperatureInsulation = tempInsulation;
        OverheadCoverage = overheadCoverage;
        WindCoverage = windCoverage;
    }

    [System.Text.Json.Serialization.JsonConstructor]
    public ShelterFeature() : base() { }

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
}