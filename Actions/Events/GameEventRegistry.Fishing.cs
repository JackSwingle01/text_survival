using text_survival.Actors.Animals;
using text_survival.Bodies;
using text_survival.Effects;
using text_survival.Environments.Features;
using text_survival.Items;

namespace text_survival.Actions;

public static partial class GameEventRegistry
{
    // === FISHING EVENTS ===

    /// <summary>
    /// Ice gives way while fishing on thin ice without an ice hole.
    /// Major hazard event with multiple resolution paths.
    /// </summary>
    private static GameEvent IceGivesWay(GameContext ctx)
    {
        return new GameEvent("Ice Gives Way",
            "A crack shoots across the ice beneath you. The surface groans and shifts. " +
            "You're on thin ice — literally.",
            1.0)
            .Requires(EventCondition.OnThinIce, EventCondition.FieldWork)
            .WithSituationFactor(Situations.FishingOnThinIce, 2.0)
            .WithSituationFactor(Situations.CognitivelyImpaired, 2.0)
            .WithSituationFactor(Situations.HarshConditions, 1.5)
            .Choice("Spread Your Weight",
                "Drop flat. Distribute the load across more ice.",
                [
                    new EventResult("You ease down carefully. The cracking stops. You crawl to safer ice.", 0.60, 10),
                    new EventResult("The ice stabilizes. You inch back, heart pounding.", 0.25, 15)
                        .Shaken(),
                    new EventResult("Too late — the ice breaks. You plunge into freezing water.", 0.15, 5)
                        .FellThroughIce()
                ])
            .Choice("Jump Back",
                "Move fast. Get to thicker ice before it breaks.",
                [
                    new EventResult("You leap backward. The ice where you stood crashes into dark water.", 0.50, 5)
                        .Shaken(),
                    new EventResult("You make it — barely. Your gear scatters across the ice.", 0.25, 8)
                        .Frightening(),
                    new EventResult("Your foot punches through. You scramble back, one leg soaked.", 0.15, 5)
                        .WithEffects(EffectFactory.Wet(0.5), EffectFactory.Cold(-8, 45)),
                    new EventResult("You slip and fall through. The cold hits like a hammer.", 0.10, 3)
                        .FellThroughIce()
                ])
            .Choice("Stay Perfectly Still",
                "Don't move. Let the ice settle.",
                [
                    new EventResult("You hold your breath. The cracking stops. Minutes pass. Finally, you edge away.", 0.70, 20),
                    new EventResult("The ice groans but holds. You wait until you're sure, then retreat.", 0.25, 25)
                        .WithEffects(EffectFactory.Cold(-5, 30)),
                    new EventResult("You waited too long. The ice gives way beneath you.", 0.05, 15)
                        .FellThroughIce()
                ]);
    }

    /// <summary>
    /// A bear approaches the fishing hole, attracted by fish smell.
    /// </summary>
    private static GameEvent BearAtFishingHole(GameContext ctx)
    {
        return new GameEvent("Bear at Fishing Hole",
            "Movement at the tree line. A bear emerges, nose raised, sniffing the air. " +
            "It's caught the scent of fish — and it's coming this way.",
            0.8)
            .Requires(EventCondition.HasIceHole, EventCondition.FieldWork, EventCondition.InAnimalTerritory)
            .WithSituationFactor(Situations.FishingAtIceHole, 2.0)
            .WithConditionFactor(EventCondition.HasMeat, 2.5) // Fish counts as meat for attraction
            .WithSituationFactor(Situations.AttractiveToPredators, 1.5)
            .Choice("Back Away Slowly",
                "No sudden movements. Give it space.",
                [
                    new EventResult("You retreat step by step. The bear claims the fishing hole and ignores you.", 0.60, 15),
                    new EventResult("It watches you go, then returns to sniffing the ice. Your catch is safe.", 0.25, 20),
                    new EventResult("It follows your movement. You keep backing up until it loses interest.", 0.10, 25)
                        .Frightening(),
                    new EventResult("It charges. You barely make it to cover.", 0.05, 5)
                        .Encounter(AnimalType.Bear, 20, 0.6)
                ])
            .Choice("Make Noise",
                "Assert yourself. Scare it off.",
                [
                    new EventResult("You shout and wave. The bear startles and lumbers away.", 0.40, 5)
                        .CreateTension("ClaimedTerritory", 0.3, animalType: AnimalType.Bear),
                    new EventResult("It hesitates, then retreats. But it knows you're here now.", 0.30, 8)
                        .CreateTension("ClaimedTerritory", 0.5, animalType: AnimalType.Bear),
                    new EventResult("It doesn't scare. It stands up, assessing you.", 0.20, 3)
                        .Terrifying()
                        .Encounter(AnimalType.Bear, 25, 0.5),
                    new EventResult("Wrong move. It charges.", 0.10, 0)
                        .Encounter(AnimalType.Bear, 15, 0.8)
                ])
            .Choice("Drop Fish and Run",
                "Give it what it wants. Get out alive.",
                [
                    new EventResult("You drop your catch and back away fast. The bear goes for the fish, not you.", 0.90, 10)
                        .LosesFish(),
                    new EventResult("It takes the fish but still seems interested in you. You keep moving.", 0.08, 15)
                        .LosesFish()
                        .BecomeStalked(0.3, AnimalType.Bear),
                    new EventResult("It ignores the fish and comes for you.", 0.02, 5)
                        .Encounter(AnimalType.Bear, 20, 0.7)
                ]);
    }

    /// <summary>
    /// Wolves circling while checking nets, creating a tense standoff.
    /// </summary>
    private static GameEvent WolvesCirclingNets(GameContext ctx)
    {
        bool hasFire = ctx.CurrentLocation.HasActiveHeatSource() || ctx.Inventory.HasLitTorch;
        string fireOption = hasFire ? "Use fire to drive them off." : "You'd need fire for this.";

        return new GameEvent("Wolves at the Nets",
            "They appear at the edge of the ice — three, maybe four wolves. " +
            "They've been watching you work. Now they're circling closer.",
            0.9)
            .Requires(EventCondition.FieldWork)
            .WithSituationFactor(Situations.CheckingNets, 1.5)
            .WithConditionFactor(EventCondition.Stalked, 2.0)
            .WithConditionFactor(EventCondition.PackNearby, 2.0)
            .WithConditionFactor(EventCondition.HasMeat, 2.0)
            .WithSituationFactor(Situations.Vulnerable, 1.5)
            .Choice("Fire Drives Them Off",
                fireOption,
                hasFire ?
                [
                    new EventResult("You wave the torch. The wolves retreat into the trees.", 0.70, 10)
                        .BurnsFuel(1)
                        .ResolvesStalking(),
                    new EventResult("They back off but don't leave. Watching, waiting.", 0.25, 15)
                        .BurnsFuel(1),
                    new EventResult("One is braver than the others. It snaps at you before retreating.", 0.05, 8)
                        .BurnsFuel(1)
                        .EscalatesStalking(0.2)
                ] :
                [
                    new EventResult("You have nothing to threaten them with. The standoff continues.", 1.0, 0)
                        .EscalatesStalking(0.15)
                ])
            .Choice("Wait Them Out",
                "Stay still. They might lose interest.",
                [
                    new EventResult("An hour passes. They finally drift away, seeking easier prey.", 0.40, 60)
                        .HarshCold()
                        .ResolvesStalking(),
                    new EventResult("They settle in to wait. This could take all day.", 0.35, 90)
                        .HarshCold()
                        .EscalatesStalking(0.1),
                    new EventResult("One tests you, coming close. The others watch to see what you do.", 0.20, 45)
                        .HarshCold()
                        .EscalatesStalking(0.25),
                    new EventResult("They grow bolder. This isn't working.", 0.05, 30)
                        .Encounter(AnimalType.Wolf, 25, 0.4)
                ])
            .Choice("Abandon the Nets",
                "The catch isn't worth your life.",
                [
                    new EventResult("You back away carefully. The wolves investigate the nets as you leave.", 0.85, 5)
                        .LosesFish(),
                    new EventResult("They let you go. One follows for a while, then gives up.", 0.12, 10)
                        .LosesFish()
                        .BecomeStalked(0.2),
                    new EventResult("They cut you off. You have to go through them.", 0.03, 3)
                        .Encounter(AnimalType.Wolf, 20, 0.5)
                ]);
    }

    /// <summary>
    /// Fumbling on ice due to impairment — risk of losing catch or falling.
    /// </summary>
    private static GameEvent FumbleOnIce(GameContext ctx)
    {
        return new GameEvent("Fumble on Ice",
            "Your numb hands slip. The fish — or is it your tool? — starts to slide across the ice toward the hole.",
            0.5)
            .Requires(EventCondition.FieldWork, EventCondition.FrozenWater)
            .WithSituationFactor(Situations.Fishing, 2.0)
            .WithSituationFactor(Situations.CognitivelyImpaired, 2.5)
            .WithConditionFactor(EventCondition.Clumsy, 2.0)
            .WithSituationFactor(Situations.ExtremeColdCrisis, 1.5)
            .Choice("Catch It",
                "Lunge for it before it's gone.",
                [
                    new EventResult("You snag it just in time. Crisis averted.", 0.50, 2),
                    new EventResult("Got it — but you slip in the process. Your knee hits the ice hard.", 0.25, 3)
                        .Damage(0.08, DamageType.Blunt, BodyTarget.AnyLeg)
                        .WithEffects(EffectFactory.Pain(0.15)),
                    new EventResult("You grab for it and your hand goes through the ice hole. Freezing.", 0.15, 5)
                        .WithEffects(EffectFactory.Wet(0.3), EffectFactory.Cold(-6, 30)),
                    new EventResult("You lunge, slip, and crash down. Something cracks — ice or bone?", 0.10, 5)
                        .Damage(0.15, DamageType.Blunt, BodyTarget.AnyArm)
                        .WithEffects(EffectFactory.Pain(0.3))
                ])
            .Choice("Let It Go",
                "Not worth the risk.",
                [
                    new EventResult("You watch it slide into the dark water. Gone.", 0.70, 0)
                        .LosesFish(),
                    new EventResult("It stops at the edge. You retrieve it carefully.", 0.30, 5)
                ])
            .Choice("Check Your Gear",
                "Take a moment. Get your bearings.",
                [
                    new EventResult("You steady yourself. Your hands are worse than you thought — time to warm up.", 0.80, 10)
                        .WithEffects(EffectFactory.Focused(0.15, 30)),
                    new EventResult("The cold has sapped your coordination. This is getting dangerous.", 0.20, 8)
                        .WithEffects(EffectFactory.Cold(-4, 20))
                ]);
    }

    /// <summary>
    /// Lucky catch — abundant fish creates opportunity for bigger haul.
    /// </summary>
    private static GameEvent LuckyCatch(GameContext ctx)
    {
        bool hasRod = ctx.Inventory.GetTool(ToolType.FishingRod)?.Works == true;
        var water = ctx.CurrentLocation.GetFeature<WaterFeature>();
        double abundance = water?.FishAbundance ?? 0.5;

        return new GameEvent("Lucky Catch",
            "The water churns with activity. Fish are running — more than you've seen in weeks. " +
            "This is the moment to capitalize.",
            0.6)
            .Requires(EventCondition.FieldWork)
            .WithSituationFactor(Situations.Fishing, 2.0)
            .WithConditionFactor(EventCondition.HasFuel, 1.2) // Prepared fishermen rewarded
            .Choice("Fish Fast",
                "Work quickly while they're running. Push your limits.",
                hasRod ?
                [
                    new EventResult("Your rod bends again and again. Three good fish in quick succession.", 0.45, 20)
                        .Rewards(RewardPool.SmallGame)
                        .Rewards(RewardPool.SmallGame)
                        .Rewards(RewardPool.SmallGame),
                    new EventResult("An incredible run. Four fish, maybe more. Your arms burn from the effort.", 0.25, 25)
                        .Rewards(RewardPool.SmallGame)
                        .Rewards(RewardPool.SmallGame)
                        .Rewards(RewardPool.SmallGame)
                        .Rewards(RewardPool.SmallGame)
                        .WithEffects(EffectFactory.Exhausted(0.25)),
                    new EventResult("Good catch, but you pushed too hard. Exhausted.", 0.20, 30)
                        .Rewards(RewardPool.SmallGame)
                        .Rewards(RewardPool.SmallGame)
                        .WithEffects(EffectFactory.Exhausted(0.4)),
                    new EventResult("The line snaps. You lose the fish AND damage your rod.", 0.10, 15)
                        .DamagesTool(ToolType.FishingRod, 5)
                ] :
                [
                    new EventResult("Without proper gear, you do what you can. Two fish is still good.", 0.60, 25)
                        .Rewards(RewardPool.SmallGame)
                        .Rewards(RewardPool.SmallGame),
                    new EventResult("You miss more than you catch. Still, one good fish.", 0.30, 20)
                        .Rewards(RewardPool.SmallGame),
                    new EventResult("The fish are too fast. You grab at shadows.", 0.10, 15)
                ])
            .Choice("Take What You Can Carry",
                "Don't get greedy. Secure what you can.",
                [
                    new EventResult("Two solid catches. No risks, no regrets.", 0.70, 15)
                        .Rewards(RewardPool.SmallGame)
                        .Rewards(RewardPool.SmallGame),
                    new EventResult("One good fish, properly handled. Quality over quantity.", 0.25, 12)
                        .Rewards(RewardPool.SmallGame),
                    new EventResult("The run ends before you're ready. Still, one fish is better than none.", 0.05, 10)
                ])
            .Choice("Watch and Learn",
                "Study the patterns. This knowledge is valuable.",
                [
                    new EventResult("You note the timing, the currents, the feeding patterns. Next time you'll be ready.", 0.70, 10)
                        .WithEffects(EffectFactory.Focused(0.2, 60)),
                    new EventResult("The fish move in predictable ways. You understand this water better now.", 0.30, 15)
                        .WithEffects(EffectFactory.Focused(0.15, 45))
                ]);
    }
}

// Extension methods for fishing-specific outcome templates
public static class FishingOutcomeTemplates
{
    /// <summary>
    /// Player loses their caught fish (dropped, taken by predator, etc.)
    /// Costs a fish from inventory.
    /// </summary>
    public static EventResult LosesFish(this EventResult result)
    {
        // Cost 1 fish from inventory if player has any
        return result.Costs(ResourceType.Food, 1);
    }
}
