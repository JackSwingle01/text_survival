using System.Linq;
using text_survival.Actors.Animals;
using text_survival.Environments.Features;
using text_survival.Items;

namespace text_survival.Actions;

/// <summary>
/// Compound predicates that encapsulate complex game state checks.
/// Mirrors OutcomeTemplates pattern but for conditions rather than outcomes.
///
/// When adding new systems, update the relevant Situation here.
/// All events using that Situation automatically respond.
/// </summary>
public static class Situations
{
    public static bool AttractiveToPredators(GameContext ctx) =>
        ctx.Inventory.HasMeat ||
        ctx.player.EffectRegistry.HasEffect("Bleeding") ||
        ctx.player.EffectRegistry.HasEffect("Bloody") ||
        ctx.Tensions.HasTension("FoodScentStrong");

    public static double PredatorAttractionLevel(GameContext ctx)
    {
        double level = 0;
        if (ctx.Inventory.HasMeat) level += 0.3;
        if (ctx.player.EffectRegistry.HasEffect("Bleeding")) level += 0.4;
        if (ctx.Tensions.HasTension("FoodScentStrong")) level += 0.3;
        if (ctx.Check(EventCondition.Injured)) level += 0.2;

        // Blood on player attracts predators (scales with severity)
        double bloodySeverity = ctx.player.EffectRegistry.GetSeverity("Bloody");
        level += bloodySeverity * 0.3;  // Up to +0.3 at full severity

        return Math.Min(1.0, level);
    }

    public static bool IsFollowingAnimalSigns(GameContext ctx) =>
        ctx.CurrentActivity == ActivityType.Tracking;

    public static bool HasFreshTrail(GameContext ctx) =>
        ctx.Tensions.HasTension("FreshTrail");

    public static bool Vulnerable(GameContext ctx) =>
        ctx.Check(EventCondition.Injured) ||
        ctx.Check(EventCondition.Slow) ||
        ctx.Check(EventCondition.Impaired) ||
        !ctx.Inventory.HasWeapon ||
        ctx.player.Body.Blood.Condition < 0.7 ||
        GetWetness(ctx) > 0.5;

    public static double VulnerabilityLevel(GameContext ctx)
    {
        double level = 0;
        if (ctx.Check(EventCondition.Injured)) level += 0.25;
        if (ctx.Check(EventCondition.Slow)) level += 0.2;
        if (ctx.Check(EventCondition.Impaired)) level += 0.2;
        if (!ctx.Inventory.HasWeapon) level += 0.15;
        if (ctx.Check(EventCondition.Limping)) level += 0.1;
        if (ctx.Check(EventCondition.Winded)) level += 0.1;

        // Blood loss - scales with severity
        double bloodCondition = ctx.player.Body.Blood.Condition;
        if (bloodCondition < 0.5) level += 0.3;
        else if (bloodCondition < 0.7) level += 0.15;

        // Wetness impairs movement and reactions
        level += GetWetness(ctx) * 0.2;

        return Math.Min(1.0, level);
    }

    public static bool ResourceScarcity(GameContext ctx) =>
        ctx.CurrentLocation.GetFeature<ForageFeature>()?.IsDepleted() == true ||
        ctx.CurrentLocation.GetFeature<HarvestableFeature>()?.IsDepleted() == true ||
        ctx.CurrentLocation.GetFeature<AnimalTerritoryFeature>()?.CanHunt() == false;

    public static bool SupplyPressure(GameContext ctx) =>
        ctx.Check(EventCondition.LowOnFuel) ||
        ctx.Check(EventCondition.LowOnFood) ||
        ctx.Inventory.WaterLiters < 0.5;

    public static double SupplyPressureLevel(GameContext ctx)
    {
        double level = 0;
        if (ctx.Check(EventCondition.NoFuel)) level += 0.4;
        else if (ctx.Check(EventCondition.LowOnFuel)) level += 0.2;
        if (ctx.Check(EventCondition.NoFood)) level += 0.4;
        else if (ctx.Check(EventCondition.LowOnFood)) level += 0.2;
        if (ctx.Inventory.WaterLiters <= 0) level += 0.3;
        else if (ctx.Inventory.WaterLiters < 0.5) level += 0.15;
        return Math.Min(1.0, level);
    }

    public static bool Exposed(GameContext ctx) =>
        (ctx.Check(EventCondition.NoShelter) &&
         (ctx.Check(EventCondition.IsSnowing) ||
          ctx.Check(EventCondition.HighWind) ||
          ctx.Check(EventCondition.IsRaining))) ||
        (GetWetness(ctx) > 0.5 && ctx.Check(EventCondition.ExtremelyCold));

    public static bool HarshConditions(GameContext ctx) =>
        ctx.Check(EventCondition.IsBlizzard) ||
        ctx.Check(EventCondition.IsStormy) ||
        ctx.Check(EventCondition.ExtremelyCold);

    public static bool UnderThreat(GameContext ctx) =>
        ctx.Tensions.HasTension("Stalked") ||
        ctx.Tensions.HasTension("Hunted") ||
        ctx.Tensions.HasTension("PackNearby");

    public static bool UnderSeriousThreat(GameContext ctx) =>
        ctx.Tensions.HasTensionAbove("Stalked", 0.5) ||
        ctx.Tensions.HasTension("Hunted") ||
        ctx.Tensions.HasTensionAbove("PackNearby", 0.5);

    public static bool InCrisis(GameContext ctx) =>
        (Vulnerable(ctx) && UnderThreat(ctx)) ||
        (SupplyPressure(ctx) && Exposed(ctx)) ||
        ctx.Check(EventCondition.DeadlyColdCritical) ||
        ctx.player.Body.Blood.Condition < 0.5;

    public static bool FavorableConditions(GameContext ctx) =>
        ctx.Check(EventCondition.IsDaytime) &&
        ctx.Check(EventCondition.IsClear) &&
        !UnderThreat(ctx) &&
        !SupplyPressure(ctx);

    public static bool WellEquipped(GameContext ctx) =>
        ctx.Inventory.HasWeapon &&
        ctx.Check(EventCondition.HasFuel) &&
        ctx.Check(EventCondition.HasFood) &&
        !ctx.Check(EventCondition.Injured);

    public static bool HuntingAdvantage(GameContext ctx) =>
        ctx.Inventory.HasWeapon &&
        !ctx.player.EffectRegistry.HasEffect("Bleeding") &&
        !ctx.player.EffectRegistry.HasEffect("Bloody") &&
        GoodForStealth(ctx);

    public static bool Recovering(GameContext ctx) =>
        ctx.Check(EventCondition.AtCamp) &&
        ctx.Check(EventCondition.NearFire) &&
        ctx.Check(EventCondition.HasFood);

    public static bool Detectable(GameContext ctx) =>
        AttractiveToPredators(ctx) ||
        ctx.Check(EventCondition.HighVisibility) ||
        ctx.CurrentActivity == ActivityType.Traveling;

    public static bool GoodForStealth(GameContext ctx) =>
        ctx.Check(EventCondition.LowVisibility) &&
        !AttractiveToPredators(ctx) &&
        ctx.CurrentActivity != ActivityType.Traveling;

    public static bool NocturnalVulnerability(GameContext ctx) =>
        ctx.Check(EventCondition.Night) &&
        ctx.Check(EventCondition.InDarkness) &&
        ctx.Check(EventCondition.LowVisibility);

    public static bool InDarkness(GameContext ctx) =>
        ctx.Check(EventCondition.Night) ||
        ctx.Check(EventCondition.InDarkness);

    public static bool DarkPassageConditions(GameContext ctx)
    {
        // Must have a reason for darkness
        bool hasDarkness = ctx.Check(EventCondition.Night)
                        || ctx.Check(EventCondition.InDarkness)
                        || ctx.Check(EventCondition.LowVisibility);

        // Must have terrain that can create a "passage" (cover/obstruction)
        bool hasObstruction = ctx.Check(EventCondition.IsForest)
                           || ctx.Check(EventCondition.HighOverheadCover)
                           || ctx.Check(EventCondition.LowVisibility);

        return hasDarkness && hasObstruction;
    }

    public static bool CriticallyDepleted(GameContext ctx) =>
        ctx.Check(EventCondition.LowCalories) &&
        ctx.Check(EventCondition.LowHydration);

    public static double CriticallyDepletedLevel(GameContext ctx)
    {
        double level = 0;
        if (ctx.Check(EventCondition.LowCalories)) level += 0.5;
        if (ctx.Check(EventCondition.LowHydration)) level += 0.5;
        return Math.Min(1.0, level);
    }

    public static bool PsychologicallyCompromised(GameContext ctx) =>
        ctx.Check(EventCondition.Disturbed) ||
        ctx.Check(EventCondition.DisturbedHigh) ||
        ctx.Tensions.HasTension("Stalked");

    public static bool SeverelyCompromised(GameContext ctx) =>
        ctx.Check(EventCondition.DisturbedHigh) ||
        ctx.Tensions.HasTensionAbove("Stalked", 0.5);

    public static bool CognitivelyImpaired(GameContext ctx) =>
        ctx.Check(EventCondition.Clumsy) ||
        ctx.Check(EventCondition.Foggy) ||
        ctx.Check(EventCondition.Impaired);

    public static bool TrapLineActive(GameContext ctx) =>
        ctx.Check(EventCondition.SnareBaited) ||
        ctx.Check(EventCondition.SnareHasCatch) ||
        ctx.Tensions.HasTension("TrapLineActive");

    public static bool TrapLineAttractive(GameContext ctx) =>
        ctx.Check(EventCondition.SnareHasCatch) ||
        ctx.Check(EventCondition.SnareBaited);

    public static bool ExtremeColdCrisis(GameContext ctx) =>
        ctx.Check(EventCondition.ExtremelyCold) ||
        (ctx.Check(EventCondition.IsBlizzard) && ctx.Check(EventCondition.LowOnFuel)) ||
        (GetWetness(ctx) > 0.7 && ctx.Check(EventCondition.LowTemperature));

    public static double ExtremeColdLevel(GameContext ctx)
    {
        double level = 0;
        if (ctx.Check(EventCondition.ExtremelyCold)) level += 0.4;
        if (ctx.Check(EventCondition.IsBlizzard)) level += 0.25;

        // Fuel - use else-if to avoid double-counting
        if (ctx.Check(EventCondition.NoFuel)) level += 0.3;
        else if (ctx.Check(EventCondition.LowOnFuel)) level += 0.15;

        // Wetness massively compounds cold danger
        level += GetWetness(ctx) * 0.3;

        return Math.Min(1.0, level);
    }

    public static bool InfectionRisk(GameContext ctx) =>
        (ctx.Check(EventCondition.WoundUntreated) ||
         ctx.Check(EventCondition.WoundUntreatedHigh)) &&
        ctx.Check(EventCondition.LowTemperature);

    public static bool HasUntreatedWound(GameContext ctx) =>
        ctx.Check(EventCondition.WoundUntreated) ||
        ctx.Check(EventCondition.WoundUntreatedHigh);

    public static bool StructuralStress(GameContext ctx) =>
        ctx.Check(EventCondition.ShelterWeakened) &&
        (ctx.Check(EventCondition.HighWind) || ctx.Check(EventCondition.IsSnowing));

    public static double StructuralStressLevel(GameContext ctx)
    {
        double level = 0;
        if (ctx.Check(EventCondition.ShelterWeakened)) level += 0.4;
        if (ctx.Check(EventCondition.HighWind)) level += 0.3;
        if (ctx.Check(EventCondition.IsSnowing)) level += 0.2;
        if (ctx.Check(EventCondition.IsBlizzard)) level += 0.3;
        return Math.Min(1.0, level);
    }

    public static bool RemoteAndVulnerable(GameContext ctx) =>
        ctx.Check(EventCondition.FarFromCamp) &&
        (Vulnerable(ctx) || SupplyPressure(ctx));

    public static double IsolationLevel(GameContext ctx)
    {
        double level = 0;

        // Distance component
        if (ctx.Check(EventCondition.VeryFarFromCamp)) level += 0.4;
        else if (ctx.Check(EventCondition.FarFromCamp)) level += 0.2;

        // Resource pressure amplifies isolation
        level += SupplyPressureLevel(ctx) * 0.3;

        // Vulnerability amplifies isolation
        level += VulnerabilityLevel(ctx) * 0.3;

        return Math.Min(1.0, level);
    }

    public static bool AtTerrainTransition(GameContext ctx) =>
        ctx.Check(EventCondition.OnBoundary);

    public static bool TrappedByTerrain(GameContext ctx) =>
        ctx.Check(EventCondition.Cornered) ||
        ctx.Check(EventCondition.AtTerrainBottleneck);

    public static double TerrainHazardWeight(GameContext ctx, double baseWeight = 0.1, double scale = 3.0)
    {
        double hazard = ctx.CurrentLocation.GetEffectiveTerrainHazard();
        return baseWeight + hazard * scale;
    }

    public static bool IceHazard(GameContext ctx) =>
        ctx.Check(EventCondition.NearWater) &&
        (ctx.Check(EventCondition.LowTemperature) ||
         ctx.CurrentLocation.GetFeature<WaterFeature>()?.IsFrozen == true);

    public static double IceHazardLevel(GameContext ctx)
    {
        double level = 0;
        if (!ctx.Check(EventCondition.NearWater)) return 0;

        level += 0.3; // Base level for being near water
        var water = ctx.CurrentLocation.GetFeature<WaterFeature>();
        if (water?.IsFrozen == true)
        {
            level += 0.4;
            // Thin ice is more dangerous (IceThicknessLevel < 0.3 = thin)
            if (water.HasThinIce) level += 0.3;
        }
        if (ctx.Check(EventCondition.LowTemperature)) level += 0.2;
        return Math.Min(1.0, level);
    }

    public static bool StormApproaching(GameContext ctx) =>
        ctx.Check(EventCondition.WeatherWorsening);

    public static bool InActiveStorm(GameContext ctx) =>
        (ctx.Check(EventCondition.IsBlizzard) || ctx.Check(EventCondition.IsStormy)) &&
        ctx.Check(EventCondition.Outside);

    public static double StormDangerLevel(GameContext ctx)
    {
        double level = 0;
        if (ctx.Check(EventCondition.IsBlizzard)) level += 0.5;
        else if (ctx.Check(EventCondition.IsStormy)) level += 0.3;
        else if (ctx.Check(EventCondition.WeatherWorsening)) level += 0.15;

        if (ctx.Check(EventCondition.Outside)) level += 0.2;
        if (ctx.Check(EventCondition.FarFromCamp)) level += 0.2;
        if (ctx.Check(EventCondition.NoShelter)) level += 0.15;
        return Math.Min(1.0, level);
    }

    public static bool PackThreat(GameContext ctx) =>
        ctx.Tensions.HasTension("PackNearby") &&
        ctx.Check(EventCondition.HasPredators);

    public static double PackThreatLevel(GameContext ctx)
    {
        var packTension = ctx.Tensions.GetTension("PackNearby");
        if (packTension == null) return 0;

        double level = packTension.Severity;
        level += VulnerabilityLevel(ctx) * 0.3;
        level += PredatorAttractionLevel(ctx) * 0.2;
        return Math.Min(1.0, level);
    }

    public static bool HeavilyEncumbered(GameContext ctx) =>
        ctx.Inventory.MaxWeightKg > 0 &&
        ctx.Inventory.CurrentWeightKg > ctx.Inventory.MaxWeightKg * 0.75;

    public static double EncumbranceLevel(GameContext ctx)
    {
        if (ctx.Inventory.MaxWeightKg <= 0) return 0;
        double ratio = ctx.Inventory.CurrentWeightKg / ctx.Inventory.MaxWeightKg;
        return Math.Clamp(ratio, 0, 1.0);
    }

    public static bool EquipmentDegraded(GameContext ctx) =>
        HasWornEquipment(ctx, 0.4) || HasWornTool(ctx, 0.4);

    public static bool EquipmentCritical(GameContext ctx) =>
        HasWornEquipment(ctx, 0.25) || HasWornTool(ctx, 0.25);

    public static bool BootsWorn(GameContext ctx) =>
        GetEquipmentCondition(ctx, EquipSlot.Feet) is double c && c < 0.35;

    public static bool GlovesWorn(GameContext ctx) =>
        GetEquipmentCondition(ctx, EquipSlot.Hands) is double c && c < 0.4;

    public static bool ChestWrapCritical(GameContext ctx) =>
        GetEquipmentCondition(ctx, EquipSlot.Chest) is double c && c < 0.25;

    public static bool BladeWorn(GameContext ctx) =>
        GetToolCondition(ctx, ToolType.Knife) is double c && c < 0.3 ||
        GetToolCondition(ctx, ToolType.Axe) is double c2 && c2 < 0.3;

    public static bool FirestarterCritical(GameContext ctx) =>
        GetFirestarterCondition(ctx) is double c && c < 0.2;

    private static double GetWetness(GameContext ctx) =>
        ctx.player.EffectRegistry.GetEffectsByKind("Wet").FirstOrDefault()?.Severity ?? 0;

    private static bool HasWornEquipment(GameContext ctx, double threshold)
    {
        foreach (EquipSlot slot in Enum.GetValues<EquipSlot>())
        {
            var gear = ctx.Inventory.GetEquipment(slot);
            if (gear != null && gear.ConditionPct < threshold)
                return true;
        }
        return false;
    }

    private static bool HasWornTool(GameContext ctx, double threshold)
    {
        foreach (var tool in ctx.Inventory.Tools)
        {
            if (tool.ConditionPct < threshold)
                return true;
        }
        return false;
    }

    private static double? GetEquipmentCondition(GameContext ctx, EquipSlot slot)
    {
        var gear = ctx.Inventory.GetEquipment(slot);
        return gear?.ConditionPct;
    }

    private static double? GetToolCondition(GameContext ctx, ToolType type)
    {
        var tool = ctx.Inventory.Tools.FirstOrDefault(t => t.ToolType == type);
        return tool?.ConditionPct;
    }

    private static double? GetFirestarterCondition(GameContext ctx)
    {
        var firestarters = ctx.Inventory.Tools
            .Where(t => t.ToolType == ToolType.FireStriker ||
                       t.ToolType == ToolType.HandDrill ||
                       t.ToolType == ToolType.BowDrill)
            .ToList();

        if (firestarters.Count == 0) return null;
        return firestarters.Min(f => f.ConditionPct);
    }

    public static (EquipSlot Slot, double Condition)? GetWorstEquipment(GameContext ctx)
    {
        (EquipSlot Slot, double Condition)? worst = null;
        foreach (EquipSlot slot in Enum.GetValues<EquipSlot>())
        {
            var gear = ctx.Inventory.GetEquipment(slot);
            if (gear != null && (worst == null || gear.ConditionPct < worst.Value.Condition))
                worst = (slot, gear.ConditionPct);
        }
        return worst;
    }

    public static (Gear Tool, double Condition)? GetWorstTool(GameContext ctx)
    {
        Gear? worst = null;
        foreach (var tool in ctx.Inventory.Tools)
        {
            if (worst == null || tool.ConditionPct < worst.ConditionPct)
                worst = tool;
        }
        return worst != null ? (worst, worst.ConditionPct) : null;
    }

    public static bool PredatorInTerritory(GameContext ctx)
    {
        if (ctx.Map == null) return false;
        var pos = ctx.Map.CurrentPosition;
        return ctx.Herds.GetPredatorHerds()
            .Any(h => h.HomeTerritory.Contains(pos) && h.Count > 0);
    }

    public static bool PreyInTerritory(GameContext ctx)
    {
        if (ctx.Map == null) return false;
        var pos = ctx.Map.CurrentPosition;
        return ctx.Herds.GetPreyHerds()
            .Any(h => h.HomeTerritory.Contains(pos) && h.Count > 0);
    }

    public static bool PackPredatorInTerritory(GameContext ctx)
    {
        if (ctx.Map == null) return false;
        var pos = ctx.Map.CurrentPosition;
        return ctx.Herds.GetPredatorHerds()
            .Any(h => h.BehaviorType == HerdBehaviorType.PackPredator
                  && h.HomeTerritory.Contains(pos) && h.Count > 0);
    }

    public static bool SolitaryPredatorInTerritory(GameContext ctx)
    {
        if (ctx.Map == null) return false;
        var pos = ctx.Map.CurrentPosition;
        return ctx.Herds.GetPredatorHerds()
            .Any(h => h.BehaviorType == HerdBehaviorType.SolitaryPredator
                  && h.HomeTerritory.Contains(pos) && h.Count > 0);
    }

    public static double PredatorPresenceLevel(GameContext ctx)
    {
        if (ctx.Map == null) return 0;
        var pos = ctx.Map.CurrentPosition;
        var predators = ctx.Herds.GetPredatorHerds().Where(h => h.Count > 0).ToList();

        // On tile = maximum presence
        if (predators.Any(h => h.Position == pos)) return 1.0;

        // In territory = partial presence
        if (predators.Any(h => h.HomeTerritory.Contains(pos))) return 0.5;

        return 0;
    }

    public static double PreyPresenceLevel(GameContext ctx)
    {
        if (ctx.Map == null) return 0;
        var pos = ctx.Map.CurrentPosition;
        var prey = ctx.Herds.GetPreyHerds().Where(h => h.Count > 0).ToList();

        if (prey.Any(h => h.Position == pos)) return 1.0;
        if (prey.Any(h => h.HomeTerritory.Contains(pos))) return 0.5;

        return 0;
    }

    public static bool ScavengerInTerritory(GameContext ctx)
    {
        if (ctx.Map == null) return false;
        var pos = ctx.Map.CurrentPosition;
        return ctx.Herds.GetHerdsByBehavior(HerdBehaviorType.Scavenger)
            .Any(h => h.HomeTerritory.Contains(pos) && h.Count > 0);
    }

    public static bool ScavengerPresent(GameContext ctx)
    {
        if (ctx.Map == null) return false;
        var pos = ctx.Map.CurrentPosition;
        return ctx.Herds.GetHerdsByBehavior(HerdBehaviorType.Scavenger)
            .Any(h => h.Position == pos && h.Count > 0);
    }

    public static bool ScavengerWolfDynamics(GameContext ctx)
    {
        if (ctx.Map == null) return false;
        var pos = ctx.Map.CurrentPosition;

        bool hasScavengers = ctx.Herds.GetHerdsByBehavior(HerdBehaviorType.Scavenger)
            .Any(h => h.HomeTerritory.Contains(pos) && h.Count > 0);
        bool hasWolves = ctx.Herds.GetHerdsByType(AnimalType.Wolf)
            .Any(h => h.HomeTerritory.Contains(pos) && h.Count > 0);

        return hasScavengers && hasWolves;
    }

    public static double ScavengerThreatLevel(GameContext ctx)
    {
        if (ctx.Map == null) return 0;
        var pos = ctx.Map.CurrentPosition;

        double level = 0;

        // Direct presence
        if (ctx.Herds.GetHerdsByBehavior(HerdBehaviorType.Scavenger)
            .Any(h => h.Position == pos && h.Count > 0))
        {
            level += 0.5;
        }
        // In territory
        else if (ScavengerInTerritory(ctx))
        {
            level += 0.2;
        }

        // Carrying meat attracts scavengers
        if (ctx.Inventory.HasMeat) level += 0.2;

        // Vulnerability emboldens scavengers
        level += VulnerabilityLevel(ctx) * 0.2;

        // Night makes hyenas bolder
        if (ctx.Check(EventCondition.Night)) level += 0.1;

        return Math.Min(1.0, level);
    }

    public static bool InSaberToothTerritory(GameContext ctx)
    {
        if (ctx.Map == null) return false;
        var pos = ctx.Map.CurrentPosition;
        return ctx.Herds.GetHerdsByType(AnimalType.SaberTooth)
            .Any(h => h.HomeTerritory.Contains(pos) && h.Count > 0);
    }

    public static bool SaberToothThreat(GameContext ctx) =>
        ctx.Tensions.HasTension("SaberToothStalked");

    public static bool SaberToothCritical(GameContext ctx) =>
        ctx.Tensions.HasTensionAbove("SaberToothStalked", 0.6);

    public static double SaberToothThreatLevel(GameContext ctx)
    {
        var tension = ctx.Tensions.GetTension("SaberToothStalked");
        if (tension == null) return InSaberToothTerritory(ctx) ? 0.1 : 0;

        double level = tension.Severity;

        // Low visibility favors ambush predator
        if (ctx.Check(EventCondition.LowVisibility)) level += 0.15;

        // Working or distracted = vulnerable
        if (ctx.CurrentActivity != ActivityType.Traveling &&
            ctx.CurrentActivity != ActivityType.Resting)
        {
            level += 0.1;
        }

        return Math.Min(1.0, level);
    }

    public static bool InMammothTerritory(GameContext ctx)
    {
        if (ctx.Map == null) return false;
        var pos = ctx.Map.CurrentPosition;
        return ctx.Herds.GetHerdsByType(AnimalType.Mammoth)
            .Any(h => h.HomeTerritory.Contains(pos) && h.Count > 0);
    }

    public static bool NearMammothHerd(GameContext ctx)
    {
        if (ctx.Map == null) return false;
        var pos = ctx.Map.CurrentPosition;
        return ctx.Herds.GetHerdsByType(AnimalType.Mammoth)
            .Any(h => h.Count > 0 && h.Position.ManhattanDistance(pos) <= 2);
    }

    public static bool MammothHerdPresent(GameContext ctx)
    {
        if (ctx.Map == null) return false;
        var pos = ctx.Map.CurrentPosition;
        return ctx.Herds.GetHerdsByType(AnimalType.Mammoth)
            .Any(h => h.Position == pos && h.Count > 0);
    }

    public static Herd? GetMammothHerd(GameContext ctx)
    {
        return ctx.Herds.GetHerdsByType(AnimalType.Mammoth)
            .FirstOrDefault(h => h.Count > 0);
    }

    public static double MammothProximityLevel(GameContext ctx)
    {
        if (ctx.Map == null) return 0;
        var pos = ctx.Map.CurrentPosition;

        var mammothHerd = GetMammothHerd(ctx);
        if (mammothHerd == null) return 0;

        int distance = mammothHerd.Position.ManhattanDistance(pos);
        if (distance == 0) return 1.0;
        if (distance == 1) return 0.5;
        if (distance == 2) return 0.3;
        if (mammothHerd.HomeTerritory.Contains(pos)) return 0.1;

        return 0;
    }

    public static bool MammothHerdAggravated(GameContext ctx)
    {
        var herd = GetMammothHerd(ctx);
        return herd != null && (herd.State == HerdState.Alert || herd.State == HerdState.Fleeing);
    }

    public static bool CarcassPresent(GameContext ctx)
    {
        var carcass = ctx.CurrentLocation.GetFeature<CarcassFeature>();
        return carcass != null && carcass.GetTotalRemainingKg() > 0;
    }

    public static bool FreshCarcassPresent(GameContext ctx)
    {
        var carcass = ctx.CurrentLocation.GetFeature<CarcassFeature>();
        return carcass != null &&
               carcass.GetTotalRemainingKg() > 0 &&
               carcass.DecayLevel < 0.5; // Fresh or good condition
    }

    public static bool CarcassContested(GameContext ctx) =>
        CarcassPresent(ctx) &&
        (ctx.Tensions.HasTension("ScavengersWaiting") || ScavengerPresent(ctx));
}
