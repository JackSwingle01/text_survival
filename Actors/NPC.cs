using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using text_survival.Items;

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
        public int Speed { get; set; }
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
