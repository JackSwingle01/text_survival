using text_survival.Actors;
using text_survival.Bodies;

namespace text_survival.Effects
{
    public interface IEffect
    {
        string EffectType { get; }
        bool IsActive { get; }
        // float Severity { get; }
        // int DurationMin { get; } // Total duration in minutes
        // int RemainingDurationMin { get; } // Remaining duration in minutes

        void Apply(IActor target);
        void Update(IActor target);
        void Remove(IActor target);
    }

    public abstract class Effect : IEffect
    {
        public string EffectType { get; }
        public string Source { get; } // what caused this effect (e.g., cold, wound poison)
        public float Severity { get; protected set; }
        public bool IsActive { get; protected set; }
        public BodyPart? TargetBodyPart { get; }
        public bool IsStackable { get; }
        public bool RequiresTreatment { get; }
        public float SeverityChangeRate { get; } // per severity reduction per hour
        // public List<TreatmentOption> TreatmentOptions {get;}



        protected Effect(string effectType, string source, float severity = 1.0f, float severityChangeRate=0)
        {
            EffectType = effectType;
            Source = source;
            Severity = severity;
            SeverityChangeRate = severityChangeRate
            // IsActive = true;
        }

        public virtual void Apply(IActor target)
        {
            IsActive = true;
            OnApply(target);
        }
        protected abstract void OnApply(IActor target);

        public virtual void Update(IActor target)
        {
            if (!IsActive) return;

            if (!RequiresTreatment && SeverityChangeRate > 0)
            {
                float minuteChange = SeverityChangeRate / 60;
                float oldSeverity = Severity;
                Severity = Math.Max(0, Severity + minuteChange);

                if (Math.Abs(Severity - oldSeverity) > .01){
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
        protected abstract void OnUpdate(IActor target);
        protected abstract void OnSeverityChange(IActor target, float oldSeverity, float updatedSeverity);
        public virtual void Remove(IActor target)
        {
            if (!IsActive) return;
            OnRemove(target);
            IsActive = false;
        }
        protected abstract void OnRemove(IActor target);

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
            return $"{severityDesc} {EffectType}{locationDesc}";
        }
    }
}