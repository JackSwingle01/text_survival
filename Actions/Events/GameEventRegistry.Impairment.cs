using text_survival.Effects;

namespace text_survival.Actions;

public static partial class GameEventRegistry
{
    // === IMPAIRMENT EVENTS ===
    // Events triggered by cognitive/physical impairment - fumbling, disorientation, dulled senses, struggling movement

    // === CONSCIOUSNESS IMPAIRMENT ===

    private static GameEvent LostYourBearings(GameContext ctx)
    {
        return new GameEvent(
            "Lost Your Bearings",
            "You stop. Which way were you heading? The trees all look the same.", 1.0)
            .Requires(EventCondition.IsExpedition)
            // CognitivelyImpaired: clumsy, foggy, or impaired - mental/physical coordination breakdown causes disorientation
            .WithSituationFactor(Situations.CognitivelyImpaired, 2.0)
            .Choice("Backtrack Carefully",
                "Retrace your steps slowly.",
                [
                    new EventResult("You find your bearings again. The detour cost time, but you're on track.", 0.6, 10),
                    new EventResult("Took longer than expected, but you found your way.", 0.4, 20)
                ])
            .Choice("Trust Your Gut",
                "Pick a direction and commit.",
                [
                    new EventResult("Your instincts were right. Only a minor delay.", 0.5, 5),
                    new EventResult("Wrong. Very wrong. You wasted a lot of time before realizing.", 0.35, 25)
                        .WithEffects(EffectFactory.Shaken(0.2)),
                    new EventResult("Completely lost. By the time you find your bearings, you're shaken and exhausted.", 0.15, 35)
                        .WithEffects(EffectFactory.Shaken(0.3), EffectFactory.Exhausted(0.2, 60))
                ])
            .Choice("Sit and Think",
                "Rest until your head clears.",
                [
                    new EventResult("You sit down, close your eyes, breathe. When you open them, you know exactly where you are.", 0.7, 8)
                        .WithEffects(EffectFactory.Focused(0.5, 45)),
                    new EventResult("The rest helps more than expected. Your mind sharpens.", 0.3, 8)
                        .WithEffects(EffectFactory.Focused(0.7, 60))
                ]);
    }

    // === MANIPULATION IMPAIRMENT ===

    private static GameEvent FumblingHands(GameContext ctx)
    {
        return new GameEvent(
            "Fumbling Hands",
            "Your fingers won't cooperate. Simple tasks become frustrating puzzles as you drop things and fumble with every motion.", 1.0)
            .Requires(EventCondition.IsCampWork)
            // CognitivelyImpaired: clumsy, foggy, or impaired - fine motor control suffers under mental/physical compromise
            .WithSituationFactor(Situations.CognitivelyImpaired, 2.0)
            .Choice("Work Slowly",
                "Take your time, move deliberately, don't fight it.",
                [
                    new EventResult("Slow and steady. The work takes longer but gets done.", 0.7, 10),
                    new EventResult("Patience pays off. You adapt to your limitations.", 0.3, 15)
                        .WithEffects(EffectFactory.Focused(0.2, 20))
                ])
            .Choice("Force Through",
                "Grit your teeth and push through the clumsiness.",
                [
                    new EventResult("Brute determination overcomes your unsteady hands.", 0.4, 5),
                    new EventResult("You drop something. Picking it up costs you time.", 0.35, 15),
                    new EventResult("Frustration builds as things slip from your grasp. You cut yourself on something.", 0.25, 10)
                        .WithEffects(EffectFactory.Pain(0.15), EffectFactory.Bleeding(0.1))
                ])
            .Choice("Rest Your Hands",
                "Warm them, flex them, give them a moment to recover.",
                [
                    new EventResult("A few minutes of rest and your hands feel more responsive.", 0.6, 10)
                        .WithEffects(EffectFactory.Focused(0.3, 15)),
                    new EventResult("The rest helps somewhat. At least you're not dropping everything.", 0.4, 15)
                ]);
    }

    // === PERCEPTION IMPAIRMENT ===

    private static GameEvent DulledSenses(GameContext ctx)
    {
        return new GameEvent(
            "Dulled Senses",
            "Everything feels distant and muffled. Your exhausted eyes struggle to focus, and sounds seem to come from far away.", 1.0)
            .Requires(EventCondition.IsExpedition)
            // CognitivelyImpaired: clumsy, foggy, or impaired - sensory processing degrades under mental fatigue
            .WithSituationFactor(Situations.CognitivelyImpaired, 2.0)
            .Choice("Stay Alert",
                "Force yourself to pay attention. It takes effort, but you can't afford to miss something.",
                [
                    new EventResult("You strain your senses, catching details you would have missed.", 0.6, 10)
                        .WithEffects(EffectFactory.Focused(0.2, 20)),
                    new EventResult("The effort helps, but it's exhausting to maintain.", 0.4, 15)
                ])
            .Choice("Trust Your Instincts",
                "You've survived this long. Let your gut guide you.",
                [
                    new EventResult("You move on instinct, your body knowing the way even when your mind is foggy.", 0.5, 5),
                    new EventResult("Your instincts fail you. You nearly walk into something dangerous.", 0.3, 10)
                        .WithEffects(EffectFactory.Shaken(0.2)),
                    new EventResult("You stumble, your dulled senses betraying you at the wrong moment.", 0.2, 15)
                        .WithEffects(EffectFactory.Pain(0.15))
                ])
            .Choice("Slow Down",
                "Move carefully. Take your time and double-check everything.",
                [
                    new EventResult("Slow and steady. You compensate for your dulled senses with caution.", 0.7, 20),
                    new EventResult("The slower pace is frustrating, but at least you're not making mistakes.", 0.3, 25)
                ]);
    }

    // === MOVING IMPAIRMENT ===

    private static GameEvent StrugglingToKeepPace(GameContext ctx)
    {
        return new GameEvent(
            "Struggling to Keep Pace",
            "Each step is a battle. Your leg refuses to cooperate, and you're falling behind where you need to be.", 1.0)
            .Requires(EventCondition.Limping, EventCondition.IsExpedition)
            .Choice("Push Through",
                "Grit your teeth and keep moving at full pace.",
                [
                    new EventResult("Pain flares with every step, but you make good time.", 0.4, 5)
                        .WithEffects(EffectFactory.Pain(0.15)),
                    new EventResult("You push too hard. The strain worsens your condition.", 0.4, 10)
                        .WithEffects(EffectFactory.Pain(0.25)),
                    new EventResult("Your leg gives out. You stumble and barely catch yourself.", 0.2, 15)
                        .WithEffects(EffectFactory.Pain(0.35), EffectFactory.Stiff(0.2, 30))
                ])
            .Choice("Take Frequent Breaks",
                "Slow down and rest your leg when needed.",
                [
                    new EventResult("The slower pace is frustrating, but your leg thanks you.", 0.7, 15),
                    new EventResult("Taking it easy prevents the injury from worsening.", 0.3, 20)
                ])
            .Choice("Turn Back",
                "This is too risky. Head back to camp now.",
                [
                    new EventResult("Better to rest than risk making things worse. You turn around.", 1.0, 5)
                        .WithEffects(EffectFactory.Focused(0.3, 30))
                ]);
    }
}
