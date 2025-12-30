using text_survival.Environments.Features;

namespace text_survival.Actions.Variants;

/// <summary>
/// Terrain-aware escape scenarios for stalking encounters.
/// Different terrains offer different escape methods with varying success rates.
/// </summary>
public record EscapeScenario(
    string TerrainType,           // "forest", "rocky", "water", "open"
    string SetupDescription,      // "Dense brush ahead"
    EscapeMethod[] Methods,       // Available escape options
    double TerrainEscapeBonus     // 0.0-0.3 added to base escape chance
);

/// <summary>
/// A specific escape method within a terrain type.
/// </summary>
public record EscapeMethod(
    string Name,                  // "Dense Brush"
    string ActionText,            // "Dive into the thick undergrowth"
    string SuccessText,           // "You lose them in the tangle"
    string FailureText,           // "They push through after you"
    double BaseSuccessChance,     // 0.3-0.7
    string[]? ExcludedPredators,  // ["bear"] - predators this doesn't work against
    double InjuryChance = 0.0,    // Chance of minor injury during escape
    int TimeCost = 5              // Minutes the escape takes
);

/// <summary>
/// Predefined escape scenarios organized by terrain type.
/// </summary>
public static class EscapeScenarios
{
    public static readonly EscapeScenario Forest = new(
        "forest",
        "Trees and brush provide options",
        [
            new("Dense Brush", "Dive into thick undergrowth",
                "Thorns tear at you but you're through. They can't follow.",
                "They push through after you",
                0.55, ["bear", "wolverine"], InjuryChance: 0.2),

            new("Climb Tree", "Scramble up the nearest trunk",
                "You pull yourself up. They circle below, frustrated.",
                "You slip. Back on the ground.",
                0.40, ["bear", "wolverine", "lynx"], TimeCost: 8),

            new("Break Trail", "Weave between trees, change direction",
                "Your zigzag pattern loses them.",
                "They cut you off at the turn.",
                0.45, null),

            new("Thick Cover", "Disappear into the dense forest",
                "You slip between the trees. The forest swallows you.",
                "Not dense enough. They track your movement.",
                0.50, null)
        ],
        0.15
    );

    public static readonly EscapeScenario Rocky = new(
        "rocky",
        "Rocks and narrow passages offer escape routes",
        [
            new("Narrow Passage", "Squeeze through a gap too small for them",
                "You wedge through. They can't follow.",
                "Stuck. They're waiting on the other side.",
                0.60, ["fox", "lynx"], InjuryChance: 0.1, TimeCost: 6),

            new("High Ground", "Scramble up the rocks",
                "You reach a ledge they can't climb.",
                "You lose your grip halfway up.",
                0.35, null, InjuryChance: 0.3, TimeCost: 10),

            new("Rock Maze", "Duck into the boulder field",
                "You lose them in the maze of stone.",
                "They know these rocks better than you.",
                0.50, null, TimeCost: 8),

            new("Cliff Edge", "Use the terrain to your advantage",
                "They won't risk the narrow ledge. You edge away.",
                "Loose rocks. You slip, they gain ground.",
                0.45, null, InjuryChance: 0.25)
        ],
        0.20
    );

    public static readonly EscapeScenario Water = new(
        "water",
        "Water might break your trail",
        [
            new("Cross Stream", "Wade across and keep going",
                "The water breaks your scent trail. They lose you.",
                "They splash across after you.",
                0.50, null, TimeCost: 8),

            new("Thin Ice Gamble", "Cross where ice looks thin",
                "You make it. They won't risk the crossing.",
                "Ice cracks. You're wet and they're still coming.",
                0.30, null, InjuryChance: 0.4, TimeCost: 5),

            new("Wade Downstream", "Walk in the water to hide your trail",
                "Your scent disappears. They search the wrong bank.",
                "The cold slows you down. They catch up.",
                0.55, null, TimeCost: 12),

            new("River Bank", "Use the bank's cover",
                "The overhang hides you. They pass by.",
                "They spot your tracks in the mud.",
                0.45, null, TimeCost: 6)
        ],
        0.10
    );

    public static readonly EscapeScenario Open = new(
        "open",
        "Nowhere to hide. Other options needed.",
        [
            new("Fire Scare", "Light something and wave it at them",
                "The flames make them hesitate. You back away.",
                "They're not impressed.",
                0.45, ["bear"], TimeCost: 8),

            new("Drop Meat", "Throw your food behind you",
                "They stop to investigate. You run.",
                "They want more than scraps.",
                0.40, null, TimeCost: 3),

            new("Make Noise", "Shout, bang rocks, look big",
                "Your display makes them uncertain. They retreat.",
                "They call your bluff.",
                0.35, null, TimeCost: 5),

            new("Defensive Stand", "Back toward cover while facing them",
                "You keep them at bay while retreating.",
                "You stumble. They press the advantage.",
                0.30, null, InjuryChance: 0.15, TimeCost: 10)
        ],
        0.0
    );

    /// <summary>
    /// Default escape scenario when terrain can't be determined.
    /// </summary>
    public static readonly EscapeScenario Default = Forest;
}

/// <summary>
/// Selects appropriate escape scenarios based on context.
/// </summary>
public static class EscapeScenarioSelector
{
    /// <summary>
    /// Select an escape scenario based on location terrain.
    /// </summary>
    public static EscapeScenario SelectForLocation(GameContext ctx)
    {
        var location = ctx.CurrentLocation;
        if (location == null) return EscapeScenarios.Default;

        // Check for water feature first
        var water = location.GetFeature<WaterFeature>();
        if (water != null)
            return EscapeScenarios.Water;

        // Check terrain properties
        if (location.IsEscapeTerrain && location.VisibilityFactor < 0.7)
            return EscapeScenarios.Forest;

        if (location.TerrainHazardLevel > 0.5)
            return EscapeScenarios.Rocky;

        if (location.VisibilityFactor > 1.2)
            return EscapeScenarios.Open;

        // Default based on visibility
        return location.VisibilityFactor < 0.8
            ? EscapeScenarios.Forest
            : EscapeScenarios.Open;
    }

    /// <summary>
    /// Get available escape methods for a scenario, filtered by predator type.
    /// </summary>
    public static List<EscapeMethod> GetAvailableMethods(
        EscapeScenario scenario,
        string? predatorType,
        GameContext ctx)
    {
        var available = new List<EscapeMethod>();
        string predatorLower = predatorType?.ToLower() ?? "";

        foreach (var method in scenario.Methods)
        {
            // Check if method is excluded for this predator
            if (method.ExcludedPredators != null &&
                method.ExcludedPredators.Any(p => predatorLower.Contains(p.ToLower())))
            {
                continue;
            }

            // Check special requirements
            if (method.Name == "Fire Scare" &&
                !ctx.Inventory.HasLitTorch &&
                !ctx.CurrentLocation.HasActiveHeatSource())
            {
                continue;
            }

            if (method.Name == "Drop Meat" && !ctx.Check(EventCondition.HasMeat))
            {
                continue;
            }

            available.Add(method);
        }

        // Always have at least one option
        if (available.Count == 0)
        {
            // Fallback to "Make Noise" or first method
            available.Add(scenario.Methods.FirstOrDefault(m => m.Name == "Make Noise")
                ?? scenario.Methods[0]);
        }

        return available;
    }

    /// <summary>
    /// Calculate escape chance with all modifiers applied.
    /// </summary>
    public static double CalculateEscapeChance(
        EscapeMethod method,
        EscapeScenario scenario,
        GameContext ctx,
        string? predatorType)
    {
        double chance = method.BaseSuccessChance + scenario.TerrainEscapeBonus;

        // Predator-specific modifiers
        string predatorLower = predatorType?.ToLower() ?? "";
        if (predatorLower.Contains("wolf"))
            chance -= 0.1; // Wolves coordinate, harder to lose
        else if (predatorLower.Contains("bear"))
            chance += 0.1; // Bears give up easier
        else if (predatorLower.Contains("lynx"))
            chance -= 0.05; // Lynx are stealthy, moderate difficulty

        // Player condition modifiers
        var caps = ctx.player.GetCapacities();
        chance *= caps.Moving; // Injured = worse odds

        // Active tensions
        var stalkedTension = ctx.Tensions.GetTension("Stalked");
        if (stalkedTension?.Severity > 0.5)
            chance -= 0.1; // They know you well now

        // Time of day
        bool isDark = !ctx.Weather.IsDaytime(ctx.GameTime);
        if (isDark)
            chance -= 0.05; // Harder to navigate in dark

        return Math.Clamp(chance, 0.1, 0.9);
    }

    /// <summary>
    /// Get a description hint for the escape based on predator.
    /// </summary>
    public static string GetPredatorHint(string? predatorType)
    {
        string predatorLower = predatorType?.ToLower() ?? "";

        if (predatorLower.Contains("wolf"))
            return "Wolves coordinate. Breaking line of sight won't be enough.";
        if (predatorLower.Contains("bear"))
            return "Bears are powerful but give up easier. Show you're not easy prey.";
        if (predatorLower.Contains("lynx"))
            return "Lynx are patient stalkers. Quick, decisive action.";
        if (predatorLower.Contains("wolverine"))
            return "Wolverines are tenacious. You need real distance.";

        return "Move fast. Think faster.";
    }
}
