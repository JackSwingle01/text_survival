using text_survival.Actions.Tensions;
using text_survival.Actors.Animals;
using text_survival.Effects;
using text_survival.UI;

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
                ])
            .Choice("Rest Now",
                "Stop before it takes hold. It costs you the hour.",
                [
                    new EventResult("You sit still and let your body fight. The chill eases a little.", 1.0, 60)
                        .Escalate("FeverRising", -0.1)
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
                        "You need to stop. Hours, not minutes.",
                        [
                            new EventResult("You curl up and shiver it out. It's not gone, but it's not winning.", 1.0, 90)
                                .WithEffects(EffectFactory.Fever(0.3))
                                .Escalate("FeverRising", -0.15)
                        ])
                    .Choice("Keep Going",
                        "There's no time to be sick.",
                        [
                            new EventResult("You keep moving. The shaking gets worse.", 1.0, 2)
                                .WithEffects(EffectFactory.Fever(0.4))
                                .Escalate("FeverRising", 0.1)
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
                        ])
                    .Choice("Lie Down",
                        "Ride it out where you are. Hope nothing finds you.",
                        [
                            new EventResult("You lose hours to sweat and shivering. When it breaks, you're weak but clear.", 1.0, 120)
                                .WithEffects(EffectFactory.Fever(0.5))
                                .Escalate("FeverRising", -0.2)
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
            GameDisplay.AddWarning(ctx, $"The blood trail is thinning. The {prey.DisplayName().ToLower()} is getting away.");

        return null;
    }

    // === DEADLY COLD TENSION ===

    private static GameEvent? DeadlyColdCreated(ActiveTension tension, GameContext ctx)
    {
        GameDisplay.AddWarning(ctx, "The cold is no longer discomfort. It's becoming dangerous. You need fire. Now.");
        ctx.player.EffectRegistry.AddEffect(EffectFactory.Fear(0.2));
        return null;
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
                    ])
                .Choice("Save Your Hands",
                    "Stop. Hands in your armpits until they burn. The cold gets more time with the rest of you.",
                    [
                        new EventResult("Feeling comes back, painful and slow. You've lost ground to the cold.", 1.0, 10)
                            .WithEffects(EffectFactory.Frostbite(0.15))
                            .Escalate("DeadlyCold", 0.1)
                    ]);
        }

        return null;
    }
}
