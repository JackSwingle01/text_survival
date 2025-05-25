using text_survival.Effects;
using text_survival.IO;
using text_survival.Items;
using text_survival.Survival;

namespace text_survival.Bodies;

public class BodyStats
{
    public BodyPartFactory.BodyTypes type;
    public double overallWeight;
    public double fatPercent;
    public double musclePercent;
}

public class SurvivalContext
{
    public double LocationTemperature;
    public double ClothingInsulation;
    public double ActivityLevel;
}

public class Body
{
    // Root part and core properties
    private readonly BodyPart _rootPart;
    public double Health => _rootPart.Health;
    public double MaxHealth => _rootPart.MaxHealth;
    public bool IsDestroyed => _rootPart.IsDestroyed;

    public readonly EffectRegistry EffectRegistry;
    private readonly HungerModule _hungerModule;
    private readonly ThirstModule _thirstModule;
    private readonly ExhaustionModule _exhaustionModule;
    private readonly TemperatureModule _temperatureModule;

    // Physical composition
    private double _bodyFat;
    private double _muscle;
    private readonly double _baseWeight;

    public Body(string ownerName, BodyStats stats, EffectRegistry effectRegistry)
    {
        OwnerName = ownerName;
        EffectRegistry = effectRegistry;
        _rootPart = BodyPartFactory.CreateBody(stats.type, stats.overallWeight);
        _hungerModule = new HungerModule(this);
        _thirstModule = new ThirstModule();
        _exhaustionModule = new ExhaustionModule();
        _temperatureModule = new TemperatureModule(this);

        // Initialize physical composition
        _bodyFat = stats.overallWeight * stats.fatPercent;
        _muscle = stats.overallWeight * stats.musclePercent;
        _baseWeight = stats.overallWeight - _bodyFat - _muscle;

        BodyTemperature = 98.6;
    }


    // Physical composition properties
    public double BodyFat
    {
        get => _bodyFat;
        set
        {
            _bodyFat = Math.Max(value, 0);
        }
    }

    public double Muscle
    {
        get => _muscle;
        set
        {
            _muscle = Math.Max(value, 0);
        }
    }

    public readonly string OwnerName;
    public double BodyFatPercentage => _bodyFat / Weight;
    public double MusclePercentage => _muscle / Weight;
    public double Weight => _baseWeight + _bodyFat + _muscle;

    public double BodyTemperature { get; set; }

    // Forwarding methods to root part
    public BodyPart? Damage(DamageInfo damageInfo) => _rootPart.Damage(damageInfo);
    public void Heal(HealingInfo healingInfo) => _rootPart.Heal(healingInfo);

    private double GetCurrentMetabolism(double activityLevel)
    {
        // Base BMR uses the Harris-Benedict equation (simplified)
        double bmr = 370 + (21.6 * Muscle) + (6.17 * BodyFat);

        // Adjust for injuries and conditions
        double healthFactor = Health / MaxHealth;
        bmr *= 0.7 + (0.3 * healthFactor); // Injured bodies need more energy to heal

        double currentMetabolism = bmr * activityLevel;
        return currentMetabolism;
    }


    public void Update(TimeSpan timePassed, SurvivalContext context)
    {
        // Handle metabolism and energy expenditure
        double currentMetabolism = GetCurrentMetabolism(context.ActivityLevel);
        double calories = currentMetabolism / 24 * timePassed.TotalHours;

        BodyTemperature += calories / 24000;

        _hungerModule.Update(currentMetabolism);
        _thirstModule.Update();
        _temperatureModule.Update(context.LocationTemperature, context.ClothingInsulation);
        _exhaustionModule.Update();
    }

    // todo - move this to the survival manager or something

    // Calculate derived attributes
    public double CalculateStrength()
    {
        double manipulationCapacity = GetCapacity("Manipulation");
        double bloodPumping = GetCapacity("BloodPumping"); // Energy delivery

        // Base strength that everyone has
        double baseStrength = 0.30; // 30% strength from structural aspects

        // Muscle contribution with diminishing returns
        double muscleContribution;
        if (MusclePercentage < 0.2) // Below normal
            muscleContribution = MusclePercentage * 2.5; // Rapid gains when building from low muscle
        else if (MusclePercentage < 0.4) // Normal to athletic
            muscleContribution = 0.5 + (MusclePercentage - 0.2) * 1.0; // Moderate gains
        else // Athletic+
            muscleContribution = 0.7 + (MusclePercentage - 0.4) * 0.5; // Diminishing returns

        // Energy state affects strength expression
        double energyFactor = Math.Min(bloodPumping, 1.0);

        // Very low body fat impairs strength
        double fatPenalty = (BodyFatPercentage < 0.05) ? (0.05 - BodyFatPercentage) * 3.0 : 0;

        return manipulationCapacity * (baseStrength + muscleContribution * energyFactor - fatPenalty);
    }

    public double CalculateSpeed()
    {
        double movingCapacity = GetCapacity("Moving");
        double structuralWeightRatio = (_baseWeight / Weight) + (1 - 0.45); // avg 45% structure weight
        double sizeRatio = Weight / BaseLineHumanStats.overallWeight;

        double muscleBonus = Math.Min(MusclePercentage * 0.20, 0); // Up to 20% bonus from muscle

        double fatPenalty;
        // Minimal fat has no penalty, excess has increasing penalties
        if (BodyFatPercentage < 0.10)
        {
            // 10% is minimal necessary fat
            fatPenalty = -.01; // negative penalty if under 10% bf
        }
        else if (BodyFatPercentage <= BaseLineHumanStats.fatPercent)
        {
            fatPenalty = ((BodyFatPercentage - .10) * .20) - .01; // at baseline a 0% penalty
        }
        else
        {
            // Steeper penalty for excess, 1.5% reduction per 1% of fat 
            // 1.5(fat% - baselineFat%)
            // fat% => speed penalty (abs)
            // 20%  =>  8.5%, 
            // 30%  => 23.5%
            // 40%  => 38.5%
            // 50%  => 53.5%
            fatPenalty = (BodyFatPercentage - BaseLineHumanStats.fatPercent) * 1.5;
        }

        // Penalty for excess weight relative to frame
        // -(ratio^.7 - 1)
        // ratio => penalty (abs)
        // 0.1   => +0.80
        // 0.5   => +0.38
        // 0.9   => +0.07
        // 1.5   => -0.33
        // 3.0   => -1.16
        double weightEffect = -(Math.Pow(structuralWeightRatio, 0.7) - 1.0);

        // smaller creatures are faster and larger ones are slower
        // 1 - .1(Log2(sizeRatio))
        // ratio => speed (multiplier)
        // 0.1   => 1.33  
        // 0.5   => 1.10   - 1/2 size means 10% faster
        // 2.0   => 0.90  - 10% slower
        // 10.   => 0.66  - 1/3 slower
        // 50    => 0.44
        double speedSizeModifier = 1 - 0.1 * Math.Log(sizeRatio, 2);

        return movingCapacity * (1 + muscleBonus - fatPenalty + weightEffect) * speedSizeModifier;
    }
    public double CalculateVitality()
    {
        double breathing = GetCapacity("Breathing");
        double bloodPumping = GetCapacity("BloodPumping");
        double digestion = GetCapacity("Digestion");

        double organFunction = (2 * (breathing + bloodPumping) + digestion) / 5;

        // Base vitality that scales more gently with body composition
        double baseMultiplier = 0.7;  // Everyone gets 70% baseline
        double muscleContribution = MusclePercentage * 0.25;  // Up to 25% from muscle
        double fatContribution;

        // Essential fat is beneficial, excess isn't
        if (BodyFatPercentage < .10)
            fatContribution = BodyFatPercentage * 0.5;  // Fat is very important when low
        else if (BodyFatPercentage < .25)
            fatContribution = 0.05;  // Optimal fat gives 5%
        else
            fatContribution = 0.05 - (BodyFatPercentage - .25) * 0.1;  // Excess fat penalizes slightly

        double bodyComposition = baseMultiplier + muscleContribution + fatContribution;
        return organFunction * bodyComposition;
    }

    public double CalculatePerception()
    {
        double sight = GetCapacity("Sight");
        double hearing = GetCapacity("Hearing");

        return (sight + hearing) / 2;
    }

    public double CalculateColdResistance()
    {
        // Base cold resistance that everyone has
        double baseColdResistance = 0.5;
        double fatInsulation;

        if (BodyFatPercentage < 0.05)
            fatInsulation = (BodyFatPercentage / 0.05) * 0.1;  // Linear up to 5%
        else if (BodyFatPercentage < 0.15)
            fatInsulation = 0.1 + ((BodyFatPercentage - 0.05) / 0.1) * 0.15;  // From 0.1 to 0.25
        else
            fatInsulation = 0.25 + ((BodyFatPercentage - 0.15)) * 0.15;  // Diminishing returns after 15%

        return baseColdResistance + fatInsulation;
    }


    private double GetCapacity(string capacity)
    {
        var parts = GetAllParts().Where(p => p.GetCapacity(capacity) > 0);
        var values = parts.Select(p => GetEffectivePartCapacity(p, capacity)).ToList();
        if (values.Sum() <= 0) return 0;

        double result;
        // todo, see if I want to revisit the special logic here
        // if (capacity is "Moving" or "Manipulation" or "Breathing" or "Consciousness"
        // or "BloodPumping" or "Digestion" or "Eating" or "Talking")
        // {
        // }
        // else if (capacity is "Sight" or "Hearing" or "BloodFiltration")
        // {
        //     result = values.Average();
        // }
        // else
        // {
        //     result = values.Min();
        // }
        result = Math.Min(1, values.Sum());
        double bodyModifier = EffectRegistry.GetBodyCapacityModifier(capacity);
        result *= (1 + bodyModifier);
        result = Math.Max(0, result);
        return result;
    }

    private double GetEffectivePartCapacity(BodyPart part, string capacityName)
    {
        if (part.IsDestroyed) return 0;

        // Get base capacity (already includes health scaling)
        double baseCapacity = part.GetCapacity(capacityName);
        if (baseCapacity <= 0) return 0;

        // Apply effect modifiers for this part
        double modifier = EffectRegistry.GetPartCapacityModifier(capacityName, part);
        return Math.Max(0, baseCapacity * (1.0 + modifier));
    }


    // Helper to get all body parts
    public List<BodyPart> GetAllParts()
    {
        var result = new List<BodyPart>();
        CollectBodyParts(_rootPart, result);
        return result;
    }

    private void CollectBodyParts(BodyPart part, List<BodyPart> result)
    {
        result.Add(part);
        foreach (var child in part.Parts)
        {
            CollectBodyParts(child, result);
        }
    }

    public List<BodyPart>? GetPartsToNDepth(int depth)
    {
        if (depth <= 0) return null;

        var result = new List<BodyPart>();
        CollectBodyPartsToDepth(_rootPart, result, 1, depth);
        return result;
    }

    private static void CollectBodyPartsToDepth(BodyPart part, List<BodyPart> result, int currentDepth, int maxDepth)
    {
        // Add the current part
        result.Add(part);

        // If we've reached our depth limit or there are no children, return
        if (currentDepth >= maxDepth || part.Parts.Count == 0)
            return;

        // Otherwise, recursively add children
        foreach (var child in part.Parts)
        {
            CollectBodyPartsToDepth(child, result, currentDepth + 1, maxDepth);
        }
    }

    // helper for baseline male human stats
    public static BodyStats BaseLineHumanStats => new BodyStats
    {
        type = BodyPartFactory.BodyTypes.Human,
        overallWeight = 75, // KG ~165 lbs
        fatPercent = .15, // pretty lean
        musclePercent = .40 // low end of athletic
    };

    public void Describe()
    {
        // Overall body statistics
        Output.WriteLine("Body Health: " + (int)(Health / MaxHealth * 100) + "%");
        Output.WriteLine("Weight: " + Math.Round(Weight * 2.2, 1) + " lbs");
        Output.WriteLine("Body Composition: " + (int)(BodyFatPercentage * 100) + "% fat, " + (int)(MusclePercentage * 100) + "% muscle");

        // Physical capabilities
        Output.WriteLine("\nPhysical Capabilities:");
        Output.WriteLine("- Strength: " + Math.Round(CalculateStrength() * 100) + "%");
        Output.WriteLine("- Speed: " + Math.Round(CalculateSpeed() * 100) + "%");
        Output.WriteLine("- Vitality: " + Math.Round(CalculateVitality() * 100) + "%");
        Output.WriteLine("- Perception: " + Math.Round(CalculatePerception() * 100) + "%");
        Output.WriteLine("- Cold Resistance: " + Math.Round(CalculateColdResistance() * 100) + "%");

        // Display damaged body parts
        List<BodyPart> allParts = GetAllParts();
        List<BodyPart> damagedParts = allParts.Where(p => p.Health < p.MaxHealth && !p.IsDestroyed).ToList();
        List<BodyPart> destroyedParts = allParts.Where(p => p.IsDestroyed).ToList();

        if (damagedParts.Count > 0 || destroyedParts.Count > 0)
        {
            Output.WriteLine("\nInjuries:");

            // Show damaged parts
            foreach (var part in damagedParts)
            {
                part.Describe();
            }

            // Show destroyed parts
            foreach (var part in destroyedParts)
            {
                Output.WriteLine($"- {part.Name} is destroyed!");
            }
        }
        else
        {
            Output.WriteLine("\nNo injuries detected.");
        }

        // Check for any body system impairments
        Dictionary<string, double> systemCapacities = new Dictionary<string, double>
        {
            { "Moving", GetCapacity("Moving") },
            { "Manipulation", GetCapacity("Manipulation") },
            { "Breathing", GetCapacity("Breathing") },
            { "BloodPumping", GetCapacity("BloodPumping") },
            { "Consciousness", GetCapacity("Consciousness") },
            { "Sight", GetCapacity("Sight") },
            { "Hearing", GetCapacity("Hearing") },
            { "Digestion", GetCapacity("Digestion") }
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

    // Helper method to check and describe system impairments
    private void CheckAndDescribeImpairments()
    {
        Dictionary<string, double> systemCapacities = new Dictionary<string, double>
        {
            { "Moving", GetCapacity("Moving") },
            { "Manipulation", GetCapacity("Manipulation") },
            { "Breathing", GetCapacity("Breathing") },
            { "BloodPumping", GetCapacity("BloodPumping") },
            { "Consciousness", GetCapacity("Consciousness") },
            { "Sight", GetCapacity("Sight") },
            { "Hearing", GetCapacity("Hearing") },
            { "Digestion", GetCapacity("Digestion") }
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

    // Helper method to determine impairment severity description
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

    public void DescribeSurvivalStats()
    {
        Output.WriteLine("\n----------------------------------------------------\n| Survival Stats:");
        _hungerModule.Describe();
        _thirstModule.Describe();
        _exhaustionModule.Describe();
        _temperatureModule.Describe();
        Output.WriteLine("----------------------------------------------------");
    }

    public bool Rest(int minutes)
    {
        int minutesSlept = 0;
        while (minutesSlept < minutes)
        {
            _exhaustionModule.Rest(1);
            World.Update(1);
            minutesSlept++;
            if (_exhaustionModule.IsFullyRested)
            {
                break;
            }
        }
        HealingInfo healing = new HealingInfo()
        {
            Amount = minutesSlept / 10,
            Type = "natural",
            TargetPart = "Body",
            Quality = _exhaustionModule.IsFullyRested ? 1 : .7, // healing quality is better after a full night's sleep
        };

        Heal(healing);
        return _exhaustionModule.IsFullyRested;
    }
    public void Consume(FoodItem food)
    {
        _hungerModule.AddCalories(food.Calories);
        _thirstModule.AddHydration(food.WaterContent);

        if (food.HealthEffect != null)
        {
            Heal(food.HealthEffect);
        }
        if (food.DamageEffect != null)
        {
            Damage(food.DamageEffect);
        }
    }

    public void UpdateSurvivalStats(SurvivalStatsUpdate stats)
    {
        if (stats.Temperature != 0)
        {
            BodyTemperature += stats.Temperature;
        }
        if (stats.Calories != 0)
        {
            _hungerModule.AddCalories(stats.Calories);
        }
        if (stats.Hydration != 0)
        {
            _thirstModule.AddHydration(stats.Hydration);
        }
        if (stats.Exhaustion != 0)
        {
            _exhaustionModule.ModifyExhaustion(stats.Exhaustion);
        }
    }
}