using text_survival.Bodies;
using text_survival.Items;
using text_survival.IO;

namespace text_survival.Actors
{
    public class Animal : Npc
    {
        // public string Description { get; set; } = "";
        // public bool IsFound { get; set; } = false;
        // public bool IsHostile { get; set; } = true;
        public override Weapon ActiveWeapon { get ; protected set; }
        private Container Loot { get; }
    
    
        public Animal(string name, Weapon weapon, BodyStats bodyStats) : base(name, weapon, bodyStats)
        {
            Name = name;
            ActiveWeapon = weapon;

            // Set up loot container
            Loot = new Container(name, 10);
        }

  
        public override string ToString() => Name;
    }
}