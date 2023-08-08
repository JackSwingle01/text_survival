namespace text_survival.Actors
{
    public interface ICombatant
    {
        public string Name { get;  }
        public float Health { get;  }
        public float MaxHealth { get;  }
        public float Strength { get;  }
        public float Defense { get; }
        public float Speed { get; }

        public void Damage(float damage);
        public void Attack(ICombatant target);

    }
}
