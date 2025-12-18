using text_survival.Effects;
using text_survival.IO;
using text_survival.Items;

namespace text_survival.Actions;

public static class GameEventRegistry
{
    public record TickResult(int MinutesElapsed, GameEvent? TriggeredEvent);

    public static List<GameEvent> AllEvents { get; } =
    [
        WeatherTurning(),
        TreacherousFooting(),
        SomethingCatchesYourEye(),
        MinorAccident()
    ];

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
        var evt = new GameEvent("Weather Worsening", "Dark clouds are gathering. A storm is coming.", 0.5);

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

    private static GameEvent TreacherousFooting()
    {
        var evt = new GameEvent("Treacherous Footing",
            "The ground ahead looks unstable — ice beneath the snow, or loose rocks hidden by debris.",
            0.25);

        evt.RequiredConditions.Add(EventCondition.Traveling);
        evt.ChanceModifiers.Add(EventCondition.Injured, 1.5);
        evt.ChanceModifiers.Add(EventCondition.Slow, 1.3);

        var testCarefully = new EventChoice("Test Carefully",
            "You probe ahead with each step, testing your weight before committing.",
            [
                new EventResult("You find a safe path through.", weight: 0.85f)
                { TimeAddedMinutes = 8 },
                new EventResult("Despite your caution, the ground shifts. You stumble but catch yourself.", weight: 0.15f)
                { TimeAddedMinutes = 12, NewEffect = EffectFactory.Bruised(0.2) }
            ]);

        var goAround = new EventChoice("Go Around",
            "You backtrack and find another route entirely.",
            [
                new EventResult("The detour costs time, but you avoid the hazard completely.")
                { TimeAddedMinutes = 18 }
            ]);

        var pushThrough = new EventChoice("Push Through",
            "No time for caution. You move quickly across.",
            [
                new EventResult("You make it through without incident.", weight: 0.5f)
                { TimeAddedMinutes = 0 },
                new EventResult("Your foot breaks through. You wrench your leg free, bruised.", weight: 0.3f)
                { TimeAddedMinutes = 5, NewEffect = EffectFactory.Bruised(0.3) },
                new EventResult("You slip hard. Pain shoots through your ankle.", weight: 0.2f)
                { TimeAddedMinutes = 10, NewEffect = EffectFactory.SprainedAnkle(0.5) }
            ]);

        evt.AddChoice(testCarefully);
        evt.AddChoice(goAround);
        evt.AddChoice(pushThrough);
        return evt;
    }

    private static GameEvent SomethingCatchesYourEye()
    {
        var evt = new GameEvent("Something Catches Your Eye",
            "Movement in your peripheral vision — or was it just a shape that doesn't belong? Something about the landscape ahead seems worth a closer look.",
            0.3);

        evt.RequiredConditions.Add(EventCondition.Working);

        var investigate = new EventChoice("Investigate",
            "You set aside your current task and move closer to examine what you saw.",
            [
                new EventResult("Nothing. Just shadows and your imagination.", weight: 0.4f)
                { TimeAddedMinutes = 12 },
                new EventResult("You find some useful materials partially buried in the snow.", weight: 0.35f)
                { TimeAddedMinutes = 15, RewardPool = RewardPool.BasicSupplies },
                new EventResult("Signs of an old campsite. Someone was here before — they left in a hurry.", weight: 0.15f)
                { TimeAddedMinutes = 20, RewardPool = RewardPool.AbandonedCamp },
                new EventResult("A cache, deliberately hidden. Whoever left this isn't coming back.", weight: 0.1f)
                { TimeAddedMinutes = 18, RewardPool = RewardPool.HiddenCache }
            ]);

        var markAndContinue = new EventChoice("Mark It For Later",
            "You note the location but stay focused on your current task.",
            [
                new EventResult("You file it away mentally and return to work.")
                { TimeAddedMinutes = 2 }
            ]);

        var ignore = new EventChoice("Ignore It",
            "Probably nothing. You have work to do.",
            [
                new EventResult("You push the distraction from your mind and continue.")
                { TimeAddedMinutes = 0 }
            ]);

        evt.AddChoice(investigate);
        evt.AddChoice(markAndContinue);
        evt.AddChoice(ignore);
        return evt;
    }

    private static GameEvent MinorAccident()
    {
        var evt = new GameEvent("Minor Accident",
            "In a moment of inattention, you've hurt yourself.",
            0.2);

        evt.ChanceModifiers.Add(EventCondition.Injured, 1.4);
        evt.ChanceModifiers.Add(EventCondition.Slow, 1.3);

        var assessAndTreat = new EventChoice("Stop and Assess",
            "You take a moment to examine the injury and tend to it properly.",
            [
                new EventResult("It's minor — just a scrape. You clean it and move on.", weight: 0.6f)
                { TimeAddedMinutes = 5 },
                new EventResult("A small cut. You bind it to prevent worse.", weight: 0.3f)
                { TimeAddedMinutes = 8, NewEffect = EffectFactory.MinorCut(0.2) },
                new EventResult("You've twisted something. Rest helps, but it'll slow you down.", weight: 0.1f)
                { TimeAddedMinutes = 12, NewEffect = EffectFactory.SprainedAnkle(0.3) }
            ]);

        var pushOn = new EventChoice("Push On",
            "No time to worry about it. Keep moving.",
            [
                new EventResult("You ignore it. Probably fine.", weight: 0.5f)
                { TimeAddedMinutes = 0, NewEffect = EffectFactory.MinorCut(0.15) },
                new EventResult("You try to ignore it, but it's affecting your movement.", weight: 0.35f)
                { TimeAddedMinutes = 0, NewEffect = EffectFactory.MinorCut(0.3) },
                new EventResult("Ignoring it was a mistake. It's getting worse.", weight: 0.15f)
                { TimeAddedMinutes = 0, NewEffect = EffectFactory.SprainedAnkle(0.4) }
            ]);

        var headBack = new EventChoice("Head Back",
            "This might be serious. Better to return to camp.",
            [
                new EventResult("You turn back, favoring the injury.")
                { AbortsExpedition = true, NewEffect = EffectFactory.MinorCut(0.2) }
            ]);

        evt.AddChoice(assessAndTreat);
        evt.AddChoice(pushOn);
        evt.AddChoice(headBack);
        return evt;
    }

}