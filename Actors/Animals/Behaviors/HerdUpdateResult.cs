using text_survival.Actors.Animals;
using text_survival.Environments.Grid;

namespace text_survival.Actors.Animals.Behaviors;

/// <summary>
/// Result of a herd behavior update. Contains any side effects that need processing.
/// </summary>
public record HerdUpdateResult
{
    /// <summary>New position if the herd moved.</summary>
    public GridPosition? NewPosition { get; init; }

    /// <summary>Request for a predator encounter with the player.</summary>
    public HerdEncounterRequest? EncounterRequest { get; init; }

    /// <summary>Narrative message to show the player if they can see it.</summary>
    public string? NarrativeMessage { get; init; }

    /// <summary>Empty result - no side effects.</summary>
    public static HerdUpdateResult None => new();

    /// <summary>Create result requesting a predator encounter.</summary>
    public static HerdUpdateResult WithEncounter(Herd herd, bool isDefending = false) =>
        new() { EncounterRequest = new HerdEncounterRequest(herd, isDefending) };

    /// <summary>Create result with a narrative message.</summary>
    public static HerdUpdateResult WithNarrative(string message) =>
        new() { NarrativeMessage = message };

}

/// <summary>
/// Request for a predator encounter with the player.
/// </summary>
/// <param name="Herd">The predator herd initiating the encounter.</param>
/// <param name="IsDefendingKill">Whether the predator is defending a kill (more aggressive).</param>
public record HerdEncounterRequest(Herd Herd, bool IsDefendingKill);
