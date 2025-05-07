using text_survival.Actors;
using text_survival.Effects;

namespace text_survival.Magic
{
    public class Spell
    {
        public enum SpellFamily
        {
            Destruction,
            Restoration,
        }
        public string Name { get; private set; }
        //public string Description { get; set; }
        public double PsychCost { get; private set; }
        private IEffect Effect { get; }
        public SpellFamily Family { get; }

        public Spell(string name, double psychCost, IEffect effect, SpellFamily family)
        {
            Name = name;
            PsychCost = psychCost;
            Effect = effect;
            Family = family;
        }

        public void Cast(IActor target)
        {
            target.ApplyEffect(Effect);
        }


    }
}
