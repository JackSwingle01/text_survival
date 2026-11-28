using CsCheck;
using Survival;

namespace Survival.Tests.Verification;

/// Generators for the effects verification bar (documentation/verification.md). Every range is
/// physical: a value the simulation can actually hand the body, with the reason beside it, so a
/// failing case is a case the game can reach.
static class Gens
{
    /// Iterations per property. Enough to find the bugs these laws were written for, small enough
    /// that the whole file stays well under a second of the suite.
    public const long Iter = 400;

    /// Set to "random" for CsCheck's own random search (the nightly job); otherwise every run
    /// draws the same cases, so CI fails only when the code changes.
    public const string SearchVariable = "SIM_PROPERTY_SEARCH";
    /// A whole number that multiplies every property's iterations (default 1).
    public const string IterFactorVariable = "SIM_PROPERTY_ITER_FACTOR";

    const ulong FixedSeed = 0x5EED_CA5E;

    public static bool RandomSearch =>
        string.Equals(Environment.GetEnvironmentVariable(SearchVariable), "random", StringComparison.OrdinalIgnoreCase);

    static long IterFactor =>
        long.TryParse(Environment.GetEnvironmentVariable(IterFactorVariable), out var n) && n > 0 ? n : 1;

    /// Runs a property single-threaded (xunit already runs test classes in parallel). CsCheck's
    /// `seed` pins only the first case and draws the rest from a clock-seeded generator, so the
    /// fixed search walks one PCG stream itself. A failing case is handed back to CsCheck by its
    /// seed: the failure is certain, CsCheck shrinks from it and throws with the seed to replay
    /// (CsCheck_Seed), which takes the CsCheck path here too.
    public static void Check<T>(this Gen<T> gen, Action<T> assert, long iter = Iter)
    {
        iter *= IterFactor;
        if (RandomSearch || CsCheck.Check.Seed is not null)
        {
            gen.Sample(assert, iter: iter, threads: 1);
            return;
        }
        var pcg = new PCG(1, FixedSeed);
        for (long i = 0; i < iter; i++)
        {
            string seed = pcg.ToString(pcg.State);
            try { assert(gen.Generate(pcg, null, out _)); }
            catch (Exception)
            {
                try { gen.Sample(assert, seed: seed, iter: iter, threads: 1); }
                catch (Exception shrunk) { throw new CsCheckException($"Fixed search fails at CsCheck_Seed={seed}; shrunk by CsCheck below", shrunk); }
                throw new InvalidOperationException($"CsCheck_Seed={seed} failed once and passed when replayed: the property is not a function of its input");
            }
        }
    }

    // ---- effect state ----

    public static readonly Gen<EffectKind> Kind = Gen.Enum<EffectKind>();

    /// A stored severity: what an effect can hold, 0 excluded (zero removes it).
    public static readonly Gen<double> Severity = Gen.Double[1e-6, 1.0];

    /// What a caller may pass to Add or Set: mostly in range, sometimes past either end (both clamp),
    /// with the edges 0 and 1 weighted in.
    public static readonly Gen<double> RawSeverity = Gen.Frequency(
        (6, Gen.Double[0, 1]),
        (2, Gen.Double[-1, 2]),
        (1, Gen.OneOf(Gen.Const(0.0), Gen.Const(1.0))));

    /// Minutes for Decay: one minute up to two days; the game calls it with 1.
    public static readonly Gen<int> Minutes = Gen.Int[0, 2880];

    public enum Op { Add, Set, Decay }

    /// One call against the effect registry.
    public sealed record EffectOp(Op Op, EffectKind Kind, double Severity, int Minutes)
    {
        public void Apply(BodyState body)
        {
            switch (Op)
            {
                case Op.Add: Effects.Add(body, Kind, Severity); break;
                case Op.Set: Effects.Set(body, Kind, Severity); break;
                case Op.Decay: Effects.Decay(body, Minutes); break;
            }
        }
    }

    public static readonly Gen<EffectOp> EffectOpGen =
        Gen.Select(Gen.Enum<Op>(), Kind, RawSeverity, Gen.Int[0, 240], (op, k, s, m) => new EffectOp(op, k, s, m));

    /// Any sequence of registry calls: the reachable effect states are whatever these leave behind.
    public static readonly Gen<EffectOp[]> EffectOps = EffectOpGen.Array[0, 40];

    /// A body carrying the effects a sequence of calls leaves (rebuilt fresh each time it is used).
    public static BodyState BodyAfter(EffectOp[] ops)
    {
        var b = BodyState.BaselineHuman();
        foreach (var op in ops) op.Apply(b);
        return b;
    }

    // ---- the body the survival step reads ----

    /// What the survival step reads off a body, kept to states a living person passes through.
    /// Calories, water and energy stay above zero so hunger and thirst never harm the organs, and
    /// every output is a pure function of the input (the cold's harm below 95 °F is one too). An injury, when there is one, is a blunt blow
    /// to the left leg, so there is something for sleep and regeneration to heal.
    public sealed record BodyInput(double CoreTempF, double WetPct, double ClothingHeatBufferPct,
        double CaloriesKcal, double HydrationMl, double EnergyMinutes, double InjuryAmount)
    {
        public BodyState Build()
        {
            var b = BodyState.BaselineHuman();
            b.CoreTempF = CoreTempF;
            b.ClothingHeatBufferPct = ClothingHeatBufferPct;
            b.CaloriesKcal = CaloriesKcal;
            b.HydrationMl = HydrationMl;
            b.EnergyMinutes = EnergyMinutes;
            if (InjuryAmount > 0) Body.Damage(b, new DamageInfo(InjuryAmount, DamageType.Blunt, BodyTarget.LeftLeg), Rng());
            Effects.Set(b, EffectKind.Wet, WetPct);
            return b;
        }
    }

    public static readonly Gen<BodyInput> BodyInputs = Gen.Select(
        Gen.Double[88, 103],       // °F: deep hypothermia (31 C) to a high fever; the thresholds all sit inside
        Gen.Double[0, 1],          // wetness
        Gen.Double[0, 1],          // clothing heat buffer
        Gen.Double[1, 2000],       // kcal: nearly empty, where hunger slows healing, up to full
        Gen.Double[1, 4000],       // ml: above an empty store, where healing stops, up to full
        Gen.Double[0, 960],        // waking minutes: spent to full; healing reads none of it
        Gen.Frequency((1, Gen.Const(0.0)), (1, Gen.Double[1, 20])), // a bruise to a bad blow, never a break that kills
        (core, wet, buf, kcal, ml, energy, injury) => new BodyInput(core, wet, buf, kcal, ml, energy, injury));

    // ---- exposure: what the world hands the body (SurvivalContext) ----

    /// Air -50..95 °F: the coldest ice-age night with wind chill to a midsummer afternoon.
    public static readonly Gen<double> AirTempF = Gen.Double[-50, 95];
    /// 0..1, 1 a 45 mph gale (SurvivalContext.WindLevel).
    public static readonly Gen<double> Level = Gen.Double[0, 1];
    /// Radiant fire heat 0..100 °F: nothing to sitting at a roaring stone pit in a held shelter.
    public static readonly Gen<double> FireBonusF = Gen.Frequency((1, Gen.Const(0.0)), (2, Gen.Double[0, 100]));
    /// A carried torch, 0..15 °F.
    public static readonly Gen<double> TorchHeatF = Gen.Frequency((2, Gen.Const(0.0)), (1, Gen.Double[0, 15]));
    /// Clothing 0..4 clo (naked to full megafauna furs) and 0..10 kg of it.
    public static readonly Gen<double> ClothingClo = Gen.Double[0, 4];
    public static readonly Gen<double> ClothingWeightKg = Gen.Double[0, 10];
    /// Ground wetting 0..0.12 per minute: Ground.ContactWettingRate's largest rate (walking ankle-deep water).
    public static readonly Gen<double> GroundWettingPctPerMinute = Gen.Frequency((1, Gen.Const(0.0)), (2, Gen.Double[0, 0.12]));
    /// Metabolic multiplier 0.5 (asleep) .. 2 (hard labour).
    public static readonly Gen<double> ActivityFactor = Gen.Double[0.5, 2.0];

    public static readonly Gen<SurvivalContext> Contexts = Gen.Select(
        AirTempF, Level, Gen.Frequency((1, Gen.Const((double?)null)), (1, Level.Select(l => (double?)l))), FireBonusF, TorchHeatF,
        ClothingClo, ClothingWeightKg, Level, Level,
        Gen.Enum<Precipitation>(), Level, GroundWettingPctPerMinute, Level,
        Gen.Double[0, 1], ActivityFactor, Gen.Bool,
        (air, wind, fireWind, fire, torch, clo, kg, sealedLevel, cover, precip, pct, ground, bed, quality, activity, asleep) => new SurvivalContext
        {
            AirTempF = air,
            WindLevel = wind,
            // Sim derives it from the open wind less the pit's walls only, so it is never below WindLevel.
            FireDryingWindLevel = fireWind is double w ? Math.Max(w, wind) : null,
            FireBonusF = fire,
            TorchHeatF = torch,
            ClothingClo = clo,
            ClothingWeightKg = kg,
            WaterproofingLevel = sealedLevel,
            OverheadCoverLevel = cover,
            Precipitation = precip,
            // Weather reports no intensity when nothing falls.
            PrecipitationPct = precip == Precipitation.None ? 0 : pct,
            GroundWettingPctPerMinute = ground,
            GroundProtectionLevel = bed,
            BeddingQualityPct = quality,
            ActivityFactor = asleep ? 0.5 : activity,
            IsSleeping = asleep,
        });

    public static Rng Rng() => Survival.Rng.ForSystem(1, "verification");

    /// Wetness after `minutes` of the survival step, as Process reports it (Update sets exactly this).
    public static double NextWet(BodyState body, SurvivalContext ctx, int minutes) =>
        Body.Process(body, ctx, minutes, Rng()).Effects.Single(e => e.Kind == EffectKind.Wet).Severity;
}
