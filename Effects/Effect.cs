using System.ComponentModel;
using text_survival.Actors;
using text_survival.Bodies;

namespace text_survival.Effects
{
    /// <summary>
    /// Manages its own severity state but does apply survival stats changes or capacity modifiers directly
    /// </summary>
    /// <param name="effectKind"></param>
    /// <param name="source"></param>
    /// <param name="targetBodyPart"></param>
    /// <param name="severity"></param>
    /// <param name="hourlySeverityChange"></param>
    public abstract class Effect(string effectKind, string source, string? targetBodyPart = null, double severity = 1, double hourlySeverityChange = 0)
    {
        #region Properties
        public string EffectKind { get; protected set; } = effectKind;
        public string Source { get; } = source;
        public string? TargetBodyPart { get; set; } = targetBodyPart;
        public bool CanHaveMultiple { get; protected set; } = false;
        public bool IsActive { get; protected set; } = true;
        public double Severity { get; protected set; } = severity; // multiplier for survival and capacity effects
        public double HourlySeverityChange { get; protected set; } = hourlySeverityChange;
        public bool RequiresTreatment { get; protected set; } = false;
        public CapacityModifierContainer CapacityModifiers { get; set; } = new(); // gets applied in body
        public SurvivalStatsDelta SurvivalStatsDelta { get; set; } = new(); // gets applied in survival processor
        // public List<TreatmentOption> TreatmentOptions {get;}

        // Status messages
        public string? ApplicationMessage;
        public string? RemovalMessage;

        public class ThresholdMessage(double severityThreshold, string message, bool whenRising)
        {
            public double Threshold = severityThreshold;
            public string Message = message;
            public bool WhenRising = whenRising;
            public bool WhenDropping => !WhenRising;
        }
        public readonly List<ThresholdMessage> ThresholdMessages = [];

        #endregion

        #region  core algorithm methods - typically don't override
        /// <summary>
        /// Gets called once by the effect registry, you probably shouldn't be calling this directly.
        /// </summary>
        public void Apply(Actor target)
        {
            IsActive = true;
            OnApply(target);
            if (!string.IsNullOrWhiteSpace(ApplicationMessage))
                target.AddLog(ApplicationMessage);
        }

        /// <summary>
        /// Gets called every minute. The main algorithm which calls the hook methods.
        /// If IsActive is false do nothing, otherwise call each hook in this order
        /// 1. OnUpdate
        /// 2. OnUpdateSeverity if SeverityChangeRate <> 0
        /// 3. OnRemove if severity <= 0
        /// </summary>
        public void Update(Actor target)
        {
            if (!IsActive) return;

            OnUpdate(target);

            if (!RequiresTreatment || HourlySeverityChange > 0)
            {
                double minuteChange = HourlySeverityChange / 60;
                UpdateSeverity(target, minuteChange);
            }

            if (Severity <= 0)
            {
                Remove(target);
                return;
            }
        }

        /// <summary>
        /// Gets called when the severity reaches zero and automatically removes the effect from the target
        /// </summary>
        public void Remove(Actor target)
        {
            if (!IsActive) return;
            OnRemove(target);
            IsActive = false;
            if (!string.IsNullOrWhiteSpace(RemovalMessage))
                target.AddLog(RemovalMessage);
        }

        /// <summary>
        /// Gets called every minute if the severity change rate is not 0
        /// </summary>
        public virtual void UpdateSeverity(Actor target, double severityChange)
        {
            if (!IsActive) return;

            double oldSeverity = Severity;

            Severity = Math.Clamp(Severity + severityChange, 0, 1);

            var message = GetThresholdMessage(oldSeverity, Severity);
            if (!string.IsNullOrWhiteSpace(message))
                target.AddLog(message);

            if (Math.Abs(oldSeverity - Severity) > 0.01)
            {
                OnSeverityChange(target, oldSeverity, Severity);
            }
        }

        private string? GetThresholdMessage(double oldSeverity, double newSeverity)
        {
            if (oldSeverity == newSeverity) return null;
            bool increasing = oldSeverity < newSeverity;

            double low = Math.Min(oldSeverity, newSeverity);
            double high = Math.Max(oldSeverity, newSeverity);

            var crossed = ThresholdMessages
                .Where(x => x.WhenRising == increasing) // filter by increasing/decreasing
                .Where(x => low < x.Threshold && x.Threshold < high); // get all between

            // early return in case of 0 or 1 found
            if (!crossed.Any()) return null;
            if (crossed.Count() == 1) return crossed.First().Message;
            // else get the max or min threshold passed
            var mostSignifigant = increasing ? crossed.MaxBy(x => x.Threshold) : crossed.MinBy(x => x.Threshold);
            return mostSignifigant?.Message;
        }

        #endregion


        #region Hook Methods
        // hook methods that can be implemented by sub classes

        /// <summary>
        /// Event meant to be overwritten by implementing classes that gets called once when the effect is applied to the target
        /// </summary>
        protected virtual void OnApply(Actor target) { }
        /// <summary>
        /// Event meant to be overwritten by implementing classes that gets called every minute when the effect is active
        /// </summary>
        protected virtual void OnUpdate(Actor target) { }
        /// <summary>
        /// Event meant to be overwritten by implementing classes that gets called whenever the severity changes
        /// </summary>
        protected virtual void OnSeverityChange(Actor target, double oldSeverity, double updatedSeverity) { }
        /// <summary>
        /// Event meant to be overwritten by implementing classes that gets called once when the effect is removed from the target
        /// </summary>
        protected virtual void OnRemove(Actor target) { }

        #endregion

        #region UI methods
        public string GetSeverityDescription()
        {
            if (Severity < 0.3f) return "Minor";
            if (Severity < 0.7f) return "Moderate";
            if (Severity < 0.9f) return "Severe";
            return "Critical";
        }
        public virtual string Describe()
        {
            string severityDesc = GetSeverityDescription();
            string locationDesc = TargetBodyPart != null ? $" on {TargetBodyPart}" : "";
            return $"{severityDesc} {EffectKind}{locationDesc}";
        }
        #endregion
    }
}