using text_survival.Level;

namespace text_survival.Items
{
    public class Armor : Item, IEquippable
    {
        public EquipSpots EquipSpot { get; set; }
        public double Rating { get; set; }
        public double Warmth { get; set; }
        public ArmorClass Type { get; set; }
        public Buff Buff { get; set; }

        public Armor(string name, double rating, EquipSpots equipSpot, double warmth = 0, ArmorClass type = ArmorClass.Light) : base(name)
        {
            Rating = rating;
            EquipSpot = equipSpot;
            UseEffect = player => player.Equip(this);
            Warmth = warmth;
            Type = type;
            Buff = CommonBuffs.Warmth(warmth, -1);
        }

        public override string ToString()
        {
            return Name;
        }

        public void OnEquip(Player player)
        {
            player.ApplyBuff(Buff);
        }

        public void OnUnequip(Player player)
        {
            player.RemoveBuff(Buff);
        }
    }
}