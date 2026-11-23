namespace Survival;

/// GLOSSARY: Stat. Within a priority the NPC ranks them warmth, water, rest, food (Npc.Ladder).
public enum Stat { Warmth, Water, Food, Rest }

/// Least urgent first: a later priority outranks an earlier one (GLOSSARY: Priority). None: nothing to do, not even topping up.
public enum Priority { None, Want, Need, Critical }

public readonly record struct StatPriorities(Priority Warmth, Priority Water, Priority Food, Priority Rest)
{
    public Priority this[Stat stat] => stat switch
    {
        Stat.Warmth => Warmth,
        Stat.Water => Water,
        Stat.Food => Food,
        Stat.Rest => Rest,
        _ => throw new ArgumentOutOfRangeException(nameof(stat), stat, "Not a stat"),
    };
}

/// seeingTo is the caller's hysteresis: the NPC holds a stat at Need until it is back over its satisfied line,
/// and keeps that state itself (Npc); the player has none. Decision record 2026-11-21-stat-tiers.
public static class Priorities
{
    public const double HypothermiaCoreF = Body.HypothermiaThresholdF, WarmNeedCoreF = 97.5, WarmAgainCoreF = 97.8;
    public const double WaterCriticalPct = 0.15, WaterNeedPct = 0.5, WaterAgainPct = 0.9;
    public const double RestCriticalPct = 0.1, RestNeedPct = 0.3, RestAgainPct = 0.5, RestedPct = 0.8;
    public const double FatFloorPct = 0.10; // below it the body pays for leanness (Body.Speed, Body.ColdResistance)
    /// The fat the body burns down to before it burns muscle (BodySurvival's MinFatPct).
    public const double StarvingFatPct = 0.03, StarvingToleranceKg = 0.05;
    public const double WantedShortfallKg = 0.3, SatisfiedShortfallKg = 0.1;
    public const double HungryPct = 0.3;
    public const double SpareFireHours = 12;

    /// Tonight's fire is warmth, not a stockpile. usableFireHours is fire the person can have (Fire.UsableHours), not raw wood.
    public static Priority WarmthPriority(double coreAtHomeF, bool seeingTo, double usableFireHours, double tonightHours)
    {
        var core = CorePriority(coreAtHomeF, seeingTo);
        if (core > Priority.None) return core;
        if (usableFireHours < tonightHours) return Priority.Need;
        return usableFireHours < tonightHours + SpareFireHours ? Priority.Want : Priority.None;
    }

    public const double HealthCriticalBloodPct = 0.6, HealthNeedBloodPct = 0.8, HealthCriticalVitalityPct = 0.3, HealthNeedVitalityPct = 0.6;

    /// Bleeding by where it will leave the blood (Body.LowestBloodPct): a deep cut is Critical while the blood is still full.
    public static Priority HealthPriority(BodyState body)
    {
        double bloodPct = Body.LowestBloodPct(body), vitality = Body.TissueVitality(body);
        if (bloodPct <= HealthCriticalBloodPct || vitality <= HealthCriticalVitalityPct) return Priority.Critical;
        if (Effects.BleedMlPerHour(body) > 0 || bloodPct < HealthNeedBloodPct || vitality < HealthNeedVitalityPct) return Priority.Need;
        return Body.IsMended(body) ? Priority.None : Priority.Want;
    }

    public static Priority CorePriority(double coreF, bool seeingTo) =>
        coreF <= HypothermiaCoreF ? Priority.Critical
        : coreF < WarmNeedCoreF || seeingTo && coreF < WarmAgainCoreF ? Priority.Need
        : Priority.None;

    public static Priority WaterPriority(double hydrationPct, bool seeingTo, double toHandL, double hoursLeftToday)
    {
        if (hydrationPct <= WaterCriticalPct) return Priority.Critical;
        if (hydrationPct < WaterNeedPct || seeingTo && hydrationPct < WaterAgainPct) return Priority.Need;
        double dayLossPct = Body.BaseWaterLossMlPerDay * hoursLeftToday / 24 / Body.MaxHydrationMl;
        if (toHandL < 0.1 && hydrationPct - dayLossPct <= WaterCriticalPct) return Priority.Need;
        return toHandL < Body.BaseWaterLossMlPerDay / 1000 ? Priority.Want : Priority.None;
    }

    /// Fat under the floor is Need, not Critical: at the floor a person holds weeks of reserve.
    public static Priority FoodPriority(BodyState body, double targetFatKg, bool seeingTo, bool foodToHand)
    {
        if (Starving(body)) return Priority.Critical;
        double shortfallKg = targetFatKg - body.FatKg;
        if (body.FatKg < FatFloorKg(body) || shortfallKg >= WantedShortfallKg || seeingTo && shortfallKg > SatisfiedShortfallKg) return Priority.Need;
        return body.CaloriesPct < HungryPct && !foodToHand ? Priority.Need : Priority.Want;
    }

    public static Priority RestPriority(double energyPct, bool seeingTo) =>
        energyPct <= RestCriticalPct ? Priority.Critical
        : energyPct < RestNeedPct || seeingTo && energyPct < RestAgainPct ? Priority.Need
        : energyPct < RestedPct ? Priority.Want : Priority.None;

    /// Fat down to what the body will burn with nothing in the stomach: the muscle is what burns.
    public static bool Starving(BodyState body) =>
        body.CaloriesKcal <= 0 && body.FatKg <= StarvingFatPct * body.WeightKg + StarvingToleranceKg;

    public static double FatFloorKg(BodyState body) => FatFloorPct * body.WeightKg;

    /// The stat hierarchy within a priority (GLOSSARY: Priority).
    public static readonly Stat[] Ladder = [Stat.Warmth, Stat.Water, Stat.Rest, Stat.Food];

    /// The top-ranked stat at minimum or above: the worst priority, then the hierarchy; null if none is.
    public static (Stat Stat, Priority Priority)? Top(StatPriorities priorities, Priority minimum)
    {
        (Stat, Priority)? top = null;
        foreach (var stat in Ladder)
            if (priorities[stat] >= minimum && (top is not var (_, p) || priorities[stat] > p)) top = (stat, priorities[stat]);
        return top;
    }

    /// None ranks nowhere; place is the stat's position in the hierarchy (a fraction falls between two stats).
    public static double Rank(Priority priority, double place) => priority == Priority.None ? 0 : (int)priority * 10 - place;

    /// With no hysteresis: the fire's fuel is its own alert, so warmth reads the core alone.
    public static StatPriorities Of(World world, AgentId who)
    {
        var agent = world.Agent(who);
        var body = agent.Body;
        var pack = Items.PackOf(agent);
        double waterL = Work.HasOpenWater(world, agent.Tile) ? double.PositiveInfinity : pack != null ? Items.Have(pack, Resource.Water) : 0;
        bool food = pack != null && pack.Kg.Any(x => x.Value > 0 && Items.Resources[x.Key].Category == ResourceCategory.Food
            && x.Key is not (Resource.RawMeat or Resource.RawFish));
        double hoursLeftToday = (1440 - Weather.MinuteOfDay(world.Minute)) / 60.0;
        return new StatPriorities(
            CorePriority(body.CoreTempF, seeingTo: false),
            WaterPriority(body.HydrationPct, seeingTo: false, waterL, hoursLeftToday),
            FoodPriority(body, Body.BaselineFatPct * body.WeightKg, seeingTo: false, food),
            RestPriority(body.EnergyPct, seeingTo: false));
    }
}
