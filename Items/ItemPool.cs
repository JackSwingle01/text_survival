namespace text_survival.Items
{
    public class ItemPool
    {
        private List<Func<Item>> ItemMethods { get; set; }
        public ItemPool()
        {
            ItemMethods = new List<Func<Item>>();
        }
        
        public void Add(Func<Item> item)
        {
            ItemMethods.Add(item);
        }

        public void Remove(Func<Item> itemFactoryMethod)
        {
            ItemMethods.Remove(itemFactoryMethod);
        }
        public Item GetRandomItem()
        {
            Random rand = new Random();
            int index = rand.Next(ItemMethods.Count);
            return ItemMethods[index].Invoke();
        }

    }
}
