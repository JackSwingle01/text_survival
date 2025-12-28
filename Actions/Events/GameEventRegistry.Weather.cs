using text_survival.Actions.Variants;
using text_survival.Bodies;
using text_survival.Effects;
using text_survival.Items;

namespace text_survival.Actions;

public static partial class GameEventRegistry
{
    private static GameEvent StormApproaching(GameContext ctx)
    {
        return new GameEvent("Storm Approaching",
            "The sky darkens. The wind is picking up fast. A serious storm is coming.", 2.0)
            .Requires(EventCondition.WeatherWorsening, EventCondition.IsExpedition)
            .Choice("Race to Finish",
                "Try to complete what you came for before the worst hits.",
                [
                    new EventResult("You finish just in time and escape before the worst of it.", 0.35, 5)
                        .FindsSupplies(),
                    new EventResult("The storm arrives faster than expected. You're caught in the thick of it.", 0.40, 25)
                        .HarshCold(),
                    new EventResult("The storm changes direction. You wasted time worrying.", 0.15, 10),
                    new EventResult("You find unexpected shelter along the way.", 0.10, 15)
                        .LightChill()
                ])
            .Choice("Seek Shelter Immediately",
                "Drop everything and find cover before the storm hits.",
                [
                    new EventResult("You find good shelter and wait it out safely.", 0.50, 45)
                        .StormSheltered(),
                    new EventResult("The shelter is poor. You're out of the wind but still cold.", 0.30, 40)
                        .PartialShelter(),
                    new EventResult("While sheltering, you discover something useful.", 0.15, 50)
                        .FindsSupplies(),
                    new EventResult("The shelter collapses under the wind. Worse than being outside.", 0.05, 35)
                        .WithCold(-12, 40)
                        .DebrisDamage(5)
                ])
            .Choice("Head Back Now",
                "Abort the expedition and get back to camp before the storm hits.",
                [
                    new EventResult("You make it back before the storm hits.", 0.55)
                        .Aborts(),
                    new EventResult("The storm catches you partway, but you're closer to camp.", 0.30)
                        .Aborts()
                        .WithCold(-8, 25),
                    new EventResult("In your haste, you drop something.", 0.10)
                        .Aborts()
                        .Costs(ResourceType.Fuel, 1),
                    new EventResult("You stumble in your rush. Minor injury.", 0.05)
                        .Aborts()
                        .MinorFall()
                ]);
    }

    private static GameEvent Whiteout(GameContext ctx)
    {
        return new GameEvent("Whiteout",
            "The snow is so thick you can't see your hand in front of your face. You've lost all sense of direction.", 1.5)
            .Requires(EventCondition.IsBlizzard, EventCondition.Traveling)
            .Choice("Keep Moving",
                "Trust your instincts and keep walking. You'll find your way.",
                [
                    new EventResult("Your instincts serve you well. You stay on course.", 0.30, 10),
                    new EventResult("You drift off course. It takes time to correct.", 0.40, 25)
                        .WithCold(-10, 30),
                    new EventResult("You're completely lost. When the snow clears, nothing looks familiar.", 0.20, 45)
                        .HarshCold(),
                    new EventResult("You walk straight into a hidden hazard.", 0.10, 15)
                        .SeriousFall()
                        .WithCold(-8, 25)
                ])
            .Choice("Stop and Wait",
                "Dig in, hunker down, and wait for visibility to return.",
                [
                    new EventResult("The storm passes in about half an hour. You continue.", 0.40, 30)
                        .MinorCold(),
                    new EventResult("The storm lasts longer than expected. Your fire is in danger.", 0.35, 60)
                        .WithCold(-12, 50),
                    new EventResult("The cold seeps in despite your efforts. You feel the early signs of hypothermia.", 0.20, 40)
                        .DangerousCold(),
                    new EventResult("Something finds you while you're stationary.", 0.05, 20)
                        .MinorBite()
                        .Aborts()
                ])
            .Choice("Burn Fuel for Warmth",
                "Use some of your fuel to start a fire and wait out the storm in relative comfort.",
                [
                    new EventResult("The fire keeps you warm. You wait out the storm comfortably.", 0.60, 45)
                        .BurnsFuel(2),
                    new EventResult("Your fuel runs low before the storm ends. Still better than nothing.", 0.25, 50)
                        .WithCold(-5, 25)
                        .BurnsFuel(2),
                    new EventResult("The fire's glow attracts your attention to a landmark. You can navigate now.", 0.10, 20)
                        .BurnsFuel(1),
                    new EventResult("The wind snuffs out your fire. Wasted fuel.", 0.05, 35)
                        .StormExposure()
                        .BurnsFuel(2)
                ]);
    }

    /// <summary>
    /// Unified cold exposure event that adapts to weather conditions.
    /// Uses ColdExposureVariant to match description with mechanics.
    /// </summary>
    private static GameEvent ColdExposure(GameContext ctx)
    {
        var variant = ColdExposureSelector.SelectByWeather(ctx);

        var evt = new GameEvent(variant.EventName, variant.Description, 1.0)
            .Requires(EventCondition.Outside, EventCondition.IsExpedition);

        // Add type-specific conditions
        switch (variant.ExposureType)
        {
            case ColdExposureType.ExtremeCold:
                evt.Requires(EventCondition.ExtremelyCold);
                break;
            case ColdExposureType.WetCold:
                evt.Requires(EventCondition.IsRaining, EventCondition.LowTemperature);
                break;
            case ColdExposureType.WindChill:
                evt.Requires(EventCondition.HighWind);
                break;
        }

        // Build choices based on exposure type
        AddColdExposureChoices(evt, variant);

        return evt;
    }

    private static void AddColdExposureChoices(GameEvent evt, ColdExposureVariant variant)
    {
        switch (variant.ExposureType)
        {
            case ColdExposureType.ExtremeCold:
                AddExtremeColdChoices(evt);
                break;
            case ColdExposureType.WetCold:
                AddWetColdChoices(evt);
                break;
            case ColdExposureType.WindChill:
                AddWindChillChoices(evt);
                break;
        }
    }

    private static void AddExtremeColdChoices(GameEvent evt)
    {
        evt.Choice("Treat It Now",
            "Stop, tuck your hands under your arms, stamp your feet. Get the blood flowing.",
            [
                new EventResult("You catch it in time. Painful but no lasting damage.", 0.55, 10)
                    .WithCold(-3, 15),
                new EventResult("It takes longer than expected, but the feeling returns.", 0.30, 20)
                    .WithCold(-5, 20),
                new EventResult("Despite your efforts, the damage was already done. Your fingertips are numb.", 0.15, 15)
                    .WithFrostbite(5, 0.3)
            ])
        .Choice("Push On",
            "You'll deal with it at camp. Just need to get back.",
            [
                new EventResult("You make it back. The damage is treatable.", 0.35)
                    .MinorFrostbite(),
                new EventResult("Too late. Some tissue is permanently damaged.", 0.30)
                    .ModerateFrostbite(),
                new EventResult("The movement actually helps circulation. You warm up a bit.", 0.25, 5)
                    .WithCold(-2, 10),
                new EventResult("Your body gives out before you reach safety.", 0.10, 30)
                    .SevereFrostbite()
                    .SevereCold()
            ])
        .Choice("Burn Supplies",
            "Use fuel or tinder to start an emergency fire. Your extremities are worth more than supplies.",
            [
                new EventResult("The fire saves your fingers. Worth every stick.", 0.50, 15)
                    .BurnsFuel(2),
                new EventResult("It partially works. Some damage, but you'll keep your fingers.", 0.30, 20)
                    .MinorFrostbite()
                    .BurnsFuel(2),
                new EventResult("The fire doesn't help enough. The damage is done, and you've wasted supplies.", 0.15, 15)
                    .ModerateFrostbite()
                    .BurnsFuel(2),
                new EventResult("The attempt makes things worse. Wet hands in extreme cold.", 0.05, 20)
                    .Damage(10, DamageType.Internal)
                    .BurnsFuel(1)
            ]);
    }

    private static void AddWetColdChoices(GameEvent evt)
    {
        evt.Choice("Strip and Wring",
            "Find what cover you can, strip off wet layers, wring them out. Brief exposure, but drier clothes.",
            [
                new EventResult("It works. You're cold but no longer waterlogged.", 0.50, 15)
                    .WithCold(-5, 20),
                new EventResult("The brief exposure causes additional cold damage.", 0.30, 15)
                    .WithCold(-12, 35),
                new EventResult("You can't get dry enough. Still soaked.", 0.15, 20)
                    .HarshCold(),
                new EventResult("Something sees you while you're vulnerable.", 0.05, 10)
                    .Frightening()
                    .Aborts()
            ])
        .Choice("Keep Moving Fast",
            "Generate body heat through movement. If you stop, you'll freeze.",
            [
                new EventResult("The movement keeps you warm enough to push through.", 0.35, 5)
                    .WithCold(-8, 25),
                new EventResult("Exhaustion plus cold. You're in serious trouble.", 0.35, 20)
                    .WithCold(-18, 50)
                    .ExposureDamage(3),
                new EventResult("You find shelter faster than expected.", 0.20, 10)
                    .WithCold(-3, 15),
                new EventResult("You push through successfully. Cold but alive.", 0.10, 5)
            ])
        .Choice("Start Emergency Fire",
            "Find what dry materials you can. You need heat desperately.",
            [
                new EventResult("You find dry tinder under a rock overhang. The fire saves you.", 0.30, 25)
                    .QuickFire(),
                new EventResult("Everything is too wet. The fire won't catch.", 0.35, 20)
                    .WithCold(-12, 40),
                new EventResult("Partial success. A small fire buys you time to dry out somewhat.", 0.25, 30)
                    .WithCold(-5, 25)
                    .QuickFire(),
                new EventResult("The fire attempt takes too long. Hypothermia sets in.", 0.10, 35)
                    .SevereCold()
                    .WastesTinder()
            ]);
    }

    private static GameEvent LostInFog(GameContext ctx)
    {
        return new GameEvent("Lost in Fog",
            "The fog is disorienting. Every direction looks the same. You're not sure which way you came from.", 1.0)
            .Requires(EventCondition.IsMisty, EventCondition.Traveling)
            .Choice("Wait for it to Lift",
                "Sit tight. The fog will clear eventually.",
                [
                    new EventResult("The fog clears in about twenty minutes. You continue on your way.", 0.45, 20),
                    new EventResult("The fog persists. You lose significant time waiting.", 0.35, 40)
                        .LightChill(),
                    new EventResult("While waiting, you hear something useful. Animal sounds, water, voices.", 0.15, 25)
                        .FindsGameTrail(),
                    new EventResult("Something finds you while you're sitting still.", 0.05, 15)
                        .Frightening()
                        .Aborts()
                ])
            .Choice("Keep Moving Slowly",
                "Careful steps. Watch for landmarks. You'll find your way.",
                [
                    new EventResult("You find your way with only minor delay.", 0.50, 15),
                    new EventResult("You end up somewhere unexpected, but interesting.", 0.25, 20)
                        .FindsSupplies(),
                    new EventResult("You walk in circles. When the fog clears, you're barely closer.", 0.20, 35),
                    new EventResult("You walk straight into trouble.", 0.05, 10)
                        .ModerateFall()
                        .Aborts()
                ])
            .Choice("Use the Fog",
                "The fog conceals you too. Maybe you can get close to something that wouldn't normally let you approach.",
                [
                    new EventResult("You get close to game you couldn't normally approach. An opportunity.", 0.30, 25)
                        .FindsMeat(),
                    new EventResult("You discover something hidden. The fog revealed it by making everything else disappear.", 0.25, 30)
                        .FindsCache(),
                    new EventResult("You waste time stalking shadows. Nothing there.", 0.35, 25),
                    new EventResult("The thing you were stalking was stalking you.", 0.10, 15)
                        .AnimalAttack()
                        .Aborts()
                ]);
    }

    private static void AddWindChillChoices(GameEvent evt)
    {
        evt.Choice("Find a Windbreak",
            "Look for natural cover. Rocks, trees, a depression in the ground.",
            [
                new EventResult("You find good cover. You warm up and continue.", 0.50, 8)
                    .WithCold(-2, 10),
                new EventResult("Partial cover. It helps somewhat.", 0.30, 12)
                    .WithCold(-5, 20),
                new EventResult("No good options nearby. You waste time looking.", 0.15, 18)
                    .WithCold(-10, 30),
                new EventResult("The windbreak has another problem. You're not alone.", 0.05, 10)
                    .Unsettling()
            ])
        .Choice("Turn Your Back and Keep Moving",
            "Let your clothing do its job. The wind is cold but you can take it.",
            [
                new EventResult("Cold but manageable. You push through.", 0.40, 5)
                    .WithCold(-8, 25),
                new EventResult("The cold is worse than you thought. Hypothermia risk.", 0.35, 10)
                    .WithCold(-15, 40),
                new EventResult("You find a route that naturally shields you from the wind.", 0.15, 8),
                new EventResult("The wind clears snow from something interesting.", 0.10, 10)
                    .FindsSupplies()
            ])
        .Choice("Build a Quick Shelter",
            "Pile up snow, lean branches, create a windbreak. It takes time but might be worth it.",
            [
                new EventResult("Worth it. You warm up significantly behind your barrier.", 0.40, 25)
                    .WithCold(-2, 15),
                new EventResult("Takes longer than expected, but works.", 0.30, 40)
                    .LightChill(),
                new EventResult("The wind destroys your shelter. Wasted effort.", 0.20, 20)
                    .WithCold(-10, 30),
                new EventResult("Your shelter works too well. You fall asleep and wake up later.", 0.10, 60)
                    .WithEffects(EffectFactory.Cold(-5, 25), EffectFactory.Rested(0.5, 60))
            ]);
    }

    private static GameEvent SuddenClearing(GameContext ctx)
    {
        return new GameEvent("Sudden Clearing",
            "The clouds part. For the first time in hours, you can see clearly. The sun breaks through.", 0.3)
            .Requires(EventCondition.IsClear, EventCondition.IsExpedition)
            .Choice("Push Further",
                "Use the good weather while it lasts. Get more done.",
                [
                    new EventResult("You accomplish more than planned. The good weather holds.", 0.50, -10),
                    new EventResult("Weather changes again. You're caught out.", 0.20, 15)
                        .WithCold(-8, 25),
                    new EventResult("The good weather reveals something you would have missed.", 0.20, 10)
                        .FindsSupplies(),
                    new EventResult("Overconfidence. You push too far and have a minor accident.", 0.10, 10)
                        .MinorFall()
                ])
            .Choice("Rest and Recover",
                "Take a moment to enjoy the warmth. Let your body recover.",
                [
                    new EventResult("You feel better for the rest. Energy restored.", 0.60, 15),
                    new EventResult("You 'waste' the opportunity but feel much better.", 0.30, 20),
                    new EventResult("While resting, you notice something useful nearby.", 0.10, 15)
                        .FindsSupplies()
                ])
            .Choice("Scout from Here",
                "Use the clear visibility to get your bearings and plan your route.",
                [
                    new EventResult("You spot useful landmarks. The rest of the journey is easier.", 0.45, 5),
                    new EventResult("You confirm your route is correct. Reassuring.", 0.30, 5),
                    new EventResult("You see something concerning. A predator, or a storm on the horizon.", 0.20, 5)
                        .Shaken(),
                    new EventResult("Nothing special visible. At least you know your surroundings now.", 0.05, 10)
                ]);
    }

    /// <summary>
    /// Prolonged Blizzard Warning - triggers during the calm phase before a massive blizzard
    /// Guaranteed to fire (high weight) when ProlongedBlizzard front is active at state index 0
    /// </summary>
    private static GameEvent MassiveStormApproaching(GameContext ctx)
    {
        return new GameEvent("The Calm",
            "The wind dies. The sky clears to an unnatural brightness. " +
            "To the north, a band of grey-white stretches across the horizon. " +
            "The trees have gone silent. The birds have fled.\n\n" +
            "This is going to be bad. A storm unlike any you've seen is building. " +
            "You have perhaps a day before it arrives in earnest.",
            10.0)  // Very high weight to guarantee trigger
            .Requires(EventCondition.CalmBeforeTheStorm)
            .Choice("Continue",
                "You make note of the warning signs.",
                [new EventResult("You will need to prepare.", 1.0, 0)]);
    }
}
