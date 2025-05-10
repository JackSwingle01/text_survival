
namespace text_survival.Items;

public class LootTable
{
    private List<Func<Item>> itemFactories = [];
    public LootTable() { }
    public LootTable(List<Func<Item>> factories)
    {
        itemFactories = factories;
    }
    public void AddLootFactory(Func<Item> factory)
    {
        itemFactories.Add(factory);
    }
    public bool IsEmpty()
    {
        return itemFactories.Count == 0;
    }
    public Item GenerateRandomItem()
    {
        var factory = Utils.GetRandomFromList(itemFactories);
        return factory();
    }
}

