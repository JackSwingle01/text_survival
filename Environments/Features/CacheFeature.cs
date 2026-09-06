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
    public override string? MapIcon => HasItems ? "cache" : null;
    public override int IconPriority => 2;

    /// <summary>
    /// The storage inventory for this cache.
    /// </summary>
    [System.Text.Json.Serialization.JsonInclude]
    public Inventory Storage { get; private set; }

    /// <summary>
    /// Type of cache - affects UI and available features.
    /// </summary>
    [System.Text.Json.Serialization.JsonInclude]
    public CacheType Type { get; private set; }

    public override double PlacementHeightM => 0.5;
    public override double PlacementFootprintM2 => 1.0;
    public override bool BlockedByCover => true;
    public override string AccessName => Name;

    /// <summary>
    /// Maximum storage capacity in kg.
    /// </summary>
    [System.Text.Json.Serialization.JsonInclude]
    public double CapacityKg { get; private set; }

    /// <summary>
    /// If true, stored food won't attract predators.
    /// </summary>
    [System.Text.Json.Serialization.JsonInclude]
    public bool ProtectsFromPredators { get; private set; }

    /// <summary>
    /// If true, cache is protected from weather (rain, snow).
    /// </summary>
    [System.Text.Json.Serialization.JsonInclude]
    public bool ProtectsFromWeather { get; private set; }

    /// <summary>
    /// If true, food stored here won't decay (ice cache).
    /// </summary>
    [System.Text.Json.Serialization.JsonInclude]
    public bool PreservesFood { get; private set; }

    public CacheFeature()
        : base("")
    {
        Storage = new Inventory();
    }

    public CacheFeature(
        string name,
        CacheType type,
        double capacityKg = 100,
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

        double used = Storage.CurrentWeightKg;
        parts.Add($"{used:F1}/{CapacityKg:F0} kg");

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
    public bool IsAtCapacity => Storage.CurrentWeightKg >= CapacityKg;

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

    public override List<Resource> ProvidedResources() =>
        Storage.GetResourceTypes();

    /// <summary>
    /// Create a basic camp cache (large storage, no special protection).
    /// </summary>
    public static CacheFeature CreateCampCache() => new(
        "Camp Storage",
        CacheType.Built,
        capacityKg: 1000,
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
