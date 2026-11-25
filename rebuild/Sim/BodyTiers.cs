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

        // Moving from the legs (found by name, so four-legged bodies count too), manipulation from a human's arms.
        const int moving = (int)Capacity.Moving, manipulation = (int)Capacity.Manipulation;
        c[moving] = LimbsScaledByTheRest(regions, caps, moving, IsLeg, _ => true);
        c[manipulation] = body.Kind != BodyKind.Human ? 0
            : LimbsScaledByTheRest(regions, caps, manipulation, IsArm, r => r.Name is "Head" or "Chest" or "Abdomen" || IsLeg(r));

        for (int cap = 0; cap < CapacityCount; cap++)
        {
            if (cap is moving or manipulation) continue;
            for (int r = 0; r < regions.Count; r++)
                c[cap] = Clamp01(c[cap] + caps[r * CapacityCount + cap]);
        }
    }

    /// The limbs' mean times (0.9 + 0.1 × the rest's mean): the rest scales them and adds nothing, so no working limb is zero.
    static double LimbsScaledByTheRest(List<Region> regions, ReadOnlySpan<double> caps, int cap, Func<Region, bool> isLimb, Func<Region, bool> isRest)
    {
        double limbSum = 0, restSum = 0;
        int limbs = 0, rest = 0;
        for (int r = 0; r < regions.Count; r++)
        {
            double v = caps[r * CapacityCount + cap];
            if (isLimb(regions[r])) { limbSum += v; limbs++; }
            else if (isRest(regions[r])) { restSum += v; rest++; }
        }
        if (limbs == 0) return 0;
        return limbSum / limbs * (0.9 + 0.1 * (rest == 0 ? 1 : restSum / rest));
    }

    static bool IsLeg(Region region) => region.Name.Contains("Leg");
    static bool IsArm(Region region) => region.Name.Contains("Arm");

    /// The gut's tissue alone, for eating and healing: no effect slows absorbing food. Not a capacity.
    internal static double Digestion(BodyState body)
    {
        Span<double> c = stackalloc double[CapacityCount];
        Fill(body, NoEffects, c);
        return c[(int)Capacity.Digestion];
    }

    static readonly double[] NoEffectDeltas = new double[CapacityCount];
    internal static ReadOnlySpan<double> NoEffects => NoEffectDeltas;

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
                Capacity.Breath => 0.8 + 0.2 * c,
                Capacity.Blood => 0.9 + 0.1 * c,
                _ => 1.0,
            },
            // Squared: a cracked leg bone barely slows you, a shattered one stops you.
            TissueKind.Bone => cap switch
            {
                Capacity.Moving => c * c,
                Capacity.Manipulation => c,
                Capacity.Blood => 0.8 + 0.2 * c,
                _ => 1.0,
            },
            _ => throw new ArgumentOutOfRangeException(nameof(t), t.Kind, "Not a layer"),
        };
    }

    internal static double Clamp01(double x) => Math.Clamp(x, 0, 1);
}

/// Blood and breath, and their scales (1 when whole): lost blood on consciousness and the limbs, short breath on consciousness.
internal readonly record struct BloodAndBreath(double Blood, double Breath, double LostBloodScale, double ShortBreathScale)
{
    internal const double CirculationFailsBloodPct = 0.5;
    const double BreathShortBelow = 0.3;

    /// Only real blood loss collapses circulation, not cold-slowed pumping.
    internal static BloodAndBreath Of(BodyState body, ReadOnlySpan<double> tissue)
    {
        double pumping = tissue[(int)Capacity.Blood], breath = tissue[(int)Capacity.Breath];
        return new(
            pumping * body.BloodPct,
            breath,
            body.BloodPct < 1.0 ? Math.Max(0, (body.BloodPct - CirculationFailsBloodPct) / (1 - CirculationFailsBloodPct)) : 1,
            breath < BreathShortBelow ? breath / BreathShortBelow : 1);
    }
}

/// The body's own business: outside it, only CapacityReadTests' listed reads may look.
public readonly record struct Capacities(double Moving, double Manipulation, double Consciousness, double Sight, double Hearing)
{
    internal static Capacities Of(BodyState body) => Of(body, out _);

    internal static Capacities Of(BodyState body, out BloodAndBreath feed)
    {
        Span<double> effectDeltas = stackalloc double[TissueReads.CapacityCount];
        Effects.CapacityDeltas(body, effectDeltas);
        return Of(body, effectDeltas, out feed);
    }

    /// Tissue and blood alone, as life uses: no effect stops a heart.
    internal static Capacities WithoutEffects(BodyState body, out BloodAndBreath feed) => Of(body, TissueReads.NoEffects, out feed);

    static Capacities Of(BodyState body, ReadOnlySpan<double> effectDeltas, out BloodAndBreath feed) =>
        Scaled(Unscaled(body, effectDeltas, out feed), feed);

    internal static Capacities Unscaled(BodyState body, out BloodAndBreath feed)
    {
        Span<double> effectDeltas = stackalloc double[TissueReads.CapacityCount];
        Effects.CapacityDeltas(body, effectDeltas);
        return Unscaled(body, effectDeltas, out feed);
    }

    static Capacities Unscaled(BodyState body, ReadOnlySpan<double> effectDeltas, out BloodAndBreath feed)
    {
        Span<double> c = stackalloc double[TissueReads.CapacityCount];
        TissueReads.Fill(body, effectDeltas, c);
        feed = BloodAndBreath.Of(body, c);
        return new Capacities(c[(int)Capacity.Moving], c[(int)Capacity.Manipulation], c[(int)Capacity.Consciousness],
            c[(int)Capacity.Sight], c[(int)Capacity.Hearing]);
    }

    internal static Capacities Scaled(in Capacities unscaled, in BloodAndBreath feed)
    {
        double consciousness = unscaled.Consciousness * feed.LostBloodScale * feed.ShortBreathScale;
        double limbs = consciousness > 0 ? feed.LostBloodScale : 0; // unconscious, the limbs give nothing
        return new Capacities(unscaled.Moving * limbs, unscaled.Manipulation * limbs, consciousness, unscaled.Sight, unscaled.Hearing);
    }
}

public enum Faculty { Strength, Speed, Dexterity, Perception }

/// Why a faculty is at zero (GLOSSARY: Crippled). NoWorkingLimb: its limbs give nothing, wounded or cold.
public enum CrippledBy { NoWorkingLimb, LostBlood, Unconscious, Load, Build }

/// Each reads the capacities and the body's build: none reads vitality, the tier above.
internal static class Faculties
{
    internal static double Strength(BodyState body) => Strength(body, Capacities.Of(body));

    /// Power output from composition, through working legs and hands.
    internal static double Strength(BodyState body, in Capacities c)
    {
        double musclePct = body.MusclePct, fatPct = body.FatPct;

        double muscle = musclePct < 0.2 ? musclePct * 2.5
            : musclePct < 0.4 ? 0.5 + (musclePct - 0.2) * 1.0
            : 0.7 + (musclePct - 0.4) * 0.5;
        muscle += body.MuscleKg / 3 / 100;

        double fat = body.FatKg / 50 / 100 - (fatPct < 0.05 ? (0.05 - fatPct) * 3.0 : 0);

        return (0.30 + muscle + fat) * (0.50 + 0.50 * c.Moving) * (0.80 + 0.20 * c.Manipulation);
    }

    /// The build Speed's numbers were tuned around; not the baseline man, so moving that does not re-tune it.
    const double SpeedReferenceWeightKg = 75, SpeedReferenceFatPct = 0.15, SpeedReferenceMusclePct = 0.30;

    internal static double Speed(BodyState body, AbilityContext context = default) => Speed(body, Capacities.Of(body), context);

    internal static double Speed(BodyState body, in Capacities c, AbilityContext context)
    {
        var (shape, size) = Build(body);
        return c.Moving * shape * size * LoadScale(body, c, context);
    }

    static (double Shape, double Size) Build(BodyState body)
    {
        double fatPct = body.FatPct;

        double muscleModifier = (body.MusclePct - SpeedReferenceMusclePct) / 1.5 + body.MuscleKg / 20 / 100;

        double fatPenalty = fatPct < 0.10 ? -0.01
            : fatPct <= SpeedReferenceFatPct ? (fatPct - 0.10) * 0.20 - 0.01
            : (fatPct - SpeedReferenceFatPct) * 1.5;
        fatPenalty += body.FatKg / 10 / 100;

        double structuralRatio = body.StructuralKg / body.WeightKg / 0.45;
        double weightEffect = -(Math.Pow(structuralRatio, 0.7) - 1.0);
        double sizeModifier = 1 - 0.03 * Math.Log2(body.WeightKg / SpeedReferenceWeightKg);

        // Each term held at zero: two below it would make a body faster under its pack.
        return (Math.Max(0, 1 + muscleModifier - fatPenalty + weightEffect), Math.Max(0, sizeModifier));
    }

    /// Strength modulates load: a strong person feels 30kg less than a weak one.
    static double LoadScale(BodyState body, in Capacities c, AbilityContext context)
    {
        double effectiveLoad = context.EncumbrancePct / (0.5 + Strength(body, c) * 0.5);
        double loadPenalty = Math.Max(0, (effectiveLoad - 0.5) * 0.8);
        return Math.Max(0, 1 - loadPenalty);
    }

    /// Empty exactly when Speed, these terms multiplied, is above zero.
    internal static IReadOnlyList<CrippledBy> SpeedCrippledBy(BodyState body, AbilityContext context = default)
    {
        var unscaled = Capacities.Unscaled(body, out var feed);
        var c = Capacities.Scaled(unscaled, feed);
        var why = new List<CrippledBy>();
        if (unscaled.Moving <= 0) why.Add(CrippledBy.NoWorkingLimb);
        if (feed.LostBloodScale <= 0) why.Add(CrippledBy.LostBlood);
        else if (unscaled.Consciousness * feed.ShortBreathScale <= 0) why.Add(CrippledBy.Unconscious);
        if (Build(body) is var (shape, size) && (shape <= 0 || size <= 0)) why.Add(CrippledBy.Build);
        if (LoadScale(body, c, context) <= 0) why.Add(CrippledBy.Load);
        return why;
    }

    internal static double Perception(BodyState body, AbilityContext context = default) => Perception(Capacities.Of(body), context);

    /// Sight and hearing are half each, so the healthy perceive half as much in pitch dark; a foggy head takes from both.
    internal static double Perception(in Capacities c, AbilityContext context)
    {
        double darkness = context.HasLightSource ? 0 : context.DarknessLevel;
        double perceived = c.Sight * 0.5 * (1 - darkness) + c.Hearing * 0.5;
        return perceived * c.Consciousness;
    }

    internal static double Dexterity(BodyState body, AbilityContext context = default) =>
        Dexterity(Capacities.Of(body), context, Effects.Severity(body, EffectKind.Wet));

    /// Fine motor control: a foggy head fumbles, you can't see your hands in the dark, wet hands slip.
    internal static double Dexterity(in Capacities c, AbilityContext context, double wetSeverity)
    {
        double darknessPenalty = context.HasLightSource ? 0 : context.DarknessLevel * 0.5;
        double wetnessPenalty = Math.Max(0, (wetSeverity - 0.3) * 0.3);
        return c.Manipulation * (0.5 + 0.5 * c.Consciousness) * (1 - darknessPenalty) * (1 - wetnessPenalty);
    }

    internal static IReadOnlyList<string> DexterityCosts(BodyState body, AbilityContext context = default)
    {
        var c = Capacities.Of(body);
        var costs = new List<string>();
        if (c.Manipulation < 1) costs.Add("hurt or cold hands");
        if (c.Consciousness < 1) costs.Add("foggy head");
        if (!context.HasLightSource && context.DarknessLevel > 0) costs.Add("dark");
        if (Effects.Severity(body, EffectKind.Wet) > 0.3) costs.Add("wet");
        return costs;
    }
}

/// The mean of the faculties, each as its share of a fresh body of the species and sex, read with no
/// surroundings (no dark, wet or load). One a fresh body lacks (a wolf's dexterity) is left out.
internal static class Vitality
{
    readonly record struct FacultyRead(double Strength, double Speed, double Dexterity, double Perception);

    static FacultyRead Read(BodyState body, in Capacities c) =>
        new(Faculties.Strength(body, c), Faculties.Speed(body, c, default), Faculties.Dexterity(c, default, wetSeverity: 0), Faculties.Perception(c, default));

    static readonly int SexCount = Enum.GetValues<Sex>().Length;

    /// Built once on first use, by the type initializer: thread-safe.
    static readonly FacultyRead[] Fresh = [.. Enum.GetValues<Species>().SelectMany(s => Enum.GetValues<Sex>().Select(sex =>
    {
        var body = SpeciesTable.Average(s, sex);
        return Read(body, Capacities.Of(body));
    }))];

    static FacultyRead FreshOf(Species species, Sex sex) =>
        Fresh[(int)species * SexCount + (int)(species == Species.Human ? sex : Sex.Male)];

    /// A faculty as its share of a fresh body of the species and sex, read with no surroundings and held at
    /// one for a body better than fresh. Zero for a faculty a fresh body lacks (a wolf's dexterity).
    internal static double Share(BodyState body, Species species, Faculty faculty)
    {
        var c = Capacities.Of(body);
        var fresh = FreshOf(species, body.Sex);
        var (now, whole) = faculty switch
        {
            Faculty.Strength => (Faculties.Strength(body, c), fresh.Strength),
            Faculty.Speed => (Faculties.Speed(body, c, default), fresh.Speed),
            Faculty.Dexterity => (Faculties.Dexterity(c, default, wetSeverity: 0), fresh.Dexterity),
            Faculty.Perception => (Faculties.Perception(c, default), fresh.Perception),
            _ => throw new ArgumentOutOfRangeException(nameof(faculty), faculty, null),
        };
        return whole > 0 ? TissueReads.Clamp01(now / whole) : 0;
    }

    internal static double Of(BodyState body, Species species)
    {
        var now = Read(body, Capacities.Of(body));
        var fresh = FreshOf(species, body.Sex);
        double sum = 0;
        int counted = 0;
        void Add(double faculty, double whole)
        {
            if (whole <= 0) return;
            sum += TissueReads.Clamp01(faculty / whole);
            counted++;
        }
        Add(now.Strength, fresh.Strength);
        Add(now.Speed, fresh.Speed);
        Add(now.Dexterity, fresh.Dexterity);
        Add(now.Perception, fresh.Perception);
        return sum / counted;
    }
}

/// Effects disable (consciousness can fall to nothing) but never kill: life is tissue and blood alone.
internal static class Alive
{
    internal static bool Is(BodyState body) => TissueVitality(body) > 0;

    internal static double TissueVitality(BodyState body)
    {
        var c = Capacities.WithoutEffects(body, out var feed);
        return Math.Min(feed.Breath, Math.Min(feed.Blood, c.Consciousness));
    }
}

/// The screen's one read of a body's capacities, blood and breath, in the health panel's order (BodyTierTests).
public static class BodyReadOut
{
    public static IReadOnlyList<(Capacity Capacity, double Pct)> Of(BodyState body)
    {
        var c = Capacities.Of(body, out var feed);
        return [(Capacity.Moving, c.Moving), (Capacity.Manipulation, c.Manipulation), (Capacity.Breath, feed.Breath),
            (Capacity.Blood, feed.Blood), (Capacity.Consciousness, c.Consciousness), (Capacity.Sight, c.Sight),
            (Capacity.Hearing, c.Hearing)];
    }
}
