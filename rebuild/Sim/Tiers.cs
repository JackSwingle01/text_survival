namespace Survival;

/// Ordered as the hierarchy that ranks stats within a tier (GLOSSARY: Stat).
public enum Stat { Warmth, Water, Food, Rest }

/// Least urgent first: a later tier outranks an earlier one (GLOSSARY: Tier). None: nothing to do, not even topping up.
public enum Tier { None, Want, Need, Critical }

public readonly record struct StatTiers(Tier Warmth, Tier Water, Tier Food, Tier Rest)
{
    public Tier this[Stat stat] => stat switch
    {
        Stat.Warmth => Warmth, Stat.Water => Water, Stat.Food => Food, Stat.Rest => Rest,
        _ => throw new ArgumentOutOfRangeException(nameof(stat), stat, "Not a stat"),
    };
}

/// seeingTo is the caller's hysteresis: the NPC holds a stat at Need until it is back over its satisfied line,
/// and keeps that state itself (Npc); the player has none. Decision record 2026-11-21-stat-tiers.
public static class Tiers
{
    public const double HypothermiaCoreF = Body.HypothermiaThresholdF, WarmNeedCoreF = 97.5, WarmAgainCoreF = 97.8;
    public const double WaterCriticalPct = 0.15, WaterNeedPct = 0.5, WaterAgainPct = 0.9;
    public const double RestCriticalPct = 0.1, RestNeedPct = 0.3, RestAgainPct = 0.5, RestedPct = 0.8;
    public const double FatFloorPct = 0.10; // below it the body pays for leanness (Body.Speed, Body.ColdResistance)
    /// The fat the body burns down to before it burns muscle (BodySurvival's MinFatPct).
    public const double StarvingFatPct = 0.03, StarvingMarginKg = 0.05;
    public const double WantedShortfallKg = 0.3, SatisfiedShortfallKg = 0.1;
    public const double HungryPct = 0.3;
    public const double SpareFireHours = 12;

    /// Tonight's fuel is warmth, not a stockpile: a night without it kills.
    public static Tier WarmthTier(double coreAtHomeF, bool seeingTo, double fuelHours, double tonightHours)
    {
        var core = CoreTier(coreAtHomeF, seeingTo);
        if (core > Tier.None) return core;
        if (fuelHours < tonightHours) return Tier.Need;
        return fuelHours < tonightHours + SpareFireHours ? Tier.Want : Tier.None;
    }

    public static Tier CoreTier(double coreF, bool seeingTo) =>
        coreF <= HypothermiaCoreF ? Tier.Critical
        : coreF < WarmNeedCoreF || seeingTo && coreF < WarmAgainCoreF ? Tier.Need
        : Tier.None;

    public static Tier WaterTier(double hydrationPct, bool seeingTo, double toHandL, double hoursLeftToday)
    {
        if (hydrationPct <= WaterCriticalPct) return Tier.Critical;
        if (hydrationPct < WaterNeedPct || seeingTo && hydrationPct < WaterAgainPct) return Tier.Need;
        double dayLossPct = Body.BaseWaterLossMlPerDay * hoursLeftToday / 24 / Body.MaxHydrationMl;
        if (toHandL < 0.1 && hydrationPct - dayLossPct <= WaterCriticalPct) return Tier.Need;
        return toHandL < Body.BaseWaterLossMlPerDay / 1000 ? Tier.Want : Tier.None;
    }

    /// Fat under the floor is Need, not Critical: at the floor a person holds weeks of reserve.
    public static Tier FoodTier(BodyState body, double targetFatKg, bool seeingTo, bool foodToHand)
    {
        if (Starving(body)) return Tier.Critical;
        double shortfallKg = targetFatKg - body.FatKg;
        if (body.FatKg < FatFloorKg(body) || shortfallKg >= WantedShortfallKg || seeingTo && shortfallKg > SatisfiedShortfallKg) return Tier.Need;
        return body.CaloriesPct < HungryPct && !foodToHand ? Tier.Need : Tier.Want;
    }

    public static Tier RestTier(double energyPct, bool seeingTo) =>
        energyPct <= RestCriticalPct ? Tier.Critical
        : energyPct < RestNeedPct || seeingTo && energyPct < RestAgainPct ? Tier.Need
        : energyPct < RestedPct ? Tier.Want : Tier.None;

    /// Fat down to what the body will burn with nothing in the stomach: the muscle is what burns.
    public static bool Starving(BodyState body) =>
        body.CaloriesKcal <= 0 && body.FatKg <= StarvingFatPct * body.WeightKg + StarvingMarginKg;

    public static double FatFloorKg(BodyState body) => FatFloorPct * body.WeightKg;

    /// None ranks nowhere; place is the stat's position in the hierarchy (a fraction falls between two stats).
    public static double Rank(Tier tier, double place) => tier == Tier.None ? 0 : (int)tier * 10 - place;

    /// With no hysteresis: the fire's fuel is its own alert, so warmth reads the core alone.
    public static StatTiers Of(World world, AgentId who)
    {
        var agent = world.Agent(who);
        var body = agent.Body;
        var pack = Items.PackOf(agent);
        double waterL = Work.HasOpenWater(world, agent.Tile) ? double.PositiveInfinity : pack != null ? Items.Have(pack, Resource.Water) : 0;
        bool food = pack != null && pack.Kg.Any(x => x.Value > 0 && Items.Resources[x.Key].Category == ResourceCategory.Food
            && x.Key is not (Resource.RawMeat or Resource.RawFish));
        double hoursLeftToday = (1440 - Weather.MinuteOfDay(world.Minute)) / 60.0;
        return new StatTiers(
            CoreTier(body.CoreTempF, seeingTo: false),
            WaterTier(body.HydrationPct, seeingTo: false, waterL, hoursLeftToday),
            FoodTier(body, Body.BaselineFatPct * body.WeightKg, seeingTo: false, food),
            RestTier(body.EnergyPct, seeingTo: false));
    }
}
