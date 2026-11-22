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
    /// Starvation, dehydration, cold: bypasses clothing and natural absorption, and takes condition
    /// directly, whatever the tissue's toughness or what it has already lost.
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

/// One layer or organ. Condition 0-1; Toughness is how many units of damage destroy it whole in a
/// body of the reference weight (Body.EffectiveToughness).
public sealed class Tissue
{
    public required string Name { get; init; }
    public required TissueKind Kind { get; init; }
    public double Condition { get; set; } = 1.0;
    public double Toughness { get; set; } = 1.0;
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

    /// Weakest layer; organs are excluded: they show up as vitality, not wounds.
    [JsonIgnore] public double Condition => Math.Min(Skin.Condition, Math.Min(Muscle.Condition, Bone.Condition));
    [JsonIgnore] public bool IsDestroyed => Condition <= 0;
    [JsonIgnore] public IEnumerable<Tissue> Layers => [Skin, Muscle, Bone];
}

/// Capacities and abilities are derived from this, never stored.
public sealed class BodyState
{
    public required BodyKind Kind { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public Sex Sex { get; init; }
    public required List<Region> Regions { get; init; }
    /// Of Body.BloodVolumeMl; circulation collapses at 0.5.
    public double BloodPct { get; set; } = 1.0;
    /// Bone, organs, water: everything that is neither fat nor muscle.
    public required double StructuralKg { get; set; }
    public required double FatKg { get; set; }
    public required double MuscleKg { get; set; }
    public double CoreTempF { get; set; } = Body.BaseCoreTempF;
    public double CaloriesKcal { get; set; } = 1500;
    public double EnergyMinutes { get; set; } = 900;
    public double HydrationMl { get; set; } = 3000;
    /// Minutes lived in a row without the Hungry effect, what Well fed waits on (Body.Update sees no world clock).
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public int MinutesWithoutHunger { get; set; }
    /// Warmth stored in clothing's thermal mass, 0-1 of what it can hold.
    public double ClothingHeatBufferPct { get; set; } = 0.3;
    /// Sprint reserve spent, 0 full to 1 empty (Body.SprintReservePct); refills every minute lived.
    /// The one stored value of the fight energy model; everything else is derived.
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public double SprintSpentLevel { get; set; }
    public List<BodyEffect> Effects { get; init; } = [];
    /// The last world minute this body's wounds have lived (0: none yet); a headless fight's rounds move
    /// it ahead of the clock. A minute up to this one is not lived again: every minute exactly once.
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public long WoundsLivedThroughMinute { get; set; }
    /// Who last hurt this body in a fight, and with what ("the player's wooden spear"): what an
    /// animal that bleeds out later, or a person, died of. Cleared on an animal once it has mended.
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Cause? WoundedBy { get; set; }
    /// When it died (Body.MarkDead), and of what: kept the moment the fatal damage lands (Body.Damage).
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public long? DiedMinute { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Cause? DeathCause { get; set; }
    /// Kg of flesh scavengers have picked off since it died (Remains.Pick); a picked body goes to bones sooner.
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public double PickedKg { get; set; }
    /// Clothing belongs to the body it is on.
    public List<Gear> Worn { get; init; } = [];
    [JsonIgnore] public double ClothingClo => Items.ClothingClo(Worn);
    [JsonIgnore] public double ClothingWeightKg => Items.ClothingWeightKg(Worn);

    [JsonIgnore] public double WeightKg => StructuralKg + FatKg + MuscleKg;
    [JsonIgnore] public double FatPct => FatKg / WeightKg;
    [JsonIgnore] public double MusclePct => MuscleKg / WeightKg;
    [JsonIgnore] public double EnergyPct => EnergyMinutes / Body.MaxEnergyMinutes;
    [JsonIgnore] public double CaloriesPct => CaloriesKcal / Body.CaloriesCapacityKcal(this);
    [JsonIgnore] public double HydrationPct => HydrationMl / Body.MaxHydrationMl;

    public static BodyState Create(BodyKind kind, double weightKg, double fatPct, double musclePct, Sex sex = Sex.Male) => new()
    {
        Kind = kind,
        Sex = sex,
        Regions = kind == BodyKind.Human ? Body.HumanRegions() : Body.QuadrupedRegions(),
        FatKg = weightKg * fatPct,
        MuscleKg = weightKg * musclePct,
        StructuralKg = weightKg * (1 - fatPct - musclePct),
    };

    /// A dressed adult in furs and boots: 1.33 clo, 3.05 kg.
    public static BodyState BaselineHuman(Sex sex = Sex.Male)
    {
        var row = SpeciesTable.Of(Species.Human);
        var b = sex == Sex.Female && row.Female is PersonBuild female
            ? Create(BodyKind.Human, female.WeightKg, female.FatPct, female.MusclePct, sex)
            : Create(BodyKind.Human, row.WeightKg, row.FatPct, row.MusclePct);
        b.Worn.AddRange(Items.StartingClothes());
        return b;
    }
}

public readonly record struct Capacities(
    double Moving, double Manipulation, double Breathing, double BloodPumping,
    double Consciousness, double Sight, double Hearing, double Digestion);

public readonly record struct AbilityContext(double EncumbrancePct = 0, double DarknessLevel = 0, bool HasLightSource = false);

/// By: what this damage is, as the death it causes would be named ("the cold", "the player's wooden
/// spear"). Null names nothing in particular: "its injuries".
/// Amount is in units of tissue condition (GLOSSARY.md, Damage); blood loss is in ml.
public sealed record DamageInfo(
    double Amount, DamageType Type = DamageType.Blunt, BodyTarget Target = BodyTarget.Random, Cause? By = null);

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
    /// The reference man the body's scales are measured from (the calorie store's 2,000 kcal, the
    /// reserve's six minutes): an Upper Palaeolithic man (documentation/species-stats-research.md §2.1).
    public const double BaselineWeightKg = 68, BaselineFatPct = 0.12, BaselineMusclePct = 0.38;
    public const double BloodMlPerKg = 70;
    public static double BloodVolumeMl(BodyState body) => BloodMlPerKg * body.WeightKg;


    static Region NewRegion(string name, double coverage, double boneToughness = 3) => new()
    {
        Name = name,
        Coverage = coverage,
        Skin = new Tissue { Name = "Skin", Kind = TissueKind.Skin },
        Muscle = new Tissue { Name = "Muscle", Kind = TissueKind.Muscle },
        Bone = new Tissue { Name = "Bone", Kind = TissueKind.Bone, Toughness = boneToughness },
    };

    /// An organ is far smaller than a region's whole skin: half a unit left in a spear destroys a heart.
    static Tissue Organ(string name, double toughness, Capacity? provides, double amount, bool external = false) =>
        new() { Name = name, Kind = TissueKind.Organ, Toughness = toughness, Provides = provides, ProvidesAmount = amount, IsExternal = external };

    static List<Tissue> HeadOrgans(double sightEach, double hearingEach) =>
    [
        Organ("Brain", 0.3, Capacity.Consciousness, 1.0),
        Organ("Left Eye", 0.2, Capacity.Sight, sightEach, external: true),
        Organ("Right Eye", 0.2, Capacity.Sight, sightEach, external: true),
        Organ("Left Ear", 0.3, Capacity.Hearing, hearingEach, external: true),
        Organ("Right Ear", 0.3, Capacity.Hearing, hearingEach, external: true),
    ];

    static List<Tissue> HeartAndLungs() =>
    [
        Organ("Heart", 0.5, Capacity.BloodPumping, 1.0),
        Organ("Left Lung", 0.6, Capacity.Breathing, 0.5),
        Organ("Right Lung", 0.6, Capacity.Breathing, 0.5),
    ];

    static List<Tissue> Gut() => [Organ("Liver", 0.6, Capacity.Digestion, 0.6), Organ("Stomach", 0.5, Capacity.Digestion, 0.4)];

    internal static List<Region> HumanRegions()
    {
        var head = NewRegion("Head", 10, boneToughness: 4);
        head.Organs.AddRange(HeadOrgans(0.5, 0.5));
        var chest = NewRegion("Chest", 25);
        chest.Organs.AddRange(HeartAndLungs());
        var abdomen = NewRegion("Abdomen", 20);
        abdomen.Organs.AddRange(Gut());
        // Kidneys supply no capacity yet, but they are still something a blade can reach.
        abdomen.Organs.Add(Organ("Left Kidney", 0.4, null, 0));
        abdomen.Organs.Add(Organ("Right Kidney", 0.4, null, 0));
        return [head, chest, abdomen,
            NewRegion("Left Arm", 10), NewRegion("Right Arm", 10),
            NewRegion("Left Leg", 12.5), NewRegion("Right Leg", 12.5)];
    }

    internal static List<Region> QuadrupedRegions()
    {
        var head = NewRegion("Head", 15, boneToughness: 4);
        head.Organs.AddRange(HeadOrgans(0.6, 0.6));
        var torso = NewRegion("Torso", 35);
        torso.Organs.AddRange(HeartAndLungs());
        torso.Organs.AddRange(Gut());
        return [head, torso,
            NewRegion("Front Left Leg", 12.5), NewRegion("Front Right Leg", 12.5),
            NewRegion("Rear Left Leg", 12.5), NewRegion("Rear Right Leg", 12.5)];
    }

    const int CapacityCount = 8;

    public static Capacities GetCapacities(BodyState body) => GetCapacities(body, Effects.CapacityDeltas(body));

    /// Tissue and blood alone, as digestion and life use: hunger does not slow absorbing food, and no effect stops a heart.
    static Capacities GetCapacitiesWithoutEffects(BodyState body) => GetCapacities(body, new double[CapacityCount]);

    static Capacities GetCapacities(BodyState body, double[] effectDeltas)
    {
        var c = IsWhole(body) ? (double[])WholeTissueCapacities[(int)body.Kind].Clone() : TissueCapacities(body);

        for (int i = 0; i < CapacityCount; i++)
            c[i] = Clamp01(Clamp01(c[i]) + Math.Clamp(effectDeltas[i], -1, 1));

        c[(int)Capacity.BloodPumping] *= body.BloodPct;

        // Cascades. Only real blood loss collapses circulation, not cold-slowed pumping.
        if (body.BloodPct < 1.0)
        {
            double circulation = Math.Max(0, (body.BloodPct - CirculationFailsBloodPct) / (1 - CirculationFailsBloodPct));
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

    /// Every tissue and organ whole, so WholeTissueCapacities applies: worked out once, for most bodies most of the time.
    static bool IsWhole(BodyState body)
    {
        foreach (var r in body.Regions)
        {
            if (r.Skin.Condition != 1 || r.Muscle.Condition != 1 || r.Bone.Condition != 1) return false;
            foreach (var o in r.Organs) if (o.Condition != 1) return false;
        }
        return true;
    }

    static readonly double[][] WholeTissueCapacities =
        [.. Enum.GetValues<BodyKind>().Select(k => TissueCapacities(BodyState.Create(k, BaselineWeightKg, BaselineFatPct, BaselineMusclePct)))];

    static double[] TissueCapacities(BodyState body)
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

        foreach (var cap in new[] { Capacity.Breathing, Capacity.BloodPumping, Capacity.Consciousness, Capacity.Sight, Capacity.Hearing, Capacity.Digestion })
            foreach (var (_, caps) in regions)
                c[(int)cap] = Clamp01(c[(int)cap] + caps[(int)cap]);
        return c;
    }

    /// Organs supply in proportion to their condition, plus the muscle's work, limited by the region's
    /// weakest layer. Not an average over tissues: that leaves two-thirds of breathing with both lungs destroyed.
    public static double[] RegionCapacities(Region region)
    {
        var result = new double[CapacityCount];
        if (region.IsDestroyed) return result;

        foreach (var organ in region.Organs)
            if (organ.Provides is Capacity p)
                result[(int)p] += organ.ProvidesAmount * organ.Condition;
        result[(int)Capacity.Moving] += 1;       // muscle
        result[(int)Capacity.Manipulation] += 1;

        for (int i = 0; i < CapacityCount; i++)
            result[i] = Clamp01(result[i]) * region.Layers.Min(t => LayerMultiplier(t, (Capacity)i));
        return result;
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

    static double Clamp01(double x) => Math.Clamp(x, 0, 1);

    public static double Vitality(BodyState body) => Vitality(GetCapacities(body));
    static double Vitality(Capacities c) => Math.Min(c.Breathing, Math.Min(c.BloodPumping, c.Consciousness));

    /// Life is tissue and blood: breathing, a pumping heart and a working brain, before any effect.
    /// Effects disable (consciousness can fall to nothing and leave you unable to act) but never kill.
    public static bool IsAlive(BodyState body) => TissueVitality(body) > 0;

    /// Vitality from tissue and blood alone: the harm that kills, not the hunger or pain that only disables.
    public static double TissueVitality(BodyState body) => Vitality(GetCapacitiesWithoutEffects(body));

    public const double CirculationFailsBloodPct = 0.5;

    /// Keeps the time, and the cause only if no damage already named one (Body.Damage). A body dies once.
    public static void MarkDead(BodyState body, long minute, Cause cause)
    {
        if (body.DiedMinute != null) return;
        body.DiedMinute = minute;
        body.DeathCause ??= cause;
    }

    public static readonly Cause UnnamedInjuries = new(null, "its injuries");

    /// Kept as the fatal damage lands (DamageInfo.By), not read off the dead body afterwards, so a
    /// person who freezes days after a wolf bite died of the cold.
    public static Cause CauseOfDeath(BodyState body)
    {
        if (IsAlive(body)) throw new InvalidOperationException("This body is alive: it has no cause of death");
        return body.DeathCause ?? UnnamedInjuries; // made dead by setting its state, not by damage: nothing to name
    }

    /// Whole again: every layer, every organ not lost for good, and the blood. Whoever hurt it no longer counts.
    public static bool IsMended(BodyState body)
    {
        if (body.BloodPct < 1) return false;
        foreach (var r in body.Regions)
        {
            if (r.Condition < 1) return false;
            foreach (var o in r.Organs) if (o.Condition is > 0 and < 1) return false;
        }
        return true;
    }

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

    /// The build Speed's numbers were tuned around; not the baseline man, so moving that does not re-tune it.
    const double SpeedReferenceWeightKg = 75, SpeedReferenceFatPct = 0.15, SpeedReferenceMusclePct = 0.30;

    /// Movement rate. Strength modulates load: a strong person feels 30kg less than a weak one.
    public static double Speed(BodyState body, AbilityContext context = default)
    {
        var c = GetCapacities(body);
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

        double vitalityFactor = 0.5 + Vitality(c) * 0.5;
        double effectiveLoad = context.EncumbrancePct / (0.5 + Strength(body) * 0.5);
        double loadPenalty = Math.Max(0, (effectiveLoad - 0.5) * 0.8);
        return baseSpeed * vitalityFactor * (1 - loadPenalty);
    }

    /// Consciousness counts twice, once through vitality: being dazed tanks it. Sight and hearing are
    /// half each, so the healthy perceive half as much in pitch dark as at noon.
    public static double Perception(BodyState body, AbilityContext context = default)
    {
        var c = GetCapacities(body);
        double darkness = context.HasLightSource ? 0 : context.DarknessLevel;
        double perceived = c.Sight * 0.5 * (1 - darkness) + c.Hearing * 0.5;
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

    public static IReadOnlyList<string> DexterityCosts(BodyState body, AbilityContext context = default)
    {
        var c = GetCapacities(body);
        var costs = new List<string>();
        if (c.Manipulation < 1) costs.Add("hurt or cold hands");
        if (!context.HasLightSource && context.DarknessLevel > 0) costs.Add("dark");
        if (Effects.Severity(body, EffectKind.Wet) > 0.3) costs.Add("wet");
        if (Vitality(c) < 1) costs.Add("weak");
        return costs;
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

    /// The one way anything hurts a body. Damage is the only thing that kills (IsAlive), so the damage
    /// that takes a body over the line names its death.
    public static DamageResult Damage(BodyState body, DamageInfo hit, Rng rng)
    {
        bool wasAlive = body.DeathCause == null && IsAlive(body);
        var result = Wound(body, hit, rng);
        if (wasAlive && !IsAlive(body)) body.DeathCause = hit.By ?? UnnamedInjuries;
        return result;
    }

    static DamageResult Wound(BodyState body, DamageInfo hit, Rng rng)
    {
        var result = new DamageResult();

        if (hit.Target == BodyTarget.Blood && hit.Type is DamageType.Bleed or DamageType.Internal)
        {
            double before = body.BloodPct;
            body.BloodPct = Math.Max(0, body.BloodPct - hit.Amount / BloodVolumeMl(body));
            result.HitRegion = "Blood";
            result.TissuesDamaged.Add(("Blood", before - body.BloodPct));
            return result;
        }

        // Organs can only be aimed at by internal damage, or clothing could be bypassed by naming one.
        Tissue? organ = hit.Type == DamageType.Internal ? TargetOrgan(body, hit.Target, rng) : null;
        Region region = organ != null
            ? body.Regions.First(r => r.Organs.Contains(organ))
            : TargetRegion(body, hit.Target, rng) ?? RandomRegionByCoverage(body, rng);
        double amount = hit.Amount * (1 - ClothingReduction(body, region, hit.Type));

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
            DamageRegion(region, amount, hit.Type, SizeFactor(body), result, rng);
        }
        result.RegionConditionAfter = region.Condition;

        void Trigger(EffectKind kind, double severity)
        {
            Effects.Add(body, kind, severity);
            result.TriggeredEffects.Add((kind, severity));
        }

        // Severed vessels: fatal without treatment.
        if (!wasDestroyed && region.IsDestroyed) Trigger(EffectKind.Bleeding, 1.0);
        else if (hit.Type is DamageType.Blunt or DamageType.Sharp or DamageType.Pierce && WoundBleeding(region, hit.Type, result) is double bleeding and > 0)
            Trigger(EffectKind.Bleeding, bleeding);

        if (result.TotalDamageDealt > 0.25)
            Trigger(EffectKind.Shock, Math.Clamp((result.TotalDamageDealt - 0.25) * 2.0, 0.2, 0.8));

        if (hit.Type is DamageType.Blunt or DamageType.Sharp or DamageType.Pierce or DamageType.Burn
            && result.TotalDamageDealt > 0.02)
        {
            Trigger(EffectKind.Pain, Math.Clamp(result.TotalDamageDealt, 0.1, 0.8));
            Wake(body); // nobody sleeps through a blow that hurts
        }

        if (hit.Type == DamageType.Blunt && region.Name == "Head" && result.TotalDamageDealt > 0.08)
            Trigger(EffectKind.Dazed, Math.Clamp(result.TotalDamageDealt * 1.5, 0.2, 0.8));

        return result;
    }

    /// A wound bleeds from what it destroyed, deeper tissue more: a liver torn under whole skin bleeds.
    /// A club bruises flesh without opening it, so only what it breaks inside bleeds.
    static double WoundBleeding(Region region, DamageType type, DamageResult result)
    {
        double severity = 0;
        foreach (var (name, lost) in result.TissuesDamaged)
        {
            var tissue = region.Layers.Concat(region.Organs).First(t => t.Name == name);
            bool bruised = type == DamageType.Blunt && tissue.Kind is TissueKind.Skin or TissueKind.Muscle;
            severity += bruised ? 0 : lost * (tissue.IsExternal ? BleedingPerSkinLost : tissue.Kind switch
            {
                TissueKind.Skin => BleedingPerSkinLost,
                TissueKind.Muscle => BleedingPerMuscleLost,
                TissueKind.Bone => BleedingPerBoneLost,
                _ => BleedingPerOrganLost,
            });
        }
        return Math.Min(1, severity);
    }

    const double BleedingPerSkinLost = 0.1, BleedingPerMuscleLost = 0.3, BleedingPerBoneLost = 0.3, BleedingPerOrganLost = 0.8;

    /// What is worn over the region hit: cushioning stops blunt, toughness edges, at most 80%.
    static double ClothingReduction(BodyState body, Region region, DamageType type)
    {
        if (type is DamageType.Internal or DamageType.Bleed or DamageType.Burn or DamageType.Poison) return 0;
        double cushioning = 0, toughness = 0;
        foreach (var g in body.Worn)
        {
            var def = Items.Def(g);
            if (def.Slot is not EquipSlot slot || !Covers(slot, region)) continue;
            cushioning += def.Cushioning;
            toughness += def.Toughness;
        }
        double reduction = type switch
        {
            DamageType.Blunt => cushioning,
            DamageType.Sharp => toughness,
            DamageType.Pierce => toughness * 0.7,
            _ => (cushioning + toughness) / 2,
        };
        return Math.Clamp(reduction, 0, 0.8);
    }

    static bool Covers(EquipSlot slot, Region region) => slot switch
    {
        EquipSlot.Head => region.Name == "Head",
        EquipSlot.Chest => region.Name is "Chest" or "Abdomen",
        EquipSlot.Legs or EquipSlot.Feet => region.Name.Contains("Leg"),
        EquipSlot.Hands => region.Name.Contains("Arm"),
        _ => throw new ArgumentOutOfRangeException(nameof(slot), slot, null),
    };

    /// What passes all three layers reaches an organ, if the region has one, or goes out the far side.
    static void DamageRegion(Region region, double amount, DamageType type, double size, DamageResult result, Rng rng)
    {
        double remaining = amount;
        if (type != DamageType.Internal)
            foreach (var layer in region.Layers)
            {
                if (remaining <= 0) return;
                remaining = Take(layer, remaining, type, size, result);
            }
        if (remaining <= 0) return;

        var organ = OrganToHit(region, rng);
        if (organ == null)
        {
            if (type == DamageType.Internal) throw new ArgumentException($"Internal harm to {region.Name}, which has no organ to take it");
            return;
        }
        result.OrganHit = organ.Name;
        if (type == DamageType.Internal) result.TissuesDamaged.Add((organ.Name, DamageTissue(organ, remaining, type)));
        else Take(organ, remaining, type, size, result);
    }

    /// A tissue stops up to its EffectiveToughness of one blow and passes the rest on: a narrow blow
    /// destroys a layer only over several blows.
    static double Take(Tissue tissue, double amount, DamageType type, double size, DamageResult result)
    {
        double shrugged = tissue.Condition > 0 ? Math.Min(amount, NaturalAbsorption(tissue, type)) : 0;
        double taken = Math.Min(amount - shrugged, Math.Min(EffectiveToughness(tissue, type, size), tissue.Toughness * size * tissue.Condition));
        result.DamageAbsorbed += shrugged + taken;
        if (taken > 0)
        {
            double before = tissue.Condition;
            tissue.Condition = Math.Max(0, before - taken / (tissue.Toughness * size));
            result.TissuesDamaged.Add((tissue.Name, before - tissue.Condition));
        }
        return amount - shrugged - taken;
    }

    static Tissue? OrganToHit(Region region, Rng rng)
    {
        var external = region.Organs.Where(o => o.IsExternal).ToList();
        var inner = region.Organs.Where(o => !o.IsExternal).ToList();
        if (external.Count > 0 && (inner.Count == 0 || rng.NextDouble() < ExternalOrganPct))
            return external[rng.NextInt(external.Count)];
        return inner.Count > 0 ? inner[rng.NextInt(inner.Count)] : null;
    }

    const double ExternalOrganPct = 0.3;

    /// Internal harm (hunger, thirst, cold) takes condition directly, so its rates are what they say.
    static double DamageTissue(Tissue tissue, double amount, DamageType type)
    {
        if (type != DamageType.Internal) throw new ArgumentException("Only internal harm reaches a tissue directly", nameof(type));
        double before = tissue.Condition;
        tissue.Condition = Math.Max(0, before - amount);
        return before - tissue.Condition;
    }

    /// How much of a blow a whole tissue can stop: its toughness times how much of it the blow meets.
    /// A point goes deep; soft tissue gives little to a club, which breaks the bone behind.
    public static double EffectiveToughness(Tissue tissue, DamageType type, double size) =>
        tissue.Toughness * size * (tissue.Kind, type) switch
        {
            (TissueKind.Skin or TissueKind.Muscle, DamageType.Pierce) => 0.4,
            (TissueKind.Skin or TissueKind.Muscle, DamageType.Sharp) => 0.6,
            (TissueKind.Skin or TissueKind.Muscle, DamageType.Blunt) => 0.3,
            (TissueKind.Bone, DamageType.Pierce) => 0.3,
            (TissueKind.Bone, DamageType.Sharp) => 0.5,
            _ => 1.0,
        };

    /// Thicker hide, deeper muscle, heavier bone: toughness grows as the cube root of weight.
    public static double SizeFactor(BodyState body) => Math.Cbrt(body.WeightKg / BaselineWeightKg);

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

    /// Worst-hurt region first. A destroyed organ (condition 0) is gone for good: neither mended nor counted as hurt.
    public static void Heal(BodyState body, double amount)
    {
        var worst = body.Regions
            .Select(r => (Region: r, Worst: r.Organs.Where(o => o.Condition > 0).Select(o => o.Condition).Append(r.Condition).Min()))
            .Where(x => x.Worst < 1.0)
            .OrderBy(x => x.Worst)
            .Select(x => x.Region)
            .FirstOrDefault();
        if (worst == null) return;

        double size = SizeFactor(body);
        foreach (var tissue in worst.Layers.Concat(worst.Organs.Where(o => o.Condition > 0)))
        {
            if (amount <= 0) return;
            if (tissue.Condition >= 1.0) continue;
            double heal = Math.Min(amount, (1.0 - tissue.Condition) * tissue.Toughness * size);
            tissue.Condition = Math.Min(1.0, tissue.Condition + heal / (tissue.Toughness * size));
            amount -= heal;
        }
    }

    /// A damaged gut absorbs half as much at worst. What the store cannot hold is laid down as fat:
    /// every kilocalorie absorbed is either in the store or on the body.
    public static void Eat(BodyState body, double kcal)
    {
        double absorbed = kcal * (0.5 + 0.5 * GetCapacitiesWithoutEffects(body).Digestion);
        double stored = body.CaloriesKcal + absorbed;
        double capacity = CaloriesCapacityKcal(body);
        body.CaloriesKcal = Math.Min(capacity, stored);
        body.FatKg += Math.Max(0, stored - capacity) / KcalPerKgFat;
    }

    public static void Drink(BodyState body, double ml) =>
        body.HydrationMl = Math.Min(MaxHydrationMl, body.HydrationMl + ml);
}
