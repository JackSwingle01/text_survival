using text_survival.Actors;

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
        private Buff EffectBuff { get; }
        public SpellFamily Family { get; }

        public Spell(string name, double psychCost, Buff effectBuff, SpellFamily family)
        {
            this.Name = name;
            PsychCost = psychCost;
            EffectBuff = effectBuff;
            Family = family;
        }

        public void Cast(IBuffable target)
        {
            EffectBuff.ApplyTo(target);
        }


    }
}
