using text_survival.Actions.Variants;
using text_survival.Bodies;
using text_survival.Effects;
using text_survival.Environments.Features;
using text_survival.Items;

namespace text_survival.Actions;

public static partial class GameEventRegistry
{
    // === FEVER ARC EVENTS ===

    /// <summary>
    /// Stage 1 of Fever arc - something wrong.
    /// Triggered by untreated wounds or prolonged cold exposure.
    /// Prevention is the real skill - best players never see this.
    /// </summary>
    private static GameEvent SomethingWrong(GameContext ctx)
    {
        // Use IllnessVariant for context-aware cause detection
        var onset = IllnessSelector.SelectOnsetVariant(ctx);

        return new GameEvent("Something Wrong",
            $"Chills that won't stop. Head pounding. {onset.Description}", 0.8)
            .Requires(EventCondition.Awake, EventCondition.LowTemperature)
            // InfectionRisk covers: WoundUntreated/WoundUntreatedHigh + LowTemperature
            .WithSituationFactor(Situations.InfectionRisk, 4.0)
            // Keep individual condition for base cold effect
            .WithConditionFactor(EventCondition.LowTemperature, 2.0)
            .Choice("Rest by Fire",
                "Stop. Warm up. Let your body fight.",
                [
                    new EventResult("You rest. Body temperature stabilizes. Crisis averted.", weight: 0.55, minutes: 60)
                        .WithEffects(EffectFactory.Rested(0.5, 60)),
                    new EventResult("Rest helps but something's still wrong.", weight: 0.30, minutes: 45)
                        .CreateTension("FeverRising", 0.2),
                    new EventResult("The rest reveals how sick you are. Fever rising.", weight: 0.15, minutes: 40)
                        .CreateTension("FeverRising", 0.35)
                        .WithEffects(EffectFactory.Fever(0.25))
                ],
                [EventCondition.NearFire])
            .Choice("Push Through",
                "Mind over matter. You can't afford to stop.",
                [
                    new EventResult("You work through the discomfort. It doesn't get worse.", weight: 0.35)
                        .WithEffects(EffectFactory.Exhausted(0.2, 60)),
                    new EventResult("Your body had other plans. Fever taking hold.", weight: 0.45)
                        .CreateTension("FeverRising", 0.3)
                        .WithEffects(EffectFactory.Fever(0.2)),
                    new EventResult("Big mistake. The fever hits hard.", weight: 0.20)
                        .CreateTension("FeverRising", 0.45)
                        .WithEffects(EffectFactory.Fever(0.35))
                ])
            .Choice("Treat the Cause",
                "Address whatever's causing this.",
                [
                    new EventResult("Herbal treatment. The sickness retreats.", weight: 0.70, minutes: 30)
                        .Costs(ResourceType.Medicine, 2),
                    new EventResult("Treatment helps but it's not enough.", weight: 0.30, minutes: 25)
                        .Costs(ResourceType.Medicine, 2)
                        .CreateTension("FeverRising", 0.15)
                ],
                [EventCondition.HasMedicine]);
    }

    /// <summary>
    /// Stage 2 of Fever arc - fever takes hold.
    /// Player is now sick. Stat penalties apply.
    /// </summary>
    private static GameEvent FeverTakesHold(GameContext ctx)
    {
        return new GameEvent("Fever Takes Hold",
            "Sweating despite the cold. Shivering despite the heat. World starting to blur at the edges.", 1.5)
            .Requires(EventCondition.FeverRising, EventCondition.Awake)
            .Choice("Rest and Stay Warm",
                "Full rest. Fire. Water. Let your body fight.",
                [
                    new EventResult("Hours of misery. But the fever is losing.", weight: 0.50, minutes: 120)
                        .Escalate("FeverRising", -0.2)
                        .WithEffects(EffectFactory.Exhausted(0.4, 120)),
                    new EventResult("You rest. Fever holds steady. Stalemate.", weight: 0.35, minutes: 90)
                        .WithEffects(EffectFactory.Fever(0.4), EffectFactory.Exhausted(0.3, 90)),
                    new EventResult("Rest isn't enough. Fever climbing.", weight: 0.15, minutes: 60)
                        .Escalate("FeverRising", 0.15)
                        .WithEffects(EffectFactory.Fever(0.5))
                ],
                [EventCondition.NearFire])
            .Choice("Keep Working",
                "Survival doesn't wait. Push through.",
                [
                    new EventResult("You function but barely. Everything is harder.", weight: 0.45)
                        .Escalate("FeverRising", 0.1)
                        .WithEffects(EffectFactory.Fever(0.35)),
                    new EventResult("Body rebels. Forced to stop.", weight: 0.35, minutes: 45)
                        .Escalate("FeverRising", 0.2)
                        .WithEffects(EffectFactory.Fever(0.45), EffectFactory.Exhausted(0.4, 90)),
                    new EventResult("You collapse. Fever wins this round.", weight: 0.20, minutes: 60)
                        .Escalate("FeverRising", 0.25)
                        .WithEffects(EffectFactory.Fever(0.6))
                ])
            .Choice("Seek Medicinal Plants",
                "There are plants that help. If you can find them.",
                [
                    new EventResult("You find willow bark. Fever-fighting properties help.", weight: 0.50, minutes: 45)
                        .Escalate("FeverRising", -0.15)
                        .WithEffects(EffectFactory.Fever(0.25)),
                    new EventResult("Hard to focus. You find something. Hope it helps.", weight: 0.30, minutes: 50)
                        .Escalate("FeverRising", -0.05)
                        .WithEffects(EffectFactory.Fever(0.35)),
                    new EventResult("Too sick to search effectively. Wasted effort.", weight: 0.20, minutes: 40)
                        .WithEffects(EffectFactory.Exhausted(0.3, 60))
                ]);
    }

    /// <summary>
    /// Stage 3 of Fever arc - the fire illusion.
    /// Hallucination event. 80% fake, 20% real. Third option (verify) is the learned response.
    /// </summary>
    private static GameEvent TheFireIllusion(GameContext ctx)
    {
        // Use IllnessVariant for context-aware hallucination
        var hallucination = IllnessSelector.SelectFireHallucination(ctx);
        var isReal = IllnessSelector.IsHallucinationReal(hallucination, ctx);

        var description = isReal
            ? "Your fire — the embers are dying! The light fading! You need to act!"  // Real - more urgent
            : hallucination.Description;  // Varied hallucination text

        return new GameEvent("The Fire Illusion",
            $"Through the fog of fever, panic strikes. {description}", 2.0)
            .Requires(EventCondition.FeverHigh, EventCondition.NearFire)
            .Choice("Rush to Tend Fire",
                "No time to think! Save the fire!",
                isReal
                    ? [
                        new EventResult("The fire WAS dying. Your instinct saved you.", weight: 1.0, minutes: 10)
                            .BurnsFuel(2)
                            .WithEffects(EffectFactory.Exhausted(0.2, 30))
                    ]
                    : [
                        new EventResult("You rush over. The fire is fine. The fever lied.", weight: 0.80, minutes: 8)
                            .WithEffects(EffectFactory.Exhausted(0.15, 20), EffectFactory.Shaken(0.15)),
                        new EventResult("You feed a healthy fire. Wasted fuel, but no harm done.", weight: 0.20, minutes: 8)
                            .BurnsFuel(1)
                    ])
            .Choice("Ignore It",
                "It's the fever. Trust nothing.",
                isReal
                    ? [
                        new EventResult("You ignore it. The fire dies. You were wrong.", weight: 1.0, minutes: 30)
                            .Escalate("FeverRising", 0.15)
                            .WithCold(-10, 60)
                    ]
                    : [
                        new EventResult("You stay put. The fire burns steady. Good call.", weight: 0.80)
                            .Escalate("FeverRising", -0.05),
                        new EventResult("You resist the urge. Fire is fine. You're learning.", weight: 0.20)
                            .WithEffects(EffectFactory.Focused(0.1, 30))
                    ])
            .Choice("Verify First",
                "Slow down. Check before reacting. Is it real?",
                isReal
                    ? [
                        new EventResult("You look carefully. It IS low. You add fuel in time.", weight: 1.0, minutes: 5)
                            .BurnsFuel(1)
                    ]
                    : [
                        new EventResult("Careful observation. Fire is fine. The fever lied.", weight: 0.85, minutes: 5)
                            .Escalate("FeverRising", -0.05),
                        new EventResult("You check. All good. Verification is the key.", weight: 0.15, minutes: 5)
                            .WithEffects(EffectFactory.Focused(0.15, 45))
                    ]);
    }

    /// <summary>
    /// Alternative Stage 3 - footsteps outside.
    /// Another hallucination type. Same 80/20 split.
    /// </summary>
    private static GameEvent FootstepsOutside(GameContext ctx)
    {
        // Use IllnessVariant for context-aware hallucination
        // Reality now responds to Stalked tension state, not flat 20%
        var hallucination = IllnessSelector.SelectPredatorHallucination(ctx);
        var isReal = IllnessSelector.IsHallucinationReal(hallucination, ctx);

        var description = isReal
            ? "Footsteps. Circling the camp. Something is OUT THERE."
            : hallucination.Description;  // Varied hallucination text

        return new GameEvent("Footsteps Outside",
            $"Through fevered haze, you hear it. {description}", 1.8)
            .Requires(EventCondition.FeverHigh, EventCondition.AtCamp, EventCondition.Night)
            .Choice("Investigate Immediately",
                "Grab a weapon. Check the perimeter.",
                isReal
                    ? [
                        new EventResult("You were right to check. Something slinks away into darkness.", weight: 0.70, minutes: 10)
                            .BecomeStalked(0.3)
                            .Unsettling(),
                        new EventResult("Eyes reflect in firelight. It's real. It's watching.", weight: 0.30, minutes: 8)
                            .BecomeStalked(0.4)
                            .Frightening()
                    ]
                    : [
                        new EventResult("You search the darkness. Nothing. Just fever and paranoia.", weight: 0.70, minutes: 15)
                            .WithEffects(EffectFactory.Exhausted(0.2, 30), EffectFactory.Paranoid(0.2)),
                        new EventResult("Empty snow. Your mind playing tricks.", weight: 0.30, minutes: 12)
                            .Shaken()
                    ])
            .Choice("Ignore It",
                "Fever. It's just the fever.",
                isReal
                    ? [
                        new EventResult("You stay by fire. In the morning, tracks circle the camp.", weight: 0.70)
                            .BecomeStalked(0.35),
                        new EventResult("You ignore it. Something tests your defenses overnight.", weight: 0.30, minutes: 60)
                            .BecomeStalked(0.45)
                            .Unsettling()
                    ]
                    : [
                        new EventResult("You ignore it. Just paranoia. Good call.", weight: 0.80)
                            .Escalate("FeverRising", -0.05),
                        new EventResult("Nothing. You're learning not to trust the fever.", weight: 0.20)
                    ])
            .Choice("Watch and Listen",
                "Don't move. Wait. Observe. Know the difference.",
                isReal
                    ? [
                        new EventResult("Patient observation reveals movement. It's real.", weight: 0.80, minutes: 20)
                            .BecomeStalked(0.25),
                        new EventResult("You see it clearly now. Wolf. Circling.", weight: 0.20, minutes: 15)
                            .BecomeStalked(0.4, "Wolf")
                    ]
                    : [
                        new EventResult("Long minutes of watching. Nothing. Fever lied.", weight: 0.70, minutes: 20)
                            .Escalate("FeverRising", -0.05)
                            .WithEffects(EffectFactory.Exhausted(0.15, 20)),
                        new EventResult("Your patience reveals truth. Empty night. Fever illusion.", weight: 0.30, minutes: 15)
                    ]);
    }

    /// <summary>
    /// Stage 4 of Fever arc - crisis point.
    /// Fever peaks. Outcome based on care conditions.
    /// </summary>
    private static GameEvent FeverCrisisPoint(GameContext ctx)
    {
        var hasGoodCare = ctx.Check(EventCondition.NearFire) && ctx.Check(EventCondition.HasShelter);

        return new GameEvent("Crisis Point",
            "The fever peaks. Your body at war with itself. This is the turning point.", 2.5)
            .Requires(EventCondition.FeverCritical, EventCondition.Awake)
            .Choice("Fight Through",
                hasGoodCare ? "Fire, shelter, water. Everything you need. Now fight." : "It's bad. But you have to try.",
                hasGoodCare
                    ? [
                        new EventResult("Hours of misery. Then... the fever breaks. You'll live.", weight: 0.60, minutes: 180)
                            .ResolveTension("FeverRising")
                            .WithEffects(EffectFactory.Exhausted(0.7, 240)),
                        new EventResult("Long battle. Fever subsides. You're weak but recovering.", weight: 0.30, minutes: 240)
                            .ResolveTension("FeverRising")
                            .WithEffects(EffectFactory.Exhausted(0.8, 300), EffectFactory.Fever(0.2)),
                        new EventResult("Touch and go. But you survive. Barely.", weight: 0.10, minutes: 300)
                            .ResolveTension("FeverRising")
                            .Damage(0.12, DamageType.Internal)
                            .WithEffects(EffectFactory.Exhausted(0.9, 360))
                    ]
                    : [
                        new EventResult("Against the odds, the fever breaks.", weight: 0.30, minutes: 240)
                            .ResolveTension("FeverRising")
                            .WithEffects(EffectFactory.Exhausted(0.8, 300)),
                        new EventResult("Fever holds. You're in trouble.", weight: 0.40, minutes: 180)
                            .WithEffects(EffectFactory.Fever(0.7))
                            .Damage(0.20, DamageType.Internal),
                        new EventResult("Critical. Your body is failing.", weight: 0.30, minutes: 120)
                            .Damage(0.40, DamageType.Internal)
                            .WithEffects(EffectFactory.Fever(0.8))
                    ])
            .Choice("Herbal Treatment",
                "Use everything you have. All the plants. Fight it.",
                [
                    new EventResult("The herbs work. Fever breaks cleanly. Full recovery.", weight: 0.60, minutes: 120)
                        .ResolveTension("FeverRising")
                        .Costs(ResourceType.Medicine, 3)
                        .WithEffects(EffectFactory.Exhausted(0.5, 180)),
                    new EventResult("Herbs help. Fever drops but lingers.", weight: 0.30, minutes: 150)
                        .Escalate("FeverRising", -0.4)
                        .Costs(ResourceType.Medicine, 3)
                        .WithEffects(EffectFactory.Fever(0.3), EffectFactory.Exhausted(0.4, 120)),
                    new EventResult("Not enough. But every bit helps.", weight: 0.10, minutes: 90)
                        .Escalate("FeverRising", -0.2)
                        .Costs(ResourceType.Medicine, 2)
                        .WithEffects(EffectFactory.Fever(0.5))
                ],
                [EventCondition.HasMedicine])
            .Choice("Accept Fate",
                "Too weak to fight. Let it take you or let it pass.",
                [
                    new EventResult("You surrender to it. Hours blur. You wake. Alive.", weight: 0.40, minutes: 360)
                        .ResolveTension("FeverRising")
                        .WithEffects(EffectFactory.Exhausted(0.9, 480)),
                    new EventResult("Darkness. Cold. Then warmth. You survive. Barely.", weight: 0.35, minutes: 300)
                        .ResolveTension("FeverRising")
                        .Damage(0.25, DamageType.Internal)
                        .WithEffects(EffectFactory.Exhausted(0.95, 420)),
                    new EventResult("The darkness takes you. Is this the end?", weight: 0.25, minutes: 240)
                        .Damage(0.50, DamageType.Internal)
                        .WithEffects(EffectFactory.Fever(0.9))
                ]);
    }
}
