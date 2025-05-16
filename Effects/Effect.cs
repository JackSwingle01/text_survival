using text_survival.Actors;
using text_survival.Bodies;

namespace text_survival.Effects
{
    // public interface IEffect
    // {
    //     string EffectType { get; }
    //     bool IsActive { get; }
    //     // float Severity { get; }
    //     // int DurationMin { get; } // Total duration in minutes
    //     // int RemainingDurationMin { get; } // Remaining duration in minutes

    //     void Apply(IActor target);
    //     void Update(IActor target);
    //     void Remove(IActor target);
    // }

    public abstract class Effect
    {
        protected Effect(string effectKind, string source, BodyPart? targetBodyPart = null, double severity = 1, double severityChangeRate = 0)
        {
            EffectKind = effectKind;
            Source = source;
            TargetBodyPart = targetBodyPart;
            Severity = severity;
            SeverityChangeRate = severityChangeRate;

            IsActive = true;
            IsStackable = false;
            RequiresTreatment = false;
        }

        public string EffectKind { get; protected set; }
        public string Source { get; } // what caused this effect (e.g., cold, wound poison)
        public BodyPart? TargetBodyPart { get; }
        public bool IsStackable { get; protected set; }
        public bool IsActive { get; protected set; }
        public double Severity { get; protected set; }
        public double SeverityChangeRate { get; protected set; } // per severity reduction per hour
        public bool RequiresTreatment { get; protected set; }
        public Dictionary<string, double> CapacityModifiers { get; } = [];
        // public List<TreatmentOption> TreatmentOptions {get;}

        // main algorithm methods - typically don't override
        public void Apply(Actor target)
        {
            IsActive = true;
            OnApply(target);
        }

        public void Update(Actor target)
        {
            if (!IsActive) return;

            if (!RequiresTreatment && SeverityChangeRate > 0)
            {
                double minuteChange = SeverityChangeRate / 60;
                double oldSeverity = Severity;
                Severity = Math.Max(0, Severity + minuteChange);

                if (Math.Abs(Severity - oldSeverity) > .01)
                {
                    OnSeverityChange(target, oldSeverity, Severity);
                }

                if (Severity <= 0)
                {
                    Remove(target);
                    return;
                }
            }

            OnUpdate(target);
        }
        public void Remove(Actor target)
        {
            if (!IsActive) return;
            OnRemove(target);
            IsActive = false;
        }
        public virtual void UpdateSeverity(Actor target, double severityChange)
        {
            if (!IsActive) return;

            double oldSeverity = Severity;

            Severity = Math.Clamp(Severity + severityChange, 0, 1);

            if (Math.Abs(oldSeverity - Severity) > 0.01)
            {
                OnSeverityChange(target, oldSeverity, Severity);
            }
        }


        // hook methods that can be implemented by sub classes
        protected virtual void OnApply(Actor target) { }
        protected virtual void OnUpdate(Actor target) { }
        protected virtual void OnSeverityChange(Actor target, double oldSeverity, double updatedSeverity) { }
        protected virtual void OnRemove(Actor target) { }


        // UI methods
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
            string locationDesc = TargetBodyPart != null ? $" on {TargetBodyPart.Name}" : "";
            return $"{severityDesc} {EffectKind}{locationDesc}";
        }
    }
}