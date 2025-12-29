using text_survival.Bodies;
using text_survival.Environments;
using text_survival.Environments.Features;
using text_survival.Items;

namespace text_survival.Actions.Variants;

/// <summary>
/// Focus options for foraging. Affects which resources are prioritized.
/// </summary>
public enum ForageFocus
{
    General,    // No focus, balanced yield
    Fuel,       // Sticks, bark, deadwood
    Food,       // Berries, roots, nuts, small game
    Medicine,   // Fungi, moss, medicinal bark
    Materials,  // Stone, bone, fiber, crafting supplies
    FollowClue  // Investigate a specific clue
}

/// <summary>
/// Represents an environmental clue that hints at available forage resources.
/// Players learn to read these clues to make better foraging decisions.
/// </summary>
public record ForageClue(
    string Description,           // What the player observes: "Woodpecker holes in dead birch"
    string HintText,              // Subtle hint for new players: "(bark, insects)"
    Resource[] SuggestedResources, // Resources this clue suggests
    double YieldModifier = 1.0    // Multiplier if player follows this clue
);

/// <summary>
/// Predefined clue pools organized by what triggers them.
/// Each clue connects an observable sign to likely resources.
/// </summary>
public static class ClueLibrary
{
    // Forest clues - triggered by forest terrain or wooded features
    public static readonly ForageClue[] ForestClues =
    [
        new("Woodpecker holes in dead birch", "(bark, tinder)",
            [Resource.BirchBark, Resource.Amadou, Resource.Chaga], 1.2),

        new("Fresh deadfall from the wind", "(fuel)",
            [Resource.Stick, Resource.Pine, Resource.Birch], 1.3),

        new("Mushrooms growing on fallen logs", "(medicine)",
            [Resource.BirchPolypore, Resource.Chaga, Resource.Amadou], 1.2),

        new("Animal trails through the underbrush", "(game signs)",
            [Resource.Bone, Resource.RawMeat], 1.1),

        new("Resin bleeding from pine bark", "(pitch, tinder)",
            [Resource.PineResin, Resource.PineNeedles, Resource.Pine], 1.2),

        new("Lichen hanging from branches", "(medicine)",
            [Resource.Usnea], 1.3),

        new("Nuts scattered beneath the trees", "(food)",
            [Resource.Nuts, Resource.Berries], 1.2),

        new("Dry leaves gathered in hollows", "(tinder)",
            [Resource.Tinder, Resource.PlantFiber], 1.2),
    ];

    // Rocky clues - triggered by rocky terrain or high hazard areas
    public static readonly ForageClue[] RockyClues =
    [
        new("Shale fragments along the cliff base", "(stone, flint)",
            [Resource.Shale, Resource.Flint, Resource.Stone], 1.3),

        new("Lichen crusting north-facing rocks", "(medicine)",
            [Resource.Usnea], 1.2),

        new("Raptor pellets below a ledge", "(bones)",
            [Resource.Bone], 1.4),

        new("Wind-scoured scrub growth", "(tough fibers)",
            [Resource.PlantFiber, Resource.RawFiber], 1.2),

        new("Pyrite glinting in exposed rock", "(firestarter)",
            [Resource.Pyrite, Resource.Flint], 1.5),

        new("Exposed roots in eroded soil", "(food)",
            [Resource.Roots], 1.3),
    ];

    // Water clues - triggered by water features
    public static readonly ForageClue[] WaterClues =
    [
        new("Thick moss along the water's edge", "(medicine, tinder)",
            [Resource.SphagnumMoss, Resource.Usnea], 1.3),

        new("Animal tracks in the mud", "(game signs)",
            [Resource.Bone, Resource.RawMeat], 1.2),

        new("Reeds and fibrous plants", "(materials)",
            [Resource.PlantFiber, Resource.RawFiber], 1.3),

        new("Willow growing near the bank", "(medicine)",
            [Resource.WillowBark], 1.4),

        new("Berry bushes along the shore", "(food)",
            [Resource.Berries, Resource.RoseHip, Resource.JuniperBerry], 1.2),
    ];

    // Storm/weather clues - triggered by weather conditions
    public static readonly ForageClue[] StormClues =
    [
        new("Branches torn down by recent wind", "(abundant fuel)",
            [Resource.Stick, Resource.Pine, Resource.Birch, Resource.Oak], 1.5),

        new("Debris washed up from the storm", "(mixed finds)",
            [Resource.Stick, Resource.Bone, Resource.PlantFiber], 1.3),
    ];

    public static readonly ForageClue[] RainClues =
    [
        new("Fungi pushing up after the wet", "(medicine, tinder)",
            [Resource.BirchPolypore, Resource.Chaga, Resource.Amadou], 1.4),

        new("Saturated moss everywhere", "(medicine)",
            [Resource.SphagnumMoss], 1.3),
    ];

    public static readonly ForageClue[] SnowClues =
    [
        new("Fresh tracks in the snow", "(game signs)",
            [Resource.Bone, Resource.RawMeat], 1.3),

        new("Scat visible against the white", "(territory signs)",
            [Resource.Bone], 1.2),
    ];

    // Time of day clues
    public static readonly ForageClue[] DawnDuskClues =
    [
        new("Fresh feeding marks in the bark", "(active game)",
            [Resource.RawMeat, Resource.Bone], 1.2),

        new("Movement in the underbrush", "(small game)",
            [Resource.RawMeat], 1.3),
    ];

    // Generic fallback clues
    public static readonly ForageClue[] GenericClues =
    [
        new("Signs of past foraging here", "(mixed)",
            [Resource.Stick, Resource.Stone, Resource.PlantFiber], 1.0),

        new("The ground looks picked over", "(sparse)",
            [], 0.8),
    ];
}

/// <summary>
/// Selects appropriate foraging clues based on location and context.
/// Weights clues by what's actually available and current conditions.
/// </summary>
public static class ClueSelector
{
    /// <summary>
    /// Generate contextually appropriate clues for foraging.
    /// Uses seed for deterministic results (prevents reroll exploit).
    /// </summary>
    public static List<ForageClue> GenerateClues(GameContext ctx, Location location, int seed)
    {
        var rng = new Random(seed);
        var pool = BuildWeightedPool(ctx, location);
        var clues = new List<ForageClue>();

        // Perception affects how many clues the player notices
        var perception = AbilityCalculator.CalculatePerception(
            ctx.player.Body, ctx.player.EffectRegistry.GetCapacityModifiers());
        int count = GetClueCount(perception, rng);

        for (int i = 0; i < count && pool.Count > 0; i++)
        {
            var selected = SelectWeighted(pool, rng);
            clues.Add(selected);
            pool.RemoveAll(p => p.clue == selected);
        }

        // Fallback if somehow empty
        if (clues.Count == 0)
        {
            clues.Add(ClueLibrary.GenericClues[0]);
        }

        return clues;
    }

    private static List<(ForageClue clue, double weight)> BuildWeightedPool(GameContext ctx, Location location)
    {
        var pool = new List<(ForageClue clue, double weight)>();
        var forage = location.GetFeature<ForageFeature>();
        var availableResources = forage?.Resources.Select(r => r.ResourceType).ToHashSet()
            ?? new HashSet<Resource>();

        // Forest clues - check for wooded features or forest tags
        bool isForested = location.HasFeature<WoodedAreaFeature>() ||
                          location.Tags.Contains("forest") ||
                          location.Tags.Contains("birch") ||
                          location.Tags.Contains("pine") ||
                          location.Tags.Contains("wood");
        if (isForested)
        {
            foreach (var clue in ClueLibrary.ForestClues)
            {
                double weight = CalculateClueWeight(clue, availableResources);
                if (weight > 0) pool.Add((clue, weight * 1.5));
            }
        }

        // Rocky clues - check terrain hazard or rocky tags
        bool isRocky = location.TerrainHazardLevel > 0.4 ||
                       location.Tags.Contains("rock") ||
                       location.Tags.Contains("cliff") ||
                       location.Tags.Contains("overlook") ||
                       location.IsVantagePoint;
        if (isRocky)
        {
            foreach (var clue in ClueLibrary.RockyClues)
            {
                double weight = CalculateClueWeight(clue, availableResources);
                if (weight > 0) pool.Add((clue, weight * 1.5));
            }
        }

        // Water clues - check for water features
        bool hasWater = location.HasFeature<WaterFeature>() ||
                        location.Tags.Contains("water") ||
                        location.Tags.Contains("river") ||
                        location.Tags.Contains("creek") ||
                        location.Tags.Contains("lake");
        if (hasWater)
        {
            foreach (var clue in ClueLibrary.WaterClues)
            {
                double weight = CalculateClueWeight(clue, availableResources);
                if (weight > 0) pool.Add((clue, weight * 1.5));
            }
        }

        // Weather-triggered clues
        var weather = ctx.Weather;
        if (weather.WindSpeed > 0.5)
        {
            foreach (var clue in ClueLibrary.StormClues)
            {
                double weight = CalculateClueWeight(clue, availableResources);
                if (weight > 0) pool.Add((clue, weight * 1.3));
            }
        }

        if (weather.Precipitation > 0.3)
        {
            foreach (var clue in ClueLibrary.RainClues)
            {
                double weight = CalculateClueWeight(clue, availableResources);
                if (weight > 0) pool.Add((clue, weight * 1.4));
            }
        }

        if (weather.TemperatureInFahrenheit < 32)
        {
            foreach (var clue in ClueLibrary.SnowClues)
            {
                double weight = CalculateClueWeight(clue, availableResources);
                if (weight > 0) pool.Add((clue, weight * 1.2));
            }
        }

        // Time of day clues
        var timeOfDay = ctx.GetTimeOfDay();
        if (timeOfDay == GameContext.TimeOfDay.Dawn || timeOfDay == GameContext.TimeOfDay.Dusk)
        {
            foreach (var clue in ClueLibrary.DawnDuskClues)
            {
                double weight = CalculateClueWeight(clue, availableResources);
                if (weight > 0) pool.Add((clue, weight * 1.2));
            }
        }

        // Always include neutral generic clues at low weight
        // But exclude "picked over" unless location is actually depleted
        foreach (var clue in ClueLibrary.GenericClues)
        {
            bool isPickedOverClue = clue.Description.Contains("picked over");
            if (!isPickedOverClue)
            {
                pool.Add((clue, 0.3));
            }
        }

        // Only show "picked over" clue when location is actually depleted
        if (forage?.IsNearlyDepleted() == true)
        {
            var pickedOver = ClueLibrary.GenericClues.FirstOrDefault(c => c.Description.Contains("picked over"));
            if (pickedOver != null)
            {
                pool.Add((pickedOver, 2.0));
            }
        }

        return pool;
    }

    /// <summary>
    /// Calculate weight for a clue based on whether its suggested resources
    /// are actually available at this location.
    /// </summary>
    private static double CalculateClueWeight(ForageClue clue, HashSet<Resource> availableResources)
    {
        if (clue.SuggestedResources.Length == 0) return 0.5;

        int matchCount = clue.SuggestedResources.Count(r => availableResources.Contains(r));
        double matchRatio = (double)matchCount / clue.SuggestedResources.Length;

        // Weight based on match ratio - clues are more likely when resources are actually present
        return matchRatio * clue.YieldModifier;
    }

    private static ForageClue SelectWeighted(List<(ForageClue clue, double weight)> pool, Random rng)
    {
        if (pool.Count == 0)
            return ClueLibrary.GenericClues[0];

        double totalWeight = pool.Sum(p => p.weight);
        double roll = rng.NextDouble() * totalWeight;

        double cumulative = 0;
        foreach (var (clue, weight) in pool)
        {
            cumulative += weight;
            if (roll <= cumulative)
                return clue;
        }

        return pool[^1].clue;
    }

    /// <summary>
    /// Determine clue count based on perception.
    /// Full perception: 2-3 clues, moderate impairment: 1-2, severe: 1
    /// </summary>
    private static int GetClueCount(double perception, Random rng)
    {
        if (perception >= 0.7)
            return rng.Next(2, 4);  // 2-3 clues (normal)
        else if (perception >= 0.4)
            return rng.Next(1, 3);  // 1-2 clues (moderate impairment)
        else
            return 1;               // 1 clue (severe impairment)
    }

    /// <summary>
    /// Check if hint text should be shown based on perception.
    /// Impaired perception hides subtle hints, forcing players to learn clue meanings.
    /// </summary>
    public static bool ShouldShowHints(double perception) => perception >= 0.5;
}

/// <summary>
/// Applies focus multipliers to forage results.
/// Keeps the focus logic self-contained - ForageFeature doesn't need to change.
/// </summary>
public static class FocusProcessor
{
    private static readonly Random _rng = new();

    /// <summary>
    /// Apply focus to forage results by filtering/adjusting the inventory.
    /// - Focused category: keep all (100%)
    /// - Other categories: randomly drop ~50% of items
    /// - Follow clue: keep clue resources, drop ~60% of others
    /// </summary>
    public static void ApplyFocus(Inventory results, ForageFocus focus, ForageClue? followedClue = null)
    {
        if (focus == ForageFocus.General)
            return; // No changes for general search

        // Get the target category for this focus
        ResourceCategory? targetCategory = focus switch
        {
            ForageFocus.Fuel => ResourceCategory.Fuel,
            ForageFocus.Food => ResourceCategory.Food,
            ForageFocus.Medicine => ResourceCategory.Medicine,
            ForageFocus.Materials => ResourceCategory.Material,
            _ => null
        };

        // For each resource type, decide whether to keep or reduce
        foreach (Resource resourceType in Enum.GetValues<Resource>())
        {
            var stack = results[resourceType];
            if (stack.Count == 0) continue;

            bool shouldKeep = ShouldKeepResource(resourceType, focus, targetCategory, followedClue);

            if (!shouldKeep)
            {
                // Randomly drop items from this stack (keep ~40-50%)
                ReduceStack(stack, 0.4 + _rng.NextDouble() * 0.2);
            }
            // Apply yield multiplier for resources matching the followed clue
            else if (followedClue != null && followedClue.SuggestedResources.Contains(resourceType))
            {
                MultiplyStack(stack, followedClue.YieldModifier);
            }
        }
    }

    private static bool ShouldKeepResource(
        Resource resource,
        ForageFocus focus,
        ResourceCategory? targetCategory,
        ForageClue? followedClue)
    {
        // Follow clue: keep resources suggested by the clue
        if (focus == ForageFocus.FollowClue && followedClue != null)
        {
            return followedClue.SuggestedResources.Contains(resource);
        }

        // Category focus: keep resources in target category
        if (targetCategory != null)
        {
            return ResourceCategories.Items[targetCategory.Value].Contains(resource);
        }

        return true;
    }

    /// <summary>
    /// Reduce a stack to approximately the target percentage of items.
    /// </summary>
    private static void ReduceStack(Stack<double> stack, double keepRatio)
    {
        if (stack.Count == 0) return;

        var items = stack.ToList();
        stack.Clear();

        foreach (var item in items)
        {
            if (_rng.NextDouble() < keepRatio)
            {
                stack.Push(item);
            }
        }
    }

    /// <summary>
    /// Multiply all item weights in a stack by the given multiplier.
    /// Used for yield bonuses when following clues.
    /// </summary>
    private static void MultiplyStack(Stack<double> stack, double multiplier)
    {
        if (stack.Count == 0 || multiplier == 1.0) return;

        var items = stack.ToList();
        stack.Clear();

        foreach (var item in items)
        {
            stack.Push(item * multiplier);
        }
    }

    /// <summary>
    /// Get display name for a focus option.
    /// </summary>
    public static string GetFocusDisplayName(ForageFocus focus) => focus switch
    {
        ForageFocus.General => "Search generally",
        ForageFocus.Fuel => "Focus on fuel",
        ForageFocus.Food => "Focus on food",
        ForageFocus.Medicine => "Focus on medicine",
        ForageFocus.Materials => "Focus on materials",
        ForageFocus.FollowClue => "Follow the clue",
        _ => focus.ToString()
    };

    /// <summary>
    /// Get description for a focus option.
    /// </summary>
    public static string GetFocusDescription(ForageFocus focus) => focus switch
    {
        ForageFocus.General => "balanced yield",
        ForageFocus.Fuel => "sticks, bark, wood",
        ForageFocus.Food => "berries, roots, nuts",
        ForageFocus.Medicine => "fungi, moss, bark",
        ForageFocus.Materials => "stone, bone, fiber",
        ForageFocus.FollowClue => "+10 min, better odds",
        _ => ""
    };
}
