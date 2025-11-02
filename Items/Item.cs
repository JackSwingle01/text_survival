using text_survival.Crafting;
using text_survival.IO;

namespace text_survival.Items
{
    public class Item(string name, double weight = 1)
    {
        public string Name { get; set; } = name;
        public double Weight { get; set; } = weight;
        public string Description { get; set; } = "";
        public bool IsFound { get; set; }
        public int NumUses { get; set; } = -1; // -1 is not consumable/durable
        private int InitialUses { get; set; } = -1; // Track max uses for display
        public List<ItemProperty> CraftingProperties = [];

        /// <summary>
        /// Use the item once, decrementing durability. Returns true if item broke.
        /// </summary>
        public bool UseOnce()
        {
            if (NumUses <= 0) return true; // Already broken
            if (NumUses == -1) return false; // Not consumable

            NumUses--;
            return NumUses <= 0; // Broke on this use
        }

        /// <summary>
        /// Set item as durable/consumable with uses
        /// </summary>
        public void SetDurability(int uses)
        {
            NumUses = uses;
            InitialUses = uses;
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
            // Show durability for tools/consumables
            if (NumUses > 0 && InitialUses > 0)
            {
                return $"{Name} ({NumUses}/{InitialUses} uses)";
            }
            else if (NumUses == 0)
            {
                return $"{Name} (broken)";
            }
            return Name;
        }

        public ItemProperty? GetProperty(ItemProperty property)
        {
            return CraftingProperties.FirstOrDefault(p => p == property);
        }

        public bool HasProperty(ItemProperty name, double minAmount = 0)
        {
            var property = GetProperty(name);
            return property != null && Weight >= minAmount;
        }

    }
}
