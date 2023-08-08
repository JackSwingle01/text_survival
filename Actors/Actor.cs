namespace text_survival.Actors
{
    public interface ICombatant
    {
        public string Name { get; }
        public double Health { get; }
        public double MaxHealth { get; }
        public float Defense { get; }
        public Attributes Attributes { get; }

        public void Damage(double damage);
        public void Attack(ICombatant target);

    }
}
