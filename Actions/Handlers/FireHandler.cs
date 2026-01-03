using text_survival.Actors;
using text_survival.Bodies;
using text_survival.Environments;
using text_survival.Environments.Features;
using text_survival.IO;
using text_survival.Items;
using text_survival.UI;
using text_survival.Web;

namespace text_survival.Actions.Handlers;

/// <summary>
/// Pure game logic for fire starting and tending.
/// UI code calls these methods; NPCs can call them directly.
/// </summary>
public static class FireHandler
{
    // ============================================
    // Data Types
    // ============================================

    /// <summary>
    /// Result of a fire start attempt.
    /// </summary>
    public record FireStartResult(bool Success, string Message, double ChanceUsed);

    /// <summary>
    /// Available fire-starting materials in an inventory.
    /// </summary>
    public record FireMaterials(
        List<Gear> Tools,
        List<Resource> Tinders,
        bool HasKindling,
        Gear? EmberCarrier
    );

    // ============================================
    // Pure Game Logic - Material Queries
    // ============================================

    /// <summary>
    /// Get all available fire-starting materials from inventory.
    /// </summary>
    public static FireMaterials GetFireMaterials(Inventory inv)
    {
        var tools = inv.Tools.Where(t =>
            t.ToolType == ToolType.FireStriker ||
            t.ToolType == ToolType.HandDrill ||
            t.ToolType == ToolType.BowDrill).ToList();

        var tinders = new List<Resource>();
        if (inv.Count(Resource.BirchBark) > 0) tinders.Add(Resource.BirchBark);
        if (inv.Count(Resource.Amadou) > 0) tinders.Add(Resource.Amadou);
        if (inv.Count(Resource.Usnea) > 0) tinders.Add(Resource.Usnea);
        if (inv.Count(Resource.Chaga) > 0) tinders.Add(Resource.Chaga);
        if (inv.Count(Resource.Tinder) > 0) tinders.Add(Resource.Tinder);

        bool hasKindling = inv.Count(Resource.Stick) > 0;
        var emberCarrier = inv.Tools.FirstOrDefault(t => t.IsEmberCarrier && t.IsEmberLit);

        return new FireMaterials(tools, tinders, hasKindling, emberCarrier);
    }

    /// <summary>
    /// Get best fire-starting tool by base success chance.
    /// </summary>
    public static Gear? GetBestTool(Inventory inv)
    {
        return inv.Tools
            .Where(t => t.ToolType == ToolType.FireStriker ||
                       t.ToolType == ToolType.HandDrill ||
                       t.ToolType == ToolType.BowDrill)
            .OrderByDescending(GetToolBaseChance)
            .FirstOrDefault();
    }

    /// <summary>
    /// Get best tinder by ignition bonus.
    /// Priority: BirchBark > Amadou > Usnea > Chaga > Tinder
    /// </summary>
    public static Resource? GetBestTinder(Inventory inv)
    {
        if (inv.Count(Resource.BirchBark) > 0) return Resource.BirchBark;
        if (inv.Count(Resource.Amadou) > 0) return Resource.Amadou;
        if (inv.Count(Resource.Usnea) > 0) return Resource.Usnea;
        if (inv.Count(Resource.Chaga) > 0) return Resource.Chaga;
        if (inv.Count(Resource.Tinder) > 0) return Resource.Tinder;
        return null;
    }

    /// <summary>
    /// Get tool base success chance.
    /// </summary>
    public static double GetToolBaseChance(Gear tool)
    {
        return tool.ToolType switch
        {
            ToolType.FireStriker => 0.90,
            ToolType.BowDrill => 0.50,
            ToolType.HandDrill => 0.30,
            _ => 0.30
        };
    }

    /// <summary>
    /// Map resource to fuel type for tinder.
    /// </summary>
    public static FuelType GetTinderFuelType(Resource tinder)
    {
        return tinder switch
        {
            Resource.BirchBark => FuelType.BirchBark,
            Resource.Amadou => FuelType.Amadou,
            Resource.Usnea => FuelType.Usnea,
            Resource.Chaga => FuelType.Chaga,
            _ => FuelType.Tinder
        };
    }

    // ============================================
    // Pure Game Logic - Fire Chance Calculation
    // ============================================

    /// <summary>
    /// Calculate fire start success chance with all modifiers.
    /// </summary>
    public static double CalculateFireChance(
        Gear tool,
        Resource tinder,
        int skillLevel = 0,
        bool consciousnessImpaired = false,
        bool manipulationImpaired = false,
        double wetness = 0)
    {
        double chance = GetToolBaseChance(tool);

        // Skill bonus (+10% per level)
        chance += skillLevel * 0.1;

        // Tinder ignition bonus
        var fuelType = GetTinderFuelType(tinder);
        chance += FuelDatabase.Get(fuelType).IgnitionBonus;

        // Consciousness impairment (-20%)
        if (consciousnessImpaired)
            chance -= 0.2;

        // Manipulation impairment (-25%)
        if (manipulationImpaired)
            chance -= 0.25;

        // Wetness penalty (up to -25% at full wetness)
        if (wetness > 0.3)
            chance -= wetness * 0.25;

        return Math.Clamp(chance, 0.05, 0.95);
    }

    /// <summary>
    /// Calculate fire chance for an actor (gets impairments from capacities).
    /// </summary>
    public static double CalculateFireChance(Actor actor, Gear tool, Resource tinder, int skillLevel = 0)
    {
        var capacities = actor.GetCapacities();
        bool consciousnessImpaired = AbilityCalculator.IsConsciousnessImpaired(capacities.Consciousness);
        bool manipulationImpaired = AbilityCalculator.IsManipulationImpaired(capacities.Manipulation);
        double wetness = actor.EffectRegistry.GetEffectsByKind("Wet").FirstOrDefault()?.Severity ?? 0;

        return CalculateFireChance(tool, tinder, skillLevel, consciousnessImpaired, manipulationImpaired, wetness);
    }

    // ============================================
    // Pure Game Logic - Fire Execution
    // ============================================

    /// <summary>
    /// Attempt to start a fire. Consumes materials regardless of success.
    /// Returns result with success status and message.
    /// </summary>
    public static FireStartResult AttemptStartFire(
        Actor actor,
        Inventory inv,
        Location location,
        Gear tool,
        Resource tinder,
        int skillLevel = 0,
        HeatSourceFeature? existingFire = null)
    {
        // Validate materials
        if (inv.Count(tinder) <= 0)
            return new FireStartResult(false, "No tinder available.", 0);
        if (inv.Count(Resource.Stick) <= 0)
            return new FireStartResult(false, "No kindling available.", 0);

        // Calculate success chance
        double chance = CalculateFireChance(actor, tool, tinder, skillLevel);

        // Consume tinder
        double tinderWeight = inv.Pop(tinder);
        var tinderFuelType = GetTinderFuelType(tinder);

        // Roll for success
        bool success = Utils.DetermineSuccess(chance);

        if (success)
        {
            // Consume kindling on success
            double kindlingWeight = inv.Pop(Resource.Stick);

            // Create or relight fire
            if (existingFire != null)
            {
                existingFire.AddFuel(tinderWeight, tinderFuelType);
                existingFire.AddFuel(kindlingWeight, FuelType.Kindling);
                existingFire.IgniteAll();
                return new FireStartResult(true, $"Fire relit! ({chance:P0} chance)", chance);
            }
            else
            {
                var newFire = new HeatSourceFeature();
                newFire.AddFuel(tinderWeight, tinderFuelType);
                newFire.AddFuel(kindlingWeight, FuelType.Kindling);
                newFire.IgniteAll();
                location.Features.Add(newFire);
                return new FireStartResult(true, $"Fire started! ({chance:P0} chance)", chance);
            }
        }
        else
        {
            // Tinder wasted, kindling preserved
            string tinderName = tinder switch
            {
                Resource.BirchBark => "birch bark",
                Resource.Amadou => "amadou",
                Resource.Usnea => "usnea",
                Resource.Chaga => "chaga",
                _ => "tinder"
            };
            return new FireStartResult(false, $"Failed to start fire. The {tinderName} was wasted. ({chance:P0} chance)", chance);
        }
    }

    /// <summary>
    /// Start fire from ember carrier. Always succeeds. Consumes the carrier.
    /// </summary>
    public static void StartFromEmber(
        Actor actor,
        Inventory inv,
        Location location,
        Gear emberCarrier,
        HeatSourceFeature? existingFire = null)
    {
        if (inv.Count(Resource.Stick) <= 0)
            return;

        // Consume kindling
        double kindlingWeight = inv.Pop(Resource.Stick);

        // Consume the ember carrier
        inv.Tools.Remove(emberCarrier);

        if (existingFire != null)
        {
            existingFire.AddFuel(kindlingWeight, FuelType.Kindling);
            existingFire.IgniteAll();
        }
        else
        {
            var newFire = new HeatSourceFeature();
            newFire.AddFuel(kindlingWeight, FuelType.Kindling);
            newFire.IgniteAll();
            location.Features.Add(newFire);
        }
    }

    /// <summary>
    /// Add fuel to a fire.
    /// </summary>
    public static void AddFuel(Inventory inv, HeatSourceFeature fire, Resource fuel, int count = 1)
    {
        var fuelType = fuel switch
        {
            Resource.Stick => FuelType.Kindling,
            Resource.Pine => FuelType.PineWood,
            Resource.Birch => FuelType.BirchWood,
            Resource.Oak => FuelType.OakWood,
            Resource.Tinder => FuelType.Tinder,
            Resource.BirchBark => FuelType.BirchBark,
            Resource.Usnea => FuelType.Usnea,
            Resource.Chaga => FuelType.Chaga,
            Resource.Charcoal => FuelType.Kindling,
            Resource.Bone => FuelType.Bone,
            _ => FuelType.Kindling
        };

        for (int i = 0; i < count && inv.Count(fuel) > 0; i++)
        {
            if (!fire.CanAddFuel(fuelType)) break;
            double weight = inv.Pop(fuel);
            fire.AddFuel(weight, fuelType);
        }
    }

    // ============================================
    // NPC Entry Points
    // ============================================

    /// <summary>
    /// NPC fire starting - auto-selects best tool and tinder.
    /// Returns true if fire was started.
    /// </summary>
    public static bool StartFire(Actor actor, Inventory inv, Location location)
    {
        var materials = GetFireMaterials(inv);
        var existingFire = location.GetFeature<HeatSourceFeature>();

        // Use ember carrier if available (100% success)
        if (materials.EmberCarrier != null && materials.HasKindling)
        {
            StartFromEmber(actor, inv, location, materials.EmberCarrier, existingFire);
            return true;
        }

        // Need tools, tinder, and kindling
        var tool = GetBestTool(inv);
        var tinder = GetBestTinder(inv);
        if (tool == null || tinder == null || !materials.HasKindling)
            return false;

        var result = AttemptStartFire(actor, inv, location, tool, tinder.Value, skillLevel: 0, existingFire);
        return result.Success;
    }

    /// <summary>
    /// Check if inventory has appropriate fuel for the current fire temperature.
    /// </summary>
    public static bool CanTendFire(Inventory inv, HeatSourceFeature fire)
    {
        if (fire.GetCurrentFireTemperature() > 200)
        {
            return inv.Count(Resource.Oak) > 0
                || inv.Count(Resource.Birch) > 0
                || inv.Count(Resource.Pine) > 0
                || inv.Count(Resource.Stick) > 0;
        }
        return inv.Count(Resource.Stick) > 0;
    }

    /// <summary>
    /// Check if inventory has materials needed to start a fire.
    /// </summary>
    public static bool CanStartFire(Inventory inv)
    {
        var materials = GetFireMaterials(inv);
        var tool = GetBestTool(inv);
        var tinder = GetBestTinder(inv);
        return tool != null && tinder != null && materials.HasKindling;
    }

    /// <summary>
    /// NPC fire tending - adds best available fuel.
    /// </summary>
    public static void TendFire(Inventory inv, HeatSourceFeature fire)
    {
        // Add logs if fire is hot enough, otherwise kindling
        if (fire.GetCurrentFireTemperature() > 200)
        {
            if (inv.Count(Resource.Oak) > 0)
                AddFuel(inv, fire, Resource.Oak);
            else if (inv.Count(Resource.Birch) > 0)
                AddFuel(inv, fire, Resource.Birch);
            else if (inv.Count(Resource.Pine) > 0)
                AddFuel(inv, fire, Resource.Pine);
            else if (inv.Count(Resource.Stick) > 0)
                AddFuel(inv, fire, Resource.Stick);
        }
        else if (inv.Count(Resource.Stick) > 0)
        {
            AddFuel(inv, fire, Resource.Stick);
        }
    }

    // ============================================
    // UI Entry Points (Player)
    // ============================================

    public static void ManageFire(GameContext ctx, HeatSourceFeature? fire = null)
    {
        fire ??= ctx.CurrentLocation.GetFeature<HeatSourceFeature>() ?? new HeatSourceFeature();
        WebIO.RunFireUI(ctx, fire);
    }

    /// <summary>
    /// Console UI for starting a fire.
    /// </summary>
    public static void StartFireConsole(GameContext ctx)
    {
        var inv = ctx.Inventory;
        var existingFire = ctx.CurrentLocation.GetFeature<HeatSourceFeature>();
        bool relightingFire = existingFire != null;

        if (relightingFire)
            GameDisplay.AddNarrative(ctx, "You prepare to relight the fire.");
        else
            GameDisplay.AddNarrative(ctx, "You prepare to start a fire.");

        var materials = GetFireMaterials(inv);

        // Check for lit ember carrier - 100% success fire start
        if (materials.EmberCarrier != null)
        {
            if (!materials.HasKindling)
            {
                GameDisplay.AddNarrative(ctx, $"Your {materials.EmberCarrier.Name} smolders with an ember, but you have no kindling to start a fire.");
            }
            else
            {
                GameDisplay.Render(ctx, statusText: "Thinking.");
                if (Input.Confirm(ctx, $"Use your {materials.EmberCarrier.Name} ({materials.EmberCarrier.EmberBurnHoursRemaining:F1}h remaining) to start the fire?"))
                {
                    GameDisplay.UpdateAndRenderProgress(ctx, "Starting fire from ember...", 5, ActivityType.TendingFire);
                    StartFromEmber(ctx.player, inv, ctx.CurrentLocation, materials.EmberCarrier, existingFire);
                    GameDisplay.AddSuccess(ctx, $"The ember catches the kindling. You have a fire!");
                    ctx.player.Skills.GetSkill("Firecraft").GainExperience(2);
                    return;
                }
            }
        }

        if (materials.Tools.Count == 0)
        {
            GameDisplay.AddWarning(ctx, "You don't have any fire-making tools!");
            return;
        }

        if (materials.Tinders.Count == 0)
        {
            GameDisplay.AddWarning(ctx, "You don't have any tinder to start a fire!");
            return;
        }

        if (!materials.HasKindling)
        {
            GameDisplay.AddWarning(ctx, "You don't have any kindling to start a fire!");
            return;
        }

        // Show materials
        var tinderParts = materials.Tinders.Select(t => $"{inv.Count(t)} {t.ToString().ToLower()}").ToList();
        GameDisplay.AddNarrative(ctx, $"Materials: {string.Join(", ", tinderParts)}, {inv.Count(Resource.Stick)} kindling");

        // Build tool options
        int skillLevel = ctx.player.Skills.GetSkill("Firecraft").Level;
        var toolChoices = new List<string>();
        var toolMap = new Dictionary<string, Gear>();

        foreach (var tool in materials.Tools)
        {
            var bestTinder = GetBestTinder(inv)!.Value;
            double chance = CalculateFireChance(ctx.player, tool, bestTinder, skillLevel);
            string label = $"{tool.Name} - {chance:P0} success chance";
            toolChoices.Add(label);
            toolMap[label] = tool;
        }
        toolChoices.Add("Cancel");

        GameDisplay.Render(ctx, addSeparator: false, statusText: "Preparing.");
        string choice = Input.Select(ctx, "Choose fire-making tool:", toolChoices);

        if (choice == "Cancel")
        {
            GameDisplay.AddNarrative(ctx, "You decide not to start a fire right now.");
            return;
        }

        var selectedTool = toolMap[choice];

        // Show impairment warnings
        var capacities = ctx.player.GetCapacities();
        if (AbilityCalculator.IsConsciousnessImpaired(capacities.Consciousness))
            GameDisplay.AddWarning(ctx, "Your foggy mind makes this harder.");
        if (AbilityCalculator.IsManipulationImpaired(capacities.Manipulation))
            GameDisplay.AddWarning(ctx, "Your unsteady hands make this harder.");
        var wetness = ctx.player.EffectRegistry.GetEffectsByKind("Wet").FirstOrDefault()?.Severity ?? 0;
        if (wetness > 0.3)
            GameDisplay.AddWarning(ctx, "Your wet hands make this harder.");

        // Fire starting loop
        while (true)
        {
            GameDisplay.AddNarrative(ctx, $"You work with the {selectedTool.Name}...");
            GameDisplay.UpdateAndRenderProgress(ctx, "Starting fire...", 10, ActivityType.TendingFire);

            var tinder = GetBestTinder(inv);
            if (tinder == null)
            {
                GameDisplay.AddWarning(ctx, "You ran out of tinder!");
                break;
            }

            var result = AttemptStartFire(ctx.player, inv, ctx.CurrentLocation, selectedTool, tinder.Value, skillLevel, existingFire);

            if (result.Success)
            {
                GameDisplay.AddSuccess(ctx, result.Message);
                ctx.player.Skills.GetSkill("Firecraft").GainExperience(3);

                // Tutorial
                if (ctx.DaysSurvived == 0 && ctx.TryShowTutorial("first_fire_started"))
                {
                    GameDisplay.AddNarrative(ctx, "");
                    GameDisplay.AddWarning(ctx, "A night fire needs to burn 10-12 hours.");
                    GameDisplay.AddWarning(ctx, "Figure a good log burns about an hour per kilogram.");
                    GameDisplay.AddWarning(ctx, "You do the math.");
                }
                break;
            }
            else
            {
                GameDisplay.AddWarning(ctx, result.Message);
                ctx.player.Skills.GetSkill("Firecraft").GainExperience(1);

                // Check if retry is possible
                var remainingMaterials = GetFireMaterials(inv);
                if (remainingMaterials.Tinders.Count > 0 && remainingMaterials.HasKindling)
                {
                    GameDisplay.Render(ctx, statusText: "Thinking.");
                    if (Input.Confirm(ctx, $"Try again with {selectedTool.Name}?"))
                        continue;
                }
                break;
            }
        }
    }
}
