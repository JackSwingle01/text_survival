namespace text_survival
{ 
    public class ItemPool
    {
        private List<Item> Items { get; set; }
        public ItemPool() 
        {
            Items = new List<Item>();
        }
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
                item.Write();
            }
        }

        public void Print(int index)
        {
            Items[index].Write();
        }

        public Item GetRandomItem()
        {
            Random rand = new Random();
            int index = rand.Next(Items.Count);
            return Items[index];
        }

        public  List<Item>.Enumerator GetEnumerator()
        {
            return Items.GetEnumerator();
        }
    }
}
