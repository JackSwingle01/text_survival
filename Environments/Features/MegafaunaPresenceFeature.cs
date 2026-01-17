using text_survival.Actions;
using text_survival.Actions.Expeditions;
using text_survival.Actions.Expeditions.WorkStrategies;
using text_survival.Actors.Animals;

namespace text_survival.Environments.Features;

/// <summary>
/// Represents the presence of megafauna (mammoth, cave bear, saber-tooth) at a location.
/// Provides multi-day hunt opportunities through scouting, tracking, and confrontation work sessions.
/// </summary>
public class MegafaunaPresenceFeature : LocationFeature, IWorkableFeature
{
    public override string? MapIcon => "danger";
    public override int IconPriority => 5;  // High priority - important gameplay feature

    /// <summary>
    /// Type of megafauna present at this location (AnimalType.Mammoth, AnimalType.CaveBear, AnimalType.SaberTooth).
    /// </summary>
    public AnimalType MegafaunaType { get; set; } = AnimalType.Mammoth;

    /// <summary>
    /// Current activity level (0-1). Affects encounter chance and event triggers.
    /// Can vary by season, time of day, or recent player actions.
    /// </summary>
    public double ActivityLevel { get; set; } = 0.7;

    /// <summary>
    /// Timestamp of last successful encounter/kill. Used for respawn timing.
    /// Null if never encountered.
    /// </summary>
    public DateTime? LastEncounterTime { get; set; }

    /// <summary>
    /// Respawn time in hours after a successful kill. Varies by megafauna type.
    /// </summary>
    public double RespawnHours { get; set; } = 720.0;  // 30 days for mammoth

    public MegafaunaPresenceFeature() : base("Megafauna Territory") { }

    public MegafaunaPresenceFeature(AnimalType megafaunaType, double activityLevel = 0.7, double respawnHours = 720.0)
        : base($"{megafaunaType.DisplayName()} Territory")
    {
        MegafaunaType = megafaunaType;
        ActivityLevel = activityLevel;
        RespawnHours = respawnHours;
    }

    /// <summary>
    /// Provides work options based on current hunt progress (tracked via MammothTracked tension).
    /// Scout → Track → Approach progression gates access to later stages.
    /// </summary>
    public IEnumerable<WorkOption> GetWorkOptions(GameContext ctx)
    {
        // Check if megafauna is available (respawn logic)
        if (LastEncounterTime.HasValue)
        {
            var hoursSinceEncounter = (DateTime.Now - LastEncounterTime.Value).TotalHours;
            if (hoursSinceEncounter < RespawnHours)
            {
                yield break;  // Not yet respawned
            }
        }

        // Get current hunt tension to determine available work options
        var huntTension = ctx.Tensions.GetTension($"{MegafaunaType.DisplayName()}Tracked");
        double tensionSeverity = huntTension?.Severity ?? 0.0;

        // Scout option - always available when no or low tension
        if (tensionSeverity < 0.5)
        {
            yield return new WorkOption(
                $"Scout for {MegafaunaType.DisplayName().ToLower()} signs",
                $"scout_{MegafaunaType.DisplayName().ToLower()}",
                new MegafaunaStrategy()
            );
        }

        // Track option - available with some tension
        if (tensionSeverity >= 0.3 && tensionSeverity < 0.6)
        {
            yield return new WorkOption(
                $"Track the {MegafaunaType.DisplayName().ToLower()}",
                $"track_{MegafaunaType.DisplayName().ToLower()}",
                new MegafaunaStrategy()
            );
        }

        // Approach option - available with high tension
        if (tensionSeverity >= 0.6)
        {
            yield return new WorkOption(
                $"Approach for confrontation",
                $"approach_{MegafaunaType.DisplayName().ToLower()}",
                new MegafaunaStrategy()
            );
        }
    }

    /// <summary>
    /// Called after successful megafauna kill to start respawn timer.
    /// </summary>
    public void RecordEncounter()
    {
        LastEncounterTime = DateTime.Now;
        ActivityLevel = 0.0;  // Reset activity after encounter
    }

    /// <summary>
    /// Returns hunt stage based on current tension severity.
    /// Used by work strategy to determine which events can trigger.
    /// </summary>
    public string GetHuntStage(GameContext ctx)
    {
        var huntTension = ctx.Tensions.GetTension($"{MegafaunaType.DisplayName()}Tracked");
        double tensionSeverity = huntTension?.Severity ?? 0.0;

        if (tensionSeverity < 0.3) return "scout";
        if (tensionSeverity < 0.6) return "track";
        return "approach";
    }

    public override List<Resource> ProvidedResources() => []; // we don't want NPCs hunting megafauna
}
