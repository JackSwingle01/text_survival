using text_survival.Effects;

namespace text_survival.Actions;

public static partial class GameEventRegistry
{
    // === MOVING IMPAIRMENT EVENTS ===

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
