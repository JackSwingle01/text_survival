using text_survival.Actions.Variants;
using text_survival.Actors.Animals;
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

        return new GameEvent(
            "Fresh Carcass",
            $"Something killed a {animal.ToLower()} recently. The meat's still good, but you didn't make this kill.", 0.5)
            .Requires(EventCondition.Working, EventCondition.InAnimalTerritory)
            .WithSituationFactor(Situations.PredatorInTerritory, 2.0)  // More likely in predator territory
            .Choice("Scavenge Quickly", "Grab what you can and get out before whatever killed this returns.",
                [
                    new EventResult("You cut away some meat and leave.", weight: 0.7f, minutes:8)
                        .FindsMeat(),
                    new EventResult("A low growl. You grab what you can and run.", weight: 0.3, minutes:5)
                        .FindsMeat()
                        .Frightening()
                ])
            .Choice("Butcher Thoroughly",
                "Take your time. Get everything you can from this.",
                [
                    new EventResult("You work quickly but thoroughly. A good haul.", weight: 0.5f, minutes:25)
                        .FindsLargeMeat(),
                    new EventResult("You're nearly done when something crashes through the brush. You flee.", weight: 0.35f, minutes:20)
                        .FindsLargeMeat()
                        .Frightening()
                        .Aborts(),
                    new EventResult("It comes back. You barely escape with your life, taking some meat with you.", weight: 0.15f, minutes:15)
                        .Mauled()
                        .FindsMeat()
                        .Aborts()
                ])
            .Choice("Leave It",
                "Not worth the risk. You move on.",
                [
                    new EventResult("You leave the carcass behind.")
                    { TimeAddedMinutes = 0 }
                ]);
    }

    private static GameEvent Tracks(GameContext ctx)
    {
        // Determine animal type - prefer herd data over territory feature
        var pos = ctx.Map?.CurrentPosition ?? default;
        var herdInTerritory = ctx.Herds._herds
            .FirstOrDefault(h => h.HomeTerritory.Contains(pos) && h.Count > 0);
        var territory = ctx.CurrentLocation.GetFeature<AnimalTerritoryFeature>();
        var animal = herdInTerritory?.AnimalType.DisplayName() ?? territory?.GetRandomAnimalName() ?? "animal";
        bool isPredator = herdInTerritory != null
            ? herdInTerritory.IsPredator
            : (territory?.HasPredators() ?? false);

        return new GameEvent(
            "Tracks",
            $"Fresh {animal.ToLower()} tracks cross your path. They're recent.", 0.3)
            .Requires(EventCondition.IsExpedition, EventCondition.InAnimalTerritory)
            .WithConditionFactor(EventCondition.HighVisibility, 1.5) // Easier to see tracks in open terrain
            .WithSituationFactor(Situations.PredatorInTerritory, 5.0) // Very common when predator herd present
            .WithSituationFactor(Situations.PreyInTerritory, 5.0) // Very common when prey herd present
            .Choice("Follow Them",
                "The trail is clear. You could track this animal.",
                [
                    new EventResult("The tracks lead nowhere. You lose the trail.", weight: 0.35f, minutes: 20)
                        .CreateTension("FreshTrail", 0.1, description: "faint sign of game"),
                    new EventResult($"You follow the tracks and find signs of {animal.ToLower()} activity.", weight: 0.35f, minutes: 25)
                        .FollowsTracks(animal, isPredator, isPredator ? 1 : 3),
                    new EventResult("You find a game trail — good hunting ground.", weight: 0.15f, minutes: 30)
                        .FindsGameTrail(),
                    new EventResult("You were so focused on the tracks, you didn't notice what was tracking YOU. It lunges.", weight: 0.15f, minutes: 15)
                        .AnimalAttack()
                        .DiscoversPredator(isPredator ? animal : "Wolf", 0.4)
                        .Aborts()
                ])
            .Choice("Note Direction",
                "You mark the direction mentally. Could be useful later.",
                [
                    new EventResult("You file the information away and continue.", minutes: 2)
                        .CreateTension("FreshTrail", 0.15, description: "noted animal direction")
                        .MarksAnimalSign(animal, 0.3)
                ])
            .Choice("Avoid the Area",
                "Best not to cross paths with whatever made these.",
                [
                    new EventResult("You detour around. Slower but safer.", minutes: 10)
                ]);
    }

    private static GameEvent SomethingWatching(GameContext ctx)
    {
        // Prefer herd data for predator type
        var predatorHerd = ctx.Herds.GetPredatorHerds()
            .FirstOrDefault(h => h.HomeTerritory.Contains(ctx.Map?.CurrentPosition ?? default) && h.Count > 0);
        var territory = ctx.CurrentLocation.GetFeature<AnimalTerritoryFeature>();
        var predator = predatorHerd?.AnimalType.DisplayName() ?? territory?.GetRandomPredatorName() ?? "Wolf";
        var variant = AnimalSelector.GetVariant(predator);

        // Noise effectiveness varies by animal - skittish animals flee, others may be provoked
        double noiseFleeWeight = 0.40 + variant.NoiseEffectiveness * 0.3;  // 0.40-0.70
        double noiseProvokeWeight = 0.05 + (1 - variant.NoiseEffectiveness) * 0.10;  // 0.05-0.15

        return new GameEvent("Something Watching",
            $"The hair on your neck stands up. Something is watching. You catch a glimpse of movement — {predator.ToLower()}?", 0.2)
            .Requires(EventCondition.Working, EventCondition.HasPredators)
            .WithSituationFactor(Situations.PredatorInTerritory, 5.0)    // Very common when predator herd present
            .WithSituationFactor(Situations.AttractiveToPredators, 3.0)  // Meat, bleeding, food scent
            .WithSituationFactor(Situations.Vulnerable, 2.0)             // Injured, slow, no weapon
            .WithSituationFactor(Situations.IsFollowingAnimalSigns, 2.5) // Following tracks/scat clues
            .WithConditionFactor(EventCondition.LowVisibility, 1.5)      // Harder to spot stalker
            .WithConditionFactor(EventCondition.FarFromCamp, 1.5)        // More dangerous far from safety
            .WithSituationFactor(Situations.TrappedByTerrain, 2.0)       // Cornered or bottleneck
            .Choice("Make Noise",
                $"Stand tall, make yourself big, shout. {(variant.NoiseEffectiveness > 0.6 ? "Should work." : "Risky.")}",
                [
                    new EventResult("Whatever it was slinks away. You're not worth the trouble.", weight: noiseFleeWeight, minutes: 5),
                    new EventResult("It doesn't retreat. It's testing you. You back away slowly.", weight: 0.30 - noiseFleeWeight * 0.2, minutes: 10)
                        .Unsettling()
                        .DiscoversPredator(predator, 0.3),
                    new EventResult("Your noise provokes it. It attacks.", weight: noiseProvokeWeight, minutes: 5)
                        .Damage(12, DamageType.Sharp)
                        .DiscoversPredator(predator, 0.5)
                        .Aborts(),
                    new EventResult("Nothing there. Just paranoia.", weight: 0.05, minutes: 3)
                        .Shaken()
                ])
            .Choice("Finish and Leave",
                "Cut your work short. Get out before it decides you're prey.",
                [
                    new EventResult("You gather what you have and leave quickly.", minutes: 3)
                        .Aborts()
                ])
            .Choice("Try to Spot It",
                "Knowledge is survival. You need to know what you're dealing with.",
                [
                    new EventResult("Just a fox. It watches you work but keeps its distance.", weight: 0.40, minutes: 8),
                    new EventResult("You see it now — keeping its distance. It's not attacking yet.", weight: 0.35, minutes: 10)
                        .WithEffects(EffectFactory.Fear(0.15))
                        .DiscoversPredator(predator, 0.25),
                    new EventResult($"You make eye contact. {(variant.AmbushChance > 0.3 ? "It lunges." : "It retreats, but remembers you.")}", weight: 0.15, minutes: 5)
                        .AnimalAttack()
                        .DiscoversPredator(predator, 0.4)
                        .Aborts(),
                    new EventResult("Can't see it but you KNOW it's there.", weight: 0.10, minutes: 10)
                        .Frightening()
                        .DiscoversPredator(predator, 0.4)
                ]);
    }

    private static GameEvent RavenCall(GameContext ctx)
    {
        return new GameEvent("Raven Call",
            "Ravens circling overhead. They've spotted something — or someone. They're watching you.", 0.6)
            .Requires(EventCondition.Outside)
            .WithConditionFactor(EventCondition.LowOnFood, 1.5)
            .Choice("Follow Them",
                "Ravens often lead to carcasses or resources.",
                [
                    new EventResult("They lead you to a fresh carcass.", weight: 0.35, minutes: 25)
                        .CreatesCarcass()
                        .BecomeStalked(0.2),
                    new EventResult("They lead nowhere. Wasting your time.", weight: 0.25, minutes: 30),
                    new EventResult("They lead you to another predator's kill.", weight: 0.20, minutes: 25)
                        .CreatesCarcass()
                        .BecomeStalked(0.4),
                    new EventResult("They lead you somewhere dangerous.", weight: 0.10, minutes: 20)
                        .Encounter("Wolf", 25, 0.5),
                    new EventResult("They lead you to something unexpected.", weight: 0.10, minutes: 30)
                        .FindsCache()
                ])
            .Choice("Ignore Them",
                "They're just birds.",
                [
                    new EventResult("You continue working. They circle away eventually.", weight: 1.0, minutes: 2)
                ]);
    }

    // === STALKER ARC EVENTS ===

    private static GameEvent StalkerCircling(GameContext ctx)
    {
        var stalkedTension = ctx.Tensions.GetTension("Stalked");
        var predator = stalkedTension?.AnimalType ?? "Wolf";
        var variant = AnimalSelector.GetVariant(predator);

        // Calculate weights based on animal behavior
        double loseTrailWeight = AnimalSelector.LoseTrailSuccessWeight(variant) * 0.5 + 0.15;  // 0.15-0.65
        double staysWithYouWeight = variant.StalkingPersistence * 0.5;  // 0.0-0.50
        double chaseOnRetreatWeight = variant.ChaseThreshold * 0.25;  // 0.0-0.25

        // Darkness affects differently based on whether animal is nocturnal
        double darknessFactor = variant.IsDiurnal ? 0.8 : 1.5;

        return new GameEvent("Stalker Circling",
            $"You catch movement in your peripheral vision. Again. {variant.CirclingDescription}", 1.5)
            .Requires(EventCondition.Stalked, EventCondition.IsExpedition)
            .WithSituationFactor(Situations.InDarkness, darknessFactor)
            .Choice("Confront It Now",
                "Turn and face it. Better to fight on your terms.",
                [
                    new EventResult("You spin to face it. The confrontation is now.", weight: 1.0, minutes: 5)
                        .ConfrontStalker(predator, 20, stalkedTension?.Severity ?? 0.5)
                ])
            .Choice("Try to Lose It",
                $"Double back, cross water, break your trail. {(variant.StalkingPersistence > 0.5 ? "Difficult with this one." : "Worth a shot.")}",
                [
                    new EventResult("You double back, cross water, break your trail. It works.", weight: loseTrailWeight, minutes: 25)
                        .ResolvesStalking(),
                    new EventResult("It stays with you. You've wasted time and energy.", weight: staysWithYouWeight, minutes: 20)
                        .EscalatesStalking(0.2),
                    new EventResult("You get turned around trying to lose it.", weight: 0.15, minutes: 35)
                        .WithEffects(EffectFactory.Cold(-8, 30), EffectFactory.Shaken(0.2)),
                    new EventResult("Your evasion leads you somewhere unexpected.", weight: 0.10, minutes: 30)
                ])
            .Choice("Keep Moving, Stay Alert",
                "Maintain distance. Don't show weakness.",
                [
                    new EventResult("You maintain distance. Exhausting but stable.", weight: 0.40, minutes: 10),
                    new EventResult("It's getting bolder.", weight: 0.25 + variant.StalkingPersistence * 0.1, minutes: 8)
                        .EscalatesStalking(),
                    new EventResult("It backs off. Maybe lost interest.", weight: 0.25 - variant.StalkingPersistence * 0.1, minutes: 5)
                        .EscalatesStalking(-0.1),
                    new EventResult("It commits.", weight: 0.10, minutes: 5)
                        .ConfrontStalker(predator, 15, 0.6)
                ])
            .Choice("Return to Camp",
                $"Head back now. {(variant.FireEffectiveness > 0.6 ? "Fire should deter it." : "Risky — fire may not work.")}",
                [
                    new EventResult("You make it back. Fire deters it.", weight: 0.40 + variant.FireEffectiveness * 0.3)
                        .ResolvesStalking()
                        .Aborts(),
                    new EventResult("It follows to camp perimeter but won't approach fire.", weight: 0.20 + variant.FireEffectiveness * 0.1)
                        .ResolvesStalking()
                        .Unsettling()
                        .Aborts(),
                    new EventResult($"It's bolder than you thought. {(variant.ChaseThreshold > 0.5 ? "Attacks as you flee." : "Cuts you off.")}", weight: chaseOnRetreatWeight + 0.10, minutes: 5)
                        .ConfrontStalker(predator, 10, 0.8)
                        .Aborts()
                ]);
    }

    private static GameEvent PredatorRevealed(GameContext ctx)
    {
        var stalkedTension = ctx.Tensions.GetTension("Stalked");
        var predator = stalkedTension?.AnimalType ?? "Wolf";
        var variant = AnimalSelector.GetVariant(predator);

        // Slow retreat success depends on chase threshold - low chase = easier escape
        double slowRetreatWeight = AnimalSelector.SlowRetreatSuccessWeight(variant) * 0.5 + 0.25;  // 0.25-0.75
        double chargeWeight = variant.ChaseThreshold * 0.35;  // 0.0-0.35

        return new GameEvent("The Predator Revealed",
            $"You finally see it clearly. A {predator.ToLower()}. It's watching you from maybe thirty feet away. Not hiding anymore. {variant.TacticsDescription}", 2.0)
            .Requires(EventCondition.StalkedHigh, EventCondition.IsExpedition)
            .WithSituationFactor(Situations.TrappedByTerrain, 2.0)  // Cornered = revealed faster
            .WithSituationFactor(Situations.RemoteAndVulnerable, 1.5)  // Far + weak = dangerous
            .Choice("Stand Your Ground",
                "Face it. This ends now.",
                [
                    new EventResult("You turn to face it. The confrontation is inevitable.", weight: 1.0, minutes: 5)
                        .ConfrontStalker(predator, 30, stalkedTension?.Severity ?? 0.6)
                ])
            .Choice("Calculated Retreat",
                $"Slow, deliberate backward movement. Don't run. {(variant.ChaseThreshold > 0.5 ? "Don't trigger the chase." : "Keep steady.")}",
                [
                    new EventResult("You back away slowly. It watches but doesn't follow.", weight: slowRetreatWeight, minutes: 15)
                        .ResolvesStalking(),
                    new EventResult("It follows at a distance. You're not out of this yet.", weight: 0.40 - slowRetreatWeight * 0.3, minutes: 10)
                        .EscalatesStalking(0.2),
                    new EventResult($"Your retreat emboldens it. {(variant.AmbushChance > 0.3 ? "It was waiting for this." : "It charges.")}", weight: chargeWeight + 0.10, minutes: 5)
                        .ConfrontStalker(predator, 15, 0.75)
                ]);
    }

    private static GameEvent Ambush(GameContext ctx)
    {
        var stalkedTension = ctx.Tensions.GetTension("Stalked");
        var predator = stalkedTension?.AnimalType ?? "Wolf";
        var variant = AnimalSelector.GetVariant(predator);

        // Ambush chance affects how the attack unfolds
        string ambushDesc = variant.AmbushChance > 0.3
            ? $"It's been waiting for this moment. The {predator.ToLower()} strikes from cover."
            : $"It's done testing. The {predator.ToLower()} charges.";

        return new GameEvent("Ambush", ambushDesc, 3.0)
            .Requires(EventCondition.StalkedCritical, EventCondition.IsExpedition)
            .WithSituationFactor(Situations.RemoteAndVulnerable, 2.0)  // Isolation invites attack
            .WithSituationFactor(Situations.TrappedByTerrain, 2.5)  // No escape = perfect ambush
            .Choice("Brace Yourself",
                "No time to run. It's on you.",
                [
                    new EventResult($"The {predator.ToLower()} attacks!", weight: 1.0, minutes: 3)
                        .ConfrontStalker(predator, 5, 0.9)
                ]);
    }

    // === BODY EVENTS ===

    private static GameEvent TheShakes(GameContext ctx)
    {
        return new GameEvent("The Shakes",
            "It's not just the cold. Your blood sugar has crashed. Your hands are trembling so violently you can barely hold anything.", 1.0)
            .Requires(EventCondition.LowCalories, EventCondition.Awake)
            .WithConditionFactor(EventCondition.LowTemperature, 2.0)
            .Choice("Eat Immediately",
                "You need food now.",
                [
                    new EventResult("Warmth spreads through you. The shaking stops.", weight: 0.70, minutes: 5)
                        .Costs(ResourceType.Food, 1)
                        .WithEffects(EffectFactory.Focused(0.2, 90)),
                    new EventResult("Takes the edge off. Still shaky.", weight: 0.20, minutes: 5)
                        .Costs(ResourceType.Food, 1),
                    new EventResult("You eat too fast. Nauseous.", weight: 0.10, minutes: 8)
                        .Costs(ResourceType.Food, 1)
                        .WithEffects(EffectFactory.Nauseous(0.2, 30))
                ],
                [EventCondition.HasFood])
            .Choice("Warm Up by Fire",
                "Heat helps. Get close to the flames. (Risk: exhaustion may cause sleep)",
                [
                    new EventResult("Heat helps. Shaking subsides.", weight: 0.70, minutes: 20)
                        .WithEffects(EffectFactory.Warmed(0.3, 30)),
                    new EventResult("Takes longer but works.", weight: 0.25, minutes: 35),
                    new EventResult("You doze off by the fire. Time lost, but you feel better.", weight: 0.05, minutes: 60)
                        .WithEffects(EffectFactory.Rested(0.5, 60))
                ],
                [EventCondition.NearFire])
            .Choice("Push Through",
                "Mind over matter. Keep working.",
                [
                    new EventResult("Mind over matter. Shaking fades to background.", weight: 0.40)
                        .WithEffects(EffectFactory.Shaken(0.3)),
                    new EventResult("You drop something. Minor setback.", weight: 0.35, minutes: 5)
                        .WithEffects(EffectFactory.Shaken(0.3)),
                    new EventResult("Can't function. Forced rest.", weight: 0.15, minutes: 30),
                    new EventResult("You push through and acclimate.", weight: 0.25)
                        .WithEffects(EffectFactory.Hardened(0.4, 120))
                ]);
    }

    private static GameEvent GutWrench(GameContext ctx)
    {
        return new GameEvent("Gut Wrench",
            "Your stomach twists. Something you ate isn't sitting right. At all.", 0.6)
            .Requires(EventCondition.Eating)
            .WithConditionFactor(EventCondition.LowOnFood, .5)
            .Choice("Induce Vomiting",
                "Get it out before it gets worse.",
                [
                    new EventResult("Painful but effective. You feel emptied out but better.", weight: 1.0, minutes: 10)
                        .DrainsStats(calories: 300, hydration: 200)
                ])
            .Choice("Bear It",
                "Your body will handle it. Probably.",
                [
                    new EventResult("It passes eventually. Uncomfortable but manageable.", weight: 0.40, minutes: 15)
                        .WithEffects(EffectFactory.Nauseous(0.4, 120)),
                    new EventResult("Worse than expected. You're really sick.", weight: 0.30, minutes: 20)
                        .WithEffects(EffectFactory.Nauseous(0.6, 180))
                        .Damage(3, DamageType.Internal),
                    new EventResult("Your body handles it. You feel tougher for it.", weight: 0.30, minutes: 10)
                        .WithEffects(EffectFactory.Hardened(0.35, 180)),
                    new EventResult("Serious food poisoning. This is bad.", weight: 0.10, minutes: 30)
                        .WithEffects(EffectFactory.Nauseous(0.8, 240))
                        .Damage(8, DamageType.Internal)
                ])
            .Choice("Herbal Treatment",
                "Use plant fiber to settle your stomach.",
                [
                    new EventResult("Settles your stomach. Mild discomfort only.", weight: 0.70, minutes: 15)
                        .Costs(ResourceType.PlantFiber, 1)
                        .WithEffects(EffectFactory.Nauseous(0.2, 30)),
                    new EventResult("Doesn't help much.", weight: 0.20, minutes: 15)
                        .Costs(ResourceType.PlantFiber, 1)
                        .WithEffects(EffectFactory.Nauseous(0.4, 90)),
                    new EventResult("Makes it worse somehow.", weight: 0.10, minutes: 15)
                        .Costs(ResourceType.PlantFiber, 1)
                        .WithEffects(EffectFactory.Nauseous(0.5, 120))
                ],
                [EventCondition.HasPlantFiber]);
    }

    private static GameEvent MuscleCramp(GameContext ctx)
    {
        var strainVariant = VariantSelector.SelectMuscleStrain(ctx);
        var tearVariant = VariantSelector.SelectMuscleTear(ctx);
        var fallVariant = VariantSelector.SelectAccidentVariant(ctx);

        return new GameEvent("Muscle Cramp",
            "Sharp pain shoots through your leg. The muscle seizes, locks up. You can't put weight on it.", 0.5)
            .Requires(EventCondition.Awake, EventCondition.Traveling)
            .WithSituationFactor(Situations.CriticallyDepleted, 2.5)  // LowCalories + LowHydration
            .Choice("Work It Out",
                "Massage and stretch. Give it time.",
                [
                    new EventResult("Cramp releases. Sore but mobile.", weight: 0.50, minutes: 8)
                        .WithEffects(EffectFactory.Sore(0.15, 60)),
                    new EventResult("Takes a while but releases.", weight: 0.25, minutes: 15)
                        .WithEffects(EffectFactory.Sore(0.2, 45)),
                    new EventResult("Won't release fully. You're limping.", weight: 0.15, minutes: 12)
                        .WithEffects(EffectFactory.SprainedAnkle(0.3)),
                    new EventResult($"{strainVariant.Description} You made it worse.", weight: 0.10, minutes: 10)
                        .DamageWithVariant(strainVariant)
                        .WithEffects(EffectFactory.SprainedAnkle(0.45))
                ])
            .Choice("Push Through",
                "Keep moving. It'll work itself out.",
                [
                    new EventResult("Movement helps. Cramp fades.", weight: 0.30, minutes: 5)
                        .WithEffects(EffectFactory.Sore(0.1, 30)),
                    new EventResult("Gets worse before better.", weight: 0.35, minutes: 12)
                        .WithEffects(EffectFactory.SprainedAnkle(0.25)),
                    new EventResult(tearVariant.Description, weight: 0.20, minutes: 8)
                        .DamageWithVariant(tearVariant)
                        .WithEffects(EffectFactory.SprainedAnkle(0.5)),
                    new EventResult($"{fallVariant.Description} You fall hard.", weight: 0.15, minutes: 15)
                        .DamageWithVariant(fallVariant)
                        .WithEffects(EffectFactory.SprainedAnkle(0.6))
                ])
            .Choice("Eat Something",
                "Maybe it's low blood sugar.",
                [
                    new EventResult("Food helps. Cramp releases quickly.", weight: 0.55, minutes: 8)
                        .Costs(ResourceType.Food, 1)
                        .WithEffects(EffectFactory.Focused(0.1, 60)),
                    new EventResult("Doesn't help the cramp but you feel steadier.", weight: 0.30, minutes: 10)
                        .Costs(ResourceType.Food, 1)
                        .WithEffects(EffectFactory.Sore(0.15, 45)),
                    new EventResult("Hard to eat through the pain. Nauseous.", weight: 0.15, minutes: 12)
                        .Costs(ResourceType.Food, 1)
                        .WithEffects(EffectFactory.Nauseous(0.25, 30))
                ],
                [EventCondition.HasFood])
            .Choice("Apply Heat",
                "Get close to the fire. Heat loosens muscles. (Risk: minor burn if too close)",
                [
                    new EventResult("Heat loosens it. Cramp releases smoothly.", weight: 0.80, minutes: 10),
                    new EventResult("Takes a while but warmth helps.", weight: 0.15, minutes: 18),
                    new EventResult("Too close. Minor burn, but cramp's gone.", weight: 0.05, minutes: 12)
                        .Damage(2, DamageType.Burn)
                        .WithEffects(EffectFactory.Burn(0.15, 45))
                ],
                [EventCondition.NearFire]);
    }

    private static GameEvent VisionBlur(GameContext ctx)
    {
        return new GameEvent("Vision Blur",
            "Your vision swims. Hard to focus. The world keeps sliding sideways.", 0.7)
            .Requires(EventCondition.LowHydration, EventCondition.Awake)
            .WithConditionFactor(EventCondition.LowCalories, 1.5)
            .Choice("Rub Eyes and Push On",
                "Shake it off. Keep going.",
                [
                    new EventResult("Clears momentarily. Still fuzzy around the edges.", weight: 0.50)
                        .WithEffects(EffectFactory.Shaken(0.25)),
                    new EventResult("Doesn't help. Getting worse.", weight: 0.30)
                        .WithEffects(EffectFactory.Shaken(0.4)),
                    new EventResult("Made it worse. Eyes burning now.", weight: 0.20)
                        .Damage(2, DamageType.Internal)
                        .WithEffects(EffectFactory.Shaken(0.3))
                ])
            .Choice("Rest Eyes",
                "Close your eyes, rest for a bit.",
                [
                    new EventResult("Rest helps. Vision clears.", weight: 0.70, minutes: 15),
                    new EventResult("Takes longer, but eventually clears.", weight: 0.30, minutes: 25)
                ])
            .Choice("Snow-Wipe Face",
                "Cold shock to restore alertness.",
                [
                    new EventResult("Works. Cold but vision clear.", weight: 0.85, minutes: 3)
                        .WithEffects(EffectFactory.Cold(-3, 15)),
                    new EventResult("Too cold. Vision still blurry.", weight: 0.15, minutes: 5)
                        .WithEffects(EffectFactory.Cold(-8, 25))
                ])
            .Choice("Drink Water",
                "Maybe it's dehydration.",
                [
                    new EventResult("Hydration helps. Vision clears.", weight: 0.60, minutes: 5)
                        .Costs(ResourceType.Water, 2),
                    new EventResult("Not just dehydration. Still blurry.", weight: 0.40, minutes: 5)
                        .Costs(ResourceType.Water, 2)
                        .WithEffects(EffectFactory.Shaken(0.2))
                ],
                [EventCondition.HasWater]);
    }

    // === PSYCHOLOGICAL EVENTS ===

    private static GameEvent ParanoiaEvent(GameContext ctx)
    {
        return new GameEvent("Paranoia",
            "You are certain — absolutely certain — you see eyes reflecting at the edge of the firelight.", 0.5)
            .Requires(EventCondition.AtCamp, EventCondition.Night, EventCondition.Awake)
            .WithSituationFactor(Situations.PsychologicallyCompromised, 3.0)  // Disturbed, stalked
            .Choice("Throw Fuel on Fire",
                "More light. Drive back the darkness.",
                [
                    new EventResult("Fire blazes up. Light reveals: nothing there. Probably.", weight: 0.80, minutes: 3)
                        .Costs(ResourceType.Fuel, 2),
                    new EventResult("Something was there — you see it slink away.", weight: 0.20, minutes: 3)
                        .Costs(ResourceType.Fuel, 2)
                        .CreateTension("Stalked", 0.2)
                ])
            .Choice("Investigate",
                "Step out into the dark and look.",
                [
                    new EventResult("Nothing. Your mind playing tricks.", weight: 0.55, minutes: 8)
                        .WithEffects(EffectFactory.Shaken(0.2)),
                    new EventResult("Something might have been there. Hard to tell.", weight: 0.20, minutes: 10)
                        .BecomeStalked(0.2),
                    new EventResult("Something is there.", weight: 0.25, minutes: 5)
                        .Encounter("Wolf", 20, 0.4)
                ])
            .Choice("Huddle by Fire",
                "Stay close. Wait it out.",
                [
                    new EventResult("You stare into the dark for hours. Sleep won't come easy.", weight: 0.70)
                        .WithEffects(EffectFactory.Paranoid(0.3)),
                    new EventResult("Eventually you relax. Nothing happened.", weight: 0.30, minutes: 30)
                        .Shaken()
                ]);
    }

    private static GameEvent MomentOfClarity(GameContext ctx)
    {
        return new GameEvent("Moment of Clarity",
            "Your mind clears. For a brief moment, everything makes sense. You see your situation with perfect clarity.", 0.12)
            .Requires(EventCondition.Awake)
            .WithSituationFactor(Situations.CriticallyDepleted, 2.0)  // LowCalories + LowHydration
            .WithSituationFactor(Situations.Vulnerable, 1.5)  // Injured, slow, impaired, no weapon
            .Choice("Act on It",
                "Use this clarity productively.",
                [
                    new EventResult("You notice something you'd been missing — a better approach.", weight: 0.60, minutes: 2)
                        .FindsSupplies(),
                    new EventResult("You see a solution to something that's been bothering you.", weight: 0.40, minutes: 2)
                        .WithEffects(EffectFactory.Focused(0.5, 120))
                ])
            .Choice("Rest in the Feeling",
                "Don't force it. Let clarity come naturally.",
                [
                    new EventResult("You feel centered. Calm.", weight: 1.0, minutes: 5)
                        .WithEffects(EffectFactory.Rested(0.7, 90))
                ]);
    }

    // === WOUND/INFECTION ARC ===

    private static GameEvent WoundFesters(GameContext ctx)
    {
        var woundTension = ctx.Tensions.GetTension("WoundUntreated");
        var bodyPart = woundTension?.Description ?? "wound";

        return new GameEvent("The Wound Festers",
            $"The {bodyPart} is red, swollen. Hot to the touch. This is infection.", 2.0)
            .Requires(EventCondition.WoundUntreated)
            .Choice("Clean It Properly",
                "Use water to thoroughly clean the wound.",
                [
                    new EventResult("Thorough cleaning. Infection stopped.", weight: 0.70, minutes: 15)
                        .Costs(ResourceType.Water, 2)
                        .ResolveTension("WoundUntreated")
                        .WithEffects(EffectFactory.Focused(0.1, 30)),
                    new EventResult("Cleaned but damage done. Mild fever remains.", weight: 0.20, minutes: 15)
                        .Costs(ResourceType.Water, 2)
                        .ResolveTension("WoundUntreated")
                        .WithEffects(EffectFactory.Exhausted(0.3, 120)),
                    new EventResult("Too late for just cleaning. Need more aggressive treatment.", weight: 0.10, minutes: 10)
                        .Costs(ResourceType.Water, 2)
                        .Escalate("WoundUntreated", 0.3)
                ],
                [EventCondition.HasWater])
            .Choice("Cauterize",
                "Brutal but effective. Use fire to burn out the infection.",
                [
                    new EventResult("Brutal but effective. Wound sealed.", weight: 0.60, minutes: 10)
                        .ResolveTension("WoundUntreated")
                        .Damage(5, DamageType.Burn)
                        .WithEffects(EffectFactory.Burn(0.4, 120)),
                    new EventResult("Effective but traumatic. You won't forget this.", weight: 0.25, minutes: 10)
                        .ResolveTension("WoundUntreated")
                        .Damage(5, DamageType.Burn)
                        .WithEffects(EffectFactory.Fear(0.3), EffectFactory.Burn(0.4, 120)),
                    new EventResult("Not thorough enough. Still infected.", weight: 0.10, minutes: 10)
                        .Damage(3, DamageType.Burn)
                        .WithEffects(EffectFactory.Burn(0.3, 90))
                        .Escalate("WoundUntreated", 0.1),
                    new EventResult("You can't do it. The pain stops you.", weight: 0.05, minutes: 5)
                ],
                [EventCondition.NearFire])
            .Choice("Herbal Treatment",
                "Use plant fiber as a poultice.",
                [
                    new EventResult("Poultice draws out infection.", weight: 0.65, minutes: 20)
                        .Costs(ResourceType.PlantFiber, 2)
                        .ResolveTension("WoundUntreated"),
                    new EventResult("Helps but slow. Still needs watching.", weight: 0.25, minutes: 20)
                        .Costs(ResourceType.PlantFiber, 2)
                        .Escalate("WoundUntreated", -0.2),
                    new EventResult("Not effective. Infection continues.", weight: 0.10, minutes: 20)
                        .Costs(ResourceType.PlantFiber, 2)
                ],
                [EventCondition.HasPlantFiber])
            .Choice("Ignore It",
                "You'll deal with it later.",
                [
                    new EventResult("Infection spreads. This is getting serious.", weight: 1.0)
                        .Escalate("WoundUntreated", 0.2)
                        .WithEffects(EffectFactory.Exhausted(0.2, 60))
                ]);
    }

    private static GameEvent FeverSetsIn(GameContext ctx)
    {
        return new GameEvent("Fever Sets In",
            "You're burning up. Chills and sweats. The infection has spread.", 2.5)
            .Requires(EventCondition.WoundUntreatedHigh)
            .Choice("Aggressive Treatment",
                "All-out effort to fight the infection. Use everything you have.",
                [
                    new EventResult("You fight the fever with everything. It breaks.", weight: 0.50, minutes: 60)
                        .ResolveTension("WoundUntreated")
                        .Costs(ResourceType.Water, 3)
                        .WithEffects(EffectFactory.Exhausted(0.6, 180)),
                    new EventResult("Not enough. The fever holds.", weight: 0.30, minutes: 45)
                        .Costs(ResourceType.Water, 2)
                        .WithEffects(EffectFactory.Fever(0.5), EffectFactory.Exhausted(0.4, 120)),
                    new EventResult("Your body is failing. This is critical.", weight: 0.20, minutes: 30)
                        .Costs(ResourceType.Water, 2)
                        .WithEffects(EffectFactory.Fever(0.7))
                        .Escalate("WoundUntreated", 0.2)
                ],
                [EventCondition.HasWater])
            .Choice("Rest and Fight It",
                "Your body vs. the infection. All you can do is rest.",
                [
                    new EventResult("Body wins. Fever breaks after 3 brutal hours.", weight: 0.35, minutes: 180)
                        .ResolveTension("WoundUntreated")
                        .WithEffects(EffectFactory.Exhausted(0.8, 240)),
                    new EventResult("Stalemate. Fever continues. You're weak but alive.", weight: 0.40, minutes: 120)
                        .WithEffects(EffectFactory.Fever(0.5), EffectFactory.Exhausted(0.5, 180)),
                    new EventResult("Body loses. Infection spreading. Condition critical.", weight: 0.25, minutes: 120)
                        .WithEffects(EffectFactory.Fever(0.8))
                        .Escalate("WoundUntreated", 0.3)
                        .Damage(10, DamageType.Internal)
                ])
            .Choice("Keep Working",
                "Deny the fever. Push on.",
                [
                    new EventResult("You push through. Every minute is agony.", weight: 0.40)
                        .WithEffects(EffectFactory.Fever(0.5))
                        .Escalate("WoundUntreated", 0.15),
                    new EventResult("You collapse. Your body has limits.", weight: 0.40, minutes: 60)
                        .WithEffects(EffectFactory.Fever(0.6), EffectFactory.Exhausted(0.5, 120)),
                    new EventResult("The fever wins. You go down.", weight: 0.20, minutes: 90)
                        .WithEffects(EffectFactory.Fever(0.75))
                        .Damage(8, DamageType.Internal)
                        .Aborts()
                ]);
    }

    private static GameEvent FrozenFingers(GameContext ctx)
    {
        var frostbiteVariant = VariantSelector.SelectFrostbiteByContext(ctx);
        var minorBurnVariant = VariantSelector.SelectEmberBurn(ctx);
        var severeBurnVariant = VariantSelector.SelectEmberBurn(ctx);

        return new GameEvent("Frozen Fingers",
            "Your fingers have gone white. You can't feel them properly. This is frostbite territory.", 0.8)
            .Requires(EventCondition.LowTemperature)
            .WithConditionFactor(EventCondition.Working, 1.5)
            .WithSituationFactor(Situations.ExtremeColdCrisis, 2.0)  // ExtremelyCold, blizzard + low fuel
            .Choice("Warm Them Now",
                "Stop everything. Get circulation back before tissue dies.",
                [
                    new EventResult("Painful but effective. Feeling returns.", weight: 0.60, minutes: 10),
                    new EventResult("Takes longer. More pain. But they'll heal.", weight: 0.25, minutes: 20)
                        .WithEffects(EffectFactory.Clumsy(0.3, 60)),
                    new EventResult("Caught it in time. No lasting damage.", weight: 0.10, minutes: 8),
                    new EventResult($"{frostbiteVariant.Description} Too late for some tissue.", weight: 0.05, minutes: 15)
                        .DamageWithVariant(frostbiteVariant)
                        .WithEffects(EffectFactory.Frostbite(0.6))
                ])
            .Choice("Tuck and Continue",
                "Hands under arms. Keep working as best you can.",
                [
                    new EventResult("Circulation returns slowly. Clumsy but functional.", weight: 0.50)
                        .WithEffects(EffectFactory.Clumsy(0.3, 45)),
                    new EventResult("Still losing feeling. You need to stop soon.", weight: 0.30)
                        .WithEffects(EffectFactory.Frostbite(0.3), EffectFactory.Clumsy(0.4, 60)),
                    new EventResult($"{frostbiteVariant.Description} Body heat isn't enough.", weight: 0.20)
                        .DamageWithVariant(frostbiteVariant)
                        .WithEffects(EffectFactory.Frostbite(0.5))
                ])
            .Choice("Use Fire",
                "Direct heat restores circulation fastest.",
                [
                    new EventResult("Direct heat restores circulation. Painful but effective.", weight: 0.80, minutes: 8),
                    new EventResult($"{minorBurnVariant.Description} Too close, but fingers saved.", weight: 0.15, minutes: 8)
                        .DamageWithVariant(minorBurnVariant)
                        .WithEffects(EffectFactory.Burn(0.15, 30)),
                    new EventResult($"{severeBurnVariant.Description} Numb fingers don't feel the heat.", weight: 0.05, minutes: 5)
                        .DamageWithVariant(severeBurnVariant)
                        .WithEffects(EffectFactory.Burn(0.3, 60))
                ],
                [EventCondition.NearFire]);
    }

    private static GameEvent OldAche(GameContext ctx)
    {
        var descriptions = new[]
        {
            "Your joints ache. The cold's settled deep.",
            "Stiffness creeps through your limbs. Every joint protests.",
            "Your body resists movement. Everything feels tight, reluctant."
        };

        return new GameEvent("Old Ache",
            descriptions[Random.Shared.Next(descriptions.Length)], 0.7)
            .Requires(EventCondition.Awake)
            .WithConditionFactor(EventCondition.LowTemperature, 1.5)
            .WithSituationFactor(Situations.Vulnerable, 2.0)  // Injured, slow, impaired, no weapon
            .WithConditionFactor(EventCondition.Working, 1.3)
            .Choice("Stretch and Rest",
                "Take a break. Let your body recover.",
                [
                    new EventResult("The rest helps. Pain subsides.", weight: 0.70, minutes: 30)
                        .WithEffects(EffectFactory.Rested(0.5, 120)),
                    new EventResult("Takes longer than expected, but eventually loosens up.", weight: 0.30, minutes: 45)
                        .WithEffects(EffectFactory.Rested(0.3, 60))
                ])
            .Choice("Work Through It",
                "Ignore the pain. Keep going.",
                [
                    new EventResult("Discomfort but manageable. You push on.", weight: 0.60)
                        .WithEffects(EffectFactory.Stiff(0.25, 360)),
                    new EventResult("Worse than expected. Every step hurts.", weight: 0.30)
                        .WithEffects(EffectFactory.Stiff(0.4, 240)),
                    new EventResult("Your body knows better. Forced rest anyway.", weight: 0.10, minutes: 30)
                        .WithEffects(EffectFactory.Stiff(0.3, 180))
                ])
            .Choice("Adjust Load",
                "Drop weight, change how you carry things.",
                [
                    new EventResult("Lighter load helps. Pain eases.", weight: 0.60, minutes: 10)
                        .WithEffects(EffectFactory.Sore(0.15, 60)),
                    new EventResult("Adjustment helps but you're still stiff.", weight: 0.40, minutes: 10)
                        .WithEffects(EffectFactory.Stiff(0.2, 180))
                ]);
    }

    private static GameEvent Toothbreaker(GameContext ctx)
    {
        return new GameEvent("Toothbreaker",
            "You bite down on something hard. A crack echoes in your skull. That was either the food or your tooth.", 0.4)
            .Requires(EventCondition.Eating)
            .WithConditionFactor(EventCondition.LowTemperature, 1.1) // frozen food
            .WithConditionFactor(EventCondition.LowOnFood, 1.3)
            .Choice("Spit It Out",
                "Lose the rest of the food but protect your teeth.",
                [
                    new EventResult("You spit it out. Mouth checked — teeth intact.", weight: 0.80, minutes: 2),
                    new EventResult("Lost some food but your teeth are fine.", weight: 0.20, minutes: 2)
                        .Costs(ResourceType.Food, 1)
                ])
            .Choice("Swallow Through Blood",
                "Get the calories. Deal with the pain.",
                [
                    new EventResult("Tooth cracked but holding. Pain lingers.", weight: 0.60, minutes: 3)
                        .WithEffects(EffectFactory.Pain(0.3))
                        .Damage(2, DamageType.Internal),
                    new EventResult("Tooth fine, just cut your gum. Minor.", weight: 0.30, minutes: 2)
                        .Damage(1, DamageType.Sharp),
                    new EventResult("Tooth broken. This will be a problem.", weight: 0.10, minutes: 5)
                        .WithEffects(EffectFactory.Pain(0.5))
                        .Damage(5, DamageType.Internal)
                ])
            .Choice("Check Carefully",
                "Take time to examine the damage.",
                [
                    new EventResult("Just the food. Your teeth are fine.", weight: 0.50, minutes: 5),
                    new EventResult("Small chip. Painful but not serious.", weight: 0.35, minutes: 5)
                        .Damage(1, DamageType.Internal),
                    new EventResult("Cracked tooth. Needs attention.", weight: 0.15, minutes: 5)
                        .WithEffects(EffectFactory.Pain(0.25))
                        .Damage(3, DamageType.Internal)
                ]);
    }

    private static GameEvent FugueState(GameContext ctx)
    {
        return new GameEvent("Fugue State",
            "You blink, and the sun has moved. You don't remember the last hour. You kept working, but you were somewhere else.", 0.2)
            .Requires(EventCondition.Working)
            .WithSituationFactor(Situations.CriticallyDepleted, 2.0)  // LowCalories + LowHydration
            .Choice("Come Back to Reality",
                "Assess the damage. What did you miss?",
                [
                    new EventResult("Time lost. Work done but you're drained.", weight: 0.50, minutes: 90)
                        .WithEffects(EffectFactory.Exhausted(0.4, 120))
                        .FindsSupplies(),
                    new EventResult("You worked while dissociated. But at what cost?", weight: 0.30, minutes: 120)
                        .WithEffects(EffectFactory.Exhausted(0.3, 90))
                        .FindsSupplies(),
                    new EventResult("You feel... hollow. What happened while you were gone?", weight: 0.15, minutes: 100)
                        .WithEffects(EffectFactory.Shaken(0.3), EffectFactory.Exhausted(0.5, 150))
                        .FindsSupplies(),
                    new EventResult("Something happened while you were away. You don't remember what.", weight: 0.05, minutes: 80)
                        .WithEffects(EffectFactory.Fear(0.25))
                        .BecomeStalked(0.2)
                ]);
    }

    private static GameEvent DistantCarcassStench(GameContext ctx)
    {
        var territory = ctx.CurrentLocation.GetFeature<AnimalTerritoryFeature>();
        var animal = territory?.GetRandomAnimalName() ?? "animal";

        return new GameEvent("Distant Carcass Stench",
            $"The wind brings a smell — death, recent. Something died nearby, or something killed nearby.", 0.6)
            .Requires(EventCondition.IsExpedition, EventCondition.InAnimalTerritory)
            .WithSituationFactor(Situations.PredatorInTerritory, 2.0)  // More likely in predator territory
            .Choice("Scout Toward It",
                "Follow the smell. Could be free meat.",
                [
                    new EventResult($"Find a {animal.ToLower()} carcass. Some meat left.", weight: 0.35, minutes: 30)
                        .FindsMeat(),
                    new EventResult("Find the carcass. Something's already there.", weight: 0.25, minutes: 25)
                        .BecomeStalked(0.3),
                    new EventResult("Tracks lead to a hunting ground. Good location.", weight: 0.20, minutes: 35)
                        .FindsGameTrail(),
                    new EventResult("Can't find it. Wind shifted.", weight: 0.15, minutes: 30),
                    new EventResult("Find it. And what killed it.", weight: 0.05, minutes: 20)
                        .Encounter(territory?.GetRandomPredatorName() ?? "Wolf", 25, 0.6)
                ])
            .Choice("Mark the Direction",
                "Note for later investigation.",
                [
                    new EventResult("You mark the direction mentally.", weight: 1.0, minutes: 3)
                ])
            .Choice("Avoid the Area",
                "Something that kills is probably around. Give it space.",
                [
                    new EventResult("You adjust your route. Slower but safer.", weight: 0.80, minutes: 15),
                    new EventResult("Detour takes you through difficult terrain.", weight: 0.20, minutes: 25)
                        .WithEffects(EffectFactory.Sore(0.15, 45))
                ]);
    }

    // === DISTURBED ARC INTERSECTION EVENTS ===

    private static GameEvent ShadowMovement(GameContext ctx)
    {
        var stalkedTension = ctx.Tensions.GetTension("Stalked");
        var predator = stalkedTension?.AnimalType ?? "something";

        return new GameEvent("Shadow Movement",
            $"Movement in your peripheral vision. Your heart hammers. Is it the {predator.ToLower()}? Or your mind again?", 2.0)
            .Requires(EventCondition.Disturbed, EventCondition.Stalked, EventCondition.IsExpedition)
            .WithSituationFactor(Situations.SeverelyCompromised, 1.5)  // DisturbedHigh or StalkedHigh
            .Choice("Assume It's Real",
                "Act as if the threat is real. Better safe than dead.",
                [
                    new EventResult("You react defensively. Nothing attacks. Was it real?", 0.40, 10)
                        .WithEffects(EffectFactory.Paranoid(0.2)),
                    new EventResult("It WAS real. Your vigilance saved you.", 0.25, 5)
                        .EscalatesStalking(0.15)
                        .Unsettling(),
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
                        .EscalatesStalking(0.25)
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
                        .ResolvesStalking()
                        .CreateTension("Hunted", 0.5, animalType: stalkedTension?.AnimalType),
                    new EventResult("Nothing. Just paranoia. Or maybe it left.", 0.10, 15)
                ]);
    }

    private static GameEvent CutOff(GameContext ctx)
    {
        // Check for existing threat
        var stalked = ctx.Tensions.GetTension("Stalked");
        var hunted = ctx.Tensions.GetTension("Hunted");
        var predator = stalked?.AnimalType ?? hunted?.AnimalType ?? "Wolf";
        bool underThreat = stalked != null || hunted != null;

        // Distance description
        string distanceDesc = ctx.Check(EventCondition.VeryFarFromCamp)
            ? "You're a long way from camp"
            : "You're far from safety";

        var evt = new GameEvent("Cut Off",
            $"{distanceDesc}. The terrain narrows here — cliffs to one side, frozen water to the other. If something comes at you, there's nowhere to go.", 0.9)
            .Requires(EventCondition.FarFromCamp, EventCondition.AtTerrainBottleneck)
            .WithSituationFactor(Situations.UnderThreat, 3.0)
            .WithSituationFactor(Situations.Vulnerable, 2.0);

        // VARIANT: If already under threat, high chance of encounter
        if (underThreat)
        {
            evt.Choice("Push through quickly",
                "Move fast, before anything notices.",
                [
                    new EventResult($"You make your move. The {predator.ToLower()} was waiting. It strikes from the bottleneck.", 0.5, 5)
                        .ResolvesStalking()
                        .Encounter(predator, 15, 0.6),
                    new EventResult("You make it through. But you were exposed.", 0.3, 10)
                        .EscalatesStalking(0.2),
                    new EventResult("You stumble in your haste. The slip costs you time.", 0.2, 15)
                        .WithEffects(EffectFactory.Shaken(0.2))
                ]);
        }
        // DEFAULT: Not yet threatened
        else
        {
            evt.Choice("Push through quickly",
                "Move fast, before anything notices.",
                [
                    new EventResult("You make it through. But you were exposed — anything watching knows you're here.", 0.7, 10)
                        .BecomeStalked(0.3),
                    new EventResult("You stumble in your haste. The slip costs you time — and composure.", 0.3, 15)
                        .WithEffects(EffectFactory.Shaken(0.2))
                ]);
        }

        return evt.Choice("Backtrack",
            "This isn't worth the risk. Find another way.",
            [
                new EventResult("You retreat. The long way around costs time, but you're still breathing.", 1.0, 20)
            ]);
    }

    // === CARCASS ATTRACTION EVENTS ===

    /// <summary>
    /// Something is investigating your carcass location.
    /// Low scent threshold - early warning.
    /// </summary>
    private static GameEvent CarcassInvestigation(GameContext ctx)
    {
        var carcass = ctx.CurrentLocation.GetFeature<CarcassFeature>();
        string animal = carcass?.AnimalName ?? "animal";

        return new GameEvent(
            "Something Investigates",
            $"Fresh tracks circle the {animal.ToLower()} carcass. Something has been here recently.", 0.8)
            .Requires(EventCondition.HasCarcass)
            .WithConditionFactor(EventCondition.HasFreshCarcass, 1.5)
            .WithConditionFactor(EventCondition.PlayerBloody, 1.3)
            .Choice("Search for signs",
                "See if you can tell what's been investigating.",
                [
                    new EventResult("Wolf tracks. A scout. The pack will come.", 0.4, 5)
                        .BecomeStalked(0.3, "wolf"),
                    new EventResult("Fox tracks. Bold, but not dangerous.", 0.35, 5),
                    new EventResult("You were so focused on the tracks you didn't notice it watching from the treeline.", 0.25, 5)
                        .BecomeStalked(0.4)
                        .Frightening()
                ])
            .Choice("Work faster",
                "Whatever it is will be back. Hurry.",
                [
                    new EventResult("You pick up the pace, keeping one eye on the treeline.", 1.0, 0)
                ])
            .Choice("Abandon the carcass",
                "Not worth the risk. Leave it.",
                [
                    new EventResult("You leave the carcass. Let them have it.", 1.0, 2)
                ]);
    }

    /// <summary>
    /// Scavenger approaches while you're at a fresh carcass.
    /// Medium scent threshold.
    /// </summary>
    private static GameEvent ScavengerApproach(GameContext ctx)
    {
        var carcass = ctx.CurrentLocation.GetFeature<CarcassFeature>();
        string carcassName = carcass?.AnimalName ?? "carcass";

        // Pick a scavenger based on territory
        var territory = ctx.CurrentLocation.GetFeature<AnimalTerritoryFeature>();
        string scavenger = territory?.HasPredators() == true ? "wolf" : "fox";

        return new GameEvent(
            "Scavenger Approach",
            $"A {scavenger} emerges from the brush, eyeing the {carcassName.ToLower()} carcass. It hasn't committed yet.", 0.7)
            .Requires(EventCondition.HasFreshCarcass)
            .WithConditionFactor(EventCondition.HasStrongScent, 1.5)
            .WithConditionFactor(EventCondition.PlayerBloody, 1.4)
            .Choice("Shout and wave",
                "Scare it off. Show you're not easy prey.",
                [
                    new EventResult($"The {scavenger} slinks away. It will remember this place.", 0.7, 2),
                    new EventResult($"The {scavenger} flinches but doesn't leave. It's hungry.", 0.2, 2)
                        .BecomeStalked(0.25, scavenger),
                    new EventResult($"Your shouting draws more attention. Now there are two.", 0.1, 2)
                        .CreateTension("PackNearby", 0.3, animalType: scavenger)
                ])
            .Choice("Ignore it",
                "Keep working. One animal won't attack while you're active.",
                [
                    new EventResult($"The {scavenger} watches but keeps its distance. For now.", 0.6, 0)
                        .BecomeStalked(0.2, scavenger),
                    new EventResult($"The {scavenger} creeps closer. You lock eyes. It backs off.", 0.3, 0),
                    new EventResult($"The {scavenger} lunges! It wants this kill.", 0.1, 5)
                        .Encounter(scavenger, 10, 0.5)
                ])
            .Choice("Throw it some meat",
                "Maybe feeding it will keep it occupied.",
                [
                    new EventResult($"It takes the offering and retreats to eat.", 0.7, 3)
                        .Costs(ResourceType.Food, 1),
                    new EventResult($"It grabs the meat and wants more. Others are watching.", 0.3, 3)
                        .Costs(ResourceType.Food, 1)
                        .BecomeStalked(0.3, scavenger)
                ]);
    }

    /// <summary>
    /// A predator challenges you for the carcass while butchering.
    /// High scent threshold, requires Working.
    /// </summary>
    private static GameEvent ContestedKill(GameContext ctx)
    {
        var carcass = ctx.CurrentLocation.GetFeature<CarcassFeature>();
        string carcassName = carcass?.AnimalName ?? "kill";

        // More dangerous predator for contested kills
        var territory = ctx.CurrentLocation.GetFeature<AnimalTerritoryFeature>();
        string predator = territory?.GetRandomAnimalName() ?? "wolf";
        if (territory?.HasPredators() == true)
            predator = "wolf";  // Default to wolf for contested kills

        return new GameEvent(
            "Contested Kill",
            $"A low growl. A {predator.ToLower()} steps into view, eyes fixed on your {carcassName.ToLower()}. It's not backing down.", 0.6)
            .Requires(EventCondition.HasStrongScent, EventCondition.Working)
            .WithConditionFactor(EventCondition.PlayerBloody, 1.5)
            .WithConditionFactor(EventCondition.Injured, 1.3)
            .Choice("Stand your ground",
                "This is YOUR kill. Make that clear.",
                [
                    new EventResult($"You raise your arms and shout. The {predator.ToLower()} hesitates, then retreats.", 0.4, 5)
                        .ResolvesStalking(),
                    new EventResult($"You lock eyes. Neither of you blinks. It circles once, then leaves.", 0.3, 8)
                        .Frightening(),
                    new EventResult($"It charges. You're between it and food.", 0.3, 3)
                        .Encounter(predator, 8, 0.7)
                ])
            .Choice("Grab what you have",
                "Take what you've butchered and go. The rest isn't worth dying for.",
                [
                    new EventResult("You back away slowly with your haul. It claims the rest.", 0.8, 5)
                        .Aborts(),
                    new EventResult($"As you turn, it lunges. It wanted YOU, not the {carcassName.ToLower()}.", 0.2, 3)
                        .Encounter(predator, 5, 0.6)
                        .Aborts()
                ])
            .Choice("Use fire",
                "Light a torch. Most animals fear flame.",
                ctx.Inventory.HasLitTorch || ctx.CurrentLocation.HasActiveHeatSource()
                    ? [
                        new EventResult($"You thrust the flame forward. The {predator.ToLower()} snarls but backs away.", 0.8, 5)
                            .ResolvesStalking(),
                        new EventResult($"It flinches from the flame but doesn't leave. It's starving.", 0.2, 5)
                            .BecomeStalked(0.4, predator)
                    ]
                    : [
                        new EventResult("You have no fire to use.", 1.0, 0)
                    ]);
    }

    /// <summary>
    /// Return to find scavengers have claimed the carcass.
    /// Triggers when player returns to carcass location after time away.
    /// </summary>
    private static GameEvent CarcassClaimed(GameContext ctx)
    {
        var carcass = ctx.CurrentLocation.GetFeature<CarcassFeature>();
        if (carcass == null) return new GameEvent("Empty", "", 0).Requires(EventCondition.Cornered);  // Invalid

        string animal = carcass.AnimalName;
        double lossPercent = Random.Shared.NextDouble() * 0.5 + 0.3;  // 30-80% loss
        double remainingKg = carcass.MeatRemainingKg * (1 - lossPercent);

        return new GameEvent(
            "Carcass Claimed",
            $"You return to find the {animal.ToLower()} carcass torn apart. Scavengers have been at it. Some meat remains.", 0.5)
            .Requires(EventCondition.HasCarcass, EventCondition.HasFreshCarcass)
            .RequiresSituation(ctx => !ctx.Check(EventCondition.Working))  // Not during active work
            .Choice("Salvage what's left",
                $"There's still meat here. Maybe {remainingKg:F1}kg.",
                [
                    new EventResult("You gather what the scavengers left behind.", 0.7, 10)
                        .WithScavengerLoss(lossPercent),
                    new EventResult("You hear movement nearby. The scavengers are watching, waiting for you to leave.", 0.3, 8)
                        .WithScavengerLoss(lossPercent)
                        .BecomeStalked(0.25)
                ])
            .Choice("Track the scavengers",
                "Fresh tracks lead away. They can't have gone far.",
                [
                    new EventResult("The tracks lead into thick brush. They're watching you.", 0.5, 15)
                        .BecomeStalked(0.3),
                    new EventResult("You find them. A standoff at their cache.", 0.3, 20)
                        .Encounter("wolf", 20, 0.4),
                    new EventResult("The trail goes cold. They know this territory better than you.", 0.2, 25)
                ])
            .Choice("Leave it",
                "Not worth fighting over scraps.",
                [
                    new EventResult($"You abandon what remains of the {animal.ToLower()}.", 1.0, 2)
                ]);
    }
}
