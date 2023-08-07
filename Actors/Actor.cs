namespace text_survival.Actors
{
    public interface IActor
    {
        public string Name { get; set; }
        public float Health { get; set; }
        public float MaxHealth { get; set; }
        public float Strength { get; set; }
        public float Defense { get; set; }
        public float Speed { get; set; }

        public void Damage(float damage);
        public void Attack(IActor target);

    }
}
