using text_survival.Environments;

namespace text_survival.Actions.Tensions;

/// <summary>
/// Represents an active tension in the game world - a building threat or opportunity
/// that can escalate, decay, or resolve based on player actions and time.
/// </summary>
public class ActiveTension
{
    public string Type { get; }
    public double Severity { get; set; }
    public DateTime CreatedAt { get; }
    public Location? RelevantLocation { get; }
    public Location? SourceLocation { get; }

    // Content properties (explicit, no Dictionary<string, object>)
    public string? AnimalType { get; }
    public string? Direction { get; }
    public string? Description { get; }

    // Decay behavior
    public double DecayPerHour { get; }
    public bool DecaysAtCamp { get; }

    private ActiveTension(
        string type,
        double severity,
        double decayPerHour,
        bool decaysAtCamp,
        Location? relevantLocation = null,
        Location? sourceLocation = null,
        string? animalType = null,
        string? direction = null,
        string? description = null)
    {
        Type = type;
        Severity = Math.Clamp(severity, 0.0, 1.0);
        CreatedAt = DateTime.Now;
        DecayPerHour = decayPerHour;
        DecaysAtCamp = decaysAtCamp;
        RelevantLocation = relevantLocation;
        SourceLocation = sourceLocation;
        AnimalType = animalType;
        Direction = direction;
        Description = description;
    }

    // === FACTORY METHODS ===
    // These encode the decay table as the source of truth

    /// <summary>
    /// A predator is stalking the player. Decays slowly at camp (predator loses the scent).
    /// </summary>
    public static ActiveTension Stalked(double severity, string? animalType = null, Location? location = null) => new(
        type: "Stalked",
        severity: severity,
        decayPerHour: 0.05,
        decaysAtCamp: true,
        relevantLocation: location,
        animalType: animalType
    );

    /// <summary>
    /// Someone spotted the player's smoke. Decays slowly (they're still out there).
    /// Does NOT decay at camp - the smoke source is the camp.
    /// </summary>
    public static ActiveTension SmokeSpotted(double severity, string? direction = null, Location? sourceLocation = null) => new(
        type: "SmokeSpotted",
        severity: severity,
        decayPerHour: 0.03,
        decaysAtCamp: false,
        sourceLocation: sourceLocation,
        direction: direction
    );

    /// <summary>
    /// Vermin have infested the camp. Does NOT decay - must be resolved through action.
    /// </summary>
    public static ActiveTension Infested(double severity, Location? location = null) => new(
        type: "Infested",
        severity: severity,
        decayPerHour: 0.0,
        decaysAtCamp: false,
        relevantLocation: location
    );

    /// <summary>
    /// An untreated wound that risks infection. Does NOT decay - escalates via effects.
    /// </summary>
    public static ActiveTension WoundUntreated(double severity, string? description = null) => new(
        type: "WoundUntreated",
        severity: severity,
        decayPerHour: 0.0,
        decaysAtCamp: false,
        description: description
    );

    /// <summary>
    /// Shelter has been damaged. Does NOT decay - structural, requires repair.
    /// </summary>
    public static ActiveTension ShelterWeakened(double severity, Location? location = null) => new(
        type: "ShelterWeakened",
        severity: severity,
        decayPerHour: 0.0,
        decaysAtCamp: false,
        relevantLocation: location
    );

    /// <summary>
    /// Strong food scent attracting predators. Decays quickly, especially at camp.
    /// </summary>
    public static ActiveTension FoodScentStrong(double severity) => new(
        type: "FoodScentStrong",
        severity: severity,
        decayPerHour: 0.10,
        decaysAtCamp: true
    );

    /// <summary>
    /// Player is actively being hunted by a specific predator.
    /// Higher urgency than Stalked - the predator has committed.
    /// </summary>
    public static ActiveTension Hunted(double severity, string? animalType = null) => new(
        type: "Hunted",
        severity: severity,
        decayPerHour: 0.02,
        decaysAtCamp: true,
        animalType: animalType
    );

    /// <summary>
    /// Player has marked a location for later discovery.
    /// Decays as memory fades.
    /// </summary>
    public static ActiveTension MarkedDiscovery(double severity, Location? location = null, string? description = null) => new(
        type: "MarkedDiscovery",
        severity: severity,
        decayPerHour: 0.02,
        decaysAtCamp: false,
        relevantLocation: location,
        description: description
    );

    /// <summary>
    /// Player witnessed disturbing content (death, remains, violence).
    /// Decays very slowly - trauma takes time to process.
    /// SourceLocation tracks where it happened for resolution events.
    /// </summary>
    public static ActiveTension Disturbed(double severity, Location? sourceLocation = null, string? description = null) => new(
        type: "Disturbed",
        severity: severity,
        decayPerHour: 0.02,
        decaysAtCamp: true,
        sourceLocation: sourceLocation,
        description: description
    );

    /// <summary>
    /// Player wounded prey that escaped. Trail decays as blood dries/snow covers.
    /// </summary>
    public static ActiveTension WoundedPrey(double severity, string? animalType = null, Location? location = null) => new(
        type: "WoundedPrey",
        severity: severity,
        decayPerHour: 0.08,
        decaysAtCamp: true,  // Trail goes cold if you return to camp
        relevantLocation: location,
        animalType: animalType
    );

    /// <summary>
    /// A pack of predators is nearby. Fire deters them; decay at camp reflects safety.
    /// </summary>
    public static ActiveTension PackNearby(double severity, string? animalType = null) => new(
        type: "PackNearby",
        severity: severity,
        decayPerHour: 0.03,
        decaysAtCamp: true,
        animalType: animalType
    );

    /// <summary>
    /// A shelter location is claimed by wildlife. No decay - structural situation.
    /// </summary>
    public static ActiveTension ClaimedTerritory(double severity, string? animalType = null, Location? location = null) => new(
        type: "ClaimedTerritory",
        severity: severity,
        decayPerHour: 0.0,
        decaysAtCamp: false,
        relevantLocation: location,
        animalType: animalType
    );

    /// <summary>
    /// A herd is passing through. High decay - they're migrating, window closes fast.
    /// </summary>
    public static ActiveTension HerdNearby(double severity, string? animalType = null, string? direction = null) => new(
        type: "HerdNearby",
        severity: severity,
        decayPerHour: 0.15,
        decaysAtCamp: true,
        animalType: animalType,
        direction: direction
    );

    /// <summary>
    /// Deadly cold exposure. No natural decay - resolves when reaching fire or shelter.
    /// </summary>
    public static ActiveTension DeadlyCold(double severity) => new(
        type: "DeadlyCold",
        severity: severity,
        decayPerHour: 0.0,
        decaysAtCamp: false  // Resolves via event, not decay
    );

    /// <summary>
    /// Fever/sickness rising. Slow decay, faster at camp with rest.
    /// Note: Camp decay handled by special logic in TensionRegistry.Update()
    /// </summary>
    public static ActiveTension FeverRising(double severity, string? description = null) => new(
        type: "FeverRising",
        severity: severity,
        decayPerHour: 0.01,
        decaysAtCamp: true,  // Decays 3x faster at camp (0.03 effective)
        description: description
    );

    /// <summary>
    /// Generic factory for custom tension types.
    /// </summary>
    public static ActiveTension Custom(
        string type,
        double severity,
        double decayPerHour,
        bool decaysAtCamp,
        Location? relevantLocation = null,
        string? animalType = null,
        string? direction = null,
        string? description = null) => new(
        type: type,
        severity: severity,
        decayPerHour: decayPerHour,
        decaysAtCamp: decaysAtCamp,
        relevantLocation: relevantLocation,
        animalType: animalType,
        direction: direction,
        description: description
    );
}
