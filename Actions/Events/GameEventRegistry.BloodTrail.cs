using text_survival.Bodies;
using text_survival.Effects;
using text_survival.Environments.Features;
using text_survival.Items;

namespace text_survival.Actions;

public static partial class GameEventRegistry
{
    // === BLOOD TRAIL ARC EVENTS ===

    /// <summary>
    /// Stage 2 of Blood Trail arc - following the wounded prey's trail.
    /// The trail is still visible, animal is slowing, but player is getting further from camp.
    /// </summary>
    private static GameEvent BloodInSnow(GameContext ctx)
    {
        var woundedTension = ctx.Tensions.GetTension("WoundedPrey");
        var animal = woundedTension?.AnimalType ?? "animal";

        // Situational reading based on blood trail description
        var trailDescription = woundedTension?.Severity > 0.5
            ? "Bright pools of blood, closely spaced. The animal is slowing."
            : "Dark droplets, widely spaced. It's still moving fast.";

        return new GameEvent("Blood in the Snow",
            $"The trail continues. {trailDescription} You're getting further from camp...", 1.5)
            .Requires(EventCondition.WoundedPrey)
            .MoreLikelyIf(EventCondition.LowOnFood, 2.0)
            .MoreLikelyIf(EventCondition.Injured, 0.5)  // Less likely to push when hurt
            .Choice("Press On",
                "The blood is fresh. It can't be far now.",
                [
                    new EventResult("You find it collapsed in the snow. Still alive, but barely.", weight: 0.45, minutes: 20)
                        .Escalate("WoundedPrey", 0.3),
                    new EventResult("Trail goes cold. Snow is covering the blood.", weight: 0.30, minutes: 25)
                        .ResolveTension("WoundedPrey"),
                    new EventResult("You're deep in unfamiliar territory now. The trail continues.", weight: 0.15, minutes: 30)
                        .Escalate("WoundedPrey", 0.15),
                    new EventResult("Something else found the trail first.", weight: 0.10, minutes: 15)
                        .CreateTension("Stalked", 0.3)
                        .ResolveTension("WoundedPrey")
                ])
            .Choice("Set Snare on Trail",
                "Rig a snare where the animal will pass. Come back later.",
                [
                    new EventResult("Snare set on the blood trail. Maybe it'll work.", weight: 0.70, minutes: 15)
                        .Costs(ResourceType.PlantFiber, 2)
                        .CreateTension("MarkedDiscovery", 0.5, description: "snare on blood trail")
                        .ResolveTension("WoundedPrey"),
                    new EventResult("Ground's too hard. Can't set the snare properly.", weight: 0.30, minutes: 10)
                        .Costs(ResourceType.PlantFiber, 1)
                ],
                [EventCondition.HasPlantFiber])
            .Choice("Turn Back",
                "You've come far enough. The risk isn't worth it.",
                [
                    new EventResult("You abandon the chase. The meat is lost.", weight: 1.0)
                        .ResolveTension("WoundedPrey")
                ]);
    }

    /// <summary>
    /// Stage 3 of Blood Trail arc - found the dying animal.
    /// Scavengers have noticed. Player must decide how to handle the kill.
    /// </summary>
    private static GameEvent TheDyingAnimal(GameContext ctx)
    {
        var woundedTension = ctx.Tensions.GetTension("WoundedPrey");
        var animal = woundedTension?.AnimalType ?? "deer";
        var territory = ctx.CurrentLocation.GetFeature<AnimalTerritoryFeature>();

        // Scavenger type affects options
        var scavengerType = territory?.HasPredators() == true
            ? (territory.GetRandomPredatorName() ?? "wolf")
            : "ravens";

        var scavengerDesc = scavengerType.ToLower() == "ravens"
            ? "Ravens circle overhead, cawing. They've spotted the blood."
            : $"A {scavengerType.ToLower()} watches from the treeline. It's been following the blood trail too.";

        return new GameEvent("The Dying Animal",
            $"You find the {animal.ToLower()}. It's down, breathing shallow, too weak to run. {scavengerDesc}", 2.0)
            .Requires(EventCondition.WoundedPreyHigh)
            .Choice("Finish It Quickly",
                "End it fast. Take the meat before competition arrives.",
                [
                    new EventResult($"A clean kill. You work fast, aware of the {scavengerType.ToLower()} watching.", weight: 0.60, minutes: 15)
                        .ResolveTension("WoundedPrey")
                        .Rewards(RewardPool.LargeMeat)
                        .CreateTension("FoodScentStrong", 0.4),
                    new EventResult($"The {scavengerType.ToLower()} moves closer as you work. Hurrying now.", weight: 0.25, minutes: 12)
                        .ResolveTension("WoundedPrey")
                        .Rewards(RewardPool.BasicMeat)
                        .CreateTension("Stalked", 0.3, animalType: scavengerType),
                    new EventResult($"Too slow. The {scavengerType.ToLower()} commits.", weight: 0.15, minutes: 10)
                        .ResolveTension("WoundedPrey")
                        .Rewards(RewardPool.BasicMeat)
                        .Encounter(scavengerType, 20, 0.5)
                ])
            .Choice("Wait for It to Die",
                "Let nature take its course. No need to risk getting close yet.",
                [
                    new EventResult("It finally stops breathing. The scavengers held back.", weight: 0.40, minutes: 25)
                        .ResolveTension("WoundedPrey")
                        .Rewards(RewardPool.LargeMeat),
                    new EventResult($"The {scavengerType.ToLower()} doesn't wait. It claims the kill.", weight: 0.35, minutes: 20)
                        .ResolveTension("WoundedPrey")
                        .CreateTension("Stalked", 0.2, animalType: scavengerType),
                    new EventResult("Takes too long. Other scavengers arrive. You retreat.", weight: 0.25, minutes: 30)
                        .ResolveTension("WoundedPrey")
                ])
            .Choice("Scare Off Scavengers First",
                scavengerType.ToLower() == "ravens"
                    ? "Make noise. Drive the birds away."
                    : "Posture aggressively. Show you're not easy prey.",
                scavengerType.ToLower() == "ravens"
                    ? [
                        new EventResult("The ravens scatter. You claim your kill in peace.", weight: 0.80, minutes: 18)
                            .ResolveTension("WoundedPrey")
                            .Rewards(RewardPool.LargeMeat),
                        new EventResult("They circle back quickly. You work fast.", weight: 0.20, minutes: 15)
                            .ResolveTension("WoundedPrey")
                            .Rewards(RewardPool.BasicMeat)
                    ]
                    : [
                        new EventResult($"The {scavengerType.ToLower()} backs off. For now.", weight: 0.50, minutes: 20)
                            .ResolveTension("WoundedPrey")
                            .Rewards(RewardPool.LargeMeat)
                            .CreateTension("Stalked", 0.2, animalType: scavengerType),
                        new EventResult("It doesn't back down. This is a confrontation.", weight: 0.30, minutes: 10)
                            .ResolveTension("WoundedPrey")
                            .Encounter(scavengerType, 25, 0.6),
                        new EventResult("You misjudged. It attacks.", weight: 0.20, minutes: 5)
                            .ResolveTension("WoundedPrey")
                            .Encounter(scavengerType, 10, 0.8)
                    ]);
    }

    /// <summary>
    /// Stage 4 of Blood Trail arc - predators have converged on the blood trail.
    /// Critical situation: predators between player and the wounded prey.
    /// </summary>
    private static GameEvent ScavengersConverge(GameContext ctx)
    {
        var woundedTension = ctx.Tensions.GetTension("WoundedPrey");
        var animal = woundedTension?.AnimalType ?? "deer";
        var territory = ctx.CurrentLocation.GetFeature<AnimalTerritoryFeature>();
        var predator = territory?.GetRandomPredatorName() ?? "Wolf";

        return new GameEvent("Scavengers Converge",
            $"Too late. {predator}s have found the blood trail. They're between you and the {animal.ToLower()}.", 3.0)
            .Requires(EventCondition.WoundedPreyCritical, EventCondition.HasPredators)
            .Choice("Fight for Your Kill",
                "You tracked it. You wounded it. It's YOUR meat.",
                [
                    new EventResult($"You charge in. The {predator.ToLower()} doesn't back down.", weight: 1.0, minutes: 5)
                        .ResolveTension("WoundedPrey")
                        .Encounter(predator, 15, 0.7)
                ])
            .Choice("Abandon and Retreat",
                "Not worth dying over. Let them have it.",
                [
                    new EventResult("You back away slowly. They let you go.", weight: 0.70, minutes: 10)
                        .ResolveTension("WoundedPrey"),
                    new EventResult("One follows. It's not letting you go that easily.", weight: 0.30, minutes: 8)
                        .ResolveTension("WoundedPrey")
                        .CreateTension("Stalked", 0.4, animalType: predator)
                ])
            .Choice("Create Distraction",
                "Throw something to draw them away. Make a break for the carcass.",
                [
                    new EventResult("You throw your meat. They take the bait. You grab what you can.", weight: 0.50, minutes: 12)
                        .ResolveTension("WoundedPrey")
                        .Costs(ResourceType.Food, 2)
                        .Rewards(RewardPool.BasicMeat),
                    new EventResult("They grab your offering AND circle back.", weight: 0.30, minutes: 10)
                        .ResolveTension("WoundedPrey")
                        .Costs(ResourceType.Food, 2),
                    new EventResult("It works perfectly. Full access to the kill.", weight: 0.20, minutes: 15)
                        .ResolveTension("WoundedPrey")
                        .Costs(ResourceType.Food, 1)
                        .Rewards(RewardPool.LargeMeat)
                ],
                [EventCondition.HasMeat]);
    }
}
