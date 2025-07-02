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
        double health = Parts.Sum(p => CalculatePartHealth(p)) / Parts.Count;
        return health;
    }

    private double CalculatePartHealth(BodyRegion part)
    {
        // Average health of materials and organs
        var materialHealth = new List<double>
        {
            part.Skin.Condition,
            part.Muscle.Condition,
            part.Bone.Condition
        };

        var organHealth = part.Organs.Select(o => o.Condition);

        var allHealth = materialHealth.Concat(organHealth);
        return allHealth.Any() ? allHealth.Average() : 1.0;
    }

    public double MaxHealth => 1;
    public bool IsDestroyed => new DeathSystem().CheckBodyState(this).Equals(BodyState.Dead);

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
        // If targeting specific part, find it
        if (damageInfo.TargetPartName != null)
        {
            var targetPart = BodyTargetHelper.GetPartByName(this, damageInfo.TargetPartName);
            if (targetPart != null)
            {
                DamagePart(targetPart, damageInfo);
                return;
            }
        }

        // Otherwise, distribute based on coverage
        var hitPart = BodyTargetHelper.GetRandomMajorPartByCoverage(this);

        DamagePart(hitPart, damageInfo);
        return;
    }

    private void DamagePart(BodyRegion part, DamageInfo damageInfo)
    {
        damageInfo.Amount = PenetrateLayers(part, damageInfo);
        if (damageInfo.Amount <= 0) return; // all absorbed

        var hitOrgan = BodyTargetHelper.SelectRandomOrganToHit(part, damageInfo.Amount);
        if (hitOrgan == null) return; // instead hit tissue

        DamageTissue(hitOrgan, damageInfo);
    }

    private void DamageTissue(Tissue tissue, DamageInfo damageInfo)
    {
        double absorption = tissue.GetNaturalAbsorption(damageInfo.Type);
        damageInfo.Amount -= absorption;
        if (damageInfo.Amount <= 0)
        {
            return; // Natural squishiness absorbed it
        }

        double healthLoss = damageInfo.Amount / tissue.GetProtection(damageInfo.Type);
        tissue.Condition = Math.Max(0, tissue.Condition - healthLoss);
    }

    private double PenetrateLayers(BodyRegion part, DamageInfo damageInfo)
    {
        DamageType damageType = damageInfo.Type;
        double damage = damageInfo.Amount;
        var layers = new[] { part.Skin, part.Muscle, part.Bone }.Where(l => l != null);

        foreach (var layer in layers)
        {
            double protection = layer!.GetProtection(damageType);
            double absorbed = Math.Min(damage * 0.7, protection); // Layer absorbs up to 70% of damage

            damageInfo.Amount -= absorbed;

            DamageTissue(layer, damageInfo); // Layer takes damage from absorbing

            if (damage <= 0) break;
        }

        return Math.Max(0, damage);
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
            .Where(p => CalculatePartHealth(p) < 1.0)
            .OrderBy(p => CalculatePartHealth(p))
            .ToList();

        if (damagedParts.Count > 0)
        {
            HealBodyPart(damagedParts[0], healingInfo);
        }
    }

    private void HealBodyPart(BodyRegion part, HealingInfo healingInfo)
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
        SurvivalData data = new()
        {
            // Primary Survival Data
            Temperature = BodyTemperature,
            Calories = CalorieStore,
            Hydration = Hydration,
            Exhaustion = Exhaustion,

            // Body Data
            BodyWeight = Weight,
            MuscleWeight = Muscle,
            FatWeight = BodyFat,
            HealthPercent = Health,

            // Environmental Data
            environmentalTemp = context.LocationTemperature,
            ColdResistance = context.ClothingInsulation,
            activityLevel = context.ActivityLevel
        };

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

    // todo - move this to the survival manager or something

    // Calculate derived attributes
    public double CalculateStrength()
    {
        var capacities = GetCapacities();
        double manipulationCapacity = capacities.Manipulation;
        double bloodPumping = capacities.BloodPumping; // Energy delivery

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
        double movingCapacity = GetCapacities().Moving;
        double structuralWeightRatio = (_baseWeight / Weight) + (1 - 0.45); // avg 45% structure weight
        double sizeRatio = Weight / BaselineHumanStats.overallWeight;

        double muscleBonus = Math.Min(MusclePercentage * 0.20, 0); // Up to 20% bonus from muscle

        double fatPenalty;
        // Minimal fat has no penalty, excess has increasing penalties
        if (BodyFatPercentage < 0.10)
        {
            // 10% is minimal necessary fat
            fatPenalty = -.01; // negative penalty if under 10% bf
        }
        else if (BodyFatPercentage <= BaselineHumanStats.fatPercent)
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
            fatPenalty = (BodyFatPercentage - BaselineHumanStats.fatPercent) * 1.5;
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
        var capacities = GetCapacities();
        double breathing = capacities.Breathing;
        double bloodPumping = capacities.BloodPumping;
        double digestion = capacities.Digestion;

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
        double sight = GetCapacities().Sight;
        double hearing = GetCapacities().Hearing;

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

    public CapacityContainer GetCapacities()
    {
        CapacityContainer total = new();
        foreach (var part in Parts)
        {
            total += GetRegionCapacities(part);
        }
        // Apply body-wide effect modifiers
        var bodyModifier = EffectRegistry.CapacityModifiers(this);
        total = total.ApplyModifier(bodyModifier);

        // Apply cascading effects
        return ApplyCascadingEffects(total);
    }

    public CapacityContainer GetRegionCapacities(BodyRegion region)
    {
        // Step 1: Sum all base capacities from organs
        var baseCapacities = new CapacityContainer();
        foreach (var organ in region.Organs)
        {
            baseCapacities += organ.GetBaseCapacities();
        }

        // Step 2: Calculate combined material multipliers
        var baseMultipliers = new CapacityContainer
        {
            Moving = 1.0,
            Manipulation = 1.0,
            Breathing = 1.0,
            BloodPumping = 1.0,
            Consciousness = 1.0,
            Sight = 1.0,
            Hearing = 1.0,
            Digestion = 1.0,
        };

        foreach (var material in new List<Tissue> { region.Skin, region.Muscle, region.Bone })
        {
            // todo revisit this, I think this will cause too big of an effect, e.g. 0.5*0.5 = .25
            var multipliers = material.GetConditionMultipliers();
            baseMultipliers = baseMultipliers.ApplyMultipliers(multipliers);
        }

        // Step 3: Apply multipliers to base capacities
        return baseCapacities.ApplyMultipliers(baseMultipliers);
    }


    private CapacityContainer ApplyCascadingEffects(CapacityContainer baseCapacities)
    {
        var result = baseCapacities;

        // Poor blood circulation affects everything
        if (result.BloodPumping < 0.5)
        {
            double circulationPenalty = 1.0 - (0.5 - result.BloodPumping);
            result.Consciousness *= circulationPenalty;
            result.Moving *= circulationPenalty;
            result.Manipulation *= circulationPenalty;
        }

        // Can't breathe? Consciousness drops rapidly
        if (result.Breathing < 0.3)
        {
            double oxygenPenalty = result.Breathing / 0.3; // 0.0 to 1.0
            result.Consciousness *= oxygenPenalty;
        }

        // Unconscious? Can't do physical actions
        if (result.Consciousness < 0.1)
        {
            result.Moving *= 0.1;
            result.Manipulation *= 0.1;
        }

        return result;
    }

    // helper for baseline male human stats
    public static BodyCreationInfo BaselineHumanStats => new BodyCreationInfo
    {
        type = BodyPartFactory.BodyTypes.Human,
        overallWeight = 75, // KG ~165 lbs
        fatPercent = .15, // pretty lean
        musclePercent = .40 // low end of athletic
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
        Output.WriteLine("- Strength: " + Math.Round(CalculateStrength() * 100) + "%");
        Output.WriteLine("- Speed: " + Math.Round(CalculateSpeed() * 100) + "%");
        Output.WriteLine("- Vitality: " + Math.Round(CalculateVitality() * 100) + "%");
        Output.WriteLine("- Perception: " + Math.Round(CalculatePerception() * 100) + "%");
        Output.WriteLine("- Cold Resistance: " + Math.Round(CalculateColdResistance() * 100) + "%");

        // Show damaged parts and materials
        var damagedParts = Parts.Where(p => CalculatePartHealth(p) < 1.0).ToList();

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
        var capacities = GetCapacities();
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

}