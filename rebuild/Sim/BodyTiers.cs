namespace Survival;

// Each tier reads only those below (BodyTierTests); Body's faculties, vitality and life are the interface over them.

public readonly record struct AbilityContext(double EncumbrancePct = 0, double DarknessLevel = 0, bool HasLightSource = false);

internal static class TissueReads
{
    internal const int CapacityCount = 8;

    /// On the stack: every animal's life and legs are read every minute (Sim.IsAlive, Fauna's minds).
    internal static void Fill(BodyState body, ReadOnlySpan<double> effectDeltas, Span<double> c)
    {
        if (IsWhole(body)) WholeTissueCapacities[(int)body.Kind].CopyTo(c);
        else TissueCapacities(body, c);

        for (int i = 0; i < CapacityCount; i++)
            c[i] = Clamp01(Clamp01(c[i]) + Math.Clamp(effectDeltas[i], -1, 1));
    }

    static bool IsWhole(BodyState body)
    {
        foreach (var r in body.Regions)
        {
            if (r.Skin.Condition != 1 || r.Muscle.Condition != 1 || r.Bone.Condition != 1) return false;
            foreach (var o in r.Organs) if (o.Condition != 1) return false;
        }
        return true;
    }

    static readonly double[][] WholeTissueCapacities = [.. Enum.GetValues<BodyKind>().Select(WholeCapacitiesOf)];

    static double[] WholeCapacitiesOf(BodyKind kind)
    {
        var c = new double[CapacityCount];
        TissueCapacities(BodyState.Create(kind, Body.BaselineWeightKg, Body.BaselineFatPct, Body.BaselineMusclePct), c);
        return c;
    }

    /// Fills c (zeroed, one per Capacity). Nothing here allocates: a wounded animal is read as often as a whole one.
    static void TissueCapacities(BodyState body, Span<double> c)
    {
        var regions = body.Regions;
        Span<double> caps = stackalloc double[regions.Count * CapacityCount];
        for (int r = 0; r < regions.Count; r++) RegionCapacities(regions[r], caps.Slice(r * CapacityCount, CapacityCount));

        // Moving: 90% legs, 10% everything else. Legs are found by name so four-legged bodies count too.
        int legs = 0;
        foreach (var r in regions) if (IsLeg(r)) legs++;
        int others = regions.Count - legs;
        const int moving = (int)Capacity.Moving, manipulation = (int)Capacity.Manipulation;
        double legSum = 0, otherSum = 0, allSum = 0;
        for (int r = 0; r < regions.Count; r++)
        {
            double m = caps[r * CapacityCount + moving];
            if (legs == 0) allSum += m;
            else if (IsLeg(regions[r])) legSum += m * 0.9 / legs;
            else otherSum += m * 0.1 / others;
        }
        c[moving] = legs == 0 ? allSum / regions.Count : legSum + otherSum;

        // Manipulation: 90% arms, 2% each from the other human regions.
        double manip = 0;
        for (int r = 0; r < regions.Count; r++)
            manip += caps[r * CapacityCount + manipulation] * regions[r].Name switch
            {
                "Left Arm" or "Right Arm" => 0.45,
                "Head" or "Chest" or "Abdomen" or "Left Leg" or "Right Leg" => 0.02,
                _ => 0,
            };
        c[manipulation] = manip;

        for (int cap = 0; cap < CapacityCount; cap++)
        {
            if (cap is moving or manipulation) continue;
            for (int r = 0; r < regions.Count; r++)
                c[cap] = Clamp01(c[cap] + caps[r * CapacityCount + cap]);
        }
    }

    static bool IsLeg(Region region) => region.Name.Contains("Leg");

    /// Organs supply in proportion to their condition, plus the muscle's work, limited by the region's
    /// weakest layer. Not an average over tissues: that leaves two-thirds of breathing with both lungs destroyed.
    internal static double[] RegionCapacities(Region region)
    {
        var result = new double[CapacityCount];
        RegionCapacities(region, result);
        return result;
    }

    static void RegionCapacities(Region region, Span<double> result)
    {
        if (region.IsDestroyed) return;

        foreach (var organ in region.Organs)
            if (organ.Provides is Capacity p)
                result[(int)p] += organ.ProvidesAmount * organ.Condition;
        result[(int)Capacity.Moving] += 1;       // muscle
        result[(int)Capacity.Manipulation] += 1;

        for (int i = 0; i < CapacityCount; i++)
        {
            var cap = (Capacity)i;
            result[i] = Clamp01(result[i])
                * WeakestOf(LayerMultiplier(region.Skin, cap), LayerMultiplier(region.Muscle, cap), LayerMultiplier(region.Bone, cap));
        }
    }

    /// The least of the three, as Enumerable.Min reads them in order: the first of equals, and a NaN wins.
    static double WeakestOf(double skin, double muscle, double bone)
    {
        if (double.IsNaN(skin)) return skin;
        double least = skin;
        if (muscle < least) least = muscle; else if (double.IsNaN(muscle)) return muscle;
        if (bone < least) least = bone; else if (double.IsNaN(bone)) return bone;
        return least;
    }

    /// 1 where the layer plays no part: skin carries a limb's work, not the organs' (a scalp wound does not dim the eyes).
    static double LayerMultiplier(Tissue t, Capacity cap)
    {
        double c = t.Condition;
        return t.Kind switch
        {
            TissueKind.Skin => cap is Capacity.Moving or Capacity.Manipulation ? c : 1.0,
            TissueKind.Muscle => cap switch
            {
                Capacity.Moving or Capacity.Manipulation => c,
                Capacity.Breathing => 0.8 + 0.2 * c,
                Capacity.BloodPumping => 0.9 + 0.1 * c,
                _ => 1.0,
            },
            // Squared: a cracked leg bone barely slows you, a shattered one stops you.
            TissueKind.Bone => cap switch
            {
                Capacity.Moving => c * c,
                Capacity.Manipulation => c,
                Capacity.BloodPumping => 0.8 + 0.2 * c,
                _ => 1.0,
            },
            _ => throw new ArgumentOutOfRangeException(nameof(t), t.Kind, "Not a layer"),
        };
    }

    internal static double Clamp01(double x) => Math.Clamp(x, 0, 1);
}

/// What lost blood takes from consciousness, moving and manipulation, and short breath from consciousness: 1
/// when nothing.
internal readonly record struct BloodAndBreath(double BloodPumping, double CirculationPct, double BreathPct)
{
    internal const double CirculationFailsBloodPct = 0.5;
    const double BreathShortBelow = 0.3;

    /// Only real blood loss collapses circulation, not cold-slowed pumping.
    internal static BloodAndBreath Of(BodyState body, double tissueBreathing, double tissueBloodPumping) => new(
        tissueBloodPumping * body.BloodPct,
        body.BloodPct < 1.0 ? Math.Max(0, (body.BloodPct - CirculationFailsBloodPct) / (1 - CirculationFailsBloodPct)) : 1,
        tissueBreathing < BreathShortBelow ? tissueBreathing / BreathShortBelow : 1);
}

/// The body's own business: outside it, only CapacityReadTests' listed reads may look.
public readonly record struct Capacities(
    double Moving, double Manipulation, double Breathing, double BloodPumping,
    double Consciousness, double Sight, double Hearing, double Digestion)
{
    internal static Capacities Of(BodyState body)
    {
        Span<double> effectDeltas = stackalloc double[TissueReads.CapacityCount];
        Effects.CapacityDeltas(body, effectDeltas);
        return Of(body, effectDeltas);
    }

    /// Tissue and blood alone, as digestion and life use: hunger does not slow absorbing food, and no effect stops a heart.
    internal static Capacities WithoutEffects(BodyState body) => Of(body, NoEffectDeltas);

    static readonly double[] NoEffectDeltas = new double[TissueReads.CapacityCount];

    internal double WeakestVital => Math.Min(Breathing, Math.Min(BloodPumping, Consciousness));

    static Capacities Of(BodyState body, ReadOnlySpan<double> effectDeltas)
    {
        Span<double> c = stackalloc double[TissueReads.CapacityCount];
        TissueReads.Fill(body, effectDeltas, c);
        const int consciousness = (int)Capacity.Consciousness, moving = (int)Capacity.Moving, manipulation = (int)Capacity.Manipulation;

        var feed = BloodAndBreath.Of(body, c[(int)Capacity.Breathing], c[(int)Capacity.BloodPumping]);
        c[(int)Capacity.BloodPumping] = feed.BloodPumping;
        c[consciousness] *= feed.CirculationPct;
        c[moving] *= feed.CirculationPct;
        c[manipulation] *= feed.CirculationPct;
        c[consciousness] *= feed.BreathPct;
        if (c[consciousness] < 0.1)
        {
            c[moving] *= 0.1;
            c[manipulation] *= 0.1;
        }

        return new Capacities(c[0], c[1], c[2], c[3], c[4], c[5], c[6], c[7]);
    }
}

/// Each still scales by vitality, a tier above: BodyTierTests lists those reads.
internal static class Faculties
{
    /// Power output from composition, scaled by vitality: dying is weak, whatever the muscles.
    internal static double Strength(BodyState body)
    {
        var c = Capacities.Of(body);
        double musclePct = body.MusclePct, fatPct = body.FatPct;

        double muscle = musclePct < 0.2 ? musclePct * 2.5
            : musclePct < 0.4 ? 0.5 + (musclePct - 0.2) * 1.0
            : 0.7 + (musclePct - 0.4) * 0.5;
        muscle += body.MuscleKg / 3 / 100;

        double fat = body.FatKg / 50 / 100 - (fatPct < 0.05 ? (0.05 - fatPct) * 3.0 : 0);

        return (0.30 + muscle + fat) * (0.50 + 0.50 * c.Moving) * (0.80 + 0.20 * c.Manipulation) * Vitality.Of(c);
    }

    /// The build Speed's numbers were tuned around; not the baseline man, so moving that does not re-tune it.
    const double SpeedReferenceWeightKg = 75, SpeedReferenceFatPct = 0.15, SpeedReferenceMusclePct = 0.30;

    /// Movement rate. Strength modulates load: a strong person feels 30kg less than a weak one.
    internal static double Speed(BodyState body, AbilityContext context = default)
    {
        var c = Capacities.Of(body);
        double fatPct = body.FatPct;

        double muscleModifier = (body.MusclePct - SpeedReferenceMusclePct) / 1.5 + body.MuscleKg / 20 / 100;

        double fatPenalty = fatPct < 0.10 ? -0.01
            : fatPct <= SpeedReferenceFatPct ? (fatPct - 0.10) * 0.20 - 0.01
            : (fatPct - SpeedReferenceFatPct) * 1.5;
        fatPenalty += body.FatKg / 10 / 100;

        double structuralRatio = body.StructuralKg / body.WeightKg / 0.45;
        double weightEffect = -(Math.Pow(structuralRatio, 0.7) - 1.0);
        double sizeModifier = 1 - 0.03 * Math.Log2(body.WeightKg / SpeedReferenceWeightKg);

        double baseSpeed = c.Moving * (1 + muscleModifier - fatPenalty + weightEffect) * sizeModifier;

        double vitalityFactor = 0.5 + Vitality.Of(c) * 0.5;
        double effectiveLoad = context.EncumbrancePct / (0.5 + Strength(body) * 0.5);
        double loadPenalty = Math.Max(0, (effectiveLoad - 0.5) * 0.8);
        return baseSpeed * vitalityFactor * (1 - loadPenalty);
    }

    /// Consciousness counts twice, once through vitality: being dazed tanks it. Sight and hearing are
    /// half each, so the healthy perceive half as much in pitch dark as at noon.
    internal static double Perception(BodyState body, AbilityContext context = default)
    {
        var c = Capacities.Of(body);
        double darkness = context.HasLightSource ? 0 : context.DarknessLevel;
        double perceived = c.Sight * 0.5 * (1 - darkness) + c.Hearing * 0.5;
        return perceived * c.Consciousness * (0.7 + Vitality.Of(c) * 0.3);
    }

    /// Fine motor control: can't see your hands in the dark, wet hands slip, the dying shake.
    internal static double Dexterity(BodyState body, AbilityContext context = default)
    {
        var c = Capacities.Of(body);
        double darknessPenalty = context.HasLightSource ? 0 : context.DarknessLevel * 0.5;
        double wetnessPenalty = Math.Max(0, (Effects.Severity(body, EffectKind.Wet) - 0.3) * 0.3);
        return c.Manipulation * (1 - darknessPenalty) * (1 - wetnessPenalty) * (0.7 + Vitality.Of(c) * 0.3);
    }

    internal static IReadOnlyList<string> DexterityCosts(BodyState body, AbilityContext context = default)
    {
        var c = Capacities.Of(body);
        var costs = new List<string>();
        if (c.Manipulation < 1) costs.Add("hurt or cold hands");
        if (!context.HasLightSource && context.DarknessLevel > 0) costs.Add("dark");
        if (Effects.Severity(body, EffectKind.Wet) > 0.3) costs.Add("wet");
        if (Vitality.Of(c) < 1) costs.Add("weak");
        return costs;
    }
}

/// The least of breathing, blood pumping and consciousness.
internal static class Vitality
{
    internal static double Of(BodyState body) => Of(Capacities.Of(body));
    internal static double Of(Capacities c) => c.WeakestVital;
}

/// Effects disable (consciousness can fall to nothing) but never kill: life is tissue and blood alone.
internal static class Alive
{
    internal static bool Is(BodyState body) => TissueVitality(body) > 0;

    internal static double TissueVitality(BodyState body) => Capacities.WithoutEffects(body).WeakestVital;
}

/// The screen's one read of a body's capacities, blood and breath, in the health panel's order (BodyTierTests).
public static class BodyReadOut
{
    public static IReadOnlyList<(Capacity Capacity, double Pct)> Of(BodyState body)
    {
        var c = Capacities.Of(body);
        return [(Capacity.Moving, c.Moving), (Capacity.Manipulation, c.Manipulation), (Capacity.Breathing, c.Breathing),
            (Capacity.BloodPumping, c.BloodPumping), (Capacity.Consciousness, c.Consciousness), (Capacity.Sight, c.Sight),
            (Capacity.Hearing, c.Hearing)];
    }
}
