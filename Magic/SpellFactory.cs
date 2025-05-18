using text_survival.Effects;

namespace text_survival.Magic
{
    public static class SpellFactory
    {
        public static Spell MinorHeal => new Spell("Minor Heal", new HealEffect("healing spell", null, 10), true);
        public static Spell Bleeding => new Spell("Bleeding", new BleedEffect("bleed spell", null, 1, 10, 60), true);
        public static Spell Poison => new Spell("Poison", new PoisonEffect("magic", "poison spell", 1, 5, 180), false);
    }
}
