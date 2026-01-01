using text_survival.Bodies;
using text_survival.Effects;

namespace text_survival.Actions;

public static partial class GameEventRegistry
{
    // === SABER-TOOTH STALKING ARC EVENTS ===
    // Unique threat profile: Fire doesn't work, noise DRAWS IT IN, tension doesn't decay at camp.
    // The only counters are unpredictability and facing it directly.

    /// <summary>
    /// Discovery event - you find signs of saber-tooth presence.
    /// Unique tension that doesn't decay at camp. Fire doesn't help.
    /// </summary>
    internal static GameEvent AncientPredator(GameContext ctx)
    {
        return new GameEvent("Ancient Predator",
            "Deep gouges on the tree bark. Higher than any wolf could reach. The claw marks are fresh. " +
            "Something from another age hunts in this territory.", 0.3)
            .Requires(EventCondition.OnExpedition)
            .RequiresSituation(Situations.InSaberToothTerritory)
            .Excludes(EventCondition.SaberToothStalked)
            .WithConditionFactor(EventCondition.LowVisibility, 1.5)
            .WithSituationFactor(Situations.AttractiveToPredators, 2.0)
            .WithCooldown(48)  // Rare discovery
            .Choice("Examine the Signs",
                "Learn what you're dealing with. Knowledge is survival.",
                [
                    new EventResult("The paw prints are enormous. Cat, but massive. A saber-tooth.", weight: 0.5, minutes: 10)
                        .BecomeSaberToothStalked(0.3)
                        .MarksDiscovery("Saber-tooth territory", 0.5),
                    new EventResult("Fresh scat. Hair in it — caribou. This predator is active, hunting.", weight: 0.3, minutes: 10)
                        .BecomeSaberToothStalked(0.4)
                        .MarksDiscovery("Saber-tooth territory", 0.5),
                    new EventResult("You find a kill site. The prey was large. Dragged away effortlessly.", weight: 0.2, minutes: 15)
                        .BecomeSaberToothStalked(0.5)
                        .Frightening()
                        .MarksDiscovery("Saber-tooth hunting ground", 0.6)
                ])
            .Choice("Mark the Location and Leave",
                "You don't want to meet what made these marks.",
                [
                    new EventResult("You note the boundary. This is its territory now.", weight: 0.7, minutes: 5)
                        .BecomeSaberToothStalked(0.2)
                        .MarksDiscovery("Saber-tooth territory edge", 0.4),
                    new EventResult("As you leave, the feeling of being watched settles over you.", weight: 0.3, minutes: 5)
                        .BecomeSaberToothStalked(0.3)
                        .Unsettling()
                ])
            .Choice("Ignore It",
                "Could be old. Could be nothing.",
                [
                    new EventResult("You continue on. But the marks stay in your mind.", weight: 0.6, minutes: 0)
                        .BecomeSaberToothStalked(0.2),
                    new EventResult("A branch snaps behind you. Probably nothing.", weight: 0.3, minutes: 0)
                        .BecomeSaberToothStalked(0.35)
                        .Unsettling(),
                    new EventResult("Ignoring it was a mistake. Something is following.", weight: 0.1, minutes: 0)
                        .BecomeSaberToothStalked(0.5)
                        .Frightening()
                ]);
    }

    /// <summary>
    /// Escalation event - the saber-tooth is actively stalking you.
    /// Key mechanic: NOISE DRAWS IT IN. "Make noise" is the WRONG choice.
    /// </summary>
    internal static GameEvent SomethingWatches(GameContext ctx)
    {
        var tension = ctx.Tensions.GetTension("SaberToothStalked");
        double severity = tension?.Severity ?? 0.3;

        var intensityDesc = severity switch
        {
            < 0.4 => "A glimpse of tawny fur. Eyes catching light in the shadows.",
            < 0.6 => "It's not hiding anymore. Moving when you move. Stopping when you stop.",
            _ => "Close now. You can hear its breathing. Waiting for you to make a mistake."
        };

        return new GameEvent("Something Watches",
            $"You feel it before you see it. {intensityDesc}", 1.5)
            .Requires(EventCondition.OnExpedition)
            .Requires(EventCondition.SaberToothStalked)
            .WithConditionFactor(EventCondition.LowVisibility, 2.0)
            .WithSituationFactor(Situations.Vulnerable, 1.5)
            .Choice("Freeze and Scan",
                "Stop. Don't move. Find it before it finds you.",
                [
                    new EventResult("There. Between the trees. Watching. It knows you've seen it.", weight: 0.4, minutes: 5)
                        .EscalatesSaberTooth(-0.1),
                    new EventResult("You spot it circling. Neither of you moves. Stalemate.", weight: 0.3, minutes: 5),
                    new EventResult("You can't find it. But you can FEEL it getting closer.", weight: 0.2, minutes: 5)
                        .EscalatesSaberTooth(0.15)
                        .Frightening(),
                    new EventResult("It's gone. For now.", weight: 0.1, minutes: 5)
                        .EscalatesSaberTooth(-0.2)
                ])
            .Choice("Make Noise",
                "Shout. Bang rocks together. Scare it off like any predator.",
                [
                    // THIS IS THE WRONG CHOICE - noise ATTRACTS this predator
                    new EventResult("The noise... it's moving TOWARD you. This isn't like wolves.", weight: 0.5, minutes: 2)
                        .EscalatesSaberTooth(0.25)
                        .Frightening(),
                    new EventResult("It freezes. Then keeps coming. Noise doesn't scare this one.", weight: 0.3, minutes: 2)
                        .EscalatesSaberTooth(0.2),
                    new EventResult("It CHARGES. Noise was prey behavior.", weight: 0.2, minutes: 1)
                        .EscalatesSaberTooth(0.35)
                        .Terrifying()
                ])
            .Choice("Move Unpredictably",
                "Don't run straight. Don't follow patterns. Confuse it.",
                [
                    new EventResult("You zigzag through the terrain. It loses your rhythm.", weight: 0.5, minutes: 10)
                        .EscalatesSaberTooth(-0.15),
                    new EventResult("Hard to track, hard to move. You tire faster than it does.", weight: 0.3, minutes: 15)
                        .EscalatesSaberTooth(-0.05)
                        .WithEffects(EffectFactory.Exhausted(0.2, 30)),
                    new EventResult("It adapts. Still there. But less certain.", weight: 0.2, minutes: 10)
                        .EscalatesSaberTooth(-0.1)
                ])
            .Choice("Return to Camp",
                "Get back to fire and shelter. Wait it out.",
                [
                    // Fire doesn't help - unique to saber-tooth
                    new EventResult("The fire means nothing to it. You see it watching from beyond the light.", weight: 0.4, minutes: 20)
                        .Unsettling(),
                    new EventResult("It doesn't follow to camp. But it will be waiting when you leave.", weight: 0.4, minutes: 20),
                    new EventResult("Even at camp, you feel those eyes. This hunter is patient.", weight: 0.2, minutes: 20)
                        .EscalatesSaberTooth(0.1)
                        .Frightening()
                ]);
    }

    /// <summary>
    /// Confrontation event - the saber-tooth attacks.
    /// Key mechanic: Running triggers the ambush. You MUST face it.
    /// </summary>
    internal static GameEvent TheAmbush(GameContext ctx)
    {
        var tension = ctx.Tensions.GetTension("SaberToothStalked");
        bool isDistracted = ctx.CurrentActivity != ActivityType.Traveling &&
                           ctx.CurrentActivity != ActivityType.Resting;
        var workingClause = isDistracted
            ? " You were focused on your task. It used the moment."
            : "";

        return new GameEvent("The Ambush",
            $"No warning. It explodes from cover — saber-teeth gleaming, claws extended.{workingClause} " +
            "Ancient instincts scream: stand and fight, or die running.", 3.0)
            .Requires(EventCondition.OnExpedition)
            .RequiresSituation(Situations.SaberToothCritical)
            .WithConditionFactor(EventCondition.LowVisibility, 2.0)
            .WithSituationFactor(Situations.Vulnerable, 2.0)
            .Choice("Brace and Face It",
                "Plant your feet. Make yourself as large as possible. Meet its eyes.",
                [
                    // THE CORRECT RESPONSE - saber-tooths respect a challenge
                    new EventResult("You don't flinch. It hesitates. A frozen moment — then it breaks off.", weight: 0.4, minutes: 3)
                        .ResolvesSaberTooth()
                        .Frightening(),
                    new EventResult("Your stance surprises it. It circles, reassessing. You've bought time.", weight: 0.3, minutes: 5)
                        .EscalatesSaberTooth(-0.3)
                        .Frightening(),
                    new EventResult("It charges anyway. But your readiness costs it the advantage.", weight: 0.2, minutes: 2)
                        .ConfrontSaberTooth(5, 0.5),
                    new EventResult("Massive. Terrifying. But it sees you won't run. Respect, predator to predator.", weight: 0.1, minutes: 5)
                        .ResolvesSaberTooth()
                        .Terrifying()
                        .MarksDiscovery("Saber-tooth confrontation site", 0.6)
                ])
            .Choice("Drop and Roll",
                "Get low. Break its line of attack. Don't be where it expects.",
                [
                    new EventResult("Its lunge passes over you. You scramble up, weapon ready.", weight: 0.4, minutes: 2)
                        .ConfrontSaberTooth(8, 0.4),
                    new EventResult("Claws rake your back as you dive. Close. Too close.", weight: 0.3, minutes: 2)
                        .Damage(0.20, DamageType.Sharp, BodyTarget.Chest)
                        .EscalatesSaberTooth(-0.2)
                        .Terrifying(),
                    new EventResult("It adjusts mid-leap. Teeth find flesh.", weight: 0.2, minutes: 2)
                        .Damage(0.40, DamageType.Sharp)
                        .EscalatesSaberTooth(-0.1)
                        .Panicking(),
                    new EventResult("Your movement confuses it. The attack becomes a pass. Now you're both standing.", weight: 0.1, minutes: 2)
                        .ConfrontSaberTooth(10, 0.35)
                ])
            .Choice("Use Terrain",
                "Put something between you. Rock. Tree. Anything solid.",
                [
                    new EventResult("A tree trunk. Its bulk can't follow through the gap.", weight: 0.4, minutes: 3)
                        .EscalatesSaberTooth(-0.25)
                        .Frightening(),
                    new EventResult("You scramble up rocky ground. It's a climber too, but slower in the rocks.", weight: 0.3, minutes: 5)
                        .EscalatesSaberTooth(-0.2)
                        .WithEffects(EffectFactory.Exhausted(0.15, 25)),
                    new EventResult("The ground gives way. You slide, but it loses you in the debris.", weight: 0.2, minutes: 5)
                        .EscalatesSaberTooth(-0.3)
                        .Damage(0.12, DamageType.Blunt),
                    new EventResult("No terrain to use. Open ground. You have to face it.", weight: 0.1, minutes: 1)
                        .ConfrontSaberTooth(6, 0.55)
                ],
                [EventCondition.HasEscapeTerrain])
            .Choice("Run",
                "Sprint. Don't look back. Prey instincts.",
                [
                    // WRONG CHOICE - running triggers the kill
                    new EventResult("Running was what it wanted. The pursuit is short.", weight: 0.6, minutes: 1)
                        .Damage(0.50, DamageType.Sharp)
                        .ConfrontSaberTooth(2, 0.8),
                    new EventResult("It brings you down. Teeth at your throat.", weight: 0.3, minutes: 1)
                        .Damage(0.60, DamageType.Sharp)
                        .Panicking()
                        .ResolvesSaberTooth(),
                    new EventResult("Somehow you get through. Bleeding, terrified, but alive.", weight: 0.1, minutes: 3)
                        .Damage(0.30, DamageType.Sharp)
                        .ResolvesSaberTooth()
                        .Panicking()
                        .Aborts()
                ]);
    }
}
