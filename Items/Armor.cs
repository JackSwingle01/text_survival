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
            Buff = new Buff(name);
            Buff.ApplyEffect += (target) =>
            {
                if (target is Player player)
                    player.WarmthBonus += this.Warmth;
            };
            Buff.RemoveEffect += (target) =>
            {
                if (target is Player player)
                    player.WarmthBonus -= this.Warmth;
            };
        }

        public override string ToString()
        {
            return Name;
        }

        public void OnEquip(Player player)
        {
            var oldItem = player.Armor.FirstOrDefault(i => i.EquipSpot == this.EquipSpot);
            if (oldItem != null)
            {
                player.Unequip(oldItem);
            }
            player.Armor.Add(this);
            Buff.ApplyTo(player);
            player.Inventory.Remove(this);
        }

        public void OnUnequip(Player player)
        {
            player.Armor.Remove(this);
            Buff.RemoveFrom(player);
            player.Inventory.Add(this);
        }
    }
}