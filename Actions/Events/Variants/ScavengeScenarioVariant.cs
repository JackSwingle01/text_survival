namespace text_survival.Actions.Variants;

/// <summary>
/// Bundles scavenge clue descriptions with matched carcass outcomes.
/// Ensures text like "circling ravens" matches appropriate animal types
/// and predator encounter risk scales with freshness.
/// </summary>
public record ScavengeScenario(
    string ClueText,                    // "Circling ravens ahead"
    WeightedAnimalPool Animals,         // What carcass you might find
    FreshnessCategory Freshness,        // How picked-over it is
    WeightedPredatorPool Predators,     // What might be guarding it
    double BaseEncounterChance,         // 0.0-0.25
    string[]? ContextTags = null        // ["snow", "predator_territory"]
);

/// <summary>
/// Weighted pool of animal types for carcass selection.
/// </summary>
public record WeightedAnimalPool(string[] Animals, double[] Weights)
{
    public static WeightedAnimalPool Single(string animal) =>
        new([animal], [1.0]);

    public static WeightedAnimalPool Empty => new([], []);

    public string Select()
    {
        if (Animals.Length == 0) return "";
        if (Animals.Length == 1) return Animals[0];

        double total = Weights.Sum();
        double roll = Random.Shared.NextDouble() * total;
        double cumulative = 0;

        for (int i = 0; i < Weights.Length; i++)
        {
            cumulative += Weights[i];
            if (roll <= cumulative) return Animals[i];
        }

        return Animals[^1];
    }
}

/// <summary>
/// Weighted pool of predator types for encounter selection.
/// </summary>
public record WeightedPredatorPool(string[] Predators, double[] Weights)
{
    public static WeightedPredatorPool Single(string predator) =>
        new([predator], [1.0]);

    public static WeightedPredatorPool None => new([], []);

    public string? Select()
    {
        if (Predators.Length == 0) return null;
        if (Predators.Length == 1) return Predators[0];

        double total = Weights.Sum();
        double roll = Random.Shared.NextDouble() * total;
        double cumulative = 0;

        for (int i = 0; i < Weights.Length; i++)
        {
            cumulative += Weights[i];
            if (roll <= cumulative) return Predators[i];
        }

        return Predators[^1];
    }
}

/// <summary>
/// Freshness categories with associated harvestedPct ranges and modifiers.
/// </summary>
public enum FreshnessCategory
{
    Fresh,      // 0.0-0.15 harvestedPct, 2.0x risk, 0.7 boldness
    Recent,     // 0.15-0.35 harvestedPct, 1.5x risk, 0.6 boldness
    PickedOver, // 0.35-0.55 harvestedPct, 1.0x risk, 0.5 boldness
    BonesOnly   // 0.7-0.9 harvestedPct, 0.5x risk, 0.3 boldness
}

public static class FreshnessHelper
{
    public static (double minPct, double maxPct) GetHarvestedRange(FreshnessCategory freshness) =>
        freshness switch
        {
            FreshnessCategory.Fresh => (0.0, 0.15),
            FreshnessCategory.Recent => (0.15, 0.35),
            FreshnessCategory.PickedOver => (0.35, 0.55),
            FreshnessCategory.BonesOnly => (0.7, 0.9),
            _ => (0.3, 0.5)
        };

    public static double GetRiskModifier(FreshnessCategory freshness) =>
        freshness switch
        {
            FreshnessCategory.Fresh => 2.0,
            FreshnessCategory.Recent => 1.5,
            FreshnessCategory.PickedOver => 1.0,
            FreshnessCategory.BonesOnly => 0.5,
            _ => 1.0
        };

    public static double GetBoldness(FreshnessCategory freshness) =>
        freshness switch
        {
            FreshnessCategory.Fresh => 0.7,
            FreshnessCategory.Recent => 0.6,
            FreshnessCategory.PickedOver => 0.5,
            FreshnessCategory.BonesOnly => 0.3,
            _ => 0.5
        };

    public static double RollHarvestedPct(FreshnessCategory freshness)
    {
        var (min, max) = GetHarvestedRange(freshness);
        return min + Random.Shared.NextDouble() * (max - min);
    }

    public static string GetDescription(FreshnessCategory freshness) =>
        freshness switch
        {
            FreshnessCategory.Fresh => "fresh carcass",
            FreshnessCategory.Recent => "recent kill",
            FreshnessCategory.PickedOver => "scavenged remains",
            FreshnessCategory.BonesOnly => "bone scraps",
            _ => "carcass"
        };
}

/// <summary>
/// Predefined scavenge scenario libraries organized by find type.
/// </summary>
public static class ScavengeScenarios
{
    // ============ BONES ONLY ============
    // Safe, low value, common
    public static readonly ScavengeScenario[] BonesOnly =
    [
        new("Raptor pellets below a ledge",
            WeightedAnimalPool.Empty,
            FreshnessCategory.BonesOnly,
            WeightedPredatorPool.None,
            0),

        new("Old bones scattered, gnawed clean",
            WeightedAnimalPool.Empty,
            FreshnessCategory.BonesOnly,
            WeightedPredatorPool.None,
            0),
    ];

    // ============ SMALL GAME ============
    // Low risk, quick meal
    public static readonly ScavengeScenario[] SmallGame =
    [
        new("Scattered feathers and blood",
            new WeightedAnimalPool(["Grouse"], [1.0]),
            FreshnessCategory.Recent,
            new WeightedPredatorPool(["Fox"], [1.0]),
            0.03),

        new("Blood trail into the brush",
            new WeightedAnimalPool(["Hare", "Grouse", "Fox"], [0.6, 0.2, 0.2]),
            FreshnessCategory.Recent,
            new WeightedPredatorPool(["Wolf", "Fox"], [0.4, 0.6]),
            0.08),
    ];

    // ============ MEDIUM GAME ============
    // Significant find, requires butchering commitment
    public static readonly ScavengeScenario[] MediumGame =
    [
        new("Circling ravens ahead",
            new WeightedAnimalPool(["Caribou", "Hare", "Fox", "Wolf"], [0.5, 0.2, 0.15, 0.15]),
            FreshnessCategory.PickedOver,
            new WeightedPredatorPool(["Wolf"], [1.0]),
            0.12),

        new("Drag marks through the snow",
            new WeightedAnimalPool(["Caribou", "Wolf", "Megaloceros"], [0.6, 0.25, 0.15]),
            FreshnessCategory.Recent,
            new WeightedPredatorPool(["Wolf", "Bear"], [0.7, 0.3]),
            0.15,
            ["snow"]),

        new("Ground torn up, blood frozen",
            new WeightedAnimalPool(["Caribou", "Bison", "Megaloceros"], [0.4, 0.35, 0.25]),
            FreshnessCategory.PickedOver,
            new WeightedPredatorPool(["Wolf"], [1.0]),
            0.10,
            ["cold"]),
    ];

    // ============ LARGE GAME ============
    // Windfall or crisis - rare and consequential
    public static readonly ScavengeScenario[] LargeGame =
    [
        new("Fresh kill, still warm. Blood steams in the cold.",
            new WeightedAnimalPool(["Caribou", "Bison", "Megaloceros", "Wolf"], [0.35, 0.30, 0.25, 0.10]),
            FreshnessCategory.Fresh,
            new WeightedPredatorPool(["Wolf", "Bear"], [0.6, 0.4]),
            0.25),

        new("Large carcass, wolves gone. Still fresh enough.",
            new WeightedAnimalPool(["Bison", "Caribou", "Megaloceros"], [0.5, 0.3, 0.2]),
            FreshnessCategory.Recent,
            new WeightedPredatorPool(["Wolf"], [1.0]),
            0.08,
            ["predator_territory"]),
    ];

    // ============ SPECIAL/RARE ============
    // Memorable moments
    public static readonly ScavengeScenario[] Special =
    [
        new("Something large, half-buried in snow. Frozen solid.",
            new WeightedAnimalPool(["Bison", "Caribou", "Bear", "Woolly Mammoth"], [0.4, 0.35, 0.15, 0.1]),
            FreshnessCategory.Fresh,
            WeightedPredatorPool.None,
            0,
            ["deep_cold"]),

        new("Pine branches piled over something. Bear cache.",
            new WeightedAnimalPool(["Caribou", "Bison", "Megaloceros"], [0.6, 0.3, 0.1]),
            FreshnessCategory.Recent,
            WeightedPredatorPool.Single("Bear"),
            0.60,
            ["bear_territory"]),
    ];
}

/// <summary>
/// Selects appropriate scavenge scenarios based on context.
/// </summary>
public static class ScavengeScenarioSelector
{
    /// <summary>
    /// Build weighted pool of scenarios based on context.
    /// </summary>
    public static List<(ScavengeScenario scenario, double weight)> BuildWeightedPool(
        GameContext ctx,
        bool isSnowy,
        bool isPredatorTerritory,
        bool isBearTerritory)
    {
        var pool = new List<(ScavengeScenario, double)>();

        // Bones - always available, common
        pool.AddRange(ScavengeScenarios.BonesOnly.Select(s => (s, 0.4)));

        // Small game - always available
        pool.AddRange(ScavengeScenarios.SmallGame.Select(s => (s, 0.25)));

        // Medium game - contextual
        foreach (var scenario in ScavengeScenarios.MediumGame)
        {
            if (scenario.ContextTags?.Contains("snow") == true && !isSnowy)
                continue;

            double weight = 0.12;
            if (isPredatorTerritory) weight *= 1.5;
            pool.Add((scenario, weight));
        }

        // Large game - rare
        foreach (var scenario in ScavengeScenarios.LargeGame)
        {
            if (scenario.ContextTags?.Contains("predator_territory") == true && !isPredatorTerritory)
                continue;

            pool.Add((scenario, 0.05));
        }

        // Special - very rare, context-dependent
        foreach (var scenario in ScavengeScenarios.Special)
        {
            if (scenario.ContextTags?.Contains("deep_cold") == true)
            {
                if (ctx.Weather.TemperatureInFahrenheit < 10)
                    pool.Add((scenario, 0.02));
            }
            else if (scenario.ContextTags?.Contains("bear_territory") == true)
            {
                if (isBearTerritory)
                    pool.Add((scenario, 0.02));
            }
            else
            {
                pool.Add((scenario, 0.02));
            }
        }

        return pool;
    }

    /// <summary>
    /// Select a scenario from a weighted pool.
    /// </summary>
    public static ScavengeScenario? SelectFromPool(List<(ScavengeScenario scenario, double weight)> pool)
    {
        if (pool.Count == 0) return null;

        double totalWeight = pool.Sum(p => p.weight);
        double roll = Random.Shared.NextDouble() * totalWeight;
        double cumulative = 0;

        foreach (var (scenario, weight) in pool)
        {
            cumulative += weight;
            if (roll <= cumulative)
                return scenario;
        }

        return pool[^1].scenario;
    }
}
