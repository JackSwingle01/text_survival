using System.Text.Json.Serialization;
using text_survival.Actions;
using text_survival.Actors.Animals.Behaviors;
using text_survival.Environments.Features;
using text_survival.Environments.Grid;

namespace text_survival.Actors.Animals;

/// <summary>
/// Central registry for all tracked herds in the game.
/// Lives on GameContext like TensionRegistry.
/// </summary>
public class HerdRegistry
{
    private static readonly Random _rng = new();

    /// <summary>All tracked herds in the game world.</summary>
    [JsonInclude]
    public List<Herd> _herds = [];

    #region Queries

    /// <summary>
    /// Gets all herds at a specific position.
    /// </summary>
    public IReadOnlyList<Herd> GetHerdsAt(GridPosition pos)
    {
        return _herds.Where(h => h.Position == pos).ToList();
    }

    /// <summary>
    /// Gets all herds within a Manhattan distance of a position.
    /// </summary>
    public IReadOnlyList<Herd> GetHerdsInRange(GridPosition center, int range)
    {
        return _herds.Where(h => h.Position.ManhattanDistance(center) <= range).ToList();
    }

    /// <summary>
    /// Gets a herd by its ID.
    /// </summary>
    public Herd? GetHerdById(Guid id)
    {
        return _herds.FirstOrDefault(h => h.Id == id);
    }

    /// <summary>
    /// Gets all predator herds.
    /// </summary>
    public IReadOnlyList<Herd> GetPredatorHerds()
    {
        return _herds.Where(h => h.IsPredator).ToList();
    }

    /// <summary>
    /// Gets all prey herds.
    /// </summary>
    public IReadOnlyList<Herd> GetPreyHerds()
    {
        return _herds.Where(h => !h.IsPredator).ToList();
    }

    /// <summary>
    /// Gets all herds of a specific animal type.
    /// </summary>
    public IReadOnlyList<Herd> GetHerdsByType(AnimalType animalType)
    {
        return _herds.Where(h => h.AnimalType == animalType).ToList();
    }

    /// <summary>
    /// Gets all herds of a specific animal type (string overload for legacy code).
    /// </summary>
    public IReadOnlyList<Herd> GetHerdsByType(string animalTypeName)
    {
        var animalType = AnimalTypes.Parse(animalTypeName);
        return animalType.HasValue ? GetHerdsByType(animalType.Value) : [];
    }

    /// <summary>
    /// Gets all herds with a specific behavior type.
    /// </summary>
    public IReadOnlyList<Herd> GetHerdsByBehavior(HerdBehaviorType behaviorType)
    {
        return _herds.Where(h => h.BehaviorType == behaviorType).ToList();
    }

    /// <summary>
    /// Gets count of all tracked animals across all herds.
    /// </summary>
    public int TotalAnimalCount => _herds.Sum(h => h.Count);

    /// <summary>
    /// Gets count of all herds.
    /// </summary>
    public int HerdCount => _herds.Count;

    #endregion

    #region Mutations

    /// <summary>
    /// Adds a herd to the registry.
    /// </summary>
    public void AddHerd(Herd herd)
    {
        _herds.Add(herd);
    }

    /// <summary>
    /// Removes a herd from the registry.
    /// </summary>
    public void RemoveHerd(Herd herd)
    {
        _herds.Remove(herd);
    }

    /// <summary>
    /// Removes a herd by ID.
    /// </summary>
    public void RemoveHerd(Guid id)
    {
        var herd = GetHerdById(id);
        if (herd != null)
        {
            _herds.Remove(herd);
        }
    }

    /// <summary>
    /// Splits a wounded animal off from its herd into a new herd of size 1.
    /// Returns the new herd.
    /// </summary>
    public Herd SplitWounded(Herd herd, Animal animal, GridPosition fleeDirection)
    {
        var newHerd = herd.SplitOffWounded(animal, fleeDirection);
        _herds.Add(newHerd);

        // Remove original herd if now empty
        if (herd.IsEmpty)
        {
            _herds.Remove(herd);
        }

        return newHerd;
    }

    /// <summary>
    /// Removes dead/empty herds from the registry.
    /// </summary>
    public void CleanupEmptyHerds()
    {
        _herds.RemoveAll(h => h.IsEmpty);
    }

    #endregion

    #region Post-Load

    /// <summary>
    /// Recreates animal members and behaviors for all herds after deserialization.
    /// Called from GameContext.RestoreAfterDeserialization().
    /// </summary>
    public void RecreateAllMembers()
    {
        foreach (var herd in _herds)
        {
            herd.RecreateMembers();
            herd.RecreateBehavior();
        }
    }

    #endregion

    #region Update

    /// <summary>
    /// Updates all herds using behavior strategies. Called each game minute from GameContext.Update().
    /// </summary>
    /// <param name="elapsedMinutes">Minutes elapsed.</param>
    /// <param name="ctx">Game context.</param>
    /// <returns>List of update results with encounters, narratives, and carcass creations.</returns>
    public List<HerdUpdateResult> Update(int elapsedMinutes, GameContext ctx)
    {
        var results = new List<HerdUpdateResult>();

        foreach (var herd in _herds.ToList()) // ToList to allow modification during iteration
        {
            if (herd.IsEmpty)
            {
                RemoveHerd(herd);
                continue;
            }

            var result = herd.Update(elapsedMinutes, ctx);

            // Collect meaningful results
            if (result.EncounterRequest != null ||
                result.PreyKill != null ||
                result.NarrativeMessage != null)
            {
                results.Add(result);
            }

            // Handle prey kills - create carcass
            if (result.PreyKill != null)
            {
                var location = ctx.Map?.GetLocationAt(result.PreyKill.Position);
                if (location != null)
                {
                    location.Features.Add(new CarcassFeature(result.PreyKill.Victim));
                }
            }
        }

        // Cleanup any empty herds
        CleanupEmptyHerds();

        return results;
    }

    /// <summary>
    /// Legacy Update method for compatibility.
    /// </summary>
    [System.Obsolete("Use Update(int, GameContext) instead.")]
    public Herd? Update(int elapsedMinutes, GridPosition playerPosition, bool playerCarryingMeat, bool playerBleeding)
    {
        Herd? encounterHerd = null;

        foreach (var herd in _herds.ToList())
        {
#pragma warning disable CS0618 // Type or member is obsolete
            bool initiatedEncounter = herd.Update(elapsedMinutes, playerPosition, playerCarryingMeat, playerBleeding);
#pragma warning restore CS0618

            if (initiatedEncounter && encounterHerd == null)
            {
                encounterHerd = herd;
            }
        }

        CleanupEmptyHerds();
        return encounterHerd;
    }

    #endregion

    #region Hunt Integration

    /// <summary>
    /// Searches for large game at a position. Used by HuntStrategy.
    /// </summary>
    /// <param name="pos">Position to search.</param>
    /// <param name="searchMinutes">Time spent searching.</param>
    /// <returns>Tuple of (herd, animal) if found, or null.</returns>
    public (Herd herd, Animal animal)? SearchForLargeGame(GridPosition pos, int searchMinutes)
    {
        // Get herds at current position and adjacent tiles
        var nearby = GetHerdsInRange(pos, 1);

        // Filter to herds with animals (both prey and predators are huntable)
        var huntableHerds = nearby.Where(h => h.Count > 0).ToList();

        if (huntableHerds.Count == 0)
            return null;

        // Base chance scales with search time
        double baseChance = searchMinutes / 30.0; // 50% at 15 min

        // More herds = higher chance
        double effectiveChance = Math.Min(0.9, baseChance * (1 + huntableHerds.Count * 0.2));

        if (_rng.NextDouble() > effectiveChance)
            return null;

        // Weight selection by herd size and condition
        var weights = huntableHerds.Select(h =>
        {
            var member = h.GetRandomMember();
            // Larger herds easier to find, better condition animals harder
            double weight = h.Count * (member?.Condition ?? 0.5);
            // Harder to find herds that are alert or fleeing
            if (h.State == HerdState.Alert || h.State == HerdState.Fleeing)
                weight *= 0.3;
            return weight;
        }).ToList();

        double totalWeight = weights.Sum();
        double roll = _rng.NextDouble() * totalWeight;
        double cumulative = 0;

        for (int i = 0; i < huntableHerds.Count; i++)
        {
            cumulative += weights[i];
            if (roll < cumulative)
            {
                var herd = huntableHerds[i];
                var animal = herd.GetRandomMember();
                if (animal != null)
                {
                    return (herd, animal);
                }
            }
        }

        // Fallback to first herd
        var fallbackHerd = huntableHerds[0];
        var fallbackAnimal = fallbackHerd.GetRandomMember();
        return fallbackAnimal != null ? (fallbackHerd, fallbackAnimal) : null;
    }

    #endregion

    #region Activity Description

    /// <summary>
    /// Gets a description of recent animal activity at a position.
    /// Used for location descriptions.
    /// </summary>
    public string? GetActivityDescription(GridPosition pos)
    {
        var herdsHere = GetHerdsAt(pos);

        if (herdsHere.Count == 0)
        {
            // Check nearby for signs
            var nearbyHerds = GetHerdsInRange(pos, 1).Where(h => h.Position != pos).ToList();
            if (nearbyHerds.Count > 0)
            {
                var closest = nearbyHerds[0];
                return $"Fresh {closest.AnimalType.DisplayName().ToLower()} tracks lead away from here.";
            }
            return null;
        }

        // Multiple herds? Describe the most notable
        var mostNotable = herdsHere.OrderByDescending(h => h.IsPredator ? 100 : h.Count).First();
        return mostNotable.GetDescription() + ".";
    }

    /// <summary>
    /// Gets track descriptions for foraging clues.
    /// </summary>
    public IReadOnlyList<string> GetTrackDescriptions(GridPosition pos, int range)
    {
        return GetHerdsInRange(pos, range)
            .Select(h => h.GetTrackDescription())
            .Distinct()
            .ToList();
    }

    #endregion
}
