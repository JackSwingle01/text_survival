using text_survival.Effects;
using text_survival.Environments.Features;

namespace text_survival.Actions;

public static partial class GameEventRegistry
{
    // === SCAVENGER (HYENA) ARC EVENTS ===

    /// <summary>
    /// Awareness event - you notice hyenas in the area.
    /// Creates ScavengersWaiting tension.
    /// </summary>
    private static GameEvent CirclingScavengers(GameContext ctx)
    {
        bool hasMeat = ctx.Inventory.HasMeat;
        var meatClause = hasMeat
            ? " They've noticed what you're carrying."
            : "";

        return new GameEvent("Circling Scavengers",
            $"Movement at the edge of your vision. Hunched shapes with sloped backs. Hyenas.{meatClause}", 0.4)
            .Requires(EventCondition.OnExpedition)
            .RequiresSituation(Situations.ScavengerInTerritory)
            .Excludes(EventCondition.AtCamp)
            .WithConditionFactor(EventCondition.HasMeat, 2.0)
            .WithSituationFactor(Situations.Vulnerable, 1.5)
            .Choice("Shout and Wave",
                "Show them you're not prey. Make yourself big.",
                [
                    new EventResult("They scatter. But they don't go far.", weight: 0.6, minutes: 2)
                        .ScavengersWaiting(0.2),
                    new EventResult("They flinch, then regroup. Patient hunters.", weight: 0.3, minutes: 2)
                        .ScavengersWaiting(0.3),
                    new EventResult("They seem unimpressed. Hyenas have learned humans aren't always dangerous.", weight: 0.1, minutes: 2)
                        .ScavengersWaiting(0.4)
                        .Unsettling()
                ])
            .Choice("Ignore Them",
                "They're scavengers, not hunters. Usually.",
                [
                    new EventResult("They keep their distance. Watching.", weight: 0.5, minutes: 0)
                        .ScavengersWaiting(0.3),
                    new EventResult("More appear. A pack. They're following now.", weight: 0.3, minutes: 0)
                        .ScavengersWaiting(0.5),
                    new EventResult("They drift away. Other opportunities elsewhere.", weight: 0.2, minutes: 0)
                ])
            .Choice("Finish Quickly and Leave",
                "Get what you came for and get out of their territory.",
                [
                    new EventResult("Rushed but done. You leave them behind.", weight: 0.7, minutes: 5)
                        .WithEffects(EffectFactory.Exhausted(0.1, 20)),
                    new EventResult("Hurrying costs you. Left some behind.", weight: 0.2, minutes: 3)
                        .WithEffects(EffectFactory.Exhausted(0.15, 20)),
                    new EventResult("They follow at a distance. Persistent.", weight: 0.1, minutes: 5)
                        .ScavengersWaiting(0.3)
                ]);
    }

    /// <summary>
    /// Confrontation event - hyenas challenge you for a carcass.
    /// </summary>
    private static GameEvent ContestedCarcass(GameContext ctx)
    {
        var carcass = ctx.CurrentLocation.GetFeature<CarcassFeature>();
        var animalName = carcass?.AnimalName.ToLower() ?? "carcass";

        return new GameEvent("Contested Carcass",
            $"The hyenas have grown bold. They circle the {animalName}, yipping and cackling. They want it.", 1.5)
            .Requires(EventCondition.OnExpedition)
            .RequiresSituation(Situations.CarcassContested)
            .WithSituationFactor(Situations.ScavengerPresent, 2.0)
            .Choice("Stand Over the Kill",
                "Plant yourself. This is yours.",
                [
                    new EventResult("Your stance keeps them back. Barely.", weight: 0.5, minutes: 5)
                        .ResolvesScavengers(),
                    new EventResult("The largest one tests you. Snapping jaws, then retreat.", weight: 0.3, minutes: 5)
                        .ResolvesScavengers()
                        .Frightening(),
                    new EventResult("They're not backing down. Blood in the air.", weight: 0.2, minutes: 3)
                        .ConfrontScavengers(15, 0.5)
                ])
            .Choice("Throw Them a Piece",
                "Give them something. Maybe they'll be satisfied.",
                [
                    new EventResult("They fight over the scrap. You work undisturbed.", weight: 0.6, minutes: 5)
                        .WithScavengerLoss(0.1)
                        .ResolvesScavengers(),
                    new EventResult("They want more. You throw another piece.", weight: 0.3, minutes: 8)
                        .WithScavengerLoss(0.2)
                        .ResolvesScavengers(),
                    new EventResult("Feeding them was a mistake. Now they're bolder.", weight: 0.1, minutes: 3)
                        .WithScavengerLoss(0.1)
                        .EscalatesScavengers(0.2)
                ])
            .Choice("Grab What You Have and Go",
                "Take what you've butchered. Leave the rest.",
                [
                    new EventResult("They descend on the carcass. You slip away.", weight: 0.8, minutes: 3)
                        .ResolvesScavengers(),
                    new EventResult("They follow, wanting what you're carrying too.", weight: 0.2, minutes: 3)
                        .ResolvesScavengers()
                        .BecomeStalked(0.2, "Cave Hyena")
                ])
            .Choice("Use Fire",
                "Light a torch. Animals fear fire.",
                [
                    new EventResult("The flames drive them back. They won't come close.", weight: 0.7, minutes: 5)
                        .ResolvesScavengers()
                        .Costs(ResourceType.Fuel, 1),
                    new EventResult("They're wary but not gone. The smell of meat is strong.", weight: 0.3, minutes: 5)
                        .EscalatesScavengers(-0.2)
                        .Costs(ResourceType.Fuel, 1)
                ],
                [EventCondition.HasFuel, EventCondition.HasFirestarter]);
    }

    /// <summary>
    /// Stolen opportunity - you find a carcass the hyenas have been at.
    /// </summary>
    private static GameEvent ThePacksLeavings(GameContext ctx)
    {
        var carcass = ctx.CurrentLocation.GetFeature<CarcassFeature>();
        var animalName = carcass?.AnimalName.ToLower() ?? "animal";
        var isFresh = carcass != null && carcass.DecayLevel < 0.5;
        var leavingsDesc = isFresh
            ? "Fresh kill, but the scavengers got here first. Half-eaten."
            : "Old carcass, picked over by scavengers. Not much left.";

        return new GameEvent("The Pack's Leavings",
            $"You find a {animalName} carcass. {leavingsDesc} Hyena tracks everywhere.", 0.5)
            .Requires(EventCondition.OnExpedition)
            .RequiresSituation(Situations.CarcassPresent)
            .RequiresSituation(Situations.ScavengerInTerritory)
            .Excludes(EventCondition.IsButchering)
            .Choice("Salvage What's Left",
                "Some meat is better than none.",
                [
                    new EventResult("You cut away what the hyenas left. It's something.", weight: 0.6, minutes: 20)
                        .ModerateBloody()
                        .WithScavengerLoss(0.4),
                    new EventResult("Bones and scraps mostly. The hyenas were thorough.", weight: 0.3, minutes: 15)
                        .WithScavengerLoss(0.6),
                    new EventResult("The pack returns while you're working.", weight: 0.1, minutes: 10)
                        .ScavengersWaiting(0.5)
                ])
            .Choice("Track the Scavengers",
                "Follow them. Maybe they know where the hunting is good.",
                [
                    new EventResult("Their trail leads toward wolf territory. They're following a pack.", weight: 0.5, minutes: 25)
                        .CreateTension("FreshTrail", 0.4, animalType: "Wolf"),
                    new EventResult("You find where they're denning. Good to know.", weight: 0.3, minutes: 30)
                        .MarksDiscovery("Hyena den location", 0.4),
                    new EventResult("Lost the trail. They move fast.", weight: 0.2, minutes: 20)
                ])
            .Choice("Mark Location and Leave",
                "Remember this. Carcasses attract scavengers.",
                [
                    new EventResult("You note the location. Hyena territory.", weight: 1.0, minutes: 3)
                        .MarksDiscovery("Hyena feeding ground", 0.3)
                ]);
    }

    /// <summary>
    /// Vulnerability probe - hyenas test if you're weak enough to attack.
    /// Happens at night when player is vulnerable.
    /// </summary>
    private static GameEvent Opportunists(GameContext ctx)
    {
        double vulnLevel = Situations.VulnerabilityLevel(ctx);
        var vulnDesc = vulnLevel > 0.5
            ? "They've been watching. They see you struggle."
            : "Eyes in the darkness. Calculating.";

        return new GameEvent("Opportunists",
            $"The hyenas are closer now. {vulnDesc} They're testing your strength.", 1.0)
            .Requires(EventCondition.OnExpedition)
            .Requires(EventCondition.Night)
            .RequiresSituation(Situations.ScavengerInTerritory)
            .RequiresSituation(Situations.Vulnerable)
            .Excludes(EventCondition.AtCamp)
            .WithSituationFactor(Situations.Vulnerable, 2.0)
            .Choice("Show No Weakness",
                "Stand tall. Move confidently. Don't let them see you falter.",
                [
                    new EventResult("Your posture convinces them. For now.", weight: 0.6, minutes: 2)
                        .ResolvesScavengers(),
                    new EventResult("They're not convinced. Still following.", weight: 0.3, minutes: 2)
                        .ScavengersWaiting(0.3),
                    new EventResult("The pack's alpha approaches. Testing.", weight: 0.1, minutes: 2)
                        .ConfrontScavengers(12, 0.4)
                ])
            .Choice("Back Toward Camp Slowly",
                "Don't run. But get closer to safety.",
                [
                    new EventResult("Step by step. They don't follow into the firelight.", weight: 0.7, minutes: 15)
                        .ResolvesScavengers(),
                    new EventResult("They pace you. Waiting for you to stumble.", weight: 0.2, minutes: 15)
                        .ScavengersWaiting(0.4),
                    new EventResult("One darts in. Testing.", weight: 0.1, minutes: 10)
                        .MinorBite()
                        .ResolvesScavengers()
                ])
            .Choice("Drop Something",
                "Throw down food or gear. Create a distraction.",
                [
                    new EventResult("They investigate the dropped item. You gain distance.", weight: 0.8, minutes: 5)
                        .ResolvesScavengers()
                        .Costs(ResourceType.Food, 1),
                    new EventResult("They snatch it and keep coming.", weight: 0.2, minutes: 3)
                        .Costs(ResourceType.Food, 1)
                        .EscalatesScavengers(0.1)
                ],
                [EventCondition.HasFood])
            .Choice("Make Fire NOW",
                "Light something. Anything. Fire means safety.",
                [
                    new EventResult("Flames leap up. The hyenas scatter.", weight: 0.7, minutes: 8)
                        .ResolvesScavengers()
                        .Costs(ResourceType.Fuel, 1)
                        .Costs(ResourceType.Tinder, 1),
                    new EventResult("Fumbling in the dark. Finally catches. They back off.", weight: 0.2, minutes: 12)
                        .ResolvesScavengers()
                        .Costs(ResourceType.Fuel, 1)
                        .Costs(ResourceType.Tinder, 1)
                        .Frightening(),
                    new EventResult("Can't get it lit. The sparks attract them.", weight: 0.1, minutes: 5)
                        .Costs(ResourceType.Tinder, 1)
                        .EscalatesScavengers(0.3)
                ],
                [EventCondition.HasFuel, EventCondition.HasFirestarter]);
    }

    /// <summary>
    /// Three-way competition - wolves, hyenas, and player compete for a carcass.
    /// Complex tactical decision.
    /// </summary>
    private static GameEvent ScavengersGambit(GameContext ctx)
    {
        var carcass = ctx.CurrentLocation.GetFeature<CarcassFeature>();
        var animalName = carcass?.AnimalName.ToLower() ?? "prey";

        return new GameEvent("Scavenger's Gambit",
            $"A {animalName} carcass. Wolves feed. Hyenas circle, waiting. You're the third faction in this stand-off.", 0.8)
            .Requires(EventCondition.OnExpedition)
            .Requires(EventCondition.PackNearby)
            .RequiresSituation(Situations.FreshCarcassPresent)
            .RequiresSituation(Situations.ScavengerWolfDynamics)
            .Choice("Wait for Wolves to Leave",
                "Patience. Let the wolves eat their fill and move on.",
                [
                    new EventResult("The wolves finish, move off. Hyenas dart in. You wait longer.", weight: 0.4, minutes: 45)
                        .ResolvesPack()
                        .WithScavengerLoss(0.3),
                    new EventResult("Wolves leave. You reach the carcass before the hyenas.", weight: 0.3, minutes: 40)
                        .ResolvesPack()
                        .ScavengersWaiting(0.3),
                    new EventResult("They're taking forever. Night is coming.", weight: 0.2, minutes: 60)
                        .EscalatesPack(0.1),
                    new EventResult("A second pack arrives. The situation gets complicated.", weight: 0.1, minutes: 30)
                        .EscalatesPack(0.3)
                        .EscalatesScavengers(0.2)
                ])
            .Choice("Spook the Wolves",
                "Make noise. Maybe they'll abandon the kill.",
                [
                    new EventResult("They startle, grab what they can, flee. Hyenas rush in. You're left with scraps.", weight: 0.4, minutes: 10)
                        .ResolvesPack()
                        .WithScavengerLoss(0.5),
                    new EventResult("Wolves retreat but stay close. Watching. You have a window.", weight: 0.3, minutes: 10)
                        .EscalatesPack(-0.2)
                        .ScavengersWaiting(0.2),
                    new EventResult("The wolves don't spook. Now they're looking at YOU.", weight: 0.2, minutes: 5)
                        .EscalatesPack(0.3),
                    new EventResult("You draw the attention of both packs. Bad idea.", weight: 0.1, minutes: 5)
                        .EscalatesPack(0.4)
                        .EscalatesScavengers(0.3)
                        .Frightening()
                ])
            .Choice("Steal While They're Distracted",
                "Wolves watch hyenas. Hyenas watch wolves. You watch the carcass.",
                [
                    new EventResult("You slice off a piece and slip away. Neither pack notices.", weight: 0.3, minutes: 15)
                        .FindsMeat()
                        .MinorBloody(),
                    new EventResult("Got something. Hyenas saw you. Following now.", weight: 0.3, minutes: 10)
                        .FindsMeat()
                        .MinorBloody()
                        .ScavengersWaiting(0.4),
                    new EventResult("Quick work. The wolves notice you leaving.", weight: 0.2, minutes: 10)
                        .FindsMeat()
                        .MinorBloody()
                        .BecomeStalked(0.2, "Wolf"),
                    new EventResult("Everyone noticed. Time to run.", weight: 0.2, minutes: 5)
                        .Frightening()
                        .EscalatesPack(0.2)
                        .EscalatesScavengers(0.2)
                ])
            .Choice("Let Them Have It",
                "Not worth the risk. Leave them to fight over it.",
                [
                    new EventResult("You fade back. The natural order continues without you.", weight: 1.0, minutes: 5)
                        .ResolvesPack()
                        .ResolvesScavengers()
                ]);
    }
}
