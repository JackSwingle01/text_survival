namespace text_survival.Actors
{
    public interface ICombatant : IActor, IBuffable
    {
        public double ArmorRating { get; }

        public void Attack(ICombatant target);
        public double DetermineDamage(ICombatant defender);
        public double DetermineHitChance(ICombatant defender);
        public double DetermineDodgeChance(ICombatant attacker);
        public double DetermineBlockChance(ICombatant attacker);

    }
}
