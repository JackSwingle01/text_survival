using System.ComponentModel.DataAnnotations;
using text_survival.IO;

namespace text_survival.Bodies;


public static class BodyPartNames
{
    public const string Head = "Head";
    public const string Chest = "Chest";
    public const string Abdomen = "Abdomen";
    public const string LeftArm = "Left Arm";
    public const string RightArm = "Right Arm";
    public const string LeftLeg = "Left Leg";
    public const string RightLeg = "Right Leg";
}


public class MajorBodyPart(string name, double coverage) : IBodyPart
{
    // Core properties
    public string Name { get; } = name;
    public double Coverage { get; set; } = coverage;

    // part makeup
    public Tissue Skin { get; set; } = new Tissue("Skin");
    public Tissue Muscle { get; set; } = new Muscle();
    public Tissue Bone { get; set; } = new Bone();
    public List<Organ> Organs { get; set; } = [];

    public double Toughness { get; }
    public double Condition { get; set; }
    public bool IsDestroyed => Condition <= 0;

    public void Heal(HealingInfo healingInfo)
    {
        if (IsDestroyed) return;

        // Handle targeted healing
        if (healingInfo.TargetOrgan != null)
        {
            // Try to find the targeted part
            var organ = Organs.FirstOrDefault(o => o.Name == healingInfo.TargetOrgan);

            if (organ != null)
            {
                organ.Heal(healingInfo);
                return;
            }
        }
        // todo heal skin, bone, muscle
        // Apply healing to this part
        double adjustedAmount = healingInfo.Amount * healingInfo.Quality;
        double newCondition = Math.Min(1, Condition + adjustedAmount);
        Condition = newCondition;
    }

    public void TakeDamage(DamageInfo damageInfo)
    {
        if (IsDestroyed) return;

        var targetPart = damageInfo.TargetPart;
        if (targetPart != null && targetPart != this)
        {
            DamageSubPart(targetPart, damageInfo);
            return;
        }

        double damage = PenetrateLayers(damageInfo);
        if (damage <= 0) return;

        damageInfo.Amount = damage;
        var hitOrgan = SelectRandomOrganToHit(damageInfo);
        if (hitOrgan == null) return;

        DamageSubPart(hitOrgan, damageInfo);
    }

    private Organ? SelectRandomOrganToHit(DamageInfo damageInfo)
    {
        double damage = damageInfo.Amount;
        // External organs can be hit even with light damage
        var externalOrgans = Organs.Where(o => o.IsExternal).ToList();
        if (externalOrgans.Count > 0 && damage > 0)
        {
            return externalOrgans[Random.Shared.Next(externalOrgans.Count)];
        }

        // Internal organs need significant penetrating damage
        var internalOrgans = Organs.Where(o => !o.IsExternal).ToList();
        if (internalOrgans.Count > 0 && damage > 5) // Threshold for internal damage
        {
            return internalOrgans[Random.Shared.Next(internalOrgans.Count)];
        }

        return null; // No organ hit
    }

    private double PenetrateLayers(DamageInfo damageInfo)
    {
        DamageType damageType = damageInfo.Type;
        double damage = damageInfo.Amount;
        var layers = new[] { Skin, Muscle, Bone }.Where(l => l != null);

        foreach (var layer in layers)
        {
            double protection = layer!.GetProtection(damageType);
            double absorbed = Math.Min(damage * 0.7, protection); // Layer absorbs up to 70% of damage

            damageInfo.Amount -= absorbed;

            layer.TakeDamage(damageInfo); // Layer takes damage from absorbing

            if (damage <= 0) break;
        }

        return Math.Max(0, damage);
    }

    private IBodyPart? DamageSubPart(IBodyPart part, DamageInfo damageInfo)
    {
        part.TakeDamage(damageInfo);
        return part;
    }

    public Capacities GetTotalCapacities()
    {
        // Step 1: Sum all base capacities from organs
        var baseCapacities = new Capacities();
        foreach (var organ in Organs)
        {
            baseCapacities += organ.GetBaseCapacities();
        }

        // Step 2: Calculate combined material multipliers
        var baseMultipliers = new Capacities
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

        foreach (var material in new List<ICapacityContributor> { Skin, Muscle, Bone })
        {
            var multipliers = material.GetConditionMultipliers();
            baseMultipliers = baseMultipliers.ApplyMultipliers(multipliers);
        }

        // Step 3: Apply multipliers to base capacities
        return baseCapacities.ApplyMultipliers(baseMultipliers);
    }

    public void Describe()
    {
        // Calculate health percentage
        int healthPercent = (int)(Condition * 100);

        // Determine damage severity description
        string damageDescription;
        if (healthPercent <= 0)
        {
            damageDescription = "destroyed";
        }
        else if (healthPercent < 20)
        {
            damageDescription = "critically damaged";
        }
        else if (healthPercent < 40)
        {
            damageDescription = "severely damaged";
        }
        else if (healthPercent < 60)
        {
            damageDescription = "moderately damaged";
        }
        else if (healthPercent < 80)
        {
            damageDescription = "lightly damaged";
        }
        else if (healthPercent < 100)
        {
            damageDescription = "slightly damaged";
        }
        else
        {
            damageDescription = "in perfect condition";
        }

        // Output description line
        Output.WriteLine($"- {Name} is {damageDescription} ({healthPercent}%)");
    }
    public override string ToString() => Name;
}