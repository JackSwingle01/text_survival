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
    // === PREDATOR ATTRACTION ===

    /// <summary>
    /// Player state that attracts predators.
    /// Combines: carrying meat, bleeding, bloody, food scent tension.
    /// </summary>
    public static bool AttractiveToPredators(GameContext ctx) =>
        ctx.Inventory.HasMeat ||
        ctx.player.EffectRegistry.HasEffect("Bleeding") ||
        ctx.player.EffectRegistry.HasEffect("Bloody") ||
        ctx.Tensions.HasTension("FoodScentStrong");

    /// <summary>
    /// Graduated predator attraction level (0-1).
    /// Use for weight multipliers: higher = more likely to attract events.
    /// </summary>
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

    // === FORAGING CONTEXT ===

    /// <summary>
    /// Player is following animal sign clues while foraging.
    /// Detected via ActivityType.Tracking (set by ForageStrategy when following animal clues).
    /// Higher chance of wildlife encounters - both opportunity and danger.
    /// </summary>
    public static bool IsFollowingAnimalSigns(GameContext ctx) =>
        ctx.CurrentActivity == ActivityType.Tracking;

    /// <summary>
    /// Player has a fresh trail tension from examining animal signs.
    /// Boosts hunting event chances.
    /// </summary>
    public static bool HasFreshTrail(GameContext ctx) =>
        ctx.Tensions.HasTension("FreshTrail");

    // === VULNERABILITY ===

    /// <summary>
    /// Player is vulnerable to threats.
    /// Combines: injured, slow, impaired, no weapon, significant blood loss, soaked.
    /// </summary>
    public static bool Vulnerable(GameContext ctx) =>
        ctx.Check(EventCondition.Injured) ||
        ctx.Check(EventCondition.Slow) ||
        ctx.Check(EventCondition.Impaired) ||
        !ctx.Inventory.HasWeapon ||
        ctx.player.Body.Blood.Condition < 0.7 ||
        GetWetness(ctx) > 0.5;

    /// <summary>
    /// Graduated vulnerability level (0-1).
    /// </summary>
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

    // === RESOURCE PRESSURE ===

    /// <summary>
    /// Location resources are depleted.
    /// </summary>
    public static bool ResourceScarcity(GameContext ctx) =>
        ctx.CurrentLocation.GetFeature<ForageFeature>()?.IsDepleted() == true ||
        ctx.CurrentLocation.GetFeature<HarvestableFeature>()?.IsDepleted() == true ||
        ctx.CurrentLocation.GetFeature<AnimalTerritoryFeature>()?.CanHunt() == false;

    /// <summary>
    /// Player is running low on essentials.
    /// Combines: low fuel, low food, low water.
    /// </summary>
    public static bool SupplyPressure(GameContext ctx) =>
        ctx.Check(EventCondition.LowOnFuel) ||
        ctx.Check(EventCondition.LowOnFood) ||
        ctx.Inventory.WaterLiters < 0.5;

    /// <summary>
    /// Graduated supply pressure (0-1).
    /// </summary>
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

    // === EXPOSURE ===

    /// <summary>
    /// Player is exposed to elements.
    /// Combines: no shelter + bad weather, or soaked in freezing temps.
    /// </summary>
    public static bool Exposed(GameContext ctx) =>
        (ctx.Check(EventCondition.NoShelter) &&
         (ctx.Check(EventCondition.IsSnowing) ||
          ctx.Check(EventCondition.HighWind) ||
          ctx.Check(EventCondition.IsRaining))) ||
        (GetWetness(ctx) > 0.5 && ctx.Check(EventCondition.ExtremelyCold));

    /// <summary>
    /// Player is in harsh conditions regardless of shelter.
    /// </summary>
    public static bool HarshConditions(GameContext ctx) =>
        ctx.Check(EventCondition.IsBlizzard) ||
        ctx.Check(EventCondition.IsStormy) ||
        ctx.Check(EventCondition.ExtremelyCold);

    // === DANGER ===

    /// <summary>
    /// Active predator threat exists.
    /// </summary>
    public static bool UnderThreat(GameContext ctx) =>
        ctx.Tensions.HasTension("Stalked") ||
        ctx.Tensions.HasTension("Hunted") ||
        ctx.Tensions.HasTension("PackNearby");

    /// <summary>
    /// High-severity predator threat.
    /// </summary>
    public static bool UnderSeriousThreat(GameContext ctx) =>
        ctx.Tensions.HasTensionAbove("Stalked", 0.5) ||
        ctx.Tensions.HasTension("Hunted") ||
        ctx.Tensions.HasTensionAbove("PackNearby", 0.5);

    /// <summary>
    /// Compound danger - multiple pressures overlapping.
    /// This is when things get desperate.
    /// </summary>
    public static bool InCrisis(GameContext ctx) =>
        (Vulnerable(ctx) && UnderThreat(ctx)) ||
        (SupplyPressure(ctx) && Exposed(ctx)) ||
        ctx.Check(EventCondition.DeadlyColdCritical) ||
        ctx.player.Body.Blood.Condition < 0.5;

    // === FAVORABLE CONDITIONS ===

    /// <summary>
    /// Conditions favor the player - good for positive events.
    /// </summary>
    public static bool FavorableConditions(GameContext ctx) =>
        ctx.Check(EventCondition.IsDaytime) &&
        ctx.Check(EventCondition.IsClear) &&
        !UnderThreat(ctx) &&
        !SupplyPressure(ctx);

    /// <summary>
    /// Player is well-prepared for the field.
    /// </summary>
    public static bool WellEquipped(GameContext ctx) =>
        ctx.Inventory.HasWeapon &&
        ctx.Check(EventCondition.HasFuel) &&
        ctx.Check(EventCondition.HasFood) &&
        !ctx.Check(EventCondition.Injured);

    /// <summary>
    /// Player has hunting advantage - good for positive hunt outcomes.
    /// Combines: weapon equipped, no blood scent (bleeding or bloody), good stealth conditions.
    /// Use for weighting positive outcomes in hunting/ambush events.
    /// </summary>
    public static bool HuntingAdvantage(GameContext ctx) =>
        ctx.Inventory.HasWeapon &&
        !ctx.player.EffectRegistry.HasEffect("Bleeding") &&
        !ctx.player.EffectRegistry.HasEffect("Bloody") &&
        GoodForStealth(ctx);

    /// <summary>
    /// Player is recovering at camp - good for positive camp events.
    /// Combines: at camp, near fire, has food.
    /// Use for weighting recovery events and positive camp outcomes.
    /// </summary>
    public static bool Recovering(GameContext ctx) =>
        ctx.Check(EventCondition.AtCamp) &&
        ctx.Check(EventCondition.NearFire) &&
        ctx.Check(EventCondition.HasFood);

    // === STEALTH / DETECTION ===

    /// <summary>
    /// Player is likely to be detected by wildlife.
    /// Combines: noise, scent, visibility factors.
    /// </summary>
    public static bool Detectable(GameContext ctx) =>
        AttractiveToPredators(ctx) ||
        ctx.Check(EventCondition.HighVisibility) ||
        ctx.CurrentActivity == ActivityType.Traveling;

    /// <summary>
    /// Good conditions for stealth/ambush.
    /// </summary>
    public static bool GoodForStealth(GameContext ctx) =>
        ctx.Check(EventCondition.LowVisibility) &&
        !AttractiveToPredators(ctx) &&
        ctx.CurrentActivity != ActivityType.Traveling;

    // === NOCTURNAL VULNERABILITY ===

    /// <summary>
    /// Night conditions remove visual control and increase psychological pressure.
    /// Combines: Night, InDarkness, LowVisibility.
    /// Found in: Camp, Trapping, and Threat events.
    /// </summary>
    public static bool NocturnalVulnerability(GameContext ctx) =>
        ctx.Check(EventCondition.Night) &&
        ctx.Check(EventCondition.InDarkness) &&
        ctx.Check(EventCondition.LowVisibility);

    /// <summary>
    /// Any night vulnerability factor present.
    /// Lighter check than full NocturnalVulnerability.
    /// </summary>
    public static bool InDarkness(GameContext ctx) =>
        ctx.Check(EventCondition.Night) ||
        ctx.Check(EventCondition.InDarkness);

    // === NUTRITIONAL DEPLETION ===

    /// <summary>
    /// Nutritional deprivation compounds physical and mental breakdown.
    /// Combines: LowCalories and LowHydration together.
    /// Found in: MuscleCramp, VisionBlur, MomentOfClarity, FugueState, TheShakes.
    /// </summary>
    public static bool CriticallyDepleted(GameContext ctx) =>
        ctx.Check(EventCondition.LowCalories) &&
        ctx.Check(EventCondition.LowHydration);

    /// <summary>
    /// Graduated depletion level (0-1).
    /// Accounts for severity of caloric and hydration deficits.
    /// </summary>
    public static double CriticallyDepletedLevel(GameContext ctx)
    {
        double level = 0;
        if (ctx.Check(EventCondition.LowCalories)) level += 0.5;
        if (ctx.Check(EventCondition.LowHydration)) level += 0.5;
        return Math.Min(1.0, level);
    }

    // === PSYCHOLOGICAL STRESS ===

    /// <summary>
    /// Psychological stress creates escalating perception issues.
    /// Combines: Disturbed states and Stalked tensions.
    /// Found in: ParanoiaEvent, NightTerrors, ShadowMovement, ProcessingTrauma, Nightmare.
    /// </summary>
    public static bool PsychologicallyCompromised(GameContext ctx) =>
        ctx.Check(EventCondition.Disturbed) ||
        ctx.Check(EventCondition.DisturbedHigh) ||
        ctx.Tensions.HasTension("Stalked");

    /// <summary>
    /// Severe psychological compromise.
    /// High-severity disturbed or stalked states.
    /// </summary>
    public static bool SeverelyCompromised(GameContext ctx) =>
        ctx.Check(EventCondition.DisturbedHigh) ||
        ctx.Tensions.HasTensionAbove("Stalked", 0.5);

    // === COGNITIVE IMPAIRMENT ===

    /// <summary>
    /// Mental and physical coordination impaired.
    /// Combines: Clumsy, Foggy, Impaired conditions.
    /// Found in: TrappingAccident, FumblingHands, DulledSenses, LostYourBearings.
    /// </summary>
    public static bool CognitivelyImpaired(GameContext ctx) =>
        ctx.Check(EventCondition.Clumsy) ||
        ctx.Check(EventCondition.Foggy) ||
        ctx.Check(EventCondition.Impaired);

    // === TRAP LINE STATUS ===

    /// <summary>
    /// Active traps create scent and resources that attract danger.
    /// Combines: SnareBaited, SnareHasCatch, TrapLineActive tension.
    /// Found in: SnareTampered, PredatorAtTrapLine, TrapLinePlundered, BaitedTrapAttention.
    /// </summary>
    public static bool TrapLineActive(GameContext ctx) =>
        ctx.Check(EventCondition.SnareBaited) ||
        ctx.Check(EventCondition.SnareHasCatch) ||
        ctx.Tensions.HasTension("TrapLineActive");

    /// <summary>
    /// Trap line has catch or bait that would attract predators.
    /// Higher-value target than just having traps set.
    /// </summary>
    public static bool TrapLineAttractive(GameContext ctx) =>
        ctx.Check(EventCondition.SnareHasCatch) ||
        ctx.Check(EventCondition.SnareBaited);

    // === EXTREME COLD ===

    /// <summary>
    /// Temperature has crossed into fatal territory.
    /// Combines: ExtremelyCold, IsBlizzard + LowOnFuel, or soaked in cold.
    /// Found in: TheWindShifts, TheFind, FrozenFingers.
    /// </summary>
    public static bool ExtremeColdCrisis(GameContext ctx) =>
        ctx.Check(EventCondition.ExtremelyCold) ||
        (ctx.Check(EventCondition.IsBlizzard) && ctx.Check(EventCondition.LowOnFuel)) ||
        (GetWetness(ctx) > 0.7 && ctx.Check(EventCondition.LowTemperature));

    /// <summary>
    /// Graduated cold crisis level (0-1).
    /// </summary>
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

    // === INFECTION RISK ===

    /// <summary>
    /// Wounds fester in cold conditions.
    /// Combines: WoundUntreated states with LowTemperature.
    /// Found in: SomethingWrong (Fever arc).
    /// </summary>
    public static bool InfectionRisk(GameContext ctx) =>
        (ctx.Check(EventCondition.WoundUntreated) ||
         ctx.Check(EventCondition.WoundUntreatedHigh)) &&
        ctx.Check(EventCondition.LowTemperature);

    /// <summary>
    /// Any untreated wound present, regardless of temperature.
    /// </summary>
    public static bool HasUntreatedWound(GameContext ctx) =>
        ctx.Check(EventCondition.WoundUntreated) ||
        ctx.Check(EventCondition.WoundUntreatedHigh);

    // === STRUCTURAL STRESS ===

    /// <summary>
    /// Multiple environmental factors compound shelter stability.
    /// Combines: HighWind, IsSnowing, ShelterWeakened.
    /// Found in: ShelterGroans.
    /// </summary>
    public static bool StructuralStress(GameContext ctx) =>
        ctx.Check(EventCondition.ShelterWeakened) &&
        (ctx.Check(EventCondition.HighWind) || ctx.Check(EventCondition.IsSnowing));

    /// <summary>
    /// Graduated structural stress level (0-1).
    /// </summary>
    public static double StructuralStressLevel(GameContext ctx)
    {
        double level = 0;
        if (ctx.Check(EventCondition.ShelterWeakened)) level += 0.4;
        if (ctx.Check(EventCondition.HighWind)) level += 0.3;
        if (ctx.Check(EventCondition.IsSnowing)) level += 0.2;
        if (ctx.Check(EventCondition.IsBlizzard)) level += 0.3;
        return Math.Min(1.0, level);
    }

    // === SPATIAL ISOLATION ===

    /// <summary>
    /// Player is far from camp in dangerous conditions.
    /// Distance + vulnerability creates compounding pressure.
    /// </summary>
    public static bool RemoteAndVulnerable(GameContext ctx) =>
        ctx.Check(EventCondition.FarFromCamp) &&
        (Vulnerable(ctx) || SupplyPressure(ctx));

    /// <summary>
    /// Graduated isolation level (0-1).
    /// Combines distance, resource pressure, and threats.
    /// </summary>
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

    // === TERRAIN CONTEXT ===

    /// <summary>
    /// Player is at a terrain boundary - transition zones create opportunities and threats.
    /// Forest edge, water's edge, mountain approach.
    /// </summary>
    public static bool AtTerrainTransition(GameContext ctx) =>
        ctx.Check(EventCondition.OnBoundary);

    /// <summary>
    /// Terrain limits escape options - cornered or in bottleneck.
    /// </summary>
    public static bool TrappedByTerrain(GameContext ctx) =>
        ctx.Check(EventCondition.Cornered) ||
        ctx.Check(EventCondition.AtTerrainBottleneck);

    // === ICE/WATER HAZARDS ===

    /// <summary>
    /// Near frozen water with ice hazard potential.
    /// Combines: NearWater + LowTemperature or frozen water feature.
    /// Found in: WaterCrossing, EdgeOfTheIce, travel events.
    /// </summary>
    public static bool IceHazard(GameContext ctx) =>
        ctx.Check(EventCondition.NearWater) &&
        (ctx.Check(EventCondition.LowTemperature) ||
         ctx.CurrentLocation.GetFeature<WaterFeature>()?.IsFrozen == true);

    /// <summary>
    /// Graduated ice hazard level (0-1).
    /// Accounts for ice thickness and temperature.
    /// </summary>
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

    // === STORM CONDITIONS ===

    /// <summary>
    /// Storm conditions are building or imminent.
    /// Use for pre-storm warning events.
    /// </summary>
    public static bool StormApproaching(GameContext ctx) =>
        ctx.Check(EventCondition.WeatherWorsening);

    /// <summary>
    /// Active dangerous storm conditions while outside.
    /// Combines: IsBlizzard or IsStormy with being outside.
    /// </summary>
    public static bool InActiveStorm(GameContext ctx) =>
        (ctx.Check(EventCondition.IsBlizzard) || ctx.Check(EventCondition.IsStormy)) &&
        ctx.Check(EventCondition.Outside);

    /// <summary>
    /// Graduated storm danger level (0-1).
    /// Accounts for storm severity, shelter, and distance from safety.
    /// </summary>
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

    // === PACK DYNAMICS ===

    /// <summary>
    /// Pack predators are actively threatening.
    /// Combines: PackNearby tension with predator territory.
    /// </summary>
    public static bool PackThreat(GameContext ctx) =>
        ctx.Tensions.HasTension("PackNearby") &&
        ctx.Check(EventCondition.HasPredators);

    /// <summary>
    /// Graduated pack threat level (0-1).
    /// Accounts for pack tension severity and player vulnerability.
    /// </summary>
    public static double PackThreatLevel(GameContext ctx)
    {
        var packTension = ctx.Tensions.GetTension("PackNearby");
        if (packTension == null) return 0;

        double level = packTension.Severity;
        level += VulnerabilityLevel(ctx) * 0.3;
        level += PredatorAttractionLevel(ctx) * 0.2;
        return Math.Min(1.0, level);
    }

    // === ENCUMBRANCE ===

    /// <summary>
    /// Player is heavily loaded, affecting movement and accident risk.
    /// Triggers at 75% of max capacity.
    /// </summary>
    public static bool HeavilyEncumbered(GameContext ctx) =>
        ctx.Inventory.MaxWeightKg > 0 &&
        ctx.Inventory.CurrentWeightKg > ctx.Inventory.MaxWeightKg * 0.75;

    /// <summary>
    /// Graduated encumbrance level (0-1).
    /// Linear scale based on weight ratio.
    /// </summary>
    public static double EncumbranceLevel(GameContext ctx)
    {
        if (ctx.Inventory.MaxWeightKg <= 0) return 0;
        double ratio = ctx.Inventory.CurrentWeightKg / ctx.Inventory.MaxWeightKg;
        return Math.Clamp(ratio, 0, 1.0);
    }

    // === EQUIPMENT DEGRADATION ===

    /// <summary>
    /// Any equipment or tool is worn (below threshold).
    /// Use for triggering equipment maintenance events.
    /// </summary>
    public static bool EquipmentDegraded(GameContext ctx) =>
        HasWornEquipment(ctx, 0.4) || HasWornTool(ctx, 0.4);

    /// <summary>
    /// Any equipment or tool is critically worn (below 25%).
    /// Use for urgent repair events.
    /// </summary>
    public static bool EquipmentCritical(GameContext ctx) =>
        HasWornEquipment(ctx, 0.25) || HasWornTool(ctx, 0.25);

    /// <summary>
    /// Boots specifically are worn. Triggers during travel.
    /// </summary>
    public static bool BootsWorn(GameContext ctx) =>
        GetEquipmentCondition(ctx, EquipSlot.Feet) is double c && c < 0.35;

    /// <summary>
    /// Gloves specifically are worn. Triggers during work.
    /// </summary>
    public static bool GlovesWorn(GameContext ctx) =>
        GetEquipmentCondition(ctx, EquipSlot.Hands) is double c && c < 0.4;

    /// <summary>
    /// Chest wrap is critically worn. Triggers during travel/exposure.
    /// </summary>
    public static bool ChestWrapCritical(GameContext ctx) =>
        GetEquipmentCondition(ctx, EquipSlot.Chest) is double c && c < 0.25;

    /// <summary>
    /// Any cutting tool (knife/axe) is worn.
    /// </summary>
    public static bool BladeWorn(GameContext ctx) =>
        GetToolCondition(ctx, ToolType.Knife) is double c && c < 0.3 ||
        GetToolCondition(ctx, ToolType.Axe) is double c2 && c2 < 0.3;

    /// <summary>
    /// Firestarter is critically worn.
    /// </summary>
    public static bool FirestarterCritical(GameContext ctx) =>
        GetFirestarterCondition(ctx) is double c && c < 0.2;

    // === HELPERS ===

    /// <summary>
    /// Get current wetness severity (0-1).
    /// </summary>
    private static double GetWetness(GameContext ctx) =>
        ctx.player.EffectRegistry.GetEffectsByKind("Wet").FirstOrDefault()?.Severity ?? 0;

    /// <summary>
    /// Check if any worn equipment is below the threshold.
    /// </summary>
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

    /// <summary>
    /// Check if any tool is below the threshold.
    /// </summary>
    private static bool HasWornTool(GameContext ctx, double threshold)
    {
        foreach (var tool in ctx.Inventory.Tools)
        {
            if (tool.ConditionPct < threshold)
                return true;
        }
        return false;
    }

    /// <summary>
    /// Get condition of equipment in a specific slot. Returns null if empty.
    /// </summary>
    private static double? GetEquipmentCondition(GameContext ctx, EquipSlot slot)
    {
        var gear = ctx.Inventory.GetEquipment(slot);
        return gear?.ConditionPct;
    }

    /// <summary>
    /// Get condition of a specific tool type. Returns null if not owned.
    /// </summary>
    private static double? GetToolCondition(GameContext ctx, ToolType type)
    {
        var tool = ctx.Inventory.Tools.FirstOrDefault(t => t.ToolType == type);
        return tool?.ConditionPct;
    }

    /// <summary>
    /// Get condition of best firestarter. Returns null if none owned.
    /// </summary>
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

    /// <summary>
    /// Get the worst-condition equipment item. Returns (slot, condition) or null.
    /// </summary>
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

    /// <summary>
    /// Get the worst-condition tool. Returns (tool, condition) or null.
    /// </summary>
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

    // === HERD PRESENCE ===

    /// <summary>
    /// Any predator herd has this tile in its territory.
    /// </summary>
    public static bool PredatorInTerritory(GameContext ctx)
    {
        if (ctx.Map == null) return false;
        var pos = ctx.Map.CurrentPosition;
        return ctx.Herds.GetPredatorHerds()
            .Any(h => h.HomeTerritory.Contains(pos) && h.Count > 0);
    }

    /// <summary>
    /// Any prey herd has this tile in its territory.
    /// </summary>
    public static bool PreyInTerritory(GameContext ctx)
    {
        if (ctx.Map == null) return false;
        var pos = ctx.Map.CurrentPosition;
        return ctx.Herds.GetPreyHerds()
            .Any(h => h.HomeTerritory.Contains(pos) && h.Count > 0);
    }

    /// <summary>
    /// Pack predator (wolf) has this tile in territory.
    /// </summary>
    public static bool PackPredatorInTerritory(GameContext ctx)
    {
        if (ctx.Map == null) return false;
        var pos = ctx.Map.CurrentPosition;
        return ctx.Herds.GetPredatorHerds()
            .Any(h => h.BehaviorType == HerdBehaviorType.PackPredator
                  && h.HomeTerritory.Contains(pos) && h.Count > 0);
    }

    /// <summary>
    /// Solitary predator (bear) has this tile in territory.
    /// </summary>
    public static bool SolitaryPredatorInTerritory(GameContext ctx)
    {
        if (ctx.Map == null) return false;
        var pos = ctx.Map.CurrentPosition;
        return ctx.Herds.GetPredatorHerds()
            .Any(h => h.BehaviorType == HerdBehaviorType.SolitaryPredator
                  && h.HomeTerritory.Contains(pos) && h.Count > 0);
    }

    /// <summary>
    /// Graduated predator presence (0-1) for weight factors.
    /// Higher when predator is on tile, lower when just in territory.
    /// </summary>
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

    /// <summary>
    /// Graduated prey presence (0-1) for weight factors.
    /// </summary>
    public static double PreyPresenceLevel(GameContext ctx)
    {
        if (ctx.Map == null) return 0;
        var pos = ctx.Map.CurrentPosition;
        var prey = ctx.Herds.GetPreyHerds().Where(h => h.Count > 0).ToList();

        if (prey.Any(h => h.Position == pos)) return 1.0;
        if (prey.Any(h => h.HomeTerritory.Contains(pos))) return 0.5;

        return 0;
    }

    // === SCAVENGER DYNAMICS ===

    /// <summary>
    /// Scavenger (hyena) herd has this tile in territory.
    /// </summary>
    public static bool ScavengerInTerritory(GameContext ctx)
    {
        if (ctx.Map == null) return false;
        var pos = ctx.Map.CurrentPosition;
        return ctx.Herds.GetHerdsByBehavior(HerdBehaviorType.Scavenger)
            .Any(h => h.HomeTerritory.Contains(pos) && h.Count > 0);
    }

    /// <summary>
    /// Scavenger is on player's current tile.
    /// </summary>
    public static bool ScavengerPresent(GameContext ctx)
    {
        if (ctx.Map == null) return false;
        var pos = ctx.Map.CurrentPosition;
        return ctx.Herds.GetHerdsByBehavior(HerdBehaviorType.Scavenger)
            .Any(h => h.Position == pos && h.Count > 0);
    }

    /// <summary>
    /// Both wolves and hyenas are active in this area.
    /// Creates three-way competition dynamics.
    /// </summary>
    public static bool ScavengerWolfDynamics(GameContext ctx)
    {
        if (ctx.Map == null) return false;
        var pos = ctx.Map.CurrentPosition;

        bool hasScavengers = ctx.Herds.GetHerdsByBehavior(HerdBehaviorType.Scavenger)
            .Any(h => h.HomeTerritory.Contains(pos) && h.Count > 0);
        bool hasWolves = ctx.Herds.GetHerdsByType("Wolf")
            .Any(h => h.HomeTerritory.Contains(pos) && h.Count > 0);

        return hasScavengers && hasWolves;
    }

    /// <summary>
    /// Graduated scavenger threat level (0-1).
    /// Higher when scavengers are present, player is vulnerable, or carrying meat.
    /// </summary>
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

    // === SABER-TOOTH DYNAMICS ===

    /// <summary>
    /// Saber-tooth tiger has this tile in territory.
    /// </summary>
    public static bool InSaberToothTerritory(GameContext ctx)
    {
        if (ctx.Map == null) return false;
        var pos = ctx.Map.CurrentPosition;
        return ctx.Herds.GetHerdsByType("Saber-Tooth")
            .Any(h => h.HomeTerritory.Contains(pos) && h.Count > 0);
    }

    /// <summary>
    /// Player has active saber-tooth stalking tension.
    /// </summary>
    public static bool SaberToothThreat(GameContext ctx) =>
        ctx.Tensions.HasTension("SaberToothStalked");

    /// <summary>
    /// Saber-tooth tension is at critical level (> 0.6).
    /// Ambush imminent.
    /// </summary>
    public static bool SaberToothCritical(GameContext ctx) =>
        ctx.Tensions.HasTensionAbove("SaberToothStalked", 0.6);

    /// <summary>
    /// Graduated saber-tooth threat level (0-1).
    /// Based on tension severity and visibility conditions.
    /// </summary>
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

    // === MAMMOTH DYNAMICS ===

    /// <summary>
    /// Player is in mammoth herd's territory.
    /// </summary>
    public static bool InMammothTerritory(GameContext ctx)
    {
        if (ctx.Map == null) return false;
        var pos = ctx.Map.CurrentPosition;
        return ctx.Herds.GetHerdsByType("Woolly Mammoth")
            .Any(h => h.HomeTerritory.Contains(pos) && h.Count > 0);
    }

    /// <summary>
    /// Player is near mammoth herd (1-2 tiles away).
    /// </summary>
    public static bool NearMammothHerd(GameContext ctx)
    {
        if (ctx.Map == null) return false;
        var pos = ctx.Map.CurrentPosition;
        return ctx.Herds.GetHerdsByType("Woolly Mammoth")
            .Any(h => h.Count > 0 && h.Position.ManhattanDistance(pos) <= 2);
    }

    /// <summary>
    /// Mammoth herd is on player's current tile.
    /// </summary>
    public static bool MammothHerdPresent(GameContext ctx)
    {
        if (ctx.Map == null) return false;
        var pos = ctx.Map.CurrentPosition;
        return ctx.Herds.GetHerdsByType("Woolly Mammoth")
            .Any(h => h.Position == pos && h.Count > 0);
    }

    /// <summary>
    /// Get the mammoth herd if present or nearby.
    /// Returns null if no mammoth herd exists.
    /// </summary>
    public static Herd? GetMammothHerd(GameContext ctx)
    {
        return ctx.Herds.GetHerdsByType("Woolly Mammoth")
            .FirstOrDefault(h => h.Count > 0);
    }

    /// <summary>
    /// Graduated mammoth proximity level (0-1).
    /// 1.0 = on tile, 0.5 = adjacent, 0.3 = 2 tiles away, 0.1 = in territory.
    /// </summary>
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

    /// <summary>
    /// Mammoth herd is alert or fleeing (dangerous to approach).
    /// </summary>
    public static bool MammothHerdAggravated(GameContext ctx)
    {
        var herd = GetMammothHerd(ctx);
        return herd != null && (herd.State == HerdState.Alert || herd.State == HerdState.Fleeing);
    }

    // === CARCASS DYNAMICS ===

    /// <summary>
    /// There is a carcass at the current location.
    /// </summary>
    public static bool CarcassPresent(GameContext ctx)
    {
        var carcass = ctx.CurrentLocation.GetFeature<CarcassFeature>();
        return carcass != null && carcass.GetTotalRemainingKg() > 0;
    }

    /// <summary>
    /// There is a fresh carcass (low decay) at the current location.
    /// </summary>
    public static bool FreshCarcassPresent(GameContext ctx)
    {
        var carcass = ctx.CurrentLocation.GetFeature<CarcassFeature>();
        return carcass != null &&
               carcass.GetTotalRemainingKg() > 0 &&
               carcass.DecayLevel < 0.5; // Fresh or good condition
    }

    /// <summary>
    /// Carcass is being contested - scavengers are waiting.
    /// </summary>
    public static bool CarcassContested(GameContext ctx) =>
        CarcassPresent(ctx) &&
        (ctx.Tensions.HasTension("ScavengersWaiting") || ScavengerPresent(ctx));
}
