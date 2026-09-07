using text_survival.Actions.Tensions;
using text_survival.Actors.Animals;
using text_survival.Effects;

namespace text_survival.Actions.Events;

/// <summary>
/// Creates events for tension stage transitions.
/// Intentional triggers replace random polling for tension lifecycle events.
/// </summary>
public static class TensionEventFactory
{
    /// <summary>
    /// Create an event for a tension stage change. Returns null if no event is appropriate.
    /// </summary>
    public static GameEvent? ForStageChange(TensionStageChange change, GameContext ctx)
    {
        // Skip initial stage assignments (tension already had a stage when created)
        // We only want to fire events on actual transitions
        if (change.IsCreation)
        {
            // Creation events communicate WHY the tension started
            return CreateCreationEvent(change, ctx);
        }

        if (change.IsResolution)
        {
            // Resolution events are generally handled by the event that resolved them
            // But natural decay resolution can have events
            return CreateResolutionEvent(change, ctx);
        }

        // Escalation/deescalation events
        return change.TensionType switch
        {
            "FeverRising" => FeverStageEvent(change, ctx),
            "WoundedPrey" => WoundedPreyStageEvent(change, ctx),
            "DeadlyCold" => DeadlyColdStageEvent(change, ctx),
            _ => null // Other tensions don't have lifecycle events yet
        };
    }

    private static GameEvent? CreateCreationEvent(TensionStageChange change, GameContext ctx)
    {
        return change.TensionType switch
        {
            "FeverRising" => FeverCreated(change.Tension, ctx),
            "DeadlyCold" => DeadlyColdCreated(change.Tension, ctx),
            _ => null
        };
    }

    private static GameEvent? CreateResolutionEvent(TensionStageChange change, GameContext ctx)
    {
        // Most resolutions are handled by explicit event outcomes
        // Natural decay resolution events are rare - the threat just fades
        return change.TensionType switch
        {
            _ => null
        };
    }

    private static GameEvent? FeverCreated(ActiveTension tension, GameContext ctx)
    {
        return new GameEvent("Something Wrong",
            "A chill runs through you that has nothing to do with the cold. Your body is fighting something.",
            0)
            .Choice("Push Through",
                "You can handle this.",
                [
                    new EventResult("You ignore the warning signs. For now.", 1.0, 2)
                        .WithEffects(EffectFactory.Exhausted(0.15, 60))
                ]);
    }

    private static GameEvent? FeverStageEvent(TensionStageChange change, GameContext ctx)
    {
        return (change.Previous, change.Current) switch
        {
            (TensionStage.Building, TensionStage.Escalating) =>
                new GameEvent("Fever Rising",
                    "The chills are getting worse. Your hands shake. The infection is spreading.",
                    0)
                    .Choice("Rest",
                        "You need to stop.",
                        [
                            new EventResult("You curl up by the fire, shivering.", 1.0, 15)
                                .WithEffects(EffectFactory.Fever(0.3))
                        ]),

            (TensionStage.Escalating, TensionStage.Critical) =>
                new GameEvent("Fever Crisis",
                    "You're burning up. The world swims. Shadows move at the edge of your vision.",
                    0)
                    .Choice("Fight It",
                        "You have to push through.",
                        [
                            new EventResult("Everything becomes a blur of heat and cold.", 1.0, 5)
                                .WithEffects(EffectFactory.Fever(0.6))
                        ]),

            _ => null
        };
    }

    // === WOUNDED PREY TENSION ===

    private static GameEvent? WoundedPreyStageEvent(TensionStageChange change, GameContext ctx)
    {
        var tension = change.Tension;
        var prey = tension.AnimalType ?? AnimalType.Caribou;

        if (change.IsDeescalation && change.Current == TensionStage.Building)
        {
            return new GameEvent("Trail Going Cold",
                $"The blood trail is thinning. The {prey.DisplayName()} is getting away.",
                0)
                .Choice("Hurry",
                    "You need to move faster.",
                    [
                        new EventResult("You quicken your pace, following what signs remain.", 1.0, 2)
                    ]);
        }

        return null;
    }

    // === DEADLY COLD TENSION ===

    private static GameEvent? DeadlyColdCreated(ActiveTension tension, GameContext ctx)
    {
        return new GameEvent("Deadly Cold",
            "The cold is no longer discomfort. It's becoming dangerous. You need fire. Now.",
            0)
            .Choice("Acknowledge",
                "This is life or death.",
                [
                    new EventResult("Every minute matters now.", 1.0, 1)
                        .WithEffects(EffectFactory.Fear(0.2))
                ]);
    }

    private static GameEvent? DeadlyColdStageEvent(TensionStageChange change, GameContext ctx)
    {
        if (change.Current == TensionStage.Critical)
        {
            return new GameEvent("Going Numb",
                "You can't feel your fingers anymore. Your thoughts are slowing. This is how it ends if you don't find warmth.",
                0)
                .Choice("Fight",
                    "Keep moving. Keep thinking.",
                    [
                        new EventResult("Every step is an act of will.", 1.0, 2)
                            .WithEffects(EffectFactory.Frostbite(0.3), EffectFactory.Shaken(0.3))
                    ]);
        }

        return null;
    }
}
