namespace text_survival.Actors
{
    public interface ICombatant
    {
        public string Name { get; }
        public double Health { get; }
        public double MaxHealth { get; }
        public double ArmorRating { get; }
        public Attributes Attributes { get; }

        public void Damage(double damage);
        public void Attack(ICombatant target);

    }
}
