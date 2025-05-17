using text_survival.Effects;

namespace text_survival.Bodies;
public class Body
{
    // Root part and core properties
    private readonly BodyPart _rootPart;
    public string Name => _rootPart.Name;
    public double Health => _rootPart.Health;
    public double MaxHealth => _rootPart.MaxHealth;
    public bool IsDestroyed => _rootPart.IsDestroyed;

    private EffectRegistry _effectRegistry;

    // Physical composition
    private double _bodyFat;
    private double _muscle;
    private double _weight;
    private readonly double _baseWeight;

    // Physical systems
    private double _coreTemperature = 98.6; // Fahrenheit
    private double _targetMetabolismRate = 2000; // Calories per day

    public Body(BodyPart rootPart, double overallWeight, double fatPercent, double musclePercent, EffectRegistry effectRegistry)
    {
        _rootPart = rootPart;
        _effectRegistry = effectRegistry;

        // Initialize physical composition
        _bodyFat = overallWeight * (fatPercent / 100);
        _muscle = overallWeight * (musclePercent / 100);
        _baseWeight = overallWeight - _bodyFat - _muscle;
        UpdateWeight();
    }

    // Physical composition properties
    public double BodyFat
    {
        get => _bodyFat;
        set
        {
            _bodyFat = Math.Max(value, 0);
            UpdateWeight();
        }
    }

    public double Muscle
    {
        get => _muscle;
        set
        {
            _muscle = Math.Max(value, 0);
            UpdateWeight();
        }
    }

    public double BodyFatPercentage => _bodyFat / Weight;
    public double MusclePercentage => _muscle / Weight;
    public double Weight => _weight;

    // Core temperature and metabolism
    public double CoreTemperature => _coreTemperature;
    public double BasalMetabolicRate => CalculateMetabolicRate();

    // Update physical state
    private void UpdateWeight()
    {
        _weight = _baseWeight + _bodyFat + _muscle;
    }

    // Calculate metabolic rate based on composition
    private double CalculateMetabolicRate()
    {
        // Base BMR uses the Harris-Benedict equation (simplified)
        double bmr = 370 + (21.6 * _muscle) + (6.17 * _bodyFat);

        // Adjust for injuries and conditions
        double healthFactor = _rootPart.Health / _rootPart.MaxHealth;
        bmr *= 0.7 + (0.3 * healthFactor); // Injured bodies need more energy to heal

        return bmr;
    }

    // Forwarding methods to root part
    public void Damage(DamageInfo damageInfo) => _rootPart.Damage(damageInfo);
    public void Heal(HealingInfo healingInfo) => _rootPart.Heal(healingInfo);

    // Update body state over time
    public void Update(TimeSpan timePassed, EnvironmentInfo environment)
    {
        // Handle temperature regulation
        UpdateTemperature(environment.Temperature, environment.EquipmentWarmth, timePassed);

        // Handle metabolism and energy expenditure
        UpdateMetabolism(environment.ActivityLevel, timePassed);
    }

    // Temperature regulation
    private void UpdateTemperature(double environmentalTemp, double insulationFactor, TimeSpan timePassed)
    {
        // Calculate temperature differential
        double tempDifferential = environmentalTemp - _coreTemperature;

        // Body fat provides natural insulation
        double naturalInsulation = 0.1 + (BodyFatPercentage / 2);

        // Combined insulation factor
        double totalInsulation = naturalInsulation + insulationFactor;

        // Calculate temperature change rate (degrees per hour)
        double hourlyChange = tempDifferential * (1.0 - totalInsulation) / 5.0;

        // Apply for the time that has passed
        double hoursElapsed = timePassed.TotalHours;
        _coreTemperature += hourlyChange * hoursElapsed;

        // Trigger shivering or sweating based on temperature
        if (_coreTemperature < 97.0)
        {
            // Shivering increases metabolism to generate heat
            _targetMetabolismRate *= 1.2;
        }
        else if (_coreTemperature > 100.0)
        {
            // Sweating increases water loss
            // This would connect to the thirst system
        }
    }

    // Metabolism updates
    private void UpdateMetabolism(double activityLevel, TimeSpan timePassed)
    {
        // Calculate calorie burn based on BMR, activity, and time
        double hourlyBurn = BasalMetabolicRate / 24.0 * activityLevel;
        double calories = hourlyBurn * timePassed.TotalHours;

        // If calories aren't provided externally, burn fat
        double fatBurnRate = calories / 7700.0; // ~7700 calories per kg of fat
        BodyFat -= fatBurnRate;

        // If completely out of fat, burn muscle
        if (BodyFat <= 0 && _muscle > 0)
        {
            double muscleBurnRate = calories / 7700.0 * 0.8; // Muscle burns less efficiently
            Muscle -= muscleBurnRate;
        }
    }

    // Calculate derived attributes
    public double CalculateStrength()
    {
        double manipulationCapacity = GetCapacity("Manipulation");
        double bloodPumping = GetCapacity("BloodPumping"); // Energy delivery

        // Base strength that everyone has
        double baseStrength = 0.3; // 30% strength from structural aspects

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
        double muscleBonus = Math.Min(MusclePercentage * 0.5, 0.2); // Up to 20% bonus from muscle
        double fatPenalty;

        // Minimal fat has no penalty, excess has increasing penalties
        if (BodyFatPercentage < 0.1) // 10% is minimal necessary fat
            fatPenalty = 0;
        else
            fatPenalty = (BodyFatPercentage - 0.1) * 1.2; // Steeper penalty for excess fat

        // Weight ratio with diminishing penalty
        double weightRatio = Math.Pow(_baseWeight / Weight, 0.7); // Less severe exponent

        return movingCapacity * (1 + muscleBonus - fatPenalty) * weightRatio;
    }
    private double CalculateVitality()
    {
        double breathing = GetCapacity("Breathing");
        double bloodPumping = GetCapacity("BloodPumping");
        double digestion = GetCapacity("Digestion");

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

        return (breathing + bloodPumping + digestion) / 3 * (baseMultiplier + muscleContribution + fatContribution);
    }

    private double CalculatePerception()
    {
        double sight = GetCapacity("Sight");
        double hearing = GetCapacity("Hearing");

        return (sight + hearing) / 2;
    }

    private double CalculateColdResistance()
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
        if (capacity is "Moving" or "Manipulation" or "Breathing" or "Consciousness"
        or "BloodPumping" or "Digestion" or "Eating" or "Talking")
        {
            result = values.Min();
        }
        else if (capacity is "Sight" or "Hearing" or "BloodFiltration")
        {
            result = values.Average();
        }
        else
        {
            result = values.Min();
        }
        double bodyModifier = _effectRegistry.GetBodyCapacityModifier(capacity);
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
        double modifier = _effectRegistry.GetPartCapacityModifier(capacityName, part);
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

    // Environment info for updates
    public class EnvironmentInfo
    {
        public double Temperature { get; set; } = 70.0; // Fahrenheit
        public double EquipmentWarmth { get; set; } = 0.0;
        public double ActivityLevel { get; set; } = 1.0; // 1.0 = normal
    }
}