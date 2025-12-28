using text_survival.Environments.Features;
using text_survival.Items;

namespace text_survival.Actions;

/// <summary>
/// Location-specific events for Tier 1 locations.
/// Events that trigger based on being at specific location types.
/// </summary>
public static partial class GameEventRegistry
{
    // === BURNT STAND EVENTS ===

    /// <summary>
    /// Narrative event - discover the origin of the fire.
    /// First visit to Burnt Stand, 30% chance.
    /// </summary>
    private static GameEvent DiscoverFireOrigin(GameContext ctx)
    {
        return new GameEvent("The Fire's Origin",
            "Near the center of the burnt stand, you find a massive split trunk. Lightning-struck. The fire started here.", 0.3)
            .Requires(EventCondition.Working)
            .OnlyAt("Burnt Stand")
            .WithCooldown(168) // Once per week (effectively once per playthrough for this location)
            .Choice("Examine the Strike",
                "Study the pattern of destruction.",
                [
                    new EventResult("The strike split the trunk and ignited the heartwood. Nature's violence made this clearing.", weight: 0.70, minutes: 5),
                    new EventResult("Among the ash, you find chunks of charcoal perfect for fire-starting.", weight: 0.30, minutes: 8)
                        .Rewards(RewardPool.TinderBundle)
                ])
            .Choice("Move On",
                "It's just burnt forest.",
                [
                    new EventResult("You leave the origin behind. The charcoal here is plentiful regardless.", weight: 1.0)
                ]);
    }

    /// <summary>
    /// Tactical event - mutual visibility in the burnt stand.
    /// Triggers when Stalked tension active in exposed location.
    /// </summary>
    private static GameEvent SpottedInOpen(GameContext ctx)
    {
        var stalked = ctx.Tensions.GetTension("Stalked");
        var predator = stalked?.AnimalType ?? "wolf";

        return new GameEvent("Exposed",
            $"Movement at the tree line. The open terrain works both ways — you spot the {predator.ToLower()}, and it sees you.", 0.8)
            .Requires(EventCondition.Working, EventCondition.Stalked)
            .OnlyAt("Burnt Stand")
            .WithCooldown(2)
            .Choice("Hold Your Ground",
                "Face it. Show no fear.",
                [
                    new EventResult($"The {predator.ToLower()} watches. You watch back. A standoff in the ash.", weight: 0.60, minutes: 10)
                        .EscalatesStalking(0.1),
                    new EventResult($"Your stance unsettles it. The {predator.ToLower()} circles wider, keeping distance.", weight: 0.40, minutes: 8)
                        .EscalatesStalking(-0.1)
                ])
            .Choice("Back Away Slowly",
                "Create distance. Don't run.",
                [
                    new EventResult("You retreat step by step. It doesn't follow. Yet.", weight: 0.70, minutes: 15),
                    new EventResult($"Your movement triggers something. The {predator.ToLower()} starts toward you.", weight: 0.30, minutes: 5)
                        .EscalatesStalking(0.3)
                ])
            .Choice("Use the Visibility",
                "If you can see it, you can prepare.",
                [
                    new EventResult($"You track its position while gathering what you need. Knowledge is power.", weight: 0.80, minutes: 12),
                    new EventResult($"Distracted by watching it, you stumble. The {predator.ToLower()} notices.", weight: 0.20, minutes: 8)
                        .EscalatesStalking(0.2)
                ]);
    }

    // === DEADFALL GROVE EVENTS (uses MakeDeadwoodGrove) ===

    /// <summary>
    /// Hazard event - logs shift while navigating deadfall.
    /// High terrain hazard location.
    /// </summary>
    private static GameEvent LogShifts(GameContext ctx)
    {
        return new GameEvent("Shifting Logs",
            "The log beneath you groans and shifts. The whole tangle is unstable.", 0.6)
            .Requires(EventCondition.Working, EventCondition.HazardousTerrain)
            .WithCooldown(1)
            .Choice("Jump Clear",
                "React fast. Get off the moving log.",
                [
                    new EventResult("You leap clear as the log rolls. Close call.", weight: 0.65, minutes: 3),
                    new EventResult("Your foot catches. The log pins your ankle briefly.", weight: 0.25, minutes: 8)
                        .WithEffects(Effects.EffectFactory.SprainedAnkle(0.3)),
                    new EventResult("You land badly. Pain shoots through your leg.", weight: 0.10, minutes: 10)
                        .WithEffects(Effects.EffectFactory.SprainedAnkle(0.5))
                ])
            .Choice("Brace and Ride It",
                "Stay low, let it settle.",
                [
                    new EventResult("The log rolls, stops. You're shaken but unhurt.", weight: 0.50, minutes: 5),
                    new EventResult("It settles hard against another log, trapping your leg. You wrench free.", weight: 0.35, minutes: 12)
                        .WithEffects(Effects.EffectFactory.SprainedAnkle(0.2)),
                    new EventResult("The whole section collapses. You're caught in the tangle.", weight: 0.15, minutes: 20)
                        .WithEffects(Effects.EffectFactory.Exhausted(0.4, 60))
                ]);
    }

    /// <summary>
    /// Discovery event - find a small animal den in the deadfall.
    /// </summary>
    private static GameEvent DeadfallDen(GameContext ctx)
    {
        return new GameEvent("Something in There",
            "Under a root ball, a dark opening. Fresh tracks in the dirt. Something small lives here.", 0.3)
            .Requires(EventCondition.Working, EventCondition.HasFuelForage)
            .WithCooldown(4)
            .Choice("Reach In Carefully",
                "Risk a bite. Could be food.",
                [
                    new EventResult("Your fingers close on fur. You pull out a rabbit — dinner.", weight: 0.35, minutes: 8)
                        .Rewards(RewardPool.SmallGame),
                    new EventResult("Nothing. The den goes deeper than you thought.", weight: 0.40, minutes: 5),
                    new EventResult("Sharp teeth! You jerk back, bleeding.", weight: 0.25, minutes: 3)
                        .WithEffects(Effects.EffectFactory.Bleeding(0.2))
                ])
            .Choice("Smoke It Out",
                "Build a small fire at the entrance.",
                [
                    new EventResult("Smoke fills the hole. A rabbit bolts out — you grab it.", weight: 0.60, minutes: 15)
                        .Costs(ResourceType.Tinder, 1)
                        .Rewards(RewardPool.SmallGame),
                    new EventResult("Nothing emerges. Maybe already abandoned.", weight: 0.40, minutes: 12)
                        .Costs(ResourceType.Tinder, 1)
                ],
                [EventCondition.HasTinder])
            .Choice("Leave It",
                "Not worth the risk.",
                [
                    new EventResult("You move on. Plenty else to find here.", weight: 1.0)
                ]);
    }

    // === ROCK OVERHANG EVENTS ===

    /// <summary>
    /// Discovery event - evidence of previous use.
    /// </summary>
    private static GameEvent PreviousUse(GameContext ctx)
    {
        return new GameEvent("Someone Was Here",
            "Charcoal smudges the rock. A fire pit circle. Someone used this shelter before.", 0.4)
            .Requires(EventCondition.Working)
            .OnlyAt("Rock Overhang")
            .WithCooldown(168)
            .Choice("Search Thoroughly",
                "They might have left something.",
                [
                    new EventResult("Under a loose rock — a cache of tinder, dry and ready.", weight: 0.40, minutes: 15)
                        .Rewards(RewardPool.TinderBundle),
                    new EventResult("Scratched into the stone: a tally of days. They survived here.", weight: 0.35, minutes: 10),
                    new EventResult("Bones, cracked for marrow. Old. Picked clean.", weight: 0.25, minutes: 12)
                        .Rewards(RewardPool.BoneHarvest)
                ])
            .Choice("Use What's Here",
                "The fire pit is ready. That's enough.",
                [
                    new EventResult("The old fire pit will save you time. Good enough.", weight: 1.0, minutes: 5)
                ]);
    }

    /// <summary>
    /// Fire management event - smoke builds up in enclosed spaces with overhead cover.
    /// </summary>
    private static GameEvent SmokeBuildsUp(GameContext ctx)
    {
        return new GameEvent("Smoke Building",
            "Smoke pools overhead. Your eyes sting. The air is getting thick.", 0.5)
            .Requires(EventCondition.NearFire, EventCondition.HighOverheadCover)
            .WithCooldown(2)
            .Choice("Let the Fire Die Down",
                "Less smoke, less heat.",
                [
                    new EventResult("You bank the fire low. The smoke clears. Colder, but you can breathe.", weight: 1.0, minutes: 10)
                ])
            .Choice("Endure It",
                "You need the warmth. Breathe shallow.",
                [
                    new EventResult("Your eyes water. Your throat burns. But you stay warm.", weight: 0.60, minutes: 5)
                        .WithEffects(Effects.EffectFactory.Nauseous(0.2, 30)),
                    new EventResult("A coughing fit. You have to move away from the fire.", weight: 0.40, minutes: 8)
                        .WithEffects(Effects.EffectFactory.Nauseous(0.3, 45))
                ])
            .Choice("Move Fire to Edge",
                "Sacrifice efficiency for air flow.",
                [
                    new EventResult("The fire burns less efficiently at the edge, but the smoke disperses.", weight: 1.0, minutes: 15)
                ]);
    }

    // === GRANITE OUTCROP EVENTS ===

    /// <summary>
    /// Scouting event - spot movement from vantage point.
    /// </summary>
    private static GameEvent SpotMovement(GameContext ctx)
    {
        return new GameEvent("Movement Below",
            "From the outcrop's height, you see it — movement at the tree line. Something is watching.", 0.5)
            .Requires(EventCondition.Working)
            .OnlyAt("Granite Outcrop")
            .WithConditionFactor(EventCondition.Stalked, 3.0)
            .WithCooldown(2)
            .Choice("Study It",
                "Use your vantage. Learn what you're dealing with.",
                [
                    new EventResult("A wolf, circling. Now you know where it is.", weight: 0.50, minutes: 10)
                        .BecomeStalked(0.3, "Wolf"),
                    new EventResult("A fox, hunting mice. No threat.", weight: 0.30, minutes: 8),
                    new EventResult("Deer, grazing. Opportunity, if you can get close.", weight: 0.20, minutes: 8)
                ])
            .Choice("Mark the Direction",
                "Note it and stay alert.",
                [
                    new EventResult("You mark the direction. Now you know which way not to go.", weight: 1.0, minutes: 3)
                ])
            .Choice("Get Down",
                "Visibility works both ways.",
                [
                    new EventResult("You descend quickly. Whatever it was, you don't want it seeing you.", weight: 1.0, minutes: 5)
                ]);
    }

    /// <summary>
    /// Tactical event - mutual visibility when Stalked.
    /// </summary>
    private static GameEvent MutualVisibility(GameContext ctx)
    {
        var stalked = ctx.Tensions.GetTension("Stalked");
        var predator = stalked?.AnimalType ?? "wolf";

        return new GameEvent("Seen and Seeing",
            $"Exposed on the rock, you see the {predator.ToLower()} below — and it sees you. Eyes lock across the distance.", 0.9)
            .Requires(EventCondition.Stalked)
            .OnlyAt("Granite Outcrop")
            .WithCooldown(4)
            .Choice("Use the Advantage",
                "You have the high ground. Act like it.",
                [
                    new EventResult($"You stand tall. The {predator.ToLower()} hesitates. Prey doesn't act like this.", weight: 0.60, minutes: 5)
                        .EscalatesStalking(-0.2),
                    new EventResult($"It's not intimidated. The {predator.ToLower()} circles, looking for a path up.", weight: 0.40, minutes: 8)
                        .EscalatesStalking(0.1)
                ])
            .Choice("Descend Carefully",
                "You can't stay up here forever.",
                [
                    new EventResult($"You descend the far side. The {predator.ToLower()} loses sight of you.", weight: 0.50, minutes: 12),
                    new EventResult($"It tracks your descent. When you reach the bottom, it's waiting.", weight: 0.50, minutes: 15)
                        .EscalatesStalking(0.3)
                ])
            .Choice("Throw Something",
                "Maybe you can drive it off.",
                [
                    new EventResult($"A rock clatters near it. The {predator.ToLower()} flinches and retreats into the trees.", weight: 0.45, minutes: 3)
                        .EscalatesStalking(-0.1),
                    new EventResult($"Your throw falls short. The {predator.ToLower()} doesn't even react.", weight: 0.35, minutes: 3),
                    new EventResult($"The motion catches its attention. It advances.", weight: 0.20, minutes: 2)
                        .EscalatesStalking(0.2)
                ]);
    }

    // === MELTWATER POOL EVENTS ===

    /// <summary>
    /// Weather event - caught exposed at remote water source.
    /// </summary>
    private static GameEvent WeatherTurns(GameContext ctx)
    {
        return new GameEvent("Weather Turning",
            "Clouds mass on the horizon, moving fast. The exposed pool offers no shelter.", 0.6)
            .Requires(EventCondition.Working, EventCondition.WeatherWorsening)
            .OnlyAt("Meltwater Pool")
            .WithCooldown(4)
            .Choice("Fill Containers Quickly",
                "Grab what you can and run.",
                [
                    new EventResult("You fill half your containers and flee. Better than nothing.", weight: 0.70, minutes: 8),
                    new EventResult("The storm catches you mid-fill. Ice stings your face as you run.", weight: 0.30, minutes: 12)
                        .LightChill()
                ])
            .Choice("Get Full Water Properly",
                "You came all this way. Take the risk.",
                [
                    new EventResult("You finish just as the first flakes hit. Worth it.", weight: 0.40, minutes: 20),
                    new EventResult("The storm hits while you're still filling. Brutal exposure.", weight: 0.60, minutes: 25)
                        .DangerousCold()
                ])
            .Choice("Leave Immediately",
                "Water isn't worth freezing.",
                [
                    new EventResult("You abandon the pool and hurry back. Smart choice.", weight: 1.0, minutes: 3)
                ]);
    }

    // === TIER 2 LOCATION EVENTS ===

    // === ANCIENT GROVE EVENTS ===

    /// <summary>
    /// Atmosphere event - the silence is unnerving.
    /// </summary>
    private static GameEvent TheSilence(GameContext ctx)
    {
        return new GameEvent("The Silence",
            "Nothing moves. No birds, no wind in the branches. The quiet presses on your ears.", 0.4)
            .Requires(EventCondition.Working)
            .OnlyAt("Ancient Grove")
            .WithCooldown(6)
            .Choice("Listen Carefully",
                "Something might be out there.",
                [
                    new EventResult("Just silence. Ancient, patient silence. You're alone here.", weight: 0.70, minutes: 5),
                    new EventResult("There — the faintest crack of a branch. Something is here.", weight: 0.30, minutes: 8)
                        .BecomeStalked(0.2)
                ])
            .Choice("Focus on Work",
                "Silence is just silence.",
                [
                    new EventResult("You push the feeling aside and get back to work.", weight: 1.0, minutes: 2)
                ]);
    }

    /// <summary>
    /// Tool-gated frustration - can't harvest hardwood without axe.
    /// </summary>
    private static GameEvent NeedAnAxe(GameContext ctx)
    {
        return new GameEvent("Need an Axe",
            "Massive trunks everywhere. Premium fuel — dense, long-burning hardwood. But you can't cut it with what you have.", 0.6)
            .Requires(EventCondition.Working)
            .OnlyAt("Ancient Grove")
            .WithCooldown(24)
            .Choice("Look for Deadfall",
                "There must be something already down.",
                [
                    new EventResult("Precious little. This forest is too healthy — everything is either standing or rotted.", weight: 0.70, minutes: 15),
                    new EventResult("A fallen branch, storm-snapped. Not much, but it's something.", weight: 0.30, minutes: 12)
                        .FindsSupplies()
                ])
            .Choice("Mark It for Later",
                "Come back with proper tools.",
                [
                    new EventResult("You note the location. When you have an axe, this place is gold.", weight: 1.0, minutes: 3)
                ]);
    }

    // === FLINT SEAM EVENTS ===

    /// <summary>
    /// Hazard event - sharp edges everywhere.
    /// </summary>
    private static GameEvent SharpEdges(GameContext ctx)
    {
        return new GameEvent("Sharp Edges",
            "Flint flakes everywhere — razor-sharp. A wrong step could cut deep.", 0.5)
            .Requires(EventCondition.Working)
            .OnlyAt("Flint Seam")
            .WithCooldown(2)
            .Choice("Move Carefully",
                "Watch every step.",
                [
                    new EventResult("Slow going, but you avoid the worst of it.", weight: 0.80, minutes: 10),
                    new EventResult("A flake slices through your footwear. Blood wells up.", weight: 0.20, minutes: 5)
                        .WithEffects(Effects.EffectFactory.Bleeding(0.15))
                ])
            .Choice("Clear a Path",
                "Sweep the worst debris aside.",
                [
                    new EventResult("You clear a workspace. Safer now.", weight: 0.90, minutes: 15),
                    new EventResult("A piece shatters as you move it. Shard catches your hand.", weight: 0.10, minutes: 8)
                        .WithEffects(Effects.EffectFactory.Bleeding(0.1))
                ]);
    }

    // === GAME TRAIL EVENTS ===

    /// <summary>
    /// Fresh animal tracks - opportunity to follow, mark, or note the area.
    /// Works anywhere with animal territory, weighted higher at Game Trail.
    /// </summary>
    private static GameEvent FreshTracks(GameContext ctx)
    {
        var territory = ctx.CurrentLocation.GetFeature<AnimalTerritoryFeature>();
        var animal = territory?.GetRandomAnimalName() ?? "deer";
        var animalLower = animal.ToLower();
        var trackDesc = animalLower switch
        {
            "deer" => "Hoofprints in the snow",
            "elk" => "Hoofprints in the snow",
            "moose" => "Hoofprints in the snow",
            "rabbit" => "Small paw prints, close together",
            "hare" => "Small paw prints, close together",
            "boar" => "Deep cloven tracks and rooted earth",
            "pig" => "Deep cloven tracks and rooted earth",
            _ => "Fresh tracks"
        };

        bool isGameTrail = ctx.CurrentLocation?.Name == "Game Trail";

        return new GameEvent("Fresh Tracks",
            $"{trackDesc}, still crisp. {char.ToUpper(animal[0]) + animal[1..]} passed through recently.", isGameTrail ? 1.2 : 0.6)
            .Requires(EventCondition.Working, EventCondition.InAnimalTerritory)
            .WithSituationFactor(Situations.IsFollowingAnimalSigns, 1.5)
            .WithCooldown(4)
            .Choice("Follow Them",
                "The trail is fresh. They can't be far.",
                [
                    new EventResult($"You track them carefully. {char.ToUpper(animal[0]) + animal[1..]} ahead, grazing. An opportunity.", weight: 0.45, minutes: 20)
                        .CreateTension("WoundedPrey", 0.5, description: $"Fresh {animal} trail"),
                    new EventResult("The trail goes cold. They're faster than you.", weight: 0.30, minutes: 15),
                    new EventResult("The trail leads to a thicket. Bedded down - dangerous to approach.", weight: 0.15, minutes: 12)
                        .CreateTension("WoundedPrey", 0.3),
                    new EventResult("Following the tracks, you find something else found them first.", weight: 0.10, minutes: 12)
                        .BecomeStalked(0.4)
                ])
            .Choice("Mark the Location",
                "Note where you found these. Return prepared for a proper hunt.",
                [
                    new EventResult("You memorize the landmarks. Good hunting ground.", weight: 0.80, minutes: 3)
                        .CreateTension("MarkedDiscovery", 0.3, description: $"{animal} trail spotted"),
                    new EventResult("You break some branches to mark the spot.", weight: 0.20, minutes: 5)
                        .CreateTension("MarkedDiscovery", 0.4, description: $"Marked {animal} trail")
                ])
            .Choice("Keep Working",
                "Note the tracks and continue what you were doing.",
                [
                    new EventResult("You file it away mentally. Good to know they're in the area.", weight: 1.0, minutes: 0)
                ]);
    }

    // === DENSE THICKET EVENTS ===

    /// <summary>
    /// Tactical event - predator can't follow into thicket.
    /// </summary>
    private static GameEvent EscapeIntoThicket(GameContext ctx)
    {
        var stalked = ctx.Tensions.GetTension("Stalked");
        var predator = stalked?.AnimalType ?? "wolf";

        return new GameEvent("Escape Route",
            $"The {predator.ToLower()} circles at the thicket's edge. It can't follow you in here.", 0.9)
            .Requires(EventCondition.Stalked)
            .OnlyAt("Dense Thicket")
            .WithCooldown(4)
            .Choice("Push Deeper",
                "Put more brush between you.",
                [
                    new EventResult($"Branches tear at you, but the {predator.ToLower()} falls back. You're safe.", weight: 0.85, minutes: 15)
                        .ResolvesStalking(),
                    new EventResult("The thicket is impassable here. You have to go around.", weight: 0.15, minutes: 20)
                ])
            .Choice("Wait It Out",
                "It has to give up eventually.",
                [
                    new EventResult($"Minutes pass. The {predator.ToLower()} paces, then leaves. You're safe.", weight: 0.70, minutes: 30)
                        .ResolvesStalking(),
                    new EventResult($"It's patient. The {predator.ToLower()} settles in to wait. So are you.", weight: 0.30, minutes: 45)
                        .EscalatesStalking(-0.1)
                ]);
    }

    /// <summary>
    /// Hazard event - caught in the brush.
    /// </summary>
    private static GameEvent CaughtInBrush(GameContext ctx)
    {
        return new GameEvent("Caught in the Brush",
            "A branch hooks your clothing. Another catches your pack. You're tangled.", 0.4)
            .Requires(EventCondition.Working)
            .OnlyAt("Dense Thicket")
            .WithCooldown(2)
            .Choice("Carefully Untangle",
                "Take your time. Don't tear anything.",
                [
                    new EventResult("Patience pays. You extract yourself without damage.", weight: 0.75, minutes: 10),
                    new EventResult("A branch snaps back, catching your face.", weight: 0.25, minutes: 8)
                        .WithEffects(Effects.EffectFactory.Bleeding(0.1))
                ])
            .Choice("Force Through",
                "Tear free. Time is short.",
                [
                    new EventResult("You rip free. Clothing torn but mobile.", weight: 0.60, minutes: 3),
                    new EventResult("Something gives — a strap, a seam. Equipment damaged.", weight: 0.40, minutes: 3)
                ]);
    }

    // === BOULDER FIELD EVENTS ===

    /// <summary>
    /// Hazard event - twisted ankle on unstable rocks.
    /// </summary>
    private static GameEvent TwistedAnkle(GameContext ctx)
    {
        return new GameEvent("Unstable Footing",
            "A rock shifts under your weight. Your ankle rolls painfully.", 0.5)
            .Requires(EventCondition.Working)
            .OnlyAt("Boulder Field")
            .WithCooldown(3)
            .Choice("Test It Gently",
                "See if you can walk on it.",
                [
                    new EventResult("Sore, but functional. You got lucky.", weight: 0.60, minutes: 5),
                    new EventResult("Sharp pain when you put weight on it. This will slow you down.", weight: 0.40, minutes: 8)
                        .WithEffects(Effects.EffectFactory.SprainedAnkle(0.3))
                ])
            .Choice("Rest It",
                "Don't make it worse.",
                [
                    new EventResult("You sit for a while. The swelling stays down.", weight: 0.80, minutes: 20),
                    new EventResult("Even with rest, it's stiffening up.", weight: 0.20, minutes: 25)
                        .WithEffects(Effects.EffectFactory.SprainedAnkle(0.2))
                ]);
    }

    // === ROCKY RIDGE EVENTS ===

    /// <summary>
    /// Scouting event - see for miles from the ridge.
    /// </summary>
    private static GameEvent SeeForMiles(GameContext ctx)
    {
        return new GameEvent("See for Miles",
            "From the ridge top, both valleys spread below. You can see everything.", 0.6)
            .Requires(EventCondition.Working)
            .OnlyAt("Rocky Ridge")
            .WithCooldown(6)
            .Choice("Survey the Landscape",
                "Take time to study what's below.",
                [
                    new EventResult("Smoke to the east — another camp? Movement in the southern forest — game.", weight: 0.50, minutes: 15),
                    new EventResult("Wolves moving in a pack to the north. Good to know where they are.", weight: 0.30, minutes: 12)
                        .CreateTension("PackNearby", 0.3),
                    new EventResult("The pass is visible. Still snow-choked, but you can see the route.", weight: 0.20, minutes: 10)
                ])
            .Choice("Note Key Features",
                "Quick scan for landmarks.",
                [
                    new EventResult("You mark locations in your mind. The lay of the land is clearer now.", weight: 1.0, minutes: 5)
                ]);
    }

    /// <summary>
    /// Exposure event - brutal wind on the ridge.
    /// </summary>
    private static GameEvent RidgeWindChill(GameContext ctx)
    {
        return new GameEvent("Ridge Wind",
            "Wind screams across the exposed stone. It cuts right through you.", 0.7)
            .Requires(EventCondition.Working, EventCondition.HighWind)
            .OnlyAt("Rocky Ridge")
            .WithCooldown(2)
            .Choice("Find Shelter in the Rocks",
                "Duck behind a boulder.",
                [
                    new EventResult("A gap in the rocks breaks the wind. Temporary relief.", weight: 0.75, minutes: 10),
                    new EventResult("Exposed from all angles. No shelter here.", weight: 0.25, minutes: 5)
                        .WithCold(-8, 20)
                ])
            .Choice("Push Through",
                "Finish what you came for.",
                [
                    new EventResult("You grit your teeth and work. Cold seeps into your bones.", weight: 0.60, minutes: 15)
                        .LightChill(),
                    new EventResult("Too much. You have to retreat.", weight: 0.40, minutes: 8)
                        .DangerousCold()
                ])
            .Choice("Descend Immediately",
                "This isn't worth frostbite.",
                [
                    new EventResult("You scramble down to the treeline. Warmer here.", weight: 1.0, minutes: 12)
                ]);
    }

    // === TIER 3 LOCATION EVENTS ===

    // === BEAR CAVE EVENTS ===
    // Note: Primary occupied-state mechanics handled by Den arc events (TheFind, AssessingTheClaim, etc.)
    // These events provide additional Bear Cave-specific flavor and encounters.

    /// <summary>
    /// Discovery event - find cached meat from bear kills.
    /// Bears cache food — a desperate opportunity or a deadly trap.
    /// </summary>
    private static GameEvent BearCache(GameContext ctx)
    {
        return new GameEvent("Bear's Cache",
            "Deep in the cave, a mound of dirt and debris. The smell of rotting meat. The bear has cached its kills here.", 0.4)
            .Requires(EventCondition.Working)
            .OnlyAt("Bear Cave")
            .WithCooldown(24)
            .Choice("Risk It — Take the Meat",
                "Food is food. Work fast.",
                [
                    new EventResult("You uncover frozen carcasses. Partially eaten, but there's meat here.", weight: 0.50, minutes: 20)
                        .FindsMeat()
                        .CreateTension("ClaimedTerritory", 0.4, animalType: "Bear", location: ctx.CurrentLocation),
                    new EventResult("The cache is fresh. The bear hasn't been gone long.", weight: 0.30, minutes: 15)
                        .Rewards(RewardPool.SmallGame)
                        .CreateTension("ClaimedTerritory", 0.6, animalType: "Bear", location: ctx.CurrentLocation),
                    new EventResult("A rumbling growl from the cave mouth. It's back. RUN.", weight: 0.20, minutes: 5)
                        .Encounter("Bear", 20, 0.7)
                ])
            .Choice("Take Bones Only",
                "Less valuable, but won't smell like fresh theft.",
                [
                    new EventResult("You grab bones from the older kills. The bear won't miss them.", weight: 0.80, minutes: 10)
                        .Rewards(RewardPool.BoneHarvest),
                    new EventResult("Even this disturbs the cache. You've left a sign.", weight: 0.20, minutes: 8)
                        .Rewards(RewardPool.BoneHarvest)
                        .CreateTension("ClaimedTerritory", 0.2, animalType: "Bear", location: ctx.CurrentLocation)
                ])
            .Choice("Leave It Untouched",
                "Not worth angering whatever lives here.",
                [
                    new EventResult("You back away from the cache. Wisdom over hunger.", weight: 1.0, minutes: 3)
                ]);
    }

    /// <summary>
    /// Atmosphere event - signs of the cave's occupant.
    /// </summary>
    private static GameEvent BearSigns(GameContext ctx)
    {
        return new GameEvent("The Bear's Domain",
            "Claw marks score the walls, head-height for something massive. The musky smell is overpowering. This cave has been occupied for years.", 0.5)
            .Requires(EventCondition.Working)
            .OnlyAt("Bear Cave")
            .WithCooldown(12)
            .Choice("Study the Marks",
                "Learn about what lives here.",
                [
                    new EventResult("Deep gouges. Old and new, layered. A regular occupant — not just passing through.", weight: 0.60, minutes: 8),
                    new EventResult("The claw marks are fresh. Sap still oozes from the scratched wood. It was here recently.", weight: 0.40, minutes: 5)
                        .CreateTension("ClaimedTerritory", 0.3, animalType: "Bear", location: ctx.CurrentLocation)
                ])
            .Choice("Focus on Why You're Here",
                "You know it's dangerous. Keep moving.",
                [
                    new EventResult("You push past the animal signs. The cave has resources if you're brave enough to take them.", weight: 1.0, minutes: 2)
                ]);
    }

    /// <summary>
    /// Special encounter - hibernating bear. Different from awake encounter.
    /// High risk, high reward. Can pass by or attempt to kill sleeping bear.
    /// </summary>
    private static GameEvent HibernatingBear(GameContext ctx)
    {
        return new GameEvent("Winter Sleep",
            "In the deepest chamber — a mountain of fur, rising and falling with slow breaths. A bear in winter torpor. Vulnerable, but not defenseless.", 1.0)
            .Requires(EventCondition.Working)
            .OnlyAt("Bear Cave")
            .WithConditionFactor(EventCondition.ExtremelyCold, 2.0)  // More likely in deep winter
            .WithCooldown(168) // Once per week
            .Choice("Strike While It Sleeps",
                "One chance. Make it count.",
                [
                    new EventResult("Your weapon finds its heart. It doesn't wake. The cave is yours.", weight: 0.30, minutes: 10)
                        .FindsLargeMeat()
                        .AddsShelter(temp: 0.6, overhead: 0.9, wind: 0.8),
                    new EventResult("A glancing blow. It wakes — confused, then enraged.", weight: 0.50, minutes: 5)
                        .Encounter("Bear", 5, 0.9),
                    new EventResult("Your nerve fails. You can't do it.", weight: 0.20, minutes: 3)
                ])
            .Choice("Back Away Silently",
                "Let sleeping bears lie. Literally.",
                [
                    new EventResult("You retreat inch by inch. It doesn't stir.", weight: 0.85, minutes: 10),
                    new EventResult("A rock shifts underfoot. The bear's ears twitch. You freeze... it settles.", weight: 0.15, minutes: 15)
                ])
            .Choice("Use the Cave's Other Areas",
                "Work around it. Carefully.",
                [
                    new EventResult("You stick to the cave mouth. Limited space, but the bear doesn't wake.", weight: 0.70, minutes: 20),
                    new EventResult("A noise echoes. The bear shifts. Time to leave.", weight: 0.30, minutes: 8)
                ]);
    }

    // === BEAVER DAM EVENTS ===

    /// <summary>
    /// Atmosphere event - beaver activity visible.
    /// </summary>
    private static GameEvent BeaverActivity(GameContext ctx)
    {
        return new GameEvent("Dam Builders",
            "Splashing in the pond. A beaver surfaces, branch in its teeth, and swims toward the dam. The ecosystem is alive.", 0.4)
            .Requires(EventCondition.Working)
            .OnlyAt("Beaver Dam")
            .WithCooldown(6)
            .Choice("Watch Them Work",
                "There's something peaceful about it.",
                [
                    new EventResult("They weave branches, pack mud, repair gaps. Industrious creatures.", weight: 0.70, minutes: 10),
                    new EventResult("One slaps its tail — warning. They've spotted you. The pond goes silent.", weight: 0.30, minutes: 5)
                ])
            .Choice("Ignore Them",
                "You have your own work to do.",
                [
                    new EventResult("You gather what you need. The beavers keep their distance.", weight: 1.0, minutes: 3)
                ]);
    }

    /// <summary>
    /// Consequence event - dam starts to fail after heavy harvest.
    /// Triggers when beaver_dam harvestable is significantly depleted.
    /// </summary>
    private static GameEvent DamWeakening(GameContext ctx)
    {
        return new GameEvent("Weakening Structure",
            "Water seeps through gaps in the dam. The structure groans. You've taken too much — it's failing.", 0.8)
            .Requires(EventCondition.Working)
            .OnlyAt("Beaver Dam")
            .WithCooldown(24)
            .Choice("Shore It Up",
                "Pack mud into the gaps. Buy time.",
                [
                    new EventResult("You work frantically, packing debris into the holes. It holds — for now.", weight: 0.60, minutes: 30),
                    new EventResult("Too much damage. Water bursts through as you watch.", weight: 0.40, minutes: 15)
                        .CreateTension("DamCollapsed", 0.8, location: ctx.CurrentLocation)
                ])
            .Choice("Let It Go",
                "You needed the fuel. Accept the consequences.",
                [
                    new EventResult("The dam groans and buckles. Water rushes downstream. The pond drains.", weight: 1.0, minutes: 20)
                        .CreateTension("DamCollapsed", 1.0, location: ctx.CurrentLocation)
                ])
            .Choice("Salvage Quickly",
                "Take what's left before it's underwater.",
                [
                    new EventResult("You grab what you can as water rises around your ankles.", weight: 0.70, minutes: 15)
                        .FindsSupplies()
                        .CreateTension("DamCollapsed", 1.0, location: ctx.CurrentLocation),
                    new EventResult("Too slow. The flood catches you.", weight: 0.30, minutes: 10)
                        .ModerateCold()
                        .CreateTension("DamCollapsed", 1.0, location: ctx.CurrentLocation)
                ]);
    }

    /// <summary>
    /// Post-collapse event - the ecosystem changes.
    /// </summary>
    private static GameEvent DrainedPond(GameContext ctx)
    {
        var collapsed = ctx.Tensions.GetTension("DamCollapsed");
        if (collapsed == null) return new GameEvent("DrainedPond", "", 0);

        return new GameEvent("What's Left Behind",
            "Where the pond was, now mud and debris. Stranded fish, exposed roots. The beavers are gone.", 0.9)
            .Requires(EventCondition.Working)
            .OnlyAt("Beaver Dam")
            .WithCooldown(168)
            .Choice("Scavenge the Remains",
                "There's opportunity in destruction.",
                [
                    new EventResult("Fish flopping in the mud. Easy protein, for now.", weight: 0.60, minutes: 15)
                        .Rewards(RewardPool.SmallGame),
                    new EventResult("The mud is treacherous. You sink to your knees.", weight: 0.40, minutes: 25)
                        .WithEffects(Effects.EffectFactory.Exhausted(0.3, 30))
                ])
            .Choice("Survey the Damage",
                "Understand what you've done.",
                [
                    new EventResult("No more water source here. No more fuel. The ecosystem is dead.", weight: 1.0, minutes: 10)
                        .ResolveTension("DamCollapsed")
                ]);
    }

    /// <summary>
    /// Opportunity event - beaver lodge accessible after collapse.
    /// </summary>
    private static GameEvent ExposedLodge(GameContext ctx)
    {
        var collapsed = ctx.Tensions.GetTension("DamCollapsed");
        if (collapsed == null) return new GameEvent("ExposedLodge", "", 0);

        return new GameEvent("The Lodge",
            "With the water drained, the beaver lodge sits exposed — a mound of mud and sticks on dry ground.", 0.6)
            .Requires(EventCondition.Working)
            .OnlyAt("Beaver Dam")
            .WithCooldown(24)
            .Choice("Break Into the Lodge",
                "See what the beavers left behind.",
                [
                    new EventResult("A cache of winter stores — bark, roots. Not much, but something.", weight: 0.50, minutes: 20)
                        .FindsSupplies(),
                    new EventResult("Young beavers, abandoned when the water drained. Easy catch.", weight: 0.30, minutes: 15)
                        .Rewards(RewardPool.SmallGame),
                    new EventResult("Empty. The beavers took what they could when they fled.", weight: 0.20, minutes: 15)
                ])
            .Choice("Leave It",
                "You've done enough damage.",
                [
                    new EventResult("The lodge sits silent. A monument to what was.", weight: 1.0)
                ]);
    }

    // === TIER 4 LOCATION EVENTS ===

    // === THE LOOKOUT EVENTS ===

    /// <summary>
    /// Core event - climbing the lone pine for the view.
    /// High risk, high information reward.
    /// </summary>
    private static GameEvent ClimbTheLookout(GameContext ctx)
    {
        return new GameEvent("The Climb",
            "The branches form a natural ladder, but ice clings to the bark. The view from the top could show you the pass — and everything else.", 0.7)
            .Requires(EventCondition.Working)
            .OnlyAt("The Lookout")
            .WithCooldown(6)
            .Choice("Climb to the Top",
                "Risk the ascent. See everything.",
                [
                    new EventResult("Branch by branch, you rise above the treeline. The world spreads below.", weight: 0.55, minutes: 20)
                        .Chain(ViewFromAbove),
                    new EventResult("Ice gives way beneath your hand. You catch yourself, heart pounding.", weight: 0.25, minutes: 15)
                        .Frightening(),
                    new EventResult("A branch snaps. You fall, catching yourself painfully on lower limbs.", weight: 0.15, minutes: 10)
                        .WithEffects(Effects.EffectFactory.Bleeding(0.2))
                        .WithEffects(Effects.EffectFactory.SprainedAnkle(0.3)),
                    new EventResult("You lose your grip. The ground rushes up.", weight: 0.05, minutes: 5)
                        .WithEffects(Effects.EffectFactory.SprainedAnkle(0.6))
                        .WithEffects(Effects.EffectFactory.Bleeding(0.4))
                ])
            .Choice("Climb Partway",
                "Get some height without the worst risk.",
                [
                    new EventResult("Halfway up, you can see above most trees. Limited view, but safer.", weight: 0.80, minutes: 12),
                    new EventResult("Even this height is treacherous. A slip costs you skin and nerve.", weight: 0.20, minutes: 8)
                        .WithEffects(Effects.EffectFactory.Bleeding(0.1))
                ])
            .Choice("Stay on the Ground",
                "The view isn't worth a broken leg.",
                [
                    new EventResult("You work around the base. The rise gives some vantage even without climbing.", weight: 1.0, minutes: 5)
                ]);
    }

    /// <summary>
    /// Chained event - what you see from the top.
    /// </summary>
    private static GameEvent ViewFromAbove(GameContext ctx)
    {
        return new GameEvent("The View",
            "From the top of the pine, the world unfolds. The mountain pass is visible — snow-choked but there. You can see for miles in every direction.", 1.0)
            .Choice("Study the Pass",
                "This is what you came for.",
                [
                    new EventResult("The route is clear in your mind now. Snow still blocks it, but you can see where the path lies.", weight: 0.60, minutes: 15),
                    new EventResult("Movement on the lower slopes — game migrating. Opportunity if you can reach them.", weight: 0.40, minutes: 12)
                ])
            .Choice("Survey the Surroundings",
                "Map the terrain in your mind.",
                [
                    new EventResult("Smoke rising to the east — a hot spring? Movement in the southern forest. A frozen lake to the north.", weight: 0.50, minutes: 10),
                    new EventResult("Wolves, a pack of them, moving through the valley below. They haven't seen you. Yet.", weight: 0.30, minutes: 8)
                        .EscalatesPack(0.4),
                    new EventResult("Dark clouds building on the horizon. Storm coming.", weight: 0.20, minutes: 5)
                        .CreateTension("StormApproaching", 0.5)
                ])
            .Choice("Descend Quickly",
                "You've seen enough. Get down before conditions change.",
                [
                    new EventResult("You descend carefully. The knowledge gained is worth the risk taken.", weight: 0.85, minutes: 15),
                    new EventResult("Rushing, you slip. A painful landing, but nothing broken.", weight: 0.15, minutes: 10)
                        .WithEffects(Effects.EffectFactory.SprainedAnkle(0.2))
                ]);
    }

    /// <summary>
    /// Weather event - spot storm approaching from vantage point.
    /// </summary>
    private static GameEvent StormOnTheHorizon(GameContext ctx)
    {
        return new GameEvent("Storm Building",
            "From this height you can see it — a wall of grey sweeping across the landscape. Hours away, maybe less.", 0.5)
            .Requires(EventCondition.Working)
            .OnlyAt("The Lookout")
            .WithConditionFactor(EventCondition.WeatherWorsening, 3.0)
            .WithCooldown(4)
            .Choice("Time Your Return",
                "Use the warning. Get back to camp.",
                [
                    new EventResult("You head back with time to spare. The storm hits while you're safe at camp.", weight: 0.75, minutes: 10),
                    new EventResult("The storm moves faster than expected. You barely make it.", weight: 0.25, minutes: 15)
                        .LightChill()
                ])
            .Choice("Keep Working",
                "You have time. Probably.",
                [
                    new EventResult("You finish what you came for and leave with the wind rising.", weight: 0.50, minutes: 25)
                        .LightChill(),
                    new EventResult("You misjudged. The storm catches you exposed on the ridge.", weight: 0.50, minutes: 30)
                        .DangerousCold()
                ]);
    }

    /// <summary>
    /// Discovery event - see movement from the vantage point.
    /// </summary>
    private static GameEvent SpotFromHeight(GameContext ctx)
    {
        return new GameEvent("Movement Below",
            "From this elevation, you catch movement — shapes moving through the terrain below.", 0.4)
            .Requires(EventCondition.Working)
            .OnlyAt("The Lookout")
            .WithCooldown(4)
            .Choice("Watch and Wait",
                "Observe from safety.",
                [
                    new EventResult("Deer, picking through the snow. You note their path.", weight: 0.40, minutes: 10),
                    new EventResult("A lone wolf, hunting. It hasn't noticed you.", weight: 0.30, minutes: 8)
                        .BecomeStalked(0.2, "Wolf"),
                    new EventResult("Humans? No — just the way the trees move. Tricks of the light.", weight: 0.20, minutes: 5),
                    new EventResult("A bear, foraging along the ridgeline. Headed this way.", weight: 0.10, minutes: 6)
                        .BecomeStalked(0.3, "Bear")
                ])
            .Choice("Mark the Location",
                "Remember where the activity is.",
                [
                    new EventResult("You note landmarks. Useful for planning your next expedition.", weight: 1.0, minutes: 3)
                ]);
    }

    // === OLD CAMPSITE EVENTS ===

    /// <summary>
    /// Investigation event - search the remains.
    /// </summary>
    private static GameEvent InvestigateRemnants(GameContext ctx)
    {
        return new GameEvent("What Happened Here",
            "The more you look, the more questions arise. Someone survived here — for a while. Then they didn't.", 0.6)
            .Requires(EventCondition.Working)
            .OnlyAt("Old Campsite")
            .WithCooldown(24)
            .Choice("Search Thoroughly",
                "Turn over every stone.",
                [
                    new EventResult("Under debris — a bundle wrapped in hide. Tools, preserved.", weight: 0.30, minutes: 30)
                        .Rewards(RewardPool.ScrapTool),
                    new EventResult("Signs of struggle. Claw marks, dried blood. They were attacked.", weight: 0.35, minutes: 20)
                        .CreateTension("PredatorTerritory", 0.3),
                    new EventResult("An ordered departure. Whoever was here left deliberately, taking what mattered.", weight: 0.25, minutes: 25),
                    new EventResult("Nothing. Just the silence of abandonment.", weight: 0.10, minutes: 15)
                ])
            .Choice("Take What's Obvious",
                "Don't dwell on mysteries.",
                [
                    new EventResult("Surface salvage only. You don't want to know what happened.", weight: 1.0, minutes: 10)
                ]);
    }

    /// <summary>
    /// Discovery event - find a journal or message.
    /// </summary>
    private static GameEvent FindTheJournal(GameContext ctx)
    {
        return new GameEvent("A Record",
            "Scratched into bark, or charcoal on stone — marks. Deliberate. A message from someone who was here before.", 0.3)
            .Requires(EventCondition.Working)
            .OnlyAt("Old Campsite")
            .WithCooldown(168)
            .Choice("Study the Marks",
                "What were they trying to say?",
                [
                    new EventResult("A tally. Days survived. The marks stop suddenly — day forty-two.", weight: 0.30, minutes: 15),
                    new EventResult("Directions. Scratched arrows pointing east. 'Water' in crude symbols.", weight: 0.25, minutes: 12),
                    new EventResult("A warning. Teeth drawn beneath stick figures. Wolves. Many of them.", weight: 0.25, minutes: 10)
                        .EscalatesPack(0.4),
                    new EventResult("Names. Or what might be names. Whoever they were, they wanted to be remembered.", weight: 0.20, minutes: 8)
                ])
            .Choice("Leave It Unread",
                "Some stories don't need telling.",
                [
                    new EventResult("You turn away. Their fate doesn't have to be yours.", weight: 1.0)
                ]);
    }

    /// <summary>
    /// Tension event - whatever happened here might still be around.
    /// </summary>
    private static GameEvent WhatKilledThem(GameContext ctx)
    {
        var territory = ctx.Tensions.GetTension("PredatorTerritory");
        if (territory == null) return new GameEvent("WhatKilledThem", "", 0);

        return new GameEvent("Still Here",
            "A sound in the brush. The same thing that ended the last occupant might still be around.", 0.8)
            .Requires(EventCondition.Working)
            .OnlyAt("Old Campsite")
            .WithCooldown(6)
            .Choice("Face It",
                "Better to know what's hunting you.",
                [
                    new EventResult("A wolf emerges from the trees. It's been watching. It knows this place.", weight: 0.50, minutes: 5)
                        .Encounter("Wolf", 15, 0.6),
                    new EventResult("Nothing. Wind in the branches. Your nerves are shot.", weight: 0.35, minutes: 8)
                        .ResolveTension("PredatorTerritory"),
                    new EventResult("Eyes in the darkness, then gone. It's not ready to confront you. Yet.", weight: 0.15, minutes: 3)
                        .BecomeStalked(0.4, "Wolf")
                ])
            .Choice("Leave Immediately",
                "Don't become the next victim.",
                [
                    new EventResult("You abandon the site. Whatever it is, it's not following. For now.", weight: 0.80, minutes: 5)
                        .ResolveTension("PredatorTerritory"),
                    new EventResult("You hear it following as you leave. Not attacking — just watching.", weight: 0.20, minutes: 8)
                        .ResolveTension("PredatorTerritory")
                        .BecomeStalked(0.3, "Wolf")
                ]);
    }

    /// <summary>
    /// Shelter opportunity - the collapsed shelter can be rebuilt.
    /// </summary>
    private static GameEvent RebuildTheShelter(GameContext ctx)
    {
        return new GameEvent("Salvageable Shelter",
            "The old shelter is collapsed but the frame is intact. With work, it could be functional again.", 0.4)
            .Requires(EventCondition.Working, EventCondition.NoShelter)
            .OnlyAt("Old Campsite")
            .WithCooldown(24)
            .Choice("Repair It",
                "Use their foundation. Build on their work.",
                [
                    new EventResult("Hours of labor. You shore up the frame, patch the gaps. It's crude, but it's shelter.", weight: 0.70, minutes: 90)
                        .Costs(ResourceType.Fuel, 1)
                        .AddsShelter(temp: 0.3, overhead: 0.5, wind: 0.5),
                    new EventResult("The frame is too rotted. It collapses as you work. Time wasted.", weight: 0.20, minutes: 45)
                        .WithEffects(Effects.EffectFactory.Exhausted(0.3, 30)),
                    new EventResult("Structural failure. Part of the frame falls on you.", weight: 0.10, minutes: 30)
                        .WithEffects(Effects.EffectFactory.Exhausted(0.2, 20))
                        .WithEffects(Effects.EffectFactory.Bleeding(0.15))
                ])
            .Choice("Salvage for Materials",
                "The frame has good wood.",
                [
                    new EventResult("You break down the structure. Seasoned wood, already cut to length.", weight: 1.0, minutes: 30)
                        .FindsSupplies()
                ])
            .Choice("Leave It",
                "Not worth the effort.",
                [
                    new EventResult("You leave the collapsed shelter. Someone else's failure, not yours to fix.", weight: 1.0)
                ]);
    }
}
