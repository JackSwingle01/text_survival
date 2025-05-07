using text_survival.Actors;
using text_survival.IO;

namespace text_survival.Effects
{
    public class HealEffect : Effect
    {
        public HealEffect(double healAmount) : base("Heal", durationMin: 0, severity: 1.0f)
        {
            HealAmount = healAmount;
        }

        public override void Apply(IActor target)
        {
            double health = HealAmount * Severity;
            target.Heal(health);
            Output.WriteLine(target, " healed ", health, " points.");
            IsActive = false;
        }

        public override void Update(IActor target)
        {
            // Instantaneous effect, no update needed
        }

        public override void Remove(IActor target)
        {
            // No cleanup needed
        }
        private double HealAmount;
    }
}