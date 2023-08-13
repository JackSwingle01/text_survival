using text_survival.Actors;

namespace text_survival.Magic
{
    public interface ISpellCaster
    {
        public double Psych { get; }
        public double MaxPsych { get; }
        public double PsychRegen { get; }
        public void CastSpell(Spell spell, ICombatant target);
    }
}
