using text_survival.Actors;
using text_survival.Bodies;
using text_survival.IO;

namespace text_survival.Effects
{
    public class RemoveBleedEffect : Effect
    {
        public RemoveBleedEffect(string source, BodyPart? targetBodyPart) : base("RemoveBleed", source, targetBodyPart, severity: 1.0f)
        {
        }

        protected override void OnApply(Actor target)
        {
            target.RemoveEffect("Bleed");
            Output.WriteLine(target, " stopped bleeding.");
            IsActive = false;
        }
    }
}