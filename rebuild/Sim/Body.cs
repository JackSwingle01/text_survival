using System.Text.Json.Serialization;

namespace Survival;

public enum BodyKind { Human, Quadruped }

/// What an organ supplies and an effect names: the five capacities, and blood and breath, which feed them. Digestion
/// is the gut's, read by eating and healing alone, and no effect names it. Saved by number: append, never insert.
public enum Capacity { Moving, Manipulation, Breath, Blood, Consciousness, Sight, Hearing, Digestion }

public enum TissueKind { Skin, Muscle, Bone, Organ }

public enum DamageType
{
    Blunt, Sharp, Pierce, Poison,
    /// Blood loss in ml; only Blood takes it.
    Bleed,
    /// Starvation, dehydration, cold: past clothing and natural absorption, whatever the tissue's toughness.
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
    public double SleepDebtMinutes { get; set; } = 60;
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
    /// Who last hurt it in a fight, and with what: the cause if it bleeds out later. Cleared once mended.
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
    [JsonIgnore] public double CaloriesPct => CaloriesKcal / Body.CaloriesCapacityKcal(this);
    [JsonIgnore] public double HydrationPct => HydrationMl / Body.MaxHydrationMlOf(this);

    public static BodyState Create(BodyKind kind, double weightKg, double fatPct, double musclePct, Sex sex = Sex.Male) => new()
    {
        Kind = kind,
        Sex = sex,
        Regions = kind == BodyKind.Human ? Body.HumanRegions() : Body.QuadrupedRegions(),
        FatKg = weightKg * fatPct,
        MuscleKg = weightKg * musclePct,
        StructuralKg = weightKg * (1 - fatPct - musclePct),
    };

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
    /// The man the 2,000 kcal store and six-minute reserve scale from (documentation/species-stats-research.md §2.1).
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
        Organ("Heart", 0.5, Capacity.Blood, 1.0),
        Organ("Left Lung", 0.6, Capacity.Breath, 0.5),
        Organ("Right Lung", 0.6, Capacity.Breath, 0.5),
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

    public static double Strength(BodyState body) => Faculties.Strength(body);
    public static double Speed(BodyState body, AbilityContext context = default) => Faculties.Speed(body, context);
    public static double Dexterity(BodyState body, AbilityContext context = default) => Faculties.Dexterity(body, context);
    public static IReadOnlyList<CrippledBy> SpeedCrippledBy(BodyState body, AbilityContext context = default) => Faculties.SpeedCrippledBy(body, context);
    /// Why this faculty is at zero; empty when it is above. Strength never reaches zero on a living body.
    public static IReadOnlyList<CrippledBy> CrippledBy(BodyState body, Faculty faculty, AbilityContext context = default) => faculty switch
    {
        Faculty.Speed => Faculties.SpeedCrippledBy(body, context),
        Faculty.Dexterity => Faculties.DexterityCrippledBy(body),
        Faculty.Perception => Faculties.PerceptionCrippledBy(body, context),
        Faculty.Vision => Faculties.VisionCrippledBy(body),
        _ => throw new ArgumentOutOfRangeException(nameof(faculty), faculty, "no verb needs strength above zero"),
    };
    public static IReadOnlyList<string> DexterityCosts(BodyState body, AbilityContext context = default) => Faculties.DexterityCosts(body, context);
    public static double Perception(BodyState body, AbilityContext context = default) => Faculties.Perception(body, context);
    public static double Vision(BodyState body) => Faculties.Vision(body);
    public static double Vitality(BodyState body, Species species) => Survival.Vitality.Of(body, species);
    public static double Share(BodyState body, Species species, Faculty faculty) => Survival.Vitality.Share(body, species, faculty);
    public static bool IsAlive(BodyState body) => Alive.Is(body);
    public static double TissueVitality(BodyState body) => Alive.TissueVitality(body);

    public static Capacities GetCapacities(BodyState body) => Capacities.Of(body);
    public static double[] RegionCapacities(Region region) => TissueReads.RegionCapacities(region);

    /// The cause counts only if no damage already named one (Body.Damage).
    public static void MarkDead(BodyState body, long minute, Cause cause)
    {
        if (body.DiedMinute != null) return;
        body.DiedMinute = minute;
        body.DeathCause ??= cause;
    }

    public static readonly Cause UnnamedInjuries = new(null, "its injuries");

    /// Kept as the fatal damage lands, so a person who freezes days after a wolf bite died of the cold.
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

    /// The one way anything hurts or kills a body (IsAlive), so the damage over the line names its death.
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

    /// Passes on what its EffectiveToughness cannot stop, so a narrow blow destroys a layer only over several.
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
        double absorbed = kcal * (0.5 + 0.5 * TissueReads.Digestion(body));
        double stored = body.CaloriesKcal + absorbed;
        double capacity = CaloriesCapacityKcal(body);
        body.CaloriesKcal = Math.Min(capacity, stored);
        body.FatKg += Math.Max(0, stored - capacity) / KcalPerKgFat;
    }

    public static void Drink(BodyState body, double ml) =>
        body.HydrationMl = Math.Min(MaxHydrationMlOf(body), body.HydrationMl + ml);
}
