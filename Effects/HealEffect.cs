using text_survival.Actors;
using text_survival.Bodies;
using text_survival.IO;

namespace text_survival.Effects
{
    public class HealEffect : Effect
    {
        public HealEffect(HealingInfo healingInfo) : base("Heal", durationMin: 0, severity: 1.0f)
        {
            Heal = healingInfo;
        }

        public override void Apply(IActor target)
        {
            Heal.Amount *= Severity;
            Heal.Quality *= Severity;
            target.Heal(Heal);
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
        private HealingInfo Heal;
    }
}