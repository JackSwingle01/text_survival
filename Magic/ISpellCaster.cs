using text_survival_rpg_web.Actors;

namespace text_survival_rpg_web.Magic
{
    public interface ISpellCaster
    {
        public double Psych { get; }
        public double MaxPsych { get; }
        public double PsychRegen { get; }
        public void CastSpell(Spell spell, ICombatant target);
    }
}
