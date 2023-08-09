using text_survival.Items;
using text_survival.Level;

namespace text_survival.Actors
{
    public class Humanoid : Npc
    {
        public Weapon Weapon { get; set; }
        public Armor Armor { get; set; }


        public Humanoid(string name, Weapon? weapon = null, Armor? armor = null, Attributes? attributes = null) : base(name, attributes)
        {
            Weapon = weapon ?? new Weapon(WeaponType.Dagger, WeaponMaterial.Iron);
            Armor = armor ?? new Armor("Leather Outfit", .1, EquipSpots.Chest, 4);
        }

    }
}
