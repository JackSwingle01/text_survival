using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
namespace text_survival
{
    public class Inventory : Container
    {
        public Inventory(string name, float maxWeight) : base(name, maxWeight)
        {
        }

        // Override the Add method
        public override void Add(Item item)
        {
            while (item.Weight + GetWeight() > MaxWeight)
            {
                Utils.Write("The ", this, " is full!\n");
                Utils.Write("Would you like to drop an item to make space? 1. Yes 2. No\n");

                // Get the user's answer
                int input = Utils.ReadInt(1, 2);
                if (input == 1)
                {
                    // If the user answered '1' (yes), let them select an item to drop
                    Utils.Write("Enter the number of the item you want to drop.\n");
                    int index = Utils.ReadInt(1, Items.Count) - 1;

                    // Drop the selected item
                    var droppedItem = GetItem(index);
                    if (droppedItem != null)
                    {
                        Utils.Write("You dropped the ", droppedItem, ".\n");
                        Remove(droppedItem);
                    }
                }
                else
                {
                    // If the user answered '2' (no), break the loop and don't add the item
                    break;
                }
            }

            // If there's enough space, add the item
            if (item.Weight + GetWeight() <= MaxWeight)
            {
                Items.Add(item);
                Utils.Write("You added the ", item, " to your inventory.\n");
            }
            else
            {
                Utils.Write("The ", this, " is still too full to add the ", item, ".\n");
            }
        }
        public override void Write()
        {
            Utils.Write(this.Name + ":\n");
            if (Items.Count == 0)
            {
                Utils.Write(Name, " is empty!\n");
            }
            else
            {
                // Use a dictionary to keep track of quantities
                Dictionary<string, int> quantities = new Dictionary<string, int>();

                // Iterate through all items, incrementing the corresponding quantity in the dictionary
                foreach (Item item in Items)
                {
                    if (quantities.ContainsKey(item.Name))
                    {
                        quantities[item.Name]++;
                    }
                    else
                    {
                        quantities.Add(item.Name, 1);
                    }
                }

                // Now write out each item and its quantity
                foreach (KeyValuePair<string, int> kvp in quantities)
                {
                    Utils.Write(kvp.Key);
                    if (kvp.Value > 1)
                    {
                        Utils.Write(" x " + kvp.Value.ToString());
                    }
                    Utils.Write("\n");
                }
            }
        }

    }
}
