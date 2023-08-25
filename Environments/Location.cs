using text_survival_rpg_web.Actors;
using text_survival_rpg_web.Interfaces;
using text_survival_rpg_web.Items;

namespace text_survival_rpg_web.Environments;

public class Location //: IInteractable, IPlace
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

    
}