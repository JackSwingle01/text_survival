using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace text_survival
{
    public class EquipableItem : Item
    {
        public int Strength { get; set; }
        public int Defense { get; set; }
        public int Speed { get; set; }
        public EquipSpots EquipSpot { get; set; }

        public enum EquipSpots
        {
            Head,
            Chest,
            Legs,
            Feet,
            Hands,
            Weapon
        }

        public EquipableItem(string name, int strength, int defense, int speed) : base(name)
        {
            Strength = strength;
            Defense = defense;
            Speed = speed;
            UseEffect = (player) => Equip(player);
        }

        public override string ToString()
        {
            return Name + ":\n" +
                "Strength: " + Strength + "\n" +
                "Defense: " + Defense + "\n" +
                "Speed: " + Speed;
        }

        public void Equip(Player player)
        {
            if(player.EquipedItems.Any(item => item.EquipSpot == this.EquipSpot))
            {
                EquipableItem? item = player.EquipedItems.Find(item => item.EquipSpot == this.EquipSpot);
                item?.Unequip(player);
            }
            player.EquipedItems.Add(this);
            ApplyStats(player);
            player.Inventory.Remove(this);
        }
        public void Unequip(Player player)
        {
            player.EquipedItems.Remove(this);
            RemoveStats(player);
            player.Inventory.Add(this);
        }
        private void ApplyStats(Player player)
        {
            player.Strength += Strength;
            player.Defense += Defense;
            player.Speed += Speed;
        }
        private void RemoveStats(Player player)
        {
            player.Strength -= Strength;
            player.Defense -= Defense;
            player.Speed -= Speed;
        }
    }
}
