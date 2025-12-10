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
        /// Mass of fuel in kg (for fuel items only). 0 for non-fuel items.
        /// </summary>
        public double FuelMassKg { get; set; } = 0;

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
            // Cast to nullable to ensure FirstOrDefault returns null when not found
            return CraftingProperties.Cast<ItemProperty?>().FirstOrDefault(p => p == property);
        }

        public bool HasProperty(ItemProperty name, double minAmount = 0)
        {
            var property = GetProperty(name);
            return property != null && Weight >= minAmount;
        }

        /// <summary>
        /// Get the fuel type for this item based on its crafting properties.
        /// Returns null if item is not a fuel.
        /// </summary>
        public FuelType? GetFuelType()
        {
            // Map ItemProperty fuel properties to FuelType enum
            if (HasProperty(ItemProperty.Fuel_Tinder)) return FuelType.Tinder;
            if (HasProperty(ItemProperty.Fuel_Kindling)) return FuelType.Kindling;
            if (HasProperty(ItemProperty.Fuel_Softwood)) return FuelType.Softwood;
            if (HasProperty(ItemProperty.Fuel_Hardwood)) return FuelType.Hardwood;
            if (HasProperty(ItemProperty.Fuel_Bone)) return FuelType.Bone;
            if (HasProperty(ItemProperty.Fuel_Peat)) return FuelType.Peat;

            return null; // Not a fuel item
        }

        /// <summary>
        /// Check if this item is a fuel item
        /// </summary>
        public bool IsFuel() => GetFuelType().HasValue;

    }
}
