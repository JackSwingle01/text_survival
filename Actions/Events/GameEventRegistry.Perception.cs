using text_survival.Effects;

namespace text_survival.Actions;

public static partial class GameEventRegistry
{
    // === PERCEPTION IMPAIRMENT EVENTS ===

    private static GameEvent DulledSenses(GameContext ctx)
    {
        var evt = new GameEvent(
            "Dulled Senses",
            "Everything feels distant and muffled. Your exhausted eyes struggle to focus, and sounds seem to come from far away.");
        evt.BaseWeight = 1.0;

        evt.RequiredConditions.Add(EventCondition.Foggy);
        evt.RequiredConditions.Add(EventCondition.IsExpedition);

        var stayAlert = new EventChoice("Stay Alert",
            "Force yourself to pay attention. It takes effort, but you can't afford to miss something.",
            [
                new EventResult("You strain your senses, catching details you would have missed.", weight: 0.6f)
                { TimeAddedMinutes = 10, Effects = [EffectFactory.Focused(0.2, 20)] },
                new EventResult("The effort helps, but it's exhausting to maintain.", weight: 0.4f)
                { TimeAddedMinutes = 15 }
            ]);

        var trustInstincts = new EventChoice("Trust Your Instincts",
            "You've survived this long. Let your gut guide you.",
            [
                new EventResult("You move on instinct, your body knowing the way even when your mind is foggy.", weight: 0.5f)
                { TimeAddedMinutes = 5 },
                new EventResult("Your instincts fail you. You nearly walk into something dangerous.", weight: 0.3f)
                { TimeAddedMinutes = 10, Effects = [EffectFactory.Shaken(0.2)] },
                new EventResult("You stumble, your dulled senses betraying you at the wrong moment.", weight: 0.2f)
                { TimeAddedMinutes = 15, Effects = [EffectFactory.Pain(0.15)] }
            ]);

        var slowDown = new EventChoice("Slow Down",
            "Move carefully. Take your time and double-check everything.",
            [
                new EventResult("Slow and steady. You compensate for your dulled senses with caution.", weight: 0.7f)
                { TimeAddedMinutes = 20 },
                new EventResult("The slower pace is frustrating, but at least you're not making mistakes.", weight: 0.3f)
                { TimeAddedMinutes = 25 }
            ]);

        evt.AddChoice(stayAlert);
        evt.AddChoice(trustInstincts);
        evt.AddChoice(slowDown);
        return evt;
    }
}
