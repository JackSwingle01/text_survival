using text_survival.Bodies;
using text_survival.Effects;
using text_survival.Environments.Features;
using text_survival.Items;

namespace text_survival.Actions;

public static partial class GameEventRegistry
{
    // === COLD SNAP ARC EVENTS ===

    /// <summary>
    /// Stage 1 of Cold Snap arc - temperature drops dangerously.
    /// Player must choose: race for camp, find terrain shelter, or build emergency shelter.
    /// </summary>
    private static GameEvent TheWindShifts(GameContext ctx)
    {
        return new GameEvent("The Wind Shifts",
            "Temperature plummeting. Wind cutting through everything. This isn't normal cold — this is killing cold.", 1.0)
            .Requires(EventCondition.OnExpedition)
            .Requires(EventCondition.ExtremelyCold)
            .MoreLikelyIf(EventCondition.IsBlizzard, 3.0)
            .MoreLikelyIf(EventCondition.Injured, 2.0)
            .MoreLikelyIf(EventCondition.LowOnFuel, 1.5)
            .Choice("Run for Camp",
                "Sprint back. Every second counts.",
                [
                    new EventResult("You run. The cold bites deep. Camp feels impossibly far.", weight: 0.60, minutes: 5)
                        .CreateTension("DeadlyCold", 0.4)
                        .WithEffects(EffectFactory.Cold(-10, 30)),
                    new EventResult("You run hard. Body heat from exertion helps, for now.", weight: 0.30, minutes: 5)
                        .CreateTension("DeadlyCold", 0.3)
                        .WithEffects(EffectFactory.Cold(-5, 20)),
                    new EventResult("You stumble in your haste. Time lost.", weight: 0.10, minutes: 8)
                        .CreateTension("DeadlyCold", 0.5)
                        .WithEffects(EffectFactory.Cold(-12, 35))
                        .Damage(3, DamageType.Blunt, "fall")
                ])
            .Choice("Find Terrain Shelter",
                "Look for a windbreak. Rocks, dense trees, anything.",
                [
                    new EventResult("You find a rock overhang. Wind blocked, but still dangerously cold.", weight: 0.50, minutes: 15)
                        .CreateTension("DeadlyCold", 0.25)
                        .WithEffects(EffectFactory.Cold(-5, 45)),
                    new EventResult("Dense trees break the wind. You hunker down.", weight: 0.30, minutes: 20)
                        .CreateTension("DeadlyCold", 0.2)
                        .WithEffects(EffectFactory.Cold(-3, 30)),
                    new EventResult("Nothing. Open ground. You're exposed.", weight: 0.20, minutes: 15)
                        .CreateTension("DeadlyCold", 0.5)
                        .WithEffects(EffectFactory.Cold(-15, 45))
                ])
            .Choice("Build Emergency Shelter",
                "Dig in. Snow walls, debris pile, anything to stop the wind.",
                [
                    new EventResult("Hands numb, you pile snow into a windbreak. It helps.", weight: 0.60, minutes: 30)
                        .CreateTension("DeadlyCold", 0.15)
                        .WithEffects(EffectFactory.Cold(-5, 60), EffectFactory.Frostbite(0.2)),
                    new EventResult("Crude shelter complete. You're out of the wind.", weight: 0.25, minutes: 35)
                        .WithEffects(EffectFactory.Cold(-3, 30), EffectFactory.Exhausted(0.3, 60)),
                    new EventResult("Too cold to work. Your fingers won't cooperate.", weight: 0.15, minutes: 20)
                        .CreateTension("DeadlyCold", 0.45)
                        .WithEffects(EffectFactory.Frostbite(0.3), EffectFactory.Cold(-12, 45))
                ],
                [EventCondition.HasFuel]);
    }

    /// <summary>
    /// Stage 2 of Cold Snap arc - going numb.
    /// Auto-triggers quickly after Stage 1. Frostbite setting in.
    /// </summary>
    private static GameEvent GoingNumb(GameContext ctx)
    {
        return new GameEvent("Going Numb",
            "Can't feel your fingers. Toes going next. Thoughts getting sluggish. This is how it starts.", 3.0)
            .Requires(EventCondition.DeadlyCold)
            .Choice("Keep Moving",
                "Body heat from exertion. Don't stop moving.",
                [
                    new EventResult("Movement keeps blood flowing. You push on.", weight: 0.50, minutes: 10)
                        .Escalate("DeadlyCold", -0.1)
                        .WithEffects(EffectFactory.Exhausted(0.3, 60)),
                    new EventResult("Exhaustion catching up. Slower now.", weight: 0.30, minutes: 12)
                        .Escalate("DeadlyCold", 0.1)
                        .WithEffects(EffectFactory.Exhausted(0.4, 90)),
                    new EventResult("You stumble. Rest might be worse.", weight: 0.20, minutes: 15)
                        .Escalate("DeadlyCold", 0.15)
                        .Damage(2, DamageType.Blunt, "fall")
                ])
            .Choice("Warm Hands in Armpits",
                "Stop. Tuck hands under arms. Restore circulation.",
                [
                    new EventResult("Feeling returns. Painful, but you can use your hands.", weight: 0.60, minutes: 10)
                        .WithEffects(EffectFactory.Frostbite(0.15)),
                    new EventResult("Takes longer. Fingers white before they warm.", weight: 0.30, minutes: 15)
                        .WithEffects(EffectFactory.Frostbite(0.25)),
                    new EventResult("Time lost. Frostbite setting in despite your efforts.", weight: 0.10, minutes: 12)
                        .Escalate("DeadlyCold", 0.15)
                        .WithEffects(EffectFactory.Frostbite(0.35))
                ])
            .Choice("Emergency Fire",
                "Risk everything on a fire. Right here, right now.",
                [
                    new EventResult("Numb fingers fumble. Sparks catch. Fire blooms.", weight: 0.40, minutes: 15)
                        .ResolveTension("DeadlyCold")
                        .Costs(ResourceType.Tinder, 1)
                        .Costs(ResourceType.Fuel, 2)
                        .WithEffects(EffectFactory.Warmed(0.5, 60)),
                    new EventResult("Wind snuffs the first attempt. Second catches.", weight: 0.30, minutes: 20)
                        .ResolveTension("DeadlyCold")
                        .Costs(ResourceType.Tinder, 2)
                        .Costs(ResourceType.Fuel, 2)
                        .WithEffects(EffectFactory.Frostbite(0.2), EffectFactory.Warmed(0.4, 45)),
                    new EventResult("Can't get it started. Tinder's damp. Fingers too numb.", weight: 0.30, minutes: 15)
                        .Escalate("DeadlyCold", 0.2)
                        .Costs(ResourceType.Tinder, 1)
                        .WithEffects(EffectFactory.Frostbite(0.3))
                ],
                [EventCondition.HasTinder, EventCondition.HasFuel])
            .Choice("Burn Something for Warmth",
                "Desperate times. Sacrifice equipment for a small fire.",
                [
                    new EventResult("You burn what you can spare. Brief warmth. Better than death.", weight: 0.70, minutes: 15)
                        .Escalate("DeadlyCold", -0.15)
                        .WithEffects(EffectFactory.Warmed(0.3, 20)),
                    new EventResult("Not enough. The cold is winning.", weight: 0.30, minutes: 12)
                        .Escalate("DeadlyCold", 0.1)
                        .WithEffects(EffectFactory.Frostbite(0.2))
                ]);
    }

    /// <summary>
    /// Stage 3 of Cold Snap arc - frostbite setting in.
    /// Critical decision: sacrifice extremities, dig in, or final push.
    /// </summary>
    private static GameEvent FrostbiteSettingIn(GameContext ctx)
    {
        return new GameEvent("Frostbite Setting In",
            "Skin turning white. Damage happening NOW. You're running out of time.", 3.5)
            .Requires(EventCondition.DeadlyColdCritical)
            .Choice("Sacrifice Fingers to Save Core",
                "Stop protecting your hands. Move faster. Accept the loss.",
                [
                    new EventResult("You stop caring about your fingers. Faster now. Core temperature stabilizing.", weight: 0.60, minutes: 10)
                        .Escalate("DeadlyCold", -0.2)
                        .Damage(8, DamageType.Internal, "severe frostbite to fingers")
                        .WithEffects(EffectFactory.Frostbite(0.6)),
                    new EventResult("The sacrifice buys you time. Your hands may never fully recover.", weight: 0.30, minutes: 8)
                        .Escalate("DeadlyCold", -0.25)
                        .Damage(12, DamageType.Internal, "permanent frostbite damage")
                        .WithEffects(EffectFactory.Frostbite(0.8)),
                    new EventResult("Too late. The cold has you.", weight: 0.10, minutes: 10)
                        .Damage(15, DamageType.Internal, "severe frostbite")
                        .WithEffects(EffectFactory.Frostbite(0.7), EffectFactory.Hypothermia(0.5))
                ])
            .Choice("Emergency Bivouac",
                "Dig into the snow. Wait for conditions to break.",
                [
                    new EventResult("You dig in. Snow insulates. You wait, shivering but alive.", weight: 0.50, minutes: 120)
                        .ResolveTension("DeadlyCold")
                        .WithEffects(EffectFactory.Frostbite(0.3), EffectFactory.Exhausted(0.6, 180)),
                    new EventResult("The bivouac holds. Storm passes. You survive.", weight: 0.30, minutes: 180)
                        .ResolveTension("DeadlyCold")
                        .WithEffects(EffectFactory.Frostbite(0.2), EffectFactory.Exhausted(0.5, 120)),
                    new EventResult("Not deep enough. Cold seeping in. This might be the end.", weight: 0.20, minutes: 90)
                        .Damage(10, DamageType.Internal, "severe hypothermia")
                        .WithEffects(EffectFactory.Hypothermia(0.6), EffectFactory.Frostbite(0.5))
                ])
            .Choice("Final Push",
                "Everything you have. Sprint for camp. Succeed or collapse.",
                [
                    new EventResult("You run. Legs burning. Lungs screaming. Camp appears.", weight: 0.40, minutes: 15)
                        .ResolveTension("DeadlyCold")
                        .WithEffects(EffectFactory.Exhausted(0.7, 120), EffectFactory.Frostbite(0.3))
                        .Aborts(),
                    new EventResult("Almost there. You see the fire. You're going to make it.", weight: 0.30, minutes: 18)
                        .ResolveTension("DeadlyCold")
                        .WithEffects(EffectFactory.Exhausted(0.8, 180), EffectFactory.Frostbite(0.4))
                        .Aborts(),
                    new EventResult("You collapse in the snow. So close. So cold.", weight: 0.20, minutes: 20)
                        .Damage(15, DamageType.Internal, "severe hypothermia")
                        .WithEffects(EffectFactory.Hypothermia(0.7), EffectFactory.Frostbite(0.5)),
                    new EventResult("You fall. The snow is surprisingly warm. That's bad. Very bad.", weight: 0.10, minutes: 15)
                        .Damage(20, DamageType.Internal, "critical hypothermia")
                        .WithEffects(EffectFactory.Hypothermia(0.9), EffectFactory.Frostbite(0.6))
                ]);
    }
}
