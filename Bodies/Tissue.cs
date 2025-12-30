using System.Text.Json.Serialization;

namespace text_survival.Bodies;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
[JsonDerivedType(typeof(Tissue), "tissue")]
[JsonDerivedType(typeof(Muscle), "muscle")]
[JsonDerivedType(typeof(Bone), "bone")]
public class Tissue
{
    public string Name { get; init; } = "Unknown";

    /// <summary>
    /// 0-1 value
    /// </summary>
    public double Condition { get; set; } = 1.0;
    public double Toughness { get; set; } = 1.0;

    // Parameterless constructor for deserialization
    public Tissue()
    {
    }

    // Normal constructor for creation
    public Tissue(string name, double toughness = 1)
    {
        Name = name;
        Toughness = toughness;
    }

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

    /// <summary>
    /// Natural damage absorption before condition loss.
    /// Values scaled for 0-1 damage system where 1.0 = destroys tissue layer.
    /// </summary>
    public virtual double GetNaturalAbsorption(DamageType damageType)
    {
        // Organs have higher absorption (protected by being internal)
        // Tissues have minimal absorption (scratches don't wound)
        double baseThreshold = Name switch
        {
            OrganNames.Heart => 0.08,
            OrganNames.Brain => 0.06,
            OrganNames.LeftLung or OrganNames.RightLung => 0.10,
            OrganNames.Liver => 0.05,
            _ => 0.01  // Skin/muscle - only absorbs tiny scratches
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

class Muscle : Tissue
{
    // Parameterless constructor for deserialization
    public Muscle() : base()
    {
    }

    // Normal constructor
    public Muscle(string name) : base(name, 1)
    {
    }

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

class Bone : Tissue
{
    // Parameterless constructor for deserialization
    public Bone() : base()
    {
    }

    // Normal constructor - toughness 3.0 provides protection but isn't invulnerable
    public Bone(string name) : base(name, 3)
    {
    }

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
