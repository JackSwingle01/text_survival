using text_survival.Actors.Animals;
using text_survival.Environments.Grid;

namespace text_survival.Combat;

public enum EncounterOpening { Approach, CloseEncounter, Ambush }
public enum EncounterFormation { Cluster, BroadFront, Pincer, Surround }

/// <summary>Bounded, rotated formations for animal-initiated encounters.</summary>
public static class EncounterPlacement
{
    public static EncounterFormation SelectFormation(AnimalType species, int count, Random random)
    {
        int roll = random.Next(100);
        if (count < 2) return EncounterFormation.Cluster;
        if (species == AnimalType.Hyena)
            return roll < 70 ? EncounterFormation.Cluster : EncounterFormation.BroadFront;
        if (species != AnimalType.Wolf) return EncounterFormation.Cluster;
        if (count == 2)
            return roll < 60 ? EncounterFormation.Cluster : roll < 90 ? EncounterFormation.BroadFront : EncounterFormation.Pincer;
        return roll < 35 ? EncounterFormation.Cluster : roll < 70 ? EncounterFormation.BroadFront :
            roll < 90 ? EncounterFormation.Pincer : EncounterFormation.Surround;
    }

    public static void Apply(CombatScenario scenario, EncounterOpening opening, int distance,
        Random? random = null, EncounterFormation? formation = null)
    {
        random ??= Utils.Rng;
        var center = new GridPosition(CombatScenario.MAP_SIZE / 2 + random.Next(-2, 3),
            CombatScenario.MAP_SIZE / 2 + random.Next(-2, 3));
        var occupied = new HashSet<GridPosition>();
        // Player is the anchor even if callers reorder their allies.
        var anchor = scenario.Player ?? scenario.Team1[0];
        anchor.Position = center;
        occupied.Add(center);
        foreach (var ally in scenario.Team1.Where(u => u != anchor))
            ally.Position = TakeFree(center, occupied, random, 4);

        var species = (scenario.Team2[0].actor as Animal)?.AnimalType;
        var selected = opening == EncounterOpening.Approach && species != null
            ? formation ?? SelectFormation(species.Value, scenario.Team2.Count, random)
            : EncounterFormation.Cluster;
        scenario.Formation = selected;
        double bearing = random.NextDouble() * Math.Tau;
        for (int i = 0; i < scenario.Team2.Count; i++)
        {
            double angle = bearing + (selected switch
            {
                EncounterFormation.BroadFront => Spread(i, scenario.Team2.Count, Math.PI / 2),
                EncounterFormation.Pincer => (i % 2) * Math.PI * 0.8 + (i / 2) * 0.12,
                EncounterFormation.Surround => i * Math.Tau / scenario.Team2.Count + (random.NextDouble() - 0.5) * 0.25,
                _ => Spread(i, scenario.Team2.Count, 0.2)
            });
            double dx = Math.Cos(angle), dy = Math.Sin(angle);
            int inset = random.Next(2, 6);
            double edgeRadius = Math.Min(
                Math.Abs(dx) < 0.0001 ? double.PositiveInfinity : (dx > 0 ? CombatScenario.MAP_SIZE - 1 - inset - center.X : center.X - inset) / Math.Abs(dx),
                Math.Abs(dy) < 0.0001 ? double.PositiveInfinity : (dy > 0 ? CombatScenario.MAP_SIZE - 1 - inset - center.Y : center.Y - inset) / Math.Abs(dy));
            double radius = opening == EncounterOpening.Approach ? edgeRadius : Math.Min(Math.Max(2, distance), edgeRadius);
            var desired = new GridPosition((int)Math.Round(center.X + dx * radius), (int)Math.Round(center.Y + dy * radius));
            scenario.Team2[i].Position = TakeFree(desired, occupied, random);
        }
    }

    private static double Spread(int index, int count, double width) => count <= 1 ? 0 : width * (index / (double)(count - 1) - 0.5);

    private static GridPosition TakeFree(GridPosition desired, HashSet<GridPosition> occupied, Random random, int clusterRadius = 0)
    {
        var candidates = Enumerable.Range(1, CombatScenario.MAP_SIZE - 2)
            .SelectMany(x => Enumerable.Range(1, CombatScenario.MAP_SIZE - 2).Select(y => new GridPosition(x, y)))
            .Where(p => !occupied.Contains(p));
        var nearby = candidates.Where(p => p.DistanceTo(desired) <= clusterRadius).ToList();
        var chosen = nearby.Count > 0 ? nearby[random.Next(nearby.Count)] : candidates.OrderBy(p => p.DistanceTo(desired)).First();
        occupied.Add(chosen);
        return chosen;
    }

    public static string Describe(EncounterFormation formation) => formation switch
    {
        EncounterFormation.BroadFront => "The animals spread across a broad front.",
        EncounterFormation.Pincer => "The pack approaches from two sides.",
        EncounterFormation.Surround => "The pack closes in from all around you.",
        _ => "The animals approach together from one direction."
    };
}
