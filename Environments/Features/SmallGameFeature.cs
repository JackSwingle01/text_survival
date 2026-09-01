using text_survival.Actions;
using text_survival.Actions.Expeditions;
using text_survival.Actions.Expeditions.WorkStrategies;
using text_survival.Actors.Animals;
using text_survival.Environments.Grid;
using text_survival.Items;

namespace text_survival.Environments.Features;

/// <summary>
/// Entry defining a small game type that can spawn on this tile.
/// </summary>
public record AnimalSpawnEntry(AnimalType AnimalType, double SpawnWeight);

/// <summary>
/// Ambient small game on a tile: rabbits, ptarmigan, fox, fish.
/// Modelled as a density like forage, not as entities. Animals spawn when searched for,
/// density depletes with kills and respawns over weeks.
/// Large animals are never here. They live in herds; see <see cref="AnimalPresence"/>.
/// </summary>
public class SmallGameFeature : LocationFeature, IWorkableFeature
{
    public override string? MapIcon => CanHunt() ? "prey" : null;
    public override int IconPriority => 2;

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

    internal double BaseGameDensity => _baseGameDensity;
    internal double GameDensity => _gameDensity;
    internal double InitialDepletedDensity => _initialDepletedDensity;
    internal double HoursSinceLastHunt => _hoursSinceLastHunt;
    internal (int Start, int End)? PeakHours => _peakHours;
    internal double PeakMultiplier => _peakMultiplier;
    internal double TemporaryHuntBonus => _temporaryHuntBonus;

    private bool HasBeenHunted => _gameDensity < _baseGameDensity;

    public SmallGameFeature(double gameDensity = 1.0) : base("animal_territory")
    {
        _baseGameDensity = gameDensity;
        _gameDensity = gameDensity;
    }

    [System.Text.Json.Serialization.JsonConstructor]
    public SmallGameFeature() : base("animal_territory") { }

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
    /// Apply a temporary hunt bonus from following game clues. Takes the highest bonus if multiple applied.
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
    /// Search for small game. Returns an animal if found, null otherwise.
    /// Automatically consumes any temporary hunt bonus from game clues.
    /// </summary>
    public Animal? SearchForGame(int minutesSearching, Location location, GameMap map)
    {
        var smallGame = SmallGameEntries;
        if (smallGame.Count == 0) return null;

        double clueBonus = ConsumeHuntBonus();

        // 15 minutes of searching at full density = ~50% chance
        double baseChance = (minutesSearching / 30.0) * (_gameDensity + clueBonus);
        double searchChance = Math.Min(0.9, baseChance);

        if (!Utils.DetermineSuccess(searchChance))
            return null;

        var entry = SelectRandomAnimalFrom(smallGame);
        if (entry == null) return null;

        return AnimalFactory.FromType(entry.AnimalType, location, map);
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

    /// <summary>
    /// A random small game type from this tile's spawn list, for event text. Null if none.
    /// </summary>
    public AnimalType? RandomSmallGame() => SelectRandomAnimalFrom(SmallGameEntries)?.AnimalType;

    // Old saves may still carry large-animal entries from before herds owned them; ignore those.
    private List<AnimalSpawnEntry> SmallGameEntries =>
        _possibleAnimals.Where(a => a.AnimalType.IsSmallGame()).ToList();

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

    /// <summary>
    /// Snares can be set wherever small game lives. Hunting is offered by the Location,
    /// which combines small game with any herds on the tile.
    /// </summary>
    public IEnumerable<WorkOption> GetWorkOptions(GameContext ctx)
    {
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

    public bool CanHunt() => SmallGameEntries.Count > 0 && _gameDensity > 0.1;

    // Builder methods for configuration

    public SmallGameFeature AddAnimal(AnimalType animalType, double spawnWeight = 1.0)
    {
        if (!animalType.IsSmallGame())
            throw new ArgumentException($"{animalType} is not small game. Large animals are placed as herds by HerdPopulator.");
        _possibleAnimals.Add(new AnimalSpawnEntry(animalType, spawnWeight));
        return this;
    }

    public SmallGameFeature AddRabbit(double weight = 1.0) => AddAnimal(AnimalType.Rabbit, weight);
    public SmallGameFeature AddPtarmigan(double weight = 1.0) => AddAnimal(AnimalType.Ptarmigan, weight);
    public SmallGameFeature AddFox(double weight = 0.5) => AddAnimal(AnimalType.Fox, weight);

    /// <summary>
    /// Set peak activity hours when game is more likely to be found.
    /// </summary>
    public SmallGameFeature WithPeakHours(int startHour, int endHour, double multiplier = 2.0)
    {
        _peakHours = (startHour, endHour);
        _peakMultiplier = multiplier;
        return this;
    }

    public bool IsPeakTime(int currentHour)
    {
        if (_peakHours == null) return false;
        var (start, end) = _peakHours.Value;
        if (start <= end)
            return currentHour >= start && currentHour < end;
        else // Wraps around midnight
            return currentHour >= start || currentHour < end;
    }

    public double GetEffectiveDensity(int currentHour)
    {
        double density = _gameDensity;
        if (IsPeakTime(currentHour))
            density *= _peakMultiplier;
        return Math.Min(1.5, density); // Cap at 150%
    }

    public string GetDescription()
    {
        if (SmallGameEntries.Count == 0) return "barren";
        return $"{GetQualityDescription()} small game signs";
    }

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

    public override FeatureUIInfo? GetUIInfo()
    {
        if (!CanHunt()) return null;
        return new FeatureUIInfo("animal", "Small Game", GetDescription(), null);
    }

    public override List<Resource> ProvidedResources() =>
        CanHunt() ? [Resource.RawMeat] : [];

    [System.Text.Json.Serialization.JsonIgnore]
    internal IReadOnlyList<AnimalSpawnEntry> PossibleAnimals => _possibleAnimals.AsReadOnly();
}
