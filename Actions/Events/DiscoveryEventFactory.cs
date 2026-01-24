using text_survival.Actions.Variants;
using text_survival.Actors.Animals;
using text_survival.Bodies;
using text_survival.Effects;
using text_survival.Environments.Features;
using text_survival.Items;

namespace text_survival.Actions;

/// <summary>
/// Factory for discovery events triggered by EventTriggerFeature.
/// Maps EventId strings to GameEvent factories.
///
/// These events are triggered when a player discovers a hidden EventTriggerFeature
/// during foraging. Unlike random events, these are guaranteed to occur and
/// represent significant discoveries in the world.
/// </summary>
public static class DiscoveryEventFactory
{
    /// <summary>
    /// Registry of event factories keyed by EventId.
    /// </summary>
    private static readonly Dictionary<string, Func<GameContext, GameEvent>> EventFactories = new()
    {
        ["old_campsite"] = OldCampsiteDiscovery,
        ["frozen_traveler"] = FrozenTravelerDiscovery,
        ["hidden_cache"] = HiddenCacheDiscovery,
        ["bone_scatter"] = BoneScatterDiscovery,
        ["old_kill_site"] = OldKillSiteDiscovery,
        // Phase 1: Terrain-based discoveries
        ["water_source"] = WaterSourceDiscovery,
        ["beehive"] = BeehiveDiscovery,
        ["squirrel_cache"] = SquirrelCacheDiscovery,
        ["mushroom_patch"] = MushroomPatchDiscovery,
        // Phase 2: Location-specific discoveries
        ["deadfall_den"] = DeadfallDenDiscovery,
        ["previous_use"] = PreviousUseDiscovery,
        ["fire_origin"] = FireOriginDiscovery,
        ["bear_cache"] = BearCacheDiscovery,
        // Phase 3: Additional location-specific discoveries
        ["fresh_tracks"] = FreshTracksDiscovery,
        ["see_for_miles"] = SeeForMilesDiscovery,
        ["need_an_axe"] = NeedAnAxeDiscovery,
        ["sharp_edges"] = SharpEdgesDiscovery,
        ["beaver_activity"] = BeaverActivityDiscovery,
        ["spot_movement"] = SpotMovementDiscovery,
    };

    /// <summary>
    /// Create a discovery event from an EventId.
    /// Returns null if the EventId is not registered.
    /// </summary>
    public static GameEvent? Create(string eventId, GameContext ctx)
    {
        if (EventFactories.TryGetValue(eventId, out var factory))
        {
            return factory(ctx);
        }
        return null;
    }

    /// <summary>
    /// Check if an EventId is registered.
    /// </summary>
    public static bool HasEvent(string eventId) => EventFactories.ContainsKey(eventId);

    // === DISCOVERY EVENTS ===

    /// <summary>
    /// Discovered an abandoned campsite while foraging.
    /// More impactful version since player specifically found this through exploration.
    /// </summary>
    private static GameEvent OldCampsiteDiscovery(GameContext ctx)
    {
        return new GameEvent("Abandoned Camp",
            "Your foot catches on something in the snow. Brushing it aside, you find the remains of a camp. " +
            "A fire ring, blackened stones, the collapsed frame of a lean-to. Someone survived here once.",
            1.0)
            .Choice("Search Thoroughly",
                "Take your time. Check everything.",
                [
                    new EventResult("Picked clean. Whoever was here took everything.", 0.25, 20),
                    new EventResult("Some scraps they couldn't carry. Useful.", 0.25, 25)
                        .Rewards(RewardPool.AbandonedCamp),
                    new EventResult("Signs of struggle. Dark stains in the snow. You take what's left.", 0.15, 20)
                        .FindsSupplies()
                        .Unsettling()
                        .CreateTension("Disturbed", 0.2, description: "signs of violence"),
                    new EventResult("A cache they hid and never returned for.", 0.15, 30)
                        .FindsCache(),
                    new EventResult("You find remains. Human. Their gear is still useful.", 0.10, 15)
                        .Rewards(RewardPool.AbandonedCamp)
                        .Panicking()
                        .CreateTension("Disturbed", 0.5, ctx.CurrentLocation, description: "human remains"),
                    new EventResult("Bones, scattered by scavengers. What happened here?", 0.10, 18)
                        .Frightening()
                        .CreateTension("Disturbed", 0.35, ctx.CurrentLocation, description: "scattered bones")
                ])
            .Choice("Quick Look",
                "Check the obvious spots, don't linger.",
                [
                    new EventResult("Some tinder in the fire pit. Nothing else.", 0.4, 8)
                        .Rewards(RewardPool.TinderBundle),
                    new EventResult("A few useful scraps.", 0.35, 10)
                        .FindsSupplies(),
                    new EventResult("Something is off about this place. You leave quickly.", 0.25, 5)
                        .Unsettling()
                ])
            .Choice("Leave It",
                "Bad luck lives here. Move on.",
                [
                    new EventResult("You move on, putting distance between you and whatever happened here.", 1.0, 0)
                ]);
    }

    /// <summary>
    /// Discovered a frozen traveler - someone who didn't make it.
    /// </summary>
    private static GameEvent FrozenTravelerDiscovery(GameContext ctx)
    {
        return new GameEvent("The Frozen One",
            "A shape in the snow, half-buried. As you brush away the powder, features emerge. " +
            "A face, frozen in the moment of death. They didn't make it.",
            1.0)
            .Choice("Search the Body",
                "They have no more use for their possessions.",
                [
                    new EventResult("Their pack is nearly empty. They were starving.", 0.3, 15)
                        .Rewards(RewardPool.BasicSupplies)
                        .Unsettling(),
                    new EventResult("Still equipped. Death came suddenly.", 0.35, 20)
                        .Rewards(RewardPool.AbandonedCamp)
                        .Frightening()
                        .CreateTension("Disturbed", 0.3, description: "a frozen corpse"),
                    new EventResult("Good gear, well-preserved by the cold. You take what you need.", 0.25, 18)
                        .Rewards(RewardPool.HiddenCache)
                        .Shaken(),
                    new EventResult("The face is familiar somehow. From your tribe?", 0.10, 25)
                        .Rewards(RewardPool.AbandonedCamp)
                        .Panicking()
                        .CreateTension("Disturbed", 0.6, ctx.CurrentLocation, description: "a familiar face in death")
                ])
            .Choice("Mark the Spot",
                "Come back when you're ready to deal with this.",
                [
                    new EventResult("You pile stones to mark the location. Something to return to.", 1.0, 5)
                        .MarksFrozenTraveler()
                ])
            .Choice("Move On",
                "The dead are dead.",
                [
                    new EventResult("You leave them to the snow. Nothing you can do now.", 1.0, 0)
                ]);
    }

    /// <summary>
    /// Discovered a hidden cache - someone's stash.
    /// </summary>
    private static GameEvent HiddenCacheDiscovery(GameContext ctx)
    {
        return new GameEvent("Hidden Cache",
            "Something about this rock formation seems deliberate. You pry at the stones and find " +
            "a hollow space behind them. Someone hid supplies here.",
            1.0)
            .Choice("Take Everything",
                "Finder's keepers.",
                [
                    new EventResult("A good haul. Whoever cached this had the right idea.", 0.5, 15)
                        .FindsCache(),
                    new EventResult("Preserved food, tools, tinder. A lifeline.", 0.35, 20)
                        .FindsCache()
                        .Rewards(RewardPool.TinderBundle),
                    new EventResult("The cache is old but well-preserved. Quality supplies.", 0.15, 18)
                        .FindsCache()
                ])
            .Choice("Take What You Need",
                "Leave some for whoever cached it. Or the next desperate soul.",
                [
                    new EventResult("You take the essentials, leave the rest.", 0.7, 12)
                        .Rewards(RewardPool.BasicSupplies),
                    new EventResult("Enough to help, not enough to feel like theft.", 0.3, 10)
                        .Rewards(RewardPool.TinderBundle)
                ])
            .Choice("Leave It",
                "Not yours to take.",
                [
                    new EventResult("You cover the cache back up. It's still here if you need it.", 1.0, 5)
                        .MarksHiddenCache()
                ]);
    }

    /// <summary>
    /// Discovered scattered bones - evidence of past predation.
    /// </summary>
    private static GameEvent BoneScatterDiscovery(GameContext ctx)
    {
        return new GameEvent("Bone Scatter",
            "Bones, scattered across the snow. Old, picked clean. Something died here. Something else ate well.",
            1.0)
            .Choice("Gather What's Useful",
                "Bone is bone. Still good for tools.",
                [
                    new EventResult("You collect several intact pieces.", 0.6, 10)
                        .Rewards(RewardPool.BoneHarvest),
                    new EventResult("Most are cracked for marrow, but some are usable.", 0.3, 12)
                        .Rewards(RewardPool.BoneHarvest),
                    new EventResult("Quality bone, surprisingly well-preserved.", 0.1, 15)
                        .Rewards(RewardPool.BoneHarvest)
                        .Rewards(RewardPool.BoneHarvest)
                ])
            .Choice("Look for Signs",
                "What killed this? What ate it?",
                [
                    new EventResult("Old wolf sign. They've moved on.", 0.5, 8)
                        .BecomeStalked(0.15),
                    new EventResult("Bear teeth marks. Be careful in this area.", 0.3, 10)
                        .CreateTension("ClaimedTerritory", 0.3, animalType: AnimalType.Bear),
                    new EventResult("Human tool marks on some bones. Someone else hunted here.", 0.2, 12)
                ])
            .Choice("Move On",
                "Death is common here.",
                [
                    new EventResult("You leave the bones where they lie.", 1.0, 0)
                ]);
    }

    /// <summary>
    /// Discovered an old kill site - fresher remains with potential danger.
    /// </summary>
    private static GameEvent OldKillSiteDiscovery(GameContext ctx)
    {
        return new GameEvent("Old Kill Site",
            "The smell hits you first. Something died here recently - not fresh, but not ancient either. " +
            "A carcass, picked over but not empty.",
            1.0)
            .Choice("Approach Carefully",
                "There might still be meat. There might still be predators.",
                [
                    new EventResult("No scavengers in sight. You claim the scraps.", 0.4, 15)
                        .Rewards(RewardPool.BasicMeat),
                    new EventResult("A raven takes off as you approach. Nothing else watches.", 0.25, 12)
                        .Rewards(RewardPool.BasicMeat)
                        .Rewards(RewardPool.BoneHarvest),
                    new EventResult("Something growls from the brush. You back away slowly.", 0.2, 5)
                        .BecomeStalked(0.4)
                        .Aborts(),
                    new EventResult("You get close and realize something is still feeding. Wolf.", 0.15, 3)
                        .Encounter(AnimalType.Wolf, 15, 0.6)
                ])
            .Choice("Watch From Distance",
                "See what comes to feed.",
                [
                    new EventResult("Ravens circle but nothing larger appears. Safe to approach.", 0.6, 20)
                        .Rewards(RewardPool.BasicMeat),
                    new EventResult("A wolf arrives. You note its path and slip away.", 0.3, 15)
                        .BecomeStalked(0.2),
                    new EventResult("Nothing comes. The carcass is truly abandoned.", 0.1, 25)
                        .Rewards(RewardPool.BasicMeat)
                        .Rewards(RewardPool.BoneHarvest)
                ])
            .Choice("Leave It",
                "Not worth the risk.",
                [
                    new EventResult("You give the kill site a wide berth.", 1.0, 5)
                ]);
    }

    // === PHASE 1: TERRAIN-BASED DISCOVERIES ===

    /// <summary>
    /// Discovered a water source while foraging.
    /// </summary>
    private static GameEvent WaterSourceDiscovery(GameContext ctx)
    {
        return new GameEvent("Water Source",
            "You hear it before you see it — running water, or the promising crack of ice over a stream. " +
            "Following the sound leads you to a reliable water source.",
            1.0)
            .Choice("Investigate Thoroughly",
                "Check the source carefully.",
                [
                    new EventResult("Fresh water source. You drink your fill and mark the location.", 0.50, 20)
                        .Rewards(RewardPool.WaterSource),
                    new EventResult("Water AND game trails. Animals need water too.", 0.25, 25)
                        .Rewards(RewardPool.WaterSource),
                    new EventResult("Thin ice at the edge. You break through but recover.", 0.15, 15)
                        .WithEffects(EffectFactory.Cold(-12, 30), EffectFactory.Wet(0.4)),
                    new EventResult("Perfect source. Clear, accessible, sheltered.", 0.10, 25)
                        .Rewards(RewardPool.WaterSource)
                ])
            .Choice("Drink Now",
                "You're thirsty. Drink first, ask questions later.",
                [
                    new EventResult("Safe and cold. Immediate relief.", 0.80, 5)
                        .Rewards(RewardPool.WaterSource),
                    new EventResult("Tastes slightly off, but drinkable.", 0.15, 5)
                        .Rewards(RewardPool.WaterSource)
                        .WithEffects(EffectFactory.Nauseous(0.15, 45)),
                    new EventResult("Something in the water. Immediate regret.", 0.05, 5)
                        .WithEffects(EffectFactory.Nauseous(0.4, 90))
                ])
            .Choice("Mark for Later",
                "Note the location and continue working.",
                [
                    new EventResult("You mark a reliable water source — good to know it's here.", 1.0, 3)
                ]);
    }

    /// <summary>
    /// Discovered a beehive while foraging in forest.
    /// </summary>
    private static GameEvent BeehiveDiscovery(GameContext ctx)
    {
        bool hasTorch = ctx.Inventory.HasLitTorch;
        bool hasFire = ctx.CurrentLocation.HasActiveHeatSource();
        string smokeOption = hasTorch ? "Use your torch to calm the bees."
            : hasFire ? "You could light something from the fire first."
            : "You'd need fire for this.";

        return new GameEvent("Beehive",
            "Movement catches your eye — bees entering a hollow in an old tree. Where there are bees, there's honey.",
            1.0)
            .Choice("Smoke Them Out",
                smokeOption,
                hasTorch ?
                [
                    new EventResult("The smoke calms the bees. You harvest honey and beeswax without trouble.", 0.75, 15)
                        .Rewards(RewardPool.HoneyHarvest, 1.5),
                    new EventResult("Good yield. The bees are sluggish from the smoke.", 0.20, 12)
                        .Rewards(RewardPool.HoneyHarvest),
                    new EventResult("The hive is smaller than expected, but still worth the effort.", 0.05, 10)
                        .Rewards(RewardPool.HoneyHarvest, 0.6)
                ] :
                [
                    new EventResult("You have nothing to make smoke with. The bees would swarm you.", 1.0, 0)
                ])
            .Choice("Grab and Run",
                "Quick hands. Accept some stings for quick rewards.",
                [
                    new EventResult("You snatch a comb and run. Stings burn, but you got honey.", 0.50, 5)
                        .Rewards(RewardPool.HoneyHarvest, 0.7)
                        .Damage(0.08, DamageType.Pierce, BodyTarget.AnyArm)
                        .WithEffects(EffectFactory.Pain(0.2)),
                    new EventResult("The bees are angry. You escape with honey and a face full of stings.", 0.30, 3)
                        .Rewards(RewardPool.HoneyHarvest, 0.5)
                        .Damage(0.12, DamageType.Pierce, BodyTarget.Head)
                        .WithEffects(EffectFactory.Pain(0.4)),
                    new EventResult("They swarm before you can react. You flee empty-handed.", 0.15, 2)
                        .Damage(0.15, DamageType.Pierce, BodyTarget.Chest)
                        .WithEffects(EffectFactory.Pain(0.4)),
                    new EventResult("Bad reaction to the stings. Your face swells.", 0.05, 5)
                        .Damage(0.20, DamageType.Pierce, BodyTarget.Head)
                        .WithEffects(EffectFactory.Nauseous(0.5, 90))
                ])
            .Choice("Leave It",
                "Come back with proper preparation.",
                [
                    new EventResult("You note the tree's location. Return with fire and this will be easy.", 0.90, 2)
                        .CreateTension("MarkedDiscovery", 0.4, description: "beehive location"),
                    new EventResult("Getting too close, a few guard bees find you.", 0.10, 3)
                        .Damage(0.04, DamageType.Pierce, BodyTarget.AnyArm)
                        .CreateTension("MarkedDiscovery", 0.4, description: "beehive location")
                ]);
    }

    /// <summary>
    /// Discovered a squirrel's winter cache while foraging.
    /// </summary>
    private static GameEvent SquirrelCacheDiscovery(GameContext ctx)
    {
        return new GameEvent("Squirrel Cache",
            "A squirrel chatters angrily from a branch above. Following its gaze, you spot a hollow where it's been storing food.",
            1.0)
            .Choice("Dig It Out",
                "Take your time and get everything.",
                [
                    new EventResult("A good haul — nuts, seeds, even some dried berries.", 0.65, 15)
                        .Rewards(RewardPool.SquirrelCache, 1.5),
                    new EventResult("Mostly acorns, but food is food.", 0.30, 12)
                        .Rewards(RewardPool.SquirrelCache, 0.8),
                    new EventResult("The cache goes deeper than expected. Worth the effort.", 0.05, 20)
                        .Rewards(RewardPool.SquirrelCache, 2.5)
                ])
            .Choice("Grab and Go",
                "Scoop what you can quickly.",
                [
                    new EventResult("You snag a handful of nuts before the squirrel gets brave.", 0.70, 3)
                        .Rewards(RewardPool.SquirrelCache, 0.5),
                    new EventResult("Quick work. Not much, but better than nothing.", 0.25, 2),
                    new EventResult("The little beast bites your hand as you reach in!", 0.05, 2)
                        .Damage(0.02, DamageType.Pierce, BodyTarget.AnyArm)
                ])
            .Choice("Leave It",
                "It's the squirrel's food. Winter's hard for everyone.",
                [
                    new EventResult("You move on. The squirrel's chatter fades behind you.", 1.0, 0)
                ]);
    }

    /// <summary>
    /// Discovered a mushroom patch while foraging.
    /// </summary>
    private static GameEvent MushroomPatchDiscovery(GameContext ctx)
    {
        return new GameEvent("Mushroom Patch",
            "You spot a cluster of mushrooms growing on a rotting log. Some look familiar, others have an unusual red tinge.",
            1.0)
            .Choice("Take the Safe Ones",
                "Gather only the mushrooms you recognize.",
                [
                    new EventResult("You carefully select the familiar fungi. Birch polypore and amadou — useful medicine.", 0.70, 8)
                        .Rewards(RewardPool.MedicinalForage, 0.8),
                    new EventResult("Slim pickings. Most of these are varieties you don't recognize.", 0.30, 5)
                ])
            .Choice("Risk the Red Ones",
                "The unusual ones might be valuable... or dangerous.",
                [
                    new EventResult("Your gamble pays off. These are potent medicinal mushrooms.", 0.25, 10)
                        .Rewards(RewardPool.MedicinalForage, 1.5),
                    new EventResult("You gather them, but something feels off. Best not to eat these.", 0.35, 8),
                    new EventResult("You nibble a small piece to test. Your stomach cramps almost immediately.", 0.30, 5)
                        .WithEffects(EffectFactory.Nauseous(0.4, 60)),
                    new EventResult("Violent reaction. You spend the next hour retching.", 0.10, 60)
                        .WithEffects(EffectFactory.Nauseous(0.7, 120))
                        .DrainsStats(calories: 200)
                ])
            .Choice("Take All Carefully",
                "Spend extra time examining and sorting everything.",
                [
                    new EventResult("Patience rewarded. You identify several useful varieties and avoid the toxic ones.", 0.60, 20)
                        .Rewards(RewardPool.MedicinalForage, 1.2),
                    new EventResult("You gather what you can identify, leaving the questionable ones.", 0.30, 15)
                        .Rewards(RewardPool.MedicinalForage, 0.7),
                    new EventResult("Despite your care, you're not certain about any of them. Better safe than sorry.", 0.10, 15)
                ]);
    }

    // === PHASE 2: LOCATION-SPECIFIC DISCOVERIES ===

    /// <summary>
    /// Discovered a small animal den in the deadfall.
    /// </summary>
    private static GameEvent DeadfallDenDiscovery(GameContext ctx)
    {
        return new GameEvent("Something in There",
            "Under a root ball, a dark opening. Fresh tracks in the dirt. Something small lives here.",
            1.0)
            .Choice("Reach In Carefully",
                "Risk a bite. Could be food.",
                [
                    new EventResult("Your fingers close on fur. You pull out a rabbit — dinner.", 0.35, 8)
                        .Rewards(RewardPool.SmallGame),
                    new EventResult("Nothing. The den goes deeper than you thought.", 0.40, 5),
                    new EventResult("Sharp teeth! You jerk back, bleeding.", 0.25, 3)
                        .WithEffects(EffectFactory.Bleeding(0.2))
                ])
            .Choice("Smoke It Out",
                "Build a small fire at the entrance.",
                [
                    new EventResult("Smoke fills the hole. A rabbit bolts out — you grab it.", 0.60, 15)
                        .Costs(ResourceType.Tinder, 1)
                        .Costs(ResourceType.Fuel, 1)
                        .Rewards(RewardPool.SmallGame),
                    new EventResult("Nothing emerges. Maybe already abandoned.", 0.40, 12)
                        .Costs(ResourceType.Tinder, 1)
                        .Costs(ResourceType.Fuel, 1)
                ],
                [EventCondition.HasTinder, EventCondition.HasFuel])
            .Choice("Leave It",
                "Not worth the risk.",
                [
                    new EventResult("You move on. Plenty else to find here.", 1.0, 0)
                ]);
    }

    /// <summary>
    /// Discovered evidence of previous use at the rock overhang.
    /// </summary>
    private static GameEvent PreviousUseDiscovery(GameContext ctx)
    {
        return new GameEvent("Someone Was Here",
            "Charcoal smudges the rock. A fire pit circle. Someone used this shelter before.",
            1.0)
            .Choice("Search Thoroughly",
                "They might have left something.",
                [
                    new EventResult("Under a loose rock — a cache of tinder, dry and ready.", 0.40, 15)
                        .Rewards(RewardPool.TinderBundle),
                    new EventResult("Scratched into the stone: a tally of days. They survived here.", 0.35, 10),
                    new EventResult("Bones, cracked for marrow. Old. Picked clean.", 0.25, 12)
                        .Rewards(RewardPool.BoneHarvest)
                ])
            .Choice("Use What's Here",
                "The fire pit is ready. That's enough.",
                [
                    new EventResult("The old fire pit will save you time. Good enough.", 1.0, 5)
                ]);
    }

    /// <summary>
    /// Discovered the origin of the fire at the burnt stand.
    /// </summary>
    private static GameEvent FireOriginDiscovery(GameContext ctx)
    {
        return new GameEvent("The Fire's Origin",
            "Near the center of the burnt stand, you find a massive split trunk. Lightning-struck. The fire started here.",
            1.0)
            .Choice("Examine the Strike",
                "Study the pattern of destruction.",
                [
                    new EventResult("The strike split the trunk and ignited the heartwood. Nature's violence made this clearing.", 0.70, 5),
                    new EventResult("Among the ash, you find chunks of charcoal perfect for fire-starting.", 0.30, 8)
                        .Rewards(RewardPool.TinderBundle)
                ])
            .Choice("Move On",
                "It's just burnt forest.",
                [
                    new EventResult("You leave the origin behind. The charcoal here is plentiful regardless.", 1.0, 0)
                ]);
    }

    /// <summary>
    /// Discovered a bear's food cache in the cave.
    /// </summary>
    private static GameEvent BearCacheDiscovery(GameContext ctx)
    {
        return new GameEvent("Bear's Cache",
            "Deep in the cave, a mound of dirt and debris. The smell of rotting meat. The bear has cached its kills here.",
            1.0)
            .Choice("Risk It — Take the Meat",
                "Food is food. Work fast.",
                [
                    new EventResult("You uncover frozen carcasses. Partially eaten, but there's meat here.", 0.50, 20)
                        .FindsMeat()
                        .CreateTension("ClaimedTerritory", 0.4, animalType: AnimalType.Bear, location: ctx.CurrentLocation),
                    new EventResult("The cache is fresh. The bear hasn't been gone long.", 0.30, 15)
                        .Rewards(RewardPool.SmallGame)
                        .CreateTension("ClaimedTerritory", 0.6, animalType: AnimalType.Bear, location: ctx.CurrentLocation),
                    new EventResult("A rumbling growl from the cave mouth. It's back. RUN.", 0.20, 5)
                        .Encounter(AnimalType.Bear, 20, 0.7)
                ])
            .Choice("Take Bones Only",
                "Less valuable, but won't smell like fresh theft.",
                [
                    new EventResult("You grab bones from the older kills. The bear won't miss them.", 0.80, 10)
                        .Rewards(RewardPool.BoneHarvest),
                    new EventResult("Even this disturbs the cache. You've left a sign.", 0.20, 8)
                        .Rewards(RewardPool.BoneHarvest)
                        .CreateTension("ClaimedTerritory", 0.2, animalType: AnimalType.Bear, location: ctx.CurrentLocation)
                ])
            .Choice("Leave It Untouched",
                "Not worth angering whatever lives here.",
                [
                    new EventResult("You back away from the cache. Wisdom over hunger.", 1.0, 3)
                ]);
    }

    // === PHASE 3: ADDITIONAL LOCATION-SPECIFIC DISCOVERIES ===

    /// <summary>
    /// Discovered fresh animal tracks while foraging at the game trail.
    /// </summary>
    private static GameEvent FreshTracksDiscovery(GameContext ctx)
    {
        var territory = ctx.CurrentLocation?.GetFeature<AnimalTerritoryFeature>();
        var animal = territory?.GetRandomAnimal() ?? AnimalType.Caribou;
        var trackDesc = animal switch
        {
            AnimalType.Caribou => "Hoofprints in the snow",
            AnimalType.Megaloceros => "Massive hoofprints with antler drag marks",
            AnimalType.Bison => "Deep cloven tracks churning the snow",
            AnimalType.Rabbit => "Small paw prints, close together",
            _ => "Fresh tracks"
        };
        var animalDisplay = animal.DisplayName();

        return new GameEvent("Fresh Tracks",
            $"Your searching uncovers something useful — {trackDesc.ToLower()}, still crisp. " +
            $"{char.ToUpper(animalDisplay[0]) + animalDisplay[1..]} passed through recently. The trail leads deeper into the area.",
            1.0)
            .Choice("Follow Them",
                "The trail is fresh. They can't be far.",
                [
                    new EventResult($"You track them carefully. {char.ToUpper(animalDisplay[0]) + animalDisplay[1..]} ahead, grazing. An opportunity.", 0.45, 20)
                        .CreateTension("WoundedPrey", 0.5, description: $"Fresh {animalDisplay.ToLower()} trail"),
                    new EventResult("The trail goes cold. They're faster than you.", 0.30, 15),
                    new EventResult("The trail leads to a thicket. Bedded down - dangerous to approach.", 0.15, 12)
                        .CreateTension("WoundedPrey", 0.3),
                    new EventResult("Following the tracks, you find something else found them first.", 0.10, 12)
                        .BecomeStalked(0.4)
                ])
            .Choice("Mark the Location",
                "Note where you found these. Return prepared for a proper hunt.",
                [
                    new EventResult("You memorize the landmarks. Good hunting ground.", 0.80, 3)
                        .CreateTension("MarkedDiscovery", 0.3, description: $"{animalDisplay.ToLower()} trail spotted"),
                    new EventResult("You break some branches to mark the spot.", 0.20, 5)
                        .CreateTension("MarkedDiscovery", 0.4, description: $"Marked {animalDisplay.ToLower()} trail")
                ])
            .Choice("Keep Working",
                "Note the tracks and continue what you were doing.",
                [
                    new EventResult("You file it away mentally. Good to know they're in the area.", 1.0, 0)
                ]);
    }

    /// <summary>
    /// Discovered a commanding view from the rocky ridge.
    /// </summary>
    private static GameEvent SeeForMilesDiscovery(GameContext ctx)
    {
        return new GameEvent("See for Miles",
            "You reach a high point on the ridge. The view opens up — both valleys spread below, " +
            "the landscape laid bare. From here, you can see everything.",
            1.0)
            .Choice("Survey the Landscape",
                "Take time to study what's below.",
                [
                    new EventResult("Smoke to the east — another camp? Movement in the southern forest — game.", 0.50, 15),
                    new EventResult("Wolves moving in a pack to the north. Good to know where they are.", 0.30, 12)
                        .CreateTension("PackNearby", 0.3),
                    new EventResult("The pass is visible. Still snow-choked, but you can see the route.", 0.20, 10)
                ])
            .Choice("Note Key Features",
                "Quick scan for landmarks.",
                [
                    new EventResult("You mark locations in your mind. The lay of the land is clearer now.", 1.0, 5)
                ]);
    }

    /// <summary>
    /// Discovered the need for an axe in the ancient grove.
    /// </summary>
    private static GameEvent NeedAnAxeDiscovery(GameContext ctx)
    {
        return new GameEvent("Need an Axe",
            "Massive trunks tower around you. Premium fuel — dense, long-burning hardwood everywhere. " +
            "But as you examine the trees, reality sets in: you can't cut this with what you have.",
            1.0)
            .Choice("Look for Deadfall",
                "There must be something already down.",
                [
                    new EventResult("Precious little. This forest is too healthy — everything is either standing or rotted.", 0.70, 15),
                    new EventResult("A fallen branch, storm-snapped. Not much, but it's something.", 0.30, 12)
                        .FindsSupplies()
                ])
            .Choice("Mark It for Later",
                "Come back with proper tools.",
                [
                    new EventResult("You note the location. When you have an axe, this place is gold.", 1.0, 3)
                ]);
    }

    /// <summary>
    /// Discovered the hazardous nature of the flint seam.
    /// </summary>
    private static GameEvent SharpEdgesDiscovery(GameContext ctx)
    {
        return new GameEvent("Sharp Edges",
            "Working among the flint nodules, you notice the ground is treacherous. " +
            "Razor-sharp flakes litter every surface — a wrong step could cut deep.",
            1.0)
            .Choice("Move Carefully",
                "Watch every step.",
                [
                    new EventResult("Slow going, but you avoid the worst of it.", 0.80, 10),
                    new EventResult("A flake slices through your footwear. Blood wells up.", 0.20, 5)
                        .WithEffects(EffectFactory.Bleeding(0.15))
                ])
            .Choice("Clear a Path",
                "Sweep the worst debris aside.",
                [
                    new EventResult("You clear a workspace. Safer now.", 0.90, 15),
                    new EventResult("A piece shatters as you move it. Shard catches your hand.", 0.10, 8)
                        .WithEffects(EffectFactory.Bleeding(0.1))
                ]);
    }

    /// <summary>
    /// Discovered beaver activity at the dam.
    /// </summary>
    private static GameEvent BeaverActivityDiscovery(GameContext ctx)
    {
        return new GameEvent("Dam Builders",
            "Movement catches your eye — splashing in the pond. A beaver surfaces, branch in its teeth, " +
            "and swims toward the dam. The ecosystem here is thriving.",
            1.0)
            .Choice("Watch Them Work",
                "There's something peaceful about it.",
                [
                    new EventResult("They weave branches, pack mud, repair gaps. Industrious creatures.", 0.70, 10),
                    new EventResult("One slaps its tail — warning. They've spotted you. The pond goes silent.", 0.30, 5)
                ])
            .Choice("Ignore Them",
                "You have your own work to do.",
                [
                    new EventResult("You gather what you need. The beavers keep their distance.", 1.0, 3)
                ]);
    }

    /// <summary>
    /// Discovered movement from the granite outcrop vantage point.
    /// </summary>
    private static GameEvent SpotMovementDiscovery(GameContext ctx)
    {
        return new GameEvent("Movement Below",
            "From the outcrop's height, you spot it — movement at the tree line below. " +
            "Something is down there, but from here you have the advantage of observation.",
            1.0)
            .Choice("Study It",
                "Use your vantage. Learn what you're dealing with.",
                [
                    new EventResult("A wolf, circling. Now you know where it is.", 0.50, 10)
                        .BecomeStalked(0.3, AnimalType.Wolf),
                    new EventResult("A fox, hunting mice. No threat.", 0.30, 8),
                    new EventResult("Deer, grazing. Opportunity, if you can get close.", 0.20, 8)
                ])
            .Choice("Mark the Direction",
                "Note it and stay alert.",
                [
                    new EventResult("You mark the direction. Now you know which way not to go.", 1.0, 3)
                ])
            .Choice("Get Down",
                "Visibility works both ways.",
                [
                    new EventResult("You descend quickly. Whatever it was, you don't want it seeing you.", 1.0, 5)
                ]);
    }
}
