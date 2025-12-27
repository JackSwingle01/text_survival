namespace text_survival.Actions;

/// <summary>
/// First-visit events that trigger immediately on arriving at a location.
/// These are called directly via Location.FirstVisitEvent, not through the normal event pool.
/// </summary>
public static partial class GameEventRegistry
{
    // === FIRST-VISIT EVENTS ===
    // These events fire once, on first arrival at a location.
    // They create immediate discovery moments before normal exploration begins.

    /// <summary>
    /// First visit to The Lookout - the vantage point reveals the landscape.
    /// </summary>
    public static GameEvent? FirstVisitLookout(GameContext ctx)
    {
        return new GameEvent("Vantage Point",
            "From this height, the landscape unfolds below. The mountain pass is visible to the north — snow-choked but there. Smoke rises in the distance. Another fire, or another survivor?", 0.9)
            .Choice("Study the smoke",
                "You memorize the direction. It's far, but reachable.",
                [
                    new EventResult("A new possibility — or a new danger. You mark the direction.", weight: 0.7, minutes: 8)
                        .CreateTension("MarkedDiscovery", 0.3),
                    new EventResult("The smoke fades as you watch. Wind scatters it. The source could be anywhere.", weight: 0.3, minutes: 5)
                ])
            .Choice("Focus on the terrain",
                "Scan for game trails and water sources.",
                [
                    new EventResult("The land reveals its patterns. Movement in the southern trees — game. A glint to the east — water.", weight: 1.0, minutes: 10)
                ]);
    }

    /// <summary>
    /// First visit to Bear Cave - the scale of the place is overwhelming.
    /// </summary>
    public static GameEvent? FirstVisitBearCave(GameContext ctx)
    {
        return new GameEvent("The Dark Mouth",
            "The cave entrance looms — a wound in the mountainside. Claw marks score the rock at the threshold. The musky smell is overpowering. Something large claims this place.", 1.0)
            .Choice("Enter carefully",
                "Move slowly. Let your eyes adjust.",
                [
                    new EventResult("Darkness swallows you. The cave goes deeper than you expected. Anything could be in here.", weight: 0.7, minutes: 5)
                        .CreateTension("ClaimedTerritory", 0.3, animalType: "Bear", location: ctx.CurrentLocation),
                    new EventResult("Your footstep echoes. Something shifts in the darkness ahead.", weight: 0.3, minutes: 3)
                        .CreateTension("ClaimedTerritory", 0.5, animalType: "Bear", location: ctx.CurrentLocation)
                ])
            .Choice("Stay near the entrance",
                "No need to push deeper. Not yet.",
                [
                    new EventResult("You linger at the threshold, letting light guide you. The cave's occupant, if any, doesn't show itself.", weight: 1.0, minutes: 3)
                ]);
    }

    /// <summary>
    /// First visit to Hot Spring - the unexpected warmth.
    /// </summary>
    public static GameEvent? FirstVisitHotSpring(GameContext ctx)
    {
        return new GameEvent("Unexpected Warmth",
            "Steam rises from the pool, curling in the cold air. The water is warm — genuinely warm. In all this ice and stone, a pocket of heat.", 0.9)
            .Choice("Test the water",
                "Is it safe?",
                [
                    new EventResult("Hot, but not scalding. Your frozen fingers tingle as warmth seeps in. This place is a sanctuary.", weight: 0.8, minutes: 5)
                        .WithEffects(Effects.EffectFactory.Warmed(0.3, 30)),
                    new EventResult("Too hot at the source. But the edges are perfect. You could rest here.", weight: 0.2, minutes: 8)
                        .WithEffects(Effects.EffectFactory.Warmed(0.4, 45))
                ])
            .Choice("Survey the area",
                "Warmth attracts more than just you.",
                [
                    new EventResult("Tracks in the mud. Animals come here too. A hunting ground, and a refuge.", weight: 1.0, minutes: 5)
                ]);
    }

    /// <summary>
    /// First visit to Old Campsite - the weight of someone else's failure.
    /// </summary>
    public static GameEvent? FirstVisitOldCampsite(GameContext ctx)
    {
        return new GameEvent("Someone Else's Story",
            "The camp is overgrown but unmistakable. A fire pit, cold and old. A collapsed lean-to. Someone survived here. The question is — for how long?", 0.8)
            .Choice("Search for signs",
                "What happened to them?",
                [
                    new EventResult("A tally scratched into bark. Days survived. The count stops at thirty-seven.", weight: 0.5, minutes: 10),
                    new EventResult("Bones, scattered. Something found them. Something with teeth.", weight: 0.3, minutes: 8)
                        .CreateTension("PredatorTerritory", 0.2),
                    new EventResult("An orderly departure. Whoever was here, they left on their own terms.", weight: 0.2, minutes: 6)
                ])
            .Choice("Make it yours",
                "Their failure doesn't have to be yours.",
                [
                    new EventResult("You assess what's salvageable. There's work to do, but this could be a second camp.", weight: 1.0, minutes: 5)
                ]);
    }

    /// <summary>
    /// First visit to Ancient Grove - the silence is unsettling.
    /// </summary>
    public static GameEvent? FirstVisitAncientGrove(GameContext ctx)
    {
        return new GameEvent("The Old Forest",
            "The trees here are immense — older than memory. Snow muffles everything. No birds sing. No wind stirs the branches. The silence presses on your ears.", 0.8)
            .Choice("Listen",
                "The quiet must mean something.",
                [
                    new EventResult("Just silence. Ancient, patient silence. You're alone here — and somehow that's worse.", weight: 0.7, minutes: 5)
                        .WithEffects(Effects.EffectFactory.Shaken(0.1)),
                    new EventResult("There — the faintest crack. A branch, somewhere deep in the grove. Something else is here.", weight: 0.3, minutes: 3)
                        .BecomeStalked(0.2)
                ])
            .Choice("Press forward",
                "Silence is just silence.",
                [
                    new EventResult("You push past the feeling. The grove offers shelter, fuel, maybe game. That's what matters.", weight: 1.0, minutes: 2)
                ]);
    }

    /// <summary>
    /// First visit to Rocky Ridge - the brutal exposure.
    /// </summary>
    public static GameEvent? FirstVisitRockyRidge(GameContext ctx)
    {
        return new GameEvent("Exposed",
            "Wind screams across the bare stone. Both valleys spread below — endless white and dark forest. Beautiful, and utterly unforgiving.", 0.9)
            .Choice("Find shelter in the rocks",
                "You need to get out of this wind.",
                [
                    new EventResult("A gap in the boulders breaks the worst of it. You can work here, but not for long.", weight: 0.8, minutes: 5),
                    new EventResult("No shelter. The ridge is exposed from every angle.", weight: 0.2, minutes: 3)
                        .LightChill()
                ])
            .Choice("Take in the view",
                "You came all this way. See what there is to see.",
                [
                    new EventResult("The cold bites, but the view is worth it. You can see for miles — every route, every danger, every opportunity.", weight: 1.0, minutes: 8)
                        .LightChill()
                ]);
    }

    /// <summary>
    /// First visit to Peat Bog - ancient fuel source in treacherous terrain.
    /// </summary>
    public static GameEvent? FirstVisitPeatBog(GameContext ctx)
    {
        return new GameEvent("Ancient Fuel",
            "The ground gives slightly with each step. Dark water seeps up around your feet. But beneath this treacherous surface lies centuries of compressed plant matter — fuel that burns slow and hot.", 0.9)
            .Choice("Test the footing",
                "Can you work here safely?",
                [
                    new EventResult("Careful probing reveals the solid paths. You can work here — but one wrong step could cost you.", weight: 0.7, minutes: 8),
                    new EventResult("Your foot punches through. Cold water floods your boot. The bog takes what it wants.", weight: 0.3, minutes: 5)
                        .LightChill()
                        .WithEffects(Effects.EffectFactory.Wet(0.15))
                ])
            .Choice("Start digging",
                "The peat is what matters.",
                [
                    new EventResult("You find solid ground and start working. Dark blocks of peat come free, dense and promising.", weight: 1.0, minutes: 10)
                ]);
    }

    /// <summary>
    /// First visit to Ice Shelf - frozen vantage and predator refuge.
    /// </summary>
    public static GameEvent? FirstVisitIceShelf(GameContext ctx)
    {
        return new GameEvent("Frozen Sentinel",
            "The climb is hard but possible. At the top, the wind hits you full force. But the view — you can see everything. The entire valley spreads below. Nothing could reach you here.", 0.9)
            .Choice("Survey the land",
                "See what's moving out there.",
                [
                    new EventResult("Movement in the southern trees. Something large. Good to know.", weight: 0.6, minutes: 10)
                        .LightChill()
                        .BecomeStalked(0.15),
                    new EventResult("Nothing moves. The world is still. You're alone — for now.", weight: 0.4, minutes: 8)
                        .LightChill()
                ])
            .Choice("Get back down",
                "The cold is too much. You've seen enough.",
                [
                    new EventResult("You descend before the cold takes hold. But you know the way now.", weight: 1.0, minutes: 5)
                ]);
    }

    /// <summary>
    /// First visit to Bone Hollow - mammoth graveyard with premium materials.
    /// </summary>
    public static GameEvent? FirstVisitBoneHollow(GameContext ctx)
    {
        return new GameEvent("The Elephant Graveyard",
            "You stand among giants. Skulls the size of boulders. Tusks longer than you are tall. The bones are old — centuries, maybe — but some are fresh. Mammoths still come here. Why?", 1.0)
            .Choice("Examine the fresh bones",
                "What killed it? How long ago?",
                [
                    new EventResult("Weeks, not months. No wounds — it came here to die. Like the others. Something draws them.", weight: 0.7, minutes: 15)
                        .CreateTension("MegafaunaTerritory", 0.3),
                    new EventResult("Tooth marks on the bones. Wolves found it after. But the mammoth died on its own terms.", weight: 0.3, minutes: 12)
                        .CreateTension("PredatorTerritory", 0.2)
                ])
            .Choice("Harvest what you can",
                "This is a windfall. Don't waste it.",
                [
                    new EventResult("You work until your arms ache. Bone, ivory, marrow. A fortune in materials.", weight: 1.0, minutes: 30)
                        .Rewards(Items.RewardPool.BoneHarvest)
                ]);
    }

    /// <summary>
    /// First visit to Wind Gap - dangerous mountain passage.
    /// </summary>
    public static GameEvent? FirstVisitWindGap(GameContext ctx)
    {
        return new GameEvent("The Wind Gap",
            "Wind screams through the notch, funneled and amplified. Snow flies horizontal. The passage is brutal — but beyond it, a whole section of the valley you couldn't reach otherwise.", 0.9)
            .Choice("Push through",
                "Brace yourself. It's just wind.",
                [
                    new EventResult("You lower your head and push. The cold cuts through everything. But you make it through.", weight: 0.5, minutes: 25)
                        .ModerateCold(),
                    new EventResult("The wind catches you wrong. You go down hard on the rocks. By the time you crawl clear, you're half-frozen.", weight: 0.3, minutes: 20)
                        .SevereCold()
                        .DamageWithVariant(Variants.AccidentVariants.RockyTerrain[0]),
                    new EventResult("The wind drives you back. You can't breathe, can't see. You stagger back the way you came.", weight: 0.2, minutes: 15)
                        .LightChill()
                ])
            .Choice("Go around",
                "The long way is the safe way.",
                [
                    new EventResult("You turn back. The mountain will have to wait.", weight: 1.0, minutes: 5)
                ]);
    }

    /// <summary>
    /// First visit to Snowfield Hollow - natural game crossing.
    /// </summary>
    public static GameEvent? FirstVisitSnowfieldHollow(GameContext ctx)
    {
        return new GameEvent("The Crossing",
            "The hollow opens before you. Fresh tracks everywhere — rabbit, ptarmigan, fox. This is a crossing point. Animals use it constantly. You crouch at the rim and watch. Within ten minutes, a rabbit darts across the bare ground. Then another. They follow the same path.", 0.9)
            .Choice("Study the patterns",
                "Learn where to place traps.",
                [
                    new EventResult("You trace the paths, note where they converge. The drift creates a natural funnel — place traps in the channel and they'll walk right into them.", weight: 1.0, minutes: 15)
                        .LightChill()
                ])
            .Choice("Note the location",
                "You've seen enough. Move on.",
                [
                    new EventResult("Good hunting ground. You'll remember this place.", weight: 1.0, minutes: 3)
                ]);
    }

    /// <summary>
    /// First visit to Sun-Warmed Cliff - discovering solar warmth.
    /// </summary>
    public static GameEvent? FirstVisitSunWarmedCliff(GameContext ctx)
    {
        return new GameEvent("Borrowed Heat",
            "The stone is warm under your palm. Not just less cold — genuinely warm. The south face of the cliff has been drinking sunlight all morning. In the lee of the wind, you could almost forget it's winter.", 0.9)
            .Choice("Rest against the stone",
                "Let the warmth soak in.",
                [
                    new EventResult("You lean back and close your eyes. The stone radiates heat into your shoulders, your spine. For a few minutes, you're not surviving — you're just warm.", weight: 0.7, minutes: 15)
                        .WithEffects(Effects.EffectFactory.Warmed(0.25, 30)),
                    new EventResult("You doze. When you jerk awake, shadows have crept across the cliff face. The warmth is fading. You've lost time — but you needed the rest.", weight: 0.3, minutes: 45)
                        .WithEffects(Effects.EffectFactory.Warmed(0.35, 45))
                ])
            .Choice("Survey the area",
                "Warmth is temporary. Information lasts.",
                [
                    new EventResult("You note the angle of exposure, where the shadows fall, which crevices stay sunny longest. This is a resource — but only if you know when to use it.", weight: 1.0, minutes: 8)
                ]);
    }
}
