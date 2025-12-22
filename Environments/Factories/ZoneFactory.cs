namespace text_survival.Environments;

using text_survival.Environments.Factories;

public static class ZoneFactory
{
    public static Zone MakeForestZone()
    {
        var generator = new ZoneGenerator
        {
            TargetLocationCount = 100,
            InitialRevealedCount = 1
        };
        return generator.GenerateForestZone("Pine Forest", "A vast expanse of evergreens.", baseTemp: -10);
    }
}
