using System.Text.Json.Serialization;

namespace Survival;

public enum BodyKind { Human, Quadruped }

public enum Capacity { Moving, Manipulation, Breathing, BloodPumping, Consciousness, Sight, Hearing, Digestion }

public enum TissueKind { Skin, Muscle, Bone, Organ }

public enum DamageType
{
    Blunt, Sharp, Pierce, Poison,
    /// Blood loss in ml; only Blood takes it.
    Bleed,
    /// Starvation, dehydration, cold: bypasses armor and natural absorption.
    Internal,
    Burn,
}

public enum BodyTarget
{
    Random,
    Head, Chest, Abdomen, LeftArm, RightArm, LeftLeg, RightLeg,
    AnyLeg, AnyArm,
    Blood,
    Heart, Brain, Lungs, Liver, Stomach, LeftKidney, RightKidney, LeftEye, RightEye, LeftEar, RightEar,
}

/// One layer or organ. Condition 0-1; Toughness divides the damage it takes.
public sealed class Tissue
{
    public required string Name { get; init; }
    public required TissueKind Kind { get; init; }
    public double Condition { get; set; } = 1.0;
    public double Toughness { get; set; } = 1.0;
    /// Organs only: the one capacity this organ supplies, and how much of it.
    public Capacity? Provides { get; init; }
    public double ProvidesAmount { get; init; }
    /// Eyes and ears: hit by anything that reaches the region at all.
    public bool IsExternal { get; init; }
}

public sealed class Region
{
    public required string Name { get; init; }
    /// Relative chance of being hit; regions sum to roughly 100.
    public required double Coverage { get; init; }
    public required Tissue Skin { get; init; }
    public required Tissue Muscle { get; init; }
    public required Tissue Bone { get; init; }
    public List<Tissue> Organs { get; init; } = [];

    /// Weakest tissue layer. Organs are deliberately excluded: they show up as vitality, not wounds.
    [JsonIgnore] public double Condition => Math.Min(Skin.Condition, Math.Min(Muscle.Condition, Bone.Condition));
    [JsonIgnore] public bool IsDestroyed => Condition <= 0;
    [JsonIgnore] public IEnumerable<Tissue> Layers => [Skin, Muscle, Bone];
}

/// Everything about a body, as plain data. Capacities and abilities are derived from it, never stored.
public sealed class BodyState
{
    public required BodyKind Kind { get; init; }
    public required List<Region> Regions { get; init; }
    /// Blood is 5000ml at 1.0; circulation collapses at 0.5.
    public double BloodPct { get; set; } = 1.0;
    /// Bone, organs, water: everything that is neither fat nor muscle.
    public required double StructuralKg { get; set; }
    public required double FatKg { get; set; }
    public required double MuscleKg { get; set; }
    public double CoreTempF { get; set; } = Body.BaseCoreTempF;
    public double CaloriesKcal { get; set; } = 1500;
    /// Minutes of waking left; sleep refills it.
    public double EnergyMinutes { get; set; } = 900;
    public double HydrationMl { get; set; } = 3000;
    /// Warmth stored in clothing's thermal mass, 0-1 of what it can hold.
    public double ClothingHeatBufferPct { get; set; } = 0.3;
    public List<BodyEffect> Effects { get; init; } = [];

    [JsonIgnore] public double WeightKg => StructuralKg + FatKg + MuscleKg;
    [JsonIgnore] public double FatPct => FatKg / WeightKg;
    [JsonIgnore] public double MusclePct => MuscleKg / WeightKg;
    [JsonIgnore] public double EnergyPct => EnergyMinutes / Body.MaxEnergyMinutes;
    [JsonIgnore] public double CaloriesPct => CaloriesKcal / Body.MaxCaloriesKcal;
    [JsonIgnore] public double HydrationPct => HydrationMl / Body.MaxHydrationMl;

    public static BodyState Create(BodyKind kind, double weightKg, double fatPct, double musclePct) => new()
    {
        Kind = kind,
        Regions = kind == BodyKind.Human ? Body.HumanRegions() : Body.QuadrupedRegions(),
        FatKg = weightKg * fatPct,
        MuscleKg = weightKg * musclePct,
        StructuralKg = weightKg * (1 - fatPct - musclePct),
    };

    public static BodyState BaselineHuman() =>
        Create(BodyKind.Human, Body.BaselineWeightKg, Body.BaselineFatPct, Body.BaselineMusclePct);
}

/// The eight body functions, each 0-1. Derived per call from tissues, blood and effects.
public readonly record struct Capacities(
    double Moving, double Manipulation, double Breathing, double BloodPumping,
    double Consciousness, double Sight, double Hearing, double Digestion);

/// Situation that changes abilities but is not the body: load carried and how dark it is.
public readonly record struct AbilityContext(double EncumbrancePct = 0, double DarknessLevel = 0, bool HasLightSource = false);

public sealed record DamageInfo(
    double Amount, DamageType Type = DamageType.Blunt, BodyTarget Target = BodyTarget.Random,
    double ArmorCushioningLevel = 0, double ArmorToughnessLevel = 0);

public sealed class DamageResult
{
    public string HitRegion { get; set; } = "";
    public string? OrganHit { get; set; }
    public double DamageAbsorbed { get; set; }
    public double RegionConditionBefore { get; set; }
    public double RegionConditionAfter { get; set; }
    public double TotalDamageDealt => RegionConditionBefore - RegionConditionAfter;
    public List<(string Tissue, double ConditionLost)> TissuesDamaged { get; } = [];
    /// Already applied to the body; listed so the caller can narrate them.
    public List<(EffectKind Kind, double Severity)> TriggeredEffects { get; } = [];
}

public static partial class Body
{
    public const double BaselineWeightKg = 75, BaselineFatPct = 0.15, BaselineMusclePct = 0.30;
    public const double BloodVolumeMl = 5000;

    // ---- body plans ----

    static Region NewRegion(string name, double coverage, double boneToughness = 3) => new()
    {
        Name = name, Coverage = coverage,
        Skin = new Tissue { Name = "Skin", Kind = TissueKind.Skin },
        Muscle = new Tissue { Name = "Muscle", Kind = TissueKind.Muscle },
        Bone = new Tissue { Name = "Bone", Kind = TissueKind.Bone, Toughness = boneToughness },
    };

    static Tissue Organ(string name, double toughness, Capacity? provides, double amount, bool external = false) =>
        new() { Name = name, Kind = TissueKind.Organ, Toughness = toughness, Provides = provides, ProvidesAmount = amount, IsExternal = external };

    static List<Tissue> HeadOrgans(double sightEach, double hearingEach) =>
    [
        Organ("Brain", 0.25, Capacity.Consciousness, 1.0),
        Organ("Left Eye", 0.5, Capacity.Sight, sightEach, external: true),
        Organ("Right Eye", 0.5, Capacity.Sight, sightEach, external: true),
        Organ("Left Ear", 1.0, Capacity.Hearing, hearingEach, external: true),
        Organ("Right Ear", 1.0, Capacity.Hearing, hearingEach, external: true),
    ];

    static List<Tissue> HeartAndLungs() =>
    [
        Organ("Heart", 5.0, Capacity.BloodPumping, 1.0),
        Organ("Left Lung", 3.0, Capacity.Breathing, 0.5),
        Organ("Right Lung", 3.0, Capacity.Breathing, 0.5),
    ];

    static List<Tissue> Gut() => [Organ("Liver", 4.0, Capacity.Digestion, 0.6), Organ("Stomach", 3.0, Capacity.Digestion, 0.4)];

    internal static List<Region> HumanRegions()
    {
        var head = NewRegion("Head", 10, boneToughness: 20);
        head.Organs.AddRange(HeadOrgans(0.5, 0.5));
        var chest = NewRegion("Chest", 25, boneToughness: 12);
        chest.Organs.AddRange(HeartAndLungs());
        var abdomen = NewRegion("Abdomen", 20);
        abdomen.Organs.AddRange(Gut());
        // Kidneys supply no capacity yet, but they are still something a blade can reach.
        abdomen.Organs.Add(Organ("Left Kidney", 4.0, null, 0));
        abdomen.Organs.Add(Organ("Right Kidney", 4.0, null, 0));
        return [head, chest, abdomen,
            NewRegion("Left Arm", 10), NewRegion("Right Arm", 10),
            NewRegion("Left Leg", 12.5), NewRegion("Right Leg", 12.5)];
    }

    internal static List<Region> QuadrupedRegions()
    {
        var head = NewRegion("Head", 15, boneToughness: 15);
        head.Organs.AddRange(HeadOrgans(0.6, 0.6));
        var torso = NewRegion("Torso", 35);
        torso.Organs.AddRange(HeartAndLungs());
        torso.Organs.AddRange(Gut());
        return [head, torso,
            NewRegion("Front Left Leg", 12.5), NewRegion("Front Right Leg", 12.5),
            NewRegion("Rear Left Leg", 12.5), NewRegion("Rear Right Leg", 12.5)];
    }

    // ---- capacities ----

    const int CapacityCount = 8;

    public static Capacities GetCapacities(BodyState body) => GetCapacities(body, Effects.CapacityDeltas(body));

    /// Capacities from tissue alone, as digestion uses: being hungry does not slow absorbing food.
    static Capacities GetCapacitiesWithoutEffects(BodyState body) => GetCapacities(body, new double[CapacityCount]);

    static Capacities GetCapacities(BodyState body, double[] effectDeltas)
    {
        var regions = body.Regions.Select(r => (r.Name, Caps: RegionCapacities(r))).ToList();
        var c = new double[CapacityCount];

        // Moving: 90% legs, 10% everything else. Legs are found by name so four-legged bodies count too.
        var legs = regions.Where(r => r.Name.Contains("Leg")).ToList();
        var others = regions.Where(r => !r.Name.Contains("Leg")).ToList();
        c[(int)Capacity.Moving] = legs.Count == 0
            ? regions.Average(r => r.Caps[(int)Capacity.Moving])
            : legs.Sum(r => r.Caps[(int)Capacity.Moving] * 0.9 / legs.Count)
              + others.Sum(r => r.Caps[(int)Capacity.Moving] * 0.1 / others.Count);

        // Manipulation: 90% arms, 2% each from the other human regions.
        double ManipWeight(string name) => name switch
        {
            "Left Arm" or "Right Arm" => 0.45,
            "Head" or "Chest" or "Abdomen" or "Left Leg" or "Right Leg" => 0.02,
            _ => 0,
        };
        c[(int)Capacity.Manipulation] = regions.Sum(r => r.Caps[(int)Capacity.Manipulation] * ManipWeight(r.Name));

        // Everything else: organs add up wherever they sit.
        foreach (var cap in new[] { Capacity.Breathing, Capacity.BloodPumping, Capacity.Consciousness, Capacity.Sight, Capacity.Hearing, Capacity.Digestion })
            foreach (var (_, caps) in regions)
                c[(int)cap] = Clamp01(c[(int)cap] + caps[(int)cap]);

        for (int i = 0; i < CapacityCount; i++)
            c[i] = Clamp01(Clamp01(c[i]) + Math.Clamp(effectDeltas[i], -1, 1));

        // The heart pumps; blood is what gets pumped.
        c[(int)Capacity.BloodPumping] *= body.BloodPct;

        // Cascades. Only real blood loss collapses circulation, not cold-slowed pumping.
        if (body.BloodPct < 1.0)
        {
            double circulation = Math.Max(0, (body.BloodPct - 0.5) / 0.5);
            c[(int)Capacity.Consciousness] *= circulation;
            c[(int)Capacity.Moving] *= circulation;
            c[(int)Capacity.Manipulation] *= circulation;
        }
        if (c[(int)Capacity.Breathing] < 0.3)
            c[(int)Capacity.Consciousness] *= c[(int)Capacity.Breathing] / 0.3;
        if (c[(int)Capacity.Consciousness] < 0.1)
        {
            c[(int)Capacity.Moving] *= 0.1;
            c[(int)Capacity.Manipulation] *= 0.1;
        }

        return new Capacities(c[0], c[1], c[2], c[3], c[4], c[5], c[6], c[7]);
    }

    /// One region's contribution: what its organs and muscle supply, scaled by the average
    /// condition multiplier of every tissue in it.
    public static double[] RegionCapacities(Region region)
    {
        var result = new double[CapacityCount];
        if (region.IsDestroyed) return result;

        foreach (var organ in region.Organs)
            if (organ.Provides is Capacity p)
                result[(int)p] = Clamp01(result[(int)p] + organ.ProvidesAmount);
        result[(int)Capacity.Moving] = Clamp01(result[(int)Capacity.Moving] + 1);       // muscle
        result[(int)Capacity.Manipulation] = Clamp01(result[(int)Capacity.Manipulation] + 1);

        var parts = region.Layers.Concat(region.Organs).ToList();
        for (int i = 0; i < CapacityCount; i++)
            result[i] = Clamp01(result[i] * parts.Average(t => ConditionMultiplier(t, (Capacity)i)));

        // A destroyed organ that supplied all of a capacity takes it to zero, averaging notwithstanding.
        foreach (var organ in region.Organs)
            if (organ.Condition <= 0 && organ.Provides is Capacity p && organ.ProvidesAmount >= 1.0)
                result[(int)p] = 0;
        return result;
    }

    static double ConditionMultiplier(Tissue t, Capacity cap)
    {
        double c = t.Condition;
        return t.Kind switch
        {
            TissueKind.Skin => c,
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
            TissueKind.Organ => t.Provides == cap && t.ProvidesAmount > 0 ? c : 1.0,
            _ => throw new ArgumentOutOfRangeException(nameof(t), t.Kind, "Unknown tissue kind"),
        };
    }

    static double Clamp01(double x) => Math.Clamp(x, 0, 1);

    // ---- abilities ----

    /// How alive you are: the weakest of breathing, pumping and consciousness.
    public static double Vitality(BodyState body) => Vitality(GetCapacities(body));
    static double Vitality(Capacities c) => Math.Min(c.Breathing, Math.Min(c.BloodPumping, c.Consciousness));

    public static bool IsAlive(BodyState body) => Vitality(body) > 0;

    /// Power output from composition, scaled by vitality: dying is weak, whatever the muscles.
    public static double Strength(BodyState body)
    {
        var c = GetCapacities(body);
        double musclePct = body.MusclePct, fatPct = body.FatPct;

        double muscle = musclePct < 0.2 ? musclePct * 2.5
            : musclePct < 0.4 ? 0.5 + (musclePct - 0.2) * 1.0
            : 0.7 + (musclePct - 0.4) * 0.5;
        muscle += body.MuscleKg / 3 / 100;

        double fat = body.FatKg / 50 / 100 - (fatPct < 0.05 ? (0.05 - fatPct) * 3.0 : 0);

        return (0.30 + muscle + fat) * (0.50 + 0.50 * c.Moving) * (0.80 + 0.20 * c.Manipulation) * Vitality(c);
    }

    /// Movement rate. Strength modulates load: a strong person feels 30kg less than a weak one.
    public static double Speed(BodyState body, AbilityContext context = default)
    {
        var c = GetCapacities(body);
        double fatPct = body.FatPct;

        double muscleModifier = (body.MusclePct - BaselineMusclePct) / 1.5 + body.MuscleKg / 20 / 100;

        double fatPenalty = fatPct < 0.10 ? -0.01
            : fatPct <= BaselineFatPct ? (fatPct - 0.10) * 0.20 - 0.01
            : (fatPct - BaselineFatPct) * 1.5;
        fatPenalty += body.FatKg / 10 / 100;

        double structuralRatio = body.StructuralKg / body.WeightKg / 0.45;
        double weightEffect = -(Math.Pow(structuralRatio, 0.7) - 1.0);
        double sizeModifier = 1 - 0.03 * Math.Log2(body.WeightKg / BaselineWeightKg);

        double baseSpeed = c.Moving * (1 + muscleModifier - fatPenalty + weightEffect) * sizeModifier;

        double vitalityFactor = 0.5 + Vitality(c) * 0.5;
        double effectiveLoad = context.EncumbrancePct / (0.5 + Strength(body) * 0.5);
        double loadPenalty = Math.Max(0, (effectiveLoad - 0.5) * 0.8);
        return baseSpeed * vitalityFactor * (1 - loadPenalty);
    }

    /// Awareness. Consciousness counts twice, once through vitality: being dazed tanks it.
    /// In the dark, hearing carries what sight cannot.
    public static double Perception(BodyState body, AbilityContext context = default)
    {
        var c = GetCapacities(body);
        double darkness = context.HasLightSource ? 0 : context.DarknessLevel;
        double sightWeight = 0.5 * (1 - darkness);
        double perceived = c.Sight * (1 - darkness) * sightWeight + c.Hearing * (1 - sightWeight);
        return perceived * c.Consciousness * (0.7 + Vitality(c) * 0.3);
    }

    /// Fine motor control: can't see your hands in the dark, wet hands slip, the dying shake.
    public static double Dexterity(BodyState body, AbilityContext context = default)
    {
        var c = GetCapacities(body);
        double darknessPenalty = context.HasLightSource ? 0 : context.DarknessLevel * 0.5;
        double wetnessPenalty = Math.Max(0, (Effects.Severity(body, EffectKind.Wet) - 0.3) * 0.3);
        return c.Manipulation * (1 - darknessPenalty) * (1 - wetnessPenalty) * (0.7 + Vitality(c) * 0.3);
    }

    /// Insulation from body fat, 0-1, diminishing past normal levels - you are not a walrus.
    public static double ColdResistance(BodyState body)
    {
        double f = body.FatPct;
        double insulation = f < 0.05 ? f
            : f < 0.15 ? 0.05 + (f - 0.05)
            : f < 0.30 ? 0.15 + (f - 0.15) / 0.15 * 0.05
            : 0.20;
        return Math.Clamp(insulation, 0, 0.95);
    }

    // ---- damage ----

    /// The one way anything hurts a body. Applies armor, picks the region, penetrates the layers,
    /// and adds the processes the wound sets off (bleeding, pain, shock, dazed).
    public static DamageResult Damage(BodyState body, DamageInfo hit, Rng rng)
    {
        double amount = hit.Amount * (1 - ArmorReduction(hit));
        var result = new DamageResult();

        if (hit.Target == BodyTarget.Blood && hit.Type is DamageType.Bleed or DamageType.Internal)
        {
            double before = body.BloodPct;
            body.BloodPct = Math.Max(0, body.BloodPct - amount / BloodVolumeMl);
            result.HitRegion = "Blood";
            result.TissuesDamaged.Add(("Blood", before - body.BloodPct));
            return result;
        }

        // Organs can only be aimed at by internal damage, or armor could be bypassed by naming one.
        Tissue? organ = hit.Type == DamageType.Internal ? TargetOrgan(body, hit.Target, rng) : null;
        Region region = organ != null
            ? body.Regions.First(r => r.Organs.Contains(organ))
            : TargetRegion(body, hit.Target, rng) ?? RandomRegionByCoverage(body, rng);

        result.HitRegion = region.Name;
        result.RegionConditionBefore = region.Condition;
        bool wasDestroyed = region.IsDestroyed;

        if (organ != null)
        {
            result.OrganHit = organ.Name;
            result.TissuesDamaged.Add((organ.Name, DamageTissue(organ, amount, hit.Type)));
        }
        else
        {
            DamageRegion(region, amount, hit.Type, result, rng);
        }
        result.RegionConditionAfter = region.Condition;

        void Trigger(EffectKind kind, double severity)
        {
            Effects.Add(body, kind, severity);
            result.TriggeredEffects.Add((kind, severity));
        }

        // Severed vessels: fatal without treatment.
        if (!wasDestroyed && region.IsDestroyed) Trigger(EffectKind.Bleeding, 1.0);

        if (hit.Type is DamageType.Sharp or DamageType.Pierce
            && result.TissuesDamaged.Any(t => t.Tissue == "Skin" && t.ConditionLost > 0.03))
            Trigger(EffectKind.Bleeding, Math.Clamp(result.TotalDamageDealt * 0.5, 0.15, 1.0));

        if (hit.Type == DamageType.Blunt)
        {
            double tissueLost = result.TissuesDamaged.Sum(t => t.ConditionLost);
            if (tissueLost > 0.20) Trigger(EffectKind.Bleeding, Math.Clamp(tissueLost * 1.5, 0.2, 0.8));
        }

        if (result.TotalDamageDealt > 0.25)
            Trigger(EffectKind.Shock, Math.Clamp((result.TotalDamageDealt - 0.25) * 2.0, 0.2, 0.8));

        if (hit.Type is DamageType.Blunt or DamageType.Sharp or DamageType.Pierce or DamageType.Burn
            && result.TotalDamageDealt > 0.02)
            Trigger(EffectKind.Pain, Math.Clamp(result.TotalDamageDealt, 0.1, 0.8));

        if (hit.Type == DamageType.Blunt && region.Name == "Head" && result.TotalDamageDealt > 0.08)
            Trigger(EffectKind.Dazed, Math.Clamp(result.TotalDamageDealt * 1.5, 0.2, 0.8));

        return result;
    }

    /// Cushioning stops blunt, toughness stops edges; at most 80%. Internal harm, blood loss, burns
    /// and poison go straight through.
    static double ArmorReduction(DamageInfo hit)
    {
        if (hit.Type is DamageType.Internal or DamageType.Bleed or DamageType.Burn or DamageType.Poison) return 0;
        double reduction = hit.Type switch
        {
            DamageType.Blunt => hit.ArmorCushioningLevel,
            DamageType.Sharp => hit.ArmorToughnessLevel,
            DamageType.Pierce => hit.ArmorToughnessLevel * 0.7,
            _ => (hit.ArmorCushioningLevel + hit.ArmorToughnessLevel) / 2,
        };
        return Math.Clamp(reduction, 0, 0.8);
    }

    static void DamageRegion(Region region, double amount, DamageType type, DamageResult result, Rng rng)
    {
        double remaining = amount;
        if (type != DamageType.Internal)
        {
            // Each layer soaks up to 70% of what reaches it, limited by how tough it still is.
            foreach (var layer in region.Layers)
            {
                if (remaining <= 0) break;
                double absorbed = Math.Min(remaining * 0.7, Protection(layer, type));
                if (absorbed > 0)
                {
                    double lost = DamageTissue(layer, absorbed, type);
                    result.DamageAbsorbed += absorbed;
                    if (lost > 0) result.TissuesDamaged.Add((layer.Name, lost));
                }
                remaining -= absorbed;
            }
        }
        if (remaining <= 0) return;

        var organ = OrganToHit(region, amount, rng);
        if (organ != null)
        {
            result.OrganHit = organ.Name;
            result.TissuesDamaged.Add((organ.Name, DamageTissue(organ, remaining, type)));
        }
        else
        {
            double lost = DamageTissue(region.Muscle, remaining * 0.5, type);
            if (lost > 0) result.TissuesDamaged.Add((region.Muscle.Name, lost));
        }
    }

    /// Eyes and ears are hit by anything that gets through; internal organs need a deep wound.
    static Tissue? OrganToHit(Region region, double amount, Rng rng)
    {
        var external = region.Organs.Where(o => o.IsExternal).ToList();
        if (external.Count > 0 && amount > 0) return external[rng.NextInt(external.Count)];
        var inner = region.Organs.Where(o => !o.IsExternal).ToList();
        // ponytail: threshold 5 predates the 0-1 damage scale, so in practice only huge hits reach inner organs.
        if (inner.Count > 0 && amount > 5) return inner[rng.NextInt(inner.Count)];
        return null;
    }

    /// Returns the condition lost.
    static double DamageTissue(Tissue tissue, double amount, DamageType type)
    {
        if (type != DamageType.Internal) amount -= NaturalAbsorption(tissue, type);
        if (amount <= 0) return 0;
        double before = tissue.Condition;
        tissue.Condition = Math.Max(0, before - amount / Protection(tissue, type));
        return before - tissue.Condition;
    }

    /// Protection a tissue gives, and the divisor for damage it takes: toughness, worn down by damage.
    public static double Protection(Tissue tissue, DamageType type) => tissue.Toughness * tissue.Condition;

    /// What a tissue shrugs off before it loses any condition. Organs, being inside, shrug off more.
    public static double NaturalAbsorption(Tissue tissue, DamageType type)
    {
        double threshold = tissue.Name switch
        {
            "Heart" => 0.08,
            "Brain" => 0.06,
            "Left Lung" or "Right Lung" => 0.10,
            "Liver" => 0.05,
            _ => 0.01,
        };
        return type switch
        {
            DamageType.Blunt => threshold,
            DamageType.Sharp => threshold * 0.4,
            DamageType.Pierce => threshold * 0.2,
            _ => threshold * 0.6,
        };
    }

    static Tissue? TargetOrgan(BodyState body, BodyTarget target, Rng rng)
    {
        var organs = body.Regions.SelectMany(r => r.Organs).ToList();
        string? name = target switch
        {
            BodyTarget.Heart => "Heart",
            BodyTarget.Brain => "Brain",
            BodyTarget.Lungs => rng.NextInt(2) == 0 ? "Left Lung" : "Right Lung",
            BodyTarget.Liver => "Liver",
            BodyTarget.Stomach => "Stomach",
            BodyTarget.LeftKidney => "Left Kidney",
            BodyTarget.RightKidney => "Right Kidney",
            BodyTarget.LeftEye => "Left Eye",
            BodyTarget.RightEye => "Right Eye",
            BodyTarget.LeftEar => "Left Ear",
            BodyTarget.RightEar => "Right Ear",
            _ => null,
        };
        return name == null ? null : organs.FirstOrDefault(o => o.Name == name);
    }

    /// Null when the target names nothing on this body (an arm on a wolf): the hit lands by coverage.
    static Region? TargetRegion(BodyState body, BodyTarget target, Rng rng)
    {
        Region? Pick(string contains)
        {
            var matches = body.Regions.Where(r => r.Name.Contains(contains)).ToList();
            return matches.Count > 0 ? matches[rng.NextInt(matches.Count)] : null;
        }
        string? name = target switch
        {
            BodyTarget.Head => "Head",
            BodyTarget.Chest => "Chest",
            BodyTarget.Abdomen => "Abdomen",
            BodyTarget.LeftArm => "Left Arm",
            BodyTarget.RightArm => "Right Arm",
            BodyTarget.LeftLeg => "Left Leg",
            BodyTarget.RightLeg => "Right Leg",
            _ => null,
        };
        return target switch
        {
            BodyTarget.AnyLeg => Pick("Leg"),
            BodyTarget.AnyArm => Pick("Arm"),
            _ => name == null ? null : body.Regions.FirstOrDefault(r => r.Name == name),
        };
    }

    static Region RandomRegionByCoverage(BodyState body, Rng rng)
    {
        var alive = body.Regions.Where(r => !r.IsDestroyed).ToList();
        if (alive.Count == 0) throw new InvalidOperationException("Every region is destroyed; this body should already be dead");
        double roll = rng.NextDouble() * alive.Sum(r => r.Coverage);
        foreach (var r in alive)
        {
            roll -= r.Coverage;
            if (roll < 0) return r;
        }
        return alive[^1];
    }

    // ---- healing ----

    /// Mends the worst-hurt region first: its layers, then its organs. A destroyed organ
    /// (condition 0) is gone for good and is neither mended nor counted as hurt.
    public static void Heal(BodyState body, double amount)
    {
        var worst = body.Regions
            .Select(r => (Region: r, Worst: r.Organs.Where(o => o.Condition > 0).Select(o => o.Condition).Append(r.Condition).Min()))
            .Where(x => x.Worst < 1.0)
            .OrderBy(x => x.Worst)
            .Select(x => x.Region)
            .FirstOrDefault();
        if (worst == null) return;

        foreach (var tissue in worst.Layers.Concat(worst.Organs.Where(o => o.Condition > 0)))
        {
            if (amount <= 0) return;
            if (tissue.Condition >= 1.0) continue;
            double heal = Math.Min(amount, (1.0 - tissue.Condition) * tissue.Toughness);
            tissue.Condition = Math.Min(1.0, tissue.Condition + heal / tissue.Toughness);
            amount -= heal;
        }
    }

    // ---- eating and drinking ----

    /// A damaged gut absorbs half as much at worst.
    public static void Eat(BodyState body, double kcal) =>
        body.CaloriesKcal = Math.Min(MaxCaloriesKcal, body.CaloriesKcal + kcal * (0.5 + 0.5 * GetCapacitiesWithoutEffects(body).Digestion));

    public static void Drink(BodyState body, double ml) =>
        body.HydrationMl = Math.Min(MaxHydrationMl, body.HydrationMl + ml);
}
