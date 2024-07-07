using text_survival.Actors;
using text_survival.Environments.Locations;
using text_survival.Interfaces;
using text_survival.Items;

namespace text_survival.Environments;

public class Location : IPlace, IInteractable, IHasThings, IHasNpcs
{
    public string Name { get; set; }
    public LocationType Type { get; set; }
    public enum LocationType
    {
        Cave,
        Trail,
        River,
        FrozenLake,
    }
    public bool Visited { get; set; }
    public double TemperatureModifier { get; protected set; }
    public const bool IsShelter = false;
    public bool IsFound { get; set; } = false;
    public virtual bool IsSafe => !Npcs.Any(npc => npc.IsHostile);
    public List<IInteractable> Things { get; set; } = [];
    public List<Npc> Npcs => Things.OfType<Npc>().ToList();
    public List<Item> Items => Things.OfType<Item>().ToList();
    public List<Location> Locations => Things.OfType<Location>().ToList();
    virtual public Location? Parent { get; set; }
    public Zone ParentZone { get; }
    protected virtual ForageModule ForageModule { get; set; } = new();
   


    #region Initialization
    public Location(string name, IPlace parent, int numItems = 0, int numNpcs = 0) : this(parent, numItems, numNpcs)
    {
        Name = name;
        InitializeLoot(numItems);
        InitializeNpcs(numNpcs);
    }
    public Location(IPlace parent, int numItems = 0, int numNpcs = 0)
    {
        if (parent is Zone z)
        {
            ParentZone = z;
            Parent = null;
        }
        else if (parent is Location l)
        {
            Parent = l;
            ParentZone = l.ParentZone;
        }
        else throw new NotImplementedException("Unknown parent type");

        Name = "Location Placeholder Name";
        InitializeLoot(numItems);
        InitializeNpcs(numNpcs);
    }

    public static readonly List<string> genericLocationAdjectives = ["", "Old", "Dusty", "Cool", "Breezy", "Quiet", "Ancient", "Ominous", "Sullen", "Forlorn", "Desolate", "Secret", "Hidden", "Forgotten", "Cold", "Dark", "Damp", "Wet", "Dry", "Warm", "Icy", "Snowy", "Frozen"];

    protected void InitializeLoot(int numItems)
    {
        LootTable lootTable = CreateLootTable();
        for (int i = 0; i < numItems; i++)
        {
            PutThing(lootTable.GenerateRandomItem());
        }
    }
    protected void InitializeNpcs(int numNpcs)
    {
        NpcSpawner spawner = CreateNpcSpawner();
        for (int i = 0; i < numNpcs; i++)
        {
            var npc = spawner.GenerateRandomNpc();
            if (npc is not null)
                PutThing(npc);
        }
    }
    protected virtual List<Npc> npcList { get; } = [];
    protected NpcSpawner CreateNpcSpawner()
    {
        NpcSpawner npcs = new();
        foreach (Npc npc in npcList)
        {
            npcs.Add(npc);
        }
        return npcs;
    }
    protected virtual List<Item> itemList { get; } = [];
    protected LootTable CreateLootTable()
    {
        LootTable lootTable = new();
        foreach (Item item in itemList)
        {
            lootTable.AddLoot(item);
        }
        return lootTable;
    }


    #endregion Initialization

    public void PutThing(IInteractable thing) => Things.Add(thing);
    public void RemoveThing(IInteractable thing) => Things.Remove(thing);
    public bool ContainsThing(IInteractable thing) => Things.Contains(thing);

    public void Interact(Player player) => player.CurrentLocation = this;
    public Command<Player> InteractCommand => new("Go to " + Name + (Visited ? " (Visited)" : ""), Interact);



    public virtual double GetTemperature() => GetTemperature(IsShelter);

    protected double GetTemperature(bool indoors = false)
    {
        if (!indoors) // only check if no child so far is indoors otherwise it propagates all the way up
        {
            indoors = IsShelter;
        }
        double temperature;
        if (Parent is null) // base case - the parent is the area root node
        {
            double modifier = ParentZone.GetTemperatureModifer();
            modifier = indoors ? modifier / 2 : modifier;
            temperature = ParentZone.BaseTemperature + modifier;
        }
        else  // recursive case - the parent is another location
        {
            temperature = Parent.GetTemperature(indoors);
        }
        return temperature + TemperatureModifier;
    }


    public static void GenerateSubLocation(IPlace parent, LocationType type, int numItems = 0, int numNpcs = 0)
    {
        Location location = type switch
        {
            LocationType.Cave => new Cave(parent, numItems, numNpcs),
            LocationType.Trail => new Trail(parent, numItems, numNpcs),
            LocationType.River => new River(parent, numItems, numNpcs),
            LocationType.FrozenLake => new FrozenLake(parent, numItems, numNpcs),
            _ => throw new Exception("Invalid LocationType")
        };
        parent.PutLocation(location);
    }

    public virtual void Update()
    {
        List<IUpdateable> updateables = Things.OfType<IUpdateable>().ToList();
        foreach (var updateable in updateables)
        {
            updateable.Update();
        }
    }

    public void PutLocation(Location location) => PutThing(location);

    public void Forage(int hours)
    {
        ForageModule.Forage(hours);
        var itemsFound = ForageModule.GetItemsFound();
        if (itemsFound.Any())
        {
            foreach (var item in itemsFound)
            {
                PutThing(item);
                item.IsFound = true;
            }
        }
    }

    public override string ToString() => Name;
}