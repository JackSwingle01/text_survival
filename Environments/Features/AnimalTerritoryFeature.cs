using text_survival.Actions;
using text_survival.Actions.Expeditions;
using text_survival.Actions.Expeditions.WorkStrategies;
using text_survival.Actors.Animals;
using text_survival.Items;

namespace text_survival.Environments.Features;

/// <summary>
/// Entry defining an animal type that can spawn in this territory.
/// </summary>
public record AnimalSpawnEntry(string AnimalType, double SpawnWeight);

/// <summary>
/// Defines what animals can be found at a location through hunting.
/// Animals are not pre-placed; they're spawned dynamically when searching.
/// Game density depletes with successful hunts and respawns over time.
/// </summary>
public class AnimalTerritoryFeature : LocationFeature, IWorkableFeature
{
    [System.Text.Json.Serialization.JsonInclude]
    private readonly List<AnimalSpawnEntry> _possibleAnimals = [];
    private readonly double _respawnRateHours = 72.0; // Full respawn takes 72 hours

    // Explicit private fields for serialization
    private double _baseGameDensity;
    private double _gameDensity;
    private double _initialDepletedDensity;
    private double _hoursSinceLastHunt;
    private (int Start, int End)? _peakHours;
    private double _peakMultiplier = 1.0;

    // Public properties backed by private fields
    internal double BaseGameDensity => _baseGameDensity;
    internal double GameDensity => _gameDensity;
    internal double InitialDepletedDensity => _initialDepletedDensity;
    internal double HoursSinceLastHunt => _hoursSinceLastHunt;
    internal (int Start, int End)? PeakHours => _peakHours;
    internal double PeakMultiplier => _peakMultiplier;

    // Derived from GameDensity - no need to track separately
    private bool HasBeenHunted => _gameDensity < _baseGameDensity;

    public AnimalTerritoryFeature(double gameDensity = 1.0) : base("animal_territory")
    {
        _baseGameDensity = gameDensity;
        _gameDensity = gameDensity;
    }

    [System.Text.Json.Serialization.JsonConstructor]
    public AnimalTerritoryFeature() : base("animal_territory") { }

    public override void Update(int minutes)
    {
        if (HasBeenHunted && _gameDensity < _baseGameDensity)
        {
            _hoursSinceLastHunt += minutes / 60.0;
            double depletedAmount = _baseGameDensity - _initialDepletedDensity;
            double respawnProgress = Math.Min(1.0, _hoursSinceLastHunt / _respawnRateHours);
            _gameDensity = _initialDepletedDensity + (depletedAmount * respawnProgress);
        }
    }

    /// <summary>
    /// Search for game. Returns an animal if found, null otherwise.
    /// </summary>
    /// <param name="minutesSearching">Time spent searching</param>
    /// <returns>An Animal if found, null if search fails</returns>
    public Animal? SearchForGame(int minutesSearching)
    {
        if (_possibleAnimals.Count == 0) return null;

        // Base chance scales with time spent and current density
        // 15 minutes of searching at full density = ~50% chance
        double baseChance = (minutesSearching / 30.0) * _gameDensity;
        double searchChance = Math.Min(0.9, baseChance); // Cap at 90%

        if (!Utils.DetermineSuccess(searchChance))
            return null;

        // Pick an animal based on spawn weights
        var entry = SelectRandomAnimal();
        if (entry == null) return null;

        return CreateAnimal(entry.AnimalType);
    }

    /// <summary>
    /// Record a successful hunt - depletes game density.
    /// </summary>
    public void RecordSuccessfulHunt()
    {
        _gameDensity *= 0.7; // 30% depletion per kill
        _initialDepletedDensity = _gameDensity;
        _hoursSinceLastHunt = 0;
    }

    private AnimalSpawnEntry? SelectRandomAnimal()
    {
        if (_possibleAnimals.Count == 0) return null;

        double totalWeight = _possibleAnimals.Sum(a => a.SpawnWeight);
        double roll = Random.Shared.NextDouble() * totalWeight;

        double cumulative = 0;
        foreach (var entry in _possibleAnimals)
        {
            cumulative += entry.SpawnWeight;
            if (roll <= cumulative)
                return entry;
        }

        return _possibleAnimals.Last();
    }

    private static Animal? CreateAnimal(string animalType)
    {
        return animalType.ToLower() switch
        {
            "deer" => AnimalFactory.MakeDeer(),
            "rabbit" => AnimalFactory.MakeRabbit(),
            "ptarmigan" => AnimalFactory.MakePtarmigan(),
            "fox" => AnimalFactory.MakeFox(),
            "wolf" => AnimalFactory.MakeWolf(),
            "bear" => AnimalFactory.MakeBear(),
            "cave bear" => AnimalFactory.MakeCaveBear(),
            "rat" => AnimalFactory.MakeRat(),
            _ => null
        };
    }

    /// <summary>
    /// Get work options for this feature.
    /// Only returns "Set snare" if player has snares. Hunt is a separate action type.
    /// </summary>
    public IEnumerable<WorkOption> GetWorkOptions(GameContext ctx)
    {
        // Only offer "Set snare" if player has usable snares
        var snares = ctx.Inventory.Tools.Where(t => t.Type == ToolType.Snare && t.Works).ToList();
        if (snares.Count > 0)
        {
            yield return new WorkOption(
                $"Set snare ({snares.Count} available)",
                "set_trap",
                new TrapStrategy(TrapStrategy.TrapMode.Set)
            );
        }
    }

    /// <summary>
    /// Check if hunting is possible in this territory.
    /// Separate from work options - hunting is an interactive action type.
    /// </summary>
    public bool CanHunt() => _possibleAnimals.Count > 0 && _gameDensity > 0.1;

    /// <summary>
    /// Get description for hunt menu option.
    /// </summary>
    public string GetHuntDescription() => $"Hunt ({GetQualityDescription()})";

    // Builder methods for configuration

    /// <summary>
    /// Add an animal type that can be found in this territory.
    /// </summary>
    public AnimalTerritoryFeature AddAnimal(string animalType, double spawnWeight = 1.0)
    {
        _possibleAnimals.Add(new AnimalSpawnEntry(animalType, spawnWeight));
        return this;
    }

    // Convenience methods for common configurations

    public AnimalTerritoryFeature AddDeer(double weight = 1.0) => AddAnimal("deer", weight);
    public AnimalTerritoryFeature AddRabbit(double weight = 1.0) => AddAnimal("rabbit", weight);
    public AnimalTerritoryFeature AddPtarmigan(double weight = 1.0) => AddAnimal("ptarmigan", weight);
    public AnimalTerritoryFeature AddFox(double weight = 0.5) => AddAnimal("fox", weight);
    public AnimalTerritoryFeature AddWolf(double weight = 0.3) => AddAnimal("wolf", weight);
    public AnimalTerritoryFeature AddBear(double weight = 0.2) => AddAnimal("bear", weight);

    /// <summary>
    /// Set peak activity hours when game is more likely to be found.
    /// Outside peak hours, base density applies. During peak hours, density is multiplied.
    /// </summary>
    /// <param name="startHour">Start of peak activity (0-23)</param>
    /// <param name="endHour">End of peak activity (0-23)</param>
    /// <param name="multiplier">Density multiplier during peak hours (e.g., 2.0 = double chance)</param>
    public AnimalTerritoryFeature WithPeakHours(int startHour, int endHour, double multiplier = 2.0)
    {
        _peakHours = (startHour, endHour);
        _peakMultiplier = multiplier;
        return this;
    }

    /// <summary>
    /// Check if current hour is within peak activity period.
    /// </summary>
    public bool IsPeakTime(int currentHour)
    {
        if (_peakHours == null) return false;
        var (start, end) = _peakHours.Value;
        if (start <= end)
            return currentHour >= start && currentHour < end;
        else // Wraps around midnight
            return currentHour >= start || currentHour < end;
    }

    /// <summary>
    /// Get the effective game density considering peak hours.
    /// </summary>
    public double GetEffectiveDensity(int currentHour)
    {
        double density = _gameDensity;
        if (IsPeakTime(currentHour))
            density *= _peakMultiplier;
        return Math.Min(1.5, density); // Cap at 150%
    }

    /// <summary>
    /// Get a description of what game might be found here.
    /// </summary>
    public string GetDescription()
    {
        if (_possibleAnimals.Count == 0) return "barren";

        // Find the heaviest animal type to determine description
        var heaviest = _possibleAnimals
            .Select(a => (a, GetAnimalWeight(a.AnimalType)))
            .OrderByDescending(x => x.Item2)
            .First();

        string gameType = heaviest.Item2 switch
        {
            > 100 => "signs of large game",
            > 20 => "medium animal tracks",
            > 5 => "small game signs",
            _ => "tiny creature signs"
        };

        string density = _gameDensity switch
        {
            >= 0.8 => "abundant",
            >= 0.5 => "moderate",
            >= 0.3 => "sparse",
            _ => "scarce"
        };

        return $"{density} {gameType}";
    }

    /// <summary>
    /// Get a quality description based on current game density.
    /// </summary>
    public string GetQualityDescription()
    {
        return _gameDensity switch
        {
            >= 0.8 => "plentiful",
            >= 0.5 => "decent",
            >= 0.3 => "sparse",
            _ => "overhunted"
        };
    }

    private static double GetAnimalWeight(string animalType)
    {
        return animalType.ToLower() switch
        {
            "deer" => 80,
            "rabbit" => 2,
            "ptarmigan" => 0.5,
            "fox" => 6,
            "wolf" => 40,
            "bear" => 250,
            "cave bear" => 350,
            "rat" => 2,
            _ => 10
        };
    }

    // Event system helpers

    /// <summary>
    /// Get a random animal name based on spawn weights (for event text).
    /// </summary>
    public string GetRandomAnimalName()
    {
        var entry = SelectRandomAnimal();
        return entry?.AnimalType ?? "animal";
    }

    /// <summary>
    /// Check if any predators exist in this territory.
    /// </summary>
    public bool HasPredators() => _possibleAnimals.Any(a => IsPredatorType(a.AnimalType));

    /// <summary>
    /// Get a random predator name from this territory (for "Something Watching" event).
    /// </summary>
    public string? GetRandomPredatorName()
    {
        var predators = _possibleAnimals.Where(a => IsPredatorType(a.AnimalType)).ToList();
        if (predators.Count == 0) return null;

        double totalWeight = predators.Sum(a => a.SpawnWeight);
        double roll = Random.Shared.NextDouble() * totalWeight;
        double cumulative = 0;
        foreach (var entry in predators)
        {
            cumulative += entry.SpawnWeight;
            if (roll <= cumulative)
                return entry.AnimalType;
        }
        return predators.Last().AnimalType;
    }

    private static bool IsPredatorType(string animalType)
    {
        return animalType.ToLower() switch
        {
            "wolf" or "bear" or "cave bear" => true,
            _ => false
        };
    }

    #region Save/Load Support - No longer needed with field-based serialization

    // Collection needs backing field for mutation
    // JsonIgnore prevents serializer from using this property instead of the private field
    [System.Text.Json.Serialization.JsonIgnore]
    internal IReadOnlyList<AnimalSpawnEntry> PossibleAnimals => _possibleAnimals.AsReadOnly();

    #endregion

    // Static factory methods for common territory configurations

    /// <summary>
    /// Create a mixed territory with prey and predators.
    /// </summary>
    public static AnimalTerritoryFeature CreateMixedTerritory(double gameDensity = 0.8)
    {
        return new AnimalTerritoryFeature(gameDensity)
            .AddDeer(1.0)
            .AddRabbit(1.5)
            .AddPtarmigan(1.0)
            .AddWolf(0.3);
    }

    /// <summary>
    /// Create a small game territory (no large predators).
    /// </summary>
    public static AnimalTerritoryFeature CreateSmallGameTerritory(double gameDensity = 1.0)
    {
        return new AnimalTerritoryFeature(gameDensity)
            .AddRabbit(1.5)
            .AddPtarmigan(1.2)
            .AddFox(0.4);
    }

    /// <summary>
    /// Create a predator-heavy territory.
    /// </summary>
    public static AnimalTerritoryFeature CreatePredatorTerritory(double gameDensity = 0.6)
    {
        return new AnimalTerritoryFeature(gameDensity)
            .AddDeer(0.5)
            .AddWolf(1.0)
            .AddBear(0.4);
    }
}
