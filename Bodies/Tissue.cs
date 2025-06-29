namespace text_survival.Bodies;

interface ICapacityContributor
{
    Capacities GetBaseCapacities();
    Capacities GetConditionMultipliers();
}

public interface IBodyPart
{
    string Name { get; }
    double Condition { get; } // 0-1 health
    double Toughness { get; } // divisor of damage applied
    void TakeDamage(DamageInfo damageInfo);
}

public class Tissue(string name, double toughness = 1) : ICapacityContributor, IBodyPart
{
    public string Name { get; } = name;
    public double Condition { get; set; } = 1.0;
    public double Toughness { get; set; } = toughness;

    public virtual Capacities GetBaseCapacities() => new(); // Most tissues don't provide base capacities    
    public virtual Capacities GetConditionMultipliers()
    {
        {
            // Default: condition affects everything equally
            return new Capacities
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

    public double GetProtection(DamageType damageType)
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

    public void TakeDamage(DamageInfo damageInfo)
    {
        double damage = damageInfo.Amount;
        var damageType = damageInfo.Type;

        double threshold = GetNaturalAbsorption(damageType);
        if (damage < threshold)
        {
            return; // Natural squishiness absorbed it
        }

        damage -= threshold;
        damageInfo.Amount = damage;

        double healthLoss = damageInfo.Amount / GetProtection(damageInfo.Type);
        Condition = Math.Max(0, Condition - healthLoss);
    }

    private double GetNaturalAbsorption(DamageType damageType)
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
    public override Capacities GetConditionMultipliers()
    {
        // Muscle primarily affects movement and manipulation
        return new Capacities
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
    public override Capacities GetConditionMultipliers()
    {
        return new Capacities
        {
            Moving = Condition * Condition, // squared, small impact near 1, exponential debuff approaching 0
            Manipulation = Condition,
            BloodPumping = 0.8 * (0.2 * Condition),
            // Other capacities get minimal impact
            Breathing = 1.0,
            Consciousness = 1.0,
            Sight = 1.0,
            Hearing = 1.0,
            Digestion = 1.0
        };
    }
}
