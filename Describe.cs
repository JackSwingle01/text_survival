
using text_survival.IO;
using text_survival.Items;
using text_survival.PlayerComponents;

namespace text_survival
{
    public static class Describe
    {
        public static void DescribeGear(InventoryManager inv)
        {
            if (inv.IsArmed)
            {
                Output.Write("Weapon => ");
                DescribeItem(inv.Weapon);
            }
            foreach (Armor armor in inv.Armor)
            {
                Output.Write(armor.EquipSpot, " => ");
                DescribeItem(armor);
            }
            if (inv.HeldItem is not null)
            {
                Output.Write("Held Item => ");
                DescribeItem(inv.HeldItem);
            }
        }

        public static void DescribeItem(Item item)
        {
            Output.Write(item, " => ", item.Description, " ");
            if (item is Weapon weapon)
            {
                Output.Write("Damage: ", weapon.Damage, " hp, ");
                Output.Write("Hit Chance: ", weapon.Accuracy * 100, "%, ");
                if (weapon.BlockChance != 0)
                {
                    Output.Write(", BlockChance: ", weapon.BlockChance * 100, "%, ");
                }
            }
            else if (item is Armor armor)
            {
                if (armor.Rating != 0)
                    Output.Write("Defense: ", armor.Rating * 100, "%, ");

                if (armor.Insulation != 0)
                    Output.Write("Warmth: ", armor.Insulation, "F, ");
            }

            if (item.Weight != 0)
            {
                Output.Write("Weight: ", item.Weight, "kg");
            }
            Output.WriteLine();
        }

        public static void DescribeSkills(Player player)
        {
            player._skillRegistry.Describe();

        }
    }
}
