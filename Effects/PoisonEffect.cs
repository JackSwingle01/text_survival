using text_survival.Actors;
using text_survival.IO;

namespace text_survival.Effects
{
    public class PoisonEffect : Effect
    {
        public PoisonEffect(float damagePerHour, int durationMin) 
            : base("Poison", durationMin, severity: 1.0f)
        {
            DamagePerHour = damagePerHour;
        }

        public override void Apply(IActor target)
        {
            Output.WriteLine($"{target} is poisoned!");
            IsActive = true;
        }

        public override void Update(IActor target)
        {
            if (!IsActive) return;

            float damage = DamagePerHour / 60 * Severity;
            target.Damage(damage);
            Output.WriteLine($"{target} takes {damage:F2} damage from poison.");

            RemainingDurationMin -= 1;
            if (RemainingDurationMin <= 0)
            {
                Remove(target);
            }
        }

        public override void Remove(IActor target)
        {
            if (!IsActive) return;
            IsActive = false;
            Output.WriteLine($"{target} is no longer poisoned.");
        }

        private float DamagePerHour;
    }
}