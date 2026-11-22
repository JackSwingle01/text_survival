namespace Survival;

/// Once-per-game teaching lines (GLOSSARY: Tutorial): each shown the first time it applies, then never again.
public static class Tutorials
{
    public static void Show(World world, Agent agent, Tutorial tutorial, List<WorldEvent> events, double? kg = null)
    {
        world.Tutorials ??= [];
        if (!world.Tutorials.Add(tutorial)) return;
        events.Add(new TutorialShown(world.Minute, agent.Id, agent.Tile, tutorial, kg));
    }

    public static bool Shown(World world, Tutorial tutorial) => world.Tutorials?.Contains(tutorial) == true;

    public static string Text(Tutorial tutorial, double? kg = null) =>
        Clues.Table.Tutorials[tutorial].Replace("{kg}", (kg ?? 0).ToString("0", System.Globalization.CultureInfo.InvariantCulture));
}
