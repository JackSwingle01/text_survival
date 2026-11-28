using CsCheck;
using Survival;

namespace Survival.Tests.Verification;

/// Law 5: calories. Over any run of eating, activity and time, for one body:
///
///   Σ absorbed = Δ CaloriesKcal + 7700 Δ FatKg + 1320 Δ MuscleKg + Σ burned
///
/// (BodySurvival.cs: KcalPerKgFat 7700, KcalPerKgMuscle 1320.) absorbed is what Body.Eat put in the
/// store or laid down as fat: between half the food's kcal (a ruined gut) and all of it, all of it
/// with a whole gut. burned is metabolism (MetabolismKcalPerDay at the minute's activity, with Well fed's
/// thermic share) paid in
/// full while the body holds any fat or muscle: Forbes' split leaves no floor (declared sink: nothing to burn).
public class ConservationCaloriesTests
{
    public enum OpKind { Eat, Live, HurtGut }

    public sealed record Op(OpKind Kind, double Kcal, SurvivalContext Ctx, int Minutes, double Hurt);

    static readonly Gen<Op> Ops = Gen.Select(
        Gen.Frequency((2, Gen.Const(OpKind.Eat)), (5, Gen.Const(OpKind.Live)), (1, Gen.Const(OpKind.HurtGut))),
        Gen.Frequency((1, Gen.Double[0, 300]), (2, Gen.Double[0, 4000])),
        Gens.Contexts, Gen.Frequency((3, Gen.Int[1, 60]), (1, Gen.Int[60, 720])), Gen.Double[1, 40],
        (k, kcal, ctx, m, hurt) => new Op(k, kcal, ctx, m, hurt));

    public sealed record Scene(double FatKg, double MuscleKg, double CaloriesKcal, double HydrationMl, Op[] Ops);

    static readonly Gen<Scene> Scenes = Gen.Select(
        Gen.Frequency((1, Gen.Double[0.01, 3]), (3, Gen.Double[3, 25])),  // nearly none to well fed
        Gen.Frequency((1, Gen.Double[0.01, 12]), (3, Gen.Double[12, 35])), // wasted to strong
        Gen.Double[0, Body.MaxCaloriesKcal], Gen.Double[400, 4000], Ops.Array[1, 30],
        (fat, muscle, kcal, ml, ops) => new Scene(fat, muscle, kcal, ml, ops));

    const double KcalPerKgFat = Body.KcalPerKgFat, KcalPerKgMuscle = 1320; // BodySurvival.cs

    static double Held(BodyState b) => b.CaloriesKcal + b.FatKg * KcalPerKgFat + b.MuscleKg * KcalPerKgMuscle;

    static void Run(Scene s)
    {
        var b = BodyState.BaselineHuman();
        b.FatKg = s.FatKg;
        b.MuscleKg = s.MuscleKg;
        b.CaloriesKcal = s.CaloriesKcal;
        b.HydrationMl = s.HydrationMl;
        var rng = Gens.Rng();
        double absorbed = 0, burned = 0, held0 = Held(b);
        for (int i = 0; i < s.Ops.Length && Body.IsAlive(b); i++)
        {
            var op = s.Ops[i];
            switch (op.Kind)
            {
                case OpKind.Eat:
                    {
                        bool wholeGut = b.Regions.Single(r => r.Name == "Abdomen") is { IsDestroyed: false } abdomen
                            && abdomen.Organs.All(o => o.Condition >= 1 - 1e-12);
                        double before = Held(b);
                        Body.Eat(b, op.Kcal);
                        double got = Held(b) - before;
                        Assert.True(got >= op.Kcal * 0.5 - 1e-6 && got <= op.Kcal + 1e-6, $"op {i}: ate {op.Kcal:R} kcal, absorbed {got:R}");
                        // A whole gut, effects and all (effects only lower a capacity): starvation and blows wear it too.
                        if (wholeGut) Assert.True(Math.Abs(got - op.Kcal) <= 1e-6, $"op {i}: a whole gut absorbed {got:R} of {op.Kcal:R} kcal");
                        Assert.True(b.CaloriesKcal <= Body.CaloriesCapacityKcal(b) + 1e-9, $"op {i}: store {b.CaloriesKcal:R} over its cap");
                        absorbed += got;
                        break;
                    }
                case OpKind.HurtGut:
                    Body.Damage(b, new DamageInfo(op.Hurt, DamageType.Internal, BodyTarget.Stomach), rng);
                    break;
                case OpKind.Live:
                    for (int m = 0; m < op.Minutes && Body.IsAlive(b); m++)
                    {
                        // Well fed's extra heat is burned too (the thermic effect of food).
                        double kcalPerMinute = Body.MetabolismKcalPerDay(b, op.Ctx.ActivityFactor) / 24.0 / 60.0 * Effects.MetabolicHeatFactor(b);
                        double before = Held(b);
                        Body.Update(b, op.Ctx, rng);
                        double paid = before - Held(b);
                        bool reservesLeft = b.FatKg > 1e-9 || b.MuscleKg > 1e-9 || b.CaloriesKcal > 1e-9;
                        string at = $"op {i} minute {m} (fat {b.FatKg:R}, muscle {b.MuscleKg:R}, store {b.CaloriesKcal:R})";
                        Assert.True(paid <= kcalPerMinute + 1e-6, $"{at}: burned {paid:R} kcal, metabolism is {kcalPerMinute:R}");
                        Assert.True(paid >= -1e-6, $"{at}: living a minute made {-paid:R} kcal");
                        if (reservesLeft) Assert.True(Math.Abs(paid - kcalPerMinute) <= 1e-6, $"{at}: with reserves left, burned {paid:R} kcal, metabolism {kcalPerMinute:R}");
                        burned += paid;
                    }
                    break;
            }
            double drift = absorbed - (Held(b) - held0) - burned;
            Assert.True(Math.Abs(drift) <= 1e-6 * (1 + absorbed + burned), $"after op {i}: absorbed {absorbed:R} kcal, held changed {Held(b) - held0:R}, burned {burned:R}: {drift:R} unaccounted");
        }
    }

    [Fact]
    public void Calories_Absorbed_AreHeldOrBurned() => Scenes.Check(Run, iter: 60);

    [Fact]
    [Trait("Category", TestCategories.Slow)]
    public void Calories_Absorbed_AreHeldOrBurned_Many() => Scenes.Check(Run, iter: 2000);
}
