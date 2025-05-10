namespace text_survival.Bodies;
public class Body : IPhysicalEntity
{
    // Root part and core properties
    private readonly BodyPart _rootPart;
    public string Name => _rootPart.Name;
    public double Health => _rootPart.Health;
    public double MaxHealth => _rootPart.MaxHealth;
    public bool IsDestroyed => _rootPart.IsDestroyed;
    
    // Physical composition
    private double _bodyFat;
    private double _muscle;
    private double _weight;
    private readonly double _baseWeight;
    
    // Physical systems
    private double _coreTemperature = 98.6; // Fahrenheit
    private double _targetMetabolismRate = 2000; // Calories per day
    
    public Body(BodyPart rootPart, double overallWeight, double fatPercent, double musclePercent)
    {
        _rootPart = rootPart;
        
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
    
    public double BodyFatPercentage => Weight > 0 ? (_bodyFat / Weight) * 100 : 0;
    public double MusclePercentage => Weight > 0 ? (_muscle / Weight) * 100 : 0;
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
        // Update all body parts and conditions
        _rootPart.Update(timePassed);
        
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
        double naturalInsulation = 0.1 + (BodyFatPercentage * 0.005);
        
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
    
    // Get body capacities for external systems
    public IReadOnlyDictionary<string, double> GetCapacities()
    {
        // Get base capacities from body parts
        var baseCapacities = GetAllCapacities();
        
        // Add derived physical attributes
        baseCapacities["Strength"] = CalculateStrength(baseCapacities);
        baseCapacities["Speed"] = CalculateSpeed(baseCapacities);
        baseCapacities["Vitality"] = CalculateVitality(baseCapacities);
        baseCapacities["Perception"] = CalculatePerception(baseCapacities);
        baseCapacities["ColdResistance"] = CalculateColdResistance();
        
        return baseCapacities;
    }
    
    // Calculate derived attributes
    private double CalculateStrength(Dictionary<string, double> capacities)
    {
        double manipulationCapacity = capacities.GetValueOrDefault("Manipulation", 0);
        return manipulationCapacity * (MusclePercentage / 100);
    }
    
    private double CalculateSpeed(Dictionary<string, double> capacities)
    {
        double movingCapacity = capacities.GetValueOrDefault("Moving", 0);
        return movingCapacity * (1 - BodyFatPercentage / 100) * (_baseWeight / Weight);
    }
    
    private double CalculateVitality(Dictionary<string, double> capacities)
    {
        double breathing = capacities.GetValueOrDefault("Breathing", 0);
        double bloodPumping = capacities.GetValueOrDefault("BloodPumping", 0);
        double digestion = capacities.GetValueOrDefault("Digestion", 0);
        
        return (breathing + bloodPumping + digestion) / 3 * 
               (MusclePercentage / 100 + BodyFatPercentage / 200);
    }
    
    private double CalculatePerception(Dictionary<string, double> capacities)
    {
        double sight = capacities.GetValueOrDefault("Sight", 0);
        double hearing = capacities.GetValueOrDefault("Hearing", 0);
        
        return (sight + hearing) / 2;
    }
    
    private double CalculateColdResistance()
    {
        // Fat provides insulation
        return 0.5 + (BodyFatPercentage / 100);
    }
    
    // Get all capacities from all body parts
    private Dictionary<string, double> GetAllCapacities()
    {
        var result = new Dictionary<string, double>();
        var allParts = GetAllBodyParts();
        
        // Group parts by capacity type
        var capacityGroups = new Dictionary<string, List<KeyValuePair<BodyPart, double>>>();
        
        foreach (var part in allParts)
        {
            foreach (var cap in part.GetCapacities())
            {
                if (!capacityGroups.ContainsKey(cap.Key))
                {
                    capacityGroups[cap.Key] = new List<KeyValuePair<BodyPart, double>>();
                }
                
                capacityGroups[cap.Key].Add(new KeyValuePair<BodyPart, double>(part, cap.Value));
            }
        }
        
        // Calculate each capacity based on its aggregation rule
        result["Moving"] = CalculateMinCapacity(capacityGroups.GetValueOrDefault("Moving", new()));
        result["Manipulation"] = CalculateMinCapacity(capacityGroups.GetValueOrDefault("Manipulation", new()));
        result["Breathing"] = CalculateMinCapacity(capacityGroups.GetValueOrDefault("Breathing", new()));
        result["Consciousness"] = CalculateMinCapacity(capacityGroups.GetValueOrDefault("Consciousness", new()));
        result["Sight"] = CalculateAverageCapacity(capacityGroups.GetValueOrDefault("Sight", new()));
        result["Hearing"] = CalculateAverageCapacity(capacityGroups.GetValueOrDefault("Hearing", new()));
        result["BloodPumping"] = CalculateMinCapacity(capacityGroups.GetValueOrDefault("BloodPumping", new()));
        result["Digestion"] = CalculateMinCapacity(capacityGroups.GetValueOrDefault("Digestion", new()));
        result["BloodFiltration"] = CalculateAverageCapacity(capacityGroups.GetValueOrDefault("BloodFiltration", new()));
        result["Eating"] = CalculateMinCapacity(capacityGroups.GetValueOrDefault("Eating", new()));
        result["Talking"] = CalculateMinCapacity(capacityGroups.GetValueOrDefault("Talking", new()));
        
        return result;
    }
    
    // Capacity calculation helpers
    private double CalculateMinCapacity(List<KeyValuePair<BodyPart, double>> capacities)
    {
        return capacities.Count > 0 ? capacities.Min(c => c.Value) : 0;
    }
    
    private double CalculateAverageCapacity(List<KeyValuePair<BodyPart, double>> capacities)
    {
        return capacities.Count > 0 ? capacities.Average(c => c.Value) : 0;
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