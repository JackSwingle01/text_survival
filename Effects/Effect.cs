using text_survival.Bodies;

namespace text_survival.Effects
{
    public class Effect
    {
        // Identity
        public string EffectKind { get; init; } = "";
        public string Source { get; init; } = "";
        public string? TargetBodyPart { get; init; }

        // State
        public double Severity { get; set; } = 1.0;
        public double HourlySeverityChange { get; init; } = 0;
        public bool RequiresTreatment { get; init; } = false;
        public bool CanHaveMultiple { get; init; } = false;
        public bool IsActive { get; set; } = true;

        // What it does - all optional data components
        public SurvivalStatsDelta StatsDelta { get; init; } = new();
        public CapacityModifierContainer CapacityModifiers { get; init; } = new();
        public DamageOverTime? Damage { get; init; }

        // Messages
        public string? ApplicationMessage { get; init; }
        public string? RemovalMessage { get; init; }
        public List<ThresholdMessage> ThresholdMessages { get; init; } = [];

        public record ThresholdMessage(double Threshold, string Message, bool WhenRising);
        public record DamageOverTime(double PerHour, DamageType Type);
    }
}