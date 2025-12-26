namespace text_survival.Actions;

/// <summary>
/// Configuration for activity types that defines:
/// - Event multiplier (0 = no events, 1.0 = full event rate)
/// - Activity level (metabolic multiplier for survival calculations)
/// - Fire proximity (heat bonus multiplier when near fire)
/// - Status text (displayed during activity)
/// </summary>
public static class ActivityConfig
{
    /// <summary>
    /// Activity configuration tuple: (EventMultiplier, ActivityLevel, FireProximity, StatusText)
    /// </summary>
    public record Config(double EventMultiplier, double ActivityLevel, double FireProximity, string StatusText);

    private static readonly Dictionary<ActivityType, Config> _configs = new()
    {
        // No events
        [ActivityType.Idle] = new(0.0, 1.0, 0.0, "Thinking."),
        [ActivityType.Fighting] = new(0.0, 2.0, 0.0, "Fighting."),
        [ActivityType.Encounter] = new(0.0, 1.5, 0.0, "Alert."),

        // Camp activities (near fire, moderate events)
        [ActivityType.Sleeping] = new(0.1, 0.5, 2.0, "Sleeping."),
        [ActivityType.Resting] = new(0.3, 1.0, 2.0, "Resting."),
        [ActivityType.TendingFire] = new(0.5, 1.0, 2.0, "Tending fire."),
        [ActivityType.Eating] = new(0.5, 1.0, 2.0, "Eating."),
        [ActivityType.Cooking] = new(0.5, 1.0, 2.0, "Cooking."),
        [ActivityType.Crafting] = new(0.5, 1.0, 0.5, "Crafting."),

        // Expedition activities (away from fire, full events)
        [ActivityType.Traveling] = new(1.0, 1.5, 0.0, "Traveling."),
        [ActivityType.Foraging] = new(1.0, 1.5, 0.0, "Foraging."),
        [ActivityType.Hunting] = new(1.0, 1.5, 0.0, "Hunting."),
        [ActivityType.Exploring] = new(1.0, 1.5, 0.0, "Exploring."),
        [ActivityType.Chopping] = new(1.0, 1.8, 0.0, "Chopping wood."),
    };

    /// <summary>
    /// Get configuration for an activity type.
    /// </summary>
    public static Config Get(ActivityType activity) => _configs[activity];
}
