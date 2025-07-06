using text_survival.Crafting;
using text_survival.IO;

namespace text_survival.Items
{
    public class Item
    {
        public string Name { get; set; }
        public double Weight { get; set; } // in kg
        public string Description { get; set; } = "";
        public double Quality { get; set; } // percentage 0% being extremely poor quality, 100% being perfect quality
        public bool IsFound { get; set; }
        public int NumUses { get; set; }
        public List<ItemCraftingProperty> CraftingProperties = [];

        public Item(string name, double weight = 1, int quality = 50)
        {
            Name = name;
            Weight = weight;
            Quality = quality;
            NumUses = -1; // not consumable
        }

        public void Describe()
        {
            Output.Write(this, " => ", Description, " ");
            if (this is Weapon weapon)
            {
                Output.Write("Damage: ", weapon.Damage, " hp, ");
                Output.Write("Hit Chance: ", weapon.Accuracy * 100, "%, ");
                if (weapon.BlockChance != 0)
                {
                    Output.Write(", BlockChance: ", weapon.BlockChance * 100, "%, ");
                }
            }
            else if (this is Armor armor)
            {
                if (armor.Rating != 0)
                    Output.Write("Defense: ", armor.Rating * 100, "%, ");

                if (armor.Insulation != 0)
                    Output.Write("Warmth: ", armor.Insulation, "F, ");
            }

            if (Weight != 0)
            {
                Output.Write("Weight: ", Weight, "kg");
            }
            Output.WriteLine();
        }
        public override string ToString()
        {
            return Name;
        }

        public ItemCraftingProperty? GetProperty(string name)
        {
            return CraftingProperties.FirstOrDefault(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        }

        public bool HasProperty(string name, double minAmount = 0, double minQuality = 0)
        {
            var property = GetProperty(name);
            return property != null && property.Quality >= minAmount && property.Quality >= minQuality;
        }

    }
}
