namespace text_survival.Actions.Variants;

/// <summary>
/// Environmental signs that tell stories during travel.
/// Signs create narrative texture and can trigger tensions or opportunities.
/// </summary>
public record TrailSign(
    string Description,           // "Tracks in the snow, half-filled"
    SignCategory Category,        // Predator, Prey, Human, Weather, Danger
    SignAge Age,                  // Fresh, Recent, Old, Ancient
    string? TensionToCreate,      // Optional: "Stalked", "FreshTrail", etc.
    double TensionSeverity,       // 0.0-0.5
    string? FollowUpHint          // "The trail heads north" or null
);

public enum SignCategory
{
    Predator,   // Wolf scat, claw marks, tracks
    Prey,       // Game trails, droppings, bedding
    Human,      // Old camps, knapping sites, bones
    Weather,    // Snow patterns, ice formations
    Danger      // Thin ice, unstable ground, deadfall
}

public enum SignAge
{
    Fresh,      // Minutes to hours old
    Recent,     // Hours to a day old
    Old,        // Days old
    Ancient     // Long-established or very old
}

/// <summary>
/// Predefined trail sign libraries organized by category.
/// </summary>
public static class TrailSigns
{
    // ============ PREDATOR SIGNS ============
    // Creates Stalked tension, raises awareness of threat
    public static readonly TrailSign[] PredatorSigns =
    [
        new("Fresh wolf scat, still warm",
            SignCategory.Predator, SignAge.Fresh, "Stalked", 0.3,
            "Territory marked recently"),

        new("Deep claw marks on a tree trunk",
            SignCategory.Predator, SignAge.Recent, "Stalked", 0.2,
            null),

        new("Tracks crossing yours — large cat",
            SignCategory.Predator, SignAge.Fresh, "Stalked", 0.4,
            "Moving same direction"),

        new("Old bones cracked for marrow",
            SignCategory.Predator, SignAge.Old, null, 0,
            "Feeding site nearby"),

        new("Yellow stain in snow, musky smell",
            SignCategory.Predator, SignAge.Recent, "Stalked", 0.15,
            null),

        new("Drag marks through the brush",
            SignCategory.Predator, SignAge.Recent, "Stalked", 0.25,
            "Something made a kill here"),

        new("Fur caught on branches at shoulder height",
            SignCategory.Predator, SignAge.Recent, null, 0,
            "Large animal passed through"),

        new("Paw prints in mud, claws extended",
            SignCategory.Predator, SignAge.Fresh, "Stalked", 0.35,
            "Moving with purpose"),
    ];

    // ============ PREY SIGNS ============
    // Creates FreshTrail or WoundedPrey tension, hunting opportunity
    public static readonly TrailSign[] PreySigns =
    [
        new("Game trail, worn deep",
            SignCategory.Prey, SignAge.Ancient, "FreshTrail", 0.3,
            "Regular migration path"),

        new("Browsed saplings, bark stripped",
            SignCategory.Prey, SignAge.Recent, "FreshTrail", 0.2,
            "Deer feeding here"),

        new("Caribou droppings, scattered fresh",
            SignCategory.Prey, SignAge.Fresh, "FreshTrail", 0.4,
            "Herd passed through"),

        new("Bed of flattened grass",
            SignCategory.Prey, SignAge.Recent, null, 0,
            "Resting spot"),

        new("Tracks of a limping animal",
            SignCategory.Prey, SignAge.Fresh, "WoundedPrey", 0.5,
            "Easy target if you can find it"),

        new("Hoof prints clustered near water",
            SignCategory.Prey, SignAge.Recent, "FreshTrail", 0.25,
            "Watering hole nearby"),

        new("Antler scrape on tree bark",
            SignCategory.Prey, SignAge.Recent, null, 0,
            "Territorial marking"),

        new("Blood droplets in the snow",
            SignCategory.Prey, SignAge.Fresh, "WoundedPrey", 0.6,
            "Something is injured"),
    ];

    // ============ HUMAN SIGNS ============
    // Creates MarkedDiscovery or Disturbed tension, rare encounters
    public static readonly TrailSign[] HumanSigns =
    [
        new("Stone chips around a flat rock",
            SignCategory.Human, SignAge.Ancient, "MarkedDiscovery", 0.3,
            "Knapping site"),

        new("Fire-blackened stones in a ring",
            SignCategory.Human, SignAge.Old, "MarkedDiscovery", 0.4,
            "Old camp"),

        new("Cut marks on a long bone",
            SignCategory.Human, SignAge.Old, null, 0,
            "Someone butchered here"),

        new("Footprint, human but not yours",
            SignCategory.Human, SignAge.Recent, "Disturbed", 0.4,
            "Someone else survives"),

        new("Sharpened stake driven into the ground",
            SignCategory.Human, SignAge.Old, "MarkedDiscovery", 0.35,
            "Trap site or marker"),

        new("Charcoal drawing on rock — animals",
            SignCategory.Human, SignAge.Ancient, "MarkedDiscovery", 0.5,
            "Hunting record"),
    ];

    // ============ WEATHER SIGNS ============
    // Environmental storytelling, no tension
    public static readonly TrailSign[] WeatherSigns =
    [
        new("Wind-scoured snow, exposing ground",
            SignCategory.Weather, SignAge.Fresh, null, 0,
            "Storm passed recently"),

        new("Ice crust over deep snow",
            SignCategory.Weather, SignAge.Recent, null, 0,
            "Freeze-thaw cycle"),

        new("Drifts piled against the windward side",
            SignCategory.Weather, SignAge.Fresh, null, 0,
            "Prevailing wind direction"),

        new("Icicles forming on the south face",
            SignCategory.Weather, SignAge.Fresh, null, 0,
            "Brief warming"),

        new("Snow clumped on branches, ready to fall",
            SignCategory.Weather, SignAge.Fresh, null, 0,
            "Recent heavy snow"),
    ];

    // ============ DANGER SIGNS ============
    // Warnings about terrain hazards
    public static readonly TrailSign[] DangerSigns =
    [
        new("Disturbed earth, soil soft underneath",
            SignCategory.Danger, SignAge.Fresh, null, 0,
            "Unstable footing"),

        new("Tree leaning at a steep angle",
            SignCategory.Danger, SignAge.Recent, null, 0,
            "Could fall"),

        new("Ice surface darker here",
            SignCategory.Danger, SignAge.Fresh, null, 0,
            "Thin ice warning"),

        new("Cracks radiating from a central point",
            SignCategory.Danger, SignAge.Fresh, null, 0,
            "Ice stress fractures"),

        new("Rocks piled at the base of a slope",
            SignCategory.Danger, SignAge.Recent, null, 0,
            "Rockfall area"),

        new("Broken branches overhead",
            SignCategory.Danger, SignAge.Recent, null, 0,
            "Widow maker territory"),
    ];
}

/// <summary>
/// Selects appropriate trail signs based on context.
/// </summary>
public static class TrailSignSelector
{
    /// <summary>
    /// Build weighted pool of signs based on context.
    /// </summary>
    public static List<(TrailSign sign, double weight)> BuildWeightedPool(
        GameContext ctx,
        bool hasPredators,
        bool hasPrey,
        bool isSnowy,
        bool hasWater)
    {
        var pool = new List<(TrailSign, double)>();
        bool isStalked = ctx.Tensions.HasTension("Stalked");

        // Predator signs - higher weight in predator territory or when already stalked
        if (hasPredators || isStalked)
        {
            double predatorWeight = hasPredators ? 1.5 : 0.5;
            if (isStalked) predatorWeight *= 1.3; // More signs when being watched
            pool.AddRange(TrailSigns.PredatorSigns.Select(s => (s, predatorWeight)));
        }

        // Prey signs - higher weight in prey territory
        if (hasPrey)
        {
            pool.AddRange(TrailSigns.PreySigns.Select(s => (s, 1.2)));
        }

        // Human signs - rare but always possible
        pool.AddRange(TrailSigns.HumanSigns.Select(s => (s, 0.12)));

        // Weather signs - higher in snowy conditions
        if (isSnowy)
        {
            pool.AddRange(TrailSigns.WeatherSigns.Select(s => (s, 0.8)));
        }
        else
        {
            pool.AddRange(TrailSigns.WeatherSigns.Select(s => (s, 0.3)));
        }

        // Danger signs - higher near water (ice) or always some baseline
        double dangerWeight = hasWater ? 0.6 : 0.25;
        pool.AddRange(TrailSigns.DangerSigns.Select(s => (s, dangerWeight)));

        return pool;
    }

    /// <summary>
    /// Select a sign from a weighted pool.
    /// </summary>
    public static TrailSign? SelectFromPool(List<(TrailSign sign, double weight)> pool)
    {
        if (pool.Count == 0) return null;

        double totalWeight = pool.Sum(p => p.weight);
        double roll = Random.Shared.NextDouble() * totalWeight;
        double cumulative = 0;

        foreach (var (sign, weight) in pool)
        {
            cumulative += weight;
            if (roll <= cumulative)
                return sign;
        }

        return pool[^1].sign;
    }

    /// <summary>
    /// Convenience method to select a sign for current context.
    /// </summary>
    public static TrailSign? SelectForContext(GameContext ctx)
    {
        var territory = ctx.CurrentLocation.GetFeature<Environments.Features.AnimalTerritoryFeature>();
        var water = ctx.CurrentLocation.GetFeature<Environments.Features.WaterFeature>();

        bool hasPredators = territory?.HasPredators() == true;
        // Prey are non-predators - if territory has animals but not all predators, there's prey
        bool hasPrey = territory != null && !territory.HasPredators();
        bool isSnowy = ctx.Weather.Precipitation > 0.1 && ctx.Weather.TemperatureInFahrenheit < 35;
        bool hasWater = water != null;

        var pool = BuildWeightedPool(ctx, hasPredators, hasPrey, isSnowy, hasWater);
        return SelectFromPool(pool);
    }
}
