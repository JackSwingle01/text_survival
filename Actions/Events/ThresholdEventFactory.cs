using text_survival.Bodies;
using text_survival.Effects;

namespace text_survival.Actions.Events;

/// <summary>
/// Discrete stages for survival stat thresholds.
/// </summary>
public enum ThresholdStage
{
    Healthy = 0,    // > 50%
    Normal = 1,     // 25-50%
    Severe = 2,     // 10-25%
    Critical = 3    // < 10%
}

/// <summary>
/// Records a survival stat threshold crossing.
/// </summary>
public record ThresholdChange(
    string StatName,
    ThresholdStage? Previous,
    ThresholdStage Current
)
{
    public bool IsWorsening => Previous != null && Current > Previous;
    public bool IsImproving => Previous != null && Current < Previous;
    public bool IsInitial => Previous == null;
}

/// <summary>
/// Creates events for survival stat threshold crossings.
/// Fires once when entering crisis zones to teach players about danger levels.
/// </summary>
public static class ThresholdEventFactory
{
    /// <summary>
    /// Create an event for a threshold crossing. Returns null if no event is appropriate.
    /// </summary>
    public static GameEvent? ForThresholdChange(ThresholdChange change, GameContext ctx)
    {
        // Only fire events when entering worse stages (not improving)
        if (!change.IsWorsening)
            return null;

        // Only fire for Severe and Critical entries
        if (change.Current != ThresholdStage.Severe && change.Current != ThresholdStage.Critical)
            return null;

        return (change.StatName, change.Current) switch
        {
            // Energy (exhaustion)
            ("Energy", ThresholdStage.Severe) => EnergySevere(ctx),
            ("Energy", ThresholdStage.Critical) => EnergyCritical(ctx),

            // Calories (starvation)
            ("Calories", ThresholdStage.Severe) => CaloriesSevere(ctx),
            ("Calories", ThresholdStage.Critical) => CaloriesCritical(ctx),

            // Hydration (dehydration)
            ("Hydration", ThresholdStage.Severe) => HydrationSevere(ctx),
            ("Hydration", ThresholdStage.Critical) => HydrationCritical(ctx),

            _ => null
        };
    }

    // === ENERGY (EXHAUSTION) ===

    private static GameEvent EnergySevere(GameContext ctx)
    {
        return new GameEvent("Exhaustion Setting In",
            "Your legs are heavy. Your thoughts sluggish. " +
            "Every movement takes conscious effort. You need rest soon.",
            0)
            .Choice("Push Through",
                "You can keep going. For now.",
                [
                    new EventResult("You force yourself to continue, knowing you'll pay for it later.", 1.0, 2)
                ]);
    }

    private static GameEvent EnergyCritical(GameContext ctx)
    {
        return new GameEvent("Dangerous Exhaustion",
            "You can barely keep your eyes open. Your body screams for rest. " +
            "You're making mistakes. Dangerous mistakes.",
            0)
            .Choice("Acknowledge",
                "You need to rest. Now.",
                [
                    new EventResult("Your body is shutting down. Rest is no longer optional.", 1.0, 2)
                        .WithEffects(EffectFactory.Exhausted(0.4, 120))
                ]);
    }

    // === CALORIES (STARVATION) ===

    private static GameEvent CaloriesSevere(GameContext ctx)
    {
        return new GameEvent("Hunger Gnaws",
            "Your stomach cramps. Your hands tremble slightly. " +
            "The emptiness inside you is becoming impossible to ignore.",
            0)
            .Choice("Endure",
                "You've been hungry before.",
                [
                    new EventResult("But not like this. Your body is starting to consume itself.", 1.0, 2)
                ]);
    }

    private static GameEvent CaloriesCritical(GameContext ctx)
    {
        return new GameEvent("Starvation",
            "The world narrows. Colors seem muted. Your thoughts come slowly, " +
            "as if through deep water. Your body is eating itself to survive.",
            0)
            .Choice("Acknowledge",
                "You need food. Anything.",
                [
                    new EventResult("Without food soon, you won't have the strength to find any.", 1.0, 2)
                        .WithEffects(EffectFactory.Hungry(0.5))
                ]);
    }

    // === HYDRATION (DEHYDRATION) ===

    private static GameEvent HydrationSevere(GameContext ctx)
    {
        return new GameEvent("Thirst Building",
            "Your mouth is dry. Your lips cracked. " +
            "Every breath seems to pull more moisture from your body.",
            0)
            .Choice("Endure",
                "You need water, but you can still function.",
                [
                    new EventResult("For now. Dehydration kills faster than hunger.", 1.0, 2)
                ]);
    }

    private static GameEvent HydrationCritical(GameContext ctx)
    {
        return new GameEvent("Severe Dehydration",
            "Your head pounds. The world spins when you stand. " +
            "Your tongue feels thick and foreign in your mouth.",
            0)
            .Choice("Acknowledge",
                "Water. You need water.",
                [
                    new EventResult("Your body is shutting down. Water is life or death now.", 1.0, 2)
                        .WithEffects(EffectFactory.Thirsty(0.5))
                ]);
    }
}
