using text_survival_rpg_web.Level;
using text_survival_rpg_web.Magic;

namespace text_survival_rpg_web.Items
{
    public class Armor : Item, IEquippable
    {
        public EquipSpots EquipSpot { get; set; }
        public double Rating { get; set; }
        public double Warmth { get; set; }
        public ArmorClass Type { get; set; }
        public Buff? Buff { get; set; }

        public Armor(string name, double rating, EquipSpots equipSpot, double warmth = 0, ArmorClass type = ArmorClass.Light) : base(name)
        {
            Rating = rating;
            EquipSpot = equipSpot;
            UseEffect = player => player.Equip(this);
            Warmth = warmth;
            Type = type;
            if (warmth > 0) Buff = CommonBuffs.Warmth(warmth);

        }

        public override string ToString()
        {
            return Name;
        }

        public void OnEquip(Player player)
        {
            if (Buff != null)
                player.ApplyBuff(Buff);
        }

        public void OnUnequip(Player player)
        {
            if (Buff != null)
                player.RemoveBuff(Buff);
        }
    }
}