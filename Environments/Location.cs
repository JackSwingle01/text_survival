using text_survival.Actors;
using text_survival.Interfaces;
using text_survival.IO;

namespace text_survival.Environments;

public class Location : IInteractable, IPlace
{
    public string Name { get; set; }
    public LocationType Type { get; set; }
    public bool IsFound { get; set; }
    public List<IInteractable> Things { get; }
    private IPlace Parent { get; }

    public bool IsShelter => IsLocationShelter(Type);
    public List<Npc> Npcs => Things.OfType<Npc>().Where(npc => npc.IsAlive).ToList();
    private List<Location> ChildLocations => Things.OfType<Location>().ToList();
    public List<IUpdateable> GetUpdateables => Things.OfType<IUpdateable>().ToList();
    public bool IsSafe => !Npcs.Any(npc => npc.IsHostile);
    public double TemperatureModifier { get; private set; }
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


    public Location(string name, IPlace parent)
    {
        Name = name;
        Things = [];
        Parent = parent;
    }

    public enum LocationType
    {
        AbandonedBuilding,
        Cave,
        Road,
        River,
        Lake,
    }

    public void Enter(Player player)
    {
        Output.WriteLine("You go to the ", this);
        player.MoveTo(this);
    }

    public void Leave(Player player)
    {
        player.MoveTo(Parent);
    }

    public void PutThing(IInteractable thing)
    {
        Things.Add(thing);
    }

    public void Interact(Player player)
    {
        this.Enter(player);
    }
    public Command<Player> InteractCommand => new("Go to " + Name, Interact);
    public Command<Player> LeaveCommand => new("Leave " + Name, Leave);
    public override string ToString() => Name;

    public void Update()
    {
        List<IUpdateable> updateables = new List<IUpdateable>(GetUpdateables);
        foreach (var updateable in updateables)
        {
            updateable.Update();
        }
    }
    public bool ContainsThing(IInteractable thing) => Things.Contains(thing);

    public double GetTemperature()
    {
        return GetTemperature(IsShelter);
    }

    public double GetTemperature(bool indoors = false)
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

    private static bool IsLocationShelter(LocationType type)
    {
        return type switch
        {
            LocationType.AbandonedBuilding => true,
            LocationType.Cave => true,
            _ => false,
        };
    }


    public void GenerateSubLocation(LocationType type, int numItems = 0, int numNpcs = 0)
    {
        Things.Add(LocationFactory.GenerateLocation(type, this, numItems, numNpcs));
    }
}