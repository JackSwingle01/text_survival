using System.Diagnostics;
using System.Dynamic;
using text_survival.Actors;
using text_survival.Environments.Locations;
using text_survival.Interfaces;
using text_survival.IO;
using text_survival.Items;

namespace text_survival.Environments;

public class Location : IPlace, IInteractable
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
    virtual protected IPlace? Parent { get; set; }
    public bool Visited { get; set; }
    public double TemperatureModifier { get; protected set; }
    public const bool IsShelter = false;
    public bool IsFound { get; set; } = false;
    public List<IInteractable> Things { get; set; } = [];
    protected List<Location> ChildLocations => Things.OfType<Location>().ToList();
    public Area ParentArea
    {
        get
        {
            if (Parent is Area a)
            {
                return a;
            }
            else if (Parent is Location l)
            {
                return l.ParentArea;
            }
            else throw new Exception("Parent is not an Area or Location");
        }
    }

   
    #region Initialization
    public Location (string name, IPlace parent, int numItems = 0, int numNpcs = 0) : this(parent, numItems, numNpcs)
    {
        Name = name;
        InitializeLoot(numItems);
        InitializeNpcs(numNpcs);
    }
    public Location(IPlace parent, int numItems = 0, int numNpcs = 0) : this(numItems, numNpcs)
    {
        Parent = parent;
        Name = "Location Placeholder Name";
    }
    public Location(int numItems = 0, int numNpcs = 0)
    {
        Parent = null;
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
            PutThing(spawner.GenerateRandomNpc());
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
    protected virtual List<Item> itemList { get;} = [];
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

    public virtual void Enter(Player player)
    {
        Output.WriteLine("You go to the ", this);
        player.MoveTo(this);
    }

    public virtual void Leave(Player player) => player.MoveTo(Parent);

    public void Interact(Player player) => Enter(player);
    public Command<Player> InteractCommand => new("Go to " + Name, Interact);
    public Command<Player> LeaveCommand => new("Leave " + Name, Leave);

    

    public void Update()
    {
        List<IUpdateable> updateables = new List<IUpdateable>(GetUpdateables);
        foreach (var updateable in updateables)
        {
            updateable.Update();
        }
    }
    protected List<IUpdateable> GetUpdateables => Things.OfType<IUpdateable>().ToList();

    public virtual double GetTemperature()
    {
        return GetTemperature(IsShelter);
    }

    protected double GetTemperature(bool indoors = false)
    {
        if (!indoors) // only check if no child so far is indoors otherwise it propagates all the way up
        {
            indoors = IsShelter;
        }
        double temperature;
        if (Parent is Area a) // base case - the parent is the area root node
        {
            double modifier = a.GetTemperatureModifer();
            modifier = indoors ? modifier / 2 : modifier;
            temperature = a.BaseTemperature + modifier;
        }
        else if (Parent is Location l) // recursive case - the parent is another location
        {
            temperature = l.GetTemperature(indoors);
        }
        else throw new Exception("Parent is not an Area or Location"); // in the future there may be more types of places 
        return temperature + TemperatureModifier;
    }

   
    public void GenerateSubLocation(LocationType type, int numItems = 0, int numNpcs = 0)
    {
        Location location = type switch 
        {
            LocationType.Cave => new Cave(this, numItems, numNpcs),
            LocationType.Trail => new Trail(this, numItems, numNpcs),
            LocationType.River => new River(this, numItems, numNpcs),
            LocationType.FrozenLake => new FrozenLake(this, numItems, numNpcs),
            _ => throw new Exception("Invalid LocationType")
        };
        PutThing(location);
    }
    


    public override string ToString() => Name;
}