using text_survival.Actions;
using text_survival.Actions.Expeditions;
using text_survival.Actions.Expeditions.WorkStrategies;
using text_survival.Items;

namespace text_survival.Environments.Features;

/// <summary>
/// A storage cache at a location - can be natural (ice crevasse, tree cache)
/// or built (raised platform, pit cache).
/// </summary>
public class CacheFeature : LocationFeature, IWorkableFeature
{
    public override string? MapIcon => HasItems ? "inventory_2" : null;
    public override int IconPriority => 2;

    /// <summary>
    /// The storage inventory for this cache.
    /// </summary>
    public Inventory Storage { get; }

    /// <summary>
    /// Type of cache - affects UI and available features.
    /// </summary>
    public CacheType Type { get; }

    /// <summary>
    /// Maximum storage capacity in kg (-1 for unlimited).
    /// </summary>
    public double CapacityKg { get; }

    /// <summary>
    /// If true, stored food won't attract predators.
    /// </summary>
    public bool ProtectsFromPredators { get; }

    /// <summary>
    /// If true, cache is protected from weather (rain, snow).
    /// </summary>
    public bool ProtectsFromWeather { get; }

    /// <summary>
    /// If true, food stored here won't decay (ice cache).
    /// </summary>
    public bool PreservesFood { get; }

    public CacheFeature(
        string name,
        CacheType type,
        double capacityKg = -1,
        bool protectsFromPredators = false,
        bool protectsFromWeather = false,
        bool preservesFood = false)
        : base(name)
    {
        Type = type;
        CapacityKg = capacityKg;
        ProtectsFromPredators = protectsFromPredators;
        ProtectsFromWeather = protectsFromWeather;
        PreservesFood = preservesFood;

        // Create storage with capacity limit
        Storage = new Inventory { MaxWeightKg = capacityKg };
    }

    /// <summary>
    /// Get a description of the cache status.
    /// </summary>
    public string GetDescription()
    {
        var parts = new List<string>();

        if (CapacityKg > 0)
        {
            double used = Storage.CurrentWeightKg;
            parts.Add($"{used:F1}/{CapacityKg:F0} kg");
        }
        else
        {
            parts.Add($"{Storage.CurrentWeightKg:F1} kg stored");
        }

        if (ProtectsFromPredators) parts.Add("predator-safe");
        if (PreservesFood) parts.Add("preserves food");

        return string.Join(", ", parts);
    }

    /// <summary>
    /// Check if cache has any items stored.
    /// </summary>
    public bool HasItems => Storage.CurrentWeightKg > 0;

    /// <summary>
    /// Check if cache can be accessed. Always true if cache exists.
    /// Future: could return false if cache is damaged, buried, etc.
    /// </summary>
    public bool CanBeAccessed => true;

    /// <summary>
    /// Check if cache is at capacity.
    /// </summary>
    public bool IsAtCapacity => CapacityKg > 0 && Storage.CurrentWeightKg >= CapacityKg;

    /// <summary>
    /// Get work options for this feature.
    /// Skip camp storage since it's accessed via the sidebar button.
    /// </summary>
    public IEnumerable<WorkOption> GetWorkOptions(GameContext ctx)
    {
        if (!CanBeAccessed) yield break;

        // Skip camp storage - accessed via sidebar button instead
        if (ctx.CurrentLocation == ctx.Camp && Name == "Camp Storage")
            yield break;

        yield return new WorkOption(
            $"Access {Name} ({GetDescription()})",
            "cache",
            new CacheStrategy()
        );
    }

    /// <summary>
    /// Create a basic camp cache (unlimited storage, no special protection).
    /// </summary>
    public static CacheFeature CreateCampCache() => new(
        "Camp Storage",
        CacheType.Built,
        capacityKg: -1,
        protectsFromPredators: false,
        protectsFromWeather: true,
        preservesFood: false
    );

    /// <summary>
    /// Create an ice crevasse cache (preserves food, predator-safe).
    /// </summary>
    public static CacheFeature CreateIceCache() => new(
        "Ice Crevasse",
        CacheType.Natural,
        capacityKg: 50,
        protectsFromPredators: true,
        protectsFromWeather: true,
        preservesFood: true
    );

    /// <summary>
    /// Create a tree cache (hung from branches, predator-safe from ground predators).
    /// </summary>
    public static CacheFeature CreateTreeCache() => new(
        "Tree Cache",
        CacheType.Natural,
        capacityKg: 30,
        protectsFromPredators: true,
        protectsFromWeather: false,
        preservesFood: false
    );

    /// <summary>
    /// Create a rock cleft cache (weather protected).
    /// </summary>
    public static CacheFeature CreateRockCache() => new(
        "Rock Cleft",
        CacheType.Natural,
        capacityKg: 40,
        protectsFromPredators: false,
        protectsFromWeather: true,
        preservesFood: false
    );
}

public enum CacheType
{
    Natural,    // Found in the environment (ice crevasse, tree, rock cleft)
    Built       // Player-constructed (raised platform, pit cache)
}
