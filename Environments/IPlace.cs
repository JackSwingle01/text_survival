using text_survival.Actors;
using text_survival.Interfaces;
using text_survival.Items;

namespace text_survival.Environments
{
    public interface IPlace : IUpdateable, IHasLocations
    {
        string Name { get; set; }
        double GetTemperature();
        bool Visited { get; set; }
    }

    public interface IHasLocations
    {
        List<Location> Locations { get; }
        void PutLocation(Location location);
    }

    public interface IHasThings
    {
        List<IInteractable> Things { get; }
        void PutThing(IInteractable thing);
        void RemoveThing(IInteractable thing);
        bool ContainsThing(IInteractable thing);
    }
    public interface IHasNpcs
    {
        bool IsSafe => !Npcs.Any(npc => npc.IsHostile);
        List<Npc> Npcs { get; }
    }
    public interface IHasItems
    {
        List<Item> Items { get; }
        void PutItem(Item item);
        void RemoveItem(Item item);
        bool ContainsItem(Item item);
    }
}
