using text_survival.Bodies;

namespace text_survival.Effects
{
    public class Effect
    {
        // Identity
        public string EffectKind { get; init; } = "";
        public BodyTarget? TargetBodyPart { get; init; }

        // State
        public double Severity { get; set; } = 1.0;
        public double PreviousSeverity { get; set; } = -1; // -1 = not yet tracked
        public double HourlySeverityChange { get; init; } = 0;
        public bool RequiresTreatment { get; init; } = false;
        public bool CanHaveMultiple { get; init; } = false;
        public bool IsActive { get; set; } = true;

        // What it does - all optional data components
        public SurvivalStatsDelta StatsDelta { get; init; } = new();
        public CapacityModifierContainer CapacityModifiers { get; init; } = new();
        public DamageOverTime? Damage { get; init; }

        /// <summary>
        /// Multiplier for body healing rate. 1.0 = normal, 1.5 = +50% faster healing.
        /// Used by positive effects like Nourished.
        /// </summary>
        public double HealingMultiplier { get; init; } = 1.0;

        /// <summary>
        /// True if this effect provides benefits (positive capacity modifiers or healing boost).
        /// False if harmful (negative capacity modifiers or damage).
        /// </summary>
        public bool IsBeneficial
        {
            get
            {
                // Has damage = harmful
                if (Damage != null) return false;

                // Healing boost = beneficial
                if (HealingMultiplier > 1.0) return true;

                // Check capacity modifiers - sum them up
                double totalModifier = CapacityModifiers.ToDictionary().Values.Sum();
                return totalModifier > 0;
            }
        }

        // Messages
        public string? ApplicationMessage { get; init; }
        public string? RemovalMessage { get; init; }
        public List<ThresholdMessage> ThresholdMessages { get; init; } = [];

        public record ThresholdMessage(double Threshold, string Message, bool WhenRising);
        public record DamageOverTime(double PerHour, DamageType Type);
    }
}