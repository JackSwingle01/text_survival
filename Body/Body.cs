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
    private double CalculateStrength()
    {
        double manipulationCapacity = GetCapacity("Manipulation");
        return manipulationCapacity * MusclePercentage;
    }

    private double CalculateSpeed()
    {
        double movingCapacity = GetCapacity("Moving");
        return movingCapacity * (1 - BodyFatPercentage) * (_baseWeight / Weight);
    }

    private double CalculateVitality()
    {
        double breathing = GetCapacity("Breathing");
        double bloodPumping = GetCapacity("BloodPumping");
        double digestion = GetCapacity("Digestion");

        return (breathing + bloodPumping + digestion) / 3 *
               (MusclePercentage + BodyFatPercentage / 2);
    }

    private double CalculatePerception()
    {
        double sight = GetCapacity("Sight");
        double hearing = GetCapacity("Hearing");

        return (sight + hearing) / 2;
    }

    private double CalculateColdResistance()
    {
        // Fat provides insulation
        return 0.5 + (BodyFatPercentage / 1);
    }




    private double GetCapacity(string capacity)
    {
        var parts = GetAllBodyParts().Where(p => p.GetCapacity(capacity) > 0);
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
    private List<BodyPart> GetAllBodyParts()
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