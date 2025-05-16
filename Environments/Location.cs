using System.Reflection.Metadata.Ecma335;
using text_survival.Actors;
using text_survival.Environments.Locations;
using text_survival.Interfaces;
using text_survival.IO;
using text_survival.Items;

namespace text_survival.Environments;

public class Location : Place
{
    public LocationType Type { get; set; }
    public enum LocationType
    {
        None,
        Cave,
        Trail,
        River,
        FrozenLake,
    }
    public double TemperatureModifier { get; protected set; }
    public const bool IsShelter = false;
    public bool IsFound { get; set; } = false;
    public virtual bool IsSafe => !Npcs.Any(npc => npc.IsHostile);
    public List<Npc> Npcs = [];
    public List<Item> Items = [];
    public List<Container> Containers = [];
    virtual public Location? Parent { get; set; }
    public List<IInteractable> Things
    {
        get 
        {
            var things = new List<IInteractable>();
            things.AddRange(Npcs.OfType<IInteractable>());
            things.AddRange(Locations.OfType<IInteractable>());
            things.AddRange(Containers.OfType<IInteractable>());
            things.AddRange(Items.OfType<IInteractable>());
            return things;
        }
    }
    public Zone ParentZone { get; }
    protected virtual ForageModule ForageModule { get; set; } = new();
    protected LootTable LootTable;


    #region Initialization
    public Location(string name, Place parent, int numItems = 0, int numNpcs = 0) : this(parent, numItems, numNpcs)
    {
        Name = name;
        InitializeLoot(numItems);
        InitializeNpcs(numNpcs);
    }
    public Location(Place parent, int numItems = 0, int numNpcs = 0)
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
        LootTable = new();
        NpcSpawner = new();
        InitializeLoot(numItems);
        InitializeNpcs(numNpcs);
    }

    public static readonly List<string> genericLocationAdjectives = ["", "Old", "Dusty", "Cool", "Breezy", "Quiet", "Ancient", "Ominous", "Sullen", "Forlorn", "Desolate", "Secret", "Hidden", "Forgotten", "Cold", "Dark", "Damp", "Wet", "Dry", "Warm", "Icy", "Snowy", "Frozen"];

    protected void InitializeLoot(int numItems)
    {
        if (!LootTable.IsEmpty())
        {
            for (int i = 0; i < numItems; i++)
            {
                Items.Add(LootTable.GenerateRandomItem());
            }
        }
    }
    protected void InitializeNpcs(int numNpcs)
    {
        for (int i = 0; i < numNpcs; i++)
        {
            var npc = NpcSpawner.GenerateRandomNpc();
            if (npc is not null)
                Npcs.Add(npc);
        }
    }
    protected virtual NpcSpawner NpcSpawner { get; }


    #endregion Initialization
    public void Interact(Player player)
    {
        Output.WriteLine("You consider heading to the " + Name + "...");
        Output.WriteLine("It is a " + Type + ".");
        Output.WriteLine("Do you want to go there? (y/n)");
        if (Input.ReadYesNo())
        {
            player.CurrentLocation = this;
        }
        else
        {
            Output.WriteLine("You decide to stay.");
        }
    }
    public Command<Player> InteractCommand => new("Go to " + Name + (Visited ? " (Visited)" : ""), Interact);


    public override double GetTemperature() => GetTemperature(IsShelter);

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


    public static void GenerateSubLocation(Place parent, LocationType type, int numItems = 0, int numNpcs = 0)
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

    public override void Update()
    {
        Locations.ForEach(i => i.Update());
        Npcs.ForEach(n => n.Update());
    }


    public void Forage(int hours)
    {
        ForageModule.Forage(hours);
        var itemsFound = ForageModule.GetItemsFound();
        if (itemsFound.Any())
        {
            foreach (var item in itemsFound)
            {
                Items.Add(item);
                item.IsFound = true;
            }
        }
    }

    public override string ToString() => Name;
}