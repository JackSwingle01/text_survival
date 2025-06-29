using text_survival.Actors;
using text_survival.Bodies;
using text_survival.Effects;

namespace text_survival.Magic;

public class Spell
{
    public string Name { get; private set; }
    public bool NeedsTargetPart { get; private set; }
    private Effect Effect { get; }


    public Spell(string name, Effect effect, bool needsTargetPart)
    {
        Name = name;
        Effect = effect;
        NeedsTargetPart = needsTargetPart;
    }

    public void Cast(Actor target)
    {
        target.ApplyEffect(Effect);
    }
    public void Cast(Actor target, MajorBodyPart part)
    {
        Effect.TargetBodyPart = part;
        target.ApplyEffect(Effect);
    }

    public override string ToString()
    {
        return Name;
    }


}
