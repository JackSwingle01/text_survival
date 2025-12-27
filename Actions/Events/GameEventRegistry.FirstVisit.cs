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
}
