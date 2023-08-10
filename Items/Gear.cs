using text_survival.Level;

namespace text_survival.Items
{
    public class Gear : Item, IEquippable
    {
        public Buff Buff { get; set; }

        public Gear(string name, double weight = 1) : base(name, weight)
        {
            UseEffect = (player) =>
            {
                player.Equip(this);
            };
            Buff = new Buff(name, -1);
        }

        public void OnEquip(Player player)
        {
            if (player.HeldItem != null)
            {
                player.Unequip(player.HeldItem);
            }
            player.HeldItem = this;
            Buff.ApplyTo(player);
            player.Inventory.Remove(this);
        }

        public void OnUnequip(Player player)
        {
            player.HeldItem = null;
            Buff.RemoveFrom(player);
            player.Inventory.Add(this);
        }
    }

}

