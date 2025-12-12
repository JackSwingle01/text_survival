using text_survival.Effects;
using text_survival.IO;

namespace text_survival.Actions;

public static class GameEventRegistry
{
    public static List<GameEvent> AllEvents { get; } =
    [
        WeatherTurning()
    ];

    public static GameEvent? GetEventOnTick(GameContext ctx)
    {
        List<GameEvent> triggered = [];

        foreach (var evt in AllEvents)
        {
            // Check required conditions
            if (!evt.RequiredConditions.All(ctx.Check))
                continue;

            // Calculate modified chance
            double chance = evt.BaseChancePerMinute;
            foreach (var (condition, modifier) in evt.ChanceModifiers)
            {
                if (ctx.Check(condition))
                    chance *= modifier;
            }

            if (Utils.DetermineSuccess(chance))
                Output.WriteLine($"Debug: event triggered with chance: {chance}");
            triggered.Add(evt);
        }

        // Return one at random if any triggered
        return triggered.Count == 0 ? null : Utils.GetRandomFromList(triggered);
    }

    private static GameEvent WeatherTurning()
    {
        var evt = new GameEvent("Weather Worsening", "Dark clouds are gathering. A storm is coming.", 1);

        var pushThrough = new EventChoice("Push Through",
            "You need to keep going. You wrap your clothes tight and keep moving.",
            [
            new EventResult("The storm grazes you. Cold, but manageable.", weight: 0.6f)
            {
                TimeAddedMinutes = 10,
                NewEffect = EffectFactory.Cold(-8, 30)
            },
            new EventResult("The wind cuts through everything. Your hands go stiff.", weight: 0.3f)
            {
                TimeAddedMinutes = 20,
                NewEffect = EffectFactory.Cold(-15, 45)
            },
            new EventResult("The cold bites deep. You can barely feel your fingers.", weight: 0.1f)
            {
                TimeAddedMinutes = 25,
                NewEffect = EffectFactory.Cold(-20, 60),
                // todo add frostbite
            }
            ]);
        var seekShelter = new EventChoice("Seek Shelter",
            "You look for shelter. You see a natural rock overhang that will block the wind. You dig in and brace yourself.",
            [
                new EventResult("The storm passes you by mostly unharmed. But it cost you time.")
                {
                    TimeAddedMinutes = 45,
                    NewEffect = EffectFactory.Cold(-2, 45)
                }
            ]);
        var headBack = new EventChoice("Head Back",
            "It's too risky to keep going. You stop and turn back.",
            [
                new EventResult("You get your bearings and hurry back towards camp as the wind picks up.")
                {
                    AbortsExpedition = true,
                    NewEffect = EffectFactory.Cold(-5, 30)
                }
            ]);

        evt.AddChoice(pushThrough);
        evt.AddChoice(seekShelter);
        evt.AddChoice(headBack);

        return evt;
    }
}