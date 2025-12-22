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
                        .Rewards(RewardPool.BasicSupplies),
                    new EventResult("The storm arrives faster than expected. You're caught in the thick of it.", 0.40, 25)
                        .WithEffects(EffectFactory.Cold(-15, 45)),
                    new EventResult("The storm changes direction. You wasted time worrying.", 0.15, 10),
                    new EventResult("You find unexpected shelter along the way.", 0.10, 15)
                        .WithEffects(EffectFactory.Cold(-3, 20))
                ])
            .Choice("Seek Shelter Immediately",
                "Drop everything and find cover before the storm hits.",
                [
                    new EventResult("You find good shelter and wait it out safely.", 0.50, 45)
                        .WithEffects(EffectFactory.Cold(-2, 30)),
                    new EventResult("The shelter is poor. You're out of the wind but still cold.", 0.30, 40)
                        .WithEffects(EffectFactory.Cold(-8, 35)),
                    new EventResult("While sheltering, you discover something useful.", 0.15, 50)
                        .Rewards(RewardPool.BasicSupplies),
                    new EventResult("The shelter collapses under the wind. Worse than being outside.", 0.05, 35)
                        .WithEffects(EffectFactory.Cold(-12, 40))
                        .Damage(5, DamageType.Blunt, "debris")
                ])
            .Choice("Head Back Now",
                "Abort the expedition and get back to camp before the storm hits.",
                [
                    new EventResult("You make it back before the storm hits.", 0.55)
                        .Aborts(),
                    new EventResult("The storm catches you partway, but you're closer to camp.", 0.30)
                        .Aborts()
                        .WithEffects(EffectFactory.Cold(-8, 25)),
                    new EventResult("In your haste, you drop something.", 0.10)
                        .Aborts()
                        .Costs(ResourceType.Fuel, 1),
                    new EventResult("You stumble in your rush. Minor injury.", 0.05)
                        .Aborts()
                        .Damage(4, DamageType.Blunt, "fall")
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
                        .WithEffects(EffectFactory.Cold(-10, 30)),
                    new EventResult("You're completely lost. When the snow clears, nothing looks familiar.", 0.20, 45)
                        .WithEffects(EffectFactory.Cold(-15, 45)),
                    new EventResult("You walk straight into a hidden hazard.", 0.10, 15)
                        .Damage(8, DamageType.Blunt, "fall")
                        .WithEffects(EffectFactory.Cold(-8, 25))
                ])
            .Choice("Stop and Wait",
                "Dig in, hunker down, and wait for visibility to return.",
                [
                    new EventResult("The storm passes in about half an hour. You continue.", 0.40, 30)
                        .WithEffects(EffectFactory.Cold(-5, 30)),
                    new EventResult("The storm lasts longer than expected. Your fire margin is in danger.", 0.35, 60)
                        .WithEffects(EffectFactory.Cold(-12, 50)),
                    new EventResult("The cold seeps in despite your efforts. You feel the early signs of hypothermia.", 0.20, 40)
                        .WithEffects(EffectFactory.Cold(-18, 60)),
                    new EventResult("Something finds you while you're stationary.", 0.05, 20)
                        .WithEffects(EffectFactory.Fear(0.4))
                        .Damage(6, DamageType.Sharp, "predator attack")
                        .Aborts()
                ])
            .Choice("Burn Fuel for Warmth",
                "Use some of your fuel to start a fire and wait out the storm in relative comfort.",
                [
                    new EventResult("The fire keeps you warm. You wait out the storm comfortably.", 0.60, 45)
                        .Costs(ResourceType.Fuel, 2),
                    new EventResult("Your fuel runs low before the storm ends. Still better than nothing.", 0.25, 50)
                        .WithEffects(EffectFactory.Cold(-5, 25))
                        .Costs(ResourceType.Fuel, 2),
                    new EventResult("The fire's glow attracts your attention to a landmark. You can navigate now.", 0.10, 20)
                        .Costs(ResourceType.Fuel, 1),
                    new EventResult("The wind snuffs out your fire. Wasted fuel.", 0.05, 35)
                        .WithEffects(EffectFactory.Cold(-10, 35))
                        .Costs(ResourceType.Fuel, 2)
                ]);
    }

    private static GameEvent FrostbiteWarning(GameContext ctx)
    {
        return new GameEvent("Frostbite Warning",
            "Your fingers have gone white. You can't feel your toes. This is getting serious.", 1.2)
            .Requires(EventCondition.ExtremelyCold, EventCondition.Outside, EventCondition.IsExpedition)
            .Choice("Treat It Now",
                "Stop, tuck your hands under your arms, stamp your feet. Get the blood flowing.",
                [
                    new EventResult("You catch it in time. Painful but no lasting damage.", 0.55, 10)
                        .WithEffects(EffectFactory.Cold(-3, 15)),
                    new EventResult("It takes longer than expected, but the feeling returns.", 0.30, 20)
                        .WithEffects(EffectFactory.Cold(-5, 20)),
                    new EventResult("Despite your efforts, the damage was already done. Your fingertips are numb.", 0.15, 15)
                        .Damage(5, DamageType.Internal, "frostbite")
                        .WithEffects(EffectFactory.Frostbite(0.3))
                ])
            .Choice("Push On",
                "You'll deal with it at camp. Just need to get back.",
                [
                    new EventResult("You make it back. The damage is treatable.", 0.35)
                        .Damage(3, DamageType.Internal, "frostbite"),
                    new EventResult("Too late. Some tissue is permanently damaged.", 0.30)
                        .Damage(8, DamageType.Internal, "frostbite"),
                    new EventResult("The movement actually helps circulation. You warm up a bit.", 0.25, 5)
                        .WithEffects(EffectFactory.Cold(-2, 10)),
                    new EventResult("Your body gives out before you reach safety.", 0.10, 30)
                        .Damage(12, DamageType.Internal, "frostbite")
                        .WithEffects(EffectFactory.Cold(-20, 60))
                ])
            .Choice("Burn Supplies",
                "Use fuel or tinder to start an emergency fire. Your extremities are worth more than supplies.",
                [
                    new EventResult("The fire saves your fingers. Worth every stick.", 0.50, 15)
                        .Costs(ResourceType.Fuel, 2),
                    new EventResult("It partially works. Some damage, but you'll keep your fingers.", 0.30, 20)
                        .Damage(3, DamageType.Internal, "frostbite")
                        .Costs(ResourceType.Fuel, 2),
                    new EventResult("The fire doesn't help enough. The damage is done, and you've wasted supplies.", 0.15, 15)
                        .Damage(6, DamageType.Internal, "frostbite")
                        .Costs(ResourceType.Fuel, 2),
                    new EventResult("The attempt makes things worse. Wet hands in extreme cold.", 0.05, 20)
                        .Damage(10, DamageType.Internal, "frostbite")
                        .Costs(ResourceType.Fuel, 1)
                ]);
    }

    private static GameEvent ColdRainSoaking(GameContext ctx)
    {
        return new GameEvent("Cold Rain Soaking",
            "The rain is seeping through everything. You're getting dangerously wet in freezing conditions.", 1.0)
            .Requires(EventCondition.IsRaining, EventCondition.Outside, EventCondition.IsExpedition)
            .Choice("Strip and Wring",
                "Find what cover you can, strip off wet layers, wring them out. Brief exposure, but drier clothes.",
                [
                    new EventResult("It works. You're cold but no longer waterlogged.", 0.50, 15)
                        .WithEffects(EffectFactory.Cold(-5, 20)),
                    new EventResult("The brief exposure causes additional cold damage.", 0.30, 15)
                        .WithEffects(EffectFactory.Cold(-12, 35)),
                    new EventResult("You can't get dry enough. Still soaked.", 0.15, 20)
                        .WithEffects(EffectFactory.Cold(-15, 45)),
                    new EventResult("Something sees you while you're vulnerable.", 0.05, 10)
                        .WithEffects(EffectFactory.Fear(0.3))
                        .Aborts()
                ])
            .Choice("Keep Moving Fast",
                "Generate body heat through movement. If you stop, you'll freeze.",
                [
                    new EventResult("The movement keeps you warm enough to push through.", 0.35, 5)
                        .WithEffects(EffectFactory.Cold(-8, 25)),
                    new EventResult("Exhaustion plus cold. You're in serious trouble.", 0.35, 20)
                        .WithEffects(EffectFactory.Cold(-18, 50))
                        .Damage(3, DamageType.Internal, "exposure"),
                    new EventResult("You find shelter faster than expected.", 0.20, 10)
                        .WithEffects(EffectFactory.Cold(-3, 15)),
                    new EventResult("You push through successfully. Cold but alive.", 0.10, 5)
                ])
            .Choice("Start Emergency Fire",
                "Find what dry materials you can. You need heat desperately.",
                [
                    new EventResult("You find dry tinder under a rock overhang. The fire saves you.", 0.30, 25)
                        .Costs(ResourceType.Fuel, 1),
                    new EventResult("Everything is too wet. The fire won't catch.", 0.35, 20)
                        .WithEffects(EffectFactory.Cold(-12, 40)),
                    new EventResult("Partial success. A small fire buys you time to dry out somewhat.", 0.25, 30)
                        .WithEffects(EffectFactory.Cold(-5, 25))
                        .Costs(ResourceType.Fuel, 1),
                    new EventResult("The fire attempt takes too long. Hypothermia sets in.", 0.10, 35)
                        .WithEffects(EffectFactory.Cold(-20, 60))
                        .Costs(ResourceType.Tinder, 1)
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
                        .WithEffects(EffectFactory.Cold(-3, 20)),
                    new EventResult("While waiting, you hear something useful. Animal sounds, water, voices.", 0.15, 25)
                        .Rewards(RewardPool.GameTrailDiscovery),
                    new EventResult("Something finds you while you're sitting still.", 0.05, 15)
                        .WithEffects(EffectFactory.Fear(0.3))
                        .Aborts()
                ])
            .Choice("Keep Moving Slowly",
                "Careful steps. Watch for landmarks. You'll find your way.",
                [
                    new EventResult("You find your way with only minor delay.", 0.50, 15),
                    new EventResult("You end up somewhere unexpected, but interesting.", 0.25, 20)
                        .Rewards(RewardPool.BasicSupplies),
                    new EventResult("You walk in circles. When the fog clears, you're barely closer.", 0.20, 35),
                    new EventResult("You walk straight into trouble.", 0.05, 10)
                        .Damage(6, DamageType.Blunt, "fall")
                        .Aborts()
                ])
            .Choice("Use the Fog",
                "The fog conceals you too. Maybe you can get close to something that wouldn't normally let you approach.",
                [
                    new EventResult("You get close to game you couldn't normally approach. An opportunity.", 0.30, 25)
                        .Rewards(RewardPool.BasicMeat),
                    new EventResult("You discover something hidden. The fog revealed it by making everything else disappear.", 0.25, 30)
                        .Rewards(RewardPool.HiddenCache),
                    new EventResult("You waste time stalking shadows. Nothing there.", 0.35, 25),
                    new EventResult("The thing you were stalking was stalking you.", 0.10, 15)
                        .Damage(10, DamageType.Sharp, "animal attack")
                        .Aborts()
                ]);
    }

    private static GameEvent BitterWind(GameContext ctx)
    {
        return new GameEvent("Bitter Wind",
            "The wind cuts through your clothes like they're not there. Your body heat is being stripped away.", 1.0)
            .Requires(EventCondition.HighWind, EventCondition.Outside, EventCondition.IsExpedition)
            .Choice("Find a Windbreak",
                "Look for natural cover. Rocks, trees, a depression in the ground.",
                [
                    new EventResult("You find good cover. You warm up and continue.", 0.50, 8)
                        .WithEffects(EffectFactory.Cold(-2, 10)),
                    new EventResult("Partial cover. It helps somewhat.", 0.30, 12)
                        .WithEffects(EffectFactory.Cold(-5, 20)),
                    new EventResult("No good options nearby. You waste time looking.", 0.15, 18)
                        .WithEffects(EffectFactory.Cold(-10, 30)),
                    new EventResult("The windbreak has another problem. You're not alone.", 0.05, 10)
                        .WithEffects(EffectFactory.Fear(0.2))
                ])
            .Choice("Turn Your Back and Keep Moving",
                "Let your clothing do its job. The wind is cold but you can take it.",
                [
                    new EventResult("Cold but manageable. You push through.", 0.40, 5)
                        .WithEffects(EffectFactory.Cold(-8, 25)),
                    new EventResult("The cold is worse than you thought. Hypothermia risk.", 0.35, 10)
                        .WithEffects(EffectFactory.Cold(-15, 40)),
                    new EventResult("You find a route that naturally shields you from the wind.", 0.15, 8),
                    new EventResult("The wind clears snow from something interesting.", 0.10, 10)
                        .Rewards(RewardPool.BasicSupplies)
                ])
            .Choice("Build a Quick Shelter",
                "Pile up snow, lean branches, create a windbreak. It takes time but might be worth it.",
                [
                    new EventResult("Worth it. You warm up significantly behind your barrier.", 0.40, 25)
                        .WithEffects(EffectFactory.Cold(-2, 15)),
                    new EventResult("Takes longer than expected, but works.", 0.30, 40)
                        .WithEffects(EffectFactory.Cold(-3, 20)),
                    new EventResult("The wind destroys your shelter. Wasted effort.", 0.20, 20)
                        .WithEffects(EffectFactory.Cold(-10, 30)),
                    new EventResult("Your shelter works too well. You fall asleep and wake up later.", 0.10, 60)
                        .WithEffects(EffectFactory.Cold(-5, 25), EffectFactory.Rested(0.2, 60))
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
                        .WithEffects(EffectFactory.Cold(-8, 25)),
                    new EventResult("The good weather reveals something you would have missed.", 0.20, 10)
                        .Rewards(RewardPool.BasicSupplies),
                    new EventResult("Overconfidence. You push too far and have a minor accident.", 0.10, 10)
                        .Damage(3, DamageType.Blunt, "accident")
                ])
            .Choice("Rest and Recover",
                "Take a moment to enjoy the warmth. Let your body recover.",
                [
                    new EventResult("You feel better for the rest. Energy restored.", 0.60, 15),
                    new EventResult("You 'waste' the opportunity but feel much better.", 0.30, 20),
                    new EventResult("While resting, you notice something useful nearby.", 0.10, 15)
                        .Rewards(RewardPool.BasicSupplies)
                ])
            .Choice("Scout from Here",
                "Use the clear visibility to get your bearings and plan your route.",
                [
                    new EventResult("You spot useful landmarks. The rest of the journey is easier.", 0.45, 5),
                    new EventResult("You confirm your route is correct. Reassuring.", 0.30, 5),
                    new EventResult("You see something concerning. A predator, or a storm on the horizon.", 0.20, 5)
                        .WithEffects(EffectFactory.Fear(0.15)),
                    new EventResult("Nothing special visible. At least you know your surroundings now.", 0.05, 10)
                ]);
    }
}
