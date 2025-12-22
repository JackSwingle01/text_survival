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
        return new GameEvent("Treacherous Footing",
            "The ground ahead looks unstable — ice beneath the snow, or loose rocks hidden by debris.", 1.0)
            .Requires(EventCondition.Traveling)
            .MoreLikelyIf(EventCondition.Injured, 1.5)
            .MoreLikelyIf(EventCondition.Slow, 1.3)
            .Choice("Test Carefully",
                "You probe ahead with each step, testing your weight before committing.",
                [
                    new EventResult("You find a safe path through.", 0.90, 13),
                    new EventResult("Despite your caution, the ground shifts. You stumble but catch yourself.", 0.10, 15)
                        .Damage(3, DamageType.Blunt, "fall")
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
                    new EventResult("Your foot breaks through. You wrench your leg free, bruised.", 0.3, 5)
                        .Damage(4, DamageType.Blunt, "fall"),
                    new EventResult("You slip hard. Pain shoots through your ankle.", 0.2, 10)
                        .WithEffects(EffectFactory.SprainedAnkle(0.5))
                ]);
    }

    private static GameEvent SomethingCatchesYourEye(GameContext ctx)
    {
        return new GameEvent("Something Catches Your Eye",
            "Movement in your peripheral vision — or was it just a shape that doesn't belong? Something about the landscape ahead seems worth a closer look.", 1.5)
            .Requires(EventCondition.Working)
            .Choice("Investigate",
                "You set aside your current task and move closer to examine what you saw.",
                [
                    new EventResult("Nothing. Just shadows and your imagination.", 0.4, 12),
                    new EventResult("You find some useful materials partially buried in the snow.", 0.35, 15)
                        .Rewards(RewardPool.BasicSupplies),
                    new EventResult("Signs of an old campsite. Someone was here before — they left in a hurry.", 0.15, 20)
                        .Rewards(RewardPool.AbandonedCamp),
                    new EventResult("A cache, deliberately hidden. Whoever left this isn't coming back.", 0.1, 10)
                        .Rewards(RewardPool.HiddenCache)
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
        return new GameEvent("Minor Accident",
            "Your foot catches on something hidden. A sharp pain as you stumble.", 0.8)
            .Requires(EventCondition.IsExpedition)
            .MoreLikelyIf(EventCondition.Injured, 1.4)
            .MoreLikelyIf(EventCondition.Slow, 1.3)
            .Choice("Stop and Assess",
                "You take a moment to examine the injury and tend to it properly.",
                [
                    new EventResult("It's minor — just a scrape. You clean it and move on.", 0.6, 5),
                    new EventResult("A small cut. You bind it to prevent worse.", 0.3, 8)
                        .Damage(2, DamageType.Sharp, "accident"),
                    new EventResult("You've twisted something. Rest helps, but it'll slow you down.", 0.1, 12)
                        .WithEffects(EffectFactory.SprainedAnkle(0.3))
                ])
            .Choice("Push On",
                "No time to worry about it. Keep moving.",
                [
                    new EventResult("You ignore it. Probably fine.", 0.5, 0)
                        .Damage(2, DamageType.Sharp, "accident"),
                    new EventResult("You try to ignore it, but it's affecting your movement.", 0.35, 0)
                        .Damage(3, DamageType.Sharp, "accident"),
                    new EventResult("Ignoring it was a mistake. It's getting worse.", 0.15, 0)
                        .WithEffects(EffectFactory.SprainedAnkle(0.4))
                ])
            .Choice("Head Back",
                "This might be serious. Better to return to camp.",
                [
                    new EventResult("You turn back, favoring the injury.", 1.0)
                        .Damage(2, DamageType.Sharp, "accident")
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
                        .Damage(3, DamageType.Sharp, "sharp debris")
                ])
            .Choice("Stir the Ashes",
                "Quick search, less careful.",
                [
                    new EventResult("Charred tinder. Still usable.", 0.45, 3)
                        .Rewards(RewardPool.TinderBundle),
                    new EventResult("Nothing useful.", 0.40, 3),
                    new EventResult("Scatter embers. Minor hazard.", 0.15, 5)
                        .Damage(1, DamageType.Burn, "ember burn")
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
            .Requires(EventCondition.Working)
            .Choice("Search Thoroughly",
                "Take your time. Check everything.",
                [
                    new EventResult("Picked clean. Wasted effort.", 0.28, 20),
                    new EventResult("Some scraps they couldn't carry.", 0.20, 25)
                        .Rewards(RewardPool.AbandonedCamp),
                    new EventResult("Signs of struggle. Blood in snow. You take what's left.", 0.15, 20)
                        .Rewards(RewardPool.BasicSupplies)
                        .WithEffects(EffectFactory.Fear(0.2))
                        .CreateTension("Disturbed", 0.2, description: "signs of violence"),
                    new EventResult("A cache they hid and never returned for.", 0.12, 30)
                        .Rewards(RewardPool.HiddenCache),
                    new EventResult("You find remains. Human. You take their gear.", 0.10, 15)
                        .Rewards(RewardPool.AbandonedCamp)
                        .WithEffects(EffectFactory.Fear(0.6))
                        .CreateTension("Disturbed", 0.5, ctx.CurrentLocation, description: "human remains"),
                    new EventResult("Bones, scattered by scavengers. Torn clothing.", 0.08, 18)
                        .WithEffects(EffectFactory.Fear(0.4))
                        .CreateTension("Disturbed", 0.35, ctx.CurrentLocation, description: "scattered bones"),
                    new EventResult("Something is still here. Watching. You leave fast.", 0.07, 10)
                        .WithEffects(EffectFactory.Fear(0.3))
                        .CreateTension("Stalked", 0.3)
                        .Aborts()
                ])
            .Choice("Scavenge Fast",
                "Quick look, don't linger.",
                [
                    new EventResult("Fire ring has usable charcoal.", 0.40, 8)
                        .Rewards(RewardPool.TinderBundle),
                    new EventResult("Nothing obvious. At least you didn't waste much time.", 0.35, 8),
                    new EventResult("A few sticks they left stacked.", 0.20, 10)
                        .Rewards(RewardPool.BasicSupplies),
                    new EventResult("Cut yourself on hidden debris.", 0.05, 8)
                        .Damage(4, DamageType.Sharp, "debris")
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
            .Requires(EventCondition.Working)
            .Choice("Investigate Thoroughly",
                "Check the source carefully.",
                [
                    new EventResult("Fresh water source. You fill up.", 0.50, 20)
                        .Rewards(RewardPool.WaterSource),
                    new EventResult("Water AND game trails. Animals need water too.", 0.20, 25)
                        .Rewards(RewardPool.WaterSource),
                    new EventResult("Thin ice. You break through.", 0.15, 15)
                        .Damage(5, DamageType.Blunt, "ice break")
                        .WithEffects(EffectFactory.Cold(-15, 45), EffectFactory.Wet(0.6, 90)),
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
                    new EventResult("You make a mental note and continue.", 1.0, 3)
                ]);
    }

    private static GameEvent UnexpectedYield(GameContext ctx)
    {
        return new GameEvent("Unexpected Yield",
            "As you work, you notice something useful you almost missed.", 0.8)
            .Requires(EventCondition.Working)
            .Choice("Take It",
                "A lucky find.",
                [
                    new EventResult("Extra materials found. Lucky day.", 0.50, 5)
                        .Rewards(RewardPool.CraftingMaterials),
                    new EventResult("Quality tinder in unexpected place.", 0.25, 3)
                        .Rewards(RewardPool.TinderBundle),
                    new EventResult("Usable bone fragment.", 0.15, 5)
                        .Rewards(RewardPool.BoneHarvest),
                    new EventResult("A scrap of hide, still usable.", 0.10, 5)
                        .Rewards(RewardPool.HideScrap)
                ])
            .Choice("Leave It",
                "You're focused. Stay on task.",
                [
                    new EventResult("You ignore the distraction and keep working.", 1.0, 0)
                ]);
    }

    private static GameEvent ExposedPosition(GameContext ctx)
    {
        return new GameEvent("Exposed Position",
            "You've wandered into an exposed area. Wind cuts through you. No cover nearby.", 0.9)
            .Requires(EventCondition.IsExpedition, EventCondition.Outside, EventCondition.HighWind)
            .Choice("Move Fast",
                "Push through quickly. Minimize exposure time.",
                [
                    new EventResult("Through it quickly. Cold but moving.", 0.45, 5)
                        .WithEffects(EffectFactory.Cold(-10, 20)),
                    new EventResult("Longer than expected. Wind brutal.", 0.30, 10)
                        .WithEffects(EffectFactory.Cold(-15, 30)),
                    new EventResult("Stumble in wind. Fall.", 0.15, 8)
                        .Damage(4, DamageType.Blunt, "fall")
                        .WithEffects(EffectFactory.Cold(-12, 25)),
                    new EventResult("Wind knocks you down. Disoriented.", 0.10, 15)
                        .Damage(6, DamageType.Blunt, "fall")
                        .WithEffects(EffectFactory.Cold(-20, 40))
                ])
            .Choice("Find Cover First",
                "Look for any windbreak before crossing.",
                [
                    new EventResult("Small windbreak. Helps.", 0.50, 12)
                        .WithEffects(EffectFactory.Cold(-5, 15)),
                    new EventResult("Nothing useful. Wasted time in wind.", 0.30, 15)
                        .WithEffects(EffectFactory.Cold(-12, 25)),
                    new EventResult("Decent hollow. Warm up before continuing.", 0.15, 18)
                        .WithEffects(EffectFactory.Cold(-3, 10)),
                    new EventResult("'Shelter' funnels wind. Worse than open.", 0.05, 10)
                        .WithEffects(EffectFactory.Cold(-18, 35))
                ])
            .Choice("Emergency Fire",
                "Stop and make a fire. Desperate but might work.",
                [
                    new EventResult("Fire catches. Warmth floods back.", 0.45, 15)
                        .Costs(ResourceType.Fuel, 2),
                    new EventResult("Wind makes it hard. Uses more fuel.", 0.30, 20)
                        .Costs(ResourceType.Fuel, 3)
                        .WithEffects(EffectFactory.Cold(-5, 15)),
                    new EventResult("Won't catch. Wasted fuel.", 0.15, 12)
                        .Costs(ResourceType.Fuel, 2)
                        .WithEffects(EffectFactory.Cold(-12, 25)),
                    new EventResult("Sparks in wind. Burn yourself.", 0.10, 10)
                        .Costs(ResourceType.Fuel, 2)
                        .Damage(3, DamageType.Burn, "ember burn")
                        .WithEffects(EffectFactory.Burn(0.2, 45))
                ],
                requires: [EventCondition.HasFuelPlenty])
            .Choice("Turn Back",
                "Retreat the way you came.",
                [
                    new EventResult("Backtracking costs time but avoids worst.", 0.70, 8)
                        .WithEffects(EffectFactory.Cold(-6, 15)),
                    new EventResult("Way back harder than remembered.", 0.20, 12)
                        .WithEffects(EffectFactory.Cold(-10, 25)),
                    new EventResult("Get turned around. Lost time, cold.", 0.10, 15)
                        .WithEffects(EffectFactory.Cold(-15, 35))
                ]);
    }

    private static GameEvent NaturalShelterSpotted(GameContext ctx)
    {
        return new GameEvent("Natural Shelter Spotted",
            "A defensible overhang. A dense thicket. A hollow in the hillside. Natural shelter, if improved.", 0.5)
            .Requires(EventCondition.Working)
            .Choice("Improve It Now",
                "Spend time making this usable shelter.",
                [
                    new EventResult("Solid work. This is shelter now.", 0.60, 45)
                        .Costs(ResourceType.Fuel, 3)
                        .WithEffects(EffectFactory.Focused(0.15, 60))
                        .AddsFeature(typeof(ShelterFeature), (0.4, 0.5, 0.6)),
                    new EventResult("Takes longer but done right.", 0.25, 60)
                        .Costs(ResourceType.Fuel, 4)
                        .WithEffects(EffectFactory.Exhausted(0.2, 60))
                        .AddsFeature(typeof(ShelterFeature), (0.5, 0.6, 0.7)),
                    new EventResult("Not as good as hoped. Partial shelter.", 0.10, 40)
                        .Costs(ResourceType.Fuel, 2)
                        .AddsFeature(typeof(ShelterFeature), (0.2, 0.3, 0.4)),
                    new EventResult("Collapses during construction. Wasted effort.", 0.05, 30)
                        .Costs(ResourceType.Fuel, 3)
                        .Damage(3, DamageType.Blunt, "collapse")
                        .WithEffects(EffectFactory.Shaken(0.2))
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
                        .WithEffects(EffectFactory.Rested(0.15, 45))
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
            .Requires(EventCondition.Working)
            .MoreLikelyIf(EventCondition.IsStormy, 1.5)
            .Choice("Haul It In",
                "Gather what you can carry.",
                [
                    new EventResult("Good fuel. Worth the effort.", 0.60, 15)
                        .Rewards(RewardPool.BasicSupplies),
                    new EventResult("Mixed quality. Some usable.", 0.25, 15)
                        .Rewards(RewardPool.BasicSupplies),
                    new EventResult("Excellent find. Dry, quality wood.", 0.10, 12)
                        .Rewards(RewardPool.AbandonedCamp),
                    new EventResult("Something else caught in the debris.", 0.05, 10)
                        .Rewards(RewardPool.HiddenCache)
                ])
            .Choice("Check for More",
                "Investigate the source. Might be recurring.",
                [
                    new EventResult("Find the source — debris field. Good to know.", 0.30, 30)
                        .Rewards(RewardPool.BasicSupplies),
                    new EventResult("Just this batch. Good haul though.", 0.50, 20)
                        .Rewards(RewardPool.BasicSupplies),
                    new EventResult("Nothing more. At least you got some.", 0.20, 25)
                        .Rewards(RewardPool.BasicSupplies)
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
            .MoreLikelyIf(EventCondition.DisturbedHigh, 2.0)
            .MoreLikelyIf(EventCondition.DisturbedCritical, 3.0)
            .Choice("Push Through",
                "Force yourself back to the present. Focus on your hands, the cold, anything real.",
                [
                    new EventResult("You ground yourself. It passes.", 0.45, 10)
                        .WithEffects(EffectFactory.Shaken(0.15)),
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
            .MoreLikelyIf(EventCondition.DisturbedCritical, 2.5)
            .Choice("Take Stock",
                "Check yourself. Check your surroundings. What happened?",
                [
                    new EventResult("Maybe 30 minutes. Lost in thought. At least you're warm enough.", 0.50, 30)
                        .WithEffects(EffectFactory.Shaken(0.2)),
                    new EventResult("An hour, at least. You're colder than you should be.", 0.30, 60)
                        .WithEffects(EffectFactory.Cold(-12, 40)),
                    new EventResult("The sun has moved significantly. This is concerning.", 0.15, 90)
                        .WithEffects(EffectFactory.Cold(-18, 60), EffectFactory.Paranoid(0.25)),
                    new EventResult("You... worked? Your pack has more resources than before.", 0.05, 45)
                        .Rewards(RewardPool.BasicSupplies)
                        .WithEffects(EffectFactory.Shaken(0.3))
                        .Escalate("Disturbed", 0.15)
                ]);
    }

    private static GameEvent FacingTheSource(GameContext ctx)
    {
        var disturbedTension = ctx.Tensions.GetTension("Disturbed");
        var source = disturbedTension?.Description ?? "what happened here";

        // Only trigger if at the source location where trauma occurred
        if (disturbedTension?.SourceLocation == null || disturbedTension.SourceLocation != ctx.CurrentLocation)
        {
            // Return a dummy event that will never pass requirements
            return new GameEvent("FacingTheSource", "", 0)
                .Requires(EventCondition.Stalked, EventCondition.SmokeSpotted); // Impossible combination
        }

        return new GameEvent("Facing the Source",
            $"You're back. Where you found {source}. Your heart pounds, but you're here.", 3.0)
            .Requires(EventCondition.Working, EventCondition.Disturbed)
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
                        .WithEffects(EffectFactory.Fear(0.3))
                ])
            .Choice("Search for Answers",
                "Understand what happened. Knowledge might help.",
                [
                    new EventResult("You piece together what you can. A story emerges. It ends badly.", 0.50, 30)
                        .Escalate("Disturbed", -0.25)
                        .WithEffects(EffectFactory.Shaken(0.2)),
                    new EventResult("You find something useful they left behind.", 0.30, 25)
                        .Rewards(RewardPool.AbandonedCamp)
                        .Escalate("Disturbed", -0.2),
                    new EventResult("The more you learn, the worse it gets.", 0.15, 35)
                        .Escalate("Disturbed", 0.15)
                        .WithEffects(EffectFactory.Fear(0.4)),
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
                        .WithEffects(EffectFactory.Fear(0.2))
                ]);
    }
}