using text_survival.Level;

namespace text_survival.Actors
{
    public interface IActor
    {
        public string Name { get; set; }
        public double Health { get; set; }
        public double MaxHealth { get; set;  }
        public double ArmorRating { get;  }
        public Attributes Attributes { get; set; }
        public List<Buff> Buffs { get; set; }

        public void Update();
        public void Damage(double damage);
        public void Attack(IActor target);
        public double DetermineDamage(IActor defender);
        public double DetermineHitChance(IActor defender);
        public double DetermineDodgeChance(IActor attacker);





    }
}
