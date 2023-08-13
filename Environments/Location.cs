using System.Transactions;
using text_survival.Actors;
using text_survival.Items;

namespace text_survival.Environments;

public class Location
{
    public string Name { get; set; }
    public List<Item> Items { get; private set; }
    public List<Npc> Npcs { get; private set; }

    public Location(string name)
    {
        Name = name;
        Items = new List<Item>();
        Npcs = new List<Npc>();
    }

//    public void Enter(Player player)
//    {
//        // Existing logic...
//    }
}