using text_survival.Bodies;
using text_survival.Effects;
using text_survival.Environments.Features;
using text_survival.Items;

namespace text_survival.Actions;

public static partial class GameEventRegistry
{
    // === PACK ARC EVENTS ===

    /// <summary>
    /// Stage 1 of Pack arc - discovering pack signs.
    /// Multiple coordinated tracks indicating a pack rather than lone predator.
    /// </summary>
    private static GameEvent PackSigns(GameContext ctx)
    {
        var territory = ctx.CurrentLocation.GetFeature<AnimalTerritoryFeature>();
        var predator = territory?.GetRandomPredatorName() ?? "Wolf";

        return new GameEvent("Pack Signs",
            $"Multiple tracks, recent. Coordinated movement patterns. This isn't a lone hunter — it's a pack of {predator.ToLower()}s.", 0.8)
            .Requires(EventCondition.InAnimalTerritory, EventCondition.HasPredators)
            .Requires(EventCondition.OnExpedition)
            .WithConditionFactor(EventCondition.HasMeat, 2.0)
            .WithConditionFactor(EventCondition.Injured, 1.5)
            .Choice("Move Carefully, Watch Flanks",
                "Slow down. Stay alert. Don't let them get behind you.",
                [
                    new EventResult("You proceed cautiously. No sign of them... yet.", weight: 0.70, minutes: 15),
                    new EventResult("You spot movement in your peripheral. They're paralleling you.", weight: 0.30, minutes: 10)
                        .CreateTension("PackNearby", 0.25, animalType: predator)
                ])
            .Choice("Pick Up Pace Toward Camp",
                "Get back to fire. Packs fear fire.",
                [
                    new EventResult("You move quickly. Something is definitely following.", weight: 0.60, minutes: 8)
                        .CreateTension("PackNearby", 0.2, animalType: predator),
                    new EventResult("Fast movement. Can't see them but you hear them in the brush.", weight: 0.30, minutes: 10)
                        .CreateTension("PackNearby", 0.3, animalType: predator),
                    new EventResult("Your haste triggers their chase instinct.", weight: 0.10, minutes: 5)
                        .CreateTension("PackNearby", 0.45, animalType: predator)
                ])
            .Choice("Hold Position and Assess",
                "Stop. Watch. Know what you're dealing with.",
                [
                    new EventResult("You count the tracks. Three, maybe four. A small pack.", weight: 0.50, minutes: 12)
                        .CreateTension("PackNearby", 0.3, animalType: predator),
                    new EventResult("The snow is churned with prints. Large pack. Five or more.", weight: 0.30, minutes: 12)
                        .CreateTension("PackNearby", 0.4, animalType: predator),
                    new EventResult("Old tracks. They passed hours ago. You're probably fine.", weight: 0.20, minutes: 10)
                ]);
    }

    /// <summary>
    /// Stage 2 of Pack arc - eyes in the treeline.
    /// They're paralleling the player, testing. Not attacking yet.
    /// </summary>
    private static GameEvent EyesInTreeline(GameContext ctx)
    {
        var packTension = ctx.Tensions.GetTension("PackNearby");
        var predator = packTension?.AnimalType ?? "wolf";

        return new GameEvent("Eyes in the Treeline",
            $"Glimpses of movement. The {predator.ToLower()}s are paralleling you. Not attacking yet — testing, probing.", 1.5)
            .Requires(EventCondition.PackNearby, EventCondition.IsExpedition)
            .WithConditionFactor(EventCondition.Injured, 3.0)
            .WithConditionFactor(EventCondition.Slow, 2.0)
            .WithConditionFactor(EventCondition.HasMeat, 2.0)
            .Choice("Keep Moving Steadily",
                "Don't run. Don't stop. Steady pace. Show no weakness.",
                [
                    new EventResult("They match your pace. Watching. Waiting.", weight: 0.40, minutes: 10),
                    new EventResult("One peels off. Then another. Losing interest?", weight: 0.25, minutes: 8)
                        .EscalatesPack(-0.1),
                    new EventResult("They're getting closer.", weight: 0.25, minutes: 8)
                        .EscalatesPack(0.15),
                    new EventResult("Still there. Neither closing nor leaving.", weight: 0.10, minutes: 12)
                ])
            .Choice("Make Yourself Large, Shout",
                "Posturing. Show them you're not prey.",
                [
                    new EventResult("They hesitate. Some back off. Posturing works.", weight: 0.40, minutes: 8)
                        .EscalatesPack(-0.15),
                    new EventResult("No reaction. They've seen this before.", weight: 0.35, minutes: 10),
                    new EventResult("Your aggression provokes them. They're committed now.", weight: 0.25, minutes: 5)
                        .EscalatesPack(0.25)
                        .Unsettling()
                ])
            .Choice("Light a Torch",
                "Fire. The ultimate deterrent.",
                [
                    new EventResult("Flame catches. The pack retreats to the shadows.", weight: 0.70, minutes: 10)
                        .Costs(ResourceType.Tinder, 1)
                        .EscalatesPack(-0.3),
                    new EventResult("Torch lit. They keep their distance but don't leave.", weight: 0.20, minutes: 10)
                        .Costs(ResourceType.Tinder, 1)
                        .EscalatesPack(-0.15),
                    new EventResult("Won't light. Tinder's damp. They see you struggling.", weight: 0.10, minutes: 8)
                        .Costs(ResourceType.Tinder, 1)
                        .EscalatesPack(0.2)
                ],
                [EventCondition.HasTinder, EventCondition.HasFuel])
            .Choice("Run for Camp",
                "Sprint. Hope you're faster than they are.",
                [
                    new EventResult("You run. They give chase. But camp is close.", weight: 0.35, minutes: 8)
                        .EscalatesPack(0.3)
                        .Aborts(),
                    new EventResult("Chase instinct triggered. They're closing fast.", weight: 0.40, minutes: 5)
                        .EscalatesPack(0.4)
                        .Frightening(),
                    new EventResult("Too slow. They cut you off.", weight: 0.25, minutes: 5)
                        .EscalatesPack(0.5)
                        .Terrifying()
                ])
            .Choice("Back Away Slowly",
                "Maintain eye contact. Don't turn your back. Slow retreat.",
                [
                    new EventResult("Slow and steady. They watch but don't follow.", weight: 0.45, minutes: 15)
                        .EscalatesPack(-0.1),
                    new EventResult("One circles. They're testing your flanks.", weight: 0.35, minutes: 12)
                        .EscalatesPack(0.1),
                    new EventResult("Your caution is working. Distance growing.", weight: 0.20, minutes: 20)
                        .EscalatesPack(-0.2)
                ]);
    }

    /// <summary>
    /// Stage 3 of Pack arc - circling.
    /// They're closing in. Need defensible ground or fire NOW.
    /// </summary>
    private static GameEvent Circling(GameContext ctx)
    {
        var packTension = ctx.Tensions.GetTension("PackNearby");
        var predator = packTension?.AnimalType ?? "wolf";

        return new GameEvent("Circling",
            $"The {predator.ToLower()}s are closing the circle. Cutting off escape routes. You need defensible ground — NOW.", 2.0)
            .Requires(EventCondition.PackNearbyHigh, EventCondition.IsExpedition)
            .Choice("Find Defensible Ground",
                "High ground. Choke point. Anything that limits their angles.",
                [
                    new EventResult("Rocky outcrop. Back to stone. They can only come from one direction.", weight: 0.50, minutes: 15)
                        .EscalatesPack(-0.1),
                    new EventResult("Dense thicket. Hard to move but harder for them to coordinate.", weight: 0.30, minutes: 12),
                    new EventResult("Nothing. Open ground. You're exposed.", weight: 0.20, minutes: 10)
                        .EscalatesPack(0.2)
                        .Frightening()
                ])
            .Choice("Back Against Tree or Cliff",
                "Limit attack angles. Nothing behind you.",
                [
                    new EventResult("Solid tree at your back. They can only come from the front.", weight: 0.60, minutes: 8)
                        .EscalatesPack(-0.05),
                    new EventResult("Cliff face. Safe from behind, but nowhere to run.", weight: 0.30, minutes: 10),
                    new EventResult("You're cornered. But so are they, in a way.", weight: 0.10, minutes: 8)
                        .EscalatesPack(0.1)
                ])
            .Choice("Start Fire Here",
                "Right here. Right now. Fire is your only real defense.",
                [
                    new EventResult("Fire catches. Flames push them back. A circle of safety.", weight: 0.45, minutes: 15)
                        .Costs(ResourceType.Tinder, 1)
                        .BurnsFuel(3)
                        .EscalatesPack(-0.4),
                    new EventResult("Small fire. Not enough. But it's something.", weight: 0.30, minutes: 12)
                        .Costs(ResourceType.Tinder, 1)
                        .BurnsFuel(2)
                        .EscalatesPack(-0.2),
                    new EventResult("Won't catch. Hands shaking. They're getting closer.", weight: 0.25, minutes: 10)
                        .Costs(ResourceType.Tinder, 1)
                        .EscalatesPack(0.25)
                        .Terrifying()
                ],
                [EventCondition.HasTinder, EventCondition.HasFuel])
            .Choice("Make Break for Camp",
                "All-out sprint. Succeed or fail.",
                [
                    new EventResult("You run. Legs pumping. Camp in sight!", weight: 0.35, minutes: 10)
                        .EscapeToCamp(),
                    new EventResult("Almost made it. They catch you at the perimeter.", weight: 0.35, minutes: 8)
                        .Encounter(predator, 10, 0.7),
                    new EventResult("Too slow. They drag you down.", weight: 0.30, minutes: 5)
                        .Encounter(predator, 5, 0.9)
                ]);
    }

    /// <summary>
    /// Stage 4 of Pack arc - the pack commits.
    /// They've decided to attack. Multiple attackers.
    /// </summary>
    private static GameEvent ThePackCommits(GameContext ctx)
    {
        var packTension = ctx.Tensions.GetTension("PackNearby");
        var predator = packTension?.AnimalType ?? "Wolf";

        return new GameEvent("The Pack Commits",
            $"They've decided. This is happening. The {predator.ToLower()}s move as one.", 3.0)
            .Requires(EventCondition.PackNearbyCritical, EventCondition.IsExpedition)
            .Choice("Stand and Fight",
                "Face them. Take as many as you can.",
                [
                    new EventResult($"The first {predator.ToLower()} lunges. The fight is on.", weight: 1.0, minutes: 5)
                        .ResolvesPack()
                        .Encounter(predator, 5, 0.85)
                ])
            .Choice("Feed the Fire",
                "Everything on the flames. Make it roar.",
                [
                    new EventResult("Fire blazes high. They stop, blinded. The pack retreats.", weight: 0.50, minutes: 5)
                        .BurnsFuel(4)
                        .ResolvesPack(),
                    new EventResult("Fire grows but they circle. Waiting for it to die.", weight: 0.35, minutes: 10)
                        .BurnsFuel(3)
                        .EscalatesPack(-0.3),
                    new EventResult("Not enough fuel. Fire sputters. They see weakness.", weight: 0.15, minutes: 5)
                        .BurnsFuel(2)
                        .Encounter(predator, 10, 0.75)
                ],
                [EventCondition.NearFire, EventCondition.HasFuel])
            .Choice("Drop All Meat and Flee",
                "Give them what they want. Food. Not you.",
                [
                    new EventResult("You throw everything and run. They take the bait.", weight: 0.65, minutes: 5)
                        .ResolvesPack()
                        .Costs(ResourceType.Food, 5),
                    new EventResult("Most go for the meat. One still chases.", weight: 0.25, minutes: 5)
                        .Costs(ResourceType.Food, 5)
                        .BecomeStalked(0.3, predator),
                    new EventResult("They take the food AND you.", weight: 0.10, minutes: 3)
                        .Costs(ResourceType.Food, 3)
                        .Encounter(predator, 8, 0.8)
                ],
                [EventCondition.HasMeat]);
    }
}
