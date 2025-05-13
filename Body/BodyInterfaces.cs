
namespace text_survival.Bodies;


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

