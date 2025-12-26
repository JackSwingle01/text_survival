namespace text_survival.Environments.Features;

/// <summary>
/// A place to rest and sleep. Can be natural (soft ground, leaves) or constructed
/// (bedroll, fur pile). Enables sleeping at any location with bedding.
/// </summary>
public class BeddingFeature : LocationFeature
{
    /// <summary>
    /// Quality affects sleep efficiency (0-1).
    /// Crude bedding = 0.5, good bedding = 1.0
    /// Higher quality means faster energy recovery.
    /// </summary>
    public double Quality { get; set; } = 1.0;

    /// <summary>
    /// Description of this bedding.
    /// </summary>
    public string Description { get; init; } = "A place to rest.";

    /// <summary>
    /// Whether this bedding provides any wind protection during sleep.
    /// True for enclosed bedding, false for open ground.
    /// </summary>
    public bool HasWindProtection { get; init; } = false;

    /// <summary>
    /// Whether this bedding provides ground insulation.
    /// True for raised/insulated beds, false for bare ground.
    /// </summary>
    public bool HasGroundInsulation { get; init; } = false;

    /// <summary>
    /// Temperature bonus added while sleeping (in °F).
    /// Applied to effective temperature during rest/sleep.
    /// </summary>
    public double WarmthBonus { get; init; } = 0;

    public BeddingFeature() : base("Bedding") { }

    public BeddingFeature(string name) : base(name) { }

    /// <summary>
    /// Bedding doesn't decay over time.
    /// </summary>
    public override void Update(int minutes) { }

    /// <summary>
    /// Get a description of the bedding status.
    /// </summary>
    public string GetDescription()
    {
        var parts = new List<string>();

        if (Quality >= 0.9)
            parts.Add("comfortable");
        else if (Quality >= 0.6)
            parts.Add("adequate");
        else
            parts.Add("crude");

        if (HasWindProtection)
            parts.Add("sheltered");
        if (HasGroundInsulation)
            parts.Add("insulated");

        return string.Join(", ", parts);
    }

    /// <summary>
    /// Create home camp bedding (high quality, well-prepared).
    /// </summary>
    public static BeddingFeature CreateCampBedding() => new("Sleeping area")
    {
        Description = "Furs and leaves arranged near the fire.",
        Quality = 1.0,
        HasWindProtection = true,
        HasGroundInsulation = true,
        WarmthBonus = 0
    };

    /// <summary>
    /// Create makeshift field bedding (lower quality, quick setup).
    /// </summary>
    public static BeddingFeature CreateMakeshiftBedding() => new("Makeshift bed")
    {
        Description = "Leaves and branches arranged for rest.",
        Quality = 0.6,
        HasWindProtection = false,
        HasGroundInsulation = false,
        WarmthBonus = 0
    };

    /// <summary>
    /// Create a natural soft ground spot (very crude).
    /// </summary>
    public static BeddingFeature CreateNaturalBedding() => new("Soft ground")
    {
        Description = "A patch of soft leaves and moss.",
        Quality = 0.4,
        HasWindProtection = false,
        HasGroundInsulation = false,
        WarmthBonus = 0
    };

    /// <summary>
    /// Create padded bedding with plant fiber mat and hide blanket.
    /// Better than makeshift, not as good as sleeping bag.
    /// </summary>
    public static BeddingFeature CreatePaddedBedding() => new("Padded bedding")
    {
        Description = "Plant fiber mat with a hide blanket.",
        Quality = 0.8,
        HasWindProtection = false,
        HasGroundInsulation = true,  // Mat lifts off cold ground
        WarmthBonus = 0
    };

    /// <summary>
    /// Create sleeping bag sewn from hides.
    /// Best warmth and recovery for sleeping.
    /// </summary>
    public static BeddingFeature CreateSleepingBag() => new("Sleeping bag")
    {
        Description = "Hides sewn together into an enclosed sleeping bag.",
        Quality = 1.0,
        HasWindProtection = true,   // Enclosed bag
        HasGroundInsulation = true,
        WarmthBonus = 5.0  // +5°F while sleeping
    };
}
