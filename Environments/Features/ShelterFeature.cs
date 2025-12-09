using text_survival;
using text_survival.Environments;

namespace text_survival.Environments.Features;

public class ShelterFeature : LocationFeature
{
    // todo add enums and presets like environment features
    public double TemperatureInsulation { get; } = 0; // ambient temp protection 0-1
    public double OverheadCoverage { get; } = 0; // rain / snow / sun protection 0-1
    public double WindCoverage { get; } = 0; // wind protection 0-1
    // public double Durability {get; private}
    public ShelterFeature(Location location, double tempInsulation, double overheadCoverage, double windCoverage) : base("shelter", location)
    {
        TemperatureInsulation = tempInsulation;
        OverheadCoverage = overheadCoverage;
        WindCoverage = windCoverage;
    }
    
}