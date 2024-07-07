namespace text_survival.Items
{
    public class LootTable
    {
        private List<Item> items = [];
        public LootTable()
        {
        }
        public LootTable(List<Item> items)
        {
            this.items = items;
        }
        public void AddLoot(Item loot)
        {
            items.Add(loot);
        }
        public Item? GenerateRandomItem()
        {
            var loot = Utils.GetRandomFromList(items)?.Clone();
            if (loot is Item i)
            {
                return i;
            }
            return null;
        }
    }
}
