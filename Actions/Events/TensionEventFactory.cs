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
            "Stalked" => StalkedStageEvent(change, ctx),
            "Hunted" => HuntedStageEvent(change, ctx),
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
            "Stalked" => StalkedCreated(change.Tension, ctx),
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
            "Stalked" when change.Previous == TensionStage.Building =>
                StalkedFadedAway(change.Tension, ctx),
            _ => null
        };
    }

    // === STALKED TENSION ===

    private static GameEvent? StalkedCreated(ActiveTension tension, GameContext ctx)
    {
        var predator = tension.AnimalType ?? AnimalType.Wolf;
        var reason = GetStalkedReason(ctx);

        return new GameEvent("Something's Attention",
            $"The hair on your neck rises. {reason} Something has noticed you.",
            0)  // Weight 0 - this is an intentional trigger, not random
            .Choice("Stay Alert",
                "You're being watched. Act accordingly.",
                [
                    new EventResult("You keep your guard up. Whatever it is hasn't committed yet.", 1.0, 2)
                        .WithEffects(EffectFactory.Paranoid(0.1))
                ]);
    }

    private static string GetStalkedReason(GameContext ctx)
    {
        if (ctx.Inventory.HasMeat)
            return "The smell of meat carries on the wind.";
        if (ctx.Check(EventCondition.PlayerBloody))
            return "Blood drips from your wound.";
        if (ctx.Check(EventCondition.HasStrongScent))
            return "The scent of the kill clings to you.";
        return "Movement in the trees. Eyes catching light.";
    }

    private static GameEvent? StalkedStageEvent(TensionStageChange change, GameContext ctx)
    {
        var tension = change.Tension;
        var predator = tension.AnimalType ?? AnimalType.Wolf;

        return (change.Previous, change.Current) switch
        {
            // Building -> Escalating: It's getting closer
            (TensionStage.Building, TensionStage.Escalating) =>
                new GameEvent("Closing In",
                    $"Movement parallels your path. The {predator.DisplayName()} is getting bolder.",
                    0)
                    .Choice("Acknowledge",
                        "You see it now, keeping pace through the brush.",
                        [
                            new EventResult("It knows you know. The game has changed.", 1.0, 2)
                                .Unsettling()
                        ]),

            // Escalating -> Critical: Confrontation imminent
            (TensionStage.Escalating, TensionStage.Critical) =>
                new GameEvent("Eyes in the Dark",
                    $"Eyes reflect in the firelight. The {predator.DisplayName()} is close now. Too close.",
                    0)
                    .Choice("Face It",
                        "This ends now.",
                        [
                            new EventResult($"You turn to face the {predator.DisplayName()}. It doesn't back down.", 1.0, 3)
                                .Frightening()
                        ]),

            // Deescalation events (rare - usually resolved by action)
            (TensionStage.Critical, TensionStage.Escalating) =>
                new GameEvent("Backing Off",
                    $"The {predator.DisplayName()} retreats slightly. Something gave it pause.",
                    0)
                    .Choice("Keep Pressure",
                        "Don't let up.",
                        [
                            new EventResult("It's reconsidering. Good.", 1.0, 2)
                        ]),

            _ => null
        };
    }

    private static GameEvent? StalkedFadedAway(ActiveTension tension, GameContext ctx)
    {
        var predator = tension.AnimalType ?? AnimalType.Wolf;

        return new GameEvent("Lost Interest",
            $"The feeling of being watched fades. Whatever was following you has moved on.",
            0)
            .Choice("Continue",
                "You're alone again. Probably.",
                [
                    new EventResult("The tension drains from your shoulders.", 1.0, 2)
                ]);
    }

    // === HUNTED TENSION ===

    private static GameEvent? HuntedStageEvent(TensionStageChange change, GameContext ctx)
    {
        var tension = change.Tension;
        var predator = tension.AnimalType ?? AnimalType.Wolf;

        if (change.Current == TensionStage.Critical && change.Previous != TensionStage.Critical)
        {
            return new GameEvent("The Hunt",
                $"The {predator.DisplayName()} is committed now. It's hunting you.",
                0)
                .Choice("Brace Yourself",
                    "It's coming.",
                    [
                        new EventResult("You hear it accelerating through the brush.", 1.0, 2)
                            .Terrifying()
                    ]);
        }

        return null;
    }

    // === FEVER TENSION ===

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
