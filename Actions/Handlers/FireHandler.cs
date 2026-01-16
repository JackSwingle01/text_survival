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
            ToolType.HandDrill => 0.35,  // Increased from 0.30 for better early game
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
    /// Dexterity combines manipulation, wetness, darkness, and vitality effects.
    /// </summary>
    public static double CalculateFireChance(
        Gear tool,
        Resource tinder,
        int skillLevel = 0,
        bool consciousnessImpaired = false,
        double dexterity = 1.0)
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

        // Dexterity penalty (up to -50% at dexterity 0)
        // Dexterity combines: manipulation capacity, wetness, darkness, vitality
        double dexterityPenalty = (1.0 - dexterity) * 0.5;
        chance -= dexterityPenalty;

        return Math.Clamp(chance, 0.05, 0.95);
    }

    /// <summary>
    /// Calculate fire chance for an actor with context (gets dexterity from abilities).
    /// </summary>
    public static double CalculateFireChance(Actor actor, Gear tool, Resource tinder, int skillLevel, Location location, int hourOfDay)
    {
        var capacities = actor.GetCapacities();
        bool consciousnessImpaired = AbilityCalculator.IsConsciousnessImpaired(capacities.Consciousness);

        // Use context-aware Dexterity which combines manipulation, wetness, darkness, vitality
        var context = AbilityContext.FromFullContext(actor, null, location, hourOfDay);
        double dexterity = actor.GetDexterity(context);

        return CalculateFireChance(tool, tinder, skillLevel, consciousnessImpaired, dexterity);
    }

    /// <summary>
    /// Calculate fire chance for an actor (backward compatible, uses default context).
    /// </summary>
    public static double CalculateFireChance(Actor actor, Gear tool, Resource tinder, int skillLevel = 0)
    {
        var capacities = actor.GetCapacities();
        bool consciousnessImpaired = AbilityCalculator.IsConsciousnessImpaired(capacities.Consciousness);
        double dexterity = actor.Dexterity; // Uses default context

        return CalculateFireChance(tool, tinder, skillLevel, consciousnessImpaired, dexterity);
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
}