using text_survival.Bodies;
using text_survival.Effects;
using text_survival.Environments.Features;
using text_survival.Items;

namespace text_survival.Actions;

public static partial class GameEventRegistry
{
    // === HERD ARC EVENTS ===

    /// <summary>
    /// Stage 1 of Herd arc - distant thunder of hooves.
    /// A herd is migrating through the area. Major food opportunity.
    /// </summary>
    private static GameEvent DistantThunder(GameContext ctx)
    {
        return new GameEvent("Distant Thunder",
            "The ground trembles. A sound like thunder, but rhythmic. A herd is moving through — hundreds of animals.", 0.5)
            .Requires(EventCondition.OnExpedition)
            // SupplyPressure: low food, fuel, or water - more driven to pursue food opportunity
            .WithSituationFactor(Situations.SupplyPressure, 2.0)
            .Choice("Track Them",
                "Follow the sound. Find the herd.",
                [
                    new EventResult("Heavy hoofbeats. Bison. Dangerous but rewarding.", weight: 0.35, minutes: 25)
                        .CreateTension("HerdNearby", 0.4, animalType: "Bison", direction: "east"),
                    new EventResult("Lighter rhythm. Deer or caribou. Safer targets.", weight: 0.45, minutes: 20)
                        .CreateTension("HerdNearby", 0.4, animalType: "Deer", direction: "east"),
                    new EventResult("You catch glimpses through the trees. A massive herd.", weight: 0.20, minutes: 30)
                        .CreateTension("HerdNearby", 0.5, animalType: "Caribou", direction: "north")
                ])
            .Choice("Let Them Pass",
                "Too dangerous. Too far. Not worth the risk.",
                [
                    new EventResult("The thunder fades into the distance. Opportunity lost.", weight: 1.0)
                ])
            .Choice("Rush to Intercept",
                "Run. Cut them off before they're gone.",
                [
                    new EventResult("You run hard. Find the edge of the herd.", weight: 0.50, minutes: 15)
                        .CreateTension("HerdNearby", 0.6, animalType: "Deer")
                        .WithEffects(EffectFactory.Exhausted(0.2, 30)),
                    new EventResult("Made it. Wolves shadow the herd's edge.", weight: 0.30, minutes: 15)
                        .CreateTension("HerdNearby", 0.6, animalType: "Caribou")
                        .CreateTension("Stalked", 0.2, animalType: "Wolf"),
                    new EventResult("Too late. They've moved past. Just stragglers left.", weight: 0.20, minutes: 20)
                        .CreateTension("HerdNearby", 0.3, animalType: "Deer")
                ]);
    }

    /// <summary>
    /// Stage 2 of Herd arc - at the edge of the herd.
    /// Choose how to hunt: stragglers, prime kill, or scavenge.
    /// </summary>
    private static GameEvent EdgeOfHerd(GameContext ctx)
    {
        var herdTension = ctx.Tensions.GetTension("HerdNearby");
        var animal = herdTension?.AnimalType ?? "deer";

        var isLargeAnimal = animal.ToLower() == "bison";
        var description = isLargeAnimal
            ? "A river of fur and muscle. Wolves shadow the edges, waiting. You see a young bull favoring its left leg."
            : "The herd flows past. Wolves pick at the edges. You spot weak targets — the old, the lame.";

        return new GameEvent("Edge of the Herd",
            $"You've found them. {description}", 2.0)
            .Requires(EventCondition.HerdNearby)
            .Choice("Hunt the Stragglers",
                "Target the weak. Safer, smaller reward.",
                [
                    new EventResult($"Clean kill on a weak {animal.ToLower()}. Meat for days.", weight: 0.55, minutes: 30)
                        .ResolveTension("HerdNearby")
                        .FindsLargeMeat()
                        .CreateTension("FoodScentStrong", 0.4),
                    new EventResult("Miss. The straggler rejoins the herd.", weight: 0.25, minutes: 25)
                        .Escalate("HerdNearby", -0.2),
                    new EventResult("Kill, but wolves noticed. Working fast now.", weight: 0.15, minutes: 20)
                        .ResolveTension("HerdNearby")
                        .FindsMeat()
                        .BecomeStalked(0.3, "Wolf"),
                    new EventResult("Spooked them. Stampede!", weight: 0.05, minutes: 5)
                        .Escalate("HerdNearby", 0.3)
                        .Frightening()
                ])
            .Choice("Go for a Prime Kill",
                "Target a healthy animal. More risk, more reward.",
                [
                    new EventResult($"Perfect throw. The {animal.ToLower()} goes down. Huge haul.", weight: 0.30, minutes: 25)
                        .ResolveTension("HerdNearby")
                        .FindsLargeMeat()
                        .FindsLargeMeat()
                        .CreateTension("FoodScentStrong", 0.6),
                    new EventResult("Hit but not down. It bolts.", weight: 0.25, minutes: 20)
                        .ResolveTension("HerdNearby")
                        .CreateTension("WoundedPrey", 0.4, animalType: animal),
                    new EventResult("Miss. They scatter. Stampede risk.", weight: 0.25, minutes: 15)
                        .Escalate("HerdNearby", 0.25),
                    new EventResult("You spooked the herd. STAMPEDE!", weight: 0.20, minutes: 5)
                        .Damage(15, DamageType.Blunt, "glancing blow from stampede")
                        .ResolveTension("HerdNearby")
                        .Terrifying()
                ])
            .Choice("Wait for Predator Leftovers",
                "Let wolves make kills. Scavenge after.",
                [
                    new EventResult("Wolves bring one down. You wait, then claim scraps.", weight: 0.50, minutes: 45)
                        .ResolveTension("HerdNearby")
                        .FindsMeat(),
                    new EventResult("Wolves don't leave much. But something is better than nothing.", weight: 0.30, minutes: 50)
                        .ResolveTension("HerdNearby")
                        .Rewards(RewardPool.SmallGame),
                    new EventResult("Wolves don't like you near their kill.", weight: 0.20, minutes: 40)
                        .ResolveTension("HerdNearby")
                        .Encounter("Wolf", 20, 0.5)
                ])
            .Choice("Observe Migration Route",
                "Learn for next time. Mark the path they take.",
                [
                    new EventResult("You note the route. They'll pass again next season.", weight: 1.0, minutes: 20)
                        .ResolveTension("HerdNearby")
                        .CreateTension("MarkedDiscovery", 0.6, description: "herd migration route")
                ]);
    }

    /// <summary>
    /// Stage 3a of Herd arc - stampede.
    /// Herd spooked. Player in danger.
    /// </summary>
    private static GameEvent Stampede(GameContext ctx)
    {
        var herdTension = ctx.Tensions.GetTension("HerdNearby");
        var animal = herdTension?.AnimalType ?? "deer";

        var isLargeAnimal = animal.ToLower() == "bison";
        var description = isLargeAnimal
            ? "The bison are running. Toward you. Thousands of pounds of muscle and horn."
            : "The herd stampedes. A wall of hooves and panic.";

        return new GameEvent("Stampede",
            $"They've spooked. {description}", 3.0)
            .Requires(EventCondition.HerdNearbyUrgent)
            .Choice("Run Perpendicular",
                "Sprint at a right angle. Get out of their path.",
                [
                    new EventResult("You dive aside. The herd thunders past.", weight: 0.55, minutes: 3)
                        .ResolveTension("HerdNearby")
                        .WithEffects(EffectFactory.Exhausted(0.3, 30)),
                    new EventResult("Almost clear. A glancing blow sends you sprawling.", weight: 0.30, minutes: 5)
                        .ResolveTension("HerdNearby")
                        .Damage(8, DamageType.Blunt, "glancing blow from stampede")
                        .WithEffects(EffectFactory.Sore(0.4, 60)),
                    new EventResult("Too slow. They're everywhere.", weight: 0.15, minutes: 5)
                        .ResolveTension("HerdNearby")
                        .Damage(20, DamageType.Blunt, "trampled by herd")
                        .Panicking()
                ])
            .Choice("Find Cover",
                "Rock. Tree. Anything solid.",
                [
                    new EventResult("Behind a boulder. They flow around you.", weight: 0.50, minutes: 5)
                        .ResolveTension("HerdNearby"),
                    new EventResult("Tree trunk. Pressed flat. Hooves inches away.", weight: 0.30, minutes: 5)
                        .ResolveTension("HerdNearby")
                        .Frightening(),
                    new EventResult("No cover. Open ground.", weight: 0.20, minutes: 3)
                        .Damage(15, DamageType.Blunt, "trampled")
                        .ResolveTension("HerdNearby")
                ])
            .Choice("Drop and Curl",
                "Hit the ground. Make yourself small. Hope they avoid you.",
                [
                    new EventResult("They flow around you. Hooves everywhere but none hit.", weight: 0.35, minutes: 5)
                        .ResolveTension("HerdNearby")
                        .Terrifying(),
                    new EventResult("Mostly missed. One clips your shoulder.", weight: 0.35, minutes: 5)
                        .ResolveTension("HerdNearby")
                        .Damage(10, DamageType.Blunt, "kicked by fleeing animal"),
                    new EventResult("Bad gamble. Trampled.", weight: 0.30, minutes: 5)
                        .ResolveTension("HerdNearby")
                        .Damage(25, DamageType.Blunt, "trampled by herd")
                        .Panicking()
                ])
            .Choice("Drop Pack and Sprint",
                "Lose everything but live.",
                [
                    new EventResult("Pack abandoned. You escape. Everything lost.", weight: 0.80, minutes: 3)
                        .ResolveTension("HerdNearby")
                        .Costs(ResourceType.Fuel, 5)
                        .Costs(ResourceType.Food, 3),
                    new EventResult("Still not fast enough.", weight: 0.20, minutes: 3)
                        .ResolveTension("HerdNearby")
                        .Damage(12, DamageType.Blunt, "trampled")
                        .Costs(ResourceType.Fuel, 5)
                        .Costs(ResourceType.Food, 3)
                ]);
    }

    /// <summary>
    /// Stage 4 of Herd arc - the followers.
    /// After a kill, wolves that were following the herd now follow you.
    /// </summary>
    private static GameEvent TheFollowers(GameContext ctx)
    {
        return new GameEvent("The Followers",
            "The herd has moved on. Their shadows haven't. Wolves that were following the herd are now following YOU.", 2.0)
            .Requires(EventCondition.FoodScentStrong)
            .Requires(EventCondition.OnExpedition)
            // AttractiveToPredators: carrying meat, bleeding, or food scent - wolves drawn to you
            .WithSituationFactor(Situations.AttractiveToPredators, 3.0)
            .Choice("Move Quickly, Stay Alert",
                "Get the meat back to camp. Stay vigilant.",
                [
                    new EventResult("A lone wolf peels off from the pack. It's following.", weight: 0.45, minutes: 10)
                        .BecomeStalked(0.3, "Wolf"),
                    new EventResult("Three shapes detach from the herd's shadow. They're coordinating.", weight: 0.35, minutes: 10)
                        .CreateTension("PackNearby", 0.25, animalType: "Wolf"),
                    new EventResult("They don't follow. Too busy with their own kills.", weight: 0.20, minutes: 10)
                ])
            .Choice("Create Distance",
                "Detour. Lose them in the terrain.",
                [
                    new EventResult("Long way around. But you lose the followers.", weight: 0.60, minutes: 30)
                        .Escalate("FoodScentStrong", -0.2),
                    new EventResult("They're persistent. Still behind you.", weight: 0.30, minutes: 25)
                        .BecomeStalked(0.2, "Wolf"),
                    new EventResult("Detour leads you somewhere unfamiliar.", weight: 0.10, minutes: 35)
                        .Shaken()
                ])
            .Choice("Leave Some Meat Behind",
                "Sacrifice a portion. Maybe they'll be satisfied.",
                [
                    new EventResult("They take the offering. Don't follow further.", weight: 0.70, minutes: 10)
                        .Costs(ResourceType.Food, 2),
                    new EventResult("They take it AND keep following. Want more.", weight: 0.30, minutes: 10)
                        .Costs(ResourceType.Food, 2)
                        .BecomeStalked(0.3, "Wolf")
                ],
                [EventCondition.HasMeat]);
    }
}
