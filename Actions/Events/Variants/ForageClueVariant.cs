using text_survival.Actors.Animals;
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
    Food,       // Berries, roots, nuts
    Medicine,   // Fungi, moss, medicinal bark
    Materials   // Stone, bone, fiber, crafting supplies
}

/// <summary>
/// Categories of clues that drive different outcomes.
/// </summary>
public enum ClueCategory
{
    Resource,   // Boosts forage yield for specific resources
    Game,       // Provides hunt bonus at this location
    Scavenge,   // Leads to carcass discovery with predator risk
    Negative    // Reduces overall forage yield
}

/// <summary>
/// Represents an environmental clue that hints at foraging opportunities.
/// Players learn to read these clues through experience.
/// </summary>
public record ForageClue(
    string Description,                        // What the player observes
    ClueCategory Category,                     // What type of clue this is
    Resource[] SuggestedResources,             // Resources this clue suggests (Resource clues)
    double YieldModifier = 1.0,                // Multiplier for Resource/Negative clues
    AnimalType? GameAnimalType = null,             // For Game clues: AnimalType.Rabbit, AnimalType.Caribou, etc.
    double HuntBonus = 0,                      // For Game clues: additive density bonus
    ScavengeScenario? Scenario = null          // For Scavenge: scenario with animal/predator pools
)
{
    /// <summary>
    /// Get the focus ID suggested by this clue, if any.
    /// Only Resource clues suggest a focus category.
    /// </summary>
    public string? GetSuggestedFocusId()
    {
        if (Category != ClueCategory.Resource)
            return null;

        if (SuggestedResources.Any(r => r.IsFuel()))
            return "fuel";
        if (SuggestedResources.Any(r => r.IsFood()))
            return "food";
        if (SuggestedResources.Any(r => r.IsMedicine()))
            return "medicine";
        if (SuggestedResources.Any(r => r.IsMaterial()))
            return "materials";
        return null;
    }
}

/// <summary>
/// Predefined clue pools organized by category and context.
/// Resource clues boost forage yield; Game clues boost hunting; Scavenge clues lead to carcasses.
/// </summary>
public static class ClueLibrary
{
    // ============ RESOURCE CLUES ============
    // Boost yield for specific resources when followed

    public static readonly ForageClue[] ForestResourceClues =
    [
        new("Woodpecker holes in dead birch", ClueCategory.Resource,
            [Resource.BirchBark, Resource.Amadou, Resource.Chaga], 1.2),

        new("Fresh deadfall from the wind", ClueCategory.Resource,
            [Resource.Stick, Resource.Pine, Resource.Birch], 1.3),

        new("Shelf fungus on fallen logs", ClueCategory.Resource,
            [Resource.BirchPolypore, Resource.Amadou], 1.2),

        new("Resin bleeding from pine bark", ClueCategory.Resource,
            [Resource.PineResin, Resource.PineNeedles], 1.2),

        new("Lichen hanging from branches", ClueCategory.Resource,
            [Resource.Usnea], 1.3),

        new("Nuts scattered beneath the trees", ClueCategory.Resource,
            [Resource.Nuts], 1.2),

        new("Dry leaves gathered in hollows", ClueCategory.Resource,
            [Resource.Tinder, Resource.PlantFiber], 1.2),
    ];

    public static readonly ForageClue[] RockyResourceClues =
    [
        new("Shale fragments along the cliff base", ClueCategory.Resource,
            [Resource.Shale, Resource.Flint, Resource.Stone], 1.3),

        new("Pyrite glinting in exposed rock", ClueCategory.Resource,
            [Resource.Pyrite, Resource.Flint], 1.5),

        new("Exposed roots in eroded soil", ClueCategory.Resource,
            [Resource.Roots], 1.3),

        new("Wind-scoured scrub growth", ClueCategory.Resource,
            [Resource.PlantFiber], 1.2),
    ];

    public static readonly ForageClue[] WaterResourceClues =
    [
        new("Thick sphagnum at the water's edge", ClueCategory.Resource,
            [Resource.SphagnumMoss], 1.3),

        new("Reeds and fibrous plants", ClueCategory.Resource,
            [Resource.PlantFiber], 1.3),

        new("Willow growing near the bank", ClueCategory.Resource,
            [Resource.WillowBark], 1.4),

        new("Berry bushes along the shore", ClueCategory.Resource,
            [Resource.Berries, Resource.RoseHip], 1.2),
    ];

    public static readonly ForageClue[] StormResourceClues =
    [
        new("Branches torn down by recent wind", ClueCategory.Resource,
            [Resource.Stick, Resource.Pine, Resource.Birch, Resource.Oak], 1.5),
    ];

    public static readonly ForageClue[] RainResourceClues =
    [
        new("Bracket fungi swollen after the rain", ClueCategory.Resource,
            [Resource.BirchPolypore, Resource.Amadou], 1.4),
    ];

    // ============ GAME CLUES ============
    // Boost next hunt at this location

    public static readonly ForageClue[] SnowGameClues =
    [
        new("Fresh tracks in the snow", ClueCategory.Game,
            [], 1.0, GameAnimalType: AnimalType.Caribou, HuntBonus: 0.20),

        new("Scat visible against the white", ClueCategory.Game,
            [], 1.0, GameAnimalType: AnimalType.Rabbit, HuntBonus: 0.10),
    ];

    public static readonly ForageClue[] ForestGameClues =
    [
        new("Animal trails through the brush", ClueCategory.Game,
            [], 1.0, GameAnimalType: AnimalType.Caribou, HuntBonus: 0.15),
    ];

    public static readonly ForageClue[] DawnDuskGameClues =
    [
        new("Fresh feeding marks in the bark", ClueCategory.Game,
            [], 1.0, GameAnimalType: AnimalType.Caribou, HuntBonus: 0.15),

        new("Movement in the underbrush", ClueCategory.Game,
            [], 1.0, GameAnimalType: AnimalType.Rabbit, HuntBonus: 0.10),
    ];

    // ============ SCAVENGE CLUES ============
    // Now handled by ScavengeScenarioSelector - see ScavengeScenarioVariant.cs

    // ============ NEGATIVE CLUES ============
    // Reduce overall forage yield

    public static readonly ForageClue[] NegativeClues =
    [
        new("The ground looks picked over", ClueCategory.Negative,
            [], 0.8),

        new("Everything frozen solid", ClueCategory.Negative,
            [], 0.7),

        new("Trampled and grazed bare", ClueCategory.Negative,
            [], 0.7),

        new("Signs of recent fire damage", ClueCategory.Negative,
            [], 0.6),

        new("Saturated and rotting", ClueCategory.Negative,
            [], 0.7),
    ];

    // ============ GENERIC FALLBACK ============

    public static readonly ForageClue[] GenericClues =
    [
        new("Signs of past foraging here", ClueCategory.Resource,
            [Resource.Stick, Resource.Stone, Resource.PlantFiber], 1.0),
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
        var territory = location.GetFeature<AnimalTerritoryFeature>();
        var availableResources = forage?.Resources.Select(r => r.ResourceType).ToHashSet()
            ?? new HashSet<Resource>();
        bool hasHuntableGame = territory != null;

        // Forest clues - check for wooded features or forest tags
        bool isForested = location.HasFeature<WoodedAreaFeature>() ||
                          location.Tags.Contains("forest") ||
                          location.Tags.Contains("birch") ||
                          location.Tags.Contains("pine") ||
                          location.Tags.Contains("wood");
        if (isForested)
        {
            foreach (var clue in ClueLibrary.ForestResourceClues)
            {
                double weight = CalculateClueWeight(clue, availableResources);
                if (weight > 0) pool.Add((clue, weight * 1.5));
            }
            if (hasHuntableGame)
            {
                foreach (var clue in ClueLibrary.ForestGameClues)
                    pool.Add((clue, 0.8));
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
            foreach (var clue in ClueLibrary.RockyResourceClues)
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
            foreach (var clue in ClueLibrary.WaterResourceClues)
            {
                double weight = CalculateClueWeight(clue, availableResources);
                if (weight > 0) pool.Add((clue, weight * 1.5));
            }
        }

        // Weather-triggered clues
        var weather = ctx.Weather;
        if (weather.WindSpeedPct > 0.5)
        {
            foreach (var clue in ClueLibrary.StormResourceClues)
            {
                double weight = CalculateClueWeight(clue, availableResources);
                if (weight > 0) pool.Add((clue, weight * 1.3));
            }
        }

        if (weather.PrecipitationPct > 0.3)
        {
            foreach (var clue in ClueLibrary.RainResourceClues)
            {
                double weight = CalculateClueWeight(clue, availableResources);
                if (weight > 0) pool.Add((clue, weight * 1.4));
            }
        }

        // Snow clues - game clues when cold enough
        bool isSnowy = weather.TemperatureInFahrenheit < 32;
        if (isSnowy)
        {
            if (hasHuntableGame)
            {
                foreach (var clue in ClueLibrary.SnowGameClues)
                    pool.Add((clue, 1.0));
            }
        }

        // Time of day clues - game activity at dawn/dusk
        var timeOfDay = ctx.GetTimeOfDay();
        if (timeOfDay == GameContext.TimeOfDay.Dawn || timeOfDay == GameContext.TimeOfDay.Dusk)
        {
            if (hasHuntableGame)
            {
                foreach (var clue in ClueLibrary.DawnDuskGameClues)
                    pool.Add((clue, 1.0));
            }
        }

        // Scavenge scenarios - context-aware carcass discovery
        bool isPredatorTerritory = territory?.HasPredators() == true;
        bool isBearTerritory = territory?._possibleAnimals.Any(a =>
            a.AnimalType == AnimalType.Bear) == true;

        var scavengePool = ScavengeScenarioSelector.BuildWeightedPool(
            ctx, isSnowy, isPredatorTerritory, isBearTerritory);

        foreach (var (scenario, weight) in scavengePool)
        {
            var clue = new ForageClue(
                scenario.ClueText,
                ClueCategory.Scavenge,
                [],
                Scenario: scenario
            );
            pool.Add((clue, weight));
        }

        // Negative clues - context-specific
        if (forage?.IsNearlyDepleted() == true)
        {
            var pickedOver = ClueLibrary.NegativeClues.First(c => c.Description.Contains("picked over"));
            pool.Add((pickedOver, 2.0));
        }
        if (weather.TemperatureInFahrenheit < 10)
        {
            var frozen = ClueLibrary.NegativeClues.FirstOrDefault(c => c.Description.Contains("frozen"));
            if (frozen != null) pool.Add((frozen, 1.5));
        }
        if (weather.PrecipitationPct > 0.6)
        {
            var saturated = ClueLibrary.NegativeClues.FirstOrDefault(c => c.Description.Contains("Saturated"));
            if (saturated != null) pool.Add((saturated, 1.2));
        }

        // Always include neutral generic clues at low weight
        foreach (var clue in ClueLibrary.GenericClues)
        {
            pool.Add((clue, 0.3));
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
    /// 0-2 clues max, 1 is most common. Better perception = higher chance of 2.
    /// Perception: 1.0 = healthy, 0.5 = half blind
    /// </summary>
    private static int GetClueCount(double perception, Random rng)
    {
        double roll = rng.NextDouble();

        if (perception >= 0.9)
        {
            // Full perception: 70% → 1, 15% → 0, 15% → 2
            if (roll < 0.15) return 0;
            if (roll < 0.85) return 1;
            return 2;
        }
        else if (perception >= 0.7)
        {
            // Slight impairment: 80% → 1, 15% → 0, 5% → 2
            if (roll < 0.15) return 0;
            if (roll < 0.95) return 1;
            return 2;
        }
        else if (perception >= 0.5)
        {
            // Moderate impairment: 70% → 1, 30% → 0
            return roll < 0.30 ? 0 : 1;
        }
        else
        {
            // Severe impairment (half blind or worse): 80% → 0, 20% → 1
            return roll < 0.80 ? 0 : 1;
        }
    }

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
    /// - If following a Resource clue, apply its yield modifier
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

            bool isTargetCategory = targetCategory != null &&
                ResourceCategories.Items[targetCategory.Value].Contains(resourceType);

            if (!isTargetCategory)
            {
                // Randomly drop items from this stack (keep ~40-50%)
                ReduceStack(stack, 0.4 + _rng.NextDouble() * 0.2);
            }
            // Apply yield multiplier for resources matching the followed Resource clue
            else if (followedClue?.Category == ClueCategory.Resource &&
                     followedClue.SuggestedResources.Contains(resourceType))
            {
                MultiplyStack(stack, followedClue.YieldModifier);
            }
        }
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
        _ => ""
    };
}
