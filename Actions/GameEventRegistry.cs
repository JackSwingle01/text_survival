using text_survival.Bodies;
using text_survival.Effects;
using text_survival.Environments.Features;
using text_survival.IO;
using text_survival.Items;
using text_survival.UI;

namespace text_survival.Actions;

public static class GameEventRegistry
{
    public record TickResult(int MinutesElapsed, GameEvent? TriggeredEvent);

    // Single knob to control overall event frequency
    private const double EventsPerHour = 1.0;
    private static readonly double BaseChancePerMinute = RateToChancePerMinute(EventsPerHour);

    private static double RateToChancePerMinute(double eventsPerHour)
    {
        double ratePerMinute = eventsPerHour / 60.0;
        return 1 - Math.Exp(-ratePerMinute);
    }

    /// <summary>
    /// Event factories that create fresh events with context baked in.
    /// </summary>
    public static List<Func<GameContext, GameEvent>> AllEventFactories { get; } =
    [
        // Weather events (7)
        StormApproaching,
        Whiteout,
        FrostbiteWarning,
        ColdRainSoaking,
        LostInFog,
        BitterWind,
        SuddenClearing,

        // Other events
        TreacherousFooting,
        SomethingCatchesYourEye,
        MinorAccident,
        FreshCarcass,
        Tracks,
        SomethingWatching
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
        // Stage 1: Base roll - does ANY event trigger?
        // Events are half as likely at camp
        double multiplier = ctx.IsAtCamp ? 0.5 : 1.0;
        double chance = BaseChancePerMinute * multiplier;
        if (!Utils.DetermineSuccess(chance))
            return null;

        // Stage 2: Build eligible pool with weights
        var eligible = new Dictionary<GameEvent, double>();

        foreach (var factory in AllEventFactories)
        {
            var evt = factory(ctx);

            // Filter: skip if required conditions not met
            if (!evt.RequiredConditions.All(ctx.Check))
                continue;

            // Calculate weight with modifiers
            double weight = evt.BaseWeight;
            foreach (var (condition, modifier) in evt.WeightModifiers)
            {
                if (ctx.Check(condition))
                    weight *= modifier;
            }

            eligible[evt] = weight;
        }

        // If no eligible events, no event triggers
        if (eligible.Count == 0)
            return null;

        // Stage 3: Weighted random selection
        return Utils.GetRandomWeighted(eligible);
    }

    /// <summary>
    /// Handle a triggered event - display, get player choice, apply outcome.
    /// </summary>
    public static void HandleEvent(GameContext ctx, GameEvent evt)
    {
        GameDisplay.Render(ctx, statusText: "Event!");
        GameDisplay.AddNarrative($"{evt.Name}", LogLevel.Warning);
        GameDisplay.AddNarrative(evt.Description);
        GameDisplay.Render(ctx, statusText: "Thinking.");

        var choice = evt.GetChoice(ctx);
        GameDisplay.AddNarrative(choice.Description + "\n");
        // Input.WaitForKey();

        var outcome = choice.DetermineResult();

        HandleOutcome(ctx, outcome);
        GameDisplay.Render(ctx);
        Input.WaitForKey();
    }

    /// <summary>
    /// Apply an event outcome - time, effects, rewards.
    /// </summary>
    public static void HandleOutcome(GameContext ctx, EventResult outcome)
    {

        if (outcome.TimeAddedMinutes != 0)
        {
            GameDisplay.AddNarrative($"(+{outcome.TimeAddedMinutes} minutes)");
            GameDisplay.UpdateAndRenderProgress(ctx, "Acting", outcome.TimeAddedMinutes, updateTime: false);
        }

        GameDisplay.AddNarrative(outcome.Message);

        if (outcome.NewEffect is not null)
        {
            ctx.player.EffectRegistry.AddEffect(outcome.NewEffect);
        }

        if (outcome.NewDamage is not null)
        {
            var dmgResult = ctx.player.Body.Damage(outcome.NewDamage);
            if (dmgResult.TriggeredEffect != null)
                ctx.player.EffectRegistry.AddEffect(dmgResult.TriggeredEffect);
        }

        if (outcome.RewardPool != RewardPool.None)
        {
            var resources = RewardGenerator.Generate(outcome.RewardPool);
            if (!resources.IsEmpty)
            {
                ctx.Inventory.Add(resources);
                foreach (var desc in resources.Descriptions)
                {
                    GameDisplay.AddNarrative($"You found {desc}");
                }
            }
        }

        if (outcome.Cost is not null)
        {
            DeductResources(ctx.Inventory, outcome.Cost);
        }
    }

    /// <summary>
    /// Deduct resources from inventory based on cost type.
    /// </summary>
    private static void DeductResources(Items.Inventory inv, ResourceCost cost)
    {
        for (int i = 0; i < cost.Amount; i++)
        {
            switch (cost.Type)
            {
                case ResourceType.Fuel:
                    // Prefer sticks over logs (less wasteful)
                    if (inv.Sticks.Count > 0)
                        inv.TakeSmallestStick();
                    else if (inv.Logs.Count > 0)
                        inv.TakeSmallestLog();
                    break;

                case ResourceType.Tinder:
                    inv.TakeTinder();
                    break;

                case ResourceType.Food:
                    // Prefer berries, then cooked, then raw
                    if (inv.Berries.Count > 0)
                        inv.Berries.RemoveAt(0);
                    else if (inv.CookedMeat.Count > 0)
                        inv.CookedMeat.RemoveAt(0);
                    else if (inv.RawMeat.Count > 0)
                        inv.RawMeat.RemoveAt(0);
                    break;
            }
        }
    }

    // === EVENT FACTORIES ===

    // === WEATHER EVENTS ===

    private static GameEvent StormApproaching(GameContext ctx)
    {
        var evt = new GameEvent("Storm Approaching",
            "The sky darkens. The wind is picking up fast. A serious storm is coming.");
        evt.BaseWeight = 2.0;  // High priority when weather worsens
        evt.RequiredConditions.Add(EventCondition.WeatherWorsening);

        var raceToFinish = new EventChoice("Race to Finish",
            "Try to complete what you came for before the worst hits.",
            [
                new EventResult("You finish just in time and escape before the worst of it.", weight: 0.35)
                { TimeAddedMinutes = 5 },
                new EventResult("The storm arrives faster than expected. You're caught in the thick of it.", weight: 0.40)
                { TimeAddedMinutes = 25, NewEffect = EffectFactory.Cold(-15, 45) },
                new EventResult("The storm changes direction. You wasted time worrying.", weight: 0.15)
                { TimeAddedMinutes = 10 },
                new EventResult("You find unexpected shelter along the way.", weight: 0.10)
                { TimeAddedMinutes = 15, NewEffect = EffectFactory.Cold(-3, 20) }
            ]);

        var seekShelter = new EventChoice("Seek Shelter Immediately",
            "Drop everything and find cover before the storm hits.",
            [
                new EventResult("You find good shelter and wait it out safely.", weight: 0.50)
                { TimeAddedMinutes = 45, NewEffect = EffectFactory.Cold(-2, 30) },
                new EventResult("The shelter is poor. You're out of the wind but still cold.", weight: 0.30)
                { TimeAddedMinutes = 40, NewEffect = EffectFactory.Cold(-8, 35) },
                new EventResult("While sheltering, you discover something useful.", weight: 0.15)
                { TimeAddedMinutes = 50, RewardPool = RewardPool.BasicSupplies },
                new EventResult("The shelter collapses under the wind. Worse than being outside.", weight: 0.05)
                { TimeAddedMinutes = 35, NewEffect = EffectFactory.Cold(-12, 40), NewDamage = new DamageInfo(5, DamageType.Blunt, "debris") }
            ]);

        var headBack = new EventChoice("Head Back Now",
            "Abort the expedition and get back to camp before the storm hits.",
            [
                new EventResult("You make it back before the storm hits.", weight: 0.55)
                { AbortsExpedition = true },
                new EventResult("The storm catches you partway, but you're closer to camp.", weight: 0.30)
                { AbortsExpedition = true, NewEffect = EffectFactory.Cold(-8, 25) },
                new EventResult("In your haste, you drop something.", weight: 0.10)
                { AbortsExpedition = true, Cost = new ResourceCost(ResourceType.Fuel, 1) },
                new EventResult("You stumble in your rush. Minor injury.", weight: 0.05)
                { AbortsExpedition = true, NewDamage = new DamageInfo(4, DamageType.Blunt, "fall") }
            ]);

        evt.AddChoice(raceToFinish);
        evt.AddChoice(seekShelter);
        evt.AddChoice(headBack);
        return evt;
    }

    private static GameEvent Whiteout(GameContext ctx)
    {
        var evt = new GameEvent("Whiteout",
            "The snow is so thick you can't see your hand in front of your face. You've lost all sense of direction.");
        evt.BaseWeight = 1.5;
        evt.RequiredConditions.Add(EventCondition.IsBlizzard);
        evt.RequiredConditions.Add(EventCondition.Traveling);

        var keepMoving = new EventChoice("Keep Moving",
            "Trust your instincts and keep walking. You'll find your way.",
            [
                new EventResult("Your instincts serve you well. You stay on course.", weight: 0.30)
                { TimeAddedMinutes = 10 },
                new EventResult("You drift off course. It takes time to correct.", weight: 0.40)
                { TimeAddedMinutes = 25, NewEffect = EffectFactory.Cold(-10, 30) },
                new EventResult("You're completely lost. When the snow clears, nothing looks familiar.", weight: 0.20)
                { TimeAddedMinutes = 45, NewEffect = EffectFactory.Cold(-15, 45) },
                new EventResult("You walk straight into a hidden hazard.", weight: 0.10)
                { TimeAddedMinutes = 15, NewDamage = new DamageInfo(8, DamageType.Blunt, "fall"), NewEffect = EffectFactory.Cold(-8, 25) }
            ]);

        var stopAndWait = new EventChoice("Stop and Wait",
            "Dig in, hunker down, and wait for visibility to return.",
            [
                new EventResult("The storm passes in about half an hour. You continue.", weight: 0.40)
                { TimeAddedMinutes = 30, NewEffect = EffectFactory.Cold(-5, 30) },
                new EventResult("The storm lasts longer than expected. Your fire margin is in danger.", weight: 0.35)
                { TimeAddedMinutes = 60, NewEffect = EffectFactory.Cold(-12, 50) },
                new EventResult("The cold seeps in despite your efforts. You feel the early signs of hypothermia.", weight: 0.20)
                { TimeAddedMinutes = 40, NewEffect = EffectFactory.Cold(-18, 60) },
                new EventResult("Something finds you while you're stationary.", weight: 0.05)
                { TimeAddedMinutes = 20, NewEffect = EffectFactory.Fear(0.4), AbortsExpedition = true }
            ]);

        var burnFuel = new EventChoice("Burn Fuel for Warmth",
            "Use some of your fuel to start a fire and wait out the storm in relative comfort.",
            [
                new EventResult("The fire keeps you warm. You wait out the storm comfortably.", weight: 0.60)
                { TimeAddedMinutes = 45, Cost = new ResourceCost(ResourceType.Fuel, 2) },
                new EventResult("Your fuel runs low before the storm ends. Still better than nothing.", weight: 0.25)
                { TimeAddedMinutes = 50, NewEffect = EffectFactory.Cold(-5, 25), Cost = new ResourceCost(ResourceType.Fuel, 2) },
                new EventResult("The fire's glow attracts your attention to a landmark. You can navigate now.", weight: 0.10)
                { TimeAddedMinutes = 20, Cost = new ResourceCost(ResourceType.Fuel, 1) },
                new EventResult("The wind snuffs out your fire. Wasted fuel.", weight: 0.05)
                { TimeAddedMinutes = 35, NewEffect = EffectFactory.Cold(-10, 35), Cost = new ResourceCost(ResourceType.Fuel, 2) }
            ]);

        evt.AddChoice(keepMoving);
        evt.AddChoice(stopAndWait);
        evt.AddChoice(burnFuel);
        return evt;
    }

    private static GameEvent FrostbiteWarning(GameContext ctx)
    {
        var evt = new GameEvent("Frostbite Warning",
            "Your fingers have gone white. You can't feel your toes. This is getting serious.");
        evt.BaseWeight = 1.2;
        evt.RequiredConditions.Add(EventCondition.ExtremelyCold);
        evt.RequiredConditions.Add(EventCondition.Outside);

        var treatNow = new EventChoice("Treat It Now",
            "Stop, tuck your hands under your arms, stamp your feet. Get the blood flowing.",
            [
                new EventResult("You catch it in time. Painful but no lasting damage.", weight: 0.55)
                { TimeAddedMinutes = 10, NewEffect = EffectFactory.Cold(-3, 15) },
                new EventResult("It takes longer than expected, but the feeling returns.", weight: 0.30)
                { TimeAddedMinutes = 20, NewEffect = EffectFactory.Cold(-5, 20) },
                new EventResult("Despite your efforts, the damage was already done. Your fingertips are numb.", weight: 0.15)
                { TimeAddedMinutes = 15, NewDamage = new DamageInfo(5, DamageType.Internal, "frostbite") }
            ]);

        var pushOn = new EventChoice("Push On",
            "You'll deal with it at camp. Just need to get back.",
            [
                new EventResult("You make it back. The damage is treatable.", weight: 0.35)
                { NewDamage = new DamageInfo(3, DamageType.Internal, "frostbite") },
                new EventResult("Too late. Some tissue is permanently damaged.", weight: 0.30)
                { NewDamage = new DamageInfo(8, DamageType.Internal, "frostbite") },
                new EventResult("The movement actually helps circulation. You warm up a bit.", weight: 0.25)
                { TimeAddedMinutes = 5, NewEffect = EffectFactory.Cold(-2, 10) },
                new EventResult("Your body gives out before you reach safety.", weight: 0.10)
                { TimeAddedMinutes = 30, NewDamage = new DamageInfo(12, DamageType.Internal, "frostbite"), NewEffect = EffectFactory.Cold(-20, 60) }
            ]);

        var burnSupplies = new EventChoice("Burn Supplies",
            "Use fuel or tinder to start an emergency fire. Your extremities are worth more than supplies.",
            [
                new EventResult("The fire saves your fingers. Worth every stick.", weight: 0.50)
                { TimeAddedMinutes = 15, Cost = new ResourceCost(ResourceType.Fuel, 2) },
                new EventResult("It partially works. Some damage, but you'll keep your fingers.", weight: 0.30)
                { TimeAddedMinutes = 20, NewDamage = new DamageInfo(3, DamageType.Internal, "frostbite"), Cost = new ResourceCost(ResourceType.Fuel, 2) },
                new EventResult("The fire doesn't help enough. The damage is done, and you've wasted supplies.", weight: 0.15)
                { TimeAddedMinutes = 15, NewDamage = new DamageInfo(6, DamageType.Internal, "frostbite"), Cost = new ResourceCost(ResourceType.Fuel, 2) },
                new EventResult("The attempt makes things worse. Wet hands in extreme cold.", weight: 0.05)
                { TimeAddedMinutes = 20, NewDamage = new DamageInfo(10, DamageType.Internal, "frostbite"), Cost = new ResourceCost(ResourceType.Fuel, 1) }
            ]);

        evt.AddChoice(treatNow);
        evt.AddChoice(pushOn);
        evt.AddChoice(burnSupplies);
        return evt;
    }

    private static GameEvent ColdRainSoaking(GameContext ctx)
    {
        var evt = new GameEvent("Cold Rain Soaking",
            "The rain is seeping through everything. You're getting dangerously wet in freezing conditions.");
        evt.BaseWeight = 1.0;
        evt.RequiredConditions.Add(EventCondition.IsRaining);
        evt.RequiredConditions.Add(EventCondition.Outside);

        var stripAndWring = new EventChoice("Strip and Wring",
            "Find what cover you can, strip off wet layers, wring them out. Brief exposure, but drier clothes.",
            [
                new EventResult("It works. You're cold but no longer waterlogged.", weight: 0.50)
                { TimeAddedMinutes = 15, NewEffect = EffectFactory.Cold(-5, 20) },
                new EventResult("The brief exposure causes additional cold damage.", weight: 0.30)
                { TimeAddedMinutes = 15, NewEffect = EffectFactory.Cold(-12, 35) },
                new EventResult("You can't get dry enough. Still soaked.", weight: 0.15)
                { TimeAddedMinutes = 20, NewEffect = EffectFactory.Cold(-15, 45) },
                new EventResult("Something sees you while you're vulnerable.", weight: 0.05)
                { TimeAddedMinutes = 10, NewEffect = EffectFactory.Fear(0.3), AbortsExpedition = true }
            ]);

        var keepMovingFast = new EventChoice("Keep Moving Fast",
            "Generate body heat through movement. If you stop, you'll freeze.",
            [
                new EventResult("The movement keeps you warm enough to push through.", weight: 0.35)
                { TimeAddedMinutes = 5, NewEffect = EffectFactory.Cold(-8, 25) },
                new EventResult("Exhaustion plus cold. You're in serious trouble.", weight: 0.35)
                { TimeAddedMinutes = 20, NewEffect = EffectFactory.Cold(-18, 50), NewDamage = new DamageInfo(3, DamageType.Internal, "exposure") },
                new EventResult("You find shelter faster than expected.", weight: 0.20)
                { TimeAddedMinutes = 10, NewEffect = EffectFactory.Cold(-3, 15) },
                new EventResult("You push through successfully. Cold but alive.", weight: 0.10)
                { TimeAddedMinutes = 5 }
            ]);

        var startFire = new EventChoice("Start Emergency Fire",
            "Find what dry materials you can. You need heat desperately.",
            [
                new EventResult("You find dry tinder under a rock overhang. The fire saves you.", weight: 0.30)
                { TimeAddedMinutes = 25, Cost = new ResourceCost(ResourceType.Fuel, 2) },
                new EventResult("Everything is too wet. The fire won't catch.", weight: 0.35)
                { TimeAddedMinutes = 20, NewEffect = EffectFactory.Cold(-12, 40), Cost = new ResourceCost(ResourceType.Tinder, 1) },
                new EventResult("Partial success. A small fire buys you time to dry out somewhat.", weight: 0.25)
                { TimeAddedMinutes = 30, NewEffect = EffectFactory.Cold(-5, 25), Cost = new ResourceCost(ResourceType.Fuel, 2) },
                new EventResult("The fire attempt takes too long. Hypothermia sets in.", weight: 0.10)
                { TimeAddedMinutes = 35, NewEffect = EffectFactory.Cold(-20, 60), Cost = new ResourceCost(ResourceType.Tinder, 1) }
            ]);

        evt.AddChoice(stripAndWring);
        evt.AddChoice(keepMovingFast);
        evt.AddChoice(startFire);
        return evt;
    }

    private static GameEvent LostInFog(GameContext ctx)
    {
        var evt = new GameEvent("Lost in Fog",
            "The fog is disorienting. Every direction looks the same. You're not sure which way you came from.");
        evt.BaseWeight = 1.0;
        evt.RequiredConditions.Add(EventCondition.IsMisty);
        evt.RequiredConditions.Add(EventCondition.Traveling);

        var waitForLift = new EventChoice("Wait for it to Lift",
            "Sit tight. The fog will clear eventually.",
            [
                new EventResult("The fog clears in about twenty minutes. You continue on your way.", weight: 0.45)
                { TimeAddedMinutes = 20 },
                new EventResult("The fog persists. You lose significant time waiting.", weight: 0.35)
                { TimeAddedMinutes = 40, NewEffect = EffectFactory.Cold(-3, 20) },
                new EventResult("While waiting, you hear something useful. Animal sounds, water, voices.", weight: 0.15)
                { TimeAddedMinutes = 25, RewardPool = RewardPool.GameTrailDiscovery },
                new EventResult("Something finds you while you're sitting still.", weight: 0.05)
                { TimeAddedMinutes = 15, NewEffect = EffectFactory.Fear(0.3), AbortsExpedition = true }
            ]);

        var keepMovingSlowly = new EventChoice("Keep Moving Slowly",
            "Careful steps. Watch for landmarks. You'll find your way.",
            [
                new EventResult("You find your way with only minor delay.", weight: 0.50)
                { TimeAddedMinutes = 15 },
                new EventResult("You end up somewhere unexpected, but interesting.", weight: 0.25)
                { TimeAddedMinutes = 20, RewardPool = RewardPool.BasicSupplies },
                new EventResult("You walk in circles. When the fog clears, you're barely closer.", weight: 0.20)
                { TimeAddedMinutes = 35 },
                new EventResult("You walk straight into trouble.", weight: 0.05)
                { TimeAddedMinutes = 10, NewDamage = new DamageInfo(6, DamageType.Blunt, "fall"), AbortsExpedition = true }
            ]);

        var useTheFog = new EventChoice("Use the Fog",
            "The fog conceals you too. Maybe you can get close to something that wouldn't normally let you approach.",
            [
                new EventResult("You get close to game you couldn't normally approach. An opportunity.", weight: 0.30)
                { TimeAddedMinutes = 25, RewardPool = RewardPool.BasicMeat },
                new EventResult("You discover something hidden. The fog revealed it by making everything else disappear.", weight: 0.25)
                { TimeAddedMinutes = 30, RewardPool = RewardPool.HiddenCache },
                new EventResult("You waste time stalking shadows. Nothing there.", weight: 0.35)
                { TimeAddedMinutes = 25 },
                new EventResult("The thing you were stalking was stalking you.", weight: 0.10)
                { TimeAddedMinutes = 15, NewDamage = new DamageInfo(10, DamageType.Sharp, "animal attack"), AbortsExpedition = true }
            ]);

        evt.AddChoice(waitForLift);
        evt.AddChoice(keepMovingSlowly);
        evt.AddChoice(useTheFog);
        return evt;
    }

    private static GameEvent BitterWind(GameContext ctx)
    {
        var evt = new GameEvent("Bitter Wind",
            "The wind cuts through your clothes like they're not there. Your body heat is being stripped away.");
        evt.BaseWeight = 1.0;
        evt.RequiredConditions.Add(EventCondition.HighWind);
        evt.RequiredConditions.Add(EventCondition.Outside);

        var findWindbreak = new EventChoice("Find a Windbreak",
            "Look for natural cover. Rocks, trees, a depression in the ground.",
            [
                new EventResult("You find good cover. You warm up and continue.", weight: 0.50)
                { TimeAddedMinutes = 8, NewEffect = EffectFactory.Cold(-2, 10) },
                new EventResult("Partial cover. It helps somewhat.", weight: 0.30)
                { TimeAddedMinutes = 12, NewEffect = EffectFactory.Cold(-5, 20) },
                new EventResult("No good options nearby. You waste time looking.", weight: 0.15)
                { TimeAddedMinutes = 18, NewEffect = EffectFactory.Cold(-10, 30) },
                new EventResult("The windbreak has another problem. You're not alone.", weight: 0.05)
                { TimeAddedMinutes = 10, NewEffect = EffectFactory.Fear(0.2) }
            ]);

        var keepMoving = new EventChoice("Turn Your Back and Keep Moving",
            "Let your clothing do its job. The wind is cold but you can take it.",
            [
                new EventResult("Cold but manageable. You push through.", weight: 0.40)
                { TimeAddedMinutes = 5, NewEffect = EffectFactory.Cold(-8, 25) },
                new EventResult("The cold is worse than you thought. Hypothermia risk.", weight: 0.35)
                { TimeAddedMinutes = 10, NewEffect = EffectFactory.Cold(-15, 40) },
                new EventResult("You find a route that naturally shields you from the wind.", weight: 0.15)
                { TimeAddedMinutes = 8 },
                new EventResult("The wind clears snow from something interesting.", weight: 0.10)
                { TimeAddedMinutes = 10, RewardPool = RewardPool.BasicSupplies }
            ]);

        var buildShelter = new EventChoice("Build a Quick Shelter",
            "Pile up snow, lean branches, create a windbreak. It takes time but might be worth it.",
            [
                new EventResult("Worth it. You warm up significantly behind your barrier.", weight: 0.40)
                { TimeAddedMinutes = 25, NewEffect = EffectFactory.Cold(-2, 15) },
                new EventResult("Takes longer than expected, but works.", weight: 0.30)
                { TimeAddedMinutes = 40, NewEffect = EffectFactory.Cold(-3, 20) },
                new EventResult("The wind destroys your shelter. Wasted effort.", weight: 0.20)
                { TimeAddedMinutes = 20, NewEffect = EffectFactory.Cold(-10, 30) },
                new EventResult("Your shelter works too well. You fall asleep and wake up later.", weight: 0.10)
                { TimeAddedMinutes = 60, NewEffect = EffectFactory.Cold(-5, 25) }
            ]);

        evt.AddChoice(findWindbreak);
        evt.AddChoice(keepMoving);
        evt.AddChoice(buildShelter);
        return evt;
    }

    private static GameEvent SuddenClearing(GameContext ctx)
    {
        var evt = new GameEvent("Sudden Clearing",
            "The clouds part. For the first time in hours, you can see clearly. The sun breaks through.");
        evt.BaseWeight = 0.3;  // Rare positive event
        evt.RequiredConditions.Add(EventCondition.IsClear);

        var pushFurther = new EventChoice("Push Further",
            "Use the good weather while it lasts. Get more done.",
            [
                new EventResult("You accomplish more than planned. The good weather holds.", weight: 0.50)
                { TimeAddedMinutes = -10 },  // Negative = time saved
                new EventResult("Weather changes again. You're caught out.", weight: 0.20)
                { TimeAddedMinutes = 15, NewEffect = EffectFactory.Cold(-8, 25) },
                new EventResult("The good weather reveals something you would have missed.", weight: 0.20)
                { TimeAddedMinutes = 10, RewardPool = RewardPool.BasicSupplies },
                new EventResult("Overconfidence. You push too far and have a minor accident.", weight: 0.10)
                { TimeAddedMinutes = 10, NewDamage = new DamageInfo(3, DamageType.Blunt, "accident") }
            ]);

        var restAndRecover = new EventChoice("Rest and Recover",
            "Take a moment to enjoy the warmth. Let your body recover.",
            [
                new EventResult("You feel better for the rest. Energy restored.", weight: 0.60)
                { TimeAddedMinutes = 15 },
                new EventResult("You 'waste' the opportunity but feel much better.", weight: 0.30)
                { TimeAddedMinutes = 20 },
                new EventResult("While resting, you notice something useful nearby.", weight: 0.10)
                { TimeAddedMinutes = 15, RewardPool = RewardPool.BasicSupplies }
            ]);

        var scoutFromHere = new EventChoice("Scout from Here",
            "Use the clear visibility to get your bearings and plan your route.",
            [
                new EventResult("You spot useful landmarks. The rest of the journey is easier.", weight: 0.45)
                { TimeAddedMinutes = 5 },
                new EventResult("You confirm your route is correct. Reassuring.", weight: 0.30)
                { TimeAddedMinutes = 5 },
                new EventResult("You see something concerning. A predator, or a storm on the horizon.", weight: 0.20)
                { TimeAddedMinutes = 5, NewEffect = EffectFactory.Fear(0.15) },
                new EventResult("Nothing special visible. At least you know your surroundings now.", weight: 0.05)
                { TimeAddedMinutes = 10 }
            ]);

        evt.AddChoice(pushFurther);
        evt.AddChoice(restAndRecover);
        evt.AddChoice(scoutFromHere);
        return evt;
    }

    private static GameEvent TreacherousFooting(GameContext ctx)
    {
        var evt = new GameEvent("Treacherous Footing",
            "The ground ahead looks unstable — ice beneath the snow, or loose rocks hidden by debris.");
        evt.BaseWeight = 1.0;

        evt.RequiredConditions.Add(EventCondition.Traveling);
        evt.WeightModifiers.Add(EventCondition.Injured, 1.5);
        evt.WeightModifiers.Add(EventCondition.Slow, 1.3);

        var testCarefully = new EventChoice("Test Carefully",
            "You probe ahead with each step, testing your weight before committing.",
            [
                new EventResult("You find a safe path through.", weight: 0.85f)
                { TimeAddedMinutes = 8 },
                new EventResult("Despite your caution, the ground shifts. You stumble but catch yourself.", weight: 0.15f)
                { TimeAddedMinutes = 12, NewDamage = new DamageInfo(3, DamageType.Blunt, "fall") }
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
                { TimeAddedMinutes = 5, NewDamage = new DamageInfo(4, DamageType.Blunt, "fall") },
                new EventResult("You slip hard. Pain shoots through your ankle.", weight: 0.2f)
                { TimeAddedMinutes = 10, NewEffect = EffectFactory.SprainedAnkle(0.5) }
            ]);

        evt.AddChoice(testCarefully);
        evt.AddChoice(goAround);
        evt.AddChoice(pushThrough);
        return evt;
    }

    private static GameEvent SomethingCatchesYourEye(GameContext ctx)
    {
        var evt = new GameEvent("Something Catches Your Eye",
            "Movement in your peripheral vision — or was it just a shape that doesn't belong? Something about the landscape ahead seems worth a closer look.");
        evt.BaseWeight = 1.5;  // Opportunities feel good

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

    private static GameEvent MinorAccident(GameContext ctx)
    {
        var evt = new GameEvent("Minor Accident",
            "You hurt yourself.");
        evt.BaseWeight = 0.8;  // Punishing, slightly less common

        evt.WeightModifiers.Add(EventCondition.Injured, 1.4);
        evt.WeightModifiers.Add(EventCondition.Slow, 1.3);

        var assessAndTreat = new EventChoice("Stop and Assess",
            "You take a moment to examine the injury and tend to it properly.",
            [
                new EventResult("It's minor — just a scrape. You clean it and move on.", weight: 0.6f)
                { TimeAddedMinutes = 5 },
                new EventResult("A small cut. You bind it to prevent worse.", weight: 0.3f)
                { TimeAddedMinutes = 8, NewDamage = new DamageInfo(2, DamageType.Sharp, "accident") },
                new EventResult("You've twisted something. Rest helps, but it'll slow you down.", weight: 0.1f)
                { TimeAddedMinutes = 12, NewEffect = EffectFactory.SprainedAnkle(0.3) }
            ]);

        var pushOn = new EventChoice("Push On",
            "No time to worry about it. Keep moving.",
            [
                new EventResult("You ignore it. Probably fine.", weight: 0.5f)
                { TimeAddedMinutes = 0, NewDamage = new DamageInfo(2, DamageType.Sharp, "accident") },
                new EventResult("You try to ignore it, but it's affecting your movement.", weight: 0.35f)
                { TimeAddedMinutes = 0, NewDamage = new DamageInfo(3, DamageType.Sharp, "accident") },
                new EventResult("Ignoring it was a mistake. It's getting worse.", weight: 0.15f)
                { TimeAddedMinutes = 0, NewEffect = EffectFactory.SprainedAnkle(0.4) }
            ]);

        var headBack = new EventChoice("Head Back",
            "This might be serious. Better to return to camp.",
            [
                new EventResult("You turn back, favoring the injury.")
                { AbortsExpedition = true, NewDamage = new DamageInfo(2, DamageType.Sharp, "accident") }
            ]);

        evt.AddChoice(assessAndTreat);
        evt.AddChoice(pushOn);
        evt.AddChoice(headBack);
        return evt;
    }

    private static GameEvent FreshCarcass(GameContext ctx)
    {
        var territory = ctx.CurrentLocation.GetFeature<AnimalTerritoryFeature>();
        var animal = territory?.GetRandomAnimalName() ?? "animal";

        var evt = new GameEvent(
            "Fresh Carcass",
            $"Something killed a {animal.ToLower()} recently. The meat's still good, but you didn't make this kill.");
        evt.BaseWeight = 0.5;  // Territory-specific reward

        evt.RequiredConditions.Add(EventCondition.Working);
        evt.RequiredConditions.Add(EventCondition.InAnimalTerritory);

        var scavengeQuick = new EventChoice("Scavenge Quickly",
            "Grab what you can and get out before whatever killed this returns.",
            [
                new EventResult("You cut away some meat and leave.", weight: 0.7f)
                { TimeAddedMinutes = 8, RewardPool = RewardPool.BasicMeat },
                new EventResult("A low growl. You grab what you can and run.", weight: 0.3f)
                { TimeAddedMinutes = 5, RewardPool = RewardPool.BasicMeat, NewEffect = EffectFactory.Fear(0.3) }
            ]);

        var butcherThoroughly = new EventChoice("Butcher Thoroughly",
            "Take your time. Get everything you can from this.",
            [
                new EventResult("You work quickly but thoroughly. A good haul.", weight: 0.5f)
                { TimeAddedMinutes = 25, RewardPool = RewardPool.LargeMeat },
                new EventResult("You're nearly done when something crashes through the brush. You flee.", weight: 0.35f)
                { TimeAddedMinutes = 20, RewardPool = RewardPool.BasicMeat, AbortsExpedition = true },
                new EventResult("It comes back. You barely escape with your life.", weight: 0.15f)
                { TimeAddedMinutes = 15, NewDamage = new DamageInfo(15, DamageType.Sharp, "animal attack"), AbortsExpedition = true }
            ]);

        var leave = new EventChoice("Leave It",
            "Not worth the risk. You move on.",
            [
                new EventResult("You leave the carcass behind.")
                { TimeAddedMinutes = 0 }
            ]);

        evt.AddChoice(scavengeQuick);
        evt.AddChoice(butcherThoroughly);
        evt.AddChoice(leave);
        return evt;
    }

    private static GameEvent Tracks(GameContext ctx)
    {
        var territory = ctx.CurrentLocation.GetFeature<AnimalTerritoryFeature>();
        var animal = territory?.GetRandomAnimalName() ?? "animal";

        var evt = new GameEvent(
            "Tracks",
            $"Fresh {animal.ToLower()} tracks cross your path. They're recent.");
        evt.BaseWeight = 1.2;  // Common scouting signal

        evt.RequiredConditions.Add(EventCondition.InAnimalTerritory);

        var follow = new EventChoice("Follow Them",
            "The trail is clear. You could track this animal.",
            [
                new EventResult("The tracks lead nowhere. You lose the trail.", weight: 0.4f)
                { TimeAddedMinutes = 20 },
                new EventResult("You spot the animal in the distance but can't get close.", weight: 0.35f)
                { TimeAddedMinutes = 25 },
                new EventResult("You find a game trail — good hunting ground.", weight: 0.15f)
                { TimeAddedMinutes = 30, RewardPool = RewardPool.GameTrailDiscovery },
                new EventResult("The tracks were bait. Something was following you.", weight: 0.1f)
                { TimeAddedMinutes = 15, NewDamage = new DamageInfo(10, DamageType.Sharp, "animal attack"), AbortsExpedition = true }
            ]);

        var noteAndContinue = new EventChoice("Note Direction",
            "You mark the direction mentally. Could be useful later.",
            [
                new EventResult("You file the information away and continue.")
                { TimeAddedMinutes = 2 }
            ]);

        var avoid = new EventChoice("Avoid the Area",
            "Best not to cross paths with whatever made these.",
            [
                new EventResult("You detour around. Slower but safer.")
                { TimeAddedMinutes = 10 }
            ]);

        evt.AddChoice(follow);
        evt.AddChoice(noteAndContinue);
        evt.AddChoice(avoid);
        return evt;
    }

    private static GameEvent SomethingWatching(GameContext ctx)
    {
        var territory = ctx.CurrentLocation.GetFeature<AnimalTerritoryFeature>();
        var predator = territory?.GetRandomPredatorName();

        string description = predator != null
            ? $"The hair on your neck stands up. Something is watching. You catch a glimpse of movement — {predator.ToLower()}?"
            : "The hair on your neck stands up. Something is watching you from the shadows.";

        var evt = new GameEvent("Something Watching", description);
        evt.BaseWeight = 0.8;  // Territory-specific tension

        evt.RequiredConditions.Add(EventCondition.Working);
        evt.RequiredConditions.Add(EventCondition.HasPredators);
        evt.WeightModifiers.Add(EventCondition.HasMeat, 3.0);   // 3x more likely when carrying meat
        evt.WeightModifiers.Add(EventCondition.Injured, 2.0);   // 2x more likely when injured

        var makeNoise = new EventChoice("Make Noise",
            "Stand tall, make yourself big, shout. Assert dominance.",
            [
                new EventResult("Whatever it was slinks away. You're not worth the trouble.", weight: 0.6f)
                { TimeAddedMinutes = 5 },
                new EventResult("It doesn't retreat. It's testing you. You back away slowly.", weight: 0.3f)
                { TimeAddedMinutes = 10, NewEffect = EffectFactory.Fear(0.2) },
                new EventResult("Your noise provokes it. It attacks.", weight: 0.1f)
                { TimeAddedMinutes = 5, NewDamage = new DamageInfo(12, DamageType.Sharp, "animal attack"), AbortsExpedition = true }
            ]);

        var finishQuickly = new EventChoice("Finish and Leave",
            "Cut your work short. Get out before it decides you're prey.",
            [
                new EventResult("You gather what you have and leave quickly.")
                { TimeAddedMinutes = 3, AbortsExpedition = true }
            ]);

        var tryToSpot = new EventChoice("Try to Spot It",
            "Knowledge is survival. You need to know what you're dealing with.",
            [
                new EventResult("Just a fox. It watches you work but keeps its distance.", weight: 0.4f)
                { TimeAddedMinutes = 8 },
                new EventResult("You see it now — keeping its distance. It's not attacking yet.", weight: 0.4f)
                { TimeAddedMinutes = 10, NewEffect = EffectFactory.Fear(0.15) },
                new EventResult("You make eye contact. That was a mistake.", weight: 0.2f)
                { TimeAddedMinutes = 5, NewDamage = new DamageInfo(10, DamageType.Sharp, "animal attack"), AbortsExpedition = true }
            ]);

        evt.AddChoice(makeNoise);
        evt.AddChoice(finishQuickly);
        evt.AddChoice(tryToSpot);
        return evt;
    }
}