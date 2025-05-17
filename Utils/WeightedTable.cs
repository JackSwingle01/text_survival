using text_survival.Actors;
using text_survival.Environments;
using text_survival.Items;

namespace text_survival;

public class GenericWeightedTable<T> where T : class
{
    protected Dictionary<Func<T>, double> weightedFactories = [];

    public GenericWeightedTable() { }
    public void AddFactory(Func<T> factory, double weight = 1.0)
    {
        if (weight <= 0f)
            throw new ArgumentException("Weight must be greater than zero", nameof(weight));

        weightedFactories[factory] = weight;
    }

    public bool IsEmpty()
    {
        return weightedFactories.Count == 0;
    }

    public virtual T GenerateRandom()
    {
        if (IsEmpty())
            throw new InvalidOperationException("Cannot generate from an empty loot table");

        return Utils.GetRandomWeighted(weightedFactories)();
    }
}

public class LootTable : GenericWeightedTable<Item>
{
    public void AddItem(Func<Item> itemFactory, double weight = 1)
    {
        AddFactory(itemFactory, weight);
    }

    public Item GenerateRandomItem()
    {
        return base.GenerateRandom();
    }
}

public class LocationTable
{
    protected Dictionary<Func<Zone, Location>, double> weightedFactories = [];

    public void AddFactory(Func<Zone, Location> factory, double weight = 1.0)
    {
        if (weight <= 0f)
            throw new ArgumentException("Weight must be greater than zero", nameof(weight));

        weightedFactories[factory] = weight;
    }

    public bool IsEmpty()
    {
        return weightedFactories.Count == 0;
    }

    public virtual Location GenerateRandom(Zone parent)
    {
        if (IsEmpty())
            throw new InvalidOperationException("Cannot generate from an empty loot table");

        return Utils.GetRandomWeighted(weightedFactories)(parent);
    }
}


public class NpcTable : GenericWeightedTable<Npc>
{
    public void AddActor(Func<Npc> actorFactory, double weight = 1)
    {
        AddFactory(actorFactory, weight);
    }
}