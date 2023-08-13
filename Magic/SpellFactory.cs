using text_survival.Level;

namespace text_survival.Magic
{
    public static class SpellFactory
    {
        public static Spell MinorHeal => new Spell(
            "Minor Heal",
            10,
            CommonBuffs.Heal(10),
            Spell.SpellFamily.Restoration);
        public static Spell Heal => new Spell(
            "Heal",
            20, 
            CommonBuffs.Heal(25),
            Spell.SpellFamily.Restoration);
        public static Spell MajorHeal => new Spell(
            "Major Heal",
            30,
            CommonBuffs.Heal(40),
            Spell.SpellFamily.Restoration);
        public static Spell Bleeding => new Spell(
            "Bleeding",
            10,
            CommonBuffs.Bleeding(1, 10),
            Spell.SpellFamily.Destruction);
        public static Spell Poison => new Spell(
                "Poison",
                10,
                CommonBuffs.Poison(2, 5),
                Spell.SpellFamily.Destruction
                );
    }
}
