using text_survival.Actors.Animals;

namespace text_survival.Actions.Variants;

/// <summary>
/// Hunting sighting variants that describe animal behavior states
/// and offer appropriate approach options.
/// </summary>
public record HuntingSighting(
    string Description,           // "Head down in the grass, oblivious"
    HuntingBehavior Behavior,     // What the animal is doing
    ApproachOption[] Options,     // Valid approaches for this behavior
    double DetectionModifier      // Multiplier on base detection chance
);

/// <summary>
/// Animal behavior categories for hunting encounters.
/// </summary>
public enum HuntingBehavior
{
    Grazing,      // Feeding, head down, easiest to approach
    Moving,       // On the move, more alert
    Resting,      // Bedded down, relaxed but watchful
    Alert,        // Nervous, scanning, very difficult
    Distracted,   // Focused on something else (fighting, mating)
    Wounded       // Injured, slower but may be dangerous
}

/// <summary>
/// An approach option for hunting based on animal behavior.
/// </summary>
public record ApproachOption(
    string Name,              // "Slow Stalk"
    string Description,       // "Move carefully between cover"
    int TimeMinutes,          // How long this takes
    double SuccessModifier,   // Multiplier on success chance (1.0 = baseline)
    string SuccessNarrative,  // "You close the distance unnoticed"
    string FailureNarrative   // "A twig snaps. It bolts."
);

/// <summary>
/// Predefined hunting sighting libraries organized by behavior.
/// </summary>
public static class HuntingSightings
{
    // ============ GRAZING ENCOUNTERS ============
    // Animal is feeding, head down, easiest to approach
    public static readonly HuntingSighting[] GrazingEncounters =
    [
        new("Head down in the grass, oblivious",
            HuntingBehavior.Grazing,
            [
                new("Slow Stalk", "Move carefully between cover", 15, 1.3,
                    "You close the distance unnoticed.", "Your shadow falls across it. Gone."),
                new("Circle Downwind", "Take time to approach from downwind", 25, 1.5,
                    "The wind carries your scent away. Perfect approach.", "Wind shifts. It catches your scent."),
                new("Direct Approach", "Move quickly while it's distracted", 8, 0.8,
                    "You cover ground fast. Close enough.", "Too bold. It spots movement."),
            ],
            0.7),

        new("Pawing at snow, searching for food",
            HuntingBehavior.Grazing,
            [
                new("Wait for Head Down", "Patience. Move only when it digs", 20, 1.4,
                    "You time your approach perfectly.", "It looks up at the wrong moment."),
                new("Use Terrain", "Approach using a ridge for cover", 18, 1.2,
                    "The ridge hides your approach.", "You crest too early. Spotted."),
            ],
            0.75),

        new("Grazing at the edge of cover",
            HuntingBehavior.Grazing,
            [
                new("Creep Through Brush", "Use the vegetation to hide", 20, 1.35,
                    "You emerge within striking range.", "Branches rustle. It freezes, then bolts."),
                new("Flank Wide", "Circle to approach from the forest side", 25, 1.25,
                    "You close from an unexpected angle.", "It senses movement in the trees."),
            ],
            0.65),
    ];

    // ============ ALERT ENCOUNTERS ============
    // Animal is nervous, scanning, very difficult to approach
    public static readonly HuntingSighting[] AlertEncounters =
    [
        new("Ears up, scanning. Something has it nervous.",
            HuntingBehavior.Alert,
            [
                new("Freeze", "Don't move. Wait for it to relax.", 10, 0.6,
                    "It settles. You have a chance.", "It doesn't settle. It runs."),
                new("Distraction", "Throw something to draw its attention away", 5, 0.7,
                    "It looks the other way. You move.", "It looks toward the sound. And sees you."),
            ],
            2.0),

        new("Standing still, testing the air.",
            HuntingBehavior.Alert,
            [
                new("Wait It Out", "This one is too alert. Let it calm down.", 15, 0.5,
                    "Finally, it relaxes. Opportunity.", "It never relaxes. It leaves."),
                new("Commit", "It's now or never. Take the shot.", 2, 0.5,
                    "Your throw is true.", "It was ready. Already moving when you threw."),
            ],
            2.5),

        new("Head high, body tense.",
            HuntingBehavior.Alert,
            [
                new("Abort", "This one knows something's wrong. Find another.", 0, 0,
                    "Smart choice. You back off.", ""),
                new("Stone Still", "Become part of the landscape.", 12, 0.55,
                    "It decides the threat has passed.", "Your luck runs out. It spots you."),
            ],
            2.2),
    ];

    // ============ RESTING ENCOUNTERS ============
    // Animal is bedded down, relaxed but not oblivious
    public static readonly HuntingSighting[] RestingEncounters =
    [
        new("Bedded down, chewing cud.",
            HuntingBehavior.Resting,
            [
                new("Patient Approach", "Let it settle deeper into rest", 30, 1.4,
                    "It dozes. You're almost on top of it.", "It shifts, looks around. Sees you."),
                new("Quick While Resting", "It won't stay down forever", 12, 1.0,
                    "You make good time while it rests.", "It stands up just as you clear the brush."),
            ],
            0.9),

        new("Lying in the snow, eyes half-closed.",
            HuntingBehavior.Resting,
            [
                new("Low Crawl", "Belly to the ground, inch forward", 25, 1.35,
                    "You're close. It never saw you coming.", "Your movement disturbs the snow. It wakes."),
                new("Wait for Sleep", "Let it drift off completely", 35, 1.5,
                    "It's truly asleep now. Easy approach.", "It rests but never sleeps."),
            ],
            0.85),
    ];

    // ============ WOUNDED ENCOUNTERS ============
    // Animal is injured, easier but potentially dangerous
    public static readonly HuntingSighting[] WoundedEncounters =
    [
        new("Favoring one leg, moving slow.",
            HuntingBehavior.Wounded,
            [
                new("Press Advantage", "It can't run well. Close fast.", 8, 1.5,
                    "It tries to flee but can't escape.", "Even wounded, it's faster than expected."),
                new("Follow Blood Trail", "Let it tire itself out.", 25, 1.8,
                    "You find it collapsed ahead.", "The trail goes cold. Lost it."),
            ],
            0.5),

        new("Limping badly, leaving blood spots.",
            HuntingBehavior.Wounded,
            [
                new("Approach Cautiously", "Wounded animals can be dangerous", 12, 1.4,
                    "It can't fight back. Clean kill.", "It turns on you with desperate strength."),
                new("Wait at Distance", "Let blood loss do the work", 30, 1.6,
                    "It collapses from the wound.", "It finds cover and disappears."),
            ],
            0.45),
    ];

    // ============ DISTRACTED ENCOUNTERS ============
    // Animal focused on something else
    public static readonly HuntingSighting[] DistractedEncounters =
    [
        new("Two animals sparring, focused on each other.",
            HuntingBehavior.Distracted,
            [
                new("Wait for Opportunity", "One might present an opening", 15, 1.3,
                    "One turns away. Clear shot.", "They break apart and scatter."),
                new("Target the Loser", "The one losing will be exhausted", 20, 1.4,
                    "The beaten one doesn't see you coming.", "They both flee when you move."),
            ],
            0.6),

        new("Focused on something in the distance.",
            HuntingBehavior.Distracted,
            [
                new("Use Its Distraction", "Move while its attention is elsewhere", 10, 1.25,
                    "You close while it stares at nothing.", "It turns at the wrong moment."),
                new("Check What It Sees", "Its distraction might be a predator", 8, 0.9,
                    "Just a bird. Safe to approach.", "A wolf. You back off quietly."),
            ],
            0.7),
    ];

    // ============ MOVING ENCOUNTERS ============
    // Animal on the move, moderately alert
    public static readonly HuntingSighting[] MovingEncounters =
    [
        new("Crossing between feeding areas.",
            HuntingBehavior.Moving,
            [
                new("Intercept Route", "Get ahead of its path", 15, 1.1,
                    "You're in position when it arrives.", "It takes a different path."),
                new("Parallel Track", "Match its pace, close gradually", 20, 1.2,
                    "You close the distance unnoticed.", "It senses something and speeds up."),
            ],
            1.2),

        new("Heading somewhere with purpose.",
            HuntingBehavior.Moving,
            [
                new("Cut It Off", "Sprint to get ahead", 8, 0.9,
                    "You beat it to the chokepoint.", "You're out of breath and it sees you."),
                new("Follow to Destination", "See where it's going", 25, 1.3,
                    "It leads you to a bedding area. Easy approach.", "It leads you far from familiar ground."),
            ],
            1.3),
    ];
}

/// <summary>
/// Selects appropriate hunting sightings based on animal state.
/// </summary>
public static class HuntingSightingSelector
{
    /// <summary>
    /// Select a sighting description based on animal's current state.
    /// </summary>
    public static HuntingSighting SelectForAnimal(Animal animal, GameContext ctx)
    {
        // Map animal's current activity to behavior category
        var behavior = MapActivityToBehavior(animal);

        // Get appropriate pool
        var pool = behavior switch
        {
            HuntingBehavior.Grazing => HuntingSightings.GrazingEncounters,
            HuntingBehavior.Alert => HuntingSightings.AlertEncounters,
            HuntingBehavior.Resting => HuntingSightings.RestingEncounters,
            HuntingBehavior.Wounded => HuntingSightings.WoundedEncounters,
            HuntingBehavior.Distracted => HuntingSightings.DistractedEncounters,
            HuntingBehavior.Moving => HuntingSightings.MovingEncounters,
            _ => HuntingSightings.GrazingEncounters
        };

        // Random selection from pool
        return pool[Random.Shared.Next(pool.Length)];
    }

    /// <summary>
    /// Map Animal's activity state to hunting behavior category.
    /// </summary>
    public static HuntingBehavior MapActivityToBehavior(Animal animal)
    {
        // Check for wounded first (overrides activity)
        if (animal.WoundedTime != null)
            return HuntingBehavior.Wounded;

        // Check nervousness level (high nervousness = alert)
        if (animal.Nervousness > 0.7)
            return HuntingBehavior.Alert;

        // Map from Animal's CurrentActivity
        return animal.CurrentActivity switch
        {
            AnimalActivity.Grazing => HuntingBehavior.Grazing,
            AnimalActivity.Alert => HuntingBehavior.Alert,
            AnimalActivity.Resting => HuntingBehavior.Resting,
            AnimalActivity.Moving => HuntingBehavior.Moving,
            _ => HuntingBehavior.Grazing  // Default to grazing
        };
    }

    /// <summary>
    /// Get the detection modifier based on sighting behavior.
    /// </summary>
    public static double GetDetectionModifier(HuntingSighting sighting)
    {
        return sighting.DetectionModifier;
    }

    /// <summary>
    /// Calculate approach success chance with modifiers.
    /// </summary>
    public static double CalculateApproachSuccess(
        ApproachOption option,
        HuntingSighting sighting,
        GameContext ctx)
    {
        // Base success from option modifier
        double chance = 0.5 * option.SuccessModifier;

        // Apply behavior detection modifier (lower = easier)
        chance /= sighting.DetectionModifier;

        // Player impairment affects stealth
        var caps = ctx.player.GetCapacities();
        chance *= caps.Moving;      // Movement affects sneaking
        chance *= caps.Sight;       // Need to see where you're stepping

        // Clamp to reasonable range
        return Math.Clamp(chance, 0.1, 0.9);
    }

    /// <summary>
    /// Get a hint about the animal's behavior for the player.
    /// </summary>
    public static string GetBehaviorHint(HuntingBehavior behavior)
    {
        return behavior switch
        {
            HuntingBehavior.Grazing => "Feeding animals are distracted. Best opportunity.",
            HuntingBehavior.Moving => "Moving targets require interception or patience.",
            HuntingBehavior.Resting => "Resting animals may seem easy, but they're watchful.",
            HuntingBehavior.Alert => "This one suspects something. Risky to approach.",
            HuntingBehavior.Distracted => "Something has its attention. Use that.",
            HuntingBehavior.Wounded => "Easier target, but cornered animals fight.",
            _ => "Study its behavior before moving."
        };
    }
}
