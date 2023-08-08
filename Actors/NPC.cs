using text_survival.Items;
using text_survival.Level;

namespace text_survival.Actors
{
    public class Npc : ICombatant
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public double Health { get; set; }
        public double MaxHealth { get; set; }
        public double ArmorRating { get; set; }
        public bool IsHostile { get; set; }
        public List<Item> Loot { get; set; }
        public bool IsAlive => Health > 0;
        public Attributes Attributes { get; set; }

        public Npc(string name, int health = 10, int strength = 10, int defense = 10, int speed = 10)
        {
            Name = name;
            MaxHealth = health;
            Health = MaxHealth;
            ArmorRating = defense;

            Attributes = new Attributes();
            Buff buff = new()
            {
                Strength = strength,
                Speed = speed
            };
            Attributes.ApplyBuff(buff);

            Loot = new List<Item>();
            IsHostile = true;
            Description = "";
        }

        public void Attack(ICombatant target)
        {
            double strengthModifier = (Attributes.Strength + 75) / 100;
            double damage = strengthModifier;
            damage *= Utils.RandDouble(.5, 2);
            if (damage < 0)
            {
                damage = 0;
            }
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

        public override string ToString()
        {
            return Name;
        }
        public void Write()
        {
            Utils.Write(this);
        }

        public void Damage(double damage)
        {
            Health -= damage;
            if (Health < 0)
            {
                EventAggregator.Publish(new EnemyDefeatedEvent(this));
            }
        }

    }
}
