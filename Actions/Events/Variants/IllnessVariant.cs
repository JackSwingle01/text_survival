using text_survival.Effects;
using text_survival.Environments.Features;

namespace text_survival.Actions.Variants;

/// <summary>
/// Bundles illness onset text with matched cause and initial effects.
/// Connects visible symptoms to underlying cause for player learning.
/// </summary>
public record IllnessOnsetVariant(
    string Description,           // "Your wound is hot to the touch. Something's wrong."
    string Cause,                 // "wound", "exposure", "contamination", "exhaustion"
    Effect[] InitialEffects,      // Effects applied at onset
    double SeverityMultiplier = 1.0
);

/// <summary>
/// Bundles hallucination text with threat type and reality chance.
/// Creates uncertainty - is this real or fever dream?
/// </summary>
public record HallucinationVariant(
    string Description,           // "The embers are dying! The fire is fading fast!"
    string ThreatType,            // "fire", "predator", "intruder", "movement"
    double RealityChance = 0.2    // Chance the hallucination is actually happening
);

/// <summary>
/// Variant pools for illness events.
/// </summary>
public static class IllnessVariants
{
    /// <summary>
    /// Wound infection onset - from untreated injuries.
    /// </summary>
    public static readonly IllnessOnsetVariant[] WoundOnset =
    [
        new("Your wound is hot to the touch. Something's wrong.",
            "wound", [EffectFactory.Fever(0.2)]),
        new("The cut you ignored throbs with heat. Red lines spider outward.",
            "wound", [EffectFactory.Fever(0.25)], 1.1),
        new("That scratch. It wasn't nothing after all. The skin around it is angry and swollen.",
            "wound", [EffectFactory.Fever(0.2)]),
    ];

    /// <summary>
    /// Exposure illness onset - from prolonged cold.
    /// </summary>
    public static readonly IllnessOnsetVariant[] ExposureOnset =
    [
        new("The cold has seeped into your bones. You can't get warm.",
            "exposure", [EffectFactory.Fever(0.15)], 0.9),
        new("Shivering won't stop. Your body is losing the fight against the cold.",
            "exposure", [EffectFactory.Fever(0.2)]),
        new("Deep chill. Not the kind that goes away with fire.",
            "exposure", [EffectFactory.Fever(0.2)]),
    ];

    /// <summary>
    /// Contamination illness onset - from bad water or food.
    /// </summary>
    public static readonly IllnessOnsetVariant[] ContaminationOnset =
    [
        new("That water sits wrong in your stomach.",
            "contamination", [EffectFactory.Nauseous(0.4, 120)]),
        new("Something you ate is fighting back.",
            "contamination", [EffectFactory.Nauseous(0.5, 90)]),
        new("Your guts twist. Bad water, most likely.",
            "contamination", [EffectFactory.Nauseous(0.4, 120)]),
    ];

    /// <summary>
    /// Exhaustion illness onset - from pushing too hard.
    /// </summary>
    public static readonly IllnessOnsetVariant[] ExhaustionOnset =
    [
        new("Pushing too hard. Your body is rebelling.",
            "exhaustion", [EffectFactory.Fever(0.15)], 0.8),
        new("You've spent everything. Now you're paying for it.",
            "exhaustion", [EffectFactory.Fever(0.15)], 0.8),
        new("Weakness washes over you. You've demanded too much of yourself.",
            "exhaustion", [EffectFactory.Fever(0.1)], 0.7),
    ];

    /// <summary>
    /// Fire-related hallucinations - plays on fire anxiety.
    /// </summary>
    public static readonly HallucinationVariant[] FireHallucinations =
    [
        new("The fire — it's dying! The embers are fading fast.", "fire", 0.2),
        new("Smoke everywhere. The fire's out of control.", "fire", 0.15),
        new("The flames are guttering. You're losing them.", "fire", 0.25),
        new("Your fire. Something's wrong with your fire.", "fire", 0.2),
    ];

    /// <summary>
    /// Predator-related hallucinations - plays on stalking fear.
    /// </summary>
    public static readonly HallucinationVariant[] PredatorHallucinations =
    [
        new("Footsteps. Something circling just beyond the light.", "predator", 0.2),
        new("Yellow eyes in the darkness. Watching.", "predator", 0.15),
        new("A growl. Low and close.", "predator", 0.2),
        new("Something is out there. You can feel it watching.", "predator", 0.15),
    ];

    /// <summary>
    /// Movement hallucinations - general paranoia.
    /// </summary>
    public static readonly HallucinationVariant[] MovementHallucinations =
    [
        new("Something moves at the edge of your vision.", "movement", 0.15),
        new("A shadow shifts. Was that real?", "movement", 0.1),
        new("Movement in the trees. Or just the wind?", "movement", 0.1),
        new("Shapes in the darkness. Real or imagined?", "movement", 0.1),
    ];

    /// <summary>
    /// Intruder hallucinations - someone/something has been here.
    /// </summary>
    public static readonly HallucinationVariant[] IntruderHallucinations =
    [
        new("Someone's been here. Things are moved.", "intruder", 0.1),
        new("Your supplies. They're not where you left them.", "intruder", 0.1),
        new("Tracks in the snow. Someone was here while you slept.", "intruder", 0.15),
    ];
}

/// <summary>
/// Selects appropriate illness variants based on context.
/// </summary>
public static class IllnessSelector
{
    /// <summary>
    /// Select an illness onset variant based on what's actually wrong with the player.
    /// Priority: wound > contamination > exposure > exhaustion
    /// </summary>
    public static IllnessOnsetVariant SelectOnsetVariant(GameContext ctx)
    {
        // Check for wound-related illness first
        if (ctx.Tensions.HasTension("WoundUntreated"))
        {
            return SelectFromPool(IllnessVariants.WoundOnset);
        }

        // Check for contamination (recent questionable water/food)
        // Low hydration after drinking could indicate bad water
        if (ctx.player.Body.Hydration < 800 && ctx.player.Body.Hydration > 200)
        {
            // Slim chance this is contamination-related
            if (Random.Shared.NextDouble() < 0.3)
                return SelectFromPool(IllnessVariants.ContaminationOnset);
        }

        // Check for exposure (low body temperature)
        if (ctx.player.Body.BodyTemperature < 35)
        {
            return SelectFromPool(IllnessVariants.ExposureOnset);
        }

        // Default to exhaustion
        return SelectFromPool(IllnessVariants.ExhaustionOnset);
    }

    /// <summary>
    /// Select an illness onset variant for a specific cause.
    /// </summary>
    public static IllnessOnsetVariant SelectOnsetVariant(string cause)
    {
        return cause switch
        {
            "wound" => SelectFromPool(IllnessVariants.WoundOnset),
            "contamination" => SelectFromPool(IllnessVariants.ContaminationOnset),
            "exposure" => SelectFromPool(IllnessVariants.ExposureOnset),
            "exhaustion" => SelectFromPool(IllnessVariants.ExhaustionOnset),
            _ => SelectFromPool(IllnessVariants.ExhaustionOnset)
        };
    }

    /// <summary>
    /// Select a hallucination variant weighted by current pressures.
    /// </summary>
    public static HallucinationVariant SelectHallucinationVariant(GameContext ctx)
    {
        var pool = new List<(HallucinationVariant variant, double weight)>();

        // Fire hallucinations more likely if fire is actually concerning
        var fire = ctx.Camp.GetFeature<HeatSourceFeature>();
        double fireWeight = (fire?.BurningMassKg ?? 0) < 0.5 ? 2.5 : 0.8;
        pool.AddRange(IllnessVariants.FireHallucinations.Select(v => (v, fireWeight)));

        // Predator hallucinations more likely if stalked
        double predatorWeight = ctx.Tensions.HasTension("Stalked") ? 2.5 : 0.8;
        pool.AddRange(IllnessVariants.PredatorHallucinations.Select(v => (v, predatorWeight)));

        // Movement hallucinations always possible
        pool.AddRange(IllnessVariants.MovementHallucinations.Select(v => (v, 1.0)));

        // Intruder hallucinations less common
        pool.AddRange(IllnessVariants.IntruderHallucinations.Select(v => (v, 0.5)));

        return SelectWeighted(pool);
    }

    /// <summary>
    /// Select a fire-specific hallucination.
    /// </summary>
    public static HallucinationVariant SelectFireHallucination(GameContext ctx)
    {
        return SelectFromPool(IllnessVariants.FireHallucinations);
    }

    /// <summary>
    /// Select a predator-specific hallucination.
    /// </summary>
    public static HallucinationVariant SelectPredatorHallucination(GameContext ctx)
    {
        return SelectFromPool(IllnessVariants.PredatorHallucinations);
    }

    /// <summary>
    /// Check if a hallucination is actually real based on context and reality chance.
    /// </summary>
    public static bool IsHallucinationReal(HallucinationVariant hallucination, GameContext ctx)
    {
        // Base check against reality chance
        if (Random.Shared.NextDouble() > hallucination.RealityChance)
            return false;

        // Additional context checks based on threat type
        return hallucination.ThreatType switch
        {
            "fire" => (ctx.Camp.GetFeature<HeatSourceFeature>()?.BurningMassKg ?? 0) < 0.5,
            "predator" => ctx.Tensions.HasTension("Stalked"),
            _ => true // Other types just use base chance
        };
    }

    private static T SelectFromPool<T>(T[] pool)
    {
        return pool[Random.Shared.Next(pool.Length)];
    }

    private static HallucinationVariant SelectWeighted(List<(HallucinationVariant variant, double weight)> pool)
    {
        if (pool.Count == 0)
            return IllnessVariants.MovementHallucinations[0];

        var totalWeight = pool.Sum(p => p.weight);
        var roll = Random.Shared.NextDouble() * totalWeight;

        double cumulative = 0;
        foreach (var (variant, weight) in pool)
        {
            cumulative += weight;
            if (roll <= cumulative)
                return variant;
        }

        return pool[^1].variant;
    }
}
