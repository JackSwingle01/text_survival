namespace text_survival.Environments;

using text_survival.Environments.Factories;

public static class ZoneFactory
{
    public static (List<Location> revealed, List<Location> unrevealed, List<Location> passLocations) MakeForestZone(Weather weather)
    {
        var generator = new ZoneGenerator
        {
            TargetLocationCount = 100,
            InitialRevealedCount = 1
        };
        var (revealed, unrevealed) = generator.GenerateForestZone(weather);

        // Generate mountain pass connected to camp (start location)
        var camp = revealed[0];
        var passLocations = ZoneGenerator.GenerateMountainPass(weather, camp);

        return (revealed, unrevealed, passLocations);
    }
}
