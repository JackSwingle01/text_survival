
using text_survival.Effects;
using text_survival.IO;
using text_survival.Items;
using text_survival.Survival;

namespace text_survival.Bodies;

/// <summary>
/// External context that the body needs to update
/// </summary>
public class SurvivalContext
{
    public double LocationTemperature;
    public double ClothingInsulation;
    public double ActivityLevel;
}

public class Body
{
    // Root part and core properties
    public readonly bool IsPlayer = false;
    public readonly List<BodyRegion> Parts;
    public double Health => CalculateOverallHealth();

    private double CalculateOverallHealth()
    {
        // simple avg for now
        double health = Parts.Average(p => p.Condition);
        health = Parts.SelectMany(p => p.Organs.Select(o => o.Condition)).ToList().Append(health).Min();
        return health;
    }


    public double MaxHealth => 1;
    public bool IsDestroyed => Health <= 0;

    public bool IsTired => Energy > 60; // can sleep for at least 1 hr

    public readonly EffectRegistry EffectRegistry;

    private readonly double _baseWeight;

    public Body(string ownerName, BodyCreationInfo stats, EffectRegistry effectRegistry)
    {
        OwnerName = ownerName;
        IsPlayer = stats.IsPlayer;
        EffectRegistry = effectRegistry;
        Parts = BodyPartFactory.CreateBody(stats.type);

        // Initialize physical composition
        BodyFat = stats.overallWeight * stats.fatPercent;
        Muscle = stats.overallWeight * stats.musclePercent;
        _baseWeight = stats.overallWeight - BodyFat - Muscle;

        BodyTemperature = 98.6;
    }

    public double BodyFat;
    public double Muscle;

    public readonly string OwnerName;
    public double BodyFatPercentage => BodyFat / Weight;
    public double MusclePercentage => Muscle / Weight;
    public double Weight => _baseWeight + BodyFat + Muscle;
    public double BodyTemperature { get; set; }

    private double CalorieStore = 1500; // 75% of MAX_CALORIES (2000), up from 50% for better early-game survival
    private double Energy = 800;
    private double Hydration = 3000;

    /// <summary>
    /// Damage application rules: 
    /// 1. Body.Damage() is the only way to apply damage 
    /// 2. Body handles all targeting resolution (string -> IBodyPart)
    /// 3. Body handles damage distribution and penetration logic
    /// 4. Effects should create Damage info and pass it here 
    /// </summary>
    public void Damage(DamageInfo damageInfo)
    {
        DamageProcessor.DamageBody(damageInfo, this);
    }


    public void Heal(HealingInfo healingInfo)
    {
        // Distribute healing across damaged parts
        if (healingInfo.TargetPart != null)
        {
            var targetPart = Parts.FirstOrDefault(p => p.Name == healingInfo.TargetPart);
            if (targetPart != null)
            {
                HealBodyPart(targetPart, healingInfo);
                return;
            }
        }

        // Heal most damaged parts first
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

        // Heal materials first, then organs
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

        // Heal organs
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

    public void Update(TimeSpan timePassed, SurvivalContext context)
    {
        var data = BundleSurvivalData();
        data.environmentalTemp = context.LocationTemperature;
        data.ColdResistance = context.ClothingInsulation;
        data.activityLevel = context.ActivityLevel;

        var result = SurvivalProcessor.Process(data, (int)timePassed.TotalMinutes, EffectRegistry.GetAll());
        UpdateBodyBasedOnResult(result);
    }


    private void UpdateBodyBasedOnResult(SurvivalProcessorResult result)
    {
        var resultData = result.Data;
        BodyTemperature = resultData.Temperature;
        CalorieStore = resultData.Calories;
        Hydration = resultData.Hydration;
        Energy = resultData.Energy;

        result.Effects.ForEach(EffectRegistry.AddEffect);

        foreach (string message in result.Messages)
        {
            string formattedMessage = message.Replace("{target}", OwnerName);
            Output.WriteLine(formattedMessage);
        }
    }

    // helper for baseline male human stats
    public static BodyCreationInfo BaselineHumanStats => new BodyCreationInfo
    {
        type = BodyTypes.Human,
        overallWeight = 75, // KG ~165 lbs
        fatPercent = .15, // pretty lean
        musclePercent = .30 // low end of athletic
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
    public bool Rest(int minutes)
    {
        var data = BundleSurvivalData();
        data.activityLevel = .5; // half metabolism
        int minutesSlept = 0;
        var result = SurvivalProcessor.Sleep(data, minutes);
        UpdateBodyBasedOnResult(result);

        // just heal once at the end
        HealingInfo healing = new HealingInfo()
        {
            Amount = minutesSlept / 10,
            Type = "natural",
            Quality = Energy <= 0 ? 1 : .7, // healing quality is better after a full night's sleep
        };
        Heal(healing);

        // Note: Calling action is responsible for updating World.Update(minutes)
        // to avoid double time updates

        return Energy <= 0;
    }
    public void Consume(FoodItem food)
    {
        CalorieStore += food.Calories;
        Hydration += food.WaterContent;

        if (food.HealthEffect != null)
        {
            Heal(food.HealthEffect);
        }
        if (food.DamageEffect != null)
        {
            Damage(food.DamageEffect);
        }
    }

    public SurvivalData BundleSurvivalData() => new SurvivalData()
    {
        Temperature = BodyTemperature,
        Calories = CalorieStore,
        Hydration = Hydration,
        Energy = Energy,
        BodyStats = GetBodyStats(),
    };

    public BodyStats GetBodyStats() => new BodyStats
    {
        BodyWeight = Weight,
        MuscleWeight = Muscle,
        FatWeight = BodyFat,
        HealthPercent = Health,
    };

}