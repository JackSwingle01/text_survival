using text_survival.Bodies;
using text_survival.Items;

namespace text_survival.Actors
{
    public class Animal : Npc
    {
        public override Weapon ActiveWeapon { get; protected set; }

        public Animal(string name, Weapon weapon, BodyStats bodyStats) : base(name, weapon, bodyStats)
        {
            Name = name;
            ActiveWeapon = weapon;
        }


        public override string ToString() => Name;
    }
}