using CsCheck;
using Survival;

namespace Survival.Tests.Verification;

/// Deprivation alone never takes a faculty to zero, and never stops a body walking on a working leg
/// (documentation/specs/2026-11-27-deprivation-rethink.md, "Never stopped by deprivation alone"). Bodies are built
/// from wounds, blood, build, load and the three stores, not effects: cold can take a whole leg's moving, and is
/// the effects' own law.
public class DeprivationPropertyTests
{
    static readonly string[] Legs = ["Left Leg", "Right Leg"];

    public sealed record Case(double WeightKg, double FatPct, double MusclePct, double[] Conditions, double BloodPct,
        double EncumbrancePct, double WaterPct, double CaloriesPct, double SleepDebtHours)
    {
        public override string ToString() =>
            $"{WeightKg:0.0} kg, fat {FatPct:0.00}, muscle {MusclePct:0.00}, blood {BloodPct:0.00}, load {EncumbrancePct:0.00}, " +
            $"water {WaterPct:0.00}, calories {CaloriesPct:0.00}, sleep debt {SleepDebtHours:0.0} h, tissue [{string.Join(' ', Conditions.Select(c => c.ToString("0.00")))}]";
    }

    /// A tissue whole, destroyed, or anywhere between.
    static readonly Gen<double> Condition = Gen.Frequency((3, Gen.Const(1.0)), (1, Gen.Const(0.0)), (2, Gen.Double[0, 1]));
    static readonly Gen<double> Store = Gen.Frequency((1, Gen.Const(0.0)), (1, Gen.Const(1.0)), (3, Gen.Double[0, 1]));
    /// Past the full hour (48), where tiredness is at its worst.
    static readonly Gen<double> SleepDebtHours = Gen.Frequency((1, Gen.Const(0.0)), (1, Gen.Const(60.0)), (3, Gen.Double[0, 60]));

    static readonly int TissueCount = Template().Regions.Sum(r => r.Layers.Count() + r.Organs.Count);

    static BodyState Template() => BodyState.BaselineHuman();

    static Gen<Case> Cases(Gen<double> condition, Gen<double> blood, Gen<double> load) => Gen.Select(
        Gen.Select(Gen.Double[45, 110], Gen.Double[0.04, 0.35], Gen.Double[0.2, 0.45]),
        condition.Array[TissueCount],
        Gen.Select(blood, load),
        Gen.Select(Store, Store, SleepDebtHours),
        (build, conditions, bl, stores) => new Case(build.Item1, build.Item2, build.Item3, conditions, bl.Item1, bl.Item2,
            stores.Item1, stores.Item2, stores.Item3));

    static readonly Gen<Case> AnyBody = Cases(Condition, Gen.Frequency((2, Gen.Const(1.0)), (1, Gen.Double[0.3, 1])),
        Gen.Frequency((2, Gen.Const(0.0)), (1, Gen.Double[0, 2])));

    static BodyState Build(Case c, bool deprived = true)
    {
        var b = BodyState.Create(BodyKind.Human, c.WeightKg, c.FatPct, c.MusclePct);
        int i = 0;
        foreach (var r in b.Regions)
        {
            foreach (var layer in r.Layers) layer.Condition = c.Conditions[i++];
            foreach (var organ in r.Organs) organ.Condition = c.Conditions[i++];
        }
        b.BloodPct = c.BloodPct;
        (b.HydrationMl, b.CaloriesKcal, b.SleepDebtMinutes) = deprived
            ? (c.WaterPct * Body.MaxHydrationMlOf(b), c.CaloriesPct * Body.CaloriesCapacityKcal(b), c.SleepDebtHours * 60)
            : (Body.MaxHydrationMlOf(b), Body.CaloriesCapacityKcal(b), 0);
        return b;
    }

    static readonly Faculty[] Reads = [Faculty.Speed, Faculty.Dexterity, Faculty.Perception, Faculty.Vision];

    static double Read(BodyState b, Faculty f, AbilityContext context) => f switch
    {
        Faculty.Speed => Body.Speed(b, context),
        Faculty.Dexterity => Body.Dexterity(b, context),
        Faculty.Perception => Body.Perception(b, context),
        Faculty.Vision => Body.Vision(b),
        _ => throw new ArgumentOutOfRangeException(nameof(f), f, null),
    };

    /// A faculty at zero is at zero with the stores full too, unless the load is what takes it (thirst thins the
    /// blood, which weakens the body under its pack). Strength never reaches zero on a living body.
    [Fact]
    public void NoFaculty_IsAtZero_ForDeprivationAlone()
    {
        AnyBody.Check(c =>
        {
            var deprived = Build(c);
            var fed = Build(c, deprived: false);
            var context = new AbilityContext(EncumbrancePct: c.EncumbrancePct);
            Assert.True(Body.Strength(deprived) > 0 || Body.Strength(fed) <= 0, "strength");
            foreach (var f in Reads)
            {
                if (Read(deprived, f, context) > 0) continue;
                bool zeroFed = Read(fed, f, context) <= 0;
                bool load = Body.CrippledBy(deprived, f, context).Contains(CrippledBy.Load);
                Assert.True(zeroFed || load, $"{f} at zero for deprivation alone: {string.Join(", ", Body.CrippledBy(deprived, f, context))}");
            }
        });
    }

    /// With no wound, blood whole and nothing carried, every faculty is above zero, whatever the stores.
    [Fact]
    public void AnUnwoundedBody_HasEveryFacultyAboveZero_WhateverItsDeprivations()
    {
        Cases(Gen.Const(1.0), Gen.Const(1.0), Gen.Const(0.0)).Check(c =>
        {
            var b = Build(c);
            Assert.True(Body.Strength(b) > 0, "strength");
            foreach (var f in Reads) Assert.True(Read(b, f, default) > 0, $"{f}: {string.Join(", ", Body.CrippledBy(b, f))}");
        });
    }

    /// One leg kept working (every layer of it at least a fifth whole), the other limbs wounded anyhow, and the head and
    /// trunk whole: a wounded brain or heart is unconsciousness from another cause.
    static Gen<(Case Case, int Leg)> WithAWorkingLeg(Gen<double> blood, Gen<double> load) =>
        Gen.Select(Cases(Condition, blood, load), Gen.Int[0, 1], Gen.Double[0.2, 1].Array[3], (c, leg, kept) =>
        {
            var conditions = (double[])c.Conditions.Clone();
            int i = 0;
            foreach (var r in Template().Regions)
            {
                if (r.Name == Legs[leg]) for (int l = 0; l < r.Layers.Count(); l++) conditions[i + l] = kept[l % kept.Length];
                else if (!r.Name.Contains("Leg") && !r.Name.Contains("Arm")) for (int t = 0; t < r.Layers.Count() + r.Organs.Count; t++) conditions[i + t] = 1;
                i += r.Layers.Count() + r.Organs.Count;
            }
            return (c with { Conditions = conditions }, leg);
        });

    /// Blood whole and nothing carried: any working leg walks, however hungry, thirsty and tired.
    [Fact]
    public void ABodyWithAWorkingLeg_IsNotRefusedWalking_WhateverItsDeprivations()
    {
        WithAWorkingLeg(Gen.Const(1.0), Gen.Const(0.0)).Check(t =>
        {
            var b = Build(t.Case);
            Assert.Null(Crippled.Refusal(b, Faculty.Speed, default));
        });
    }

    /// Any blood, any load and any limb wounds: a refusal to walk never says "no working leg" while a leg works.
    [Fact]
    public void WhileALegWorks_ARefusalNeverNamesNoWorkingLimb()
    {
        WithAWorkingLeg(Gen.Double[0, 1], Gen.Double[0, 2]).Check(t =>
        {
            var b = Build(t.Case);
            var context = new AbilityContext(EncumbrancePct: t.Case.EncumbrancePct);
            Assert.DoesNotContain(CrippledBy.NoWorkingLimb, Body.CrippledBy(b, Faculty.Speed, context));
            if (Crippled.Refusal(b, Faculty.Speed, context) is { } refused) Assert.DoesNotContain("no working leg", refused.Reason);
        });
    }
}
