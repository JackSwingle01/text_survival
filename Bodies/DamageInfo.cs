
namespace text_survival.Bodies;


// Comprehensive damage information

public enum DamageType
{
    Blunt,
    Sharp,
    Pierce,
    Poison,
    Bleed,
    Internal, // Internal damage from starvation/dehydration (bypasses armor)
    Burn,     // Heat/fire damage to skin and tissue
}
public class DamageInfo
{
    public DamageInfo() { }
    public DamageInfo(double amount,
                      DamageType type = DamageType.Blunt,
                      string? source = null,
                      BodyTarget target = BodyTarget.Random
                      )
    {
        Amount = amount;
        Type = type;
        Source = source;
        Target = target;
    }

    public double Amount { get; set; }
    public DamageType Type { get; set; } = DamageType.Blunt;
    public string? Source { get; set; }
    public BodyTarget Target { get; set; } = BodyTarget.Random;

    // Internal - used only by DamageCalculator for resolution
    internal string? TargetPartName { get; set; }
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

