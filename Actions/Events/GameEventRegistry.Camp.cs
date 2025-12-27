using text_survival.Actions.Variants;
using text_survival.Bodies;
using text_survival.Effects;
using text_survival.Environments.Features;
using text_survival.Items;

namespace text_survival.Actions;

public static partial class GameEventRegistry
{
    private static GameEvent VerminRaid(GameContext ctx)
    {
        var biteVariant = VariantSelector.SelectVerminBite(ctx);

        return new GameEvent("Vermin Raid",
            "Scratching from your supply cache. Something small has found your food stores.", 1.0)
            .Requires(EventCondition.AtCamp, EventCondition.HasFood, EventCondition.Awake)
            // Night/darkness conditions increase vermin activity
            .WithSituationFactor(Situations.InDarkness, 1.5)
            .Choice("Set a Trap",
                "Use plant fiber to rig a simple snare.",
                [
                    new EventResult("The trap catches it. A small meal, and the problem is solved.", 0.50, 15)
                        .Costs(ResourceType.PlantFiber, 1)
                        .Rewards(RewardPool.SmallGame),
                    new EventResult("The trap fails. It escapes with some of your food.", 0.30, 15)
                        .Costs(ResourceType.Food, 1),
                    new EventResult("You catch it — and realize it's not alone. There are more in your walls.", 0.15, 20)
                        .Costs(ResourceType.PlantFiber, 1)
                        .CreateTension("Infested", 0.3, ctx.CurrentLocation),
                    new EventResult("Perfect catch. Quality fur for insulation.", 0.05, 15)
                        .Costs(ResourceType.PlantFiber, 1)
                        .Rewards(RewardPool.HideScrap)
                ],
                requires: [EventCondition.HasPlantFiber])
            .Choice("Flood the Nest",
                "Pour water into their burrow.",
                [
                    new EventResult("Drowned. Problem solved, though some food got wet.", 0.60, 10)
                        .Costs(ResourceType.Water, 2),
                    new EventResult("They flee. Might come back later.", 0.25, 10)
                        .Costs(ResourceType.Water, 2),
                    new EventResult("The water reveals a larger tunnel system. This is worse than you thought.", 0.15, 15)
                        .Costs(ResourceType.Water, 2)
                        .CreateTension("Infested", 0.4, ctx.CurrentLocation)
                ],
                requires: [EventCondition.HasWater])
            .Choice("Kill It Directly",
                "Corner it and finish it quickly.",
                [
                    new EventResult("Quick kill. Small meal.", 0.45, 5)
                        .Rewards(RewardPool.SmallGame),
                    new EventResult("It escapes, taking some food with it.", 0.35, 8)
                        .Costs(ResourceType.Food, 1),
                    new EventResult("You destroy some supplies in the attempt.", 0.15, 10)
                        .Costs(ResourceType.Food, 2),
                    new EventResult($"{biteVariant.Description} The puncture bleeds freely.", 0.05, 5)
                        .DamageWithVariant(biteVariant)
                        .CreateTension("WoundUntreated", 0.2, description: biteVariant.GetDisplayName(ctx.player.Body))
                ])
            .Choice("Ignore It",
                "Not worth the effort right now.",
                [
                    new EventResult("It takes what it wants and leaves. You lose some food.", 0.50, 0)
                        .Costs(ResourceType.Food, 1),
                    new EventResult("It's still there in the morning. This is becoming a problem.", 0.30, 0)
                        .CreateTension("Infested", 0.3, ctx.CurrentLocation),
                    new EventResult("The commotion attracts something larger.", 0.20, 0)
                        .CreateTension("Stalked", 0.2)
                ]);
    }

    private static GameEvent ShelterGroans(GameContext ctx)
    {
        return new GameEvent("Shelter Groans",
            "A crack from above. Your shelter is taking strain. Snow load or wind — something's giving.", 0.2)
            .Requires(EventCondition.AtCamp, EventCondition.HasShelter)
            // StructuralStress covers: ShelterWeakened + (HighWind or IsSnowing)
            .WithSituationFactor(Situations.StructuralStress, 3.0)
            .Choice("Brace It Now",
                "Hold it together with your body. Buy time.",
                [
                    new EventResult("You hold it together. Shelter survives, but it's weaker.", 0.55, 10)
                        .WithEffects(EffectFactory.Exhausted(0.3, 45))
                        .ModifiesFeature(typeof(ShelterFeature), 0.05)
                        .CreateTension("ShelterWeakened", 0.2, ctx.CurrentLocation),
                    new EventResult("Partial collapse. Shelter damaged but standing.", 0.30, 15)
                        .CreateTension("ShelterWeakened", 0.4, ctx.CurrentLocation)
                        .ModifiesFeature(typeof(ShelterFeature), 0.2),
                    new EventResult("You brace it successfully and spot a weakness to reinforce later.", 0.10, 12),
                    new EventResult("It comes down on you.", 0.05, 5)
                        .DebrisDamage(8)
                        .HarshCold()
                        .ModifiesFeature(typeof(ShelterFeature), 0.7)
                ])
            .Choice("Reinforce with Materials",
                "Use branches and debris to prop up weak points.",
                [
                    new EventResult("Solid reinforcement. Shelter improved.", 0.70, 20)
                        .Costs(ResourceType.Fuel, 2)
                        .ResolveTension("ShelterWeakened"),
                    new EventResult("Uses more material than expected but works.", 0.20, 25)
                        .Costs(ResourceType.Fuel, 3)
                        .ResolveTension("ShelterWeakened"),
                    new EventResult("The materials aren't enough. Still needs work.", 0.10, 15)
                        .Costs(ResourceType.Fuel, 2)
                        .CreateTension("ShelterWeakened", 0.3, ctx.CurrentLocation)
                ],
                requires: [EventCondition.HasFuel])
            .Choice("Evacuate",
                "Get out before it collapses.",
                [
                    new EventResult("You get out clean. Shelter collapses behind you.", 0.70, 3)
                        .ModerateCold()
                        .RemovesFeature(typeof(ShelterFeature)),
                    new EventResult("Almost clear. Debris catches you.", 0.30, 5)
                        .DebrisDamage(5)
                        .ModerateCold()
                        .RemovesFeature(typeof(ShelterFeature))
                ]);
    }

    private static GameEvent ChokingSmoke(GameContext ctx)
    {
        return new GameEvent("Choking Smoke",
            "The wind shifts. Thick smoke floods back into your space. You can't breathe.", 0.8)
            .Requires(EventCondition.AtCamp, EventCondition.FireBurning, EventCondition.IsCampWork)
            .WithConditionFactor(EventCondition.HighWind, 2.0)
            .WithConditionFactor(EventCondition.HasShelter, 1.5)
            .Choice("Douse the Fire",
                "Put it out immediately. You'll deal with the cold.",
                [
                    new EventResult("Fire out. Safe lungs, but now you're getting cold.", 1.0, 2)
                        .WithCold(-10, 30)
                    // TODO: Extinguish fire - add fire state integration when HeatSourceFeature.Extinguish() is available
                ])
            .Choice("Endure and Ventilate",
                "Create an opening, redirect the smoke. It'll hurt your lungs.",
                [
                    new EventResult("You create an opening. Smoke clears.", 0.50, 15)
                        .WithEffects(EffectFactory.Coughing(0.3, 60)),
                    new EventResult("Takes longer, more smoke inhaled.", 0.30, 25)
                        .WithEffects(EffectFactory.Coughing(0.5, 90)),
                    new EventResult("You redirect the smoke. Much better.", 0.15, 20)
                        .WithEffects(EffectFactory.Coughing(0.2, 30)),
                    new EventResult("You pass out briefly from the smoke.", 0.05, 10)
                        .Damage(5, DamageType.Internal)
                        .WithEffects(EffectFactory.Coughing(0.7, 120))
                ])
            .Choice("Evacuate Temporarily",
                "Leave shelter, wait for wind to shift.",
                [
                    new EventResult("You wait outside. The wind shifts eventually.", 0.60, 30)
                        .WithCold(-8, 35),
                    new EventResult("Takes longer than expected. Severe cold exposure.", 0.40, 50)
                        .WithCold(-15, 50)
                ]);
    }

    private static GameEvent EmbersScatter(GameContext ctx)
    {
        var burnVariant = VariantSelector.SelectEmberBurn(ctx);

        return new GameEvent("Embers Scatter",
            "A gust catches the dying fire. Embers scatter across your camp.", 0.6)
            .Requires(EventCondition.AtCamp, EventCondition.FireBurning, EventCondition.IsCampWork)
            .WithConditionFactor(EventCondition.HighWind, 2.5)
            .Choice("Stomp Them Out",
                "Quick! Before something catches fire.",
                [
                    new EventResult($"Quick response. {burnVariant.Description}", 0.60, 3)
                        .DamageWithVariant(burnVariant)
                        .WithEffects(EffectFactory.Burn(0.2, 60)),
                    new EventResult("All contained, no harm.", 0.30, 5),
                    new EventResult("You miss one. Something catches fire — you lose some supplies.", 0.10, 8)
                        .Costs(ResourceType.Fuel, 1)
                ])
            .Choice("Protect the Fire",
                "Keep the embers together. You need that fire.",
                [
                    new EventResult("You save the embers but some scatter. A supply item is damaged.", 0.50, 5)
                        .Costs(ResourceType.Tinder, 1),
                    new EventResult("You protect the fire successfully.", 0.40, 8),
                    new EventResult("Wind wins. Fire goes out, embers everywhere.", 0.10, 5)
                        .WithCold(-8, 25)
                    // TODO: Change fire state to embers - add fire state integration when available
                ])
            .Choice("Let It Burn Out",
                "Accept the fire loss. Protect everything else.",
                [
                    new EventResult("The embers die. You'll need to restart the fire, but camp is safe.", 1.0, 5)
                        .MinorCold()
                    // TODO: Change fire state to extinguished - add fire state integration when available
                ]);
    }

    private static GameEvent RustleAtCampEdge(GameContext ctx)
    {
        var territory = ctx.CurrentLocation.GetFeature<AnimalTerritoryFeature>();
        var predator = territory?.GetRandomPredatorName() ?? "Wolf";

        return new GameEvent("Rustle at Camp Edge",
            $"Rustling at the camp perimeter. Something drawn by the scent of your food.", 0.8)
            .Requires(EventCondition.AtCamp, EventCondition.HasFood, EventCondition.Awake)
            // InDarkness covers: Night, InDarkness conditions
            .WithSituationFactor(Situations.InDarkness, 2.0)
            // AttractiveToPredators covers: HasMeat, Bleeding, FoodScentStrong
            .WithSituationFactor(Situations.AttractiveToPredators, 1.5)
            .Choice("Investigate",
                "Go see what's out there.",
                [
                    new EventResult("A weak rabbit. Easy catch.", 0.25, 10)
                        .Rewards(RewardPool.SmallGame),
                    new EventResult("A fox. It retreats but doesn't go far.", 0.20, 8)
                        .BecomeStalked(0.15, "Fox"),
                    new EventResult("Nothing there now. Tracks suggest a small scavenger.", 0.15, 8),
                    new EventResult($"{predator}. Close. It hasn't decided if you're prey yet.", 0.20, 5)
                        .Frightening()
                        .BecomeStalked(0.4, predator),
                    new EventResult($"{predator}. It charges.", 0.15, 0)
                        .Panicking()
                        .Encounter(predator, 15, 0.6),
                    new EventResult("Blood on the snow. Something killed here recently — and it's still nearby.", 0.05, 3)
                        .Terrifying()
                        .BecomeStalked(0.5, predator)
                ])
            .Choice("Throw a Rock",
                "Scare it off with noise.",
                [
                    new EventResult("It crashes away through the brush.", 0.60, 2),
                    new EventResult("Miss. The noise brings it closer briefly, then it flees.", 0.25, 3),
                    new EventResult("Your noise provokes something larger than expected.", 0.15, 5)
                        .Encounter(predator, 25, 0.4)
                ])
            .Choice("Ignore It",
                "Probably just a scavenger. Not worth leaving the fire.",
                [
                    new EventResult("It passes by. Nothing taken.", 0.50, 0),
                    new EventResult("It steals some food while you're not looking.", 0.35, 0)
                        .Costs(ResourceType.Food, 1),
                    new EventResult("Ignoring it emboldens it. It'll be back.", 0.15, 0)
                        .BecomeStalked(0.2, "Scavenger")
                ]);
    }

    private static GameEvent MeltingReveal(GameContext ctx)
    {
        return new GameEvent("Melting Reveal",
            "The heat from your fire has melted the permafrost beneath. Something's emerging from the ground.", 0.4)
            .Requires(EventCondition.AtCamp, EventCondition.FireBurning, EventCondition.IsCampWork)
            .Choice("Excavate",
                "Dig it out. See what's there.",
                [
                    new EventResult("Previous occupant's cache. They're not coming back.", 0.40, 25)
                        .Rewards(RewardPool.HiddenCache),
                    new EventResult("Bones. Old. Animal. Useful for tools.", 0.30, 20)
                        .Rewards(RewardPool.BoneHarvest),
                    new EventResult("Nothing useful. Rocks and frozen dirt.", 0.15, 28),
                    new EventResult("Something strange. A relic from before.", 0.10, 30)
                        .Rewards(RewardPool.CraftingMaterials),
                    new EventResult("You disturb something you shouldn't have. A chill runs through you.", 0.05, 15)
                        .Terrifying()
                        .WithEffects(EffectFactory.Shaken(0.3))
                ])
            .Choice("Let It Emerge",
                "Wait and see what the heat reveals.",
                [
                    new EventResult("Patience reveals... nothing interesting.", 0.50, 30),
                    new EventResult("Eventually some bones emerge. Could be useful.", 0.30, 35)
                        .Rewards(RewardPool.BoneHarvest),
                    new EventResult("The heat reveals a cache. Someone hid this deliberately.", 0.20, 40)
                        .Rewards(RewardPool.HiddenCache)
                ])
            .Choice("Cover It Back Up",
                "You don't want to know what's down there.",
                [
                    new EventResult("You pile snow back over it. Whatever it was stays buried.", 0.80, 5),
                    new EventResult("Something about it stays with you. An uneasy feeling.", 0.20, 5)
                        .Shaken()
                ]);
    }

    // === DISTURBED ARC EVENTS ===

    private static GameEvent Nightmare(GameContext ctx)
    {
        var disturbedTension = ctx.Tensions.GetTension("Disturbed");
        var source = disturbedTension?.Description ?? "something terrible";

        return new GameEvent("Nightmare",
            $"You jolt awake, heart pounding. Images of {source} linger behind your eyes. The fire has burned lower than you'd like.", 1.5)
            .Requires(EventCondition.AtCamp, EventCondition.Disturbed, EventCondition.IsSleeping)
            // PsychologicallyCompromised covers: Disturbed/DisturbedHigh + Stalked states
            .WithSituationFactor(Situations.PsychologicallyCompromised, 2.0)
            // InDarkness covers: Night, InDarkness conditions
            .WithSituationFactor(Situations.InDarkness, 1.5)
            .Choice("Try to Sleep Again",
                "Close your eyes. Push it down.",
                [
                    new EventResult("Sleep eventually returns, but it's fitful.", 0.50, 30)
                        .WithEffects(EffectFactory.Exhausted(0.2, 120)),
                    new EventResult("Every time you close your eyes, you see it again.", 0.30, 60)
                        .WithEffects(EffectFactory.Exhausted(0.35, 180))
                        .Escalate("Disturbed", 0.1),
                    new EventResult("Sleep won't come. You lie awake until dawn.", 0.15, 120)
                        .WithEffects(EffectFactory.Exhausted(0.5, 240)),
                    new EventResult("Somehow, you find rest. The dream fades.", 0.05, 0)
                        .WithEffects(EffectFactory.Rested(0.4, 60))
                        .Escalate("Disturbed", -0.1)
                ])
            .Choice("Stay Awake",
                "No more sleep tonight. Tend the fire.",
                [
                    new EventResult("You feed the fire and watch the darkness. No more dreams.", 0.70, 90)
                        .Costs(ResourceType.Fuel, 1)
                        .Escalate("Disturbed", -0.05),
                    new EventResult("The night stretches on. You're exhausted but won't risk sleeping.", 0.30, 120)
                        .WithEffects(EffectFactory.Exhausted(0.4, 180))
                ],
                requires: [EventCondition.HasFuel])
            .Choice("Move Around",
                "Walk it off. Get your blood moving.",
                [
                    new EventResult("The cold air clears your head. You feel more grounded.", 0.60, 20)
                        .MinorCold()
                        .Escalate("Disturbed", -0.05),
                    new EventResult("The darkness feels oppressive. Every shadow hides something.", 0.30, 15)
                        .WithCold(-8, 25)
                        .WithEffects(EffectFactory.Paranoid(0.2)),
                    new EventResult("A noise in the dark. Probably nothing. Probably.", 0.10, 10)
                        .MinorCold()
                        .BecomeStalked(0.15)
                ]);
    }

    private static GameEvent NightTerrors(GameContext ctx)
    {
        return new GameEvent("Night Terrors",
            "You're convinced something is out there. In the dark. Watching. Waiting. Every sound is a footstep, every rustle is breathing.", 0.8)
            .Requires(EventCondition.AtCamp, EventCondition.Disturbed, EventCondition.Night, EventCondition.Awake)
            // PsychologicallyCompromised covers: Disturbed/DisturbedHigh + Stalked states
            .WithSituationFactor(Situations.PsychologicallyCompromised, 3.0)
            .Choice("Build Up the Fire",
                "Light drives back the dark. And whatever's in it.",
                [
                    new EventResult("The fire blazes. The circle of light expands. Nothing there.", 0.70, 10)
                        .BurnsFuel(2)
                        .Escalate("Disturbed", -0.05),
                    new EventResult("The flames reveal nothing. But you're not convinced.", 0.25, 10)
                        .BurnsFuel(2)
                        .WithEffects(EffectFactory.Paranoid(0.15)),
                    new EventResult("In the flickering light, you see movement. Real this time.", 0.05, 5)
                        .BurnsFuel(2)
                        .BecomeStalked(0.3)
                ],
                requires: [EventCondition.HasFuel])
            .Choice("Investigate",
                "Know for certain. Even if it kills you.",
                [
                    new EventResult("Nothing. Just your mind playing tricks.", 0.40, 15)
                        .Shaken(),
                    new EventResult("You find nothing but you don't feel better.", 0.35, 20)
                        .WithCold(-8, 25)
                        .WithEffects(EffectFactory.Paranoid(0.25)),
                    new EventResult("Wait. Was that... no. Nothing. Nothing.", 0.20, 15)
                        .Frightening()
                        .Escalate("Disturbed", 0.15),
                    new EventResult("Something WAS there. You see it slink away.", 0.05, 10)
                        .BecomeStalked(0.35)
                ])
            .Choice("Endure It",
                "Stay still. Don't give in to the fear.",
                [
                    new EventResult("You sit through the night. Tense. Watching. Dawn eventually comes.", 0.50, 60)
                        .WithEffects(EffectFactory.Exhausted(0.3, 120)),
                    new EventResult("You can't do this forever. Your nerves are shot.", 0.35, 45)
                        .WithEffects(EffectFactory.Paranoid(0.3))
                        .Escalate("Disturbed", 0.1),
                    new EventResult("Somehow, sitting with the fear helps. You feel stronger.", 0.15, 30)
                        .Escalate("Disturbed", -0.15)
                        .WithEffects(EffectFactory.Hardened(0.1, 120))
                ]);
    }

    private static GameEvent ProcessingTrauma(GameContext ctx)
    {
        var disturbedTension = ctx.Tensions.GetTension("Disturbed");
        var source = disturbedTension?.Description ?? "what you saw";

        return new GameEvent("Processing",
            $"Sitting by the fire, your thoughts keep returning to {source}. Maybe it's time to let yourself think about it.", 0.6)
            .Requires(EventCondition.AtCamp, EventCondition.Disturbed, EventCondition.NearFire, EventCondition.Awake)
            // PsychologicallyCompromised covers: Disturbed/DisturbedHigh + Stalked states
            .WithSituationFactor(Situations.PsychologicallyCompromised, 1.5)
            .Choice("Sit With It",
                "Don't push it away. Let it come.",
                [
                    new EventResult("It's painful. But when you're done, something has shifted.", 0.50, 45)
                        .Escalate("Disturbed", -0.25)
                        .WithEffects(EffectFactory.Exhausted(0.2, 60)),
                    new EventResult("You think about it until the fire burns low. Somehow, that helps.", 0.30, 60)
                        .Escalate("Disturbed", -0.35)
                        .WithEffects(EffectFactory.Rested(0.4, 30)),
                    new EventResult("Too much. You have to stop. But you made progress.", 0.15, 30)
                        .Escalate("Disturbed", -0.1)
                        .WithEffects(EffectFactory.Shaken(0.2)),
                    new EventResult("The fire helps. Warmth, light, life. The dead stay dead.", 0.05, 30)
                        .ResolveTension("Disturbed")
                        .WithEffects(EffectFactory.Hardened(0.15, 180), EffectFactory.Focused(0.2, 90))
                ])
            .Choice("Distract Yourself",
                "Work. Keep your hands busy. Don't think.",
                [
                    new EventResult("You find small tasks. Organize supplies. Keep moving.", 0.60, 30)
                        .WithEffects(EffectFactory.Focused(0.1, 45)),
                    new EventResult("It works for now. But it'll come back.", 0.40, 25)
                ])
            .Choice("Push It Down",
                "Not now. Not ever. Bury it.",
                [
                    new EventResult("You shove it deep. The thoughts recede.", 0.50, 5),
                    new EventResult("It doesn't stay down. Comes back worse.", 0.35, 5)
                        .Escalate("Disturbed", 0.1),
                    new EventResult("Something hardens inside you. Not healthy, but effective.", 0.15, 5)
                        .Escalate("Disturbed", -0.15)
                        .WithEffects(EffectFactory.Hardened(0.1, 120))
                ]);
    }
}
