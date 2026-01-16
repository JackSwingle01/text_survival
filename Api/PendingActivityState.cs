using System.Text.Json.Serialization;
using text_survival.Actions;
using text_survival.Actors.Animals;
using text_survival.Combat;

namespace text_survival.Api;

/// <summary>
/// Tracks pending activity that requires player input.
/// This replaces all blocking I/O patterns with explicit state tracking.
///
/// When an event/hunt/encounter/combat triggers, instead of blocking the thread
/// waiting for player input, we store the state here and return immediately.
/// The next request picks up where we left off.
/// </summary>
public class PendingActivityState
{
    /// <summary>
    /// Current phase of the pending activity.
    /// </summary>
    public ActivityPhase Phase { get; set; }

    // ============================================
    // EVENT STATE
    // ============================================

    /// <summary>
    /// The pending event awaiting player choice (serializable snapshot).
    /// </summary>
    public EventSnapshot? Event { get; set; }

    /// <summary>
    /// The original GameEvent object (not serialized).
    /// Used to process player choices. Null after save/load.
    /// </summary>
    [JsonIgnore]
    public GameEvent? EventSource { get; set; }

    /// <summary>
    /// The ID of the choice the player selected.
    /// </summary>
    public string? SelectedChoiceId { get; set; }

    /// <summary>
    /// The outcome of the event after choice was made.
    /// </summary>
    public EventOutcomeSnapshot? Outcome { get; set; }

    // ============================================
    // HUNT STATE
    // ============================================

    /// <summary>
    /// The active hunt state (snapshot for UI rendering).
    /// </summary>
    public HuntSnapshot? Hunt { get; set; }

    /// <summary>
    /// The actual animal being hunted (now serializable).
    /// </summary>
    public Animal? HuntTarget { get; set; }

    /// <summary>
    /// Index of the source herd in HerdRegistry for reference restoration.
    /// </summary>
    public int? HuntSourceHerdIndex { get; set; }

    /// <summary>
    /// Previous distance for animation (transient, not critical for save/load).
    /// </summary>
    [JsonIgnore]
    public double? HuntPreviousDistance { get; set; }

    // ============================================
    // ENCOUNTER STATE
    // ============================================

    /// <summary>
    /// The active encounter state (snapshot for UI rendering).
    /// </summary>
    public EncounterSnapshot? Encounter { get; set; }

    /// <summary>
    /// The actual predator in the encounter (now serializable).
    /// </summary>
    public Animal? EncounterPredator { get; set; }

    /// <summary>
    /// Previous distance for animation (transient, not critical for save/load).
    /// </summary>
    [JsonIgnore]
    public double? EncounterPreviousDistance { get; set; }

    // ============================================
    // COMBAT STATE
    // ============================================

    /// <summary>
    /// The active combat state (serializable snapshot for UI).
    /// </summary>
    public CombatSnapshot? Combat { get; set; }

    /// <summary>
    /// The actual combat scenario (not serialized).
    /// </summary>
    [JsonIgnore]
    public CombatScenario? CombatScenario { get; set; }

    /// <summary>
    /// The player's unit in combat (not serialized).
    /// </summary>
    [JsonIgnore]
    public Unit? CombatPlayerUnit { get; set; }

    /// <summary>
    /// Narrative message to display for the current combat phase.
    /// </summary>
    public string? CombatNarrative { get; set; }

    // ============================================
    // TRAVEL STATE
    // ============================================

    /// <summary>
    /// The pending travel state (serializable snapshot for UI).
    /// </summary>
    public TravelSnapshot? Travel { get; set; }
}

/// <summary>
/// Phases of pending activities. Determines what input is expected and what overlays to show.
/// </summary>
public enum ActivityPhase
{
    /// <summary>No pending activity.</summary>
    None,

    // Event phases
    /// <summary>Event triggered, awaiting player choice.</summary>
    EventPending,
    /// <summary>Choice made, showing outcome text.</summary>
    EventOutcomeShown,

    // Hunt phases
    /// <summary>Initial sighting - player decides whether to stalk.</summary>
    HuntSighting,
    /// <summary>Hunt is active, player can approach/throw/abandon.</summary>
    HuntActive,
    /// <summary>Hunt concluded, showing result.</summary>
    HuntResult,

    // Encounter phases
    /// <summary>Encounter is active, player must decide how to react.</summary>
    EncounterActive,
    /// <summary>Encounter outcome shown.</summary>
    EncounterOutcome,

    // Combat phases
    /// <summary>Combat intro message.</summary>
    CombatIntro,
    /// <summary>Combat active, player's turn to choose action.</summary>
    CombatPlayerTurn,
    /// <summary>Player action being shown.</summary>
    CombatPlayerAction,
    /// <summary>Animal's turn being resolved.</summary>
    CombatAnimalTurn,
    /// <summary>Combat concluded, showing result.</summary>
    CombatResult,

    // Travel phases
    /// <summary>Hazardous terrain detected, awaiting quick/careful choice.</summary>
    TravelHazardPending,
    /// <summary>Movement impaired, awaiting confirmation to proceed.</summary>
    TravelImpairmentWarning,
    /// <summary>Edge blocked, showing blocked message.</summary>
    TravelBlocked,
    /// <summary>Event occurred during travel, awaiting continue/stop choice.</summary>
    TravelInterrupted
}

// ============================================
// SNAPSHOT RECORDS - Serializable state snapshots
// ============================================

/// <summary>
/// Serializable snapshot of an event awaiting player choice.
/// </summary>
public record EventSnapshot(
    string Id,
    string Description,
    List<ChoiceSnapshot> Choices
);

/// <summary>
/// A choice available in an event.
/// </summary>
public record ChoiceSnapshot(
    string Id,
    string Text
);

/// <summary>
/// The outcome after an event choice is made.
/// </summary>
public record EventOutcomeSnapshot(
    string Description,
    bool AbortsAction
);

/// <summary>
/// Serializable snapshot of an active hunt.
/// </summary>
public record HuntSnapshot(
    string AnimalType,
    string AnimalDescription,
    string AnimalActivity,
    string AnimalState,
    double Distance,
    int Approaches,
    int MinutesSpent,
    string Phase,
    string? StatusMessage,
    List<HuntChoiceSnapshot> AvailableChoices
)
{
    /// <summary>Display name for the animal being hunted.</summary>
    public string AnimalDisplayName => AnimalType;

    /// <summary>Whether the hunt is still active (not in result phase).</summary>
    public bool IsActive => Phase != "escaped" && Phase != "killed" && Phase != "wounded" && Phase != "abandoned";
}

/// <summary>
/// A choice available during a hunt.
/// </summary>
public record HuntChoiceSnapshot(
    string Id,
    string Label,
    string? Description,
    bool IsAvailable,
    string? DisabledReason
);

/// <summary>
/// Serializable snapshot of an active encounter.
/// </summary>
public record EncounterSnapshot(
    string AnimalType,
    double Distance,
    double Boldness,
    string BoldnessDescriptor,
    List<string> AvailableActions,
    List<ThreatFactorSnapshot> ThreatFactors,
    string? StatusMessage
);

/// <summary>
/// A threat factor displayed during encounters.
/// </summary>
public record ThreatFactorSnapshot(
    string Id,
    string Label,
    string Icon
);

/// <summary>
/// Serializable snapshot of active combat.
/// </summary>
public record CombatSnapshot(
    string AnimalType,
    int Zone,
    double AnimalHealth,
    double PlayerHealth,
    string AnimalState,
    List<string> AvailableActions,
    double DistanceMeters,
    string ZoneName,
    double PlayerEnergy,
    string? Narrative
)
{
    /// <summary>Whether combat has ended.</summary>
    public bool IsOver => AnimalHealth <= 0 || PlayerHealth <= 0;
}

/// <summary>
/// Serializable snapshot of pending travel.
/// </summary>
public record TravelSnapshot(
    /// <summary>Target position X.</summary>
    int TargetX,
    /// <summary>Target position Y.</summary>
    int TargetY,
    /// <summary>Origin position X.</summary>
    int OriginX,
    /// <summary>Origin position Y.</summary>
    int OriginY,
    /// <summary>Whether terrain is hazardous.</summary>
    bool IsHazardous,
    /// <summary>Quick travel time in minutes.</summary>
    int QuickTimeMinutes,
    /// <summary>Careful travel time in minutes.</summary>
    int CarefulTimeMinutes,
    /// <summary>Injury risk for quick travel (0-1).</summary>
    double InjuryRisk,
    /// <summary>Description of the hazard type.</summary>
    string? HazardDescription,
    /// <summary>Whether player chose quick travel (set after hazard choice).</summary>
    bool? QuickTravelChosen,
    /// <summary>Message to display (blocked path, impairment warning, etc.).</summary>
    string? StatusMessage,
    /// <summary>Whether this is a first visit to the destination.</summary>
    bool IsFirstVisit
);
