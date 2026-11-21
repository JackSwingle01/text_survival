namespace Survival;

public enum Precipitation { None, Snow, Rain, Blizzard }

/// What the world and the activity do to a body this minute. Built by Sim from World, Agent and
/// Activity; everything about the body itself (wetness, core temperature) is read off the body.
public sealed record SurvivalContext
{
    public double AirTempF { get; init; } = 70;
    /// The wind at the body (Exposure.WindLevel), where 1 is a 45 mph gale: convection, wind through
    /// the clothing and drying read it, as the wind chill in AirTempF did.
    public double WindLevel { get; init; }
    /// The wind drying you at a fire pit: the wind at the body without the shelter's walls
    /// (Exposure.FireDryingWindLevel; old game parity: old SurvivalContext.CalculateEffectiveWindSpeed
    /// fed SurvivalProcessor's drying). Null when nothing sets it apart: the same as WindLevel.
    public double? FireDryingWindLevel { get; init; }
    /// Radiant heat reaching this agent, °F added to the air. Withheld once the body is warm
    /// enough to sweat: anyone that hot shifts back from the fire long before sweating through a night.
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
    /// Sleep efficiency of the bed slept in (old BeddingFeature.Quality); the bare ground heals at 30% of a proper bed.
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
    public double CoreTempDeltaF, CaloriesDeltaKcal, HydrationDeltaMl, EnergyDeltaMinutes, ClothingHeatBufferDeltaPct;
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
    public const double MaxCaloriesKcal = 2000;

    /// Urine, breath and insensible skin loss: everything but sweat, which is modelled on its own.
    /// (4000 is a real adult's total turnover and already includes sweat; using it double-counted.)
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

    static double FireF(BodyState body, SurvivalContext ctx) => (body.CoreTempF < SweatingThresholdF ? ctx.FireBonusF : 0) + ctx.TorchHeatF;

    internal static double ConvectiveCoefficient(SurvivalContext ctx) => 5.0 * (1.0 + ConvectiveWind(ctx.WindLevel) * 2.0);

    /// How hard the wind strips heat. Linear up to a 45 mph gale (1), where the old game's numbers were
    /// tuned; past it, the square root: real convection grows with about the square root of wind speed,
    /// so a Wind Gap storm (2.5) strips about 1.4x a gale's heat rather than 2x (decision 2026-11-21).
    internal static double ConvectiveWind(double windLevel) => windLevel <= 1 ? windLevel : Math.Sqrt(windLevel);

    /// Clothing, fat and the air's boundary layer in series, the way real resistances add. Doubling
    /// clothing never halves heat loss, so returns diminish with no clamp. 1 clo keeps a resting
    /// person comfortable at 21C, which is what makes gear values checkable against reality.
    public static double TotalThermalResistance(BodyState body, SurvivalContext ctx)
    {
        double wetness = Effects.Severity(body, EffectKind.Wet);
        double clo = ctx.ClothingClo * Math.Clamp(1 - wetness * 0.7, 0.1, 1.0); // soaked layers lose their air
        double sealedLevel = Math.Clamp(ctx.WaterproofingLevel, 0, 1);
        // Wind pushes through the unsealed, on the same curve as it strips the boundary layer (ConvectiveWind): one wind
        // response for convection and clothing alike. Old (this rebuild): linear in the wind, so past a gale it hit its
        // floor and a Wind Gap storm (2.5) took twice a gale's heat, not the square root's 1.4x.
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

    /// Katch-McArdle on lean body mass (everything not fat), in kcal/day. Damaged organs run less
    /// efficiently. Metabolism is also the body's only heat source.
    public static double MetabolismKcalPerDay(BodyState body, double activityFactor)
    {
        double leanKg = Math.Max(0, body.WeightKg - body.FatKg);
        double bmr = 370 + 21.6 * leanKg;
        bmr *= 0.7 + 0.3 * body.Regions.SelectMany(r => r.Organs).Average(o => o.Condition);
        return bmr * activityFactor;
    }

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
            MetabolismKcalPerDay(body, ctx.ActivityFactor) / 24,
            sensibleLossKcalPerHour,
            Sweat(body, ctx).CoolingKcalPerHour,
            body.WeightKg * specificHeatKcalPerKgF);
    }

    public static double TemperatureChangeFPerHour(BodyState body, SurvivalContext ctx) =>
        CalculateHeatBalance(body, ctx).DegreesFPerHour;

    // ---- the survival step ----

    /// What `minutes` of these conditions would do to this body; Apply makes it so. Pure: the rng is
    /// not drawn here (the lethal processes stopped picking an organ at random, body review B4) and
    /// is kept for the callers that pass one.
    public static SurvivalDelta Process(BodyState body, SurvivalContext ctx, int minutes, Rng rng)
    {
        var d = new SurvivalDelta();

        // Base needs.
        d.CaloriesDeltaKcal = -MetabolismKcalPerDay(body, ctx.ActivityFactor) / 24.0 / 60.0 * minutes;
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
        AddBelowThreshold(d, EffectKind.Hungry, calories / MaxCaloriesKcal);
        AddBelowThreshold(d, EffectKind.Thirsty, hydration / MaxHydrationMl);
        AddBelowThreshold(d, EffectKind.Tired, energy / MaxEnergyMinutes);
        return d;
    }

    static void AddBelowThreshold(SurvivalDelta d, EffectKind kind, double pct)
    {
        pct = Math.Clamp(pct, 0, 1);
        if (pct < SurvivalEffectThresholdPct)
            d.Effects.Add((kind, (SurvivalEffectThresholdPct - pct) / SurvivalEffectThresholdPct));
    }

    /// Body temperature change, the clothing heat buffer, the water sweat costs, and temperature effects.
    public static SurvivalDelta ProcessTemperature(BodyState body, SurvivalContext ctx, int minutes)
    {
        var d = new SurvivalDelta();
        ProcessTemperature(body, ctx, minutes, d);
        return d;
    }

    /// Minute by minute: whether the clothing pays for a chill (core at or under its setpoint), whether a fire's heat is
    /// withheld (sweating), the skin's tone and the buffer's fill all change as the core and the buffer do, so one long
    /// step is the same minutes lived one at a time, not the first minute's rates held. Old (this rebuild): decided once
    /// at the start, so a seven-minute step from 98.77F asleep in a cold rain took -1.40F where seven one-minute steps
    /// took -0.20F. Effects are judged by the core as the step began, as before.
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
        // fire's warming of it (Exposure.AirF), so adding both would count that heat twice. Old (this rebuild): any fire
        // heat replaced warm-air drying, so above about 62F a small fire or a torch dried slower than none.
        double fireF = FireF(body, ctx);
        double kcalPerHour = Math.Max(fireF > 0 ? 300 + fireF * 25 : 0, ctx.AirTempF > 32 ? (ctx.AirTempF - 32) * 10 : 0);
        if (kcalPerHour <= 0) return 0; // frozen wet

        // Wind speeds evaporation; it cannot create it. Beside a pit it is the wind past the pit's walls
        // (old game parity), whether or not its heat is withheld for sweating; anywhere else, a torch in
        // hand included, the sheltered wind.
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
    /// Old (this rebuild): it faded at 0.02 an hour to a floor only a missing treatment cleared, so one
    /// butchering left a person a scent source for the whole run (bug, fixed; body review A5).
    static void ProcessBloody(BodyState body, SurvivalContext ctx, int minutes, SurvivalDelta d)
    {
        double bloody = Effects.Severity(body, EffectKind.Bloody), bleeding = Effects.Severity(body, EffectKind.Bleeding);
        if (bloody <= 0 && bleeding <= 0) return;
        // Drying in proportion runs out to nothing only slowly; the last smear dries at a steady rate.
        double dryingPerHour = Math.Max(bloody * BloodyDryingPctPerHour, BloodyLastSmearDryingPerHour) * (FireF(body, ctx) > 0 ? 2 : 1);
        // Always set, however faint: gating at 5% meant it restarted from zero every minute and never built up.
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
        double deficit = -calories;
        d.CaloriesDeltaKcal += deficit;

        double fatKg = Math.Max(0, body.FatKg - MinFatPct * body.WeightKg);
        if (fatKg * KcalPerKgFat >= deficit) { d.FatBurnedKg = deficit / KcalPerKgFat; return; }
        d.FatBurnedKg = fatKg;
        deficit -= fatKg * KcalPerKgFat;

        double muscleKg = Math.Max(0, body.MuscleKg - MinMusclePct * body.WeightKg);
        d.MuscleBurnedKg = Math.Min(muscleKg, deficit / KcalPerKgMuscle);
    }

    /// Condition lost an hour by the organ each process fails (they are internal harm, so these are the
    /// rates; Body.DamageTissue). Thirst fails the brain in about a day and a half once the body is
    /// dry, so at rest a person with full water lives about three days without more. Starving fails
    /// the heart in about a day once fat and muscle are spent, weeks into a fast. The cold fails the
    /// heart from 95 °F, harmlessly at first and then steeply: ColdHeartFailurePerHour at 82 °F, where
    /// a cooling body dies within hours (HypothermiaDamagePerHour). Targets: body review B4, decided
    /// 2026-11-21. Old (this rebuild, old game): 1.0 and 0.5 an hour to an organ drawn at random,
    /// divided by its toughness, so every such death was brain death, thirst killed half an hour after
    /// the body ran dry, and mild cold killed at 94 °F (bug, fixed).
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
    /// Old (this rebuild): the cold was charged off the core the minute would end at, so on the minute
    /// the core crossed 95 °F the step charged what this said was nothing (bug, fixed; body review A8).
    public static (double ColdPerHour, double ThirstPerHour, double HungerPerHour) LethalRates(BodyState body) => (
        HypothermiaDamagePerHour(body.CoreTempF),
        body.HydrationMl > 0 ? 0 : DehydratedDamagePerHour,
        body.CaloriesKcal > 0 || ReservesKcal(body) >= 10 ? 0 : StarvingDamagePerHour);

    /// Below 95 °F the cold harms the heart, by the fourth power of how far the core has fallen toward
    /// 82 °F: negligible in mild hypothermia (a core held at 90 °F takes days), hours at 85 °F, and past
    /// 82 °F a heart that fails within the hour. One continuous curve, rising as the core falls.
    /// Old (this rebuild, old game): two lines that jumped at 87.5 °F and topped out at 0.75 an hour,
    /// against a comment promising 8 (bug, fixed; body review A13).
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
    /// runs while the body is fed, watered and warm (LethalRates).
    /// Old (this rebuild, old game): sleep healed on its own channel past every gate, organs included,
    /// at up to 4 condition an hour, so a naked sleeper at -40 °F lived through a night at a core below
    /// zero °F (bug, fixed; body review A1). Old: nutrition counted twice for tissue and once for
    /// blood (bug, fixed; A9).
    static void ProcessRegeneration(BodyState body, SurvivalContext ctx, double calories, double hydration, int minutes, SurvivalDelta d)
    {
        bool fed = calories > MaxCaloriesKcal * RegenMinCaloriesPct;
        bool watered = hydration > MaxHydrationMl * RegenMinHydrationPct;
        bool warm = body.CoreTempF >= HypothermiaThresholdF;
        bool rested = ctx.IsSleeping || body.EnergyMinutes >= MaxEnergyMinutes * RegenMinEnergyPct;
        bool healed = body.Regions.All(r => r.Condition >= 1.0 && r.Organs.All(o => o.Condition >= 1.0));
        bool bloodFull = body.BloodPct >= 1.0;
        if (!fed || !watered || !warm || !rested || (healed && bloodFull)) return;

        double nutritionPct = Math.Min(1.0, calories / MaxCaloriesKcal);
        double amount = BaseHealingPerHour / 60.0 * minutes * nutritionPct * GetCapacitiesWithoutEffects(body).Digestion;
        if (!bloodFull) d.BloodHealedPct = amount * 0.5;
        if (!healed) d.HealAmount = amount * (ctx.IsSleeping ? SleepHealingFactor(body, ctx) : 1);
    }

    /// How many times faster sleep mends tissue than waking rest. Exhausted sleep is deeper; wet sleep
    /// is miserable; a proper bed beats the bare ground. At full food and a whole gut this is the old
    /// sleep healing (0.1 x quality a minute, old Body.Rest); how fast bodies should heal is body
    /// review B3, not decided.
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
        // Old (this rebuild): levels were set and then faded a minute, so Shivering was absent until
        // 96.83 °F and Sweating until 99.13 °F (bug, fixed; body review A6).
        if (!woundsLived) Effects.Decay(body, 1);
        foreach (var (kind, severity) in d.Effects)
        {
            if (kind is EffectKind.Wet or EffectKind.Bloody) Effects.Set(body, kind, severity);
            else Effects.Add(body, kind, severity);
        }

        d.CoreTempDeltaF += Effects.CoreTempFPerHour(body) / 60;
        double bleedMl = woundsLived ? 0 : Effects.BleedMlPerHour(body) / 60;
        if (bleedMl > 0) d.Damage.Add(Bleed(body, bleedMl));

        Apply(body, d, rng);
        // Old (this rebuild): only an animal forgot who hurt it, so a person bitten once and frozen days
        // later was killed by the wolf pack (bug, fixed; body review A7).
        if (body.WoundedBy != null && IsMended(body)) body.WoundedBy = null;
    }

    /// A minute's bleeding: the blood lost to wounds someone gave is theirs to answer for.
    static DamageInfo Bleed(BodyState body, double ml) => new(ml, DamageType.Bleed, BodyTarget.Blood, By: body.WoundedBy ?? BloodLoss);

    /// One minute for a body outside the human survival model (an animal member, plan decision 2):
    /// effects fade and wounds bleed, as Update does it, and nothing else.
    public static void TickWounds(BodyState body, Rng rng)
    {
        Effects.Decay(body, 1);
        double bleedMl = Effects.BleedMlPerHour(body) / 60;
        if (bleedMl > 0) Damage(body, Bleed(body, bleedMl), rng);
    }

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
        if (ctx.IsSleeping && energyBefore < MaxEnergyMinutes && body.EnergyMinutes == MaxEnergyMinutes)
        {
            events.Add(new FullyRested(world.Minute, agent.Id, agent.Tile));
            return "fully rested";
        }
        return null;
    }
}
