using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace text_survival
{
    public class ItemPool
    {
        private List<Item> _items = new();
        private List<Item> Items { get => _items; set => _items = value; }
        public ItemPool(){}
        public ItemPool(List<Item> items)
        {
            Items = items;
        }

        public void Add(Item item)
        {
            Items.Add(item);
        }
        public void Add(List<Item> items)
        {
            Items.AddRange(items);
        }

        public void Remove(Item item)
        {
              Items.Remove(item);
        }
        public void RemoveAt(int index)
        {
            Items.RemoveAt(index);
        }
        public Item GetItem(int index)
        {
            return Items[index];
        }

        public void Print()
        {
            foreach (Item item in Items)
            {
                Utils.Write(item.ToString());
            }
        }

        public void Print(int index)
        {
            Utils.Write(Items[index].ToString());
        }

        public Item GetRandomItem()
        {
            Random rand = new Random();
            int index = rand.Next(Items.Count);
            return Items[index];
        }

    }
}
