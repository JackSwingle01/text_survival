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
            var effects = target.GetEffectsByKind("bleed");
            if (effects.Count == 0)
            {
                effects = target.GetEffectsByKind("bleeding");
                if (effects.Count == 0)
                {
                    Output.WriteWarning("There was no bleeding to stop");
                    this.Remove(target);
                    return;
                }
            }
            var bleed = effects[0];
            bleed.Remove(target);
            Output.WriteLine(target, " stopped bleeding.");
            this.Remove(target);
        }
    }
}