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
            .WithLocationNameRequirement("Burnt Stand")
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
            .WithLocationNameRequirement("Burnt Stand")
            .WithCooldown(2)
            .Choice("Hold Your Ground",
                "Face it. Show no fear.",
                [
                    new EventResult($"The {predator.ToLower()} watches. You watch back. A standoff in the ash.", weight: 0.60, minutes: 10)
                        .Escalate("Stalked", 0.1),
                    new EventResult($"Your stance unsettles it. The {predator.ToLower()} circles wider, keeping distance.", weight: 0.40, minutes: 8)
                        .Escalate("Stalked", -0.1)
                ])
            .Choice("Back Away Slowly",
                "Create distance. Don't run.",
                [
                    new EventResult("You retreat step by step. It doesn't follow. Yet.", weight: 0.70, minutes: 15),
                    new EventResult($"Your movement triggers something. The {predator.ToLower()} starts toward you.", weight: 0.30, minutes: 5)
                        .Escalate("Stalked", 0.3)
                ])
            .Choice("Use the Visibility",
                "If you can see it, you can prepare.",
                [
                    new EventResult($"You track its position while gathering what you need. Knowledge is power.", weight: 0.80, minutes: 12),
                    new EventResult($"Distracted by watching it, you stumble. The {predator.ToLower()} notices.", weight: 0.20, minutes: 8)
                        .Escalate("Stalked", 0.2)
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
            .Requires(EventCondition.Working)
            .WithLocationTagRequirement("[Treacherous]")
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
            .Requires(EventCondition.Working)
            .WithLocationTagRequirement("[Fuel]")
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
            .WithLocationNameRequirement("Rock Overhang")
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
    /// Fire management event - smoke builds up under overhang.
    /// </summary>
    private static GameEvent SmokeBuildsUp(GameContext ctx)
    {
        return new GameEvent("Smoke Building",
            "Smoke pools under the stone lip. Your eyes sting. The air is getting thick.", 0.5)
            .Requires(EventCondition.NearFire)
            .WithLocationNameRequirement("Rock Overhang")
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
            .WithLocationNameRequirement("Granite Outcrop")
            .WithConditionFactor(EventCondition.Stalked, 3.0)
            .WithCooldown(2)
            .Choice("Study It",
                "Use your vantage. Learn what you're dealing with.",
                [
                    new EventResult("A wolf, circling. Now you know where it is.", weight: 0.50, minutes: 10)
                        .CreateTension("Stalked", 0.3, animalType: "Wolf"),
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
            .WithLocationNameRequirement("Granite Outcrop")
            .WithCooldown(4)
            .Choice("Use the Advantage",
                "You have the high ground. Act like it.",
                [
                    new EventResult($"You stand tall. The {predator.ToLower()} hesitates. Prey doesn't act like this.", weight: 0.60, minutes: 5)
                        .Escalate("Stalked", -0.2),
                    new EventResult($"It's not intimidated. The {predator.ToLower()} circles, looking for a path up.", weight: 0.40, minutes: 8)
                        .Escalate("Stalked", 0.1)
                ])
            .Choice("Descend Carefully",
                "You can't stay up here forever.",
                [
                    new EventResult($"You descend the far side. The {predator.ToLower()} loses sight of you.", weight: 0.50, minutes: 12),
                    new EventResult($"It tracks your descent. When you reach the bottom, it's waiting.", weight: 0.50, minutes: 15)
                        .Escalate("Stalked", 0.3)
                ])
            .Choice("Throw Something",
                "Maybe you can drive it off.",
                [
                    new EventResult($"A rock clatters near it. The {predator.ToLower()} flinches and retreats into the trees.", weight: 0.45, minutes: 3)
                        .Escalate("Stalked", -0.1),
                    new EventResult($"Your throw falls short. The {predator.ToLower()} doesn't even react.", weight: 0.35, minutes: 3),
                    new EventResult($"The motion catches its attention. It advances.", weight: 0.20, minutes: 2)
                        .Escalate("Stalked", 0.2)
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
            .WithLocationNameRequirement("Meltwater Pool")
            .WithCooldown(4)
            .Choice("Fill Containers Quickly",
                "Grab what you can and run.",
                [
                    new EventResult("You fill half your containers and flee. Better than nothing.", weight: 0.70, minutes: 8),
                    new EventResult("The storm catches you mid-fill. Ice stings your face as you run.", weight: 0.30, minutes: 12)
                        .WithEffects(Effects.EffectFactory.Cold(0.3, 30))
                ])
            .Choice("Get Full Water Properly",
                "You came all this way. Take the risk.",
                [
                    new EventResult("You finish just as the first flakes hit. Worth it.", weight: 0.40, minutes: 20),
                    new EventResult("The storm hits while you're still filling. Brutal exposure.", weight: 0.60, minutes: 25)
                        .WithEffects(Effects.EffectFactory.Cold(0.5, 60))
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
            .WithLocationNameRequirement("Ancient Grove")
            .WithCooldown(6)
            .Choice("Listen Carefully",
                "Something might be out there.",
                [
                    new EventResult("Just silence. Ancient, patient silence. You're alone here.", weight: 0.70, minutes: 5),
                    new EventResult("There — the faintest crack of a branch. Something is here.", weight: 0.30, minutes: 8)
                        .CreateTension("Stalked", 0.2)
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
            .WithLocationNameRequirement("Ancient Grove")
            .WithCooldown(24)
            .Choice("Look for Deadfall",
                "There must be something already down.",
                [
                    new EventResult("Precious little. This forest is too healthy — everything is either standing or rotted.", weight: 0.70, minutes: 15),
                    new EventResult("A fallen branch, storm-snapped. Not much, but it's something.", weight: 0.30, minutes: 12)
                        .Rewards(RewardPool.BasicSupplies)
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
            .WithLocationNameRequirement("Flint Seam")
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
    /// Timing event - fresh tracks indicate peak activity.
    /// </summary>
    private static GameEvent FreshTracks(GameContext ctx)
    {
        return new GameEvent("Fresh Tracks",
            "Hoofprints in the mud, still filling with water. They passed through moments ago.", 0.5)
            .Requires(EventCondition.Working)
            .WithLocationNameRequirement("Game Trail")
            .WithCooldown(4)
            .Choice("Follow Them",
                "They can't be far.",
                [
                    new EventResult("You track them to a clearing. Deer, grazing. An opportunity.", weight: 0.50, minutes: 20),
                    new EventResult("The trail goes cold. They're faster than you.", weight: 0.35, minutes: 15),
                    new EventResult("You find them — but a wolf found them first. It looks up from its kill.", weight: 0.15, minutes: 12)
                        .CreateTension("Stalked", 0.4, animalType: "Wolf")
                ])
            .Choice("Wait Here",
                "They might come back.",
                [
                    new EventResult("You settle in. The trail sees regular traffic.", weight: 1.0, minutes: 5)
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
            .WithLocationNameRequirement("Dense Thicket")
            .WithCooldown(4)
            .Choice("Push Deeper",
                "Put more brush between you.",
                [
                    new EventResult($"Branches tear at you, but the {predator.ToLower()} falls back. You're safe.", weight: 0.85, minutes: 15)
                        .ResolveTension("Stalked"),
                    new EventResult("The thicket is impassable here. You have to go around.", weight: 0.15, minutes: 20)
                ])
            .Choice("Wait It Out",
                "It has to give up eventually.",
                [
                    new EventResult($"Minutes pass. The {predator.ToLower()} paces, then leaves. You're safe.", weight: 0.70, minutes: 30)
                        .ResolveTension("Stalked"),
                    new EventResult($"It's patient. The {predator.ToLower()} settles in to wait. So are you.", weight: 0.30, minutes: 45)
                        .Escalate("Stalked", -0.1)
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
            .WithLocationNameRequirement("Dense Thicket")
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
            .WithLocationNameRequirement("Boulder Field")
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
            .WithLocationNameRequirement("Rocky Ridge")
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
            .WithLocationNameRequirement("Rocky Ridge")
            .WithCooldown(2)
            .Choice("Find Shelter in the Rocks",
                "Duck behind a boulder.",
                [
                    new EventResult("A gap in the rocks breaks the wind. Temporary relief.", weight: 0.75, minutes: 10),
                    new EventResult("Exposed from all angles. No shelter here.", weight: 0.25, minutes: 5)
                        .WithEffects(Effects.EffectFactory.Cold(0.4, 20))
                ])
            .Choice("Push Through",
                "Finish what you came for.",
                [
                    new EventResult("You grit your teeth and work. Cold seeps into your bones.", weight: 0.60, minutes: 15)
                        .WithEffects(Effects.EffectFactory.Cold(0.3, 30)),
                    new EventResult("Too much. You have to retreat.", weight: 0.40, minutes: 8)
                        .WithEffects(Effects.EffectFactory.Cold(0.5, 20))
                ])
            .Choice("Descend Immediately",
                "This isn't worth frostbite.",
                [
                    new EventResult("You scramble down to the treeline. Warmer here.", weight: 1.0, minutes: 12)
                ]);
    }
}
