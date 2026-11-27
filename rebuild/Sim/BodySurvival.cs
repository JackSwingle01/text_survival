namespace Survival;

public enum Precipitation { None, Snow, Rain, Blizzard }

public sealed record SurvivalContext
{
    public double AirTempF { get; init; } = 70;
    public double WindLevel { get; init; }
    public double? FireDryingWindLevel { get; init; }
    /// Radiant heat, °F added to the air. Faded out from the setpoint to a sweat (anyone that hot eases back); a hard cut would bounce the core.
    public double FireBonusF { get; init; }
    /// A carried torch's heat, °F. Not withheld when sweating: you cannot step back from a torch in your hand.
    public double TorchHeatF { get; init; }
    public double ClothingClo { get; init; }
    public double ClothingWeightKg { get; init; }
    /// Resin-sealed gear: keeps rain and wind out, and sweat vapour in.
    public double WaterproofingLevel { get; init; }
    public double OverheadCoverLevel { get; init; }
    public Precipitation Precipitation { get; init; }
    public double PrecipitationPct { get; init; }
    public double GroundWettingPctPerMinute { get; init; }
    public double GroundProtectionLevel { get; init; }
    public double BeddingQualityPct { get; init; } = NoBeddingQualityPct;
    public const double NoBeddingQualityPct = 0.3;
    public double ActivityFactor { get; init; } = 1.0;
    public bool IsSleeping { get; init; }
}

public sealed class SurvivalDelta
{
    public double CoreTempDeltaF, CaloriesDeltaKcal, HydrationDeltaMl, EnergyDeltaMinutes, ClothingHeatBufferDeltaPct;
    public double FatBurnedKg, MuscleBurnedKg, BloodHealedPct, HealAmount;
    public List<(EffectKind Kind, double Severity)> Effects { get; } = [];
    public List<DamageInfo> Damage { get; } = [];
}

/// Kept as its terms so "why am I freezing" is answered by the same arithmetic that froze you.
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

    /// Gut and glycogen before fat is touched; scales as Kleiber's M^0.75 so a hare's and a mammoth's both last about a day.
    public static double CaloriesCapacityKcal(BodyState body) => MaxCaloriesKcal * Math.Pow(body.WeightKg / BaselineWeightKg, 0.75);

    /// Everything but sweat, which is modelled on its own (a real adult's 4000 total includes it).
    public const double BaseWaterLossMlPerDay = 2500;
    const double SleepWaterLossFactor = 0.7;
    public const double PersonSleepHoursPerDay = 8;

    public static double MaxHydrationMlOf(BodyState body) =>
        body.Kind == BodyKind.Human ? MaxHydrationMl : MaxHydrationMl * body.StructuralKg / BaselineStructuralKg;

    static double BaselineStructuralKg => BaselineWeightKg * (1 - BaselineFatPct - BaselineMusclePct);

    public static double WaterLossMlPerMinute(BodyState body, bool sleeping) =>
        BaseWaterLossMlPerDay / 1440 * MaxHydrationMlOf(body) / MaxHydrationMl * (sleeping ? SleepWaterLossFactor : 1.0);

    /// The store empties over the waking hours and fills over the sleeping ones: at 8 h an hour asleep buys two awake.
    public static double EnergyDeltaPerMinute(bool sleeping, double sleepHoursPerDay) => sleeping
        ? MaxEnergyMinutes / (sleepHoursPerDay * 60)
        : -MaxEnergyMinutes / ((24 - sleepHoursPerDay) * 60);

    /// An animal's ProcessLethal, without warmth; sleepFactor is how deep the sleep is where it lies.
    public static void LiveNeeds(BodyState body, bool sleeping, double sleepHoursPerDay, double sleepFactor, Rng rng)
    {
        var (_, thirst, hunger) = LethalRates(body);
        body.HydrationMl = Math.Max(0, body.HydrationMl - WaterLossMlPerMinute(body, sleeping));
        body.EnergyMinutes = Math.Clamp(body.EnergyMinutes + EnergyDeltaPerMinute(sleeping, sleepHoursPerDay) * (sleeping ? sleepFactor : 1), 0, MaxEnergyMinutes);
        if (thirst > 0) Damage(body, new DamageInfo(thirst / 60.0, DamageType.Internal, BodyTarget.Brain, By: Thirst), rng);
        if (hunger > 0) Damage(body, new DamageInfo(hunger / 60.0, DamageType.Internal, BodyTarget.Heart, By: Starvation), rng);
    }

    public const double BaseCoreTempF = 98.6;
    public const double SevereHypothermiaThresholdF = 89.6;
    public const double HypothermiaThresholdF = 95.0;
    public const double ShiveringThresholdF = 97.0;
    public const double HyperthermiaThresholdF = 100.0;
    public const double SweatingThresholdF = 99.0;

    const double MinFatPct = 0.03, MinMusclePct = 0.15;
    public const double KcalPerKgFat = 7700;
    const double KcalPerKgMuscle = 1320;

    public const double RegenMinCaloriesPct = 0.10;
    const double RegenMinHydrationPct = 0.10, RegenMinEnergyPct = 0.50;
    const double BaseHealingPerHour = 0.1;

    const double ThermalMassFPerKg = 2.0;
    public static double ClothingHeatStoredF(BodyState body) => body.ClothingHeatBufferPct * body.ClothingWeightKg * ThermalMassFPerKg;
    const double MaxSweatMlPerHour = 1000;
    const double MlPerFullSoak = 2000;
    /// Latent heat: evaporating 1ml removes ~0.58 kcal. Why sweat cools, and why trapped sweat does not.
    const double EvaporativeKcalPerMl = 0.58;
    const double ConstrictedGradientF = 14.0; // skin near 29C: the winter state
    const double DilatedGradientF = 2.8;      // skin near 35C: flushed, shedding heat
    const double M2KPerWattPerClo = 0.155;

    /// Skin circulation, 0 shut to 1 open. Constriction ends where shivering starts, dilation where sweating starts, so neither costly response fires while blood alone could cope.
    static double VasomotorTone(double coreF) => coreF < BaseCoreTempF
        ? Math.Clamp(0.5 * (coreF - ShiveringThresholdF) / (BaseCoreTempF - ShiveringThresholdF), 0, 0.5)
        : Math.Clamp(0.5 + 0.5 * (coreF - BaseCoreTempF) / (SweatingThresholdF - BaseCoreTempF), 0.5, 1);

    public static double SkinTemperatureF(double coreF) =>
        coreF - (ConstrictedGradientF + VasomotorTone(coreF) * (DilatedGradientF - ConstrictedGradientF));

    static double FireF(BodyState body, SurvivalContext ctx) =>
        ctx.FireBonusF * Math.Clamp((SweatingThresholdF - body.CoreTempF) / (SweatingThresholdF - BaseCoreTempF), 0, 1) + ctx.TorchHeatF;

    internal static double ConvectiveCoefficient(SurvivalContext ctx) => 5.0 * (1.0 + ConvectiveWind(ctx.WindLevel) * 2.0);

    /// Linear up to a 45 mph gale (1); past it the square root, as real convection grows: a 2.5 storm strips ~1.4x a gale's heat, not 2x.
    internal static double ConvectiveWind(double windLevel) => windLevel <= 1 ? windLevel : Math.Sqrt(windLevel);

    /// In series, as real resistances add: doubling clothing never halves heat loss. 1 clo is comfortable resting at 21C.
    public static double TotalThermalResistance(BodyState body, SurvivalContext ctx)
    {
        double wetness = Effects.Severity(body, EffectKind.Wet);
        double clo = ctx.ClothingClo * Math.Clamp(1 - wetness * 0.7, 0.1, 1.0); // soaked layers lose their air
        double sealedLevel = Math.Clamp(ctx.WaterproofingLevel, 0, 1);
        // Wind pushes through the unsealed on the same curve (ConvectiveWind) as it strips the boundary layer.
        clo *= Math.Clamp(1 - ConvectiveWind(ctx.WindLevel) * (1 - sealedLevel) * 0.4, 0.2, 1.0);
        double naturalClo = Math.Clamp(ColdResistance(body), 0, 1) * 1.5;
        return (clo + naturalClo) * M2KPerWattPerClo + 1.0 / ConvectiveCoefficient(ctx);
    }

    static double InsulationPct(BodyState body, SurvivalContext ctx) =>
        Math.Clamp(1 - (1.0 / ConvectiveCoefficient(ctx)) / TotalThermalResistance(body, ctx), 0, 0.99);

    public static double ComfortTemperatureF(BodyState body, SurvivalContext ctx)
    {
        double neutralSkinF = SkinTemperatureF(BaseCoreTempF);
        double sensibleLossW = MetabolismKcalPerDay(body, 1.0) / 24 / 0.86 * 0.75;
        return neutralSkinF - sensibleLossW / 1.8 * TotalThermalResistance(body, ctx) * 9.0 / 5.0;
    }

    internal static double WholeOrgansBasalKcalPerDay(BodyState body) => body.Kind == BodyKind.Human
        ? 370 + 21.6 * Math.Max(0, body.WeightKg - body.FatKg)
        : 70 * Math.Pow(body.WeightKg, 0.75);

    /// Kleiber for animals: Katch-McArdle gives a mammoth 84,600 kcal/day to Kleiber's 47,700.
    public static double MetabolismKcalPerDay(BodyState body, double activityFactor)
    {
        double bmr = WholeOrgansBasalKcalPerDay(body);
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

    public static double BurnKcalPerMinute(BodyState body, double activityFactor) => BurnKcalPerHour(body, activityFactor) / 60;

    readonly record struct SweatResponse(double Severity, double ProducedMlPerHour, double EvaporatedMlPerHour)
    {
        public double CoolingKcalPerHour => EvaporatedMlPerHour * EvaporativeKcalPerMl;
        public double SoakedMlPerHour => ProducedMlPerHour - EvaporatedMlPerHour;
    }

    /// Opens only once dilation is maxed; evaporates only as far as clothing lets it. Both near zero is heatstroke: water spent, no cooling.
    static SweatResponse Sweat(BodyState body, SurvivalContext ctx)
    {
        if (VasomotorTone(body.CoreTempF) < 1.0) return default;
        double severity = Math.Clamp((body.CoreTempF - SweatingThresholdF) / 4.0, 0, 1.0);
        double produced = MaxSweatMlPerHour * severity;
        double evaporatedPct = Math.Clamp(1 - ctx.WaterproofingLevel, 0, 1) * Math.Clamp(1 - Effects.Severity(body, EffectKind.Wet), 0, 1);
        return new SweatResponse(severity, produced, produced * evaporatedPct);
    }

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

    /// Pure: Apply makes it so. The rng is not drawn here, kept for callers that pass one.
    public static SurvivalDelta Process(BodyState body, SurvivalContext ctx, int minutes, Rng rng)
    {
        var d = new SurvivalDelta();

        double burnKcalPerMinute = BurnKcalPerHour(body, ctx.ActivityFactor) / 60.0;
        d.CaloriesDeltaKcal = -burnKcalPerMinute * minutes;
        d.EnergyDeltaMinutes = EnergyDeltaPerMinute(ctx.IsSleeping, PersonSleepHoursPerDay) * minutes;
        d.HydrationDeltaMl = -WaterLossMlPerMinute(body, ctx.IsSleeping) * minutes;

        ProcessTemperature(body, ctx, minutes, d);
        ProcessWetness(body, ctx, minutes, d);
        ProcessBloody(body, ctx, minutes, d);
        ProcessDirty(body, ctx, minutes, d);

        double calories = body.CaloriesKcal + d.CaloriesDeltaKcal;
        double hydration = body.HydrationMl + d.HydrationDeltaMl;

        ProcessStarvation(body, calories, d);
        ProcessLethal(body, minutes, d);
        ProcessRegeneration(body, ctx, calories, hydration, minutes, d);

        return d;
    }

    public static SurvivalDelta ProcessTemperature(BodyState body, SurvivalContext ctx, int minutes)
    {
        var d = new SurvivalDelta();
        ProcessTemperature(body, ctx, minutes, d);
        return d;
    }

    /// Minute by minute, as buffer, withheld fire heat and skin tone all follow the core: one long step equals its minutes.
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

    /// Paid for in latent heat: soaked clothing (2000ml) needs ~1160 kcal, half a day of resting metabolism. A fire supplies it in about an hour.
    static double DryingPctPerHour(BodyState body, SurvivalContext ctx)
    {
        // Fire heat or warm air, whichever is faster; not their sum, as the air already holds the fire's warming (Exposure.AirF).
        double fireF = FireF(body, ctx);
        double kcalPerHour = Math.Max(fireF > 0 ? 300 + fireF * 25 : 0, ctx.AirTempF > 32 ? (ctx.AirTempF - 32) * 10 : 0);
        if (kcalPerHour <= 0) return 0;

        // Wind speeds evaporation; it cannot create it. Beside a pit it is the wind past the pit's walls; anywhere else (a torch included) the sheltered wind.
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

    /// No floor: documentation/decisions/2026-11-21-no-effect-needs-a-missing-treatment.md.
    static void ProcessBloody(BodyState body, SurvivalContext ctx, int minutes, SurvivalDelta d)
    {
        double bloody = Effects.Severity(body, EffectKind.Bloody), bleeding = Effects.Severity(body, EffectKind.Bleeding);
        if (bloody <= 0 && bleeding <= 0) return;
        // Drying in proportion runs out to nothing only slowly; the last smear dries at a steady rate.
        double dryingPerHour = Math.Max(bloody * BloodyDryingPctPerHour, BloodyLastSmearDryingPerHour) * (FireF(body, ctx) > 0 ? 2 : 1);
        // Always set, however faint: gated at a threshold, it would restart from zero every minute and never build up.
        d.Effects.Add((EffectKind.Bloody, Math.Clamp(bloody + (BloodyPerBleedingHour * bleeding - dryingPerHour) / 60.0 * minutes, 0, 1)));
    }

    /// Per hour: gained from a full bleed; share dried; least dried. Butchering's 0.5 is gone in ~5 hours, 2.5 by a fire.
    const double BloodyPerBleedingHour = 0.15, BloodyDryingPctPerHour = 1.0, BloodyLastSmearDryingPerHour = 0.01;

    /// Never fades: washing is the only way off.
    static void ProcessDirty(BodyState body, SurvivalContext ctx, int minutes, SurvivalDelta d)
    {
        double perHour = DirtyPerHour + Sweat(body, ctx).ProducedMlPerHour / 1000 * DirtyPerSweatLiter;
        d.Effects.Add((EffectKind.Dirty, Math.Clamp(Effects.Severity(body, EffectKind.Dirty) + perHour / 60.0 * minutes, 0, 1)));
    }

    /// Fully dirty in about ten days of living unwashed; a hard hour's half litre of sweat adds half a day's.
    const double DirtyPerHour = 0.004, DirtyPerSweatLiter = 0.1;

    public static void Wash(BodyState body)
    {
        double washed = Effects.Severity(body, EffectKind.Dirty) + Effects.Severity(body, EffectKind.Bloody);
        Effects.Set(body, EffectKind.Dirty, 0);
        Effects.Set(body, EffectKind.Bloody, 0);
        Effects.Set(body, EffectKind.Wet, Effects.Severity(body, EffectKind.Wet) + WashWetLevel + WashWetPerSeverityWashed * washed);
    }

    const double WashWetLevel = 0.05, WashWetPerSeverityWashed = 0.15;

    static void ProcessStarvation(BodyState body, double calories, SurvivalDelta d)
    {
        if (calories >= 0) return;
        d.CaloriesDeltaKcal += -calories;
        (d.FatBurnedKg, d.MuscleBurnedKg, _) = ReserveBurn(body, -calories);
    }

    /// Fat down to 3% of body weight, then muscle down to 15%. UnpaidKcal is what both floors leave short; the heart answers for it (ProcessLethal).
    internal static (double FatKg, double MuscleKg, double UnpaidKcal) ReserveBurn(BodyState body, double deficitKcal)
    {
        double fatKg = Math.Min(Math.Max(0, body.FatKg - MinFatPct * body.WeightKg), deficitKcal / KcalPerKgFat);
        double left = deficitKcal - fatKg * KcalPerKgFat;
        double muscleKg = Math.Min(Math.Max(0, body.MuscleKg - MinMusclePct * body.WeightKg), left / KcalPerKgMuscle);
        return (fatKg, muscleKg, Math.Max(0, left - muscleKg * KcalPerKgMuscle));
    }

    /// At rest a person lives about three days without water, and a day once fat and muscle are spent.
    const double DehydratedDamagePerHour = 0.03, StarvingDamagePerHour = 1 / 24.0;
    const double ColdHeartFailurePerHour = 0.8, ColdHeartFailureF = 82.0;

    static readonly Cause TheCold = new(null, "the cold"), Thirst = new(null, "thirst"), Starvation = new(null, "starvation"),
        BloodLoss = new(null, "blood loss");

    internal static double ReservesKcal(BodyState body) =>
        Math.Max(0, body.FatKg - MinFatPct * body.WeightKg) * KcalPerKgFat + Math.Max(0, body.MuscleKg - MinMusclePct * body.WeightKg) * KcalPerKgMuscle;

    /// Read as the minute begins, so a forecast is exactly what the survival step charges.
    public static (double ColdPerHour, double ThirstPerHour, double HungerPerHour) LethalRates(BodyState body) => (
        HypothermiaDamagePerHour(body.CoreTempF),
        body.HydrationMl > 0 ? 0 : DehydratedDamagePerHour,
        body.CaloriesKcal > 0 || ReservesKcal(body) >= 10 ? 0 : StarvingDamagePerHour);

    /// Fourth power of how far the core has fallen toward 82 °F: days at 90 °F, hours at 85 °F, under an hour past 82 °F. One continuous curve.
    static double HypothermiaDamagePerHour(double coreF)
    {
        if (coreF >= HypothermiaThresholdF) return 0;
        double fallen = (HypothermiaThresholdF - coreF) / (HypothermiaThresholdF - ColdHeartFailureF);
        return ColdHeartFailurePerHour * Math.Pow(fallen, 4);
    }

    static void ProcessLethal(BodyState body, int minutes, SurvivalDelta d)
    {
        var (cold, thirst, hunger) = LethalRates(body);
        if (cold > 0) d.Damage.Add(new DamageInfo(cold / 60.0 * minutes, DamageType.Internal, BodyTarget.Heart, By: TheCold));
        if (thirst > 0) d.Damage.Add(new DamageInfo(thirst / 60.0 * minutes, DamageType.Internal, BodyTarget.Brain, By: Thirst));
        if (hunger > 0) d.Damage.Add(new DamageInfo(hunger / 60.0 * minutes, DamageType.Internal, BodyTarget.Heart, By: Starvation));
    }

    /// Sleep heals through the same gates, or a naked sleeper at -40 °F would live through the night.
    static void ProcessRegeneration(BodyState body, SurvivalContext ctx, double calories, double hydration, int minutes, SurvivalDelta d)
    {
        bool healed = body.Regions.All(r => r.Condition >= 1.0 && r.Organs.All(o => o.Condition >= 1.0));
        bool bloodFull = body.BloodPct >= 1.0;
        if (healed && bloodFull) return;
        double amount = HealingAmount(body, ctx.IsSleeping, calories, hydration, minutes);
        if (amount <= 0) return;
        if (!bloodFull) d.BloodHealedPct = amount * BloodHealingShare;
        if (!healed) d.HealAmount = amount * (ctx.IsSleeping ? SleepHealingFactor(body, ctx) : 1);
    }

    const double BloodHealingShare = 0.5;

    static double HealingAmount(BodyState body, bool sleeping, double calories, double hydration, int minutes)
    {
        bool fed = calories > CaloriesCapacityKcal(body) * RegenMinCaloriesPct;
        bool watered = hydration > MaxHydrationMl * RegenMinHydrationPct;
        bool warm = body.CoreTempF >= HypothermiaThresholdF;
        bool rested = sleeping || body.EnergyMinutes >= MaxEnergyMinutes * RegenMinEnergyPct;
        if (!fed || !watered || !warm || !rested) return 0;
        double nutritionPct = Math.Min(1.0, calories / CaloriesCapacityKcal(body));
        return BaseHealingPerHour / 60.0 * minutes * nutritionPct * TissueReads.Digestion(body) * Effects.HealingFactor(body);
    }

    /// A forecast, healing awake as now and nothing else changing; bleeding that never stops is followed for a day.
    public static double LowestBloodPct(BodyState body)
    {
        double healPerMinute = HealingAmount(body, sleeping: false, body.CaloriesKcal, body.HydrationMl, 1) * BloodHealingShare;
        double blood = body.BloodPct, lowest = blood, untilHours = Math.Min(Effects.HoursUntilBleedingStops(body), 24);
        for (int m = 0; m < untilHours * 60 && blood > 0; m++)
        {
            blood = Math.Max(0, Math.Min(1.0, blood + healPerMinute) - Effects.BleedMlPerHourIn(body, (m + 1) / 60.0) / 60 / BloodVolumeMl(body));
            lowest = Math.Min(lowest, blood);
        }
        return lowest;
    }

    /// How fast bodies should heal is not decided.
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
            if (kind is EffectKind.Wet or EffectKind.Bloody or EffectKind.Dirty) Effects.Set(body, kind, severity);
            else Effects.Add(body, kind, severity);
        }

        d.CoreTempDeltaF += Effects.CoreTempFPerHour(body) / 60;
        double bleedMl = woundsLived ? 0 : Effects.BleedMlPerHour(body) / 60;
        if (bleedMl > 0) d.Damage.Add(Bleed(body, bleedMl));

        Apply(body, d, rng);
        WellFed(body);
        // A mended body, person or animal, forgets who hurt it: a death days later is not the old wound's.
        if (body.WoundedBy != null && IsMended(body)) body.WoundedBy = null;
        Exhaustion(body, ctx, rng);
    }

    static void WellFed(BodyState body)
    {
        body.MinutesWithoutHunger = Deprivations.Has(body, Deprivation.Hungry) ? 0 : body.MinutesWithoutHunger + 1;
        Effects.Set(body, EffectKind.WellFed, body.MinutesWithoutHunger >= Effects.WellFedAfterHours * 60 ? 1 : 0);
    }

    /// One roll a minute lived, on the body's own stream, so a drop is the same however minutes are stepped.
    static void Exhaustion(BodyState body, SurvivalContext ctx, Rng rng)
    {
        int asleepLeft = AsleepMinutesLeft(body);
        if (asleepLeft > 0) SetAsleepMinutes(body, asleepLeft - 1);
        else if (!ctx.IsSleeping && body.EnergyMinutes <= 0 && rng.NextDouble() < CollapsePctPerMinute)
            SetAsleepMinutes(body, MinCollapseMinutes + rng.NextInt(MaxCollapseMinutes - MinCollapseMinutes + 1));
    }

    const double CollapsePctPerMinute = 0.01;
    /// The unit of a saved Asleep severity: changing MaxCollapseMinutes is a change of meaning (Save.MeaningChange).
    const int MinCollapseMinutes = 15, MaxCollapseMinutes = 90;

    /// Dropped from exhaustion, not the chosen Sleep activity: it can only sleep it out (Sim).
    public static bool IsAsleep(BodyState body) => AsleepMinutesLeft(body) > 0;

    public static int AsleepMinutesLeft(BodyState body) => (int)Math.Round(Effects.Severity(body, EffectKind.Asleep) * MaxCollapseMinutes);

    static void SetAsleepMinutes(BodyState body, int minutes) => Effects.Set(body, EffectKind.Asleep, (double)minutes / MaxCollapseMinutes);

    internal static void Wake(BodyState body) => Effects.Set(body, EffectKind.Asleep, 0);

    /// Blood lost to wounds someone gave is theirs to answer for.
    static DamageInfo Bleed(BodyState body, double ml) => new(ml, DamageType.Bleed, BodyTarget.Blood, By: body.WoundedBy ?? BloodLoss);

    /// For a body outside the human survival model (an animal member): effects fade, wounds bleed, sprint reserve refills, nothing else.
    public static void TickWounds(BodyState body, Rng rng)
    {
        Effects.Decay(body, 1);
        Recover(body, 60);
        double bleedMl = Effects.BleedMlPerHour(body) / 60;
        if (bleedMl > 0) Damage(body, Bleed(body, bleedMl), rng);
    }

    public const string FellAsleep = "fell asleep";

    public static string? Tick(World world, Agent agent, SurvivalContext ctx, List<WorldEvent> events)
    {
        var body = agent.Body;
        if (!IsAlive(body)) throw new InvalidOperationException($"{agent.Id} is dead and cannot act");
        var kindsBefore = body.Effects.Select(e => e.Kind).ToList();
        double energyBefore = body.EnergyMinutes;

        Update(body, ctx, world.Rng("body", agent.Id), woundsLived: world.Minute <= body.WoundsLivedThroughMinute);

        var kindsAfter = body.Effects.Select(e => e.Kind).ToList();
        // Endings first: hunger that ends Well fed reads as the change it is, not as both at once.
        foreach (var k in kindsBefore.Except(kindsAfter)) events.Add(new EffectEnded(world.Minute, agent.Id, agent.Tile, k));
        foreach (var k in kindsAfter.Except(kindsBefore)) events.Add(new EffectStarted(world.Minute, agent.Id, agent.Tile, k));

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
