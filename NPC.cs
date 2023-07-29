using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace text_survival
{
    public class NPC : IActor
    {
        public string Name { get; set; }
        public float Health { get; set; }
        public float MaxHealth { get; set; }
        public float Strength { get; set; }
        public float Defense { get; set; }
        public int Speed { get; set; }

        public NPC(string name, int health=10, int strength = 10, int defense = 10, int speed=10)
        {
            Name = name;
            Health = health;
            MaxHealth = health;
            Strength = strength;
            Defense = defense;
            Speed = speed;
        }

        public override string ToString()
        {
              return Name + ":\n" +
                "Health: " + Health + "/" + MaxHealth + "\n" +
                "Strength: " + Strength + "\n" +
                "Defense: " + Defense;
        }
        
        public void Damage(float damage)
        {
            Health -= damage;
        }

    }
}
