using text_survival.Interfaces;

namespace text_survival.Bodies;
// Core interfaces for body system
public interface IPhysicalEntity : IDamageable
{
    string Name { get; }
    bool IsDestroyed { get; }
    IReadOnlyDictionary<string, double> GetCapacities();
}

// Comprehensive damage information
public class DamageInfo
{
    public DamageInfo() { }
    public DamageInfo(double amount,
                      string type = "physical",
                      string? source = null,
                      string? targetPart = null,
                      bool isPenetrating = false,
                      bool isBlunt = false,
                      bool isSharp = false,
                      double accuracy = .9)
    {
        Amount = amount;
        Type = type;
        Source = source;
        TargetPart = targetPart;
        IsPenetrating = isPenetrating;
        IsBlunt = isBlunt;
        IsSharp = isSharp;
        Accuracy = accuracy;
    }

    public double Amount { get; set; }
    public string Type { get; set; } = "physical"; // physical, thermal, poison, etc.
    public string? Source { get; set; }
    public string? TargetPart { get; set; } // Optional specific target
    public bool IsPenetrating { get; set; } = false;
    public bool IsSharp { get; set; } = false;
    public bool IsBlunt { get; set; } = false;
    public double Accuracy { get; set; } = .9;
}

// Comprehensive healing information
public class HealingInfo
{
    public double Amount { get; set; }
    public string Type { get; set; } = "natural"; // natural, medical, magical
    public string? TargetPart { get; set; }
    public double Quality { get; set; } = 1.0; // Effectiveness multiplier
    public string? Source { get; set; }
}

// Base class for physical conditions (injuries, diseases, etc.)
public abstract class PhysicalCondition
{
    public string Type { get; protected set; } = "";
    public string Name { get; protected set; } = "";
    public string Description { get; protected set; } = "";
    public double Severity { get; protected set; } // 0.0-1.0
    public double HealRate { get; protected set; }
    public Dictionary<string, double> CapacityModifiers { get; } = new();

    public bool IsHealed => Severity <= 0;

    public abstract void Update(TimeSpan timePassed);
    public abstract void ApplyTreatment(TreatmentInfo treatment);
    public virtual double ModifyCapacity(string capacityName, double baseValue)
    {
        if (CapacityModifiers.TryGetValue(capacityName, out double modifier))
        {
            return baseValue * (1 - modifier * Severity);
        }
        return baseValue;
    }
}

// Information for treatments
public class TreatmentInfo
{
    public string Type { get; set; } = ""; // bandage, surgery, etc.
    public double Quality { get; set; } // 0.0-1.0 effectiveness
    public double Duration { get; set; } // How long treatment lasts
}