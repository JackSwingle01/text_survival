using text_survival.Actions.Variants;
using text_survival.Bodies;
using text_survival.Effects;
using text_survival.Environments.Features;
using text_survival.Items;

namespace text_survival.Actions;

public static partial class GameEventRegistry
{
    // === EXPEDITION EVENTS (Travel Hazards + Discovery) ===

    private static GameEvent TreacherousFooting(GameContext ctx)
    {
        // Pre-select variants for consistent injury targeting across outcomes
        var terrainVariant = VariantSelector.SelectTerrainVariant(ctx);
        var slipVariant = VariantSelector.SelectSlipVariant(ctx);
        var sprainVariant = VariantSelector.SelectSprainVariant(ctx);

        return new GameEvent("Treacherous Footing",
            "The ground ahead looks unstable — ice beneath the snow, or loose rocks hidden by debris.", 1.0)
            .Requires(EventCondition.Traveling, EventCondition.HazardousTerrain) // Only triggers on hazardous terrain
            // Vulnerable: injured, slow, impaired, or unarmed - physical compromise makes hazards worse
            .WithSituationFactor(Situations.Vulnerable, 1.5)
            .Choice("Test Carefully",
                "You probe ahead with each step, testing your weight before committing.",
                [
                    new EventResult("You find a safe path through.", 0.90, 13),
                    new EventResult(terrainVariant.Description, 0.10, 15)
                        .DamageWithVariant(terrainVariant)
                ])
            .Choice("Go Around",
                "You backtrack and find another route entirely.",
                [
                    new EventResult("The detour costs time, but you avoid the hazard completely.", 1.0, 18)
                ])
            .Choice("Push Through",
                "No time for caution. You move quickly across.",
                [
                    new EventResult("You make it through without incident.", 0.5, 0),
                    new EventResult(slipVariant.Description, 0.3, 5)
                        .DamageWithVariant(slipVariant),
                    new EventResult(sprainVariant.Description, 0.2, 10)
                        .DamageWithVariant(sprainVariant)
                ]);
    }

    private static GameEvent SomethingCatchesYourEye(GameContext ctx)
    {
        var campDiscovery = DiscoverySelector.SelectCampDiscovery(ctx);

        return new GameEvent("Something Catches Your Eye",
            "Movement in your peripheral vision — or was it just a shape that doesn't belong? Something about the landscape ahead seems worth a closer look.", 1.5)
            .Requires(EventCondition.Working, EventCondition.IsExpedition)
            .WithConditionFactor(EventCondition.HighVisibility, 2.0)       // Spot things easier in open terrain
            .WithConditionFactor(EventCondition.JustRevealedLocation, 3.0) // Just revealed new places - scouting pays off
            .Choice("Investigate",
                "You set aside your current task and move closer to examine what you saw.",
                [
                    new EventResult("Nothing. Just shadows and your imagination.", 0.4, 12),
                    new EventResult("You find some useful materials partially buried in the snow.", 0.35, 15)
                        .FindsSupplies(),
                    new EventResult(campDiscovery.Description, 0.15, 20)
                        .WithDiscovery(campDiscovery),
                    new EventResult("A cache, deliberately hidden. Whoever left this isn't coming back.", 0.1, 10)
                        .FindsCache()
                ])
            .Choice("Mark It For Later",
                "You note the location but stay focused on your current task.",
                [
                    new EventResult("You file it away mentally and return to work.", 1.0, 2)
                        .CreateTension("MarkedDiscovery", 0.5, description: "something interesting")
                ])
            .Choice("Ignore It",
                "Probably nothing. You have work to do.",
                [
                    new EventResult("You push the distraction from your mind and continue.", 1.0, 0)
                ]);
    }

    private static GameEvent MinorAccident(GameContext ctx)
    {
        // Select a contextual injury variant - text and targeting will match
        var variant = VariantSelector.SelectAccidentVariant(ctx);
        var partName = BodyTargetResolver.GetDisplayName(variant.Target);

        return new GameEvent("Minor Accident", variant.Description, 0.8)
            .Requires(EventCondition.IsExpedition)
            // Vulnerable: injured, slow, impaired, or unarmed - compromised physical state leads to more accidents
            .WithSituationFactor(Situations.Vulnerable, 1.5)
            .WithConditionFactor(EventCondition.HazardousTerrain, 1.5) // More accidents on bad terrain
            .Choice("Stop and Assess",
                "You take a moment to examine the injury and tend to it properly.",
                [
                    new EventResult("It's minor — just a scrape. You clean it and move on.", 0.6, 5),
                    new EventResult($"A cut on your {partName}. You bind it to prevent worse.", 0.3, 8)
                        .DamageWithVariant(variant),
                    new EventResult("You've twisted something. Rest helps, but it'll slow you down.", 0.1, 12)
                        .DamageWithVariant(VariantSelector.SelectSprainVariant(ctx))
                ])
            .Choice("Push On",
                "No time to worry about it. Keep moving.",
                [
                    new EventResult("You ignore it. Probably fine.", 0.5, 0)
                        .DamageWithVariant(variant),
                    new EventResult("You try to ignore it, but it's affecting your movement.", 0.35, 0)
                        .DamageWithVariant(variant),
                    new EventResult("Ignoring it was a mistake. It's getting worse.", 0.15, 0)
                        .DamageWithVariant(VariantSelector.SelectSprainVariant(ctx))
                ])
            .Choice("Head Back",
                "This might be serious. Better to return to camp.",
                [
                    new EventResult("You turn back, favoring the injury.", 1.0)
                        .DamageWithVariant(variant)
                        .Aborts()
                ]);
    }

    // === DISCOVERY EVENTS ===

    private static GameEvent GlintInAshes(GameContext ctx)
    {
        return new GameEvent("Glint in the Ashes",
            "A glint catches your eye in the ash pile. Something half-buried.", 0.5)
            .Requires(EventCondition.AtCamp, EventCondition.FireBurning, EventCondition.IsCampWork)
            .Choice("Dig Carefully",
                "Probe through the ashes slowly.",
                [
                    new EventResult("A usable stone tool, fire-hardened.", 0.65, 5)
                        .Rewards(RewardPool.CraftingMaterials),
                    new EventResult("Bone fragment. Useful for crafting.", 0.20, 5)
                        .Rewards(RewardPool.BoneHarvest),
                    new EventResult("Nothing but charred debris.", 0.10, 5),
                    new EventResult("Cut yourself on something sharp.", 0.05, 5)
                        .Damage(3, DamageType.Sharp)
                ])
            .Choice("Stir the Ashes",
                "Quick search, less careful.",
                [
                    new EventResult("Charred tinder. Still usable.", 0.45, 3)
                        .Rewards(RewardPool.TinderBundle),
                    new EventResult("Nothing useful.", 0.40, 3),
                    new EventResult("Scatter embers. Minor hazard.", 0.15, 5)
                        .Damage(1, DamageType.Burn)
                        .WithEffects(EffectFactory.Burn(0.1, 30))
                ])
            .Choice("Ignore",
                "Probably nothing important.",
                [
                    new EventResult("You leave it buried.", 1.0, 0)
                ]);
    }

    private static GameEvent OldCampsite(GameContext ctx)
    {
        return new GameEvent("Old Campsite",
            "Signs of a previous camp. Fire ring, flattened snow. Someone survived here. For a while.", 0.6)
            .Requires(EventCondition.Working, EventCondition.IsExpedition)
            .Choice("Search Thoroughly",
                "Take your time. Check everything.",
                [
                    new EventResult("Picked clean. Wasted effort.", 0.28, 20),
                    new EventResult("Some scraps they couldn't carry.", 0.20, 25)
                        .Rewards(RewardPool.AbandonedCamp),
                    new EventResult("Signs of struggle. Blood in snow. You take what's left.", 0.15, 20)
                        .FindsSupplies()
                        .Unsettling()
                        .CreateTension("Disturbed", 0.2, description: "signs of violence"),
                    new EventResult("A cache they hid and never returned for.", 0.12, 30)
                        .FindsCache(),
                    new EventResult("You find remains. Human. You take their gear.", 0.10, 15)
                        .Rewards(RewardPool.AbandonedCamp)
                        .Panicking()
                        .CreateTension("Disturbed", 0.5, ctx.CurrentLocation, description: "human remains"),
                    new EventResult("Bones, scattered by scavengers. Torn clothing.", 0.08, 18)
                        .Terrifying()
                        .CreateTension("Disturbed", 0.35, ctx.CurrentLocation, description: "scattered bones"),
                    new EventResult("Something is still here. Watching. You leave fast.", 0.07, 10)
                        .Frightening()
                        .BecomeStalked(0.3)
                        .Aborts()
                ])
            .Choice("Scavenge Fast",
                "Quick look, don't linger.",
                [
                    new EventResult("Fire ring has usable charcoal.", 0.40, 8)
                        .Rewards(RewardPool.TinderBundle),
                    new EventResult("Nothing obvious. At least you didn't waste much time.", 0.35, 8),
                    new EventResult("A few sticks they left stacked.", 0.20, 10)
                        .FindsSupplies(),
                    new EventResult("Cut yourself on hidden debris.", 0.05, 8)
                        .Damage(4, DamageType.Sharp)
                        .CreateTension("WoundUntreated", 0.2, description: "hand")
                ])
            .Choice("Keep Distance",
                "Something feels wrong. Move on.",
                [
                    new EventResult("You skirt the site. Probably nothing useful anyway.", 0.70, 5),
                    new EventResult("Circle wide. Costs time but feels safer.", 0.20, 12),
                    new EventResult("Something about the site sticks with you.", 0.10, 5)
                        .WithEffects(EffectFactory.Shaken(0.15))
                ]);
    }

    private static GameEvent WaterSource(GameContext ctx)
    {
        return new GameEvent("Water Source",
            "You hear it before you see it — running water, or the promising crack of ice over a stream.", 0.7)
            .Requires(EventCondition.Working, EventCondition.Outside, EventCondition.IsExpedition, EventCondition.NotNearWater)
            .Choice("Investigate Thoroughly",
                "Check the source carefully.",
                [
                    new EventResult("Fresh water source. You fill up.", 0.50, 20)
                        .Rewards(RewardPool.WaterSource),
                    new EventResult("Water AND game trails. Animals need water too.", 0.20, 25)
                        .Rewards(RewardPool.WaterSource),
                    new EventResult("Thin ice. You break through.", 0.15, 15)
                        .ModerateFall()
                        .WithEffects(EffectFactory.Cold(-15, 45), EffectFactory.Wet(0.6)),
                    new EventResult("Contaminated or mineral. Not drinkable.", 0.10, 20),
                    new EventResult("Perfect source. Clear, accessible, sheltered.", 0.05, 25)
                        .Rewards(RewardPool.WaterSource)
                ])
            .Choice("Drink Now",
                "You're thirsty. Drink first, ask questions later.",
                [
                    new EventResult("Safe. Immediate relief.", 0.70, 5)
                        .Rewards(RewardPool.WaterSource),
                    new EventResult("Questionable. Drink anyway.", 0.20, 5)
                        .Rewards(RewardPool.WaterSource)
                        .WithEffects(EffectFactory.Nauseous(0.2, 60)),
                    new EventResult("Bad water. Immediate regret.", 0.10, 5)
                        .WithEffects(EffectFactory.Nauseous(0.5, 120))
                ])
            .Choice("Mark and Continue",
                "Note the location for later.",
                [
                    new EventResult("You mark a reliable water source — a frozen stream you can return to.", 0.25, 3)
                        .AddsFeature(typeof(WaterFeature), (0.4, 0.6, 0.8)),
                    new EventResult("You find a small frozen puddle worth remembering.", 0.75, 3)
                        .AddsDetail(EnvironmentalDetail.FrozenPuddle)
                ]);
    }

    private static GameEvent UnexpectedYield(GameContext ctx)
    {
        var materialDiscovery = DiscoverySelector.SelectMaterialDiscovery(ctx);
        var tinderDiscovery = DiscoverySelector.SelectTinderDiscovery(ctx);
        var boneDiscovery = DiscoverySelector.SelectBoneDiscovery(ctx);
        var hideDiscovery = DiscoverySelector.SelectHideDiscovery(ctx);

        return new GameEvent("Unexpected Yield",
            "As you work, you notice something useful you almost missed.", 0.8)
            .Requires(EventCondition.Working, EventCondition.IsExpedition)
            .WithSituationFactor(Situations.IsFollowingAnimalSigns, 1.8) // Following tracks leads to finds
            .WithSituationFactor(Situations.HasFreshTrail, 1.3)          // Examined signs heighten awareness
            .Choice("Take It",
                "A lucky find.",
                [
                    new EventResult(materialDiscovery.Description, 0.50, 5)
                        .WithDiscovery(materialDiscovery),
                    new EventResult(tinderDiscovery.Description, 0.25, 3)
                        .WithDiscovery(tinderDiscovery),
                    new EventResult(boneDiscovery.Description, 0.15, 5)
                        .WithDiscovery(boneDiscovery),
                    new EventResult(hideDiscovery.Description, 0.10, 5)
                        .WithDiscovery(hideDiscovery)
                ])
            .Choice("Leave It",
                "You're focused. Stay on task.",
                [
                    new EventResult("You ignore the distraction and keep working.", 1.0, 0)
                ]);
    }

    private static GameEvent TrailGoesCold(GameContext ctx)
    {
        return new GameEvent("Trail Goes Cold",
            "The tracks you were following scatter and disappear. Whatever was here is gone.", 0.6)
            .Requires(EventCondition.Working, EventCondition.IsExpedition)
            .RequiresSituation(Situations.IsFollowingAnimalSigns)
            .WithSituationFactor(Situations.ResourceScarcity, 2.5)  // Much more likely in depleted areas
            .WithConditionFactor(EventCondition.LowVisibility, 1.5) // Harder to track in poor visibility
            .Choice("Keep Searching",
                "There might still be something here.",
                [
                    new EventResult("Nothing. The area's picked clean.", 0.60, 15),
                    new EventResult("Old scat, scattered bones. Nothing fresh.", 0.25, 12)
                        .FindsSupplies(),
                    new EventResult("You find a small cache — something was storing food here.", 0.15, 20)
                        .Rewards(RewardPool.SmallGame)
                ])
            .Choice("Move On",
                "Don't waste more time on dead ends.",
                [
                    new EventResult("You've learned to read the signs better for next time.", 1.0, 5)
                ]);
    }

    private static GameEvent ExposedPosition(GameContext ctx)
    {
        return new GameEvent("Exposed Position",
            "You've wandered into an exposed area. Wind cuts through you. No cover nearby.", 0.9)
            .Requires(EventCondition.IsExpedition, EventCondition.Outside, EventCondition.HighWind)
            .WithConditionFactor(EventCondition.HighVisibility, 1.3) // Worse when fully exposed
            .Choice("Move Fast",
                "Push through quickly. Minimize exposure time.",
                [
                    new EventResult("Through it quickly. Cold but moving.", 0.45, 5)
                        .WithCold(-10, 20),
                    new EventResult("Longer than expected. Wind brutal.", 0.30, 10)
                        .WithCold(-15, 30),
                    new EventResult("Stumble in wind. Fall.", 0.15, 8)
                        .Damage(4, DamageType.Blunt)
                        .ModerateCold(),
                    new EventResult("Wind knocks you down. Disoriented.", 0.10, 15)
                        .ModerateFall()
                        .SevereCold()
                ])
            .Choice("Find Cover First",
                "Look for any windbreak before crossing.",
                [
                    new EventResult("Small windbreak. Helps.", 0.50, 12)
                        .MinorCold(),
                    new EventResult("Nothing useful. Wasted time in wind.", 0.30, 15)
                        .ModerateCold(),
                    new EventResult("Decent hollow. Warm up before continuing.", 0.15, 18)
                        .LightChill(),
                    new EventResult("'Shelter' funnels wind. Worse than open.", 0.05, 10)
                        .DangerousCold()
                ])
            .Choice("Emergency Fire",
                "Stop and make a fire. Desperate but might work.",
                [
                    new EventResult("Fire catches. Warmth floods back.", 0.45, 15)
                        .BurnsFuel(2),
                    new EventResult("Wind makes it hard. Uses more fuel.", 0.30, 20)
                        .BurnsFuel(3)
                        .MinorCold(),
                    new EventResult("Won't catch. Wasted fuel.", 0.15, 12)
                        .BurnsFuel(2)
                        .ModerateCold(),
                    new EventResult("Sparks in wind. Burn yourself.", 0.10, 10)
                        .BurnsFuel(2)
                        .Damage(3, DamageType.Burn)
                        .WithEffects(EffectFactory.Burn(0.2, 45))
                ],
                requires: [EventCondition.HasFuelPlenty])
            .Choice("Turn Back",
                "Retreat the way you came.",
                [
                    new EventResult("Backtracking costs time but avoids worst.", 0.70, 8)
                        .WithCold(-6, 15),
                    new EventResult("Way back harder than remembered.", 0.20, 12)
                        .WithCold(-10, 25),
                    new EventResult("Get turned around. Lost time, cold.", 0.10, 15)
                        .HarshCold()
                ]);
    }

    private static GameEvent NaturalShelterSpotted(GameContext ctx)
    {
        return new GameEvent("Natural Shelter Spotted",
            "A defensible overhang. A dense thicket. A hollow in the hillside. Natural shelter, if improved.", 0.5)
            .Requires(EventCondition.Working, EventCondition.Outside, EventCondition.IsExpedition, EventCondition.NoShelter)
            .Choice("Improve It Now",
                "Spend time making this usable shelter.",
                [
                    new EventResult("Solid work. This is shelter now.", 0.60, 45)
                        .BurnsFuel(3)
                        .WithEffects(EffectFactory.Focused(0.15, 60))
                        .AddsShelter(temp: 0.4, overhead: 0.5, wind: 0.6),
                    new EventResult("Takes longer but done right.", 0.25, 60)
                        .BurnsFuel(4)
                        .WithEffects(EffectFactory.Exhausted(0.2, 60))
                        .AddsShelter(temp: 0.5, overhead: 0.6, wind: 0.7),
                    new EventResult("Not as good as hoped. Partial shelter.", 0.10, 40)
                        .BurnsFuel(2)
                        .AddsShelter(temp: 0.2, overhead: 0.3, wind: 0.4),
                    new EventResult("Collapses during construction. Wasted effort.", 0.05, 30)
                        .BurnsFuel(3)
                        .MinorFall()
                        .Shaken()
                ],
                requires: [EventCondition.HasFuel])
            .Choice("Note for Later",
                "Mark the location mentally. Could develop it later.",
                [
                    new EventResult("You note the spot and continue. Good to know it's here.", 1.0, 5)
                ])
            .Choice("Use Temporarily",
                "Take advantage of natural cover for now.",
                [
                    new EventResult("Brief respite from the elements. Helpful.", 0.70, 10)
                        .WithEffects(EffectFactory.Warmed(0.2, 30)),
                    new EventResult("More protection than expected. You rest briefly.", 0.30, 15)
                        .WithEffects(EffectFactory.Rested(0.4, 45))
                ])
            .Choice("Ignore",
                "Not worth the time. Keep moving.",
                [
                    new EventResult("You move on. Maybe you'll remember where this was.", 1.0, 0)
                ]);
    }

    private static GameEvent Debris(GameContext ctx)
    {
        return new GameEvent("Debris",
            "Debris washed up or blown in — wood, branches, maybe more.", 0.6)
            .Requires(EventCondition.Working, EventCondition.Outside, EventCondition.IsExpedition, EventCondition.IsRaining)
            .WithConditionFactor(EventCondition.IsStormy, 1.5)
            .Choice("Haul It In",
                "Gather what you can carry.",
                [
                    new EventResult("Good fuel. Worth the effort.", 0.60, 15)
                        .FindsSupplies(),
                    new EventResult("Mixed quality. Some usable.", 0.25, 15)
                        .FindsSupplies(),
                    new EventResult("Excellent find. Dry, quality wood.", 0.10, 12)
                        .Rewards(RewardPool.AbandonedCamp),
                    new EventResult("Something else caught in the debris.", 0.05, 10)
                        .FindsCache()
                ])
            .Choice("Check for More",
                "Investigate the source. Might be recurring.",
                [
                    new EventResult("Find the source — debris field. Good to know.", 0.30, 30)
                        .FindsSupplies(),
                    new EventResult("Just this batch. Good haul though.", 0.50, 20)
                        .FindsSupplies(),
                    new EventResult("Nothing more. At least you got some.", 0.20, 25)
                        .FindsSupplies()
                ])
            .Choice("Leave It",
                "Not worth the effort right now.",
                [
                    new EventResult("You move on. Fuel is everywhere... right?", 1.0, 0)
                ]);
    }

    // === DISTURBED ARC EXPEDITION EVENTS ===

    private static GameEvent IntrusiveThought(GameContext ctx)
    {
        var disturbedTension = ctx.Tensions.GetTension("Disturbed");
        var source = disturbedTension?.Description ?? "what you saw";

        return new GameEvent("Intrusive Thought",
            $"Your hands stop. Your mind fills with {source}. You can't look away from what isn't there.", 1.2)
            .Requires(EventCondition.Working, EventCondition.Disturbed)
            // PsychologicallyCompromised: disturbed states + stalked tension compound psychological pressure
            .WithSituationFactor(Situations.PsychologicallyCompromised, 2.5)
            .Choice("Push Through",
                "Force yourself back to the present. Focus on your hands, the cold, anything real.",
                [
                    new EventResult("You ground yourself. It passes.", 0.45, 10)
                        .Shaken(),
                    new EventResult("Takes longer than you'd like. But you push through.", 0.30, 20)
                        .WithEffects(EffectFactory.Exhausted(0.2, 60)),
                    new EventResult("You can't shake it. Work continues, but slowly.", 0.20, 30)
                        .WithEffects(EffectFactory.Paranoid(0.2))
                        .Escalate("Disturbed", 0.1),
                    new EventResult("Something breaks loose. You feel... clearer.", 0.05, 15)
                        .Escalate("Disturbed", -0.15)
                        .WithEffects(EffectFactory.Focused(0.15, 60))
                ])
            .Choice("Stop and Breathe",
                "Don't fight it. Let it pass through you.",
                [
                    new EventResult("You sit with it. The feeling passes, leaving you drained.", 0.50, 25)
                        .WithEffects(EffectFactory.Exhausted(0.15, 45))
                        .Escalate("Disturbed", -0.05),
                    new EventResult("Time slips away. When you come back, an hour has passed.", 0.30, 60)
                        .WithEffects(EffectFactory.Cold(-10, 40)),
                    new EventResult("The cold brings you back. Hard to ignore your body's demands.", 0.20, 40)
                        .WithEffects(EffectFactory.Cold(-15, 50))
                ])
            .Choice("Return to Camp",
                "Not today. Go back.",
                [
                    new EventResult("Some days you can't do this. Today is one of them.", 1.0, 5)
                        .Aborts()
                ]);
    }

    private static GameEvent LostTime(GameContext ctx)
    {
        return new GameEvent("Lost Time",
            "You blink. The shadows have moved. How long were you standing there, staring at nothing?", 0.6)
            .Requires(EventCondition.Working, EventCondition.DisturbedHigh)
            // PsychologicallyCompromised: disturbed states + stalked tension - severe psychological compromise causes dissociation
            .WithSituationFactor(Situations.PsychologicallyCompromised, 2.0)
            .Choice("Take Stock",
                "Check yourself. Check your surroundings. What happened?",
                [
                    new EventResult("Maybe 30 minutes. Lost in thought. At least you're warm enough.", 0.50, 30)
                        .Shaken(),
                    new EventResult("An hour, at least. You're colder than you should be.", 0.30, 60)
                        .WithCold(-12, 40),
                    new EventResult("The sun has moved significantly. This is concerning.", 0.15, 90)
                        .DangerousCold()
                        .WithEffects(EffectFactory.Paranoid(0.25)),
                    new EventResult("You... worked? Your pack has more resources than before.", 0.05, 45)
                        .FindsSupplies()
                        .WithEffects(EffectFactory.Shaken(0.3))
                        .Escalate("Disturbed", 0.15)
                ]);
    }

    private static GameEvent FacingTheSource(GameContext ctx)
    {
        var disturbedTension = ctx.Tensions.GetTension("Disturbed");
        var source = disturbedTension?.Description ?? "what happened here";

        return new GameEvent("Facing the Source",
            $"You're back. Where you found {source}. Your heart pounds, but you're here.", 3.0)
            .Requires(EventCondition.Working, EventCondition.AtDisturbedSource)
            .Choice("Bury the Remains",
                "Give them what peace you can. Mark the place.",
                [
                    new EventResult("You work in silence. When it's done, something feels lighter.", 0.70, 45)
                        .ResolveTension("Disturbed")
                        .WithEffects(EffectFactory.Exhausted(0.2, 60), EffectFactory.Focused(0.2, 120)),
                    new EventResult("You bury what you can. It's not enough, but it's something.", 0.25, 60)
                        .Escalate("Disturbed", -0.4)
                        .WithEffects(EffectFactory.Exhausted(0.3, 90)),
                    new EventResult("Halfway through, you have to stop. Can't do this.", 0.05, 30)
                        .Frightening()
                ])
            .Choice("Search for Answers",
                "Understand what happened. Knowledge might help.",
                [
                    new EventResult("You piece together what you can. A story emerges. It ends badly.", 0.50, 30)
                        .Escalate("Disturbed", -0.25)
                        .Shaken(),
                    new EventResult("You find something useful they left behind.", 0.30, 25)
                        .Rewards(RewardPool.AbandonedCamp)
                        .Escalate("Disturbed", -0.2),
                    new EventResult("The more you learn, the worse it gets.", 0.15, 35)
                        .Escalate("Disturbed", 0.15)
                        .Terrifying(),
                    new EventResult("You find their journal. Their last entry is... helpful somehow.", 0.05, 40)
                        .ResolveTension("Disturbed")
                        .WithEffects(EffectFactory.Hardened(0.2, 180))
                ])
            .Choice("Just Pass Through",
                "You're here. That's enough. Move on.",
                [
                    new EventResult("You stand there for a while. Then you continue.", 0.60, 10)
                        .Escalate("Disturbed", -0.15),
                    new EventResult("Returning was the hard part. Leaving is easier.", 0.30, 5)
                        .Escalate("Disturbed", -0.2)
                        .WithEffects(EffectFactory.Hardened(0.1, 90)),
                    new EventResult("You can't stay. You leave quickly.", 0.10, 3)
                        .Unsettling()
                ]);
    }

    // === LOCATION CONDITION EVENTS ===

    private static GameEvent DarkPassage(GameContext ctx)
    {
        // Pre-select darkness-specific injury variants
        var stumbleVariant = VariantSelector.SelectStumbleVariant(ctx);
        var fallVariant = VariantSelector.SelectSlipVariant(ctx);
        var sprainVariant = VariantSelector.SelectSprainVariant(ctx);

        return new GameEvent("Dark Passage",
            "The path ahead disappears into darkness. Without light, every step is a gamble.", 0.9)
            .Requires(EventCondition.Traveling)
            // InDarkness: night or lack of light source - darkness removes visual control
            .WithSituationFactor(Situations.InDarkness, 2.0)
            .Choice("Proceed Carefully",
                "Feel your way forward. Slow, deliberate steps.",
                [
                    new EventResult("You inch forward, testing each foothold. Slow but safe.", 0.60, 20),
                    new EventResult("Your foot finds empty air. You catch yourself just in time.", 0.25, 25)
                        .Unsettling(),
                    new EventResult(stumbleVariant.Description, 0.10, 18)
                        .DamageWithVariant(stumbleVariant),
                    new EventResult("Your hand brushes something useful in the dark.", 0.05, 22)
                        .FindsSupplies()
                ])
            .Choice("Wait for Light",
                "Don't risk it. Wait until conditions improve.",
                [
                    new EventResult("You wait. Time passes, but you're unharmed.", 0.70, 45),
                    new EventResult("The wait stretches. Cold seeps in.", 0.20, 60)
                        .WithCold(-8, 30),
                    new EventResult("Something stirs in the darkness nearby. You freeze.", 0.10, 50)
                        .BecomeStalked(0.25)
                ])
            .Choice("Rush Through",
                "Move fast. Minimize time in the dark.",
                [
                    new EventResult("You make it through. Heart pounding, but intact.", 0.40, 8),
                    new EventResult(fallVariant.Description, 0.30, 10)
                        .DamageWithVariant(fallVariant),
                    new EventResult(sprainVariant.Description, 0.20, 12)
                        .DamageWithVariant(sprainVariant),
                    new EventResult("You run straight into something solid. Stars explode.", 0.10, 15)
                        .SeriousFall()
                        .WithEffects(EffectFactory.Shaken(0.3))
                ]);
    }

    private static GameEvent WaterCrossing(GameContext ctx)
    {
        // Pre-select slip variant for ice/water hazards
        var slipVariant = VariantSelector.SelectSlipVariant(ctx);

        return new GameEvent("Water Crossing",
            "Water blocks your path. Moving water, or still water with thin ice at the edges.", 0.8)
            .Requires(EventCondition.NearWater, EventCondition.Traveling)
            .Choice("Wade Across",
                "Straight through. Get wet, get it over with.",
                [
                    new EventResult("Cold but quick. You're through.", 0.50, 8)
                        .WithEffects(EffectFactory.Wet(0.5), EffectFactory.Cold(-10, 30)),
                    new EventResult("Deeper than expected. Soaked to the waist.", 0.30, 12)
                        .WithEffects(EffectFactory.Wet(0.8), EffectFactory.Cold(-15, 45)),
                    new EventResult("Current stronger than it looked. You fight to stay upright.", 0.15, 15)
                        .WithEffects(EffectFactory.Wet(0.9), EffectFactory.Cold(-18, 60), EffectFactory.Exhausted(0.2, 30)),
                    new EventResult("You slip. Water closes over your head for a terrifying moment.", 0.05, 18)
                        .DamageWithVariant(slipVariant)
                        .WithEffects(EffectFactory.Wet(1.0), EffectFactory.Cold(-25, 90))
                        .Frightening()
                ])
            .Choice("Find Another Route",
                "Look for a better crossing point.",
                [
                    new EventResult("You find a narrow point. Easy crossing.", 0.40, 20),
                    new EventResult("Long detour but you stay dry.", 0.35, 30),
                    new EventResult("No good options. You end up crossing anyway.", 0.20, 25)
                        .WithEffects(EffectFactory.Wet(0.5), EffectFactory.Cold(-10, 30)),
                    new EventResult("The detour reveals something interesting.", 0.05, 25)
                        .FindsSupplies()
                ])
            .Choice("Drink First",
                "You're here. Might as well hydrate.",
                [
                    new EventResult("Fresh and cold. You drink your fill before crossing.", 0.80, 10)
                        .Rewards(RewardPool.WaterSource),
                    new EventResult("Water tastes off. You drink anyway, then cross.", 0.15, 12)
                        .Rewards(RewardPool.WaterSource)
                        .WithEffects(EffectFactory.Nauseous(0.15, 45)),
                    new EventResult("Ice breaks. You're in the water.", 0.05, 15)
                        .DamageWithVariant(slipVariant)
                        .WithEffects(EffectFactory.Wet(0.9))
                        .SevereCold()
                ]);
    }

    private static GameEvent ExposedOnRidge(GameContext ctx)
    {
        // Pre-select variants for ridge hazards
        var stumbleVariant = VariantSelector.SelectTerrainVariant(ctx);
        var sharpVariant = AccidentVariants.SharpHazards[Random.Shared.Next(AccidentVariants.SharpHazards.Length)];
        var partName = BodyTargetResolver.GetDisplayName(sharpVariant.Target);

        return new GameEvent("Exposed on the Ridge",
            "The terrain opens up. You can see for miles — which means anything for miles can see you.", 1.0)
            .Requires(EventCondition.HighVisibility, EventCondition.HazardousTerrain)
            .WithConditionFactor(EventCondition.HighWind, 1.5)
            .WithSituationFactor(Situations.RemoteAndVulnerable, 1.5)  // Far + weak = worse exposure
            .Choice("Move Fast",
                "Minimize your exposure time. Speed across.",
                [
                    new EventResult("You hustle across. Wind buffets you, but you make it.", 0.45, 10)
                        .WithCold(-8, 20),
                    new EventResult(stumbleVariant.Description, 0.30, 15)
                        .DamageWithVariant(stumbleVariant)
                        .WithCold(-10, 25),
                    new EventResult("Wind nearly takes you. You drop and crawl.", 0.15, 20)
                        .HarshCold()
                        .WithEffects(EffectFactory.Exhausted(0.2, 45)),
                    new EventResult("Something saw you. Movement on the horizon.", 0.10, 12)
                        .BecomeStalked(0.3)
                ])
            .Choice("Low Crawl",
                "Stay low, stay hidden. Use what cover exists.",
                [
                    new EventResult("Slow but safe. You cross without being spotted.", 0.55, 25)
                        .WithEffects(EffectFactory.Sore(0.2, 60)),
                    new EventResult("Rocks tear at your clothing. Cold seeps in.", 0.30, 30)
                        .ModerateCold(),
                    new EventResult($"Your {partName} finds a sharp edge. Blood makes the rocks slick.", 0.10, 28)
                        .DamageWithVariant(sharpVariant)
                        .CreateTension("WoundUntreated", 0.15, description: partName),
                    new EventResult("You spot something from this vantage point.", 0.05, 25)
                        .Rewards(RewardPool.CraftingMaterials)
                ])
            .Choice("Turn Back",
                "Not worth the risk. Find another way.",
                [
                    new EventResult("You retreat. Safer, but time lost.", 0.70, 15),
                    new EventResult("The way back is harder than you remembered.", 0.25, 25)
                        .WithEffects(EffectFactory.Sore(0.15, 45)),
                    new EventResult("As you turn, you spot tracks. Something uses this ridge.", 0.05, 10)
                        .BecomeStalked(0.2)
                ]);
    }

    private static GameEvent AmbushOpportunity(GameContext ctx)
    {
        var territory = ctx.CurrentLocation.GetFeature<AnimalTerritoryFeature>();
        var animal = territory?.GetRandomAnimalName() ?? "animal";

        return new GameEvent("Ambush Opportunity",
            $"Dense cover conceals you. Fresh {animal.ToLower()} sign everywhere. Perfect hunting ground — or perfect place for something to hunt YOU.", 0.8)
            .Requires(EventCondition.LowVisibility, EventCondition.InAnimalTerritory, EventCondition.Working)
            .WithSituationFactor(Situations.HuntingAdvantage, 1.5)  // Weapon + stealth = better odds
            .WithSituationFactor(Situations.GoodForStealth, 1.3)  // Good conditions amplify opportunity
            .Choice("Set Up an Ambush",
                "Use this cover to your advantage. Wait for prey.",
                [
                    new EventResult($"A {animal.ToLower()} wanders into your kill zone. Clean shot.", 0.35, 45)
                        .FindsMeat(),
                    new EventResult("Nothing comes. Wasted time, but you're well-positioned now.", 0.30, 40),
                    new EventResult("Something comes — too big. You let it pass.", 0.20, 50)
                        .Unsettling()
                        .BecomeStalked(0.2),
                    new EventResult("Something was hunting YOU. It pounces.", 0.10, 30)
                        .Encounter(territory?.GetRandomPredatorName() ?? "Wolf", 10, 0.7)
                        .Aborts(),
                    new EventResult("Perfect shot. Quality kill.", 0.05, 55)
                        .FindsLargeMeat()
                ])
            .Choice("Forage the Area",
                "Good cover means good foraging. Work quickly.",
                [
                    new EventResult("Dense vegetation hides useful materials.", 0.50, 25)
                        .FindsSupplies(),
                    new EventResult("You find some things, but something's watching.", 0.25, 20)
                        .Rewards(RewardPool.CraftingMaterials)
                        .BecomeStalked(0.2),
                    new EventResult("The undergrowth scratches and tears. Minor wounds.", 0.15, 22)
                        .FindsSupplies()
                        .Damage(2, DamageType.Sharp),
                    new EventResult("You disturb a nest. Something angry emerges.", 0.10, 15)
                        .Encounter(territory?.GetRandomAnimalName() ?? "Fox", 15, 0.4)
                ])
            .Choice("Move Through Quickly",
                "Don't linger. This place favors predators.",
                [
                    new EventResult("You slip through the cover. Unseen, unharmed.", 0.60, 10),
                    new EventResult("Something follows your movement. You feel eyes on you.", 0.25, 12)
                        .BecomeStalked(0.25),
                    new EventResult("You startle something. It bolts — away from you, thankfully.", 0.15, 8)
                ]);
    }

    private static GameEvent DistantSmoke(GameContext ctx)
    {
        return new GameEvent("Distant Smoke",
            "From this vantage, you can see farther. There — in the distance. Smoke. A thin column rising through still air. Too controlled to be wildfire. Another camp? Another survivor?", 0.9)
            .Requires(EventCondition.JustRevealedLocation, EventCondition.HighVisibility)
            .Choice("Mark the location",
                "You memorize the direction. It's far, but reachable.",
                [
                    new EventResult("You mark the bearing. The smoke is real — you know where to look now.", 1.0, 5)
                        .CreateTension("MarkedDiscovery", 0.4, description: "distant smoke column")
                ])
            .Choice("Keep moving",
                "Smoke could mean anything. Best to stay focused on what's in front of you.",
                [
                    new EventResult("You note it and move on. If it's real, it'll still be there.", 1.0, 0)
                ]);
    }

    private static GameEvent EdgeOfTheIce(GameContext ctx)
    {
        return new GameEvent("Edge of the Ice",
            "The forest ends abruptly. Ahead — nothing but white. A frozen lake stretches out, pristine and treacherous. The ice could hold. Or it could drop you into black water.", 0.8)
            .Requires(EventCondition.OnBoundary, EventCondition.SurroundedByWater)
            .WithConditionFactor(EventCondition.LowTemperature, 1.5)  // Thick ice makes crossing viable
            .Choice("Test the ice carefully",
                "Tap ahead with a stick. Listen for cracks.",
                [
                    new EventResult("The ice is thick. Safe enough to cross — if you're careful.", 0.6, 10),
                    new EventResult("Your test probe punches through. The ice is thin. One wrong step and you're in.", 0.4, 5)
                        .CreateTension("ThinIce", 0.4, description: "dangerous frozen water ahead")
                ])
            .Choice("Go around",
                "Not worth the risk. The long way is the safe way.",
                [
                    new EventResult("You skirt the edge. It costs time, but you keep your boots dry.", 1.0, 15)
                ])
            .Choice("Cross Now",
                "The ice looks thick enough. No time to waste testing.",
                [
                    new EventResult("You make it across. The ice holds. Faster route paid off.", 0.5, 8),
                    new EventResult("Halfway across, you hear cracks. You freeze. The ice holds... barely.", 0.3, 12)
                        .WithEffects(EffectFactory.Shaken(0.3)),
                    new EventResult("The ice gives way. You plunge into freezing water.", 0.2, 5)
                        .Damage(8, DamageType.Blunt)  // Impact from fall
                        .WithEffects(EffectFactory.Wet(0.8), EffectFactory.Cold(-12, 30))  // Completely soaked, severe cold
                        .Aborts()
                ]);
    }
}