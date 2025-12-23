using text_survival.Bodies;
using text_survival.Effects;
using text_survival.Environments.Features;
using text_survival.Items;

namespace text_survival.Actions;

public static partial class GameEventRegistry
{
    // === SNARE TRAPPING EVENTS ===

    /// <summary>
    /// Something disturbed a snare while you were away.
    /// </summary>
    private static GameEvent SnareTampered(GameContext ctx)
    {
        return new GameEvent("Snare Tampered",
            "Approaching your trap line, you notice disturbed snow around one of your snares. Something was here.", 0.8)
            .Requires(EventCondition.TrapLineActive, EventCondition.OnExpedition, EventCondition.FieldWork)
            .WithConditionFactor(EventCondition.SnareBaited, 1.5)
            .WithConditionFactor(EventCondition.Night, 1.3)
            .Choice("Investigate the Tracks",
                "Study the signs to understand what happened.",
                [
                    new EventResult("Rabbit tracks. It triggered the snare but escaped. The mechanism needs resetting.", 0.35, 10),
                    new EventResult("Fox tracks circling, sniffing. It was curious but didn't take the bait.", 0.25, 8),
                    new EventResult("Larger prints. Something bigger was interested. This could attract predators.", 0.20, 10)
                        .CreateTension("Stalked", 0.2),
                    new EventResult("The snare is gone. Dragged off by something strong. You follow the drag marks and find usable parts.", 0.15, 20)
                        .Rewards(RewardPool.CraftingMaterials),
                    new EventResult("Human bootprints. Someone else knows about your trap line.", 0.05, 15)
                        .WithEffects(EffectFactory.Paranoid(0.2))
                ])
            .Choice("Reset and Move On",
                "No time to analyze. Just fix it and continue.",
                [
                    new EventResult("You reset the snare quickly and continue.", 0.70, 5),
                    new EventResult("In your haste, you cut your finger on the mechanism.", 0.20, 5)
                        .Damage(2, DamageType.Sharp, "snare"),
                    new EventResult("The snare is damaged beyond quick repair. You'll need to replace it.", 0.10, 5)
                ])
            .Choice("Leave It",
                "Whatever it was might still be nearby.",
                [
                    new EventResult("You back away carefully. The snare can wait.", 0.80, 0),
                    new EventResult("Smart instinct. You hear movement in the brush as you retreat.", 0.20, 0)
                        .CreateTension("Stalked", 0.25)
                ]);
    }

    /// <summary>
    /// A predator is investigating your trap line.
    /// </summary>
    private static GameEvent PredatorAtTrapLine(GameContext ctx)
    {
        var territory = ctx.CurrentLocation.GetFeature<AnimalTerritoryFeature>();
        var predator = territory?.GetRandomPredatorName() ?? "Wolf";

        return new GameEvent("Predator at Trap Line",
            $"You approach your snares and freeze. A {predator.ToLower()} is there, sniffing around the bait.", 1.2)
            .Requires(EventCondition.TrapLineActive, EventCondition.Stalked, EventCondition.FieldWork)
            .WithConditionFactor(EventCondition.SnareBaited, 2.0)
            .WithConditionFactor(EventCondition.SnareHasCatch, 2.5)
            .WithConditionFactor(EventCondition.StalkedHigh, 1.5)
            .Choice("Drive It Off",
                "Make noise. Assert dominance. This is YOUR trap line.",
                [
                    new EventResult($"You shout and wave your arms. The {predator.ToLower()} backs off, watching.", 0.40, 5)
                        .ResolveTension("Stalked")
                        .WithEffects(EffectFactory.Fear(0.1)),
                    new EventResult($"It startles and runs. But it knows where to find food now.", 0.30, 3)
                        .Escalate("Stalked", 0.15),
                    new EventResult($"It doesn't back down. Hackles raised, it holds its ground.", 0.20, 0)
                        .WithEffects(EffectFactory.Fear(0.3))
                        .Encounter(predator, 20, 0.5),
                    new EventResult($"It charges.", 0.10, 0)
                        .Encounter(predator, 10, 0.7)
                ])
            .Choice("Wait It Out",
                "Stay hidden. Let it take what it wants and leave.",
                [
                    new EventResult($"The {predator.ToLower()} sniffs around, then wanders off. Your catches are safe.", 0.30, 20)
                        .ResolveTension("Stalked"),
                    new EventResult($"It finds your catch and drags it away. You watch helplessly.", 0.40, 15),
                    new EventResult($"It destroys a snare trying to get at something. Then leaves.", 0.20, 15),
                    new EventResult($"It catches your scent. Turns toward your hiding spot.", 0.10, 10)
                        .WithEffects(EffectFactory.Fear(0.4))
                        .Encounter(predator, 15, 0.6)
                ])
            .Choice("Retreat",
                "Not worth the risk. Come back later.",
                [
                    new EventResult("You slip away unseen. The trap line can wait.", 0.70, 0),
                    new EventResult($"A branch snaps. The {predator.ToLower()} looks up, ears forward.", 0.30, 0)
                        .Escalate("Stalked", 0.2)
                        .WithEffects(EffectFactory.Fear(0.2))
                ]);
    }

    /// <summary>
    /// Narrative moment when finding a successful catch.
    /// Triggers when checking snares with catches.
    /// </summary>
    private static GameEvent GoodCatch(GameContext ctx)
    {
        return new GameEvent("Good Catch",
            "Approaching your snare line, you see it immediately — the loop is tight, the snare bent under weight. Something's caught.", 0.6)
            .Requires(EventCondition.TrapLineActive, EventCondition.SnareHasCatch, EventCondition.FieldWork)
            .Choice("Check Your Prize",
                "See what you've caught.",
                [
                    new EventResult("A rabbit, cleanly caught. Still warm. The snare worked perfectly.", 0.50, 0)
                        .WithEffects(EffectFactory.Focused(0.1, 30)),
                    new EventResult("A ptarmigan, feathers scattered. Not much meat but every bit counts.", 0.30, 0),
                    new EventResult("A fat rabbit. This will make a proper meal.", 0.15, 0)
                        .WithEffects(EffectFactory.Focused(0.15, 60)),
                    new EventResult("A fox. Valuable fur, and some meat. A good day's trapping.", 0.05, 0)
                        .WithEffects(EffectFactory.Focused(0.2, 60))
                ])
            .Choice("Approach Carefully",
                "Something might be watching. Check for danger first.",
                [
                    new EventResult("Clear. You collect your catch without incident.", 0.75, 5),
                    new EventResult("Smart. Tracks around the snare — something was circling. Already gone.", 0.20, 8)
                        .CreateTension("Stalked", 0.15),
                    new EventResult("A scavenger was nearby, waiting for you to leave. It flees as you approach.", 0.05, 5)
                ]);
    }

    /// <summary>
    /// Scavengers have gotten to your catches.
    /// </summary>
    private static GameEvent TrapLinePlundered(GameContext ctx)
    {
        return new GameEvent("Trap Line Plundered",
            "Your snares have been hit. The snow is churned up, feathers and fur scattered. Something got here first.", 0.7)
            .Requires(EventCondition.TrapLineActive, EventCondition.OnExpedition)
            .WithConditionFactor(EventCondition.SnareHasCatch, 0.3) // Less likely if catch still there
            .WithConditionFactor(EventCondition.Night, 1.5)
            .Choice("Salvage What You Can",
                "Maybe there's something left.",
                [
                    new EventResult("Bones and scraps. Enough for tools, at least.", 0.50, 10)
                        .Rewards(RewardPool.BoneHarvest),
                    new EventResult("One snare destroyed, but the other still has its catch.", 0.25, 10),
                    new EventResult("Picked clean. Nothing left but tracks.", 0.20, 8),
                    new EventResult("Blood trail leads away. Something large was here recently.", 0.05, 5)
                        .CreateTension("Stalked", 0.3)
                ])
            .Choice("Track the Scavenger",
                "Follow the trail. Maybe you can recover something.",
                [
                    new EventResult("You follow the tracks but lose them in rocky ground.", 0.40, 20),
                    new EventResult("You find where it cached the remains. Some meat still good.", 0.30, 25)
                        .Rewards(RewardPool.SmallGame),
                    new EventResult("You find the scavenger — a fox, still eating. Easy target.", 0.20, 25)
                        .Rewards(RewardPool.SmallGame),
                    new EventResult("You find the 'scavenger' — a wolf. It sees you too.", 0.10, 15)
                        .WithEffects(EffectFactory.Fear(0.3))
                        .Encounter("wolf", 20, 0.4)
                ],
                requires: [EventCondition.IsDaytime])
            .Choice("Reset and Accept the Loss",
                "Part of trapping. Reset and hope for better luck.",
                [
                    new EventResult("You reset what's salvageable. Tomorrow's another day.", 0.85, 10),
                    new EventResult("The damage is worse than you thought. You'll need new snares.", 0.15, 10)
                ]);
    }

    /// <summary>
    /// Injury while setting or checking traps.
    /// </summary>
    private static GameEvent TrappingAccident(GameContext ctx)
    {
        return new GameEvent("Trap Snaps",
            "The snare mechanism releases unexpectedly. Pain shoots through your hand.", 0.3)
            .Requires(EventCondition.TrapLineActive, EventCondition.FieldWork)
            .WithConditionFactor(EventCondition.Clumsy, 3.0)
            .WithConditionFactor(EventCondition.Foggy, 2.0)
            .WithConditionFactor(EventCondition.ExtremelyCold, 1.5)
            .Choice("Check the Damage",
                "See how bad it is.",
                [
                    new EventResult("Just a pinch. Hurts but no real damage.", 0.50, 2)
                        .WithEffects(EffectFactory.Pain(0.1)),
                    new EventResult("Deep bruise forming. Your grip will be weak for a while.", 0.30, 5)
                        .Damage(3, DamageType.Blunt, "snare mechanism"),
                    new EventResult("It caught your finger badly. Bleeding, swelling fast.", 0.15, 5)
                        .Damage(5, DamageType.Sharp, "snare mechanism")
                        .WithEffects(EffectFactory.Bleeding(0.2)),
                    new EventResult("Something snapped. Not the snare — your finger.", 0.05, 5)
                        .Damage(10, DamageType.Blunt, "snare mechanism")
                        .WithEffects(EffectFactory.Pain(0.5))
                ])
            .Choice("Shake It Off",
                "Keep working. You've had worse.",
                [
                    new EventResult("You flex your hand and continue. It'll bruise.", 0.70, 0)
                        .Damage(2, DamageType.Blunt, "snare mechanism"),
                    new EventResult("Pushing through makes it worse. Should have stopped.", 0.30, 0)
                        .Damage(5, DamageType.Blunt, "snare mechanism")
                        .WithEffects(EffectFactory.Pain(0.2))
                ]);
    }

    /// <summary>
    /// Baited snare attracts something you didn't want.
    /// </summary>
    private static GameEvent BaitedTrapAttention(GameContext ctx)
    {
        var territory = ctx.CurrentLocation.GetFeature<AnimalTerritoryFeature>();
        var predator = territory?.GetRandomPredatorName() ?? "wolf";

        return new GameEvent("Unwanted Attention",
            $"The meat bait on your snare has attracted attention. {predator.ToUpper()} tracks circle the trap.", 0.5)
            .Requires(EventCondition.TrapLineActive, EventCondition.SnareBaited, EventCondition.FieldWork)
            .WithConditionFactor(EventCondition.Night, 1.5)
            .WithConditionFactor(EventCondition.Stalked, 2.0)
            .Choice("Remove the Bait",
                "It's attracting the wrong things. Take it back.",
                [
                    new EventResult("You retrieve the bait. The snare is less effective now, but safer.", 0.80, 5)
                        .Rewards(RewardPool.BasicMeat),
                    new EventResult("The bait is spoiled. Not worth keeping.", 0.20, 5)
                ])
            .Choice("Leave It",
                "Maybe it'll catch something good.",
                [
                    new EventResult("You decide the risk is worth it. For now.", 0.60, 0)
                        .Escalate("Stalked", 0.1),
                    new EventResult("As you debate, you hear something moving nearby.", 0.30, 0)
                        .CreateTension("Stalked", 0.25),
                    new EventResult($"Too late to decide. A {predator.ToLower()} emerges from cover.", 0.10, 0)
                        .Encounter(predator, 25, 0.4)
                ])
            .Choice("Set Up an Ambush",
                "If something's coming, be ready for it.",
                [
                    new EventResult("You wait. Nothing comes. Time wasted.", 0.50, 30),
                    new EventResult("A fox approaches the bait. Easy shot.", 0.30, 25)
                        .Rewards(RewardPool.SmallGame),
                    new EventResult($"A {predator.ToLower()} approaches. You have the advantage.", 0.15, 20)
                        .Encounter(predator, 30, 0.3),
                    new EventResult("You wait so long you start to freeze.", 0.05, 45)
                        .WithEffects(EffectFactory.Cold(-15, 60))
                ],
                requires: [EventCondition.HasShelter]); // Need cover for ambush
    }
}
