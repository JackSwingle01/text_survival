namespace Survival;

public enum Precipitation { None, Snow, Rain, Blizzard }

/// What the world and the activity do to a body this minute. Built by Sim from World, Agent and
/// Activity; everything about the body itself (wetness, core temperature) is read off the body.
public sealed record SurvivalContext
{
    public double AirTempF { get; init; } = 70;
    /// The wind at the body (Exposure.WindLevel), where 1 is a 45 mph gale: convection, wind through
    /// the clothing and drying read it.
    public double WindLevel { get; init; }
    /// The wind drying you at a fire pit: the wind at the body without the shelter's walls
    /// (Exposure.FireDryingWindLevel). Null when nothing sets it apart: the same as WindLevel.
    public double? FireDryingWindLevel { get; init; }
    /// Radiant heat reaching this agent, °F added to the air. Faded out from the setpoint to a sweat, as anyone that hot
    /// eases back rather than sweat through a night; cut at one line, the core would bounce across it.
    public double FireBonusF { get; init; }
    /// A carried torch's heat, °F. Not withheld when sweating: you cannot step back from a torch in your hand.
    public double TorchHeatF { get; init; }
    /// Whole-body clothing insulation, area-weighted.
    public double ClothingClo { get; init; }
    /// Thermal mass that stores warmth (the clothing heat buffer).
    public double ClothingWeightKg { get; init; }
    /// Resin-sealed gear: keeps rain and wind out, and sweat vapour in.
    public double WaterproofingLevel { get; init; }
    public double OverheadCoverLevel { get; init; }
    public Precipitation Precipitation { get; init; }
    public double PrecipitationPct { get; init; }
    /// Wetness per minute from slush, mud or standing water, before anything protects you from it.
    public double GroundWettingPctPerMinute { get; init; }
    /// Bedding between you and that ground, 0-1.
    public double GroundProtectionLevel { get; init; }
    /// Sleep efficiency of the bed slept in; the bare ground heals at 30% of a proper bed.
    public double BeddingQualityPct { get; init; } = NoBeddingQualityPct;
    public const double NoBeddingQualityPct = 0.3;
    /// Metabolic multiplier: 1 resting awake, 0.5 asleep, 1.5 walking, 1.8 chopping.
    public double ActivityFactor { get; init; } = 1.0;
    /// Sleep is the one activity that restores energy rather than spending it.
    public bool IsSleeping { get; init; }
}

/// Everything one stretch of survival does to a body, computed before any of it is applied.
public sealed class SurvivalDelta
{
    public double CoreTempDeltaF, CaloriesDeltaKcal, HydrationDeltaMl, EnergyDeltaMinutes, ClothingHeatBufferDeltaPct, CalorieBalanceDeltaKcal;
    public double FatBurnedKg, MuscleBurnedKg, BloodHealedPct, HealAmount;
    public List<(EffectKind Kind, double Severity)> Effects { get; } = [];
    public List<DamageInfo> Damage { get; } = [];
}

/// The heat balance behind a body temperature, kept as its terms so "why am I freezing" is
/// answered by the same arithmetic that froze you.
public readonly record struct HeatBalance(
    double MetabolismKcalPerHour, double SensibleLossKcalPerHour, double EvaporativeLossKcalPerHour, double HeatCapacityKcalPerF)
{
    public double DegreesFPerHour =>
        (MetabolismKcalPerHour - SensibleLossKcalPerHour - EvaporativeLossKcalPerHour) / HeatCapacityKcalPerF;
}

public static partial class Body
{
    public const double MaxEnergyMinutes = 960;
    public const double MaxHydrationMl = 4000;
    /// The baseline man's calorie store; every body's scales from it with mass (CaloriesCapacityKcal).
    public const double MaxCaloriesKcal = 2000;

    /// What the calorie store holds before fat is touched: gut contents and glycogen, about a day of rest.
    /// It scales with mass as metabolism does (Kleiber's M^0.75), from the baseline man's 2,000 kcal, so a
    /// hare's and a mammoth's both last about a day.
    public static double CaloriesCapacityKcal(BodyState body) => MaxCaloriesKcal * Math.Pow(body.WeightKg / BaselineWeightKg, 0.75);

    /// Urine, breath and insensible skin loss: everything but sweat, which is modelled on its own.
    /// (A real adult's total turnover of 4000 already includes sweat.)
    public const double BaseWaterLossMlPerDay = 2500;
    const double SleepWaterLossFactor = 0.7;
    const double SleepRecoveryFactor = 2; // an hour asleep buys two awake

    public const double BaseCoreTempF = 98.6;
    public const double SevereHypothermiaThresholdF = 89.6;
    public const double HypothermiaThresholdF = 95.0;
    public const double ShiveringThresholdF = 97.0;
    public const double HyperthermiaThresholdF = 100.0;
    public const double SweatingThresholdF = 99.0;

    const double MinFatPct = 0.03, MinMusclePct = 0.15;
    public const double KcalPerKgFat = 7700;
    const double KcalPerKgMuscle = 1320;

    const double RegenMinCaloriesPct = 0.10, RegenMinHydrationPct = 0.10, RegenMinEnergyPct = 0.50;
    const double BaseHealingPerHour = 0.1;
    const double SurvivalEffectThresholdPct = 0.30;

    const double ThermalMassFPerKg = 2.0;
    /// The chill the clothing's stored heat will take before the core falls under its setpoint (ProcessTemperature's buffer).
    public static double ClothingHeatStoredF(BodyState body) => body.ClothingHeatBufferPct * body.ClothingWeightKg * ThermalMassFPerKg;
    const double MaxSweatMlPerHour = 1000;
    /// Water held by fully soaked clothing; shared by soaking and drying so they cannot drift apart.
    const double MlPerFullSoak = 2000;
    /// Latent heat: evaporating 1ml removes ~0.58 kcal. Why sweat cools, and why trapped sweat does not.
    const double EvaporativeKcalPerMl = 0.58;
    const double ConstrictedGradientF = 14.0; // skin near 29C: the winter state
    const double DilatedGradientF = 2.8;      // skin near 35C: flushed, shedding heat
    const double M2KPerWattPerClo = 0.155;

    // ---- heat ----

    /// Skin circulation, 0 shut to 1 wide open. Constriction runs out exactly where shivering starts,
    /// dilation exactly where sweating starts, so neither costly response fires while blood alone could cope.
    static double VasomotorTone(double coreF) => coreF < BaseCoreTempF
        ? Math.Clamp(0.5 * (coreF - ShiveringThresholdF) / (BaseCoreTempF - ShiveringThresholdF), 0, 0.5)
        : Math.Clamp(0.5 + 0.5 * (coreF - BaseCoreTempF) / (SweatingThresholdF - BaseCoreTempF), 0.5, 1);

    /// What the air actually touches. Moving blood to or from it sheds or holds heat for free.
    public static double SkinTemperatureF(double coreF) =>
        coreF - (ConstrictedGradientF + VasomotorTone(coreF) * (DilatedGradientF - ConstrictedGradientF));

    static double FireF(BodyState body, SurvivalContext ctx) =>
        ctx.FireBonusF * Math.Clamp((SweatingThresholdF - body.CoreTempF) / (SweatingThresholdF - BaseCoreTempF), 0, 1) + ctx.TorchHeatF;

    internal static double ConvectiveCoefficient(SurvivalContext ctx) => 5.0 * (1.0 + ConvectiveWind(ctx.WindLevel) * 2.0);

    /// How hard the wind strips heat. Linear up to a 45 mph gale (1), where the numbers were tuned;
    /// past it, the square root: real convection grows with about the square root of wind speed,
    /// so a Wind Gap storm (2.5) strips about 1.4x a gale's heat rather than 2x.
    internal static double ConvectiveWind(double windLevel) => windLevel <= 1 ? windLevel : Math.Sqrt(windLevel);

    /// Clothing, fat and the air's boundary layer in series, the way real resistances add. Doubling
    /// clothing never halves heat loss, so returns diminish with no clamp. 1 clo keeps a resting
    /// person comfortable at 21C, which is what makes gear values checkable against reality.
    public static double TotalThermalResistance(BodyState body, SurvivalContext ctx)
    {
        double wetness = Effects.Severity(body, EffectKind.Wet);
        double clo = ctx.ClothingClo * Math.Clamp(1 - wetness * 0.7, 0.1, 1.0); // soaked layers lose their air
        double sealedLevel = Math.Clamp(ctx.WaterproofingLevel, 0, 1);
        // Wind pushes through the unsealed, on the same curve as it strips the boundary layer (ConvectiveWind):
        // one wind response for convection and clothing alike.
        clo *= Math.Clamp(1 - ConvectiveWind(ctx.WindLevel) * (1 - sealedLevel) * 0.4, 0.2, 1.0);
        double naturalClo = Math.Clamp(ColdResistance(body), 0, 1) * 1.5;
        return (clo + naturalClo) * M2KPerWattPerClo + 1.0 / ConvectiveCoefficient(ctx);
    }

    /// Fraction of heat blocked relative to naked in the same air; the clothing buffer is expressed in it.
    static double InsulationPct(BodyState body, SurvivalContext ctx) =>
        Math.Clamp(1 - (1.0 / ConvectiveCoefficient(ctx)) / TotalThermalResistance(body, ctx), 0, 0.99);

    /// Air temperature at which a resting body in this clothing is in balance. The number worth
    /// showing a player: compare it to the temperature outside.
    public static double ComfortTemperatureF(BodyState body, SurvivalContext ctx)
    {
        double neutralSkinF = SkinTemperatureF(BaseCoreTempF);
        double sensibleLossW = MetabolismKcalPerDay(body, 1.0) / 24 / 0.86 * 0.75;
        return neutralSkinF - sensibleLossW / 1.8 * TotalThermalResistance(body, ctx) * 9.0 / 5.0;
    }

    /// Basal metabolism in kcal/day: a person's by Katch-McArdle on lean mass (everything not fat), an
    /// animal's by Kleiber's 70 M^0.75 (Katch-McArdle gives a mammoth 84,600 kcal a day for Kleiber's
    /// 47,700). Damaged organs run less efficiently. Metabolism is also the body's only heat source.
    public static double MetabolismKcalPerDay(BodyState body, double activityFactor)
    {
        double bmr = body.Kind == BodyKind.Human
            ? 370 + 21.6 * Math.Max(0, body.WeightKg - body.FatKg)
            : 70 * Math.Pow(body.WeightKg, 0.75);
        double organs = 0;
        int count = 0;
        foreach (var r in body.Regions)
            foreach (var o in r.Organs) { organs += o.Condition; count++; }
        if (count == 0) throw new InvalidOperationException("A body with no organs has no metabolism");
        bmr *= 0.7 + 0.3 * organs / count;
        return bmr * activityFactor;
    }

    /// The one source for heat made and calories burned, so an effect's extra heat is always paid for.
    static double BurnKcalPerHour(BodyState body, double activityFactor) =>
        MetabolismKcalPerDay(body, activityFactor) / 24 * Effects.MetabolicHeatFactor(body);

    readonly record struct SweatResponse(double Severity, double ProducedMlPerHour, double EvaporatedMlPerHour)
    {
        public double CoolingKcalPerHour => EvaporatedMlPerHour * EvaporativeKcalPerMl;
        /// Lost from the body all the same, and it ends up in the clothing.
        public double SoakedMlPerHour => ProducedMlPerHour - EvaporatedMlPerHour;
    }

    /// Sweat opens only once dilation is maxed, ramps from zero, and evaporates only as far as
    /// sealed or saturated clothing lets it. Both near zero is heatstroke: water spent, no cooling.
    static SweatResponse Sweat(BodyState body, SurvivalContext ctx)
    {
        if (VasomotorTone(body.CoreTempF) < 1.0) return default;
        double severity = Math.Clamp((body.CoreTempF - SweatingThresholdF) / 4.0, 0, 1.0);
        double produced = MaxSweatMlPerHour * severity;
        double evaporatedPct = Math.Clamp(1 - ctx.WaterproofingLevel, 0, 1) * Math.Clamp(1 - Effects.Severity(body, EffectKind.Wet), 0, 1);
        return new SweatResponse(severity, produced, produced * evaporatedPct);
    }

    /// Metabolism in; conduction and convection out across the skin; evaporation out.
    public static HeatBalance CalculateHeatBalance(BodyState body, SurvivalContext ctx)
    {
        const double specificHeatKcalPerKgF = 0.83, surfaceAreaM2 = 1.8;
        double deltaC = (SkinTemperatureF(body.CoreTempF) - (ctx.AirTempF + FireF(body, ctx))) * 5.0 / 9.0;
        double sensibleLossKcalPerHour = surfaceAreaM2 * deltaC / TotalThermalResistance(body, ctx) * 0.86;
        return new HeatBalance(
            BurnKcalPerHour(body, ctx.ActivityFactor),
            sensibleLossKcalPerHour,
            Sweat(body, ctx).CoolingKcalPerHour,
            body.WeightKg * specificHeatKcalPerKgF);
    }

    public static double TemperatureChangeFPerHour(BodyState body, SurvivalContext ctx) =>
        CalculateHeatBalance(body, ctx).DegreesFPerHour;

    // ---- the survival step ----

    /// What `minutes` of these conditions would do to this body; Apply makes it so. Pure: the rng is
    /// not drawn here, and is kept for the callers that pass one.
    public static SurvivalDelta Process(BodyState body, SurvivalContext ctx, int minutes, Rng rng)
    {
        var d = new SurvivalDelta();

        double burnKcalPerMinute = BurnKcalPerHour(body, ctx.ActivityFactor) / 60.0;
        d.CaloriesDeltaKcal = -burnKcalPerMinute * minutes;
        d.CalorieBalanceDeltaKcal = CalorieBalanceAfter(body.CalorieBalanceKcal, burnKcalPerMinute, minutes) - body.CalorieBalanceKcal;
        d.EnergyDeltaMinutes = ctx.IsSleeping ? SleepRecoveryFactor * minutes : -minutes;
        d.HydrationDeltaMl = -BaseWaterLossMlPerDay / (24.0 * 60.0) * (ctx.IsSleeping ? SleepWaterLossFactor : 1.0) * minutes;

        ProcessTemperature(body, ctx, minutes, d);
        ProcessWetness(body, ctx, minutes, d);
        ProcessBloody(body, ctx, minutes, d);

        double calories = body.CaloriesKcal + d.CaloriesDeltaKcal;
        double hydration = body.HydrationMl + d.HydrationDeltaMl;

        ProcessStarvation(body, calories, d);
        ProcessLethal(body, minutes, d);
        ProcessRegeneration(body, ctx, calories, hydration, minutes, d);

        double energy = body.EnergyMinutes + d.EnergyDeltaMinutes;
        double hungry = HungerSeverity(body, calories / CaloriesCapacityKcal(body));
        if (hungry > 0) d.Effects.Add((EffectKind.Hungry, hungry));
        AddBelowThreshold(d, EffectKind.Thirsty, hydration / MaxHydrationMl);
        AddBelowThreshold(d, EffectKind.Tired, energy / MaxEnergyMinutes);
        d.Effects.Add((EffectKind.WellFed, WellFedSeverity(body.CalorieBalanceKcal + d.CalorieBalanceDeltaKcal))); // Set: zero ends it
        return d;
    }

    /// In closed form, so one long step is exactly the minutes lived one at a time.
    static double CalorieBalanceAfter(double balanceKcal, double burnKcalPerMinute, int minutes)
    {
        double keptPct = Math.Pow(CalorieBalanceKeptPerMinutePct, minutes);
        return balanceKcal * keptPct - burnKcalPerMinute * (1 - keptPct) / (1 - CalorieBalanceKeptPerMinutePct);
    }

    static readonly double CalorieBalanceKeptPerMinutePct = Math.Exp(-1.0 / (24 * 60));

    /// Rising from nothing, so there is no line to flicker across.
    static double WellFedSeverity(double balanceKcal) => Math.Clamp(balanceKcal / WellFedFullKcal, 0, 1);

    const double WellFedFullKcal = 500;

    static void AddBelowThreshold(SurvivalDelta d, EffectKind kind, double pct)
    {
        double severity = BelowThreshold(pct);
        if (severity > 0) d.Effects.Add((kind, severity));
    }

    static double BelowThreshold(double pct) =>
        Math.Max(0, (SurvivalEffectThresholdPct - Math.Clamp(pct, 0, 1)) / SurvivalEffectThresholdPct);

    /// Hunger follows the reserves, so a body with fat to burn is not weak from a missed day: an empty
    /// store is a pang, and the strong hunger rises over the last StarvingRampDays the reserves can feed.
    static double HungerSeverity(BodyState body, double storePct)
    {
        double pang = EmptyStoreHungerSeverity * BelowThreshold(storePct);
        double reserves = Math.Clamp(1 - ReservesKcal(body) / (StarvingRampDays * MetabolismKcalPerDay(body, 1.0)), 0, 1);
        return Math.Max(pang, reserves);
    }

    const double EmptyStorePangLoss = 0.05, StarvingRampDays = 20;
    static readonly double EmptyStoreHungerSeverity = EmptyStorePangLoss / -Effects.CapacityLoss(EffectKind.Hungry, Capacity.Moving);

    /// Body temperature change, the clothing heat buffer, the water sweat costs, and temperature effects.
    public static SurvivalDelta ProcessTemperature(BodyState body, SurvivalContext ctx, int minutes)
    {
        var d = new SurvivalDelta();
        ProcessTemperature(body, ctx, minutes, d);
        return d;
    }

    /// Minute by minute: whether the clothing pays for a chill (core at or under its setpoint), how much of a fire's heat is
    /// withheld (warming to a sweat), the skin's tone and the buffer's fill all change as the core and the buffer do, so one long
    /// step is the same minutes lived one at a time, not the first minute's rates held. Effects are judged by the core
    /// as the step began.
    static void ProcessTemperature(BodyState body, SurvivalContext ctx, int minutes, SurvivalDelta d)
    {
        double startCoreF = body.CoreTempF, startBufferPct = body.ClothingHeatBufferPct;
        double sweatSeverity = Sweat(body, ctx).Severity;
        try
        {
            for (int i = 0; i < minutes; i++)
            {
                var minute = new SurvivalDelta();
                ProcessTemperatureMinute(body, ctx, minute);
                d.CoreTempDeltaF += minute.CoreTempDeltaF;
                d.HydrationDeltaMl += minute.HydrationDeltaMl;
                d.ClothingHeatBufferDeltaPct += minute.ClothingHeatBufferDeltaPct;
                // The next minute starts where this one left the body (the delta's own sum, as Apply will make it).
                body.CoreTempF = startCoreF + d.CoreTempDeltaF;
                body.ClothingHeatBufferPct = startBufferPct + d.ClothingHeatBufferDeltaPct;
            }
        }
        finally
        {
            body.CoreTempF = startCoreF;
            body.ClothingHeatBufferPct = startBufferPct;
        }

        double core = startCoreF;
        if (core < ShiveringThresholdF)
            d.Effects.Add((EffectKind.Shivering, Math.Clamp((ShiveringThresholdF - core) / 5.0, 0.01, 1.0)));
        if (core < HypothermiaThresholdF)
            d.Effects.Add((EffectKind.Hypothermia, Math.Clamp((HypothermiaThresholdF - core) / 10.0, 0.01, 1.0)));
        if (core < SevereHypothermiaThresholdF)
            d.Effects.Add((EffectKind.Frostbite, Math.Clamp((SevereHypothermiaThresholdF - core) / 10.0, 0.01, 1.0)));
        if (core > HyperthermiaThresholdF)
            d.Effects.Add((EffectKind.Hyperthermia, Math.Clamp((core - HyperthermiaThresholdF) / 10.0, 0.01, 1.0)));
        if (sweatSeverity > 0.01)
            d.Effects.Add((EffectKind.Sweating, sweatSeverity));
    }

    /// One minute of ProcessTemperature, from the body as it stands.
    static void ProcessTemperatureMinute(BodyState body, SurvivalContext ctx, SurvivalDelta d)
    {
        const int minutes = 1;
        var balance = CalculateHeatBalance(body, ctx);
        double coreDeltaF = balance.DegreesFPerHour / 60 * minutes;
        double capacityF = ctx.ClothingWeightKg * ThermalMassFPerKg;
        double buffer = body.ClothingHeatBufferPct;
        double bufferDelta = 0, fireFill = 0;
        double fireF = FireF(body, ctx);

        if (capacityF > 0)
        {
            // Near a fire, clothing soaks up warmth to carry away from it.
            if (fireF > 0 && buffer < 1.0)
                fireFill = Math.Min(fireF / 150.0 / capacityF * minutes, 1.0 - buffer);

            // The buffer delays chilling; it never holds an overheated body above its setpoint.
            if (coreDeltaF < 0 && body.CoreTempF <= BaseCoreTempF)
            {
                if (buffer > 0)
                {
                    double lossF = -coreDeltaF, storedF = buffer * capacityF;
                    if (storedF >= lossF) { bufferDelta = -lossF / capacityF; coreDeltaF = 0; }
                    else { bufferDelta = -buffer; coreDeltaF = -(lossF - storedF); }
                }
            }
            else if (coreDeltaF > 0 && buffer < 1.0)
            {
                // Heat the insulation blocked goes into the clothing.
                double insulation = InsulationPct(body, ctx);
                if (insulation > 0)
                    bufferDelta = Math.Min(coreDeltaF * insulation / (1 - insulation), (1.0 - buffer) * capacityF) / capacityF;
            }
        }

        var sweat = Sweat(body, ctx);
        d.CoreTempDeltaF += coreDeltaF;
        d.HydrationDeltaMl -= sweat.ProducedMlPerHour / 60.0 * minutes;
        d.ClothingHeatBufferDeltaPct += bufferDelta + fireFill;
    }

    /// Drying is paid for in latent heat: soaked clothing (2000ml) needs ~1160 kcal to dry, half a
    /// day of resting metabolism. A fire supplies it in about an hour; below freezing nothing else will.
    /// Returns wetness per hour.
    static double DryingPctPerHour(BodyState body, SurvivalContext ctx)
    {
        // A fire's radiant heat and warm air each supply it; the faster wins. Not their sum: the air already holds the
        // fire's warming of it (Exposure.AirF), so adding both would count that heat twice.
        double fireF = FireF(body, ctx);
        double kcalPerHour = Math.Max(fireF > 0 ? 300 + fireF * 25 : 0, ctx.AirTempF > 32 ? (ctx.AirTempF - 32) * 10 : 0);
        if (kcalPerHour <= 0) return 0; // frozen wet

        // Wind speeds evaporation; it cannot create it. Beside a pit it is the wind past the pit's walls,
        // however much of its heat the body is taking; anywhere else, a torch in hand included, the
        // sheltered wind.
        double windLevel = ctx.FireBonusF > 0 ? ctx.FireDryingWindLevel ?? ctx.WindLevel : ctx.WindLevel;
        if (ctx.Precipitation == Precipitation.None) kcalPerHour *= 1 + windLevel * 1.5;
        return kcalPerHour / (MlPerFullSoak * EvaporativeKcalPerMl);
    }

    static void ProcessWetness(BodyState body, SurvivalContext ctx, int minutes, SurvivalDelta d)
    {
        double keptOutLevel = 1 - ctx.WaterproofingLevel;
        double exposedPct = 1 - ctx.OverheadCoverLevel;
        double perMinute = 0;

        if (exposedPct > 0)
        {
            double rate = ctx.Precipitation switch
            {
                Precipitation.Rain => 0.01,
                Precipitation.Blizzard => 0.005,
                Precipitation.Snow => 0.003,
                _ => 0,
            };
            perMinute += rate * ctx.PrecipitationPct * exposedPct * keptOutLevel;
        }

        // Sweat that could not evaporate: work hard, soak your layers, stop, and freeze in them.
        perMinute += Sweat(body, ctx).SoakedMlPerHour / 60.0 / MlPerFullSoak;

        // The ground: a roof does not drain the puddle you are standing in.
        perMinute += ctx.GroundWettingPctPerMinute * (1 - ctx.GroundProtectionLevel) * keptOutLevel;

        double wetness = Effects.Severity(body, EffectKind.Wet);
        double next = Math.Clamp(wetness + perMinute * minutes - DryingPctPerHour(body, ctx) / 60.0 * minutes, 0, 1);
        d.Effects.Add((EffectKind.Wet, next)); // Set: zero dries you off
    }

    /// Blood on you, a level like wetness: a wound adds to it, and it dries away on its own over a few
    /// hours, the more of it the faster, and twice as fast by a fire. No washing, and no floor: what
    /// the game cannot treat must not need treating
    /// (documentation/decisions/2026-11-21-no-effect-needs-a-missing-treatment.md).
    static void ProcessBloody(BodyState body, SurvivalContext ctx, int minutes, SurvivalDelta d)
    {
        double bloody = Effects.Severity(body, EffectKind.Bloody), bleeding = Effects.Severity(body, EffectKind.Bleeding);
        if (bloody <= 0 && bleeding <= 0) return;
        // Drying in proportion runs out to nothing only slowly; the last smear dries at a steady rate.
        double dryingPerHour = Math.Max(bloody * BloodyDryingPctPerHour, BloodyLastSmearDryingPerHour) * (FireF(body, ctx) > 0 ? 2 : 1);
        // Always set, however faint: gated at a threshold, it would restart from zero every minute and never build up.
        d.Effects.Add((EffectKind.Bloody, Math.Clamp(bloody + (BloodyPerBleedingHour * bleeding - dryingPerHour) / 60.0 * minutes, 0, 1)));
    }

    /// A full bleed's blood on you an hour; the share of the blood on you that dries an hour, and the
    /// least that does: from butchering's 0.5, gone in about five hours, two and a half by a fire.
    const double BloodyPerBleedingHour = 0.15, BloodyDryingPctPerHour = 1.0, BloodyLastSmearDryingPerHour = 0.01;

    /// Fat first, down to 3% of body weight; then muscle, down to 15%. What is still short once both
    /// are spent harms the heart (ProcessLethal).
    static void ProcessStarvation(BodyState body, double calories, SurvivalDelta d)
    {
        if (calories >= 0) return;
        d.CaloriesDeltaKcal += -calories;
        (d.FatBurnedKg, d.MuscleBurnedKg, _) = ReserveBurn(body, -calories);
    }

    /// What covering a deficit takes from the body's reserves, in the one order they burn: fat down to
    /// 3% of body weight, then muscle down to 15%. UnpaidKcal is what both floors leave short: for a
    /// person, the heart answers for it (ProcessLethal reads the spent reserves).
    internal static (double FatKg, double MuscleKg, double UnpaidKcal) ReserveBurn(BodyState body, double deficitKcal)
    {
        double fatKg = Math.Min(Math.Max(0, body.FatKg - MinFatPct * body.WeightKg), deficitKcal / KcalPerKgFat);
        double left = deficitKcal - fatKg * KcalPerKgFat;
        double muscleKg = Math.Min(Math.Max(0, body.MuscleKg - MinMusclePct * body.WeightKg), left / KcalPerKgMuscle);
        return (fatKg, muscleKg, Math.Max(0, left - muscleKg * KcalPerKgMuscle));
    }

    /// Condition lost an hour by the organ each process fails (they are internal harm, so these are the
    /// rates; Body.DamageTissue). Thirst fails the brain in about a day and a half once the body is
    /// dry, so at rest a person with full water lives about three days without more. Starving fails
    /// the heart in about a day once fat and muscle are spent, weeks into a fast. The cold fails the
    /// heart from 95 °F, harmlessly at first and then steeply: ColdHeartFailurePerHour at 82 °F, where
    /// a cooling body dies within hours (HypothermiaDamagePerHour).
    const double DehydratedDamagePerHour = 0.03, StarvingDamagePerHour = 1 / 24.0;
    const double ColdHeartFailurePerHour = 0.8, ColdHeartFailureF = 82.0;

    static readonly Cause TheCold = new(null, "the cold"), Thirst = new(null, "thirst"), Starvation = new(null, "starvation"),
        BloodLoss = new(null, "blood loss");

    /// What fat and muscle the body can still burn, in kcal (ProcessStarvation's floors).
    static double ReservesKcal(BodyState body) =>
        Math.Max(0, body.FatKg - MinFatPct * body.WeightKg) * KcalPerKgFat + Math.Max(0, body.MuscleKg - MinMusclePct * body.WeightKg) * KcalPerKgMuscle;

    /// The organ damage an hour that hunger, thirst and the cold are each doing to this body now, read
    /// off the body as the minute begins: exactly what the survival step charges (ProcessLethal).
    /// Hunger does none while there is fat or muscle to burn (within a few minutes' burning of the floors).
    public static (double ColdPerHour, double ThirstPerHour, double HungerPerHour) LethalRates(BodyState body) => (
        HypothermiaDamagePerHour(body.CoreTempF),
        body.HydrationMl > 0 ? 0 : DehydratedDamagePerHour,
        body.CaloriesKcal > 0 || ReservesKcal(body) >= 10 ? 0 : StarvingDamagePerHour);

    /// Below 95 °F the cold harms the heart, by the fourth power of how far the core has fallen toward
    /// 82 °F: negligible in mild hypothermia (a core held at 90 °F takes days), hours at 85 °F, and past
    /// 82 °F a heart that fails within the hour. One continuous curve, rising as the core falls.
    static double HypothermiaDamagePerHour(double coreF)
    {
        if (coreF >= HypothermiaThresholdF) return 0;
        double fallen = (HypothermiaThresholdF - coreF) / (HypothermiaThresholdF - ColdHeartFailureF);
        return ColdHeartFailurePerHour * Math.Pow(fallen, 4);
    }

    /// Each process harms its one organ at its fixed rate, named for what the death would be.
    static void ProcessLethal(BodyState body, int minutes, SurvivalDelta d)
    {
        var (cold, thirst, hunger) = LethalRates(body);
        if (cold > 0) d.Damage.Add(new DamageInfo(cold / 60.0 * minutes, DamageType.Internal, BodyTarget.Heart, By: TheCold));
        if (thirst > 0) d.Damage.Add(new DamageInfo(thirst / 60.0 * minutes, DamageType.Internal, BodyTarget.Brain, By: Thirst));
        if (hunger > 0) d.Damage.Add(new DamageInfo(hunger / 60.0 * minutes, DamageType.Internal, BodyTarget.Heart, By: Starvation));
    }

    /// Healing needs food, water, warmth and rest (sleep is rest), and is paced by how well the gut
    /// works. So nothing heals while hunger, thirst or the cold is harming the organs: none of them
    /// runs while the body is fed, watered and warm (LethalRates). Sleep heals through the same gates,
    /// or a naked sleeper at -40 °F would live through the night.
    static void ProcessRegeneration(BodyState body, SurvivalContext ctx, double calories, double hydration, int minutes, SurvivalDelta d)
    {
        bool fed = calories > CaloriesCapacityKcal(body) * RegenMinCaloriesPct;
        bool watered = hydration > MaxHydrationMl * RegenMinHydrationPct;
        bool warm = body.CoreTempF >= HypothermiaThresholdF;
        bool rested = ctx.IsSleeping || body.EnergyMinutes >= MaxEnergyMinutes * RegenMinEnergyPct;
        bool healed = body.Regions.All(r => r.Condition >= 1.0 && r.Organs.All(o => o.Condition >= 1.0));
        bool bloodFull = body.BloodPct >= 1.0;
        if (!fed || !watered || !warm || !rested || (healed && bloodFull)) return;

        double nutritionPct = Math.Min(1.0, calories / CaloriesCapacityKcal(body));
        double amount = BaseHealingPerHour / 60.0 * minutes * nutritionPct * GetCapacitiesWithoutEffects(body).Digestion * Effects.HealingFactor(body);
        if (!bloodFull) d.BloodHealedPct = amount * 0.5;
        if (!healed) d.HealAmount = amount * (ctx.IsSleeping ? SleepHealingFactor(body, ctx) : 1);
    }

    /// How many times faster sleep mends tissue than waking rest. Exhausted sleep is deeper; wet sleep
    /// is miserable; a proper bed beats the bare ground. At full food and a whole gut this is 0.1 x
    /// quality a minute; how fast bodies should heal is not decided.
    static double SleepHealingFactor(BodyState body, SurvivalContext ctx)
    {
        double quality = (body.EnergyMinutes <= 0 ? 1.0 : 0.7) * ctx.BeddingQualityPct;
        double wetness = Effects.Severity(body, EffectKind.Wet);
        if (wetness > 0.2) quality *= 1 - wetness * 0.5;
        return SleepHealingPerHour / BaseHealingPerHour * quality;
    }

    const double SleepHealingPerHour = 6.0;

    public static void Apply(BodyState body, SurvivalDelta d, Rng rng)
    {
        body.CaloriesKcal = Math.Max(0, body.CaloriesKcal + d.CaloriesDeltaKcal);
        body.HydrationMl = Math.Max(0, body.HydrationMl + d.HydrationDeltaMl);
        body.EnergyMinutes = Math.Clamp(body.EnergyMinutes + d.EnergyDeltaMinutes, 0, MaxEnergyMinutes);
        body.CoreTempF += d.CoreTempDeltaF;
        body.ClothingHeatBufferPct = Math.Clamp(body.ClothingHeatBufferPct + d.ClothingHeatBufferDeltaPct, 0, 1);
        body.CalorieBalanceKcal += d.CalorieBalanceDeltaKcal;
        body.FatKg = Math.Max(0, body.FatKg - d.FatBurnedKg);
        body.MuscleKg = Math.Max(0, body.MuscleKg - d.MuscleBurnedKg);
        body.BloodPct = Math.Min(1.0, body.BloodPct + d.BloodHealedPct);
        foreach (var hit in d.Damage) Damage(body, hit, rng);
        if (d.HealAmount > 0) Heal(body, d.HealAmount);
    }

    /// One minute of living: survival and healing, effects fading and starting, bleeding.
    /// woundsLived: a headless fight already lived this minute's bleeding and fading (BodyState.WoundsLivedThroughMinute).
    public static void Update(BodyState body, SurvivalContext ctx, Rng rng, bool woundsLived = false)
    {
        var d = Process(body, ctx, 1, rng);

        // Fade first, then this minute's levels: a level set this minute reads as the step computed it.
        if (!woundsLived)
        {
            Effects.Decay(body, 1);
            Recover(body, 60);
        }
        foreach (var (kind, severity) in d.Effects)
        {
            if (kind is EffectKind.Wet or EffectKind.Bloody or EffectKind.WellFed) Effects.Set(body, kind, severity);
            else Effects.Add(body, kind, severity);
        }

        d.CoreTempDeltaF += Effects.CoreTempFPerHour(body) / 60;
        double bleedMl = woundsLived ? 0 : Effects.BleedMlPerHour(body) / 60;
        if (bleedMl > 0) d.Damage.Add(Bleed(body, bleedMl));

        Apply(body, d, rng);
        // A mended body, person or animal, forgets who hurt it: a death days later is not the old wound's.
        if (body.WoundedBy != null && IsMended(body)) body.WoundedBy = null;
        Exhaustion(body, ctx, rng);
    }

    /// One roll a minute lived, on the body's own stream, so a drop is the same however the minutes are
    /// stepped.
    static void Exhaustion(BodyState body, SurvivalContext ctx, Rng rng)
    {
        int asleepLeft = AsleepMinutesLeft(body);
        if (asleepLeft > 0) SetAsleepMinutes(body, asleepLeft - 1);
        else if (!ctx.IsSleeping && body.EnergyMinutes <= 0 && rng.NextDouble() < CollapsePctPerMinute)
            SetAsleepMinutes(body, MinCollapseMinutes + rng.NextInt(MaxCollapseMinutes - MinCollapseMinutes + 1));
    }

    const double CollapsePctPerMinute = 0.01;
    /// The unit of a saved Asleep severity: changing MaxCollapseMinutes raises Save.FormatVersion.
    const int MinCollapseMinutes = 15, MaxCollapseMinutes = 90;

    /// Dropped from exhaustion, not the chosen Sleep activity: it can only sleep it out (Sim).
    public static bool IsAsleep(BodyState body) => AsleepMinutesLeft(body) > 0;

    public static int AsleepMinutesLeft(BodyState body) => (int)Math.Round(Effects.Severity(body, EffectKind.Asleep) * MaxCollapseMinutes);

    static void SetAsleepMinutes(BodyState body, int minutes) => Effects.Set(body, EffectKind.Asleep, (double)minutes / MaxCollapseMinutes);

    internal static void Wake(BodyState body) => Effects.Set(body, EffectKind.Asleep, 0);

    /// A minute's bleeding: the blood lost to wounds someone gave is theirs to answer for.
    static DamageInfo Bleed(BodyState body, double ml) => new(ml, DamageType.Bleed, BodyTarget.Blood, By: body.WoundedBy ?? BloodLoss);

    /// One minute for a body outside the human survival model (an animal member):
    /// effects fade, wounds bleed and the sprint reserve refills, as Update does it, and nothing else.
    public static void TickWounds(BodyState body, Rng rng)
    {
        Effects.Decay(body, 1);
        Recover(body, 60);
        double bleedMl = Effects.BleedMlPerHour(body) / 60;
        if (bleedMl > 0) Damage(body, Bleed(body, bleedMl), rng);
    }

    public const string FellAsleep = "fell asleep";

    /// Advances one agent one minute and reports what happened. Returns the reason to stop, if any.
    public static string? Tick(World world, Agent agent, SurvivalContext ctx, List<WorldEvent> events)
    {
        var body = agent.Body;
        if (!IsAlive(body)) throw new InvalidOperationException($"{agent.Id} is dead and cannot act");
        var kindsBefore = body.Effects.Select(e => e.Kind).ToList();
        double energyBefore = body.EnergyMinutes;

        Update(body, ctx, world.Rng("body", agent.Id), woundsLived: world.Minute <= body.WoundsLivedThroughMinute);

        var kindsAfter = body.Effects.Select(e => e.Kind).ToList();
        foreach (var k in kindsAfter.Except(kindsBefore)) events.Add(new EffectStarted(world.Minute, agent.Id, agent.Tile, k));
        foreach (var k in kindsBefore.Except(kindsAfter)) events.Add(new EffectEnded(world.Minute, agent.Id, agent.Tile, k));

        if (!IsAlive(body))
        {
            MarkDead(body, world.Minute, CauseOfDeath(body));
            events.Add(new Died(world.Minute, agent.Id, agent.Tile));
            return "died";
        }
        if (!ctx.IsSleeping && energyBefore > 0 && body.EnergyMinutes == 0)
        {
            events.Add(new Exhausted(world.Minute, agent.Id, agent.Tile));
            return "exhausted";
        }
        if (IsAsleep(body) && !kindsBefore.Contains(EffectKind.Asleep)) return FellAsleep;

        if (ctx.IsSleeping && energyBefore < MaxEnergyMinutes && body.EnergyMinutes == MaxEnergyMinutes)
        {
            events.Add(new FullyRested(world.Minute, agent.Id, agent.Tile));
            return "fully rested";
        }
        return null;
    }
}
