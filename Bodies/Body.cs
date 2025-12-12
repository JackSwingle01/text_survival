// Body.cs
using text_survival.Effects;
using text_survival.Items;
using text_survival.Survival;

namespace text_survival.Bodies;

public class SurvivalContext
{
    public double LocationTemperature;
    public double ClothingInsulation;
    public double ActivityLevel;
}

public class Body
{
    public readonly bool IsPlayer = false;
    public readonly List<BodyRegion> Parts;
    public readonly string OwnerName;

    private readonly double _baseWeight;

    public double Health => CalculateOverallHealth();
    public double MaxHealth => 1;
    public bool IsDestroyed => Health <= 0;
    public bool IsTired => Energy < SurvivalProcessor.MAX_ENERGY_MINUTES;

    public double BodyFatKG { get; private set; }
    public double MuscleKG { get; private set; }
    public double BodyFatPercentage => BodyFatKG / Weight;
    public double MusclePercentage => MuscleKG / Weight;
    public double Weight => _baseWeight + BodyFatKG + MuscleKG;
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

    private double CalculateOverallHealth()
    {
        double health = Parts.Average(p => p.Condition);
        health = Parts.SelectMany(p => p.Organs.Select(o => o.Condition)).ToList().Append(health).Min();
        return health;
    }

    public void Damage(DamageInfo damageInfo)
    {
        DamageProcessor.DamageBody(damageInfo, this);
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
        CalorieStore += food.Calories;
        Hydration += food.WaterContent;

        if (food.HealthEffect != null) Heal(food.HealthEffect);
        if (food.DamageEffect != null) Damage(food.DamageEffect);
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
}