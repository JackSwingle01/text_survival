using text_survival.Bodies;
using text_survival.Effects;
using text_survival.Environments.Features;
using text_survival.Items;

namespace text_survival.Actions;

public static partial class GameEventRegistry
{
    // === WILDLIFE EVENTS ===

    private static GameEvent FreshCarcass(GameContext ctx)
    {
        var territory = ctx.CurrentLocation.GetFeature<AnimalTerritoryFeature>();
        var animal = territory?.GetRandomAnimalName() ?? "animal";

        var evt = new GameEvent(
            "Fresh Carcass",
            $"Something killed a {animal.ToLower()} recently. The meat's still good, but you didn't make this kill.", 0.5);
        evt.BaseWeight = 0.5;  // Territory-specific reward

        evt.RequiredConditions.Add(EventCondition.Working);
        evt.RequiredConditions.Add(EventCondition.InAnimalTerritory);

        var scavengeQuick = new EventChoice("Scavenge Quickly",
            "Grab what you can and get out before whatever killed this returns.",
            [
                new EventResult("You cut away some meat and leave.", weight: 0.7f)
                { TimeAddedMinutes = 8, RewardPool = RewardPool.BasicMeat },
                new EventResult("A low growl. You grab what you can and run.", weight: 0.3f)
                { TimeAddedMinutes = 5, RewardPool = RewardPool.BasicMeat, Effects = [EffectFactory.Fear(0.3)] }
            ]);

        var butcherThoroughly = new EventChoice("Butcher Thoroughly",
            "Take your time. Get everything you can from this.",
            [
                new EventResult("You work quickly but thoroughly. A good haul.", weight: 0.5f)
                { TimeAddedMinutes = 25, RewardPool = RewardPool.LargeMeat },
                new EventResult("You're nearly done when something crashes through the brush. You flee.", weight: 0.35f)
                { TimeAddedMinutes = 20, RewardPool = RewardPool.LargeMeat, AbortsExpedition = true },
                new EventResult("It comes back. You barely escape with your life, taking some meat with you.", weight: 0.15f)
                { TimeAddedMinutes = 15, NewDamage = new DamageInfo(15, DamageType.Sharp, "animal attack"),
                  RewardPool = RewardPool.BasicMeat, AbortsExpedition = true }
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

        evt.RequiredConditions.Add(EventCondition.IsExpedition);
        evt.RequiredConditions.Add(EventCondition.InAnimalTerritory);

        var follow = new EventChoice("Follow Them",
            "The trail is clear. You could track this animal.",
            [
                new EventResult("The tracks lead nowhere. You lose the trail.", weight: 0.4f, 20),
                new EventResult("You spot the animal in the distance but can't get close.", weight: 0.35f)
                { TimeAddedMinutes = 25 },
                new EventResult("You find a game trail — good hunting ground.", weight: 0.15f)
                { TimeAddedMinutes = 30, RewardPool = RewardPool.GameTrailDiscovery },
                new EventResult("The tracks were bait. Something was following you.", weight: 0.1f)
                { TimeAddedMinutes = 15, NewDamage = new DamageInfo(10, DamageType.Sharp, "animal attack"),
                  CreatesTension = new TensionCreation("Stalked", 0.4, AnimalType: animal),
                  AbortsExpedition = true }
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
        var predator = territory?.GetRandomPredatorName() ?? "Wolf";

        string description = $"The hair on your neck stands up. Something is watching. You catch a glimpse of movement — {predator.ToLower()}?";

        var evt = new GameEvent("Something Watching", description);
        evt.BaseWeight = 0.8;

        evt.RequiredConditions.Add(EventCondition.Working);
        evt.RequiredConditions.Add(EventCondition.HasPredators);
        evt.WeightModifiers.Add(EventCondition.HasMeat, 3.0);
        evt.WeightModifiers.Add(EventCondition.Injured, 2.0);

        var makeNoise = new EventChoice("Make Noise",
            "Stand tall, make yourself big, shout. Assert dominance.",
            [
                new EventResult("Whatever it was slinks away. You're not worth the trouble.", weight: 0.60)
                { TimeAddedMinutes = 5 },
                new EventResult("It doesn't retreat. It's testing you. You back away slowly.", weight: 0.25)
                { TimeAddedMinutes = 10, Effects = [EffectFactory.Fear(0.2),],
                  CreatesTension = new TensionCreation("Stalked", 0.3, AnimalType: predator) },
                new EventResult("Your noise provokes it. It attacks.", weight: 0.10)
                { TimeAddedMinutes = 5, NewDamage = new DamageInfo(12, DamageType.Sharp, "animal attack"), AbortsExpedition = true },
                new EventResult("Nothing there. Just paranoia.", weight: 0.05)
                { TimeAddedMinutes = 3, Effects = [EffectFactory.Shaken(0.15)] }
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
                new EventResult("Just a fox. It watches you work but keeps its distance.", weight: 0.40)
                { TimeAddedMinutes = 8 },
                new EventResult("You see it now — keeping its distance. It's not attacking yet.", weight: 0.35)
                { TimeAddedMinutes = 10, Effects = [EffectFactory.Fear(0.15)],
                  CreatesTension = new TensionCreation("Stalked", 0.25, AnimalType: predator) },
                new EventResult("You make eye contact. That was a mistake.", weight: 0.15)
                { TimeAddedMinutes = 5, NewDamage = new DamageInfo(10, DamageType.Sharp, "animal attack"), AbortsExpedition = true },
                new EventResult("Can't see it but you KNOW it's there.", weight: 0.10)
                { TimeAddedMinutes = 10, Effects = [EffectFactory.Fear(0.3)],
                  CreatesTension = new TensionCreation("Stalked", 0.4, AnimalType: predator) }
            ]);

        evt.AddChoice(makeNoise);
        evt.AddChoice(finishQuickly);
        evt.AddChoice(tryToSpot);
        return evt;
    }

    private static GameEvent RavenCall(GameContext ctx)
    {
        var evt = new GameEvent("Raven Call",
            "Ravens circling overhead. They've spotted something — or someone. They're watching you.");
        evt.BaseWeight = 0.6;

        evt.RequiredConditions.Add(EventCondition.Working);
        evt.WeightModifiers.Add(EventCondition.LowOnFood, 1.5);

        var followThem = new EventChoice("Follow Them",
            "Ravens often lead to carcasses or resources.",
            [
                new EventResult("They lead you to a small carcass.", weight: 0.35)
                { TimeAddedMinutes = 25, RewardPool = RewardPool.BasicMeat },
                new EventResult("They lead nowhere. Wasting your time.", weight: 0.25)
                { TimeAddedMinutes = 30 },
                new EventResult("They lead you to another predator's kill.", weight: 0.20)
                { TimeAddedMinutes = 25, CreatesTension = new TensionCreation("Stalked", 0.25) },
                new EventResult("They lead you somewhere dangerous.", weight: 0.10)
                { TimeAddedMinutes = 20, SpawnEncounter = new EncounterConfig("Wolf", 25, 0.5) },
                new EventResult("They lead you to something unexpected.", weight: 0.10)
                { TimeAddedMinutes = 30, RewardPool = RewardPool.HiddenCache }
            ]);

        var ignore = new EventChoice("Ignore Them",
            "They're just birds.",
            [
                new EventResult("You continue working. They circle away eventually.", weight: 1.0)
                { TimeAddedMinutes = 0 }
            ]);

        evt.AddChoice(followThem);
        evt.AddChoice(ignore);
        return evt;
    }

    // === STALKER ARC EVENTS ===

    private static GameEvent StalkerCircling(GameContext ctx)
    {
        var stalkedTension = ctx.Tensions.GetTension("Stalked");
        var predator = stalkedTension?.AnimalType ?? "predator";

        var evt = new GameEvent("Stalker Circling",
            $"You catch movement in your peripheral vision. Again. The {predator.ToLower()} is pacing you, staying just out of clear sight. Testing.");
        evt.BaseWeight = 1.5;

        evt.RequiredConditions.Add(EventCondition.Stalked);

        var confront = new EventChoice("Confront It Now",
            "Turn and face it. Better to fight on your terms.",
            [
                new EventResult("You spin to face it. The confrontation is now.", weight: 1.0)
                { TimeAddedMinutes = 5, ResolvesTension = "Stalked",
                  SpawnEncounter = new EncounterConfig(predator, 20, stalkedTension?.Severity ?? 0.5) }
            ]);

        var tryToLose = new EventChoice("Try to Lose It",
            "Double back, cross water, break your trail.",
            [
                new EventResult("You double back, cross water, break your trail. It works.", weight: 0.35)
                { TimeAddedMinutes = 25, ResolvesTension = "Stalked" },
                new EventResult("It stays with you. You've wasted time and energy.", weight: 0.35)
                { TimeAddedMinutes = 20, EscalateTension = ("Stalked", 0.2) },
                new EventResult("You get turned around trying to lose it.", weight: 0.20)
                { TimeAddedMinutes = 35, Effects = [EffectFactory.Cold(-8, 30), EffectFactory.Shaken(0.2)] },
                new EventResult("Your evasion leads you somewhere unexpected.", weight: 0.10)
                { TimeAddedMinutes = 30 }
            ]);

        var keepMoving = new EventChoice("Keep Moving, Stay Alert",
            "Maintain distance. Don't show weakness.",
            [
                new EventResult("You maintain distance. Exhausting but stable.", weight: 0.40)
                { TimeAddedMinutes = 10 },
                new EventResult("It's getting bolder.", weight: 0.30)
                { TimeAddedMinutes = 8, EscalateTension = ("Stalked", 0.15) },
                new EventResult("It backs off. Maybe lost interest.", weight: 0.20)
                { TimeAddedMinutes = 5, EscalateTension = ("Stalked", -0.1) },
                new EventResult("It commits.", weight: 0.10)
                { TimeAddedMinutes = 5, ResolvesTension = "Stalked",
                  SpawnEncounter = new EncounterConfig(predator, 15, 0.6) }
            ]);

        var returnToCamp = new EventChoice("Return to Camp",
            "Head back now. Fire deters predators.",
            [
                new EventResult("You make it back. Fire deters it.", weight: 0.60)
                { ResolvesTension = "Stalked", AbortsExpedition = true },
                new EventResult("It follows to camp perimeter but won't approach fire.", weight: 0.25)
                { ResolvesTension = "Stalked", AbortsExpedition = true, Effects = [EffectFactory.Fear(0.2)] },
                new EventResult("It's bolder than you thought. Attacks before you reach safety.", weight: 0.15)
                { TimeAddedMinutes = 5, AbortsExpedition = true, ResolvesTension = "Stalked",
                  SpawnEncounter = new EncounterConfig(predator, 10, 0.8) }
            ]);

        evt.AddChoice(confront);
        evt.AddChoice(tryToLose);
        evt.AddChoice(keepMoving);
        evt.AddChoice(returnToCamp);
        return evt;
    }

    private static GameEvent PredatorRevealed(GameContext ctx)
    {
        var stalkedTension = ctx.Tensions.GetTension("Stalked");
        var predator = stalkedTension?.AnimalType ?? "Wolf";

        var evt = new GameEvent("The Predator Revealed",
            $"You finally see it clearly. A {predator.ToLower()}. It's watching you from maybe thirty feet away. Not hiding anymore.");
        evt.BaseWeight = 2.0;

        evt.RequiredConditions.Add(EventCondition.StalkedHigh);

        var standGround = new EventChoice("Stand Your Ground",
            "Face it. This ends now.",
            [
                new EventResult("You turn to face it. The confrontation is inevitable.", weight: 1.0)
                { TimeAddedMinutes = 5, ResolvesTension = "Stalked",
                  SpawnEncounter = new EncounterConfig(predator, 30, stalkedTension?.Severity ?? 0.6) }
            ]);

        var calculatedRetreat = new EventChoice("Calculated Retreat",
            "Slow, deliberate backward movement. Don't run. Don't look away.",
            [
                new EventResult("You back away slowly. It watches but doesn't follow.", weight: 0.45)
                { TimeAddedMinutes = 15, ResolvesTension = "Stalked" },
                new EventResult("It follows at a distance. You're not out of this yet.", weight: 0.30)
                { TimeAddedMinutes = 10, EscalateTension = ("Stalked", 0.2) },
                new EventResult("Your retreat emboldens it. It charges.", weight: 0.25)
                { TimeAddedMinutes = 5, ResolvesTension = "Stalked",
                  SpawnEncounter = new EncounterConfig(predator, 15, 0.75) }
            ]);

        evt.AddChoice(standGround);
        evt.AddChoice(calculatedRetreat);
        return evt;
    }

    private static GameEvent Ambush(GameContext ctx)
    {
        var stalkedTension = ctx.Tensions.GetTension("Stalked");
        var predator = stalkedTension?.AnimalType ?? "Wolf";

        var evt = new GameEvent("Ambush",
            $"It's done waiting. The {predator.ToLower()} bursts from cover.");
        evt.BaseWeight = 3.0;

        evt.RequiredConditions.Add(EventCondition.StalkedCritical);

        // No real choice - this is the consequence of letting tension escalate too far
        var brace = new EventChoice("Brace Yourself",
            "No time to run. It's on you.",
            [
                new EventResult("The predator attacks!", weight: 1.0)
                { TimeAddedMinutes = 3, ResolvesTension = "Stalked",
                  SpawnEncounter = new EncounterConfig(predator, 5, 0.9) }
            ]);

        evt.AddChoice(brace);
        return evt;
    }

    // === BODY EVENTS ===

    private static GameEvent TheShakes(GameContext ctx)
    {
        var evt = new GameEvent("The Shakes",
            "It's not just the cold. Your blood sugar has crashed. Your hands are trembling so violently you can barely hold anything.");
        evt.BaseWeight = 1.0;

        evt.RequiredConditions.Add(EventCondition.LowCalories);
        evt.WeightModifiers.Add(EventCondition.LowTemperature, 2.0);

        var eatImmediately = new EventChoice("Eat Immediately",
            "You need food now.",
            [
                new EventResult("Warmth spreads through you. The shaking stops.", weight: 0.70)
                { TimeAddedMinutes = 5, Cost = new ResourceCost(ResourceType.Food, 1),
                  Effects = [EffectFactory.Focused(0.2, 90)] },
                new EventResult("Takes the edge off. Still shaky.", weight: 0.20)
                { TimeAddedMinutes = 5, Cost = new ResourceCost(ResourceType.Food, 1) },
                new EventResult("You eat too fast. Nauseous.", weight: 0.10)
                { TimeAddedMinutes = 8, Cost = new ResourceCost(ResourceType.Food, 1),
                  Effects = [EffectFactory.Nauseous(0.2, 30)] }
            ],
            [EventCondition.HasFood]);

        var warmByFire = new EventChoice("Warm Up by Fire",
            "Heat helps. Get close to the flames. (Risk: exhaustion may cause sleep)",
            [
                new EventResult("Heat helps. Shaking subsides.", weight: 0.70)
                { TimeAddedMinutes = 20, Effects = [EffectFactory.Warmed(0.3, 30)] },
                new EventResult("Takes longer but works.", weight: 0.25)
                { TimeAddedMinutes = 35 },
                new EventResult("You doze off by the fire. Time lost, but you feel better.", weight: 0.05)
                { TimeAddedMinutes = 60, Effects = [EffectFactory.Rested(0.2, 60)] }
            ],
            [EventCondition.NearFire]);

        var pushThrough = new EventChoice("Push Through",
            "Mind over matter. Keep working.",
            [
                new EventResult("Mind over matter. Shaking fades to background.", weight: 0.40)
                { Effects = [EffectFactory.Shaken(0.3)] },
                new EventResult("You drop something. Minor setback.", weight: 0.35)
                { TimeAddedMinutes = 5, Effects = [EffectFactory.Shaken(0.3)] },
                new EventResult("Can't function. Forced rest.", weight: 0.15)
                { TimeAddedMinutes = 30 },
                new EventResult("You push through and acclimate.", weight: 0.10)
                { Effects = [EffectFactory.Hardened(0.2, 120)] }
            ]);

        evt.AddChoice(eatImmediately);
        evt.AddChoice(warmByFire);
        evt.AddChoice(pushThrough);
        return evt;
    }

    private static GameEvent GutWrench(GameContext ctx)
    {
        var evt = new GameEvent("Gut Wrench",
            "Your stomach twists. Something you ate isn't sitting right. At all.");
        evt.BaseWeight = 0.6;

        // More likely when low on resources (eating questionable food)
        evt.WeightModifiers.Add(EventCondition.LowOnFood, 2.0);

        var induceVomiting = new EventChoice("Induce Vomiting",
            "Get it out before it gets worse.",
            [
                new EventResult("Painful but effective. You feel emptied out but better.", weight: 1.0)
                { TimeAddedMinutes = 10, Cost = new ResourceCost(ResourceType.Water, 1) }
            ]);

        var bearIt = new EventChoice("Bear It",
            "Your body will handle it. Probably.",
            [
                new EventResult("It passes eventually. Uncomfortable but manageable.", weight: 0.40)
                { TimeAddedMinutes = 15, Effects = [EffectFactory.Nauseous(0.4, 120)] },
                new EventResult("Worse than expected. You're really sick.", weight: 0.30)
                { TimeAddedMinutes = 20, Effects = [EffectFactory.Nauseous(0.6, 180),],
                  NewDamage = new DamageInfo(3, DamageType.Internal, "food poisoning") },
                new EventResult("Your body handles it. You feel tougher for it.", weight: 0.20)
                { TimeAddedMinutes = 10, Effects = [EffectFactory.Hardened(0.15, 240)] },
                new EventResult("Serious food poisoning. This is bad.", weight: 0.10)
                { TimeAddedMinutes = 30, Effects = [EffectFactory.Nauseous(0.8, 240)],
                  NewDamage = new DamageInfo(8, DamageType.Internal, "severe food poisoning") }
            ]);

        var herbalTreatment = new EventChoice("Herbal Treatment",
            "Use plant fiber to settle your stomach.",
            [
                new EventResult("Settles your stomach. Mild discomfort only.", weight: 0.70)
                { TimeAddedMinutes = 15, Cost = new ResourceCost(ResourceType.PlantFiber, 1),
                  Effects = [EffectFactory.Nauseous(0.2, 30)] },
                new EventResult("Doesn't help much.", weight: 0.20)
                { TimeAddedMinutes = 15, Cost = new ResourceCost(ResourceType.PlantFiber, 1),
                  Effects = [EffectFactory.Nauseous(0.4, 90)] },
                new EventResult("Makes it worse somehow.", weight: 0.10)
                { TimeAddedMinutes = 15, Cost = new ResourceCost(ResourceType.PlantFiber, 1),
                  Effects = [EffectFactory.Nauseous(0.5, 120)] }
            ],
            [EventCondition.HasPlantFiber]);

        evt.AddChoice(induceVomiting);
        evt.AddChoice(bearIt);
        evt.AddChoice(herbalTreatment);
        return evt;
    }

    private static GameEvent MuscleCramp(GameContext ctx)
    {
        var evt = new GameEvent("Muscle Cramp",
            "Sharp pain shoots through your leg. The muscle seizes, locks up. You can't put weight on it.");
        evt.BaseWeight = 0.8;

        evt.WeightModifiers.Add(EventCondition.LowCalories, 1.5);
        evt.WeightModifiers.Add(EventCondition.LowHydration, 2.0);

        var workItOut = new EventChoice("Work It Out",
            "Massage and stretch. Give it time.",
            [
                new EventResult("Cramp releases. Sore but mobile.", weight: 0.50)
                { TimeAddedMinutes = 8, Effects = [EffectFactory.Sore(0.15, 60)] },
                new EventResult("Takes a while but releases.", weight: 0.25)
                { TimeAddedMinutes = 15, Effects = [EffectFactory.Sore(0.2, 45)] },
                new EventResult("Won't release fully. You're limping.", weight: 0.15)
                { TimeAddedMinutes = 12, Effects = [EffectFactory.SprainedAnkle(0.3)] },
                new EventResult("Made it worse forcing it. Something's wrong.", weight: 0.10)
                { TimeAddedMinutes = 10, NewDamage = new DamageInfo(4, DamageType.Internal, "muscle strain"),
                  Effects = [EffectFactory.SprainedAnkle(0.45)] }
            ]);

        var pushThrough = new EventChoice("Push Through",
            "Keep moving. It'll work itself out.",
            [
                new EventResult("Movement helps. Cramp fades.", weight: 0.30)
                { TimeAddedMinutes = 5, Effects = [EffectFactory.Sore(0.1, 30)] },
                new EventResult("Gets worse before better.", weight: 0.35)
                { TimeAddedMinutes = 12, Effects = [EffectFactory.SprainedAnkle(0.25)] },
                new EventResult("Something tears.", weight: 0.20)
                { TimeAddedMinutes = 8, NewDamage = new DamageInfo(6, DamageType.Internal, "muscle tear"),
                  Effects = [EffectFactory.SprainedAnkle(0.5)] },
                new EventResult("Leg gives out. You fall.", weight: 0.15)
                { TimeAddedMinutes = 15, NewDamage = new DamageInfo(5, DamageType.Blunt, "fall"),
                  Effects = [EffectFactory.SprainedAnkle(0.6)] }
            ]);

        var eatSomething = new EventChoice("Eat Something",
            "Maybe it's low blood sugar.",
            [
                new EventResult("Food helps. Cramp releases quickly.", weight: 0.55)
                { TimeAddedMinutes = 8, Cost = new ResourceCost(ResourceType.Food, 1),
                  Effects = [EffectFactory.Focused(0.1, 60)] },
                new EventResult("Doesn't help the cramp but you feel steadier.", weight: 0.30)
                { TimeAddedMinutes = 10, Cost = new ResourceCost(ResourceType.Food, 1),
                  Effects = [EffectFactory.Sore(0.15, 45)] },
                new EventResult("Hard to eat through the pain. Nauseous.", weight: 0.15)
                { TimeAddedMinutes = 12, Cost = new ResourceCost(ResourceType.Food, 1),
                  Effects = [EffectFactory.Nauseous(0.25, 30)] }
            ],
            [EventCondition.HasFood]);

        var applyHeat = new EventChoice("Apply Heat",
            "Get close to the fire. Heat loosens muscles. (Risk: minor burn if too close)",
            [
                new EventResult("Heat loosens it. Cramp releases smoothly.", weight: 0.80)
                { TimeAddedMinutes = 10 },
                new EventResult("Takes a while but warmth helps.", weight: 0.15)
                { TimeAddedMinutes = 18 },
                new EventResult("Too close. Minor burn, but cramp's gone.", weight: 0.05)
                { TimeAddedMinutes = 12, NewDamage = new DamageInfo(2, DamageType.Burn, "minor burn"),
                  Effects = [EffectFactory.Burn(0.15, 45)] }
            ],
            [EventCondition.NearFire]);

        evt.AddChoice(workItOut);
        evt.AddChoice(pushThrough);
        evt.AddChoice(eatSomething);
        evt.AddChoice(applyHeat);
        return evt;
    }

    private static GameEvent VisionBlur(GameContext ctx)
    {
        var evt = new GameEvent("Vision Blur",
            "Your vision swims. Hard to focus. The world keeps sliding sideways.");
        evt.BaseWeight = 0.7;

        evt.RequiredConditions.Add(EventCondition.LowHydration);
        evt.WeightModifiers.Add(EventCondition.LowCalories, 1.5);

        var rubAndPush = new EventChoice("Rub Eyes and Push On",
            "Shake it off. Keep going.",
            [
                new EventResult("Clears momentarily. Still fuzzy around the edges.", weight: 0.50)
                { Effects = [EffectFactory.Shaken(0.25)] },
                new EventResult("Doesn't help. Getting worse.", weight: 0.30)
                { Effects = [EffectFactory.Shaken(0.4)] },
                new EventResult("Made it worse. Eyes burning now.", weight: 0.20)
                { NewDamage = new DamageInfo(2, DamageType.Internal, "eye strain"),
                  Effects = [EffectFactory.Shaken(0.3)] }
            ]);

        var restEyes = new EventChoice("Rest Eyes",
            "Close your eyes, rest for a bit.",
            [
                new EventResult("Rest helps. Vision clears.", weight: 0.70)
                { TimeAddedMinutes = 15 },
                new EventResult("Takes longer, but eventually clears.", weight: 0.30)
                { TimeAddedMinutes = 25 }
            ]);

        var snowWipeFace = new EventChoice("Snow-Wipe Face",
            "Cold shock to restore alertness.",
            [
                new EventResult("Works. Cold but vision clear.", weight: 0.85)
                { TimeAddedMinutes = 3, Effects = [EffectFactory.Cold(-3, 15)] },
                new EventResult("Too cold. Vision still blurry.", weight: 0.15)
                { TimeAddedMinutes = 5, Effects = [EffectFactory.Cold(-8, 25)] }
            ]);

        var drinkWater = new EventChoice("Drink Water",
            "Maybe it's dehydration.",
            [
                new EventResult("Hydration helps. Vision clears.", weight: 0.60)
                { TimeAddedMinutes = 5, Cost = new ResourceCost(ResourceType.Water, 2) },
                new EventResult("Not just dehydration. Still blurry.", weight: 0.40)
                { TimeAddedMinutes = 5, Cost = new ResourceCost(ResourceType.Water, 2),
                  Effects = [EffectFactory.Shaken(0.2)] }
            ],
            [EventCondition.HasWater]);

        evt.AddChoice(rubAndPush);
        evt.AddChoice(restEyes);
        evt.AddChoice(snowWipeFace);
        evt.AddChoice(drinkWater);
        return evt;
    }

    // === PSYCHOLOGICAL EVENTS ===

    private static GameEvent ParanoiaEvent(GameContext ctx)
    {
        var evt = new GameEvent("Paranoia",
            "You are certain — absolutely certain — you see eyes reflecting at the edge of the firelight.");
        evt.BaseWeight = 0.5;

        evt.RequiredConditions.Add(EventCondition.AtCamp);
        evt.RequiredConditions.Add(EventCondition.Night);
        evt.RequiredConditions.Add(EventCondition.Awake);
        evt.WeightModifiers.Add(EventCondition.Stalked, 2.0);
        evt.WeightModifiers.Add(EventCondition.Disturbed, 2.5);
        evt.WeightModifiers.Add(EventCondition.DisturbedHigh, 3.5);

        var throwFuel = new EventChoice("Throw Fuel on Fire",
            "More light. Drive back the darkness.",
            [
                new EventResult("Fire blazes up. Light reveals: nothing there. Probably.", weight: 0.80)
                { TimeAddedMinutes = 3, Cost = new ResourceCost(ResourceType.Fuel, 2) },
                new EventResult("Something was there — you see it slink away.", weight: 0.20)
                { TimeAddedMinutes = 3, Cost = new ResourceCost(ResourceType.Fuel, 2),
                  CreatesTension = new TensionCreation("Stalked", 0.2) }
            ]);

        var investigate = new EventChoice("Investigate",
            "Step out into the dark and look.",
            [
                new EventResult("Nothing. Your mind playing tricks.", weight: 0.55)
                { TimeAddedMinutes = 8, Effects = [EffectFactory.Shaken(0.2)] },
                new EventResult("Something might have been there. Hard to tell.", weight: 0.20)
                { TimeAddedMinutes = 10, CreatesTension = new TensionCreation("Stalked", 0.2) },
                new EventResult("Something is there.", weight: 0.25)
                { TimeAddedMinutes = 5, SpawnEncounter = new EncounterConfig("Wolf", 20, 0.4) }
            ]);

        var huddleByFire = new EventChoice("Huddle by Fire",
            "Stay close. Wait it out.",
            [
                new EventResult("You stare into the dark for hours. Sleep won't come easy.", weight: 0.70)
                { Effects = [EffectFactory.Paranoid(0.3)] },
                new EventResult("Eventually you relax. Nothing happened.", weight: 0.30)
                { TimeAddedMinutes = 30, Effects = [EffectFactory.Shaken(0.15)] }
            ]);

        evt.AddChoice(throwFuel);
        evt.AddChoice(investigate);
        evt.AddChoice(huddleByFire);
        return evt;
    }

    private static GameEvent MomentOfClarity(GameContext ctx)
    {
        var evt = new GameEvent("Moment of Clarity",
            "Your mind clears. For a brief moment, everything makes sense. You see your situation with perfect clarity.");
        evt.BaseWeight = 0.3;  // Rare positive event

        // More likely when struggling
        evt.WeightModifiers.Add(EventCondition.LowCalories, 1.5);
        evt.WeightModifiers.Add(EventCondition.LowHydration, 1.5);
        evt.WeightModifiers.Add(EventCondition.Injured, 1.5);

        var actOnIt = new EventChoice("Act on It",
            "Use this clarity productively.",
            [
                new EventResult("You notice something you'd been missing — a better approach.", weight: 0.60)
                { TimeAddedMinutes = 5, RewardPool = RewardPool.BasicSupplies },
                new EventResult("You see a solution to something that's been bothering you.", weight: 0.40)
                { TimeAddedMinutes = 5, Effects = [EffectFactory.Focused(0.3, 120)] }
            ]);

        var restInFeeling = new EventChoice("Rest in the Feeling",
            "Don't force it. Let clarity come naturally.",
            [
                new EventResult("You feel centered. Calm.", weight: 1.0)
                { TimeAddedMinutes = 15, Effects = [EffectFactory.Rested(0.2, 90)] }
            ]);

        evt.AddChoice(actOnIt);
        evt.AddChoice(restInFeeling);
        return evt;
    }

    // === WOUND/INFECTION ARC ===

    private static GameEvent WoundFesters(GameContext ctx)
    {
        var woundTension = ctx.Tensions.GetTension("WoundUntreated");
        var bodyPart = woundTension?.Description ?? "wound";

        var evt = new GameEvent("The Wound Festers",
            $"The {bodyPart} is red, swollen. Hot to the touch. This is infection.");
        evt.BaseWeight = 2.0;  // High priority when wound untreated

        evt.RequiredConditions.Add(EventCondition.WoundUntreated);

        var cleanProperly = new EventChoice("Clean It Properly",
            "Use water to thoroughly clean the wound.",
            [
                new EventResult("Thorough cleaning. Infection stopped.", weight: 0.70)
                { TimeAddedMinutes = 15, Cost = new ResourceCost(ResourceType.Water, 2),
                  ResolvesTension = "WoundUntreated", Effects = [EffectFactory.Focused(0.1, 30)] },
                new EventResult("Cleaned but damage done. Mild fever remains.", weight: 0.20)
                { TimeAddedMinutes = 15, Cost = new ResourceCost(ResourceType.Water, 2),
                  ResolvesTension = "WoundUntreated", Effects = [EffectFactory.Exhausted(0.3, 120)] },
                new EventResult("Too late for just cleaning. Need more aggressive treatment.", weight: 0.10)
                { TimeAddedMinutes = 10, Cost = new ResourceCost(ResourceType.Water, 2),
                  EscalateTension = ("WoundUntreated", 0.3) }
            ],
            [EventCondition.HasWater]);

        var cauterize = new EventChoice("Cauterize",
            "Brutal but effective. Use fire to burn out the infection.",
            [
                new EventResult("Brutal but effective. Wound sealed.", weight: 0.60)
                { TimeAddedMinutes = 10, ResolvesTension = "WoundUntreated",
                  NewDamage = new DamageInfo(5, DamageType.Burn, "cauterization"),
                  Effects = [EffectFactory.Burn(0.4, 120)] },
                new EventResult("Effective but traumatic. You won't forget this.", weight: 0.25)
                { TimeAddedMinutes = 10, ResolvesTension = "WoundUntreated",
                  NewDamage = new DamageInfo(5, DamageType.Burn, "cauterization"),
                  Effects = [EffectFactory.Fear(0.3), EffectFactory.Burn(0.4, 120)] },
                new EventResult("Not thorough enough. Still infected.", weight: 0.10)
                { TimeAddedMinutes = 10, NewDamage = new DamageInfo(3, DamageType.Burn, "cauterization"),
                  Effects = [EffectFactory.Burn(0.3, 90)], EscalateTension = ("WoundUntreated", 0.1) },
                new EventResult("You can't do it. The pain stops you.", weight: 0.05)
                { TimeAddedMinutes = 5 }
            ],
            [EventCondition.NearFire]);

        var herbalTreatment = new EventChoice("Herbal Treatment",
            "Use plant fiber as a poultice.",
            [
                new EventResult("Poultice draws out infection.", weight: 0.65)
                { TimeAddedMinutes = 20, Cost = new ResourceCost(ResourceType.PlantFiber, 2),
                  ResolvesTension = "WoundUntreated" },
                new EventResult("Helps but slow. Still needs watching.", weight: 0.25)
                { TimeAddedMinutes = 20, Cost = new ResourceCost(ResourceType.PlantFiber, 2),
                  EscalateTension = ("WoundUntreated", -0.2) },
                new EventResult("Not effective. Infection continues.", weight: 0.10)
                { TimeAddedMinutes = 20, Cost = new ResourceCost(ResourceType.PlantFiber, 2) }
            ],
            [EventCondition.HasPlantFiber]);

        var ignoreIt = new EventChoice("Ignore It",
            "You'll deal with it later.",
            [
                new EventResult("Infection spreads. This is getting serious.", weight: 1.0)
                { EscalateTension = ("WoundUntreated", 0.2),
                  Effects = [EffectFactory.Exhausted(0.2, 60)] }
            ]);

        evt.AddChoice(cleanProperly);
        evt.AddChoice(cauterize);
        evt.AddChoice(herbalTreatment);
        evt.AddChoice(ignoreIt);
        return evt;
    }

    private static GameEvent FeverSetsIn(GameContext ctx)
    {
        var woundTension = ctx.Tensions.GetTension("WoundUntreated");

        var evt = new GameEvent("Fever Sets In",
            "You're burning up. Chills and sweats. The infection has spread.");
        evt.BaseWeight = 2.5;  // High priority - this is serious

        evt.RequiredConditions.Add(EventCondition.WoundUntreatedHigh);

        var aggressiveTreatment = new EventChoice("Aggressive Treatment",
            "All-out effort to fight the infection. Use everything you have.",
            [
                new EventResult("You fight the fever with everything. It breaks.", weight: 0.50)
                { TimeAddedMinutes = 60, ResolvesTension = "WoundUntreated",
                  Cost = new ResourceCost(ResourceType.Water, 3),
                  Effects = [EffectFactory.Exhausted(0.6, 180)] },
                new EventResult("Not enough. The fever holds.", weight: 0.30)
                { TimeAddedMinutes = 45, Cost = new ResourceCost(ResourceType.Water, 2),
                  Effects = [EffectFactory.Fever(0.5), EffectFactory.Exhausted(0.4, 120)] },
                new EventResult("Your body is failing. This is critical.", weight: 0.20)
                { TimeAddedMinutes = 30, Cost = new ResourceCost(ResourceType.Water, 2),
                  Effects = [EffectFactory.Fever(0.7)],
                  EscalateTension = ("WoundUntreated", 0.2) }
            ],
            [EventCondition.HasWater]);

        var restAndFight = new EventChoice("Rest and Fight It",
            "Your body vs. the infection. All you can do is rest.",
            [
                new EventResult("Body wins. Fever breaks after 3 brutal hours.", weight: 0.35)
                { TimeAddedMinutes = 180, ResolvesTension = "WoundUntreated",
                  Effects = [EffectFactory.Exhausted(0.8, 240)] },
                new EventResult("Stalemate. Fever continues. You're weak but alive.", weight: 0.40)
                { TimeAddedMinutes = 120, Effects = [EffectFactory.Fever(0.5), EffectFactory.Exhausted(0.5, 180)] },
                new EventResult("Body loses. Infection spreading. Condition critical.", weight: 0.25)
                { TimeAddedMinutes = 120, Effects = [EffectFactory.Fever(0.8)],
                  EscalateTension = ("WoundUntreated", 0.3),
                  NewDamage = new DamageInfo(10, DamageType.Internal, "systemic infection") }
            ]);

        var keepWorking = new EventChoice("Keep Working",
            "Deny the fever. Push on.",
            [
                new EventResult("You push through. Every minute is agony.", weight: 0.40)
                { Effects = [EffectFactory.Fever(0.5)],
                  EscalateTension = ("WoundUntreated", 0.15) },
                new EventResult("You collapse. Your body has limits.", weight: 0.40)
                { TimeAddedMinutes = 60, Effects = [EffectFactory.Fever(0.6), EffectFactory.Exhausted(0.5, 120)] },
                new EventResult("The fever wins. You go down.", weight: 0.20)
                { TimeAddedMinutes = 90, Effects = [EffectFactory.Fever(0.75)],
                  NewDamage = new DamageInfo(8, DamageType.Internal, "fever complications"),
                  AbortsExpedition = true }
            ]);

        evt.AddChoice(aggressiveTreatment);
        evt.AddChoice(restAndFight);
        evt.AddChoice(keepWorking);
        return evt;
    }

    private static GameEvent FrozenFingers(GameContext ctx)
    {
        var evt = new GameEvent("Frozen Fingers",
            "Your fingers have gone white. You can't feel them properly. This is frostbite territory.");
        evt.BaseWeight = 0.8;

        evt.RequiredConditions.Add(EventCondition.LowTemperature);
        evt.WeightModifiers.Add(EventCondition.Working, 1.5);
        evt.WeightModifiers.Add(EventCondition.ExtremelyCold, 2.0);

        var warmThemNow = new EventChoice("Warm Them Now",
            "Stop everything. Get circulation back before tissue dies.",
            [
                new EventResult("Painful but effective. Feeling returns.", weight: 0.60)
                { TimeAddedMinutes = 10, Effects = [EffectFactory.Frostbite(0.2)] },
                new EventResult("Takes longer. More pain. But they'll heal.", weight: 0.25)
                { TimeAddedMinutes = 20, Effects = [EffectFactory.Frostbite(0.3), EffectFactory.Clumsy(0.3, 60)] },
                new EventResult("Caught it in time. No lasting damage.", weight: 0.10)
                { TimeAddedMinutes = 8 },
                new EventResult("Too late for some tissue. Permanent damage.", weight: 0.05)
                { TimeAddedMinutes = 15, NewDamage = new DamageInfo(8, DamageType.Internal, "severe frostbite"),
                  Effects = [EffectFactory.Frostbite(0.6)] }
            ]);

        var tuckAndContinue = new EventChoice("Tuck and Continue",
            "Hands under arms. Keep working as best you can.",
            [
                new EventResult("Circulation returns slowly. Clumsy but functional.", weight: 0.50)
                { Effects = [EffectFactory.Clumsy(0.3, 45)] },
                new EventResult("Still losing feeling. You need to stop soon.", weight: 0.30)
                { Effects = [EffectFactory.Frostbite(0.3), EffectFactory.Clumsy(0.4, 60)] },
                new EventResult("Body heat isn't enough. Frostbite setting in.", weight: 0.20)
                { NewDamage = new DamageInfo(5, DamageType.Internal, "frostbite"),
                  Effects = [EffectFactory.Frostbite(0.5)] }
            ]);

        var useFire = new EventChoice("Use Fire",
            "Direct heat restores circulation fastest.",
            [
                new EventResult("Direct heat restores circulation. Painful but effective.", weight: 0.80)
                { TimeAddedMinutes = 8 },
                new EventResult("Too close. Minor burn but fingers saved.", weight: 0.15)
                { TimeAddedMinutes = 8, NewDamage = new DamageInfo(2, DamageType.Burn, "minor burn"),
                  Effects = [EffectFactory.Burn(0.15, 30)] },
                new EventResult("Numb fingers don't feel the heat. Burn damage before you notice.", weight: 0.05)
                { TimeAddedMinutes = 5, NewDamage = new DamageInfo(5, DamageType.Burn, "burn"),
                  Effects = [EffectFactory.Burn(0.3, 60)] }
            ],
            [EventCondition.NearFire]);

        evt.AddChoice(warmThemNow);
        evt.AddChoice(tuckAndContinue);
        evt.AddChoice(useFire);
        return evt;
    }

    private static GameEvent OldAche(GameContext ctx)
    {
        var evt = new GameEvent("Old Ache",
            "The damp cold settles into your joints. An old injury flares up, or your body simply protests the abuse.");
        evt.BaseWeight = 0.7;

        evt.WeightModifiers.Add(EventCondition.LowTemperature, 1.5);
        evt.WeightModifiers.Add(EventCondition.Injured, 2.0);
        evt.WeightModifiers.Add(EventCondition.Working, 1.3);

        var stretchAndRest = new EventChoice("Stretch and Rest",
            "Rest for an hour. Let your body recover.",
            [
                new EventResult("The rest helps. Pain subsides.", weight: 0.70)
                { TimeAddedMinutes = 60, Effects = [EffectFactory.Rested(0.2, 120)] },
                new EventResult("Takes longer than expected, but eventually loosens up.", weight: 0.30)
                { TimeAddedMinutes = 90, Effects = [EffectFactory.Rested(0.1, 60)] }
            ]);

        var workThroughIt = new EventChoice("Work Through It",
            "Ignore the pain. Keep going.",
            [
                new EventResult("Discomfort but manageable. You push on.", weight: 0.60)
                { Effects = [EffectFactory.Stiff(0.25, 360)] },
                new EventResult("Worse than expected. Every step hurts.", weight: 0.30)
                { Effects = [EffectFactory.Stiff(0.4, 240)] },
                new EventResult("Your body knows better. Forced rest anyway.", weight: 0.10)
                { TimeAddedMinutes = 30, Effects = [EffectFactory.Stiff(0.3, 180)] }
            ]);

        var adjustLoad = new EventChoice("Adjust Load",
            "Drop weight, change how you carry things.",
            [
                new EventResult("Lighter load helps. Pain eases.", weight: 0.60)
                { TimeAddedMinutes = 10, Effects = [EffectFactory.Sore(0.15, 60)] },
                new EventResult("Adjustment helps but you're still stiff.", weight: 0.40)
                { TimeAddedMinutes = 10, Effects = [EffectFactory.Stiff(0.2, 180)] }
            ]);

        evt.AddChoice(stretchAndRest);
        evt.AddChoice(workThroughIt);
        evt.AddChoice(adjustLoad);
        return evt;
    }

    private static GameEvent Toothbreaker(GameContext ctx)
    {
        var evt = new GameEvent("Toothbreaker",
            "You bite down on something hard. A crack echoes in your skull. That was either the food or your tooth.")
            .Requires(EventCondition.Eating);
        evt.BaseWeight = 0.4;  // Relatively rare

        evt.WeightModifiers.Add(EventCondition.LowTemperature, 1.5);  // Frozen food
        evt.WeightModifiers.Add(EventCondition.LowOnFood, 1.3);  // Eating tough/old food

        var spitItOut = new EventChoice("Spit It Out",
            "Lose the rest of the food but protect your teeth.",
            [
                new EventResult("You spit it out. Mouth checked — teeth intact.", weight: 0.80)
                { TimeAddedMinutes = 2 },
                new EventResult("Lost some food but your teeth are fine.", weight: 0.20)
                { TimeAddedMinutes = 2, Cost = new ResourceCost(ResourceType.Food, 1) }
            ]);

        var swallowThroughBlood = new EventChoice("Swallow Through Blood",
            "Get the calories. Deal with the pain.",
            [
                new EventResult("Tooth cracked but holding. Pain lingers.", weight: 0.60)
                { TimeAddedMinutes = 3, Effects = [EffectFactory.Pain(0.3)],
                  NewDamage = new DamageInfo(2, DamageType.Internal, "cracked tooth") },
                new EventResult("Tooth fine, just cut your gum. Minor.", weight: 0.30)
                { TimeAddedMinutes = 2, NewDamage = new DamageInfo(1, DamageType.Sharp, "cut gum") },
                new EventResult("Tooth broken. This will be a problem.", weight: 0.10)
                { TimeAddedMinutes = 5, Effects = [EffectFactory.Pain(0.5)],
                  NewDamage = new DamageInfo(5, DamageType.Internal, "broken tooth") }
            ]);

        var checkCarefully = new EventChoice("Check Carefully",
            "Take time to examine the damage.",
            [
                new EventResult("Just the food. Your teeth are fine.", weight: 0.50)
                { TimeAddedMinutes = 5 },
                new EventResult("Small chip. Painful but not serious.", weight: 0.35)
                { TimeAddedMinutes = 5, NewDamage = new DamageInfo(1, DamageType.Internal, "chipped tooth") },
                new EventResult("Cracked tooth. Needs attention.", weight: 0.15)
                { TimeAddedMinutes = 5, Effects = [EffectFactory.Pain(0.25)],
                  NewDamage = new DamageInfo(3, DamageType.Internal, "cracked tooth") }
            ]);

        evt.AddChoice(spitItOut);
        evt.AddChoice(swallowThroughBlood);
        evt.AddChoice(checkCarefully);
        return evt;
    }

    private static GameEvent FugueState(GameContext ctx)
    {
        // This event has no real choice — it happens TO the player
        var evt = new GameEvent("Fugue State",
            "You blink, and the sun has moved. You don't remember the last hour. You kept working, but you were somewhere else.");
        evt.BaseWeight = 0.3;  // Rare

        evt.RequiredConditions.Add(EventCondition.Working);
        evt.WeightModifiers.Add(EventCondition.LowCalories, 1.5);
        evt.WeightModifiers.Add(EventCondition.LowHydration, 1.5);

        var comeBackToReality = new EventChoice("Come Back to Reality",
            "Assess the damage. What did you miss?",
            [
                new EventResult("Time lost. Work done but you're drained.", weight: 0.50)
                { TimeAddedMinutes = 90, Effects = [EffectFactory.Exhausted(0.4, 120)],
                  RewardPool = RewardPool.BasicSupplies },
                new EventResult("You worked efficiently while dissociated. But at what cost?", weight: 0.30)
                { TimeAddedMinutes = 120, Effects = [EffectFactory.Exhausted(0.3, 90)],
                  RewardPool = RewardPool.BasicSupplies },
                new EventResult("You feel... hollow. What happened while you were gone?", weight: 0.15)
                { TimeAddedMinutes = 100, Effects = [EffectFactory.Shaken(0.3), EffectFactory.Exhausted(0.5, 150)],
                  RewardPool = RewardPool.BasicSupplies },
                new EventResult("Something happened while you were away. You don't remember what.", weight: 0.05)
                { TimeAddedMinutes = 80, Effects = [EffectFactory.Fear(0.25)],
                  CreatesTension = new TensionCreation("Stalked", 0.2) }
            ]);

        evt.AddChoice(comeBackToReality);
        return evt;
    }

    private static GameEvent DistantCarcassStench(GameContext ctx)
    {
        var territory = ctx.CurrentLocation.GetFeature<AnimalTerritoryFeature>();
        var animal = territory?.GetRandomAnimalName() ?? "animal";

        var evt = new GameEvent("Distant Carcass Stench",
            $"The wind brings a smell — death, recent. Something died nearby, or something killed nearby.");
        evt.BaseWeight = 0.6;

        evt.RequiredConditions.Add(EventCondition.IsExpedition);
        evt.RequiredConditions.Add(EventCondition.InAnimalTerritory);

        var scoutToward = new EventChoice("Scout Toward It",
            "Follow the smell. Could be free meat.",
            [
                new EventResult($"Find a {animal.ToLower()} carcass. Some meat left.", weight: 0.35)
                { TimeAddedMinutes = 30, RewardPool = RewardPool.BasicMeat },
                new EventResult("Find the carcass. Something's already there.", weight: 0.25)
                { TimeAddedMinutes = 25, CreatesTension = new TensionCreation("Stalked", 0.3) },
                new EventResult("Tracks lead to a hunting ground. Good location.", weight: 0.20)
                { TimeAddedMinutes = 35, RewardPool = RewardPool.GameTrailDiscovery },
                new EventResult("Can't find it. Wind shifted.", weight: 0.15)
                { TimeAddedMinutes = 30 },
                new EventResult("Find it. And what killed it.", weight: 0.05)
                { TimeAddedMinutes = 20, SpawnEncounter = new EncounterConfig(
                    territory?.GetRandomPredatorName() ?? "Wolf", 25, 0.6) }
            ]);

        var markDirection = new EventChoice("Mark the Direction",
            "Note for later investigation.",
            [
                new EventResult("You mark the direction mentally.", weight: 1.0)
                { TimeAddedMinutes = 3 }
            ]);

        var avoidArea = new EventChoice("Avoid the Area",
            "Something that kills is probably around. Give it space.",
            [
                new EventResult("You adjust your route. Slower but safer.", weight: 0.80)
                { TimeAddedMinutes = 15 },
                new EventResult("Detour takes you through difficult terrain.", weight: 0.20)
                { TimeAddedMinutes = 25, Effects = [EffectFactory.Sore(0.15, 45)] }
            ]);

        evt.AddChoice(scoutToward);
        evt.AddChoice(markDirection);
        evt.AddChoice(avoidArea);
        return evt;
    }

    // === DISTURBED ARC INTERSECTION EVENTS ===

    private static GameEvent ShadowMovement(GameContext ctx)
    {
        var stalkedTension = ctx.Tensions.GetTension("Stalked");
        var predator = stalkedTension?.AnimalType ?? "something";

        return new GameEvent("Shadow Movement",
            $"Movement in your peripheral vision. Your heart hammers. Is it the {predator.ToLower()}? Or your mind again?")
            .Weight(2.0)
            .Requires(EventCondition.Disturbed, EventCondition.Stalked)
            .MoreLikelyIf(EventCondition.DisturbedHigh, 1.5)
            .MoreLikelyIf(EventCondition.StalkedHigh, 1.5)
            .Choice("Assume It's Real",
                "Act as if the threat is real. Better safe than dead.",
                [
                    new EventResult("You react defensively. Nothing attacks. Was it real?", 0.40, 10)
                        .WithEffects(EffectFactory.Paranoid(0.2)),
                    new EventResult("It WAS real. Your vigilance saved you.", 0.25, 5)
                        .Escalate("Stalked", 0.15)
                        .WithEffects(EffectFactory.Fear(0.2)),
                    new EventResult("False alarm. Your nerves are fraying.", 0.25, 8)
                        .Escalate("Disturbed", 0.1),
                    new EventResult("You spin to face it. The predator is there.", 0.10, 0)
                        .Encounter(stalkedTension?.AnimalType ?? "Wolf", 25, 0.5)
                ])
            .Choice("Assume It's Nothing",
                "You're jumping at shadows. Stay calm.",
                [
                    new EventResult("Nothing happens. You were right. Probably.", 0.45, 5),
                    new EventResult("Your calm is justified. The mind plays tricks.", 0.25, 5)
                        .Escalate("Disturbed", -0.05),
                    new EventResult("It wasn't nothing. It was watching. Now it knows you're not alert.", 0.20, 0)
                        .Escalate("Stalked", 0.25)
                        .WithEffects(EffectFactory.Fear(0.25)),
                    new EventResult("Fatal mistake. It strikes.", 0.10, 0)
                        .Encounter(stalkedTension?.AnimalType ?? "Wolf", 10, 0.75)
                ])
            .Choice("Stop and Observe",
                "Freeze. Watch. Listen. Know the difference.",
                [
                    new EventResult("Patient observation. Nothing there but your fears.", 0.35, 15)
                        .Escalate("Disturbed", -0.05),
                    new EventResult("You wait. And wait. The tension is unbearable.", 0.30, 20)
                        .WithEffects(EffectFactory.Exhausted(0.2, 60)),
                    new EventResult("You see it clearly now. It's real. And it sees you.", 0.25, 10)
                        .ResolveTension("Stalked")
                        .CreateTension("Hunted", 0.5, animalType: stalkedTension?.AnimalType),
                    new EventResult("Nothing. Just paranoia. Or maybe it left.", 0.10, 15)
                ]);
    }
}
