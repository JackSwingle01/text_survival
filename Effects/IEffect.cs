using text_survival.Actors;

namespace text_survival.Effects
{
    public interface IEffect
    {
        string EffectType { get; }
        float Severity { get; }
        bool IsActive { get; }
        int DurationMin { get; } // Total duration in minutes
        int RemainingDurationMin { get; } // Remaining duration in minutes

        void Apply(IActor target);
        void Update(IActor target);
        void Remove(IActor target);
    }

    public abstract class Effect : IEffect
    {
        public string EffectType { get; }
        public float Severity { get; protected set; }
        public bool IsActive { get; protected set; }
        public int DurationMin { get; }
        public int RemainingDurationMin { get; protected set; }
        protected Effect(string effectType, int durationMin, float severity = 1.0f)
        {
            EffectType = effectType;
            DurationMin = durationMin;
            RemainingDurationMin = durationMin;
            Severity = severity;
            IsActive = true;
        }

        public abstract void Apply(IActor target);
        public abstract void Update(IActor target);
        public abstract void Remove(IActor target);
    }
}