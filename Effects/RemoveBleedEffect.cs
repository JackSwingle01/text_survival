using text_survival.Actors;
using text_survival.IO;

namespace text_survival.Effects
{
    public class RemoveBleedEffect : Effect
    {
        public RemoveBleedEffect() : base("RemoveBleed", durationMin: 0, severity: 1.0f)
        {
        }

        public override void Apply(IActor target)
        {
            target.RemoveEffect("Bleed");
            Output.WriteLine(target, " stopped bleeding.");
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
    }
}