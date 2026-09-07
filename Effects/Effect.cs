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

        /// <summary>
        /// Why this effect is at the severity it is: each term that fed it, signed, so the
        /// player can hover it and read the balance instead of guessing. Only the effects
        /// that accumulate from several competing terms bother to fill this in - the ones
        /// where "why is this getting worse" is a real question.
        ///
        /// Units are whatever <see cref="ContributionUnit"/> says, per hour, and are the
        /// producer's own working numbers rather than a re-derivation: a breakdown that can
        /// disagree with the thing it explains is worse than no breakdown.
        /// </summary>
        public Dictionary<string, double> Contributions { get; init; } = [];

        /// <summary>Unit for <see cref="Contributions"/>, e.g. "soak/hr" or "F/hr".</summary>
        public string? ContributionUnit { get; init; }

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

        /// <summary>
        /// Snapshot current severity for trend tracking.
        /// Call this ONCE at the start of each update cycle.
        /// </summary>
        public void SnapshotSeverity()
        {
            PreviousSeverity = Severity;
        }

        /// <summary>
        /// Returns the change in severity since the last snapshot.
        /// Positive = worsening, negative = improving.
        /// Returns null if no snapshot has been taken yet.
        /// </summary>
        public double? GetSeverityChangeSinceSnapshot()
        {
            if (PreviousSeverity < 0) return null;  // -1 means not yet tracked
            return Severity - PreviousSeverity;
        }

        public record ThresholdMessage(double Threshold, string Message, bool WhenRising);
        public record DamageOverTime(double PerHour, DamageType Type);
    }
}