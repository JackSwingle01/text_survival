namespace Survival;

/// GLOSSARY: Stat. Within a priority they rank warmth, water, rest, food, dirty (Priorities.Ladder).
public enum Stat { Warmth, Water, Food, Rest, Dirty }

/// Least urgent first: a later priority outranks an earlier one (GLOSSARY: Priority). None: nothing to do, not even topping up.
public enum Priority { None, Want, Need, Critical }

public readonly record struct StatPriorities(Priority Warmth, Priority Water, Priority Food, Priority Rest, Priority Dirty = Priority.None)
{
    public Priority this[Stat stat] => stat switch
    {
        Stat.Warmth => Warmth,
        Stat.Water => Water,
        Stat.Food => Food,
        Stat.Rest => Rest,
        Stat.Dirty => Dirty,
        _ => throw new ArgumentOutOfRangeException(nameof(stat), stat, "Not a stat"),
    };
}

/// seeingTo is the caller's hysteresis: the NPC holds a stat at Need until it is back over its satisfied line,
/// and keeps that state itself (Npc); the player has none. Decision record 2026-11-21-stat-tiers.
public static class Priorities
{
    public const double HypothermiaCoreF = Body.HypothermiaThresholdF, WarmNeedCoreF = 97.5, WarmAgainCoreF = 97.8;
    public const double WaterCriticalPct = 0.15, WaterNeedPct = 0.5, WaterAgainPct = 0.9;
    public const double FatFloorPct = 0.10; // below it the body pays for leanness (Body.Speed, Body.ColdResistance)
    /// About a week of a person's starving before hunger harms the heart (Body.LethalMuscleOfFreshPct).
    public const double StarvingMuscleOfFreshPct = Body.LethalMuscleOfFreshPct + 0.1;
    public const double WantedShortfallKg = 0.3, SatisfiedShortfallKg = 0.1;
    public const double SpareFireHours = 12;

    /// Tonight's fire is warmth, not a stockpile. usableFireHours is fire the person can have (Fire.Forecast's hours), not raw wood.
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
        if (Starving(body, Species.Human)) return Priority.Critical;
        double shortfallKg = targetFatKg - body.FatKg;
        if (body.FatKg < FatFloorKg(body) || shortfallKg >= WantedShortfallKg || seeingTo && shortfallKg > SatisfiedShortfallKg) return Priority.Need;
        return Deprivations.Has(body, Deprivation.Hungry) && !foodToHand ? Priority.Need : Priority.Want;
    }

    public const double AnimalFedPct = 0.8;

    /// By its own build, not a person's floor: the shortfalls are a person's share of its weight, and a meal is seen to until AnimalFedPct.
    public static Priority AnimalFoodPriority(BodyState body, Species species, bool seeingTo)
    {
        if (Starving(body, species)) return Priority.Critical;
        double buildFatPct = SpeciesTable.Of(species).FatPct;
        double scale = body.WeightKg / Body.BaselineWeightKg, shortfallKg = buildFatPct * body.WeightKg - body.FatKg;
        if (shortfallKg >= WantedShortfallKg * scale || body.CaloriesPct < Deprivations.HungerOnsetPct) return Priority.Need;
        return seeingTo && (shortfallKg > SatisfiedShortfallKg * scale || body.CaloriesPct < AnimalFedPct) ? Priority.Need : Priority.Want;
    }

    public static Priority RestPriority(SpeciesStats species, double debtMinutes, bool seeingTo) =>
        debtMinutes >= SleepDebt.CollapseOnsetMinutes(species) ? Priority.Critical
        : debtMinutes >= SleepDebt.OnsetMinutes(species) || seeingTo && debtMinutes > SleepDebt.WakingDayMinutes(species) ? Priority.Need
        : debtMinutes >= SleepDebt.WakingDayMinutes(species) || seeingTo && debtMinutes > 0 ? Priority.Want : Priority.None;

    public const double WashWantedDirtyPct = 0.5;

    /// A Want above half Dirty, and never more: washing waits on every other stat.
    public static Priority DirtyPriority(double dirtySeverity) => dirtySeverity > WashWantedDirtyPct ? Priority.Want : Priority.None;

    /// Nothing in the stomach, and the muscle wasted close to where hunger begins to kill.
    public static bool Starving(BodyState body, Species species) => Body.StarvedBelow(body, species, StarvingMuscleOfFreshPct);

    public static double FatFloorKg(BodyState body) => FatFloorPct * body.WeightKg;

    public static readonly Stat[] Ladder = [Stat.Warmth, Stat.Water, Stat.Rest, Stat.Food, Stat.Dirty];

    /// The top-ranked stat at minimum or above (Outranks); null if none is.
    public static (Stat Stat, Priority Priority)? Top(StatPriorities priorities, Priority minimum)
    {
        (Stat, Priority)? top = null;
        foreach (var stat in Ladder)
            if (priorities[stat] >= minimum && (top is not var (s, p) || Outranks(stat, priorities[stat], s, p))) top = (stat, priorities[stat]);
        return top;
    }

    /// A worse priority, or the same one higher in the hierarchy.
    public static bool Outranks(Stat stat, Priority priority, Stat than, Priority thanPriority) => priority > thanPriority
        || priority == thanPriority && Array.IndexOf(Ladder, stat) < Array.IndexOf(Ladder, than);

    /// None ranks nowhere; place is the stat's position in the hierarchy (a fraction falls between two stats).
    public static double Rank(Priority priority, double place) => priority == Priority.None ? 0 : (int)priority * 10 - place;

    /// With no hysteresis: the fire's fuel is its own alert, so warmth reads the core alone.
    public static StatPriorities Of(World world, AgentId who)
    {
        var agent = world.Agent(who);
        var body = agent.Body;
        var pack = Items.PackOf(agent);
        double waterL = Work.CanDrinkHere(world, agent.Tile) ? double.PositiveInfinity : pack != null ? Items.Have(pack, Resource.Water) : 0;
        bool food = pack != null && Items.Totals(pack).Any(x => x.Kg > 0 && Items.Resources[x.Resource].Category == ResourceCategory.Food
            && x.Resource is not (Resource.RawMeat or Resource.RawFish));
        double hoursLeftToday = (1440 - Weather.MinuteOfDay(world.Minute)) / 60.0;
        return new StatPriorities(
            CorePriority(body.CoreTempF, seeingTo: false),
            WaterPriority(body.HydrationPct, seeingTo: false, waterL, hoursLeftToday),
            FoodPriority(body, Body.BaselineFatPct * body.WeightKg, seeingTo: false, food),
            RestPriority(SpeciesTable.Of(agent.Species), body.SleepDebtMinutes, seeingTo: false));
    }
}
