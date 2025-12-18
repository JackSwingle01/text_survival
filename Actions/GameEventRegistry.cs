using text_survival.Effects;
using text_survival.IO;

namespace text_survival.Actions;

public static class GameEventRegistry
{
    public record TickResult(int MinutesElapsed, GameEvent? TriggeredEvent);

    public static List<GameEvent> AllEvents { get; } =
    [
        WeatherTurning()
    ];

    // Flavor messages - displayed at progress intervals, not random events
    private static readonly List<string> FlavorMessages =
    [
        "You spot animal tracks in the snow. Something passed through here recently.",
        "The wind shifts direction, carrying a bitter chill.",
        "A crow caws somewhere in the distance.",
        "The snow crunches beneath your feet with each step.",
        "Bare branches rattle in the wind above you.",
        "Your breath fogs in the cold air.",
        "You're making good time.",
        "The path ahead is clear."
    ];

    public static string GetRandomFlavorMessage() => Utils.GetRandomFromList(FlavorMessages);

    /// <summary>
    /// Runs minute-by-minute ticks, checking for events each minute.
    /// Returns when targetMinutes is reached OR an event triggers.
    /// Caller is responsible for calling ctx.Update() with the elapsed time.
    /// </summary>
    public static TickResult RunTicks(GameContext ctx, int targetMinutes)
    {
        int elapsed = 0;
        GameEvent? evt = null;

        while (elapsed < targetMinutes)
        {
            elapsed++;
            evt = GetEventOnTick(ctx);
            if (evt is not null)
                break;
        }

        return new TickResult(elapsed, evt);
    }

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
            {
                triggered.Add(evt);
            }
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