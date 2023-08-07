using text_survival.Items;
using text_survival.Level;

namespace text_survival.Actors
{
    public class Npc : IActor
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public float Health { get; set; }
        public float MaxHealth { get; set; }
        public float Strength { get; set; }
        public float Defense { get; set; }
        public float Speed { get; set; }
        public bool IsHostile { get; set; }
        public List<Item> Loot { get; set; }
        public bool IsAlive => Health > 0;

        public Npc(string name, int health = 10, int strength = 10, int defense = 10, int speed = 10)
        {
            Name = name;
            Health = health;
            MaxHealth = health;
            Strength = strength;
            Defense = defense;
            Speed = speed;
            Loot = new List<Item>();
            IsHostile = true;
            Description = "";
        }

        public void Attack(IActor target)
        {
            float damage = Combat.CalcDamage(this, target);
            if (Combat.DetermineDodge(this, target))
            {
                Utils.Write(target, " dodged the attack!\n");
                if (target is Player)
                {
                    EventAggregator.Publish(new GainExperienceEvent(1, SkillType.Speed));
                }
                return;
            }
            Thread.Sleep(1000);
            Utils.WriteLine(this, " attacked ", target, " for ", Math.Round(damage, 1), " damage!");
            target.Damage(damage);
            if (target is Player)
            {
                EventAggregator.Publish(new GainExperienceEvent(1, SkillType.Defense));
            }
            Thread.Sleep(1000);
        }
        public string StatsToString()
        {
            return Name + ":\n" +
              "Health: " + Health + "/" + MaxHealth + "\n" +
              "Strength: " + Strength + "\n" +
              "Defense: " + Defense;
        }
        public override string ToString()
        {
            return Name;
        }
        public void Write()
        {
            Utils.Write(this);
        }

        public void Damage(float damage)
        {
            Health -= damage;
            if (Health < 0)
            {
                EventAggregator.Publish(new EnemyDefeatedEvent(this));
            }
        }

    }
}
