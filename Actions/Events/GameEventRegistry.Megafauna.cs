using text_survival.Actions.Expeditions;
using text_survival.Actors.Animals;
using text_survival.Bodies;
using text_survival.Effects;
using text_survival.Environments.Features;
using text_survival.Items;

namespace text_survival.Actions;

public static partial class GameEventRegistry
{
    // === MEGAFAUNA HUNT ARC EVENTS ===
    // These events integrate with the persistent mammoth herd system.

    /// <summary>
    /// Stage 1: Discovery - Player hears mammoth calls near the herd territory.
    /// Creates initial MammothTracked tension. Now territory-aware.
    /// </summary>
    internal static GameEvent DistantTrumpeting(GameContext ctx)
    {
        var herd = Situations.GetMammothHerd(ctx);
        var herdDesc = herd?.State switch
        {
            HerdState.Grazing => "The calls have the rhythm of content feeding.",
            HerdState.Alert => "The calls sound agitated. Something has spooked them.",
            HerdState.Fleeing => "Urgent trumpeting. The herd is on the move, fast.",
            _ => "The sound is distant but unmistakable."
        };

        return new GameEvent("Distant Trumpeting",
            $"A deep, resonant call echoes across the valley. Mammoth. {herdDesc} " +
            "Following it means committing time, resources. But the rewards...", 1.0)
            .RequiresSituation(ctx => Situations.NearMammothHerd(ctx) || Situations.InMammothTerritory(ctx))
            .WithSituationFactor(Situations.SupplyPressure, 1.5)
            .Choice("Follow the Sound",
                "Track it down now while the trail is fresh.",
                [
                    new EventResult("You find fresh tracks. Massive. The snow tells a story of where it went.", weight: 0.55, minutes: 30)
                        .MammothTracked(0.4),
                    new EventResult("The tracks lead toward rocky ground. Harder to follow, but you mark the direction.", weight: 0.30, minutes: 35)
                        .MammothTracked(0.3),
                    new EventResult("Another call, closer now. You catch a glimpse of dark fur through the trees.", weight: 0.15, minutes: 25)
                        .MammothTracked(0.5)
                ])
            .Choice("Mark It for Later",
                "Note the direction. Return when better prepared.",
                [
                    new EventResult("You mark the treeline with a blaze. The mammoth was heading northeast.", weight: 1.0, minutes: 10)
                        .MammothTracked(0.2)
                ])
            .Choice("Too Risky Now",
                "Mammoths are dangerous. Not worth it yet.",
                [
                    new EventResult("You let the call fade into the distance. Another time, perhaps.", weight: 1.0)
                ]);
    }

    /// <summary>
    /// Stage 2: Escalation - Player tracks the mammoth, finds definitive evidence.
    /// Escalates MammothTracked tension. Requires preparation choice.
    /// </summary>
    internal static GameEvent FreshSpoor(GameContext ctx)
    {
        var mammothTension = ctx.Tensions.GetTension("MammothTracked");
        var confidence = mammothTension?.Severity ?? 0.4;

        var spoorDescription = confidence > 0.4
            ? "The spoor is fresh—less than a day old. Steam still rises from the massive dung pile. " +
              "You find where it stripped bark from a birch, tusks leaving deep gouges. It's close."
            : "Tracks, but older. Two, maybe three days. The mammoth passed through here but kept moving.";

        return new GameEvent("Fresh Spoor",
            spoorDescription + " " +
            "Following further means investing serious time. You'll need to build a position, watch its patterns. " +
            "This isn't a deer—you don't just stumble on a mammoth and throw a spear.", 1.2)
            .Requires(EventCondition.MammothTracked)  // Need some tracking progress
            .WithSituationFactor(Situations.SupplyPressure, 0.7)  // Less likely when desperate (need prep time)
            .Choice("Press On Now",
                "The trail is warm. Stay on it, don't let it go cold.",
                [
                    new EventResult("You follow for hours. Finally: a clearing where it's been grazing. Huge tracks everywhere.", weight: 0.50, minutes: 35)
                        .Escalate("MammothTracked", 0.3),
                    new EventResult("The tracks lead into dense forest. You push through, driven.", weight: 0.30, minutes: 40)
                        .Escalate("MammothTracked", 0.25)
                        .Damage(0.10, DamageType.Sharp),  // Thorns/branches
                    new EventResult("Fresh dung, still warm. It was here minutes ago. You're very close now.", weight: 0.20, minutes: 30)
                        .Escalate("MammothTracked", 0.4)
                ])
            .Choice("Build Observation Post",
                "Set up a hidden position. Watch its movements over time. (~90 min, costs materials)",
                [
                    new EventResult("You construct a blind using fallen logs and snow. Hours pass watching. " +
                                  "Finally: it emerges to drink. You know its pattern now.", weight: 0.65, minutes: 90)
                        .Costs(ResourceType.Fuel, 4)
                        .Escalate("MammothTracked", 0.35),
                    new EventResult("The blind gives you perfect sightlines. You watch the massive bull for hours. " +
                                  "It's old, scarred. Alone. Vulnerable.", weight: 0.25, minutes: 95)
                        .Costs(ResourceType.Fuel, 4)
                        .Escalate("MammothTracked", 0.4),
                    new EventResult("You build the position but the mammoth doesn't show. Still, you've learned the terrain.", weight: 0.10, minutes: 100)
                        .Costs(ResourceType.Fuel, 4)
                        .Escalate("MammothTracked", 0.2)
                ],
                [EventCondition.HasSticks])
            .Choice("Return Tomorrow",
                "You need more preparation. Better weapon, more supplies.",
                [
                    new EventResult("You withdraw. The trail will still be here.", weight: 1.0, minutes: 10)
                ]);
    }

    /// <summary>
    /// Stage 3: Confrontation - Player approaches with high tension, commits to the hunt.
    /// Gates the actual encounter spawn. High risk decision point.
    /// </summary>
    internal static GameEvent TheBull(GameContext ctx)
    {
        var mammothTension = ctx.Tensions.GetTension("MammothTracked");
        var hasWeapon = ctx.Inventory.Weapon != null;

        var readinessDescription = hasWeapon
            ? "Your spear feels inadequate for what's ahead, but it's the best you have."
            : "You have no proper weapon. This is madness.";

        return new GameEvent("The Bull",
            "You find it: an old bull, separated from the herd. Massive beyond anything you've hunted. " +
            "Shaggy dark fur hangs in matted sheets. Curved tusks as thick as your thigh. " +
            "It grazes in a clearing ringed by rocks—it chose defensive ground. Smart. " +
            readinessDescription + " " +
            "This is the moment. Attack now, or prepare more carefully.", 2.0)
            .Requires(EventCondition.MammothTrackedHigh)  // Need high tension
            .WithSituationFactor(Situations.SupplyPressure, 1.5)  // Desperation drives commitment
            .Choice("Attack Now",
                "It's alone, vulnerable. You'll never get a better chance.",
                [
                    new EventResult("You advance. It turns, sensing you. Trunks raise. Here it comes.", weight: 0.50)
                        .Encounter("Mammoth", distance: 40, boldness: 0.6)
                        .ResolveTension("MammothTracked"),
                    new EventResult("You rush forward—it bolts! Too alert. You only manage a glancing throw as it crashes into the forest.", weight: 0.30, minutes: 20)
                        .ResolveTension("MammothTracked"),
                    new EventResult("As you close in, the bull charges! Not isolated—a decoy. The herd was behind you!", weight: 0.20)
                        .Encounter("Mammoth", distance: 20, boldness: 0.8)
                        .ResolveTension("MammothTracked")
                ])
            .Choice("Set Trap with Bait",
                "Use meat to lure it into position. Ambush from the rocks. (~30 min wait, costs meat)",
                [
                    new EventResult("The trap works. It approaches the bait, head down, distracted. Perfect.", weight: 0.70, minutes: 35)
                        .Costs(ResourceType.Food, 4)
                        .Encounter("Mammoth", distance: 25, boldness: 0.4)
                        .ResolveTension("MammothTracked"),
                    new EventResult("It approaches cautiously, stops short of the bait. Wary. You attack from position anyway.", weight: 0.20, minutes: 30)
                        .Costs(ResourceType.Food, 4)
                        .Encounter("Mammoth", distance: 35, boldness: 0.5)
                        .ResolveTension("MammothTracked"),
                    new EventResult("It ignores the bait entirely. The meat is wasted. You approach openly instead.", weight: 0.10, minutes: 25)
                        .Costs(ResourceType.Food, 4)
                        .Encounter("Mammoth", distance: 40, boldness: 0.6)
                        .ResolveTension("MammothTracked")
                ],
                [EventCondition.HasCookedMeat])
            .Choice("Withdraw",
                "This is suicide. You're not ready yet.",
                [
                    new EventResult("You back away slowly. The mammoth never knew you were there. " +
                                  "The opportunity passes. The tension fades.", weight: 1.0)
                        .ResolveTension("MammothTracked")
                ]);
    }

    // === COMPOUND PRESSURE EVENTS ===

    /// <summary>
    /// Weather escalates during active mammoth hunt. Creates compounding pressure.
    /// </summary>
    internal static GameEvent ColdSnapDuringHunt(GameContext ctx)
    {
        return new GameEvent("Cold Snap During Hunt",
            "The wind shifts. Suddenly it's much colder—biting, deadly cold. " +
            "Your breath comes in white plumes. You're far from your fire, tracking the mammoth. " +
            "The cold doesn't care about your hunt.", 1.5)
            .Requires(EventCondition.MammothTracked)
            .Requires(EventCondition.OnExpedition)  // Must be away from camp
            .Choice("Abandon Hunt, Return to Fire",
                "The mammoth can wait. Freezing can't.",
                [
                    new EventResult("You hurry back toward camp. The hunt is lost, but you'll survive.", weight: 1.0, minutes: 5)
                        .ResolveTension("MammothTracked")
                        .CreateTension("DeadlyCold", 0.4)
                ])
            .Choice("Build Emergency Fire Here",
                "Make a fire. Resume tracking once you've warmed up.",
                [
                    new EventResult("You get a fire going. It's small, inefficient, but it's warmth. " +
                                  "The mammoth's trail may be colder when you resume.", weight: 0.70, minutes: 35)
                        .Costs(ResourceType.Fuel, 6)
                        .Costs(ResourceType.Tinder, 2)
                        .Escalate("MammothTracked", -0.2),
                    new EventResult("The fire struggles in the wind but catches. You huddle close.", weight: 0.30, minutes: 40)
                        .Costs(ResourceType.Fuel, 8)
                        .Costs(ResourceType.Tinder, 3)
                        .Escalate("MammothTracked", -0.15)
                ],
                [EventCondition.HasSticks, EventCondition.HasTinder])
            .Choice("Push Through",
                "Endure it. The mammoth is close.",
                [
                    new EventResult("You grit your teeth and continue. The cold is excruciating but the trail remains warm.", weight: 0.50, minutes: 15)
                        .WithEffects(EffectFactory.Shivering(0.6))
                        .Escalate("MammothTracked", 0.1),
                    new EventResult("Your fingers go numb. Your body screams at you. But you don't stop.", weight: 0.30, minutes: 20)
                        .WithEffects(EffectFactory.Shivering(0.8))
                        .Damage(0.12, DamageType.Internal),
                    new EventResult("Too cold. You can't feel your extremities anymore. This was a mistake.", weight: 0.20, minutes: 10)
                        .WithEffects(EffectFactory.Hypothermia(0.5))
                        .CreateTension("DeadlyCold", 0.6)
                        .Escalate("MammothTracked", -0.3)
                ]);
    }

    /// <summary>
    /// Wolves smell blood during mammoth butchering. Competition for the kill.
    /// </summary>
    internal static GameEvent WolvesSmellBlood(GameContext ctx)
    {
        return new GameEvent("Wolves Smell Blood",
            "You're elbow-deep in the mammoth carcass, working as fast as you can. " +
            "Then you hear it: howls. Close. They've smelled the blood, and they're coming.", 1.8)
            .Requires(EventCondition.FoodScentStrong)
            .Choice("Work Faster, Grab What You Can",
                "Take the best cuts and run. Leave the rest.",
                [
                    new EventResult("You cut free what you can carry and bolt. The howls are very close now.", weight: 0.60, minutes: 8)
                        .FindsLargeMeat()
                        .BecomeStalked(0.4, "Wolf"),
                    new EventResult("Knife slips in your haste—you cut yourself. But you get clear.", weight: 0.25, minutes: 10)
                        .FindsMeat()
                        .Damage(0.15, DamageType.Sharp)
                        .WithEffects(EffectFactory.Bleeding(0.3))
                        .BecomeStalked(0.5, "Wolf"),
                    new EventResult("Too slow. They're here. You drop everything and run.", weight: 0.15, minutes: 5)
                        .BecomeStalked(0.6, "Wolf")
                ])
            .Choice("Defend the Kill",
                "This is YOUR kill. Stand your ground.",
                [
                    new EventResult("You stand over the carcass, weapon ready. The pack circles but doesn't commit. " +
                                  "Eventually they withdraw. The kill is yours.", weight: 0.40, minutes: 20)
                        .FindsMassiveMeat()  // Full mammoth yield
                        .ResolveTension("FoodScentStrong"),
                    new EventResult("They test you. One rushes in—you drive it back. They keep circling. Waiting.", weight: 0.35, minutes: 25)
                        .FindsLargeMeat()
                        .CreateTension("PackNearby", 0.6),
                    new EventResult("There are too many. They swarm. You fight them off the carcass.", weight: 0.25)
                        .FindsMeat()
                        .Encounter("Wolf", distance: 15, boldness: 0.7)
                ]);
    }

    // === PERSISTENT HERD INTEGRATION ===

    /// <summary>
    /// Peaceful sighting - the mammoth herd is grazing/resting and player observes.
    /// Creates opportunity without immediate danger.
    /// </summary>
    internal static GameEvent TheHerd(GameContext ctx)
    {
        var herd = Situations.GetMammothHerd(ctx);
        int count = herd?.Count ?? 8;
        var countDesc = count switch
        {
            <= 4 => "a small family group",
            <= 8 => "a full family",
            <= 12 => "a large family group",
            _ => "a massive gathering"
        };

        return new GameEvent("The Herd",
            $"You crest a rise and freeze. Below: {countDesc} of woolly mammoths. " +
            "Adults, young, a matriarch with tusks curved like twin moons. " +
            "They haven't seen you. The wind is in your favor.", 0.8)
            .Requires(EventCondition.OnExpedition)
            .RequiresSituation(Situations.MammothHerdPresent)
            .RequiresSituation(ctx => {
                var h = Situations.GetMammothHerd(ctx);
                return h != null && (h.State == HerdState.Grazing || h.State == HerdState.Resting);
            })
            .WithConditionFactor(EventCondition.GoodVisibility, 1.5)
            .Choice("Scout from Cover",
                "Watch. Learn their patterns. Stay hidden.",
                [
                    new EventResult("You observe for hours. Their routes, their habits. Knowledge for the hunt.", weight: 0.6, minutes: 60)
                        .MammothTracked(0.5)
                        .MarksDiscovery("Mammoth grazing patterns", 0.6),
                    new EventResult("A young one wanders close. The matriarch watches, but you stay still.", weight: 0.3, minutes: 45)
                        .MammothTracked(0.6)
                        .AlertsHerd(0.3),
                    new EventResult("Something alerts them. They don't flee, but now they're watchful.", weight: 0.1, minutes: 30)
                        .MammothTracked(0.4)
                        .AlertsHerd(0.6)
                ])
            .Choice("Approach Slowly",
                "Get closer. Risk detection for better information.",
                [
                    new EventResult("Step by careful step. You're close enough to see their breath.", weight: 0.4, minutes: 30)
                        .MammothTracked(0.6)
                        .AlertsHerd(0.4),
                    new EventResult("The matriarch spots you. She raises her trunk, testing the air.", weight: 0.4, minutes: 25)
                        .MammothTracked(0.5)
                        .AlertsHerd(0.7)
                        .Frightening(),
                    new EventResult("You step on a branch. The CRACK echoes. Every head turns.", weight: 0.2, minutes: 20)
                        .AlertsHerd(0.9)
                        .Unsettling()
                ])
            .Choice("Mark Location and Leave",
                "You've found them. That's enough for today.",
                [
                    new EventResult("You note the terrain, the approaches. A hunt begins in the mind.", weight: 1.0, minutes: 10)
                        .MammothTracked(0.3)
                        .MarksDiscovery("Mammoth herd location", 0.5)
                ]);
    }

    /// <summary>
    /// Matriarch's warning - the herd is alert and player is detected.
    /// Standoff where player chooses confrontation or retreat.
    /// </summary>
    internal static GameEvent TheMatriarchsWarning(GameContext ctx)
    {
        return new GameEvent("The Matriarch's Warning",
            "The matriarch faces you directly. Ears spread wide. Trunk raised, testing. " +
            "The message is clear: you've been noticed. " +
            "Behind her, the herd shifts uneasily. Waiting for her signal.", 1.5)
            .Requires(EventCondition.OnExpedition)
            .RequiresSituation(Situations.MammothHerdPresent)
            .RequiresSituation(Situations.MammothHerdAggravated)
            .Choice("Back Away Slowly",
                "Show submission. Leave their space.",
                [
                    new EventResult("Step by careful step, you retreat. The matriarch watches but doesn't pursue.", weight: 0.7, minutes: 10)
                        .ResolvesMammothTracking(),
                    new EventResult("As you move back, she trumpets once. Warning to stay away.", weight: 0.25, minutes: 8)
                        .ResolvesMammothTracking()
                        .Unsettling(),
                    new EventResult("A young bull stamps forward. The matriarch stops him with a touch. Close call.", weight: 0.05, minutes: 5)
                        .ResolvesMammothTracking()
                        .Frightening()
                ])
            .Choice("Hold Your Ground",
                "Don't advance, don't retreat. Assert yourself.",
                [
                    new EventResult("A frozen moment. You stare each other down. Finally, she turns away.", weight: 0.4, minutes: 5)
                        .EscalatesMammothTracking(0.1),
                    new EventResult("She takes a step toward you. Testing. You don't flinch.", weight: 0.35, minutes: 5)
                        .EscalatesMammothTracking(0.15)
                        .Frightening(),
                    new EventResult("Your stance provokes her. She charges.", weight: 0.25, minutes: 2)
                        .MammothCharge()
                ])
            .Choice("Use Fire or Torch",
                "Fire impresses all animals. Even these.",
                [
                    new EventResult("The flames make her pause. She trumpets and the herd withdraws.", weight: 0.6, minutes: 5)
                        .Costs(ResourceType.Fuel, 1)
                        .TriggersHerdFlee(),
                    new EventResult("She's wary of the fire but doesn't flee. Standoff continues.", weight: 0.3, minutes: 5)
                        .Costs(ResourceType.Fuel, 1),
                    new EventResult("Fire means nothing to a matriarch protecting her herd. She comes anyway.", weight: 0.1, minutes: 2)
                        .Costs(ResourceType.Fuel, 1)
                        .MammothCharge()
                ],
                [EventCondition.HasFuel, EventCondition.HasFirestarter]);
    }

    /// <summary>
    /// The herd is moving - migration or relocation.
    /// Player can follow or let them go.
    /// </summary>
    internal static GameEvent TheHerdMoves(GameContext ctx)
    {
        var herd = Situations.GetMammothHerd(ctx);
        var direction = herd?.Position.X < (ctx.Map?.CurrentPosition.X ?? 0) ? "west" : "east";

        return new GameEvent("The Herd Moves",
            $"Dust rises in the distance. The mammoth herd is moving, heading {direction}. " +
            "The matriarch leads, calves in the center, bulls on the flanks. " +
            "They're covering ground fast. If you want to follow, you need to decide now.", 0.5)
            .Requires(EventCondition.OnExpedition)
            .RequiresSituation(Situations.NearMammothHerd)
            .RequiresSituation(ctx => !Situations.MammothHerdPresent(ctx))  // Nearby but not on tile
            .WithConditionFactor(EventCondition.GoodVisibility, 2.0)
            .Choice("Follow from a Distance",
                "Track them. Stay far enough back to avoid detection.",
                [
                    new EventResult("You match their pace, staying in cover. Their destination becomes clear.", weight: 0.5, minutes: 45)
                        .MammothTracked(0.5)
                        .WithEffects(EffectFactory.Exhausted(0.15, 30)),
                    new EventResult("Hard to keep up without being seen. You lose them in rough terrain.", weight: 0.3, minutes: 50)
                        .MammothTracked(0.3)
                        .WithEffects(EffectFactory.Exhausted(0.2, 35)),
                    new EventResult("Perfect tracking. You find their new grazing ground.", weight: 0.2, minutes: 40)
                        .MammothTracked(0.6)
                        .MarksDiscovery("Mammoth migration route", 0.5)
                ])
            .Choice("Let Them Go",
                "They'll return. Or you'll find them again.",
                [
                    new EventResult("The herd disappears over the horizon. You note their heading.", weight: 1.0, minutes: 5)
                        .MarksDiscovery("Mammoth migration direction", 0.3)
                ]);
    }

    /// <summary>
    /// The mammoth charges - defensive attack by the herd.
    /// This is triggered when player provokes the alert herd.
    /// </summary>
    internal static GameEvent TheCharge(GameContext ctx)
    {
        return new GameEvent("The Charge",
            "A sound like thunder. Tusks gleaming. Thousands of pounds of muscle and fury. " +
            "The mammoth is charging.", 3.0)
            .Requires(EventCondition.OnExpedition)
            .RequiresSituation(Situations.MammothHerdPresent)
            .RequiresSituation(Situations.MammothHerdAggravated)
            .WithSituationFactor(Situations.Vulnerable, 2.0)
            .Choice("Dodge and Strike",
                "Use its momentum against it. Get a hit in as it passes.",
                [
                    new EventResult("You dive left. Your spear bites deep into its flank as it thunders past.", weight: 0.35, minutes: 3)
                        .KillsMammoth()
                        .Damage(0.20, DamageType.Blunt),
                    new EventResult("Glancing blow. It staggers but keeps going. The herd flees.", weight: 0.35, minutes: 3)
                        .TriggersHerdFlee()
                        .MammothTracked(0.7)
                        .Damage(0.15, DamageType.Blunt),
                    new EventResult("You're not fast enough. Impact.", weight: 0.20, minutes: 2)
                        .Damage(0.45, DamageType.Blunt)
                        .TriggersHerdFlee()
                        .Panicking(),
                    new EventResult("Perfect timing. Your weapon finds a vital spot. It goes down.", weight: 0.10, minutes: 3)
                        .KillsMammoth()
                ],
                [EventCondition.HasWeapon])
            .Choice("Dive Behind Cover",
                "Find something solid. Put it between you and the mammoth.",
                [
                    new EventResult("Behind a boulder. The impact shakes the ground. But you're safe.", weight: 0.5, minutes: 3)
                        .TriggersHerdFlee()
                        .Frightening(),
                    new EventResult("Not enough cover. It clips you as it passes.", weight: 0.35, minutes: 3)
                        .Damage(0.25, DamageType.Blunt)
                        .TriggersHerdFlee(),
                    new EventResult("The cover holds. You hear the herd retreating.", weight: 0.15, minutes: 3)
                        .TriggersHerdFlee()
                ])
            .Choice("Stand and Yell",
                "Don't flinch. Bluff charges can be stopped.",
                [
                    new EventResult("It pulls up short. Ears spread. Then turns and leads the herd away.", weight: 0.3, minutes: 2)
                        .TriggersHerdFlee()
                        .Terrifying(),
                    new EventResult("Not a bluff. It keeps coming.", weight: 0.5, minutes: 2)
                        .Damage(0.40, DamageType.Blunt)
                        .TriggersHerdFlee()
                        .Panicking(),
                    new EventResult("Your courage impresses even a mammoth. It stops, snorts, retreats.", weight: 0.2, minutes: 2)
                        .TriggersHerdFlee()
                        .Frightening()
                        .MarksDiscovery("Survived mammoth charge", 0.7)
                ]);
    }
}
