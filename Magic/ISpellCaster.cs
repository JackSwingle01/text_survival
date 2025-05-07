using text_survival.Actors;

namespace text_survival.Magic
{
    public interface ISpellCaster
    {
        public void CastSpell(Spell spell, ICombatant target);
    }
}
