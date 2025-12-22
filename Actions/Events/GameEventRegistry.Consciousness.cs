using text_survival.Effects;

namespace text_survival.Actions;

public static partial class GameEventRegistry
{
    // === CONSCIOUSNESS EVENTS ===

    private static GameEvent LostYourBearings(GameContext ctx)
    {
        return new GameEvent(
            "Lost Your Bearings",
            "You stop. Which way were you heading? The trees all look the same.", 1.0)
            .Requires(EventCondition.Impaired, EventCondition.IsExpedition)
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
                    new EventResult("You sit down, close your eyes, breathe. When you open them, you know exactly where you are.", 0.7, 15)
                        .WithEffects(EffectFactory.Focused(0.5, 30)),
                    new EventResult("The rest helps more than expected. Your mind sharpens.", 0.3, 15)
                        .WithEffects(EffectFactory.Focused(0.7, 45))
                ]);
    }
}
