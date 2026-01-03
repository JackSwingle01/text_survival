using text_survival.Effects;
using text_survival.Environments;
using text_survival.Environments.Features;
using text_survival.Survival;

namespace text_survival.Bodies;

public class SurvivalContext
{
    public double LocationTemperature;
    public bool IsNight;
    public double ClothingInsulation;
    public double ActivityLevel;
    public double FireProximityBonus; // Direct radiant heat from fire proximity (0-2 scale multiplied by fire heat)

    // Wetness system context
    public double OverheadCoverLevel;
    public double PrecipitationPct;
    public double WindSpeedLevel;
    public bool IsRaining;
    public bool IsSnowing;                 // Weather condition flag (light snow)
    public bool IsBlizzard;
    public double CurrentWetnessPct;  // 0-1 current wetness from effect

    // Waterproofing from resin-treated equipment (0-1 scale)
    public double WaterproofingLevel;      // Reduces wetness accumulation by this factor

    // Bloody accumulation from bleeding
    public double CurrentBleedingPct; // 0-1 from Bleeding effect
    public double CurrentBloodyPct;   // 0-1 from Bloody effect

    // Clothing thermal mass
    public double ClothingWeightKg;        // Total equipment weight for capacity calc
    public double ClothingHeatBuffer;      // Current buffer level 0-1
}

public class Body
{
    // Changed from readonly to allow deserialization
    public bool IsPlayer { get; init; } = false;
    public List<BodyRegion> Parts { get; init; } = new();
    public Blood Blood { get; init; } = new();
    public const double BASE_BODY_TEMP = 98.6;

    // Public properties backed by private fields
    public double EnergyPct => Energy / SurvivalProcessor.MAX_ENERGY_MINUTES;
    public double FullPct => CalorieStore / SurvivalProcessor.MAX_CALORIES;
    public double HydratedPct => Hydration / SurvivalProcessor.MAX_HYDRATION;
    public double WarmPct => (BodyTemperature - SurvivalProcessor.HypothermiaThreshold) / (BASE_BODY_TEMP - SurvivalProcessor.HypothermiaThreshold);

    // body properties
    public double _baseWeight;
    public double BodyFatKG;
    public double MuscleKG;
    public double BodyFatPercentage => BodyFatKG / WeightKG;
    public double MusclePercentage => MuscleKG / WeightKG;
    public double WeightKG => _baseWeight + BodyFatKG + MuscleKG;
    public double BodyTemperature;
    public double LastTemperatureDelta { get; private set; }
    public double CalorieStore = 1500;
    public double Energy = 900;
    public double Hydration = 3000;
    public double ClothingHeatBufferPct = .3;

    // Parameterless constructor for deserialization
    public Body() { }

    // Normal constructor for creation
    public Body(BodyCreationInfo stats)
    {
        IsPlayer = stats.IsPlayer;
        Parts = BodyPartFactory.CreateBody(stats.type);

        BodyFatKG = stats.overallWeight * stats.fatPercent;
        MuscleKG = stats.overallWeight * stats.musclePercent;
        _baseWeight = stats.overallWeight - BodyFatKG - MuscleKG;

        BodyTemperature = BASE_BODY_TEMP;
    }

    public void ApplySizeModifier(double modifier)
    {
        _baseWeight *= modifier;
        BodyFatKG *= modifier;
        MuscleKG *= modifier;
    }

    public DamageResult Damage(DamageInfo damageInfo)
    {
        return DamageProcessor.DamageBody(damageInfo, this);
    }

    public void Heal(HealingInfo healingInfo)
    {
        if (healingInfo.TargetPart != null)
        {
            var targetPart = Parts.FirstOrDefault(p => p.Name == healingInfo.TargetPart);
            if (targetPart != null)
            {
                HealBodyPart(targetPart, healingInfo);
                return;
            }
        }

        var damagedParts = Parts
            .Where(p => p.Condition < 1.0)
            .OrderBy(p => p.Condition)
            .ToList();

        if (damagedParts.Count > 0)
        {
            HealBodyPart(damagedParts[0], healingInfo);
        }
    }

    private static void HealBodyPart(BodyRegion part, HealingInfo healingInfo)
    {
        double healingAmount = healingInfo.Amount * healingInfo.Quality;

        var materials = new[] { part.Skin, part.Muscle, part.Bone }.Where(m => m != null);
        foreach (var material in materials)
        {
            if (material!.Condition < 1.0 && healingAmount > 0)
            {
                double heal = Math.Min(healingAmount, (1.0 - material.Condition) * material.Toughness);
                material.Condition = Math.Min(1.0, material.Condition + heal / material.Toughness);
                healingAmount -= heal;
            }
        }

        foreach (var organ in part.Organs.Where(o => o.Condition < 1.0))
        {
            if (healingAmount > 0)
            {
                double heal = Math.Min(healingAmount, (1.0 - organ.Condition) * organ.Toughness);
                organ.Condition = Math.Min(1.0, organ.Condition + heal / organ.Toughness);
                healingAmount -= heal;
            }
        }
    }

    public void ApplyResult(SurvivalProcessorResult result)
    {
        // Stats
        CalorieStore = Math.Max(0, CalorieStore + result.StatsDelta.CalorieDelta);
        Hydration = Math.Max(0, Hydration + result.StatsDelta.HydrationDelta);
        Energy = Math.Clamp(Energy + result.StatsDelta.EnergyDelta, 0, SurvivalProcessor.MAX_ENERGY_MINUTES);
        LastTemperatureDelta = result.StatsDelta.TemperatureDelta;
        BodyTemperature += result.StatsDelta.TemperatureDelta;
        ClothingHeatBufferPct = Math.Clamp(ClothingHeatBufferPct + result.ClothingHeatBufferDelta, 0, 1);

        // Body composition
        BodyFatKG = Math.Max(0, BodyFatKG - result.FatToConsume);
        MuscleKG = Math.Max(0, MuscleKG - result.MuscleToConsume);

        // Blood regeneration
        if (result.BloodHealing > 0)
        {
            Blood.Condition = Math.Min(1.0, Blood.Condition + result.BloodHealing);
        }

        // Damage
        foreach (var damage in result.DamageEvents)
        {
            Damage(damage);
        }

        // Healing
        foreach (var healing in result.HealingEvents)
        {
            Heal(healing);
        }
    }

    public void AddCalories(double calories)
    {
        var digestion = GetDigestionCapacity();
        double absorptionRate = 0.5 + (0.5 * digestion);
        CalorieStore = Math.Min(SurvivalProcessor.MAX_CALORIES, CalorieStore + calories * absorptionRate);
    }

    public void AddHydration(double ml)
    {
        Hydration = Math.Min(SurvivalProcessor.MAX_HYDRATION, Hydration + ml);
    }

    private double GetDigestionCapacity()
    {
        var capacities = CapacityCalculator.GetCapacities(this, new CapacityModifierContainer());
        return capacities.Digestion;
    }

    public bool Rest(int minutes, Location? location = null, EffectRegistry? effects = null)
    {
        var result = SurvivalProcessor.Sleep(this, minutes);
        ApplyResult(result);

        // Base quality from exhaustion state
        double quality = Energy <= 0 ? 1.0 : 0.7;

        // Bedding quality (0.3 none → 1.0 sleeping bag)
        var bedding = location?.GetFeature<BeddingFeature>();
        double beddingFactor = bedding?.Quality ?? 0.3;
        quality *= beddingFactor;

        // Wetness penalty - sleeping wet is miserable (up to -50%)
        if (effects != null)
        {
            var wetness = effects.GetEffectsByKind("Wet").FirstOrDefault()?.Severity ?? 0;
            if (wetness > 0.2)
                quality *= (1 - wetness * 0.5);
        }

        HealingInfo healing = new()
        {
            Amount = minutes / 10.0,
            Type = "natural",
            Quality = quality,
        };
        Heal(healing);

        return Energy <= 0;
    }

    public static BodyCreationInfo BaselineHumanStats => new()
    {
        type = BodyTypes.Human,
        overallWeight = 75,
        fatPercent = 0.15,
        musclePercent = 0.30
    };

    public static BodyCreationInfo BaselinePlayerStats
    {
        get
        {
            var stats = BaselineHumanStats;
            stats.IsPlayer = true;
            return stats;
        }
    }

    public double BaseColdResistance { get; } = 0;
}