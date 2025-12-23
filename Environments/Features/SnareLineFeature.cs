namespace text_survival.Environments.Features;

/// <summary>
/// Manages placed snares at a location.
/// Requires AnimalTerritoryFeature for valid placement.
/// </summary>
public class SnareLineFeature : LocationFeature
{
    private readonly List<PlacedSnare> _snares = [];
    private readonly AnimalTerritoryFeature _territory;

    // Small game weight threshold (kg)
    private const double SmallGameMaxWeightKg = 10.0;

    public SnareLineFeature(AnimalTerritoryFeature territory) : base("snare_line")
    {
        _territory = territory;
    }

    /// <summary>
    /// Number of active snares at this location.
    /// </summary>
    public int SnareCount => _snares.Count(s => s.IsUsable);

    /// <summary>
    /// Number of snares with catches ready.
    /// </summary>
    public int CatchCount => _snares.Count(s => s.HasCatch);

    /// <summary>
    /// Check if any snares have catches or stolen remains.
    /// </summary>
    public bool HasAnythingToCheck => _snares.Any(s =>
        s.State == SnareState.CatchReady ||
        s.State == SnareState.Stolen ||
        s.State == SnareState.Destroyed);

    /// <summary>
    /// Check if any snares are baited.
    /// </summary>
    public bool HasBaitedSnares => _snares.Any(s => s.IsBaited);

    /// <summary>
    /// Check if any snares have catches waiting.
    /// </summary>
    public bool HasCatchWaiting => _snares.Any(s => s.State == SnareState.CatchReady);

    /// <summary>
    /// Place a new snare at this location.
    /// </summary>
    public void PlaceSnare(int durability, bool reinforced = false)
    {
        _snares.Add(new PlacedSnare(durability, reinforced));
    }

    /// <summary>
    /// Place a new snare with bait.
    /// </summary>
    public void PlaceSnareWithBait(int durability, BaitType bait, bool reinforced = false)
    {
        var snare = new PlacedSnare(durability, reinforced);
        snare.AddBait(bait);
        _snares.Add(snare);
    }

    /// <summary>
    /// Remove a snare from this location.
    /// Returns the snare if found.
    /// </summary>
    public PlacedSnare? RemoveSnare(int index)
    {
        if (index < 0 || index >= _snares.Count)
            return null;

        var snare = _snares[index];
        _snares.RemoveAt(index);
        return snare;
    }

    /// <summary>
    /// Get all snares for inspection.
    /// </summary>
    public IReadOnlyList<PlacedSnare> GetSnares() => _snares.AsReadOnly();

    /// <summary>
    /// Update all snares for elapsed time.
    /// </summary>
    public override void Update(int minutes)
    {
        var smallGame = GetSmallGameList();

        foreach (var snare in _snares.Where(s => s.IsUsable))
        {
            snare.Update(minutes, GetGameDensity(), smallGame);
        }

        // Remove broken snares
        _snares.RemoveAll(s => !s.IsUsable && s.State != SnareState.CatchReady && s.State != SnareState.Stolen);
    }

    /// <summary>
    /// Check all snares and collect results.
    /// Returns list of (animalType, weightKg, wasStolen) for each snare checked.
    /// </summary>
    public List<SnareCheckResult> CheckAllSnares()
    {
        var results = new List<SnareCheckResult>();

        foreach (var snare in _snares.ToList()) // ToList() to allow modification during iteration
        {
            if (snare.State == SnareState.CatchReady)
            {
                var (type, weight) = snare.CollectCatch();
                if (type != null)
                {
                    results.Add(new SnareCheckResult(type, weight, false, false));
                }
            }
            else if (snare.State == SnareState.Stolen)
            {
                var (type, weight) = snare.GetStolenRemains();
                results.Add(new SnareCheckResult(type ?? "unknown", weight, true, false));
            }
            else if (snare.State == SnareState.Destroyed)
            {
                results.Add(new SnareCheckResult(null, 0, false, true));
                _snares.Remove(snare);
            }
        }

        // Remove broken snares after checking
        _snares.RemoveAll(s => !s.IsUsable);

        return results;
    }

    /// <summary>
    /// Get current game density from territory.
    /// </summary>
    private double GetGameDensity()
    {
        // Access via reflection or public method if available
        // For now, assume moderate density
        return 0.7;
    }

    /// <summary>
    /// Get list of small game animals that can be caught.
    /// Filters AnimalTerritoryFeature spawn list to animals under 10kg.
    /// </summary>
    private List<(string type, double weightKg)> GetSmallGameList()
    {
        // Small game that can be caught in snares
        var smallGame = new List<(string type, double weightKg)>
        {
            ("rabbit", 2.0),
            ("ptarmigan", 0.5),
            ("fox", 6.0)
        };

        return smallGame;
    }

    /// <summary>
    /// Get a summary description of the snare line.
    /// </summary>
    public string GetDescription()
    {
        var usable = _snares.Count(s => s.IsUsable);
        var catches = _snares.Count(s => s.HasCatch);
        var stolen = _snares.Count(s => s.State == SnareState.Stolen);

        if (catches > 0)
            return $"{usable} snares ({catches} with catches!)";
        if (stolen > 0)
            return $"{usable} snares ({stolen} plundered)";
        return $"{usable} snares set";
    }
}

/// <summary>
/// Result of checking a single snare.
/// </summary>
public record SnareCheckResult(string? AnimalType, double WeightKg, bool WasStolen, bool WasDestroyed);
