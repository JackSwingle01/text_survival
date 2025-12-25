using text_survival.Bodies;
using text_survival.Effects;
using text_survival.Environments.Features;
using text_survival.Items;

namespace text_survival.Actions;

public static partial class GameEventRegistry
{
    // === DEN ARC EVENTS ===

    /// <summary>
    /// Stage 1 of Den arc - discovering a potential shelter.
    /// Signs of wildlife occupation. Superior shelter vs dangerous eviction.
    /// </summary>
    private static GameEvent TheFind(GameContext ctx)
    {
        var territory = ctx.CurrentLocation.GetFeature<AnimalTerritoryFeature>();
        var predator = territory?.GetRandomPredatorName() ?? "Wolf";

        // Vary description based on what kind of den
        var denType = Utils.DetermineSuccess(0.5) ? "cave" : "overhang";
        var denDesc = denType == "cave"
            ? "A cave mouth. Deep, dry, protected from wind. Perfect shelter."
            : "A rocky overhang. Good protection from snow and wind.";

        return new GameEvent("The Find",
            $"{denDesc} But there are signs — tracks, scat, the smell of animal.", 0.6)
            .Requires(EventCondition.IsExpedition, EventCondition.NoShelter)
            .WithConditionFactor(EventCondition.ShelterWeakened, 3.0)
            .WithConditionFactor(EventCondition.ExtremelyCold, 2.0)
            .Choice("Investigate Carefully",
                "Check the signs. Know what you're dealing with.",
                [
                    new EventResult($"Fresh scat. A {predator.ToLower()} has been here recently.", weight: 0.40, minutes: 15)
                        .CreateTension("ClaimedTerritory", 0.3, animalType: predator, location: ctx.CurrentLocation),
                    new EventResult("Old droppings. Dried. Maybe abandoned?", weight: 0.30, minutes: 15)
                        .CreateTension("ClaimedTerritory", 0.2, animalType: predator, location: ctx.CurrentLocation),
                    new EventResult($"Fresh tracks, still steaming scat. The {predator.ToLower()} is close.", weight: 0.20, minutes: 10)
                        .CreateTension("ClaimedTerritory", 0.5, animalType: predator, location: ctx.CurrentLocation),
                    new EventResult("Empty. Abandoned. Yours for the taking.", weight: 0.10, minutes: 20)
                        .AddsShelter(temp: 0.4, overhead: 0.6, wind: 0.7)
                        .Chain(ClaimingTheDen)
                ])
            .Choice("Mark It and Leave",
                "Blaze a nearby tree. Come back prepared.",
                [
                    new EventResult("You mark the location. Worth returning with preparation.", weight: 1.0, minutes: 10)
                        .CreateTension("MarkedDiscovery", 0.6, description: "potential shelter den", location: ctx.CurrentLocation)
                ])
            .Choice("Move On",
                "Not worth the risk. Keep looking.",
                [
                    new EventResult("You leave the den behind. Safer, but shelter-less.", weight: 1.0)
                ]);
    }

    /// <summary>
    /// Stage 2 of Den arc - assessing the claim.
    /// Player knows what's there. Now must decide tactics.
    /// </summary>
    private static GameEvent AssessingTheClaim(GameContext ctx)
    {
        var denTension = ctx.Tensions.GetTension("ClaimedTerritory");
        var animal = denTension?.AnimalType ?? "Wolf";

        // Different animals, different tactics
        var isBear = animal.ToLower().Contains("bear");
        var isWolf = animal.ToLower().Contains("wolf");

        var tacticsDesc = isBear
            ? "Bear. Fire works best — smoke them out. Noise alone won't help."
            : isWolf
                ? "Wolves. Fire works. Noise might work. They hunt during the day — maybe wait."
                : "Something territorial. Proceed with caution.";

        return new GameEvent("Assessing the Claim",
            $"You know what lives here now. {tacticsDesc} The question is: is it worth it?", 1.5)
            .Requires(EventCondition.ClaimedTerritory, EventCondition.Working)
            .Choice("Wait for It to Leave",
                isWolf ? "Wolves hunt during the day. Maybe it's out." : "Wait and watch. Maybe an opportunity.",
                isWolf
                    ? [
                        new EventResult("The den is empty. The pack is hunting. Quick, claim it.", weight: 0.45, minutes: 60)
                            .ResolveTension("ClaimedTerritory")
                            .AddsShelter(temp: 0.5, overhead: 0.7, wind: 0.8)
                            .Chain(ClaimingTheDen),
                        new EventResult("Hours pass. No movement. Still occupied.", weight: 0.35, minutes: 90),
                        new EventResult("You wait too long. They return. Spotted.", weight: 0.20, minutes: 75)
                            .Escalate("ClaimedTerritory", 0.3)
                            .BecomeStalked(0.3, animal)
                    ]
                    : [
                        new EventResult("It emerges to hunt. You slip in.", weight: 0.25, minutes: 120)
                            .ResolveTension("ClaimedTerritory")
                            .AddsShelter(temp: 0.5, overhead: 0.7, wind: 0.8)
                            .Chain(ClaimingTheDen),
                        new EventResult("It's not leaving. Bears are stubborn.", weight: 0.50, minutes: 90),
                        new EventResult("Hibernating. It's not going anywhere.", weight: 0.25, minutes: 60)
                            .Escalate("ClaimedTerritory", 0.2)
                    ])
            .Choice("Drive It Out with Smoke",
                "Build a fire at the entrance. Smoke it out.",
                [
                    new EventResult("Smoke fills the den. Coughing, the animal flees.", weight: 0.55, minutes: 30)
                        .Costs(ResourceType.Fuel, 3)
                        .Costs(ResourceType.Tinder, 1)
                        .ResolveTension("ClaimedTerritory")
                        .AddsShelter(temp: 0.4, overhead: 0.6, wind: 0.7)
                        .Chain(ClaimingTheDen),
                    new EventResult("It bursts out enraged. Fight for your claim.", weight: 0.30, minutes: 15)
                        .Costs(ResourceType.Fuel, 2)
                        .Costs(ResourceType.Tinder, 1)
                        .Encounter(animal, 15, 0.7),
                    new EventResult("Wind shifts. Smoke blows back at you.", weight: 0.15, minutes: 20)
                        .Costs(ResourceType.Fuel, 2)
                        .Costs(ResourceType.Tinder, 1)
                        .WithEffects(EffectFactory.Nauseous(0.3, 30))
                ],
                [EventCondition.HasFuel, EventCondition.HasTinder])
            .Choice("Drive It Out with Noise",
                "Shouting, banging rocks, make it think twice.",
                isWolf
                    ? [
                        new EventResult("The wolves scatter at the commotion. Den yours.", weight: 0.40, minutes: 25)
                            .ResolveTension("ClaimedTerritory")
                            .AddsShelter(temp: 0.4, overhead: 0.6, wind: 0.7)
                            .Chain(ClaimingTheDen),
                        new EventResult("They back off but don't leave. Stalemate.", weight: 0.40, minutes: 20),
                        new EventResult("Your noise provokes them. They attack.", weight: 0.20, minutes: 10)
                            .Encounter(animal, 20, 0.6)
                    ]
                    : [
                        new EventResult("Noise doesn't phase it. Still there.", weight: 0.60, minutes: 15),
                        new EventResult("It emerges, annoyed. Confrontation.", weight: 0.30, minutes: 10)
                            .Escalate("ClaimedTerritory", 0.3),
                        new EventResult("Against odds, it leaves. Noise worked.", weight: 0.10, minutes: 20)
                            .ResolveTension("ClaimedTerritory")
                            .AddsShelter(temp: 0.4, overhead: 0.6, wind: 0.7)
                            .Chain(ClaimingTheDen)
                    ])
            .Choice("Fight for It Now",
                "Enter the den. Force the confrontation.",
                [
                    new EventResult($"You enter. The {animal.ToLower()} is cornered. It fights.", weight: 1.0, minutes: 5)
                        .Encounter(animal, 10, 0.9)
                ])
            .Choice("Abandon the Claim",
                "Too risky. Walk away.",
                [
                    new EventResult("You leave. The shelter remains occupied.", weight: 1.0)
                        .ResolveTension("ClaimedTerritory")
                ]);
    }

    /// <summary>
    /// Stage 3 of Den arc - confrontation.
    /// The animal defends its home. High-stakes fight.
    /// </summary>
    private static GameEvent TheConfrontation(GameContext ctx)
    {
        var denTension = ctx.Tensions.GetTension("ClaimedTerritory");
        var animal = denTension?.AnimalType ?? "Wolf";

        return new GameEvent("The Confrontation",
            $"The {animal.ToLower()} is aware of you. It's defending its home. Cornered animals fight hardest.", 2.5)
            .Requires(EventCondition.ClaimedTerritoryHigh, EventCondition.Working)
            .Choice("Commit to the Fight",
                "This is happening. Make it count.",
                [
                    new EventResult($"The {animal.ToLower()} charges. The fight for the den begins.", weight: 1.0, minutes: 5)
                        .ResolveTension("ClaimedTerritory")
                        .Encounter(animal, 10, 0.85)
                ])
            .Choice("One Final Attempt at Intimidation",
                "Torch forward. Full aggression. Maybe it backs down.",
                [
                    new EventResult("Fire in its face. It breaks. The den is yours.", weight: 0.35, minutes: 10)
                        .Costs(ResourceType.Tinder, 1)
                        .Costs(ResourceType.Fuel, 1)
                        .ResolveTension("ClaimedTerritory")
                        .AddsShelter(temp: 0.4, overhead: 0.6, wind: 0.7)
                        .Chain(ClaimingTheDen),
                    new EventResult("Cornered and desperate. It attacks through the fire.", weight: 0.45, minutes: 5)
                        .Costs(ResourceType.Tinder, 1)
                        .Encounter(animal, 15, 0.8),
                    new EventResult("It bolts past you. Den yours, but it's not happy.", weight: 0.20, minutes: 8)
                        .Costs(ResourceType.Tinder, 1)
                        .ResolveTension("ClaimedTerritory")
                        .BecomeStalked(0.4, animal)
                        .AddsShelter(temp: 0.4, overhead: 0.6, wind: 0.7)
                        .Chain(ClaimingTheDen)
                ],
                [EventCondition.HasTinder, EventCondition.HasFuel])
            .Choice("Retreat Now",
                "Last chance to back out.",
                [
                    new EventResult("You back away. It watches you go.", weight: 0.70, minutes: 10)
                        .ResolveTension("ClaimedTerritory"),
                    new EventResult("Too late. It's committed. Fight or die.", weight: 0.30, minutes: 3)
                        .Encounter(animal, 8, 0.9)
                ]);
    }

    /// <summary>
    /// Stage 4 of Den arc - claiming the den.
    /// Victory aftermath. Clean it out or move in fast.
    /// Note: This event is chained from successful eviction outcomes, not triggered randomly.
    /// </summary>
    private static GameEvent ClaimingTheDen(GameContext ctx)
    {
        return new GameEvent("Claiming the Den",
            "The shelter is yours. But there's work to do — old bedding, secondary entrances, the smell of its former occupant.", 1.0)
            .Choice("Clear It Out Thoroughly",
                "Remove all traces. Make it safe.",
                [
                    new EventResult("Hours of work. Clean shelter. No disease risk.", weight: 0.80, minutes: 45)
                        .WithEffects(EffectFactory.Exhausted(0.2, 30)),
                    new EventResult("Clean but exhausting. Worth it.", weight: 0.20, minutes: 60)
                        .WithEffects(EffectFactory.Exhausted(0.4, 60))
                ])
            .Choice("Move In Immediately",
                "It's cold. The shelter works. Worry about cleaning later.",
                [
                    new EventResult("You settle in. Warm and dry. The smell fades.", weight: 0.70)
                        .WithEffects(EffectFactory.Rested(0.5, 30)),
                    new EventResult("Something in the old bedding. You're feeling ill.", weight: 0.20, minutes: 15)
                        .CreateTension("FeverRising", 0.2, description: "infected from den bedding"),
                    new EventResult("Parasites in the old fur. Itching, discomfort.", weight: 0.10)
                        .Shaken()
                ])
            .Choice("Relocate Camp Here",
                "This is better than your current camp. Move everything.",
                [
                    new EventResult("Camp relocated. Superior shelter. New home.", weight: 1.0, minutes: 120)
                        .WithEffects(EffectFactory.Exhausted(0.5, 90))
                ]);
    }
}
