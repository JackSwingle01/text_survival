using System.ComponentModel.DataAnnotations;
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

    public bool IsTired => Exhaustion > 60; // can sleep for at least 1 hr

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

    private double CalorieStore;
    private double Exhaustion;
    private double Hydration;

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
        Exhaustion = resultData.Exhaustion;

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

    public void Describe()
    {
        Output.WriteLine("Body Health: " + (int)(Health * 100) + "%");
        Output.WriteLine("Weight: " + Math.Round(Weight * 2.2, 1) + " lbs");
        Output.WriteLine("Body Composition: " + (int)(BodyFatPercentage * 100) + "% fat, " + (int)(MusclePercentage * 100) + "% muscle");

        Output.WriteLine("\nPhysical Capabilities:");
        Output.WriteLine("- Strength: " + Math.Round(AbilityCalculator.CalculateStrength(this) * 100) + "%");
        Output.WriteLine("- Speed: " + Math.Round(AbilityCalculator.CalculateSpeed(this) * 100) + "%");
        Output.WriteLine("- Vitality: " + Math.Round(AbilityCalculator.CalculateVitality(this) * 100) + "%");
        Output.WriteLine("- Perception: " + Math.Round(AbilityCalculator.CalculatePerception(this) * 100) + "%");
        Output.WriteLine("- Cold Resistance: " + Math.Round(AbilityCalculator.CalculateColdResistance(this) * 100) + "%");

        // Show damaged parts and materials
        var damagedParts = Parts.Where(p => p.Condition < 1.0).ToList();

        if (damagedParts.Count > 0)
        {
            Output.WriteLine("\nInjuries:");
            foreach (var part in damagedParts)
            {
                DescribePartCondition(part);
            }
        }
        else
        {
            Output.WriteLine("\nNo injuries detected.");
        }

        // Show capacity impairments
        var capacities = CapacityCalculator.GetCapacities(this);
        Dictionary<string, double> systemCapacities = new Dictionary<string, double>
        {
            { "Moving", capacities.Moving },
            { "Manipulation", capacities.Manipulation },
            { "Breathing", capacities.Breathing},
            { "BloodPumping", capacities.BloodPumping },
            { "Consciousness", capacities.BloodPumping },
            { "Sight", capacities.Sight},
            { "Hearing", capacities.Hearing },
            { "Digestion", capacities.Digestion }
        };

        var impairedSystems = systemCapacities.Where(kv => kv.Value < 0.9).ToList();

        if (impairedSystems.Count > 0)
        {
            Output.WriteLine("\nSystem Impairments:");
            foreach (var system in impairedSystems)
            {
                string severity = GetImpairmentSeverity(system.Value);
                Output.WriteLine($"- {system.Key}: {severity} ({(int)(system.Value * 100)}% efficiency)");
            }
        }
    }

    private void DescribePartCondition(BodyRegion part)
    {
        Output.WriteLine($"\n{part.Name}:");

        // Describe material damage
        if (part.Skin?.Condition < 1.0)
            Output.WriteLine($"  - Skin: {GetDamageDescription(part.Skin.Condition)}");
        if (part.Muscle?.Condition < 1.0)
            Output.WriteLine($"  - Muscle: {GetDamageDescription(part.Muscle.Condition)}");
        if (part.Bone?.Condition < 1.0)
            Output.WriteLine($"  - Bone: {GetDamageDescription(part.Bone.Condition)}");

        // Describe organ damage
        foreach (var organ in part.Organs.Where(o => o.Condition < 1.0))
        {
            Output.WriteLine($"  - {organ.Name}: {GetDamageDescription(organ.Condition)}");
        }
    }

    private string GetDamageDescription(double condition)
    {
        return condition switch
        {
            <= 0 => "destroyed",
            < 0.2 => "critically damaged",
            < 0.4 => "severely damaged",
            < 0.6 => "moderately damaged",
            < 0.8 => "lightly damaged",
            _ => "slightly damaged"
        };
    }

    private string GetImpairmentSeverity(double capacity)
    {
        return capacity switch
        {
            < 0.25 => "Critical impairment",
            < 0.5 => "Severe impairment",
            < 0.75 => "Moderate impairment",
            < 0.9 => "Minor impairment",
            _ => "Normal"
        };
    }

    public void DescribeSurvivalStats() => SurvivalProcessor.Describe(BundleSurvivalData());

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
            Quality = Exhaustion <= 0 ? 1 : .7, // healing quality is better after a full night's sleep
        };
        Heal(healing);

        World.Update(minutes); // need to fix, right now we are double updating

        return Exhaustion <= 0;
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
        Exhaustion = Exhaustion,
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