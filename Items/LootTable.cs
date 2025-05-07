using System.Linq.Expressions;

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
        public bool IsEmpty()
        {
            return items.Count == 0;
        }
        public Item GenerateRandomItem()
        {
            var loot = Utils.GetRandomFromList(items).Clone();
            return loot;
        }
    }
}
