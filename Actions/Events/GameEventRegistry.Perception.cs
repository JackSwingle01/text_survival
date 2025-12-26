using text_survival.Effects;

namespace text_survival.Actions;

public static partial class GameEventRegistry
{
    // === PERCEPTION IMPAIRMENT EVENTS ===

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
}
