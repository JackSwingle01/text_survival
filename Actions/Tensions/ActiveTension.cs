using text_survival.Actors.Animals;
using text_survival.Environments;

namespace text_survival.Actions.Tensions;

/// <summary>
/// Discrete stages for tension severity. Stage transitions trigger narrative events.
/// </summary>
public enum TensionStage
{
    Resolved = 0,   // Severity <= 0 (will be removed)
    Building = 1,   // 0 < severity < 0.33
    Escalating = 2, // 0.33 <= severity < 0.66
    Critical = 3    // severity >= 0.66
}

/// <summary>
/// Records a tension stage transition for event triggering.
/// </summary>
public record TensionStageChange(
    string TensionType,
    TensionStage? Previous,
    TensionStage Current,
    ActiveTension Tension
)
{
    public bool IsCreation => Previous == null;
    public bool IsEscalation => Previous != null && Current > Previous;
    public bool IsDeescalation => Previous != null && Current < Previous;
    public bool IsResolution => Current == TensionStage.Resolved;
};

public class ActiveTension
{
    public string Type { get; }
    private double _severity;
    public double Severity
    {
        get => _severity;
        set => _severity = Math.Clamp(value, 0.0, 1.0);
    }
    public DateTime CreatedAt { get; }
    public Location? RelevantLocation { get; }
    public Location? SourceLocation { get; }

    // Content properties (explicit, no Dictionary<string, object>)
    public AnimalType? AnimalType { get; }
    public string? Direction { get; }
    public string? Description { get; }

    public Herd? SourceHerd { get; }

    // Decay behavior
    public double DecayPerHour { get; }
    public bool DecaysAtCamp { get; }

    // Stage tracking for event triggers
    private readonly StageTracker<TensionStage> _stageTracker = new(severity => severity switch
    {
        <= 0 => TensionStage.Resolved,
        < 0.33 => TensionStage.Building,
        < 0.66 => TensionStage.Escalating,
        _ => TensionStage.Critical
    });

    /// <summary>
    /// Current stage based on severity.
    /// </summary>
    public TensionStage Stage => _stageTracker.CurrentStage ?? TensionStage.Resolved;

    private ActiveTension(
        string type,
        double severity,
        double decayPerHour,
        bool decaysAtCamp,
        Location? relevantLocation = null,
        Location? sourceLocation = null,
        AnimalType? animalType = null,
        string? direction = null,
        string? description = null,
        Herd? sourceHerd = null)
    {
        Type = type;
        _severity = Math.Clamp(severity, 0.0, 1.0);
        CreatedAt = DateTime.Now;
        DecayPerHour = decayPerHour;
        DecaysAtCamp = decaysAtCamp;
        RelevantLocation = relevantLocation;
        SourceLocation = sourceLocation;
        AnimalType = animalType;
        Direction = direction;
        Description = description;
        SourceHerd = sourceHerd;

        // Initialize stage tracker with current severity
        _stageTracker.Update(_severity);
    }

    /// <summary>
    /// Update severity and return stage change if a threshold was crossed.
    /// </summary>
    public TensionStageChange? UpdateSeverity(double newSeverity)
    {
        _severity = Math.Clamp(newSeverity, 0.0, 1.0);
        var change = _stageTracker.Update(_severity);

        if (change != null)
        {
            return new TensionStageChange(Type, change.Previous, change.Current, this);
        }
        return null;
    }

    public static ActiveTension Stalked(double severity, AnimalType? animalType = null, Location? location = null, Herd? sourceHerd = null) => new(
        type: "Stalked",
        severity: severity,
        decayPerHour: 0.05,
        decaysAtCamp: true,
        relevantLocation: location,
        animalType: animalType,
        sourceHerd: sourceHerd
    );

    public static ActiveTension SmokeSpotted(double severity, string? direction = null, Location? sourceLocation = null) => new(
        type: "SmokeSpotted",
        severity: severity,
        decayPerHour: 0.03,
        decaysAtCamp: false,
        sourceLocation: sourceLocation,
        direction: direction
    );

    public static ActiveTension Infested(double severity, Location? location = null) => new(
        type: "Infested",
        severity: severity,
        decayPerHour: 0.0,
        decaysAtCamp: false,
        relevantLocation: location
    );

    public static ActiveTension WoundUntreated(double severity, string? description = null) => new(
        type: "WoundUntreated",
        severity: severity,
        decayPerHour: 0.0,
        decaysAtCamp: false,
        description: description
    );

    public static ActiveTension ShelterWeakened(double severity, Location? location = null) => new(
        type: "ShelterWeakened",
        severity: severity,
        decayPerHour: 0.0,
        decaysAtCamp: false,
        relevantLocation: location
    );

    public static ActiveTension FoodScentStrong(double severity) => new(
        type: "FoodScentStrong",
        severity: severity,
        decayPerHour: 0.10,
        decaysAtCamp: true
    );

    public static ActiveTension Hunted(double severity, AnimalType? animalType = null) => new(
        type: "Hunted",
        severity: severity,
        decayPerHour: 0.02,
        decaysAtCamp: true,
        animalType: animalType
    );

    public static ActiveTension MarkedDiscovery(double severity, Location? location = null, string? description = null) => new(
        type: "MarkedDiscovery",
        severity: severity,
        decayPerHour: 0.02,
        decaysAtCamp: false,
        relevantLocation: location,
        description: description
    );

    public static ActiveTension Disturbed(double severity, Location? sourceLocation = null, string? description = null) => new(
        type: "Disturbed",
        severity: severity,
        decayPerHour: 0.02,
        decaysAtCamp: true,
        sourceLocation: sourceLocation,
        description: description
    );

    public static ActiveTension WoundedPrey(double severity, AnimalType? animalType = null, Location? location = null, Herd? sourceHerd = null) => new(
        type: "WoundedPrey",
        severity: severity,
        decayPerHour: 0.08,
        decaysAtCamp: true,  // Trail goes cold if you return to camp
        relevantLocation: location,
        animalType: animalType,
        sourceHerd: sourceHerd
    );

    public static ActiveTension PackNearby(double severity, AnimalType? animalType = null, Herd? sourceHerd = null) => new(
        type: "PackNearby",
        severity: severity,
        decayPerHour: 0.03,
        decaysAtCamp: true,
        animalType: animalType,
        sourceHerd: sourceHerd
    );

    public static ActiveTension ClaimedTerritory(double severity, AnimalType? animalType = null, Location? location = null) => new(
        type: "ClaimedTerritory",
        severity: severity,
        decayPerHour: 0.0,
        decaysAtCamp: false,
        relevantLocation: location,
        animalType: animalType
    );

    public static ActiveTension HerdNearby(double severity, AnimalType? animalType = null, string? direction = null) => new(
        type: "HerdNearby",
        severity: severity,
        decayPerHour: 0.15,
        decaysAtCamp: true,
        animalType: animalType,
        direction: direction
    );

    public static ActiveTension DeadlyCold(double severity) => new(
        type: "DeadlyCold",
        severity: severity,
        decayPerHour: 0.0,
        decaysAtCamp: false  // Resolves via event, not decay
    );

    public static ActiveTension FeverRising(double severity, string? description = null) => new(
        type: "FeverRising",
        severity: severity,
        decayPerHour: 0.01,
        decaysAtCamp: true,  // Decays 3x faster at camp (0.03 effective)
        description: description
    );

    public static ActiveTension TrapLineActive(double severity, Location? location = null) => new(
        type: "TrapLineActive",
        severity: severity,
        decayPerHour: 0.02,
        decaysAtCamp: false,  // Traps stay set whether you're at camp or not
        relevantLocation: location
    );

    public static ActiveTension MammothTracked(double severity, Location? location = null) => new(
        type: "MammothTracked",
        severity: severity,
        decayPerHour: 0.01,  // Trail goes cold slowly
        decaysAtCamp: true,  // Decay when not actively tracking
        relevantLocation: location
    );

    public static ActiveTension FreshTrail(double severity, AnimalType? animalType = null) => new(
        type: "FreshTrail",
        severity: severity,
        decayPerHour: 0.15,  // Decays quickly - trail goes cold
        decaysAtCamp: true,  // Returning to camp means losing the trail
        animalType: animalType
    );

    /// <summary>
    /// Scavengers (hyenas) are waiting nearby for an opportunity.
    /// They're patient and bold when player is vulnerable.
    /// Slower decay than other threats - hyenas wait.
    /// Fire at camp deters them (faster decay at camp).
    /// </summary>
    public static ActiveTension ScavengersWaiting(double severity, Herd? sourceHerd = null) => new(
        type: "ScavengersWaiting",
        severity: severity,
        decayPerHour: 0.05,  // Very patient - slow decay
        decaysAtCamp: true,  // Fire and activity at camp deter them
        sourceHerd: sourceHerd
    );

    /// <summary>
    /// A saber-tooth tiger is stalking you.
    /// Unique threat: fire doesn't deter it, noise draws it in.
    /// Tension does NOT decay at camp - only resolves by confrontation or leaving territory.
    /// </summary>
    public static ActiveTension SaberToothStalked(double severity, Location? location = null, Herd? sourceHerd = null) => new(
        type: "SaberToothStalked",
        severity: severity,
        decayPerHour: 0.02,  // Extremely patient - very slow decay
        decaysAtCamp: false,  // Fire doesn't work! Unique threat profile.
        relevantLocation: location,
        animalType: Actors.Animals.AnimalType.SaberTooth,
        sourceHerd: sourceHerd
    );

    public static ActiveTension Custom(
        string type,
        double severity,
        double decayPerHour,
        bool decaysAtCamp,
        Location? relevantLocation = null,
        AnimalType? animalType = null,
        string? direction = null,
        string? description = null,
        Herd? sourceHerd = null) => new(
        type: type,
        severity: severity,
        decayPerHour: decayPerHour,
        decaysAtCamp: decaysAtCamp,
        relevantLocation: relevantLocation,
        animalType: animalType,
        direction: direction,
        description: description,
        sourceHerd: sourceHerd
    );
}
