// Body.cs
using text_survival.Items;
using text_survival.Survival;

namespace text_survival.Bodies;

public class SurvivalContext
{
    public double LocationTemperature;
    public double ClothingInsulation;
    public double ActivityLevel;
    public double FireProximityBonus; // Direct radiant heat from fire proximity (0-2 scale multiplied by fire heat)
}

public class Body
{
    public readonly bool IsPlayer = false;
    public readonly List<BodyRegion> Parts;
    public readonly string OwnerName;
    public Blood Blood { get; } = new();

    private double _baseWeight;

    public bool IsTired => Energy < SurvivalProcessor.MAX_ENERGY_MINUTES / 2;

    public double BodyFatKG { get; private set; }
    public double MuscleKG { get; private set; }
    public double BodyFatPercentage => BodyFatKG / WeightKG;
    public double MusclePercentage => MuscleKG / WeightKG;
    public double WeightKG => _baseWeight + BodyFatKG + MuscleKG;
    public double BodyTemperature { get; private set; }

    public double CalorieStore { get; private set; } = 1500;
    public double Energy { get; private set; } = 800;
    public double Hydration { get; private set; } = 3000;

    public Body(string ownerName, BodyCreationInfo stats)
    {
        OwnerName = ownerName;
        IsPlayer = stats.IsPlayer;
        Parts = BodyPartFactory.CreateBody(stats.type);

        BodyFatKG = stats.overallWeight * stats.fatPercent;
        MuscleKG = stats.overallWeight * stats.musclePercent;
        _baseWeight = stats.overallWeight - BodyFatKG - MuscleKG;

        BodyTemperature = 98.6;
    }

    /// <summary>
    /// Applies a size modifier to the body weight (for animal trait generation).
    /// Scales base weight, fat, and muscle proportionally.
    /// </summary>
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

    /// <summary>
    /// Applies all mutations from a SurvivalProcessorResult.
    /// Returns the messages for the caller to handle.
    /// </summary>
    public void ApplyResult(SurvivalProcessorResult result)
    {
        // Stats
        CalorieStore = Math.Max(0, CalorieStore + result.StatsDelta.CalorieDelta);
        Hydration = Math.Max(0, Hydration + result.StatsDelta.HydrationDelta);
        Energy = Math.Clamp(Energy + result.StatsDelta.EnergyDelta, 0, SurvivalProcessor.MAX_ENERGY_MINUTES);
        BodyTemperature += result.StatsDelta.TemperatureDelta;

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

    public void Consume(FoodItem food)
    {
        var digestion = GetDigestionCapacity();
        double absorptionRate = 0.5 + (0.5 * digestion);  // 50-100% absorption

        CalorieStore += food.Calories * absorptionRate;
        Hydration += food.WaterContent;  // Water absorption unaffected

        if (food.HealthEffect != null) Heal(food.HealthEffect);
        if (food.DamageEffect != null) Damage(food.DamageEffect);
    }

    /// <summary>
    /// Add calories directly (from eating simple foods like berries, meat).
    /// Calories per kg: Cooked meat ~2500, Raw meat ~1500, Berries ~500
    /// </summary>
    public void AddCalories(double calories)
    {
        var digestion = GetDigestionCapacity();
        double absorptionRate = 0.5 + (0.5 * digestion);
        CalorieStore = Math.Min(SurvivalProcessor.MAX_CALORIES, CalorieStore + calories * absorptionRate);
    }

    /// <summary>
    /// Add hydration directly (from drinking water). 1L = 1000ml hydration.
    /// </summary>
    public void AddHydration(double ml)
    {
        Hydration = Math.Min(SurvivalProcessor.MAX_HYDRATION, Hydration + ml);
    }

    /// <summary>
    /// Drain calories directly (from vomiting, etc).
    /// </summary>
    public void DrainCalories(double calories)
    {
        CalorieStore = Math.Max(0, CalorieStore - calories);
    }

    /// <summary>
    /// Drain hydration directly (from vomiting, blood loss, etc).
    /// </summary>
    public void DrainHydration(double ml)
    {
        Hydration = Math.Max(0, Hydration - ml);
    }

    private double GetDigestionCapacity()
    {
        var capacities = CapacityCalculator.GetCapacities(this, new CapacityModifierContainer());
        return capacities.Digestion;
    }

    public bool Rest(int minutes)
    {
        var result = SurvivalProcessor.Sleep(this, minutes);
        ApplyResult(result);

        HealingInfo healing = new()
        {
            Amount = minutes / 10.0,
            Type = "natural",
            Quality = Energy <= 0 ? 1 : 0.7,
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

    /// <summary>
    /// Restore body state from save data. Used by save/load system.
    /// </summary>
    internal void Restore(double calories, double energy, double hydration,
        double temperature, double fatKg, double muscleKg, double bloodCondition)
    {
        CalorieStore = calories;
        Energy = energy;
        Hydration = hydration;
        BodyTemperature = temperature;
        BodyFatKG = fatKg;
        MuscleKG = muscleKg;
        Blood.Condition = bloodCondition;
    }
}