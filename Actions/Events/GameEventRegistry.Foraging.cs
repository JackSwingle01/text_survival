using text_survival.Bodies;
using text_survival.Effects;
using text_survival.Environments.Features;
using text_survival.Items;

namespace text_survival.Actions;

public static partial class GameEventRegistry
{
    // === FORAGING-SPECIFIC EVENTS ===
    // These only trigger while foraging and create meaningful mid-activity decisions.

    /// <summary>
    /// Discover a patch of mushrooms with varying risk/reward.
    /// Knowledge test: experienced players learn which are safe vs risky.
    /// </summary>
    private static GameEvent MushroomPatch(GameContext ctx)
    {
        return new GameEvent("Mushroom Patch",
            "You spot a cluster of mushrooms growing on a rotting log. Some look familiar, others have an unusual red tinge.", 1.0)
            .Requires(EventCondition.Foraging)
            .WithConditionFactor(EventCondition.IsForest, 1.5)
            .Choice("Take the Safe Ones",
                "Gather only the mushrooms you recognize.",
                [
                    new EventResult("You carefully select the familiar fungi. Birch polypore and amadou - useful medicine.", 0.70, 8)
                        .Rewards(RewardPool.MedicinalForage, 0.8),
                    new EventResult("Slim pickings. Most of these are varieties you don't recognize.", 0.30, 5)
                ])
            .Choice("Risk the Red Ones",
                "The unusual ones might be valuable... or dangerous.",
                [
                    new EventResult("Your gamble pays off. These are potent medicinal mushrooms.", 0.25, 10)
                        .Rewards(RewardPool.MedicinalForage, 1.5),
                    new EventResult("You gather them, but something feels off. Best not to eat these.", 0.35, 8),
                    new EventResult("You nibble a small piece to test. Your stomach cramps almost immediately.", 0.30, 5)
                        .WithEffects(EffectFactory.Nauseous(0.4, 60)),
                    new EventResult("Violent reaction. You spend the next hour retching.", 0.10, 60)
                        .WithEffects(EffectFactory.Nauseous(0.7, 120))
                        .DrainsStats(calories: 200)
                ])
            .Choice("Take All Carefully",
                "Spend extra time examining and sorting everything.",
                [
                    new EventResult("Patience rewarded. You identify several useful varieties and avoid the toxic ones.", 0.60, 20)
                        .Rewards(RewardPool.MedicinalForage, 1.2),
                    new EventResult("You gather what you can identify, leaving the questionable ones.", 0.30, 15)
                        .Rewards(RewardPool.MedicinalForage, 0.7),
                    new EventResult("Despite your care, you're not certain about any of them. Better safe than sorry.", 0.10, 15)
                ]);
    }

    /// <summary>
    /// Find a squirrel's winter cache. Time vs yield tradeoff.
    /// </summary>
    private static GameEvent SquirrelCache(GameContext ctx)
    {
        return new GameEvent("Squirrel Cache",
            "A squirrel chatters angrily from a branch above. Following its gaze, you spot a hollow where it's been storing food.", 0.9)
            .Requires(EventCondition.Foraging)
            .WithConditionFactor(EventCondition.IsForest, 1.8)
            .Choice("Dig It Out",
                "Take your time and get everything.",
                [
                    new EventResult("A good haul - nuts, seeds, even some dried berries the squirrel collected.", 0.65, 15)
                        .Rewards(RewardPool.SquirrelCache, 1.5),
                    new EventResult("Mostly acorns, but food is food.", 0.30, 12)
                        .Rewards(RewardPool.SquirrelCache, 0.8),
                    new EventResult("The cache goes deeper than expected. Worth the effort.", 0.05, 20)
                        .Rewards(RewardPool.SquirrelCache, 2.5)
                ])
            .Choice("Grab and Go",
                "Scoop what you can quickly. The squirrel's already angry.",
                [
                    new EventResult("You snag a handful of nuts before the squirrel gets brave.", 0.70, 3)
                        .Rewards(RewardPool.SquirrelCache, 0.5),
                    new EventResult("Quick work. Not much, but better than nothing.", 0.25, 2),
                    new EventResult("The little beast bites your hand as you reach in!", 0.05, 2)
                        .Damage(0.02, DamageType.Pierce, BodyTarget.AnyArm)
                ])
            .Choice("Leave It",
                "It's the squirrel's food. Winter's hard for everyone.",
                [
                    new EventResult("You move on. The squirrel's chatter fades behind you.", 1.0, 0)
                ]);
    }

    /// <summary>
    /// Spot a beehive. Risk/reward with preparation mattering.
    /// </summary>
    private static GameEvent BeehiveSpotted(GameContext ctx)
    {
        bool hasTorch = ctx.Inventory.HasLitTorch;
        bool hasFire = ctx.CurrentLocation.HasActiveHeatSource();
        string smokeOption = hasTorch ? "Use your torch to calm the bees."
            : hasFire ? "You could light something from the fire first."
            : "You'd need fire for this.";

        return new GameEvent("Beehive Spotted",
            "Movement catches your eye - bees entering a hollow in an old tree. Where there are bees, there's honey.", 0.7)
            .Requires(EventCondition.Foraging)
            .WithConditionFactor(EventCondition.IsForest, 1.5)
            .WithConditionFactor(EventCondition.IsDaytime, 1.3)
            .Choice("Smoke Them Out",
                smokeOption,
                hasTorch ?
                [
                    new EventResult("The smoke calms the bees. You harvest honey and beeswax without trouble.", 0.75, 15)
                        .Rewards(RewardPool.HoneyHarvest, 1.5),
                    new EventResult("Good yield. The bees are sluggish from the smoke.", 0.20, 12)
                        .Rewards(RewardPool.HoneyHarvest),
                    new EventResult("The hive is smaller than expected, but still worth the effort.", 0.05, 10)
                        .Rewards(RewardPool.HoneyHarvest, 0.6)
                ] :
                [
                    new EventResult("You have nothing to make smoke with. The bees would swarm you.", 1.0, 0)
                ])
            .Choice("Grab and Run",
                "Quick hands. Accept some stings for quick rewards.",
                [
                    new EventResult("You snatch a comb and run. Stings burn, but you got honey.", 0.50, 5)
                        .Rewards(RewardPool.HoneyHarvest, 0.7)
                        .Damage(0.08, DamageType.Pierce, BodyTarget.AnyArm)
                        .WithEffects(EffectFactory.Pain(0.2)),
                    new EventResult("The bees are angry. You escape with honey and a face full of stings.", 0.30, 3)
                        .Rewards(RewardPool.HoneyHarvest, 0.5)
                        .Damage(0.12, DamageType.Pierce, BodyTarget.Head)
                        .WithEffects(EffectFactory.Pain(0.4)),
                    new EventResult("They swarm before you can react. You flee empty-handed.", 0.15, 2)
                        .Damage(0.20, DamageType.Pierce, BodyTarget.Chest)
                        .WithEffects(EffectFactory.Pain(0.5)),
                    new EventResult("Bad reaction to the stings. Your throat tightens.", 0.05, 5)
                        .Damage(0.25, DamageType.Pierce, BodyTarget.Head)
                        .WithEffects(EffectFactory.Nauseous(0.6, 120))
                        .DrainsStats(calories: 300)
                ])
            .Choice("Leave It",
                "Note the location. Come back with proper preparation.",
                [
                    new EventResult("You memorize the tree's location. Return with fire and this will be easy.", 0.90, 2)
                        .CreateTension("MarkedDiscovery", 0.4, description: "Beehive location"),
                    new EventResult("You get too close looking for landmarks. A few guard bees find you.", 0.10, 3)
                        .Damage(0.05, DamageType.Pierce, BodyTarget.AnyArm)
                        .CreateTension("MarkedDiscovery", 0.4, description: "Beehive location")
                ]);
    }
}
