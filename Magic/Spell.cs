using text_survival.Actors;

namespace text_survival.Magic
{
    public class Spell
    {
        public string Name { get; set; }
        //public string Description { get; set; }
        public double PsychCost { get; set; }
        private Buff EffectBuff { get; set; }

        public Spell(string Name, double psychCost, Buff effectBuff)
        {
            this.Name = Name;
            PsychCost = psychCost;
            EffectBuff = effectBuff;
        }

        public void Cast(IBuffable target)
        {
            target.ApplyBuff(this.EffectBuff);
        }


    }
}
