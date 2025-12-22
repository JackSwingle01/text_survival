using text_survival.Effects;

namespace text_survival.Actions;

public static partial class GameEventRegistry
{
    // === MANIPULATION IMPAIRMENT EVENTS ===

    private static GameEvent FumblingHands(GameContext ctx)
    {
        return new GameEvent(
            "Fumbling Hands",
            "Your fingers won't cooperate. Simple tasks become frustrating puzzles as you drop things and fumble with every motion.", 1.0)
            .Requires(EventCondition.Clumsy, EventCondition.IsCampWork)
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
}
