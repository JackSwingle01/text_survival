using CsCheck;
using Survival;
using Survival.Tests.Verification;

/// The effect registry's laws (documentation/verification.md): bounds, time-splitting, direction,
/// fixed point and merge, checked over generated call sequences rather than picked numbers. The
/// reference model below restates each operation from the definition table alone.
public class EffectsPropertyTests
{
    static EffectDef Def(EffectKind kind) => Content.Tables.Effects[kind];

    static bool NeedsTreatment(EffectKind kind, double severity) =>
        Def(kind).TreatmentAboveSeverity is double above && severity > above;

    /// What a body's effects are, in an order-free form two bodies can be compared by.
    static List<(EffectKind Kind, double Severity, bool RequiresTreatment)> Snapshot(BodyState b) =>
        b.Effects.Select(e => (e.Kind, e.Severity, e.RequiresTreatment)).OrderBy(e => e.Kind).ToList();

    /// Same effects, same treatment flags, severities within the tolerance. With a tolerance, an
    /// effect within it of zero counts as gone: rounding can leave a stretch split in two holding
    /// 1e-17 of a bleed the whole stretch ended exactly, and that is the same body.
    static void AssertSameEffects(BodyState expected, BodyState actual, double tolerance = 0)
    {
        var (x, y) = (Snapshot(expected).Where(e => e.Severity > tolerance).ToList(), Snapshot(actual).Where(e => e.Severity > tolerance).ToList());
        Assert.Equal(x.Select(e => (e.Kind, e.RequiresTreatment)), y.Select(e => (e.Kind, e.RequiresTreatment)));
        for (int i = 0; i < x.Count; i++)
            Assert.True(Math.Abs(x[i].Severity - y[i].Severity) <= tolerance,
                $"{x[i].Kind}: {x[i].Severity:R} vs {y[i].Severity:R} (tolerance {tolerance})");
    }

    /// Decay, restated from the table: drift at the hourly rate, stop at the treatment floor
    /// (never raising an effect up to it), clamp to 1, drop at zero.
    static List<(EffectKind, double, bool)> DecayReference(BodyState b, int minutes) =>
        b.Effects
            .Select(e =>
            {
                double floor = e.RequiresTreatment ? Math.Min(Effects.TreatmentFloorPct, e.Severity) : 0;
                double next = Math.Clamp(e.Severity + Def(e.Kind).SeverityPerHour / 60 * minutes, floor, 1);
                return (e.Kind, next, e.RequiresTreatment);
            })
            .Where(e => e.next > 0)
            .OrderBy(e => e.Kind)
            .ToList();

    // ---- bounds ----

    [Fact]
    public void Bounds_AnyCallSequence_LeavesOneEffectPerKind_EachInItsRange()
    {
        Gens.EffectOps.Check(ops =>
        {
            var b = Gens.BodyAfter(ops);
            Assert.Equal(b.Effects.Count, b.Effects.Select(e => e.Kind).Distinct().Count());
            foreach (var e in b.Effects)
                Assert.InRange(e.Severity, double.Epsilon, 1.0); // zero removes, so a held effect is never 0
        });
    }

    [Fact]
    public void Severity_IsZeroWhenAbsent_AndHasMeansPresent()
    {
        Gen.Select(Gens.EffectOps, Gens.Kind).Check(t =>
        {
            var (ops, kind) = t;
            var b = Gens.BodyAfter(ops);
            var held = b.Effects.SingleOrDefault(e => e.Kind == kind);
            Assert.Equal(held != null, Effects.Has(b, kind));
            Assert.Equal(held?.Severity ?? 0, Effects.Severity(b, kind));
        });
    }

    // ---- decay: direction, time-splitting, fixed point, reference ----

    [Fact]
    public void Decay_MatchesTheReferenceModel()
    {
        Gen.Select(Gens.EffectOps, Gens.Minutes).Check(t =>
        {
            var (ops, minutes) = t;
            var b = Gens.BodyAfter(ops);
            var expected = DecayReference(b, minutes);
            Effects.Decay(b, minutes);
            Assert.Equal(expected, Snapshot(b));
        });
    }

    [Fact]
    public void Decay_NeverRaisesSeverity_NorStartsAnEffect()
    {
        Gen.Select(Gens.EffectOps, Gens.Minutes).Check(t =>
        {
            var (ops, minutes) = t;
            var b = Gens.BodyAfter(ops);
            var before = b.Effects.ToDictionary(e => e.Kind, e => e.Severity);
            Effects.Decay(b, minutes);
            foreach (var e in b.Effects)
            {
                Assert.True(before.ContainsKey(e.Kind), $"decay started {e.Kind}");
                Assert.True(e.Severity <= before[e.Kind], $"{e.Kind} rose from {before[e.Kind]:R} to {e.Severity:R}");
            }
        });
    }

    /// Decay is a clamped straight line, so splitting it is exact up to rounding: N one-minute steps
    /// against one N-minute step, and any split of a stretch in two.
    [Fact]
    public void Decay_TimeSplits_NOneMinuteStepsEqualOneNMinuteStep()
    {
        Gen.Select(Gens.EffectOps, Gen.Int[0, 600], Gen.Int[0, 600]).Check(t =>
        {
            var (ops, a, c) = t;
            var whole = Gens.BodyAfter(ops);
            var split = Gens.BodyAfter(ops);
            var minutes = Gens.BodyAfter(ops);
            Effects.Decay(whole, a + c);
            Effects.Decay(split, a);
            Effects.Decay(split, c);
            for (int i = 0; i < a + c; i++) Effects.Decay(minutes, 1);
            AssertSameEffects(whole, split, 1e-9);
            AssertSameEffects(whole, minutes, 1e-9);
        }, iter: 150);
    }

    [Fact]
    public void Decay_ZeroMinutes_ChangesNothing()
    {
        Gens.EffectOps.Check(ops =>
        {
            var b = Gens.BodyAfter(ops);
            var before = Snapshot(b);
            Effects.Decay(b, 0);
            Assert.Equal(before, Snapshot(b));
        });
    }

    /// Left alone, everything that fades is gone, what needs treatment rests at its floor, and what
    /// holds (wetness) holds: a fixed point further decay does not move.
    [Fact]
    public void Decay_SettlesAtAFixedPoint()
    {
        Gens.EffectOps.Check(ops =>
        {
            var b = Gens.BodyAfter(ops);
            var before = b.Effects.ToDictionary(e => e.Kind, e => (e.Severity, e.RequiresTreatment));
            Effects.Decay(b, 20_000); // longer than the slowest fade (0.02/h from full)
            foreach (var (kind, (severity, treat)) in before)
            {
                double rate = Def(kind).SeverityPerHour;
                double expected = rate == 0 ? severity : treat ? Math.Min(Effects.TreatmentFloorPct, severity) : 0;
                Assert.Equal(expected, Effects.Severity(b, kind), 12);
            }
            var settled = Snapshot(b);
            Effects.Decay(b, 1);
            Effects.Decay(b, 600);
            Assert.Equal(settled, Snapshot(b));
        });
    }

    [Fact]
    public void Decay_NegativeMinutes_Throws()
    {
        var b = BodyState.BaselineHuman();
        Effects.Add(b, EffectKind.Pain, 0.5);
        Assert.Throws<ArgumentOutOfRangeException>(() => Effects.Decay(b, -60));
        Assert.Equal(0.5, Effects.Severity(b, EffectKind.Pain));
    }

    // ---- add: the merge law ----

    /// The worse of old and new wins, except bleeding, where two wounds bleed more than one: max is
    /// commutative and lands on one of its inputs; bleeding's sum is commutative, at least the worse
    /// wound and at most full. Whether it needs treatment is part of the result, so it commutes too.
    [Fact]
    public void Add_Merges_Commutatively_WithinItsBounds()
    {
        Gen.Select(Gens.EffectOps, Gens.Kind, Gens.RawSeverity, Gens.RawSeverity).Check(t =>
        {
            var (ops, kind, x, y) = t;
            var ab = Gens.BodyAfter(ops);
            var ba = Gens.BodyAfter(ops);
            double old = Effects.Severity(ab, kind);
            Effects.Add(ab, kind, x); Effects.Add(ab, kind, y);
            Effects.Add(ba, kind, y); Effects.Add(ba, kind, x);
            AssertSameEffects(ab, ba, 1e-12);

            var (cx, cy) = (Math.Clamp(x, 0, 1), Math.Clamp(y, 0, 1));
            double merged = Effects.Severity(ab, kind);
            if (kind == EffectKind.Bleeding)
                Assert.Equal(Math.Min(old + cx + cy, 1.0), merged, 12);
            else
                Assert.Equal(Math.Max(old, Math.Max(cx, cy)), merged);
        });
    }

    [Fact]
    public void Add_NeverLowersSeverity_AndNeverClearsTreatment()
    {
        Gen.Select(Gens.EffectOps, Gens.Kind, Gens.RawSeverity).Check(t =>
        {
            var (ops, kind, x) = t;
            var b = Gens.BodyAfter(ops);
            double before = Effects.Severity(b, kind);
            bool treat = b.Effects.SingleOrDefault(e => e.Kind == kind)?.RequiresTreatment ?? false;
            Effects.Add(b, kind, x);
            Assert.True(Effects.Severity(b, kind) >= before);
            if (treat) Assert.True(b.Effects.Single(e => e.Kind == kind).RequiresTreatment);
        });
    }

    [Fact]
    public void Add_OfNothing_StartsNothing()
    {
        Gen.Select(Gens.Kind, Gen.Double[-1, 0]).Check(t =>
        {
            var b = BodyState.BaselineHuman();
            Effects.Add(b, t.Item1, t.Item2);
            Assert.Empty(b.Effects);
        });
    }

    /// An effect needs treatment once its severity has been strictly above the table's threshold,
    /// whether it started there or was worsened there.
    [Fact]
    public void Add_NeedsTreatment_ExactlyWhenEverAboveTheThreshold()
    {
        Gen.Select(Gens.Kind, Gens.RawSeverity.Array[1, 6]).Check(t =>
        {
            var (kind, adds) = t;
            var b = BodyState.BaselineHuman();
            foreach (var x in adds) Effects.Add(b, kind, x);
            var e = b.Effects.SingleOrDefault();
            if (e == null) return;
            double peak = kind == EffectKind.Bleeding ? e.Severity : adds.Max(x => Math.Clamp(x, 0, 1));
            Assert.Equal(NeedsTreatment(kind, peak), e.RequiresTreatment);
        });
    }

    [Fact]
    public void Add_TreatmentThreshold_IsStrict()
    {
        foreach (var (kind, def) in Content.Tables.Effects)
        {
            if (def.TreatmentAboveSeverity is not double above || above <= 0 || above >= 1) continue;
            var at = BodyState.BaselineHuman();
            Effects.Add(at, kind, above);
            Assert.False(at.Effects.Single().RequiresTreatment, $"{kind} at exactly {above}");
            var past = BodyState.BaselineHuman();
            Effects.Add(past, kind, above + 1e-9);
            Assert.True(past.Effects.Single().RequiresTreatment, $"{kind} just past {above}");
        }
    }

    /// Regression: a shock that worsened past its threshold (then 0.5) never needed treatment, while one
    /// that started there did. Shock has no threshold now (no treatment exists: body review A5), so
    /// however it worsens it needs none; worsening past a threshold is Add_RequiresTreatment's law.
    [Fact]
    public void Add_ShockWorsened_NeedsNoTreatment()
    {
        var b = BodyState.BaselineHuman();
        Effects.Add(b, EffectKind.Shock, 0.3);
        Effects.Add(b, EffectKind.Shock, 0.8);
        Assert.False(b.Effects.Single().RequiresTreatment);
    }

    /// Regression: NaN passed straight through the clamp and poisoned the severity for good.
    [Fact]
    public void Add_And_Set_RefuseNaN()
    {
        var b = BodyState.BaselineHuman();
        Effects.Add(b, EffectKind.Pain, 0.4);
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => Effects.Add(b, EffectKind.Pain, double.NaN));
        Assert.Contains("NaN", ex.Message);
        Assert.Throws<ArgumentOutOfRangeException>(() => Effects.Set(b, EffectKind.Pain, double.NaN));
        Assert.Throws<ArgumentOutOfRangeException>(() => Effects.Add(b, EffectKind.Dazed, double.NaN));
        Assert.Equal(0.4, Effects.Severity(b, EffectKind.Pain));
        Assert.False(Effects.Has(b, EffectKind.Dazed));
    }

    // ---- set: a level ----

    [Fact]
    public void Set_IsTheLevel_LastWriteWins_ZeroRemoves()
    {
        Gen.Select(Gens.EffectOps, Gens.Kind, Gens.RawSeverity, Gens.RawSeverity).Check(t =>
        {
            var (ops, kind, x, y) = t;
            var twice = Gens.BodyAfter(ops);
            var once = Gens.BodyAfter(ops);
            bool treatBefore = twice.Effects.SingleOrDefault(e => e.Kind == kind)?.RequiresTreatment ?? false;
            Effects.Set(twice, kind, x);
            Effects.Set(twice, kind, y);
            Effects.Set(once, kind, y);
            var (cx, cy) = (Math.Clamp(x, 0, 1), Math.Clamp(y, 0, 1));
            Assert.Equal(cy, Effects.Severity(twice, kind));
            Assert.Equal(cy > 0, Effects.Has(twice, kind));
            Assert.Equal(Snapshot(once).Where(e => e.Kind != kind), Snapshot(twice).Where(e => e.Kind != kind));
            if (cy > 0)
            {
                // Treatment is remembered while the effect lasts and starts afresh once zero removed it.
                bool survived = cx > 0;
                bool expected = NeedsTreatment(kind, cy) || (survived && (treatBefore || NeedsTreatment(kind, cx)));
                Assert.Equal(expected, twice.Effects.Single(e => e.Kind == kind).RequiresTreatment);
            }
        });
    }

    // ---- what effects do to the body ----

    /// The readouts are each effect's full-severity row scaled by its severity and summed, and nothing
    /// from an effect that is not there (hunger, thirst and tiredness are not effects: DeprivationTests). Read through the faculties of a whole human, whose capacities are each
    /// one less the summed loss, held in 0..1: speed is its moving, vision its sight and perception the mean of its sight
    /// and hearing, each times its consciousness for the last two; dexterity is manipulation times half of
    /// one plus consciousness, less what wet hands take. Unconscious, the limbs give nothing.
    [Fact]
    public void Readouts_AreSeverityScaledSumsOfTheTable()
    {
        double wholeSpeed = Body.Speed(BodyState.BaselineHuman());
        Gens.EffectOps.Check(ops =>
        {
            var b = Gens.BodyAfter(ops);
            var caps = new double[Enum.GetValues<Capacity>().Length];
            double core = 0, bleed = 0;
            foreach (var e in b.Effects)
            {
                foreach (var (cap, delta) in Def(e.Kind).Capacities)
                    caps[(int)cap] += delta * e.Severity;
                core += Def(e.Kind).CoreTempFPerHour * e.Severity;
                bleed += Def(e.Kind).BleedMlPerHour * e.Severity;
            }
            double Remaining(Capacity cap)
            {
                int i = (int)cap;
                return Math.Clamp(1 + Math.Clamp(caps[i], -1, 1), 0, 1);
            }
            double conscious = Remaining(Capacity.Consciousness), limbs = conscious > 0 ? 1 : 0;
            double sight = Remaining(Capacity.Sight), hearing = Remaining(Capacity.Hearing);
            double wetHands = 1 - Math.Max(0, (Effects.Severity(b, EffectKind.Wet) - 0.3) * 0.3);
            Assert.Equal(Remaining(Capacity.Moving) * limbs * wholeSpeed, Body.Speed(b), 12);
            Assert.Equal(Remaining(Capacity.Manipulation) * limbs * (0.5 + 0.5 * conscious) * wetHands, Body.Dexterity(b), 12);
            Assert.Equal(sight * conscious, Body.Vision(b), 12);
            Assert.Equal((sight + hearing) * 0.5 * conscious, Body.Perception(b), 12);
            Assert.Equal(core, Effects.CoreTempFPerHour(b), 12);
            Assert.Equal(bleed, Effects.BleedMlPerHour(b), 12);
        });
    }

    [Fact]
    public void UnknownKind_Throws_NamingIt()
    {
        var b = BodyState.BaselineHuman();
        var bogus = (EffectKind)999;
        var ex = Assert.Throws<KeyNotFoundException>(() => Effects.Add(b, bogus, 0.5));
        Assert.Contains("999", ex.Message);
    }
}
