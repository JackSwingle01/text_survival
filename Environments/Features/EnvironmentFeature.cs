namespace text_survival.Environments.Features;

public class EnvironmentFeature : LocationFeature
{
    public enum LocationType
    {
        OpenPlain,      // No natural shelter
        Forest,         // Trees provide some wind/rain protection
        Cave,           // Natural temperature moderation
        Cliff,          // Wind protection from one direction
        RiverBank,      // Water nearby, but exposure
        HighGround      // More wind but better visibility
    }
    public LocationType Type { get; private set; }
    public double TemperatureModifier { get; } = 0; // degrees F adjustment
    public double NaturalOverheadCoverage { get; } = 0;
    public double NaturalWindProtection { get; } = 0;

    public EnvironmentFeature(double tempModifier, double overheadCoverage, double windProtection) : base("shelter")
    {
        TemperatureModifier = tempModifier;
        NaturalOverheadCoverage = overheadCoverage;
        NaturalWindProtection = windProtection;
    }
    public EnvironmentFeature(LocationType type)
        : base("locationType")
    {
        Type = type;

        switch (type)
        {
            case LocationType.Forest:
                NaturalWindProtection = 0.4;     // 40% wind reduction
                NaturalOverheadCoverage = 0.3;   // 30% precipitation protection
                TemperatureModifier = 3.0;       // 3°F warmer in winter, cooler in summer
                break;

            case LocationType.Cave:
                NaturalWindProtection = 0.9;     // 90% wind protection
                NaturalOverheadCoverage = 0.95;  // 95% precipitation protection
                TemperatureModifier = 15.0;      // 15°F warmer in winter, cooler in summer
                break;

            case LocationType.Cliff:
                NaturalWindProtection = 0.6;     // 60% wind protection
                NaturalOverheadCoverage = 0.2;   // 20% precipitation protection
                TemperatureModifier = 2.0;       // 2°F temperature moderation
                break;

            case LocationType.RiverBank:
                NaturalWindProtection = 0.1;     // 10% wind protection
                NaturalOverheadCoverage = 0.0;   // No precipitation protection
                TemperatureModifier = -2.0;      // 2°F cooler from water proximity
                break;

            case LocationType.HighGround:
                NaturalWindProtection = -0.2;    // 20% increased wind
                NaturalOverheadCoverage = 0.0;   // No precipitation protection
                TemperatureModifier = -4.0;      // 4°F cooler from elevation
                break;

            case LocationType.OpenPlain:
            default:
                NaturalWindProtection = 0.0;
                NaturalOverheadCoverage = 0.0;
                TemperatureModifier = 0.0;
                break;
        }
    }

    // Get description of the location type
    public string GetDescription()
    {
        return Type switch
        {
            LocationType.Forest => "A forest with trees providing some shelter from the elements.",
            LocationType.Cave => "A cave offering protection from wind and precipitation.",
            LocationType.Cliff => "A cliff face providing some protection from the wind.",
            LocationType.RiverBank => "The bank of a river, exposed but with access to water.",
            LocationType.HighGround => "Higher elevation with increased exposure to wind.",
            LocationType.OpenPlain => "An open area with no natural protection.",
            _ => "An undefined location type."
        };
    }

}