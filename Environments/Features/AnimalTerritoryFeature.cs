using text_survival.Actions;
using text_survival.Actions.Expeditions;
using text_survival.Actions.Expeditions.WorkStrategies;
using text_survival.Actors.Animals;
using text_survival.Environments.Grid;
using text_survival.Items;

namespace text_survival.Environments.Features;

/// <summary>
/// Entry defining an animal type that can spawn in this territory.
/// </summary>
public record AnimalSpawnEntry(AnimalType AnimalType, double SpawnWeight);

/// <summary>
/// Defines what animals can be found at a location through hunting.
/// Animals are not pre-placed; they're spawned dynamically when searching.
/// Game density depletes with successful hunts and respawns over time.
/// </summary>
public class AnimalTerritoryFeature : LocationFeature, IWorkableFeature
{
    public override string? MapIcon => CanHunt() ? (HasPredators() ? "pets" : "cruelty_free") : null;
    public override int IconPriority => HasPredators() ? 3 : 2; // Predators show prominently

    public List<AnimalSpawnEntry> _possibleAnimals = [];
    private readonly double _respawnRateHours = 672.0; // Full respawn takes 4 weeks

    // Public fields for serialization (System.Text.Json IncludeFields requires public)
    public double _baseGameDensity;
    public double _gameDensity;
    public double _initialDepletedDensity;
    public double _hoursSinceLastHunt;
    public (int Start, int End)? _peakHours;
    public double _peakMultiplier = 1.0;

    // Temporary bonus from following game clues during foraging
    [System.Text.Json.Serialization.JsonInclude]
    public double _temporaryHuntBonus;
    [System.Text.Json.Serialization.JsonInclude]
    public double _huntBonusDecayMinutes;  // Time remaining before bonus expires

    // Public properties backed by private fields
    internal double BaseGameDensity => _baseGameDensity;
    internal double GameDensity => _gameDensity;
    internal double InitialDepletedDensity => _initialDepletedDensity;
    internal double HoursSinceLastHunt => _hoursSinceLastHunt;
    internal (int Start, int End)? PeakHours => _peakHours;
    internal double PeakMultiplier => _peakMultiplier;

    /// <summary>
    /// Temporary hunt bonus from following game clues. Consumed on next hunt.
    /// </summary>
    internal double TemporaryHuntBonus => _temporaryHuntBonus;

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

        // Decay temporary hunt bonus over 2 hours
        if (_temporaryHuntBonus > 0 && _huntBonusDecayMinutes > 0)
        {
            _huntBonusDecayMinutes -= minutes;
            if (_huntBonusDecayMinutes <= 0)
            {
                _temporaryHuntBonus = 0;
                _huntBonusDecayMinutes = 0;
            }
        }
    }

    /// <summary>
    /// Apply a temporary hunt bonus from following game clues.
    /// Takes the highest bonus if multiple applied.
    /// </summary>
    public void ApplyHuntBonus(double bonus)
    {
        if (bonus > _temporaryHuntBonus)
        {
            _temporaryHuntBonus = bonus;
            _huntBonusDecayMinutes = 120; // 2 hours
        }
    }

    /// <summary>
    /// Consume and return the temporary hunt bonus.
    /// </summary>
    public double ConsumeHuntBonus()
    {
        double bonus = _temporaryHuntBonus;
        _temporaryHuntBonus = 0;
        _huntBonusDecayMinutes = 0;
        return bonus;
    }

    /// <summary>
    /// Search for game. Returns a small game animal if found, null otherwise.
    /// Large game (caribou, wolves, bears, etc.) come from persistent herds via HerdRegistry.
    /// Automatically consumes any temporary hunt bonus from game clues.
    /// </summary>
    /// <param name="minutesSearching">Time spent searching</param>
    /// <param name="location">Location where search is happening</param>
    /// <param name="map">Game map</param>
    /// <returns>A small game Animal if found, null if search fails</returns>
    public Animal? SearchForGame(int minutesSearching, Location location, GameMap map)
    {
        // Filter to small game only - large game comes from persistent herds
        var smallGame = _possibleAnimals.Where(a => a.AnimalType.IsSmallGame()).ToList();
        if (smallGame.Count == 0) return null;

        // Consume temporary bonus if any (from following game clues during foraging)
        double clueBonus = ConsumeHuntBonus();

        // Base chance scales with time spent and current density
        // 15 minutes of searching at full density = ~50% chance
        double baseChance = (minutesSearching / 30.0) * (_gameDensity + clueBonus);
        double searchChance = Math.Min(0.9, baseChance); // Cap at 90%

        if (!Utils.DetermineSuccess(searchChance))
            return null;

        // Pick a small game animal based on spawn weights
        var entry = SelectRandomAnimalFrom(smallGame);
        if (entry == null) return null;

        return CreateAnimal(entry.AnimalType, location, map);
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
        return SelectRandomAnimalFrom(_possibleAnimals);
    }

    private static AnimalSpawnEntry? SelectRandomAnimalFrom(List<AnimalSpawnEntry> animals)
    {
        if (animals.Count == 0) return null;

        double totalWeight = animals.Sum(a => a.SpawnWeight);
        double roll = Random.Shared.NextDouble() * totalWeight;

        double cumulative = 0;
        foreach (var entry in animals)
        {
            cumulative += entry.SpawnWeight;
            if (roll <= cumulative)
                return entry;
        }

        return animals.Last();
    }

    private static Animal? CreateAnimal(AnimalType animalType, Location location, GameMap map) => AnimalFactory.FromType(animalType, location, map);

    /// <summary>
    /// Get work options for this feature.
    /// Returns Hunt (if game available) and Set snare (if player has snares).
    /// </summary>
    public IEnumerable<WorkOption> GetWorkOptions(GameContext ctx)
    {
        // Hunt option - search for game
        if (CanHunt())
        {
            yield return new WorkOption(
                GetHuntDescription(),
                "hunt",
                new HuntStrategy()
            );
        }

        // Set snare option - if player has usable snares
        var snares = ctx.Inventory.Tools.Where(t => t.ToolType == ToolType.Snare && t.Works).ToList();
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
    public AnimalTerritoryFeature AddAnimal(AnimalType animalType, double spawnWeight = 1.0)
    {
        _possibleAnimals.Add(new AnimalSpawnEntry(animalType, spawnWeight));
        return this;
    }

    // Convenience methods for common configurations

    public AnimalTerritoryFeature AddCaribou(double weight = 1.0) => AddAnimal(AnimalType.Caribou, weight);
    public AnimalTerritoryFeature AddRabbit(double weight = 1.0) => AddAnimal(AnimalType.Rabbit, weight);
    public AnimalTerritoryFeature AddPtarmigan(double weight = 1.0) => AddAnimal(AnimalType.Ptarmigan, weight);
    public AnimalTerritoryFeature AddFox(double weight = 0.5) => AddAnimal(AnimalType.Fox, weight);
    public AnimalTerritoryFeature AddWolf(double weight = 0.3) => AddAnimal(AnimalType.Wolf, weight);
    public AnimalTerritoryFeature AddBear(double weight = 0.2) => AddAnimal(AnimalType.Bear, weight);
    public AnimalTerritoryFeature AddMegaloceros(double weight = 0.3) => AddAnimal(AnimalType.Megaloceros, weight);
    public AnimalTerritoryFeature AddBison(double weight = 0.4) => AddAnimal(AnimalType.Bison, weight);
    public AnimalTerritoryFeature AddHyena(double weight = 0.3) => AddAnimal(AnimalType.Hyena, weight);

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
            .Select(a => (a, a.AnimalType.WeightKg()))
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
            _ => "barren"
        };
    }

    // Event system helpers

    /// <summary>
    /// Get a random animal type based on spawn weights (for event text).
    /// </summary>
    public AnimalType? GetRandomAnimal()
    {
        var entry = SelectRandomAnimal();
        return entry?.AnimalType;
    }

    /// <summary>
    /// Check if any predators exist in this territory.
    /// </summary>
    public bool HasPredators() => _possibleAnimals.Any(a => a.AnimalType.IsPredator());

    /// <summary>
    /// Get a random predator type from this territory (for "Something Watching" event).
    /// </summary>
    public AnimalType? GetRandomPredator()
    {
        var predators = _possibleAnimals.Where(a => a.AnimalType.IsPredator()).ToList();
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

    public override FeatureUIInfo? GetUIInfo()
    {
        if (!CanHunt()) return null;
        return new FeatureUIInfo(
            "animal",
            HasPredators() ? "Predator Territory" : "Wildlife",
            GetDescription(),
            null);
    }

    public override List<Resource> ProvidedResources() =>
        CanHunt() ? [Resource.RawMeat] : [];

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
            .AddCaribou(1.0)
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
            .AddCaribou(0.5)
            .AddWolf(1.0)
            .AddBear(0.4);
    }
}
