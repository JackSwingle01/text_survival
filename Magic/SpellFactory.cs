using text_survival.Effects;

namespace text_survival.Magic
{
    public static class SpellFactory
    {
        public static Spell MinorHeal => new Spell(
            "Minor Heal",
            10,
            new HealEffect(10),
            Spell.SpellFamily.Restoration);

        public static Spell Bleeding => new Spell(
            "Bleeding",
            10,
            new BleedEffect(10, 90),
            Spell.SpellFamily.Destruction);
        public static Spell Poison => new Spell(
                "Poison",
                10,
                new PoisonEffect(5, 180),
                Spell.SpellFamily.Destruction
                );
    }
}
