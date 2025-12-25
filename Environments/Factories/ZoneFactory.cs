namespace text_survival.Environments;

using text_survival.Environments.Factories;

public static class ZoneFactory
{
    public static (List<Location> revealed, List<Location> unrevealed) MakeForestZone(Weather weather)
    {
        var generator = new ZoneGenerator
        {
            TargetLocationCount = 100,
            InitialRevealedCount = 1
        };
        return generator.GenerateForestZone(weather);
    }
}
