using text_survival.Effects;

namespace text_survival.Actions;

public static partial class GameEventRegistry
{
    // === MOVING IMPAIRMENT EVENTS ===

    private static GameEvent StrugglingToKeepPace(GameContext ctx)
    {
        var evt = new GameEvent(
            "Struggling to Keep Pace",
            "Each step is a battle. Your leg refuses to cooperate, and you're falling behind where you need to be.");
        evt.BaseWeight = 1.0;

        evt.RequiredConditions.Add(EventCondition.Limping);
        evt.RequiredConditions.Add(EventCondition.IsExpedition);

        var pushThrough = new EventChoice("Push Through",
            "Grit your teeth and keep moving at full pace.",
            [
                new EventResult("Pain flares with every step, but you make good time.", weight: 0.4f)
                { TimeAddedMinutes = 5, Effects = [EffectFactory.Pain(0.15)] },
                new EventResult("You push too hard. The strain worsens your condition.", weight: 0.4f)
                { TimeAddedMinutes = 10, Effects = [EffectFactory.Pain(0.25)] },
                new EventResult("Your leg gives out. You stumble and barely catch yourself.", weight: 0.2f)
                { TimeAddedMinutes = 15, Effects = [EffectFactory.Pain(0.35), EffectFactory.Stiff(0.2, 30)] }
            ]);

        var takeBreaks = new EventChoice("Take Frequent Breaks",
            "Slow down and rest your leg when needed.",
            [
                new EventResult("The slower pace is frustrating, but your leg thanks you.", weight: 0.7f)
                { TimeAddedMinutes = 15 },
                new EventResult("Taking it easy prevents the injury from worsening.", weight: 0.3f)
                { TimeAddedMinutes = 20 }
            ]);

        var turnBack = new EventChoice("Turn Back",
            "This is too risky. Head back to camp now.",
            [
                new EventResult("Better to rest than risk making things worse. You turn around.", weight: 1.0f)
                { TimeAddedMinutes = 5, Effects = [EffectFactory.Focused(0.3, 30)] }
            ]);
        // Note: "Turn Back" doesn't actually force the player to return - it's a narrative beat

        evt.AddChoice(pushThrough);
        evt.AddChoice(takeBreaks);
        evt.AddChoice(turnBack);
        return evt;
    }
}
