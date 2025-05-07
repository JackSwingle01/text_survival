
using text_survival.Level;

namespace text_survival.Actors
{
    public interface ICombatant : IActor, IBuffable
    {
        public bool IsAlive { get; }
        public bool IsEngaged { get; set; }
        public Attributes Attributes { get; }
        public void Attack(ICombatant target);
        public double DetermineDamage();
        public double DetermineHitChance(ICombatant defender);
        public double DetermineDodgeChance(ICombatant attacker);
        public double DetermineBlockChance(ICombatant attacker);

    }
}
