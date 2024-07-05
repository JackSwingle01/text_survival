using text_survival.Actors;
using text_survival.Interfaces;
using text_survival.IO;

namespace text_survival.Environments;

public class Location : IInteractable, IPlace
{
    public string Name { get; set; }
    public List<IInteractable> Things { get; }
    public bool IsFound { get; set; }
    public List<Npc> Npcs => Things.OfType<Npc>().Where(npc => npc.IsAlive).ToList();
    public List<IUpdateable> GetUpdateables => Things.OfType<IUpdateable>().ToList();
    public bool IsSafe => !Npcs.Any(npc => npc.IsHostile);
    public Location(string name)
    {
        Name = name;
        Things = [];
    }
    public void Enter(Player player)
    {
        Output.WriteLine("You go to the ", this);
        player.MoveTo(this);
    }

    public void Leave(Player player)
    {
        player.MoveBack();
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
}