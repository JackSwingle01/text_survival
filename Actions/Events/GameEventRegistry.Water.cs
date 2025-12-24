using text_survival.Bodies;
using text_survival.Effects;
using text_survival.Items;

namespace text_survival.Actions;

/// <summary>
/// Water and ice hazard events.
/// These complement the WaterCrossing event in GameEventRegistry.Expedition.cs
/// with ice-specific hazards.
/// </summary>
public static partial class GameEventRegistry
{
    /// <summary>
    /// Major event: Risk of falling through thin ice.
    /// Triggers on thin ice when traveling or working near water.
    /// </summary>
    private static GameEvent FallThroughIce(GameContext ctx)
    {
        return new GameEvent("Thin Ice",
            "The ice groans beneath your feet. Dark water shows through cracks.", 1.2)
            .Requires(EventCondition.NearWater, EventCondition.OnThinIce)
            .WithConditionFactor(EventCondition.Injured, 1.5)
            .WithConditionFactor(EventCondition.Slow, 1.3)
            .WithConditionFactor(EventCondition.HasMeat, 1.3)
            .WithCooldown(2)
            .Choice("Test the Ice",
                "Probe ahead carefully. Find the safe path.",
                [
                    new EventResult("You find solid ice. Slow but safe.", 0.60, 15),
                    new EventResult("The route takes you around, but you stay dry.", 0.30, 20),
                    new EventResult("Even the careful route gives way. Your foot punches through.", 0.10, 12)
                        .WithEffects(EffectFactory.Wet(0.4, 60), EffectFactory.Cold(-8, 30))
                ])
            .Choice("Go Around",
                "Find another way. Not worth the risk.",
                [
                    new EventResult("The detour adds time, but you stay dry and warm.", 0.70, 25),
                    new EventResult("The shore route is treacherous too. You slip on rocks.", 0.20, 20)
                        .Damage(3, DamageType.Blunt, "fall on rocks"),
                    new EventResult("Going around reveals something interesting.", 0.10, 20)
                        .Rewards(RewardPool.BasicSupplies)
                ])
            .Choice("Cross Quickly",
                "Light and fast. Don't give it time to crack.",
                [
                    new EventResult("You make it across, heart pounding.", 0.40, 5),
                    new EventResult("The ice cracks but holds. You scramble to solid ground.", 0.30, 8)
                        .WithEffects(EffectFactory.Shaken(0.2)),
                    new EventResult("Your foot breaks through. Icy water floods your boot.", 0.20, 12)
                        .WithEffects(EffectFactory.Wet(0.6, 90), EffectFactory.Cold(-12, 45))
                        .Damage(2, DamageType.Blunt, "ice edge"),
                    new EventResult("The ice gives way. You plunge into freezing water.", 0.10, 18)
                        .WithEffects(EffectFactory.Wet(1.0, 150), EffectFactory.Cold(-25, 90), EffectFactory.Fear(0.4))
                        .Damage(6, DamageType.Blunt, "submersion impact")
                ]);
    }

    /// <summary>
    /// Minor event: Getting partially wet while working near water/ice.
    /// Triggers when working near ice holes or thin ice.
    /// </summary>
    private static GameEvent GetFootWet(GameContext ctx)
    {
        return new GameEvent("Wet Footing",
            "The ice is slick with meltwater. Every step risks a soaking.", 0.8)
            .Requires(EventCondition.NearWater, EventCondition.Working)
            .WithConditionFactor(EventCondition.HasIceHole, 1.5)
            .WithConditionFactor(EventCondition.OnThinIce, 1.3)
            .WithCooldown(3)
            .Choice("Work Carefully",
                "Slow down. Watch your footing.",
                [
                    new EventResult("Careful steps keep you dry.", 0.70, 10),
                    new EventResult("A splash soaks your ankle. Cold but manageable.", 0.25, 8)
                        .WithEffects(EffectFactory.Wet(0.2, 30)),
                    new EventResult("You slip but catch yourself. Close call.", 0.05, 5)
                        .WithEffects(EffectFactory.Shaken(0.1))
                ])
            .Choice("Keep Working",
                "A little water won't kill you. Probably.",
                [
                    new EventResult("You stay focused and stay dry.", 0.50, 0),
                    new EventResult("Meltwater seeps into your boot. The cold creeps up your leg.", 0.35, 0)
                        .WithEffects(EffectFactory.Wet(0.3, 45), EffectFactory.Cold(-5, 20)),
                    new EventResult("Your foot goes right through a puddle into slush.", 0.15, 5)
                        .WithEffects(EffectFactory.Wet(0.5, 60), EffectFactory.Cold(-10, 35))
                ]);
    }
}
