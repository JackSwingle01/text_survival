using System.Reflection;
using CsCheck;
using Survival;

namespace Survival.Tests.Verification;

/// The survival step's wetness, drying and temperature laws (documentation/verification.md), read
/// through Body.Process, Body.ProcessTemperature and Body.Update only. None of them names a number
/// the exposure rework would change: they say which way things move, what stays in range, and that
/// every input reaches the body.
public class BodySurvivalPropertyTests
{
    static double Wet(BodyState b) => Effects.Severity(b, EffectKind.Wet);

    // ---- bounds ----

    /// Any run of conditions, any body: wetness in [0,1], the clothing buffer in [0,1], every effect
    /// in its range, the core finite.
    [Fact]
    public void Bounds_HoldThroughAnyRunOfConditions()
    {
        Gen.Select(Gens.BodyInputs, Gen.Select(Gens.Contexts, Gen.Int[1, 45]).Array[1, 5]).Check(t =>
        {
            var (input, stretches) = t;
            var b = input.Build();
            var rng = Gens.Rng();
            foreach (var (ctx, minutes) in stretches)
                for (int m = 0; m < minutes && Body.IsAlive(b); m++)
                {
                    Body.Update(b, ctx, rng);
                    Assert.InRange(Wet(b), 0, 1);
                    Assert.InRange(b.ClothingHeatBufferPct, 0, 1);
                    Assert.True(double.IsFinite(b.CoreTempF), $"core {b.CoreTempF}");
                    foreach (var e in b.Effects) Assert.InRange(e.Severity, double.Epsilon, 1);
                }
        }, iter: 100);
    }

    // ---- direction ----

    /// Two values of one input, the lower first.
    static Gen<(double Lo, double Hi)> Ordered(Gen<double> g) =>
        Gen.Select(g, g, (a, b) => (Math.Min(a, b), Math.Max(a, b)));

    static readonly Gen<int> StepMinutes = Gen.Int[1, 60];

    [Fact]
    public void Direction_MorePrecipitation_NeverDriesYou()
    {
        Gen.Select(Gens.BodyInputs, Gens.Contexts, Ordered(Gens.Level), StepMinutes).Check(t =>
        {
            var (input, ctx, (lo, hi), minutes) = t;
            var falling = ctx.Precipitation == Precipitation.None ? ctx with { Precipitation = Precipitation.Snow } : ctx;
            var dry = ctx with { Precipitation = Precipitation.None, PrecipitationPct = 0 };
            double none = Gens.NextWet(input.Build(), dry, minutes);
            double light = Gens.NextWet(input.Build(), falling with { PrecipitationPct = lo }, minutes);
            double heavy = Gens.NextWet(input.Build(), falling with { PrecipitationPct = hi }, minutes);
            Assert.True(light >= none, $"{falling.Precipitation} at {lo:R} left {light:R}, nothing falling {none:R}");
            Assert.True(heavy >= light, $"{falling.Precipitation} {hi:R} left {heavy:R}, {lo:R} left {light:R}");
        });
    }

    [Fact]
    public void Direction_MoreWind_NeverDriesSlower()
    {
        Gen.Select(Gens.BodyInputs, Gens.Contexts, Ordered(Gens.Level), StepMinutes).Check(t =>
        {
            var (input, ctx, (lo, hi), minutes) = t;
            // The open wind and the wind past the pit's walls each, the other held.
            double calm = Gens.NextWet(input.Build(), ctx with { WindLevel = lo }, minutes);
            double windy = Gens.NextWet(input.Build(), ctx with { WindLevel = hi }, minutes);
            Assert.True(windy <= calm, $"wind {hi:R} left {windy:R}, wind {lo:R} left {calm:R}");
            double pitCalm = Gens.NextWet(input.Build(), ctx with { FireDryingWindLevel = lo }, minutes);
            double pitWindy = Gens.NextWet(input.Build(), ctx with { FireDryingWindLevel = hi }, minutes);
            Assert.True(pitWindy <= pitCalm, $"pit wind {hi:R} left {pitWindy:R}, {lo:R} left {pitCalm:R}");
        });
    }

    static void FireNeverDriesSlower(Gen<SurvivalContext> contexts, Gen<double> heat, Func<SurvivalContext, double, SurvivalContext> with, string what)
    {
        Gen.Select(Gens.BodyInputs, contexts, Ordered(heat), StepMinutes).Check(t =>
        {
            var (input, ctx, (lo, hi), minutes) = t;
            double cool = Gens.NextWet(input.Build(), with(ctx, lo), minutes);
            double hot = Gens.NextWet(input.Build(), with(ctx, hi), minutes);
            Assert.True(hot <= cool, $"{what} {hi:R} left {hot:R}, {what} {lo:R} left {cool:R}");
        }, iter: 1000);
    }

    /// Where the bug cannot bite: air at or below 60F, where warm air alone never out-dries the
    /// smallest fire.
    static readonly Gen<SurvivalContext> CoolAir = Gens.Contexts.Select(c => c with { AirTempF = Math.Min(c.AirTempF, 60) });

    [Fact]
    public void Direction_MoreFireHeat_NeverDriesSlower_InCoolAir() =>
        FireNeverDriesSlower(CoolAir, Gens.FireBonusF, (c, f) => c with { FireBonusF = f }, "fire");

    [Fact]
    public void Direction_MoreTorchHeat_NeverDriesSlower_InCoolAir() =>
        FireNeverDriesSlower(CoolAir, Gens.TorchHeatF, (c, f) => c with { TorchHeatF = f }, "torch");

    [Fact]
    public void Direction_MoreFireHeat_NeverDriesSlower() =>
        FireNeverDriesSlower(Gens.Contexts, Gens.FireBonusF, (c, f) => c with { FireBonusF = f }, "fire");

    [Fact]
    public void Direction_MoreTorchHeat_NeverDriesSlower() =>
        FireNeverDriesSlower(Gens.Contexts, Gens.TorchHeatF, (c, f) => c with { TorchHeatF = f }, "torch");

    [Fact]
    public void Direction_WarmerAir_NeverDriesSlower()
    {
        Gen.Select(Gens.BodyInputs, Gens.Contexts, Ordered(Gens.AirTempF), StepMinutes).Check(t =>
        {
            var (input, ctx, (lo, hi), minutes) = t;
            double cold = Gens.NextWet(input.Build(), ctx with { AirTempF = lo }, minutes);
            double warm = Gens.NextWet(input.Build(), ctx with { AirTempF = hi }, minutes);
            Assert.True(warm <= cold, $"air {hi:R} left {warm:R}, air {lo:R} left {cold:R}");
        });
    }

    [Fact]
    public void Direction_MoreCoverOrBedding_NeverWetsYouMore_AndWetterGroundNeverDries()
    {
        Gen.Select(Gens.BodyInputs, Gens.Contexts, Ordered(Gens.Level), Ordered(Gens.GroundWettingPctPerMinute)).Check(t =>
        {
            var (input, ctx, (lo, hi), (dryGround, wetGround)) = t;
            Assert.True(Gens.NextWet(input.Build(), ctx with { OverheadCoverLevel = hi }, 1)
                <= Gens.NextWet(input.Build(), ctx with { OverheadCoverLevel = lo }, 1), "roof");
            Assert.True(Gens.NextWet(input.Build(), ctx with { GroundProtectionLevel = hi }, 1)
                <= Gens.NextWet(input.Build(), ctx with { GroundProtectionLevel = lo }, 1), "bedding");
            Assert.True(Gens.NextWet(input.Build(), ctx with { GroundWettingPctPerMinute = wetGround }, 1)
                >= Gens.NextWet(input.Build(), ctx with { GroundWettingPctPerMinute = dryGround }, 1), "ground");
        });
    }

    /// The core laws hold for single one-minute steps only. A longer step reads its rates once, at the starting core,
    /// and the model switches on hard lines: sweating at Body.SweatingThresholdF (99F) and the clothing heat buffer at
    /// Body.BaseCoreTempF (98.6F); between them the fire's heat fades. Over a long step the warmer case can cross a line the colder one does not
    /// and come out about 0.04F a minute behind. Whether the hard switches stay is for the body review
    /// (documentation/body-review-2026-11-21.md).
    [Fact]
    public void Direction_ColderAir_NeverWarmsTheCoreFaster()
    {
        Gen.Select(Gens.BodyInputs, Gens.Contexts, Ordered(Gens.AirTempF)).Check(t =>
        {
            var (input, ctx, (lo, hi)) = t;
            double cold = Body.ProcessTemperature(input.Build(), ctx with { AirTempF = lo }, 1).CoreTempDeltaF;
            double warm = Body.ProcessTemperature(input.Build(), ctx with { AirTempF = hi }, 1).CoreTempDeltaF;
            Assert.True(cold <= warm, $"air {lo:R} moved the core {cold:R}, air {hi:R} {warm:R}");
        });
    }

    [Fact]
    public void Direction_MoreFireHeat_NeverCoolsTheCoreFaster()
    {
        Gen.Select(Gens.BodyInputs, Gens.Contexts, Ordered(Gens.FireBonusF)).Check(t =>
        {
            var (input, ctx, (lo, hi)) = t;
            double cool = Body.ProcessTemperature(input.Build(), ctx with { FireBonusF = lo }, 1).CoreTempDeltaF;
            double hot = Body.ProcessTemperature(input.Build(), ctx with { FireBonusF = hi }, 1).CoreTempDeltaF;
            Assert.True(hot >= cool, $"fire {hi:R} moved the core {hot:R}, fire {lo:R} {cool:R}");
        });
    }

    // ---- time-splitting ----

    /// Holding the core still, so only wetness moves: N one-minute steps, each set back on the body
    /// as Update does, against one N-minute Process.
    static (double Whole, double Stepped) SplitWet(Gens.BodyInput input, SurvivalContext ctx, int minutes)
    {
        double whole = Gens.NextWet(input.Build(), ctx, minutes);
        var b = input.Build();
        for (int i = 0; i < minutes; i++) Effects.Set(b, EffectKind.Wet, Gens.NextWet(b, ctx, 1));
        return (whole, Wet(b));
    }

    /// Out of a sweat, every wetting and drying rate is fixed for fixed exposure and the clamp
    /// absorbs, so a long step is exactly a run of short ones (to rounding).
    [Fact]
    public void TimeSplitting_Wetness_IsExactOutOfASweat()
    {
        var notSweating = Gens.BodyInputs.Where(i => i.CoreTempF < Body.SweatingThresholdF);
        Gen.Select(notSweating, Gens.Contexts, Gen.Int[1, 240]).Check(t =>
        {
            var (input, ctx, minutes) = t;
            var (whole, stepped) = SplitWet(input, ctx, minutes);
            Assert.True(Math.Abs(whole - stepped) <= 1e-9, $"{minutes} min: one step {whole:R}, minute steps {stepped:R}");
        });
    }

    /// In a sweat, the sweat soaking in depends on how wet the clothes already are (dw/dt = A + Bw),
    /// so an N-minute Process is one forward-Euler step and drifts from N one-minute steps by
    /// (A + Bw0)/B * ((1+B)^N - 1 - NB), at most 1/2 N^2 B |rate| (1+B)^N. B is at most a litre an
    /// hour of sweat over a 2-litre soak (1/120 a minute) and |rate| at most 0.15 a minute (the
    /// wettest ground and rain, or the hottest fire in a gale). Only Update (one minute) calls Process
    /// in the game; this bounds the drift for anyone who reaches for a longer step.
    [Fact]
    public void TimeSplitting_Wetness_DriftsInASweat_WithinTheEulerBound()
    {
        const double maxB = 1.0 / 120, maxRate = 0.15;
        var sweating = Gens.BodyInputs.Where(i => i.CoreTempF >= Body.SweatingThresholdF);
        Gen.Select(sweating, Gens.Contexts, Gen.Int[1, 30]).Check(t =>
        {
            var (input, ctx, minutes) = t;
            var (whole, stepped) = SplitWet(input, ctx, minutes);
            double bound = 0.5 * minutes * minutes * maxB * maxRate * Math.Pow(1 + maxB, minutes);
            Assert.True(Math.Abs(whole - stepped) <= bound, $"{minutes} min: one step {whole:R}, minute steps {stepped:R}, bound {bound:R}");
        });
    }

    /// The core under one N-minute ProcessTemperature against N one-minute ones with the core and the
    /// clothing buffer applied between. The step evaluates its switches once, at the start: whether
    /// the buffer may pay for a chill (core at or under 98.6), how much fire heat is withheld (core
    /// 98.6-99), how open the skin's circulation is (97-99). Across any of them a long step is not a run of
    /// short ones (found by this test: 7 minutes from 98.8F in a gale, one step -1.40F, minute
    /// steps -0.20F). Only Update, a minute at a time, runs it in the game, so the law holds where
    /// none of the switches can be crossed: no clothing buffer, the core below 96 (circulation shut,
    /// skin a fixed gradient under the core), and then it is a smooth decay toward the air, which a
    /// long forward-Euler step overshoots by about half of N times the decay rate per minute (at most
    /// 0.004 naked in a gale): within 0.002 N of the change itself.
    [Fact]
    public void TimeSplitting_CoreTemperature_HoldsAwayFromTheSwitches()
    {
        var shut = Gens.BodyInputs.Select(i => i with { CoreTempF = Math.Min(i.CoreTempF, 95.9) });
        Gen.Select(shut, Gens.Contexts.Select(c => c with { ClothingWeightKg = 0 }), Gen.Int[1, 15]).Check(t =>
        {
            var (input, ctx, minutes) = t;
            double whole = Body.ProcessTemperature(input.Build(), ctx, minutes).CoreTempDeltaF;
            var b = input.Build();
            double start = b.CoreTempF;
            for (int i = 0; i < minutes; i++) b.CoreTempF += Body.ProcessTemperature(b, ctx, 1).CoreTempDeltaF;
            if (b.CoreTempF >= Body.ShiveringThresholdF - 1) return; // warmed into the switches: not this law's case
            double stepped = b.CoreTempF - start;
            Assert.True(Math.Abs(whole - stepped) <= 0.002 * minutes * Math.Abs(stepped) + 1e-9,
                $"{minutes} min: one step {whole:R}F, minute steps {b.CoreTempF - start:R}F");
        });
    }

    // ---- fixed point ----

    /// Under steady exposure wetness only ever moves one way, and once it reaches either end it
    /// stays there: it settles, it never cycles.
    [Fact]
    public void FixedPoint_WetnessIsMonotone_AndItsEndsAbsorb()
    {
        Gen.Select(Gens.BodyInputs, Gens.Contexts).Check(t =>
        {
            var (input, ctx) = t;
            var b = input.Build();
            double prev = Wet(b);
            int direction = 0;
            for (int i = 0; i < 600; i++)
            {
                double next = Gens.NextWet(b, ctx, 1);
                // Arrived at an end by moving toward it: it stays.
                if (direction != 0 && prev is 0 or 1) Assert.Equal(prev, next);
                int step = Math.Sign(Math.Round(next - prev, 12));
                if (step != 0)
                {
                    Assert.True(direction == 0 || step == direction, $"minute {i}: turned from {direction} at {prev:R}");
                    direction = step;
                }
                Effects.Set(b, EffectKind.Wet, next);
                prev = next;
            }
        }, iter: 150);
    }

    /// The whole body under steady exposure: by two days the core has either settled (the last hour
    /// moves it by less than a tenth of a degree) or is on its way out of the living range. Two days,
    /// because a full clothing buffer can pay a sleeper's chill for eight hours before the core
    /// feels it, and a short run catches the turn as if it were a swing.
    static readonly Gen<(Gens.BodyInput, SurvivalContext)> MildCores =
        Gen.Select(Gens.BodyInputs.Select(i => i with { CoreTempF = Math.Clamp(i.CoreTempF, 95, 100) }), Gens.Contexts);

    static void SettlesOrLeaves((Gens.BodyInput, SurvivalContext) t)
    {
        var (input, ctx) = t;
        var b = input.Build();
        var rng = Gens.Rng();
        var hourly = new List<double>();
        for (int m = 0; m < 48 * 60 && Body.IsAlive(b); m++)
        {
            Body.Update(b, ctx, rng);
            if (m % 60 == 59) hourly.Add(b.CoreTempF);
        }
        if (hourly.Count < 48) return; // died: left the living range
        double lastHour = Math.Abs(hourly[^1] - hourly[^2]);
        bool settled = lastHour < 0.1;
        bool leaving = hourly[^1] < Body.HypothermiaThresholdF || hourly[^1] > Body.HyperthermiaThresholdF;
        Assert.True(settled || leaving, $"core still moving {lastHour:R}F/h at {hourly[^1]:R}F: {string.Join(", ", hourly.Select(h => h.ToString("F2")))}");
    }

    [Fact]
    public void FixedPoint_TheCoreSettlesOrLeaves_ItNeverOscillates() => MildCores.Check(SettlesOrLeaves, iter: 40);

    /// Seeds that caught the core bouncing across 99F beside a roaring fire: each minute it reached the line the
    /// whole fire went at once and it fell a quarter of a degree, then climbed back, every few minutes
    /// (FixedPoint_TheCoreSettlesOrLeaves_ItNeverOscillates).
    [Theory]
    [InlineData("fAZ5uqu8jhq1")] // naked in the rain, a 94F fire
    [InlineData("4k_8AfronAH3")] // unclothed under a roof edge in a light blizzard, a 93F fire
    public void FixedPoint_BesideARoaringFire_TheCoreSettles(string seed) =>
        MildCores.Sample(SettlesOrLeaves, seed: seed, iter: 1, threads: 1);

    // ---- every input matters ----

    /// Each exposure input, and how to give it another value. A new SurvivalContext property fails
    /// EveryInput_IsListed until it is added here, or to Irrelevant with its reason.
    static readonly (string Name, Func<SurvivalContext, double, SurvivalContext> Change)[] Inputs =
    [
        ("AirTempF", (c, u) => c with { AirTempF = -50 + 145 * u }),
        ("WindLevel", (c, u) => c with { WindLevel = u }),
        ("FireDryingWindLevel", (c, u) => c with { FireDryingWindLevel = u }),
        ("FireBonusF", (c, u) => c with { FireBonusF = 100 * u }),
        ("TorchHeatF", (c, u) => c with { TorchHeatF = 15 * u }),
        ("ClothingClo", (c, u) => c with { ClothingClo = 4 * u }),
        ("ClothingWeightKg", (c, u) => c with { ClothingWeightKg = 10 * u }),
        ("WaterproofingLevel", (c, u) => c with { WaterproofingLevel = u }),
        ("OverheadCoverLevel", (c, u) => c with { OverheadCoverLevel = u }),
        ("Precipitation", (c, u) => c with { Precipitation = (Precipitation)(int)(u * 4) }),
        ("PrecipitationPct", (c, u) => c with { PrecipitationPct = u }),
        ("GroundWettingPctPerMinute", (c, u) => c with { GroundWettingPctPerMinute = 0.12 * u }),
        ("GroundProtectionLevel", (c, u) => c with { GroundProtectionLevel = u }),
        ("BeddingQualityPct", (c, u) => c with { BeddingQualityPct = u }),
        ("ActivityFactor", (c, u) => c with { ActivityFactor = 0.5 + 1.5 * u }),
        ("IsSleeping", (c, u) => c with { IsSleeping = !c.IsSleeping }),
    ];

    /// Inputs the body may ignore, each with why.
    static readonly Dictionary<string, string> Irrelevant = new()
    {
        ["EqualityContract"] = "the record's own type, not an input",
    };

    [Fact]
    public void EveryInput_IsListed()
    {
        var properties = typeof(SurvivalContext).GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic)
            .Select(p => p.Name).Where(n => !Irrelevant.ContainsKey(n));
        Assert.Equal(properties.Order(), Inputs.Select(i => i.Name).Order());
    }

    /// Everything one minute does to a body, as text two runs can be compared by: the delta Process
    /// reports, and what Update leaves on every tissue (sleep healing is applied only there, and a
    /// region's own condition, the worst of its layers, hides healing that went to a better layer).
    static string Outcome(Gens.BodyInput input, SurvivalContext ctx)
    {
        var d = Body.Process(input.Build(), ctx, 1, Gens.Rng());
        var b = input.Build();
        Body.Update(b, ctx, Gens.Rng());
        double[] numbers = [d.CoreTempDeltaF, d.CaloriesDeltaKcal, d.HydrationDeltaMl, d.EnergyDeltaMinutes,
            d.ClothingHeatBufferDeltaPct, d.FatBurnedKg, d.MuscleBurnedKg, d.BloodHealedPct, d.HealAmount,
            .. b.Regions.SelectMany(r => r.Layers.Select(l => l.Condition).Concat(r.Organs.Select(o => o.Condition)))];
        return string.Join(",", numbers.Select(x => x.ToString("R")))
            + "|" + string.Join(";", d.Effects.Select(e => $"{e.Kind}:{e.Severity:R}"));
    }

    /// The test that catches an input wired in and never read (heavy snow that did not wet, bedding
    /// warmth never read, shelter wind cover never read): for every input there are states where
    /// changing it alone changes what a minute does to the body.
    [Theory]
    [MemberData(nameof(InputNames))]
    public void EveryInput_ChangesSomething(string name)
    {
        var change = Inputs.Single(i => i.Name == name).Change;
        int mattered = 0;
        Gen.Select(Gens.BodyInputs, Gens.Contexts, Gen.Double.Unit).Check(t =>
        {
            var (input, ctx, u) = t;
            if (Outcome(input, ctx) != Outcome(input, change(ctx, u))) mattered++;
        }, iter: 200);
        Assert.True(mattered > 0, $"changing {name} never changed anything the body does");
    }

    public static TheoryData<string> InputNames() => new(Inputs.Select(i => i.Name));

    // ---- the reference model ----

    /// Wetness under steady exposure, restated from the physics rather than read off the code: water
    /// in from the sky and the ground, sweat that could not evaporate, and drying paid for in latent
    /// heat. With the core held, the one-minute step is the linear recurrence w' = w(1+B) + A, whose
    /// closed form is w_n = (w0 + A/B)(1+B)^n - A/B (w0 + nA when B is 0), clamped to [0,1] once
    /// (the clamp absorbs, because the trajectory is monotone). B is the sweat term: wetter clothes
    /// let less sweat evaporate. The constants are the model's, and move when the model does.
    static class WetnessReference
    {
        const double MlPerFullSoak = 2000, KcalPerMl = 0.58;

        static double PrecipitationPerMinute(Precipitation p) => p switch
        {
            Precipitation.Rain => 0.01,
            Precipitation.Blizzard => 0.005,
            Precipitation.Snow => 0.003,
            _ => 0,
        };

        public static (double A, double B) Rates(double coreF, SurvivalContext c)
        {
            double keptOut = 1 - c.WaterproofingLevel;
            double sky = PrecipitationPerMinute(c.Precipitation) * c.PrecipitationPct * (1 - c.OverheadCoverLevel) * keptOut;
            double ground = c.GroundWettingPctPerMinute * (1 - c.GroundProtectionLevel) * keptOut;

            // Sweat, once skin circulation is wide open (at the sweating threshold): produced at up to
            // a litre an hour, evaporating only through unsealed, unsoaked clothing; the rest soaks in.
            double producedMlPerMinute = coreF >= Body.SweatingThresholdF
                ? 1000 * Math.Clamp((coreF - Body.SweatingThresholdF) / 4.0, 0, 1) / 60 : 0;
            double sealedPct = 1 - Math.Clamp(1 - c.WaterproofingLevel, 0, 1);
            // soaked = produced * (1 - (1 - sealed)(1 - w)) = produced * sealed + produced * (1 - sealed) * w
            double sweatA = producedMlPerMinute * sealedPct / MlPerFullSoak;
            double sweatB = producedMlPerMinute * (1 - sealedPct) / MlPerFullSoak;

            // Drying: the fire's heat (faded out from the setpoint to a sweat) or warm air, whichever supplies more, sped by
            // wind when nothing falls.
            double firePct = Math.Clamp((Body.SweatingThresholdF - coreF) / (Body.SweatingThresholdF - Body.BaseCoreTempF), 0, 1);
            double fireF = c.FireBonusF * firePct + c.TorchHeatF;
            double kcalPerHour = Math.Max(fireF > 0 ? 300 + fireF * 25 : 0, c.AirTempF > 32 ? (c.AirTempF - 32) * 10 : 0);
            double wind = c.FireBonusF > 0 ? c.FireDryingWindLevel ?? c.WindLevel : c.WindLevel;
            if (c.Precipitation == Precipitation.None) kcalPerHour *= 1 + wind * 1.5;
            double dryingPerMinute = kcalPerHour / (MlPerFullSoak * KcalPerMl) / 60;

            return (sky + ground + sweatA - dryingPerMinute, sweatB);
        }

        public static double After(double w0, double coreF, SurvivalContext c, int minutes)
        {
            var (a, b) = Rates(coreF, c);
            double w = b == 0 ? w0 + a * minutes : (w0 + a / b) * Math.Pow(1 + b, minutes) - a / b;
            return Math.Clamp(w, 0, 1);
        }
    }

    /// The real code, a minute at a time, against the closed form, minute by minute. The step's
    /// rounding compounds over a day, hence 1e-9 and not 0.
    [Fact]
    public void ReferenceModel_StepByStepWetness_MatchesTheClosedForm()
    {
        Gen.Select(Gens.BodyInputs, Gens.Contexts).Check(t =>
        {
            var (input, ctx) = t;
            var b = input.Build();
            double w0 = Wet(b);
            for (int n = 1; n <= 24 * 60; n++)
            {
                Effects.Set(b, EffectKind.Wet, Gens.NextWet(b, ctx, 1));
                if (n % 30 != 0) continue;
                double expected = WetnessReference.After(w0, input.CoreTempF, ctx, n);
                Assert.True(Math.Abs(expected - Wet(b)) <= 1e-9, $"minute {n}: expected {expected:R}, got {Wet(b):R}");
            }
        }, iter: 150);
    }

    /// The same exposure run through Update, the path the game takes, with the core free to move:
    /// at or under the setpoint, out of a sweat and with the fire's heat not yet fading, the core does not
    /// touch wetness, so the closed form still holds. Warmer cases are left to the step-by-step test.
    [Fact]
    public void ReferenceModel_UpdateWetness_MatchesTheClosedForm_OutOfASweat()
    {
        var notSweating = Gens.BodyInputs.Where(i => i.CoreTempF < Body.SweatingThresholdF - 1);
        Gen.Select(notSweating, Gens.Contexts).Check(t =>
        {
            var (input, ctx) = t;
            var b = input.Build();
            var rng = Gens.Rng();
            double w0 = Wet(b);
            for (int n = 1; n <= 180 && Body.IsAlive(b); n++)
            {
                if (b.CoreTempF > Body.BaseCoreTempF) return;
                Body.Update(b, ctx, rng);
                double expected = WetnessReference.After(w0, input.CoreTempF, ctx, n);
                Assert.True(Math.Abs(expected - Wet(b)) <= 1e-9, $"minute {n}: expected {expected:R}, got {Wet(b):R}");
            }
        }, iter: 150);
    }
}
