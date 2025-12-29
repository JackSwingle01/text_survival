using text_survival.Actions.Variants;
using text_survival.Bodies;
using text_survival.Effects;
using text_survival.Environments.Features;
using text_survival.Items;

namespace text_survival.Actions;

/// <summary>
/// Small game sighting events - quick opportunity moments that reward prepared players.
/// These create decisions about whether to pursue small game for food,
/// with success influenced by weapon readiness and stealth conditions.
/// </summary>
public static partial class GameEventRegistry
{
    private static GameEvent RabbitFreeze(GameContext ctx)
    {
        bool hasWeapon = ctx.Inventory.HasWeapon;
        bool goodStealth = Situations.GoodForStealth(ctx);

        return new GameEvent("Rabbit Freeze",
            "A rabbit. Frozen mid-hop, ears up, watching you. One wrong move and it bolts.", 0.8)
            .Requires(EventCondition.Foraging, EventCondition.IsDaytime)
            .WithSituationFactor(Situations.GoodForStealth, 1.5)
            .WithConditionFactor(EventCondition.LowVisibility, 1.3)
            .WithConditionFactor(EventCondition.HasPredators, 0.5) // Predators suppress small game
            .WithCooldown(4)
            .Choice("Throw",
                hasWeapon ? "Use what you have. One shot." : "Grab a rock. One chance.",
                [
                    new EventResult("Clean hit. It drops.", goodStealth ? 0.30 : 0.18, 2)
                        .Rewards(RewardPool.SmallGame),
                    new EventResult("Close. It startles and runs.", 0.40, 2),
                    new EventResult("Wide. Gone before the throw lands.", 0.35, 2),
                    new EventResult("Perfect throw. Quality catch.", goodStealth ? 0.08 : 0.04, 2)
                        .Rewards(RewardPool.SmallGame)
                        .WithEffects(EffectFactory.Focused(0.1, 30))
                ])
            .Choice("Slow Approach",
                "Patience. Close the distance.",
                [
                    new EventResult("You get close. Strike.", hasWeapon ? 0.40 : 0.20, 10)
                        .Rewards(RewardPool.SmallGame),
                    new EventResult("It bolts before you're in range.", 0.40, 8),
                    new EventResult("It watches until you're close. Then vanishes.", 0.20, 10)
                ])
            .Choice("Let It Go",
                "Not worth the noise or time.",
                [
                    new EventResult("You continue. The rabbit watches you leave.", 1.0, 0)
                ]);
    }

    private static GameEvent BirdsRoosting(GameContext ctx)
    {
        bool hasSpear = ctx.Inventory.GetTool(ToolType.Spear) != null;

        return new GameEvent("Birds Roosting",
            "Ptarmigan in the branches above. Three of them, not yet alarmed.", 0.7)
            .Requires(EventCondition.Traveling, EventCondition.IsForest)
            .WithSituationFactor(Situations.GoodForStealth, 1.3)
            .WithCooldown(6)
            .Choice("Quick Shot",
                hasSpear ? "Spear throw. High risk, could get multiple." : "Throw a rock. Startle them into a better shot.",
                [
                    new EventResult("One drops. The others scatter.", hasSpear ? 0.35 : 0.20, 3)
                        .Rewards(RewardPool.SmallGame),
                    new EventResult("Two fall! Excellent timing.", hasSpear ? 0.10 : 0.03, 3)
                        .Rewards(RewardPool.SmallGame)
                        .Rewards(RewardPool.SmallGame),
                    new EventResult("They scatter before the throw. Nothing.", 0.45, 2),
                    new EventResult("Miss. Branches everywhere. They're long gone.", 0.25, 2)
                ])
            .Choice("Careful Approach",
                "Get beneath them. Wait for the right moment.",
                [
                    new EventResult("Patience pays off. Clean catch.", 0.45, 12)
                        .Rewards(RewardPool.SmallGame),
                    new EventResult("They notice you at the last moment. Gone.", 0.35, 10),
                    new EventResult("One practically walks into your hands.", 0.15, 15)
                        .Rewards(RewardPool.SmallGame)
                        .WithEffects(EffectFactory.Focused(0.15, 45)),
                    new EventResult("Something else in the trees spooks them.", 0.05, 8)
                ])
            .Choice("Note the Location",
                "Mark this spot. Come back prepared.",
                [
                    new EventResult("You memorize the perch. They use it regularly.", 0.80, 2)
                        .MarksDiscovery("bird roosting spot", 0.4),
                    new EventResult("They're already gone when you look again.", 0.20, 2)
                ]);
    }

    private static GameEvent FishVisible(GameContext ctx)
    {
        bool hasSpear = ctx.Inventory.GetTool(ToolType.Spear) != null;
        var waterFeature = ctx.CurrentLocation.GetFeature<WaterFeature>();
        bool hasIceHole = waterFeature?.HasIceHole ?? false;

        return new GameEvent("Fish Visible",
            "Movement beneath the surface. Fish, schooling in the shallows.", 0.6)
            .Requires(EventCondition.NearWater, EventCondition.IsDaytime)
            .WithConditionFactor(EventCondition.IsClear, 1.5) // Clear weather helps visibility
            .WithCooldown(8)
            .Choice("Spear Fish",
                "Quick thrust. Test your aim.",
                [
                    new EventResult("The spear finds its mark. Fresh fish.", hasSpear ? 0.40 : 0.15, 5)
                        .Rewards(RewardPool.SmallGame),
                    new EventResult("Water distorts the position. You miss.", 0.35, 5),
                    new EventResult("A glancing blow. It escapes, wounded.", 0.20, 5)
                        .CreateTension("WoundedPrey", 0.2, animalType: "fish"),
                    new EventResult("Two fish! They were closer together than you thought.", hasSpear ? 0.08 : 0.02, 5)
                        .Rewards(RewardPool.SmallGame)
                        .Rewards(RewardPool.SmallGame)
                ])
            .Choice("Wait for Better Shot",
                "Patience. Let them settle. Study the refraction.",
                [
                    new EventResult("You learn their patterns. Clean strike.", 0.50, 20)
                        .Rewards(RewardPool.SmallGame),
                    new EventResult("They drift deeper. Opportunity passes.", 0.30, 15),
                    new EventResult("Multiple passes later, you've got the technique.", 0.15, 25)
                        .Rewards(RewardPool.SmallGame)
                        .WithEffects(EffectFactory.Focused(0.2, 60)),
                    new EventResult("Something larger scatters them. The fish are gone.", 0.05, 10)
                ])
            .Choice("Remember This Spot",
                "Mark it for when you're better prepared.",
                [
                    new EventResult("Good fishing spot noted. The fish are here regularly.", 0.85, 2)
                        .MarksDiscovery("fish spawning area", 0.5),
                    new EventResult("By the time you mark it, they've moved on.", 0.15, 2)
                ]);
    }

    private static GameEvent SquirrelDen(GameContext ctx)
    {
        var biteVariant = VariantSelector.SelectVerminBite(ctx);

        return new GameEvent("Squirrel Cache",
            "Chattering above. A squirrel watches from a hollow. Its cache might be accessible.", 0.6)
            .Requires(EventCondition.Foraging, EventCondition.IsForest)
            .WithConditionFactor(EventCondition.IsDaytime, 1.3)
            .WithCooldown(12)
            .Choice("Reach In",
                "Feel around in the hollow. The squirrel looks small.",
                [
                    new EventResult("A cache of nuts! The squirrel's winter stores.", 0.35, 5)
                        .Rewards(RewardPool.SquirrelCache),
                    new EventResult("Empty. Already cleaned out.", 0.30, 5),
                    new EventResult("It bites. Hard. You jerk your hand back.", 0.20, 3)
                        .DamageWithVariant(biteVariant)
                        .CreateTension("WoundUntreated", 0.15, description: "squirrel bite"),
                    new EventResult("Nuts and seeds. And something shiny — a pyrite fragment.", 0.10, 8)
                        .Rewards(RewardPool.SquirrelCache, 1.2)
                        .Rewards(RewardPool.CraftingMaterials),
                    new EventResult("A second squirrel. They defend their home viciously.", 0.05, 5)
                        .DamageWithVariant(biteVariant)
                        .DamageWithVariant(biteVariant)
                ])
            .Choice("Smoke It Out",
                "Build a small fire below. Drive it out.",
                [
                    new EventResult("It flees. The cache is yours.", 0.50, 15)
                        .Costs(ResourceType.Tinder, 1)
                        .Rewards(RewardPool.SquirrelCache, 1.3),
                    new EventResult("The smoke doesn't reach. Waste of tinder.", 0.25, 12)
                        .Costs(ResourceType.Tinder, 1),
                    new EventResult("Success, but the hollow catches fire. Nothing salvageable.", 0.15, 10)
                        .Costs(ResourceType.Tinder, 1),
                    new EventResult("Perfect execution. Nuts, seeds, and a very angry but displaced squirrel.", 0.10, 18)
                        .Costs(ResourceType.Tinder, 1)
                        .Rewards(RewardPool.SquirrelCache, 1.5)
                        .Rewards(RewardPool.SmallGame)
                ],
                requires: [EventCondition.HasTinder])
            .Choice("Watch Where It Goes",
                "Follow from a distance. It might lead to a better cache.",
                [
                    new EventResult("It leads you to a larger cache site.", 0.40, 20)
                        .MarksDiscovery("squirrel cache cluster", 0.6),
                    new EventResult("You lose sight of it in the canopy.", 0.40, 15),
                    new EventResult("It goes to ground. You find a second hollow.", 0.15, 25)
                        .Rewards(RewardPool.SquirrelCache, 1.2),
                    new EventResult("It circles back. You're not fooling anyone.", 0.05, 10)
                ]);
    }

    private static GameEvent TrackIntersection(GameContext ctx)
    {
        bool hasWeapon = ctx.Inventory.HasWeapon;
        var smallGame = AnimalSelector.GetRandomSmallGame();

        return new GameEvent("Track Intersection",
            $"Fresh tracks converge here. {smallGame.TacticsDescription}", 0.7)
            .Requires(EventCondition.Foraging)
            .RequiresSituation(Situations.IsFollowingAnimalSigns)
            .WithSituationFactor(Situations.GoodForStealth, 1.4)
            .WithSituationFactor(Situations.HasFreshTrail, 1.4)  // Recent sign examination helps
            .WithCooldown(6)
            .Choice("Flush It Out",
                "Make noise. Force it to move.",
                [
                    new EventResult("It bolts — right into your waiting strike.", hasWeapon ? 0.30 : 0.12, 3)
                        .Rewards(RewardPool.SmallGame),
                    new EventResult("It runs the wrong direction. Lost.", 0.45, 3),
                    new EventResult("Nothing there. The tracks were old.", 0.20, 2),
                    new EventResult("Something larger was watching too. It takes the prey before you can.", 0.05, 3)
                        .CreateTension("Stalked", 0.2)
                ])
            .Choice("Circle Around",
                "Get into ambush position. Take your time.",
                [
                    new EventResult("Perfect position. Easy catch.", 0.35, 15)
                        .Rewards(RewardPool.SmallGame),
                    new EventResult("It spots you while circling. Gone.", 0.35, 12),
                    new EventResult("Your patience is rewarded. Clean strike.", 0.20, 18)
                        .Rewards(RewardPool.SmallGame)
                        .WithEffects(EffectFactory.Focused(0.15, 30)),
                    new EventResult("You complete the circle. It's not there.", 0.10, 15)
                ])
            .Choice("Mark for Later",
                "This is a game trail. Worth remembering.",
                [
                    new EventResult("You mark the intersection. These tracks will be here again.", 0.75, 3)
                        .MarksAnimalSign(smallGame.AnimalType, 0.5),
                    new EventResult("Rain will wash these tracks away. But the path remains.", 0.25, 3)
                        .MarksDiscovery("game trail", 0.3)
                ]);
    }

    private static GameEvent GrouseFlushed(GameContext ctx)
    {
        return new GameEvent("Grouse Flushed",
            "An explosion of wings at your feet! Grouse, camouflaged until you nearly stepped on them.", 0.6)
            .Requires(EventCondition.Traveling)
            .WithConditionFactor(EventCondition.IsForest, 1.4)
            .WithCooldown(8)
            .Choice("Snap Reaction",
                "React! Grab or strike before they're gone.",
                [
                    new EventResult("Reflexes save you. One caught mid-flight.", 0.20, 1)
                        .Rewards(RewardPool.SmallGame),
                    new EventResult("Fingers close on feathers. It pulls free.", 0.35, 1),
                    new EventResult("You startle backward. They're gone.", 0.40, 1)
                        .WithEffects(EffectFactory.Shaken(0.1)),
                    new EventResult("Two in hand! Incredible reflexes.", 0.05, 1)
                        .Rewards(RewardPool.SmallGame)
                        .Rewards(RewardPool.SmallGame)
                ])
            .Choice("Watch Where They Land",
                "Mark their landing. Approach carefully.",
                [
                    new EventResult("You spot where they settle. Careful approach pays off.", 0.40, 12)
                        .Rewards(RewardPool.SmallGame),
                    new EventResult("They fly further than expected. Lost in the brush.", 0.35, 8),
                    new EventResult("Two landed separately. You get one.", 0.20, 15)
                        .Rewards(RewardPool.SmallGame)
                        .MarksDiscovery("grouse territory", 0.3),
                    new EventResult("They double back. One lands right next to you.", 0.05, 5)
                        .Rewards(RewardPool.SmallGame)
                ])
            .Choice("Let Them Go",
                "They're gone. Continue on.",
                [
                    new EventResult("Heart still pounding. You move on.", 0.90, 0),
                    new EventResult("But you remember this spot. They nest here.", 0.10, 0)
                        .MarksDiscovery("grouse nesting area", 0.4)
                ]);
    }
}
