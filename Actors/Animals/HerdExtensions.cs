using text_survival.Environments;
using text_survival.Environments.Grid;

namespace text_survival.Actors.Animals;

/// <summary>
/// Extension methods on List&lt;Herd&gt; replacing HerdRegistry queries.
/// </summary>
public static class HerdExtensions
{

    public static IReadOnlyList<Herd> At(this List<Herd> herds, Location location)
    {
        return herds.Where(h => h.CurrentLocation == location).ToList();
    }

    public static IReadOnlyList<Herd> At(this List<Herd> herds, GridPosition pos)
    {
        return herds.Where(h => h.Position == pos).ToList();
    }

    public static IReadOnlyList<Herd> InRange(this List<Herd> herds, GridPosition center, int range)
    {
        return herds.Where(h => h.Position.ManhattanDistance(center) <= range).ToList();
    }

    public static IReadOnlyList<Herd> Predators(this List<Herd> herds)
    {
        return herds.Where(h => h.IsPredator).ToList();
    }

    public static IReadOnlyList<Herd> Prey(this List<Herd> herds)
    {
        return herds.Where(h => !h.IsPredator).ToList();
    }

    public static IReadOnlyList<Herd> OfAnimalType(this List<Herd> herds, AnimalType animalType)
    {
        return herds.Where(h => h.AnimalType == animalType).ToList();
    }

    public static IEnumerable<Animal> AllAnimals(this List<Herd> herds)
    {
        foreach (var herd in herds)
            foreach (var animal in herd.Members)
                yield return animal;
    }

    public static Herd? ContainingAnimal(this List<Herd> herds, Animal animal)
    {
        return herds.FirstOrDefault(h => h.Members.Contains(animal));
    }

    public static int TotalAnimalCount(this List<Herd> herds)
    {
        return herds.Sum(h => h.Count);
    }

    /// <summary>
    /// Searches for large game at a position. Used by HuntStrategy.
    /// </summary>
    public static (Herd herd, Animal animal)? SearchForLargeGame(this List<Herd> herds, GridPosition pos, int searchMinutes)
    {
        var nearby = herds.At(pos);
        var huntableHerds = nearby.Where(h => h.Count > 0).ToList();

        if (huntableHerds.Count == 0)
            return null;

        double baseChance = searchMinutes / 30.0;
        double effectiveChance = Math.Min(0.9, baseChance * (1 + huntableHerds.Count * 0.2));

        if (Utils.Rng.NextDouble() > effectiveChance)
            return null;

        var weights = huntableHerds.Select(h =>
        {
            var member = h.GetRandomMember();
            double weight = h.Count * (member?.Condition ?? 0.5);
            if (h.State == HerdState.Alert || h.State == HerdState.Fleeing)
                weight *= 0.3;
            return weight;
        }).ToList();

        double totalWeight = weights.Sum();
        double roll = Utils.Rng.NextDouble() * totalWeight;
        double cumulative = 0;

        for (int i = 0; i < huntableHerds.Count; i++)
        {
            cumulative += weights[i];
            if (roll < cumulative)
            {
                var herd = huntableHerds[i];
                var animal = herd.GetRandomMember();
                if (animal != null)
                    return (herd, animal);
            }
        }

        var fallbackHerd = huntableHerds[0];
        var fallbackAnimal = fallbackHerd.GetRandomMember();
        return fallbackAnimal != null ? (fallbackHerd, fallbackAnimal) : null;
    }

    /// <summary>
    /// Gets a description of recent animal activity at a position.
    /// </summary>
    public static string? GetActivityDescription(this List<Herd> herds, GridPosition pos)
    {
        var herdsHere = herds.At(pos);

        if (herdsHere.Count == 0)
        {
            var nearbyHerds = herds.InRange(pos, 1).Where(h => h.Position != pos).ToList();
            if (nearbyHerds.Count > 0)
            {
                var closest = nearbyHerds[0];
                return $"Fresh {closest.AnimalType.DisplayName().ToLower()} tracks lead away from here.";
            }
            return null;
        }

        var mostNotable = herdsHere.OrderByDescending(h => h.IsPredator ? 100 : h.Count).First();
        return mostNotable.GetDescription() + ".";
    }

    /// <summary>
    /// Splits a wounded animal off from its herd into a new single-member herd.
    /// Adds the new herd to the list and removes the original if empty.
    /// </summary>
    public static Herd SplitWounded(this List<Herd> herds, Herd herd, Animal animal, GridPosition fleeDirection)
    {
        var newHerd = herd.SplitOffWounded(animal, fleeDirection);
        herds.Add(newHerd);

        if (herd.IsEmpty)
        {
            herds.Remove(herd);
        }

        return newHerd;
    }

    /// <summary>
    /// Recreates behavior strategies for all herds after deserialization.
    /// </summary>
    public static void RecreateAllBehaviors(this List<Herd> herds)
    {
        foreach (var herd in herds)
        {
            herd.RecreateBehavior();
        }
    }
}
