namespace text_survival.Bodies;

public class Tissue(string name, double toughness = 1)
{
    public string Name { get; } = name;

    /// <summary>
    /// 0-1 value
    /// </summary>
    public double Condition { get; set; } = 1.0;
    public double Toughness { get; set; } = toughness;

    public virtual CapacityContainer GetBaseCapacities() => new(); // Most tissues don't provide base capacities    
    public virtual CapacityContainer GetConditionMultipliers()
    {
        {
            // Default: condition affects everything equally
            return new CapacityContainer
            {
                Moving = Condition,
                Manipulation = Condition,
                Breathing = Condition,
                BloodPumping = Condition,
                Consciousness = Condition,
                Sight = Condition,
                Hearing = Condition,
                Digestion = Condition
            };
        }
    }

    // Protection multipliers for different damage types
    public double BluntMultiplier { get; set; } = 1.0;
    public double SharpMultiplier { get; set; } = 1.0;
    public double PierceMultiplier { get; set; } = 1.0;

    public virtual double GetProtection(DamageType damageType)
    {
        // protection provided to sub layers
        double baseProtection = Toughness * Condition;

        return damageType switch
        {
            DamageType.Blunt => baseProtection * BluntMultiplier,
            DamageType.Sharp => baseProtection * SharpMultiplier,
            DamageType.Pierce => baseProtection * PierceMultiplier,
            _ => baseProtection
        };
    }

    public virtual double GetNaturalAbsorption(DamageType damageType)
    {
        double baseThreshold = Name switch
        {
            OrganNames.Heart => 8,
            OrganNames.Brain => 6,
            OrganNames.LeftLung or OrganNames.RightLung => 10,
            OrganNames.Liver => 5,
            _ => 1
        };

        return damageType switch
        {
            DamageType.Blunt => baseThreshold,
            DamageType.Sharp => baseThreshold * 0.4,
            DamageType.Pierce => baseThreshold * 0.2,
            _ => baseThreshold * 0.6
        };
    }
}

class Muscle() : Tissue("Muscle", 1)
{
    
     public override CapacityContainer GetBaseCapacities()
    {
        // Muscle contributes to physical capacities
        return new CapacityContainer
        {
            Moving = 1,        // Each muscle group adds to movement
            Manipulation = 1,  // Each muscle group adds to manipulation
        };
    }
    public override CapacityContainer GetConditionMultipliers()
    {
        // Muscle primarily affects movement and manipulation
        return new CapacityContainer
        {
            Moving = Condition,
            Manipulation = Condition,
            // Other capacities get minimal impact
            Breathing = 0.8 + (Condition * 0.2),
            BloodPumping = 0.9 + (Condition * 0.1),
            Consciousness = 1.0,
            Sight = 1.0,
            Hearing = 1.0,
            Digestion = 1.0
        };
    }
}

class Bone() : Tissue("Bone", 10)
{
    public override CapacityContainer GetConditionMultipliers()
    {
        return new CapacityContainer
        {
            Moving = Condition * Condition, // squared, small impact near 1, exponential debuff approaching 0
            Manipulation = Condition,
            BloodPumping = 0.8 + (0.2 * Condition),
            // Other capacities get minimal impact
            Breathing = 1.0,
            Consciousness = 1.0,
            Sight = 1.0,
            Hearing = 1.0,
            Digestion = 1.0
        };
    }
}
