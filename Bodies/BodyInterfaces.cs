
namespace text_survival.Bodies;


// Comprehensive damage information

public enum DamageType
{
    Blunt,
    Sharp,
    Pierce,
    Poison,
    Bleed,
    
}
public class DamageInfo
{
    public DamageInfo() { }
    public DamageInfo(double amount,
                      DamageType type = DamageType.Blunt,
                      string? source = null,
                      IBodyPart? targetPart = null,
                      double accuracy = .9)
    {
        Amount = amount;
        Type = type;
        Source = source;
        TargetPart = targetPart;
        Accuracy = accuracy;
    }

    public double Amount { get; set; }
    public DamageType Type { get; set; } = DamageType.Blunt;
    public string? Source { get; set; }
    public IBodyPart? TargetPart { get; set; }
    public double Accuracy { get; set; } = .9;
}

// Comprehensive healing information
public class HealingInfo
{
    public double Amount { get; set; }
    public string Type { get; set; } = "natural"; // natural, medical, magical
    public string? TargetPart { get; set; }
    public string? TargetOrgan { get; set; }
    public double Quality { get; set; } = 1.0; // Effectiveness multiplier
    public string? Source { get; set; }
}

