namespace Survival;

/// One of the four things a person must keep up (GLOSSARY: Stat). Ordered as the hierarchy that ranks
/// stats within a tier: warmth, then water, then food, then rest.
public enum Stat { Warmth, Water, Food, Rest }

/// How urgent a stat is right now (GLOSSARY: Tier), least urgent first: a later tier outranks an earlier one.
/// None is a stat with nothing to do about it, not even topping up.
public enum Tier { None, Want, Need, Critical }

/// A person's four stats by tier, as the player's alerts read them (Tiers.Of).
public readonly record struct StatTiers(Tier Warmth, Tier Water, Tier Food, Tier Rest)
{
    public Tier this[Stat stat] => stat switch
    {
        Stat.Warmth => Warmth, Stat.Water => Water, Stat.Food => Food, Stat.Rest => Rest,
        _ => throw new ArgumentOutOfRangeException(nameof(stat), stat, "Not a stat"),
    };
}

/// The stat tiers, one small function per stat, for NPCs and the player alike (GLOSSARY: Tier; decision
/// record 2026-11-21-stat-tiers). seeingTo is the caller's hysteresis: the NPC holds the stat it is seeing
/// to at Need until it is back over its satisfied line, and keeps that state itself (Npc); the player has none.
public static class Tiers
{
    /// Warmth's lines (CoreTier): Critical at the hypothermia line, where cold damage starts; Need under 97.5F;
    /// warm again over WarmAgainCoreF.
    public const double HypothermiaCoreF = Body.HypothermiaThresholdF, WarmNeedCoreF = 97.5, WarmAgainCoreF = 97.8;
    /// Water's lines (WaterTier), as fractions of a full body.
    public const double WaterCriticalPct = 0.15, WaterNeedPct = 0.5, WaterAgainPct = 0.9;
    /// Rest's lines (RestTier).
    public const double RestCriticalPct = 0.1, RestNeedPct = 0.3, RestAgainPct = 0.5, RestedPct = 0.8;
    /// Below this the body starts paying for leanness (Body.Speed, Body.ColdResistance).
    public const double FatFloorPct = 0.10;
    /// The fat the body burns down to before it burns muscle (BodySurvival's MinFatPct), and how close to it counts.
    public const double StarvingFatPct = 0.03, StarvingMarginKg = 0.05;
    /// Food's lines (FoodTier): fat short of its target by WantedShortfallKg is Need, held to SatisfiedShortfallKg.
    public const double WantedShortfallKg = 0.3, SatisfiedShortfallKg = 0.1;
    /// The stomach store below which it is hungry.
    public const double HungryPct = 0.3;
    /// Wood beyond tonight's that still counts as warmth's Want: a spare half-day.
    public const double SpareFireHours = 12;

    /// Warmth: the core's tier (CoreTier), or a warm core short of wood: Need while the camp has less than
    /// tonight takes (to dawn and a morning fire), Want while it has less than that and a spare half-day. Tonight's
    /// fuel is warmth, not a stockpile: a night without it kills.
    public static Tier WarmthTier(double coreAtHomeF, bool seeingTo, double fuelHours, double tonightHours)
    {
        var core = CoreTier(coreAtHomeF, seeingTo);
        if (core > Tier.None) return core;
        if (fuelHours < tonightHours) return Tier.Need;
        return fuelHours < tonightHours + SpareFireHours ? Tier.Want : Tier.None;
    }

    /// The core's part of warmth: Critical at the hypothermia line, where cold damage starts; Need under the need
    /// line, and while seeing to it, until back over WarmAgainCoreF.
    public static Tier CoreTier(double coreF, bool seeingTo) =>
        coreF <= HypothermiaCoreF ? Tier.Critical
        : coreF < WarmNeedCoreF || seeingTo && coreF < WarmAgainCoreF ? Tier.Need
        : Tier.None;

    /// Water: Critical at WaterCriticalPct; Need under WaterNeedPct (held to WaterAgainPct while seeing to it), or
    /// with no water to hand and the rest of the day's loss taking it to the critical line; Want with less than a
    /// day's water to hand.
    public static Tier WaterTier(double hydrationPct, bool seeingTo, double toHandL, double hoursLeftToday)
    {
        if (hydrationPct <= WaterCriticalPct) return Tier.Critical;
        if (hydrationPct < WaterNeedPct || seeingTo && hydrationPct < WaterAgainPct) return Tier.Need;
        double dayLossPct = Body.BaseWaterLossMlPerDay * hoursLeftToday / 24 / Body.MaxHydrationMl;
        if (toHandL < 0.1 && hydrationPct - dayLossPct <= WaterCriticalPct) return Tier.Need;
        return toHandL < Body.BaseWaterLossMlPerDay / 1000 ? Tier.Want : Tier.None;
    }

    /// Food: Critical only starving (Starving: the fat is down to what the body will burn and the muscle is going);
    /// Need with the fat short of the target it is used to, or under the floor (held to SatisfiedShortfallKg while
    /// seeing to it), or with the stomach low and no food to hand; otherwise Want, a stockpile. Fat under the floor
    /// is Need, not Critical: at the floor a person holds weeks of reserve.
    public static Tier FoodTier(BodyState body, double targetFatKg, bool seeingTo, bool foodToHand)
    {
        if (Starving(body)) return Tier.Critical;
        double shortfallKg = targetFatKg - body.FatKg;
        if (body.FatKg < FatFloorKg(body) || shortfallKg >= WantedShortfallKg || seeingTo && shortfallKg > SatisfiedShortfallKg) return Tier.Need;
        return body.CaloriesPct < HungryPct && !foodToHand ? Tier.Need : Tier.Want;
    }

    /// Rest: Critical at RestCriticalPct (collapse); Need under RestNeedPct (held to RestAgainPct while seeing to it);
    /// Want short of RestedPct.
    public static Tier RestTier(double energyPct, bool seeingTo) =>
        energyPct <= RestCriticalPct ? Tier.Critical
        : energyPct < RestNeedPct || seeingTo && energyPct < RestAgainPct ? Tier.Need
        : energyPct < RestedPct ? Tier.Want : Tier.None;

    /// Starving: the fat down to what the body will burn (StarvingFatPct) with nothing in the stomach, so the
    /// muscle is what burns.
    public static bool Starving(BodyState body) =>
        body.CaloriesKcal <= 0 && body.FatKg <= StarvingFatPct * body.WeightKg + StarvingMarginKg;

    public static double FatFloorKg(BodyState body) => FatFloorPct * body.WeightKg;

    /// Where something stands: its tier first, then its place in the hierarchy (Stat's order; a fraction falls
    /// between two stats). None ranks nowhere.
    public static double Rank(Tier tier, double place) => tier == Tier.None ? 0 : (int)tier * 10 - place;

    /// This person's stats by tier as the player's alerts read them, with no hysteresis: warmth by the core now
    /// (the fire's fuel is its own alert), water by what is carried or flows here, food against the body's
    /// baseline fat with what is fit to eat in the pack, rest by energy.
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
