namespace text_survival.Actions;

public static class ActivityConfig
{
    public record Config(double EventMultiplier, double ActivityLevel, double FireProximity, string StatusText);

    private static readonly Dictionary<ActivityType, Config> _configs = new()
    {
        // No events
        [ActivityType.Idle] = new(0.0, 1.0, 2.0, "Thinking."),
        [ActivityType.Fighting] = new(0.0, 2.0, 0.0, "Fighting."),
        [ActivityType.Encounter] = new(0.0, 1.5, 0.0, "Alert."),

        // Camp activities (near fire, moderate events)
        [ActivityType.Sleeping] = new(0.1, 0.5, 2.0, "Sleeping."),
        [ActivityType.Resting] = new(0.3, 1.0, 2.0, "Resting."),
        [ActivityType.Incapacitated] = new(0.3, 0.8, 2.0, "Incapacitated."),
        [ActivityType.TendingFire] = new(0.5, 1.0, 2.0, "Tending fire."),
        [ActivityType.Eating] = new(0.5, 1.0, 2.0, "Eating."),
        [ActivityType.Cooking] = new(0.5, 1.0, 2.0, "Cooking."),
        // Crafting sat at 0.5 while every other camp activity got 2.0, which meant knapping a
        // blade beside your own fire warmed you a quarter as much as eating beside it. Nothing
        // about crafting moves you away from the fire, and the penalty was invisible - it read
        // as a bug rather than a tradeoff, so it is one now.
        [ActivityType.Crafting] = new(0.5, 1.0, 2.0, "Crafting."),

        // Expedition activities (away from fire, full events)
        [ActivityType.Traveling] = new(1.0, 1.5, 0.0, "Traveling."),
        [ActivityType.Foraging] = new(1.0, 1.5, 0.0, "Foraging."),
        [ActivityType.Hunting] = new(1.0, 1.5, 0.0, "Hunting."),
        [ActivityType.Exploring] = new(1.0, 1.5, 0.0, "Exploring."),
        [ActivityType.Chopping] = new(1.0, 1.8, 0.0, "Chopping wood."),
        [ActivityType.Tracking] = new(1.0, 1.5, 0.0, "Tracking."),
        [ActivityType.Butchering] = new(1.0, 1.5, 0.0, "Butchering."),
    };

    public static Config Get(ActivityType activity) => _configs[activity];

    /// <summary>
    /// Whether an activity keeps you put long enough for a structural shelter to help. This
    /// is the single source of truth for that question - <see cref="Environments.Location"/>
    /// derives its temperature from it rather than taking a bool from each caller, because
    /// callers that had to decide for themselves got it wrong silently.
    /// </summary>
    public static bool IsStationary(ActivityType activity) => activity switch
    {
        // Stationary activities - shelter applies
        ActivityType.Idle => true,
        ActivityType.Fighting => true,
        ActivityType.Encounter => true,
        ActivityType.Sleeping => true,
        ActivityType.Resting => true,
        ActivityType.Incapacitated => true,
        ActivityType.TendingFire => true,
        ActivityType.Eating => true,
        ActivityType.Cooking => true,
        ActivityType.Crafting => true,

        // Moving activities - no structural shelter
        ActivityType.Traveling => false,
        ActivityType.Foraging => false,
        ActivityType.Hunting => false,
        ActivityType.Exploring => false,
        ActivityType.Chopping => false,
        ActivityType.Tracking => false,
        ActivityType.Butchering => false,

        _ => throw new ArgumentOutOfRangeException(nameof(activity), activity,
            "New activity types must say whether shelter applies to them."),
    };
}
