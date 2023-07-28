using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace text_survival
{
    public class Container
    {
        public float MaxWeight { get; set; }
        private List<Item> Items { get; set; }
        public string Name { get; set; }

        public Container(string name, float maxWeight)
        {
            Name = name;
            MaxWeight = maxWeight;
            Items = new List<Item>();
        }

        public Item? GetItemFromInventory(int index)
        {
            if (index < 0 || index >= Items.Count)
            {
                return null;
            }
            return Items[index];
        }

        public Item? Open()
        {
            Utils.Write(this.ToString());
            if (Items.Count == 0)
            {
                return null;
            }
            Utils.Write("Enter the number of the item you want or type 'exit' to exit");
            string? input = Console.ReadLine();
            if (input == "exit" || input == null)
            {
                return null;
            }
            int index = int.Parse(input) - 1;
            return GetItemFromInventory(index);

        }

        public override string ToString()
        {
            Utils.Write(this.Name + ":");
            if (Items.Count == 0)
            {
                return this.Name + " is empty!";
            }
            string str = "";
            int count = 1;
            foreach (Item item in Items)
            {
                str += count + ". ";
                str += item.ToString();
                str += "\n";
                count++;
            }
            return str;
        }

        public void Add(Item item)
        {
            if (item.Weight + GetWeight() > MaxWeight)
            {
                Utils.Write("You can't carry that much!");
                return;
            }
            Items.Add(item);
        }

        public void Remove(Item item)
        {
            Items.Remove(item);
        }


        public float GetWeight()
        {
            float sum = 0;
            foreach (Item item in Items)
            {
                sum += item.Weight;
            }
            return sum;
        }


    }
}
