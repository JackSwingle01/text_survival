using text_survival.Effects;

namespace text_survival.Actions;

public static partial class GameEventRegistry
{
    // === MANIPULATION IMPAIRMENT EVENTS ===

    private static GameEvent FumblingHands(GameContext ctx)
    {
        var evt = new GameEvent(
            "Fumbling Hands",
            "Your fingers won't cooperate. Simple tasks become frustrating puzzles as you drop things and fumble with every motion.");
        evt.BaseWeight = 1.0;

        evt.RequiredConditions.Add(EventCondition.Clumsy);
        evt.RequiredConditions.Add(EventCondition.IsCampWork);

        var workSlowly = new EventChoice("Work Slowly",
            "Take your time, move deliberately, don't fight it.",
            [
                new EventResult("Slow and steady. The work takes longer but gets done.", weight: 0.7f)
                { TimeAddedMinutes = 10 },
                new EventResult("Patience pays off. You adapt to your limitations.", weight: 0.3f)
                { TimeAddedMinutes = 15, Effects = [EffectFactory.Focused(0.2, 20)] }
            ]);

        var forceThrough = new EventChoice("Force Through",
            "Grit your teeth and push through the clumsiness.",
            [
                new EventResult("Brute determination overcomes your unsteady hands.", weight: 0.4f)
                { TimeAddedMinutes = 5 },
                new EventResult("You drop something. Picking it up costs you time.", weight: 0.35f)
                { TimeAddedMinutes = 15 },
                new EventResult("Frustration builds as things slip from your grasp. You cut yourself on something.", weight: 0.25f)
                { TimeAddedMinutes = 10, Effects = [EffectFactory.Pain(0.15), EffectFactory.Bleeding(0.1)] }
            ]);

        var restHands = new EventChoice("Rest Your Hands",
            "Warm them, flex them, give them a moment to recover.",
            [
                new EventResult("A few minutes of rest and your hands feel more responsive.", weight: 0.6f)
                { TimeAddedMinutes = 10, Effects = [EffectFactory.Focused(0.3, 15)] },
                new EventResult("The rest helps somewhat. At least you're not dropping everything.", weight: 0.4f)
                { TimeAddedMinutes = 15 }
            ]);

        evt.AddChoice(workSlowly);
        evt.AddChoice(forceThrough);
        evt.AddChoice(restHands);
        return evt;
    }
}
