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

    /// <summary>
    /// Stage 1: Discovery - Player scouts and finds mammoth signs.
    /// Creates initial MammothTracked tension. Low risk, creates opportunity.
    /// </summary>
    private static GameEvent DistantTrumpeting(GameContext ctx)
    {
        return new GameEvent("Distant Trumpeting",
            "A deep, resonant call echoes across the valley. Mammoth. " +
            "The sound is distant but unmistakable—a bull, likely separated from the herd. " +
            "Following it means committing time, resources. But the rewards...", 1.0)
            .WithSituationFactor(Situations.SupplyPressure, 1.5)  // More likely when desperate
            .Choice("Follow the Sound",
                "Track it down now while the trail is fresh.",
                [
                    new EventResult("You find fresh tracks. Massive. The snow tells a story of where it went.", weight: 0.55, minutes: 30)
                        .CreateTension("MammothTracked", 0.4),
                    new EventResult("The tracks lead toward rocky ground. Harder to follow, but you mark the direction.", weight: 0.30, minutes: 35)
                        .CreateTension("MammothTracked", 0.3),
                    new EventResult("Another call, closer now. You catch a glimpse of dark fur through the trees.", weight: 0.15, minutes: 25)
                        .CreateTension("MammothTracked", 0.5)
                ])
            .Choice("Mark It for Later",
                "Note the direction. Return when better prepared.",
                [
                    new EventResult("You mark the treeline with a blaze. The mammoth was heading northeast.", weight: 1.0, minutes: 10)
                        .CreateTension("MammothTracked", 0.2)
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
    private static GameEvent FreshSpoor(GameContext ctx)
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
                        .Damage(4, DamageType.Sharp),  // Thorns/branches
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
    private static GameEvent TheBull(GameContext ctx)
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
    private static GameEvent ColdSnapDuringHunt(GameContext ctx)
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
                        .Damage(5, DamageType.Internal),
                    new EventResult("Too cold. You can't feel your extremities anymore. This was a mistake.", weight: 0.20, minutes: 10)
                        .WithEffects(EffectFactory.Hypothermia(0.5))
                        .CreateTension("DeadlyCold", 0.6)
                        .Escalate("MammothTracked", -0.3)
                ]);
    }

    /// <summary>
    /// Wolves smell blood during mammoth butchering. Competition for the kill.
    /// </summary>
    private static GameEvent WolvesSmellBlood(GameContext ctx)
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
                        .Damage(6, DamageType.Sharp)
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
}
