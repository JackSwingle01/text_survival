using text_survival.Effects;

namespace text_survival.Actions;

public static partial class GameEventRegistry
{
    // === CONSCIOUSNESS EVENTS ===

    private static GameEvent LostYourBearings(GameContext ctx)
    {
        var evt = new GameEvent(
            "Lost Your Bearings",
            "You stop. Which way were you heading? The trees all look the same.");
        evt.BaseWeight = 1.0;

        evt.RequiredConditions.Add(EventCondition.Impaired);
        evt.RequiredConditions.Add(EventCondition.IsExpedition);

        var backtrack = new EventChoice("Backtrack Carefully",
            "Retrace your steps slowly.",
            [
                new EventResult("You find your bearings again. The detour cost time, but you're on track.", weight: 0.6f)
                { TimeAddedMinutes = 10 },
                new EventResult("Took longer than expected, but you found your way.", weight: 0.4f)
                { TimeAddedMinutes = 20 }
            ]);

        var trustGut = new EventChoice("Trust Your Gut",
            "Pick a direction and commit.",
            [
                new EventResult("Your instincts were right. Only a minor delay.", weight: 0.5f)
                { TimeAddedMinutes = 5 },
                new EventResult("Wrong. Very wrong. You wasted a lot of time before realizing.", weight: 0.35f)
                { TimeAddedMinutes = 25, Effects = [EffectFactory.Shaken(0.2)] },
                new EventResult("Completely lost. By the time you find your bearings, you're shaken and exhausted.", weight: 0.15f)
                { TimeAddedMinutes = 35, Effects = [EffectFactory.Shaken(0.3), EffectFactory.Exhausted(0.2, 60)] }
            ]);

        var sitAndThink = new EventChoice("Sit and Think",
            "Rest until your head clears.",
            [
                new EventResult("You sit down, close your eyes, breathe. When you open them, you know exactly where you are.", weight: 0.7f)
                { TimeAddedMinutes = 15, Effects = [EffectFactory.Focused(0.5, 30)] },
                new EventResult("The rest helps more than expected. Your mind sharpens.", weight: 0.3f)
                { TimeAddedMinutes = 15, Effects = [EffectFactory.Focused(0.7, 45)] }
            ]);

        evt.AddChoice(backtrack);
        evt.AddChoice(trustGut);
        evt.AddChoice(sitAndThink);
        return evt;
    }
}
