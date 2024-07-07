using text_survival.Actors;
using text_survival.Interfaces;

namespace text_survival.Environments
{
    public interface IPlace : IUpdateable
    {
        string Name { get; set; }
        void Enter(Player player);
        void Leave(Player player);
        List<IInteractable> Things { get; }
        List<Npc> Npcs => Things.OfType<Npc>().Where(npc => npc.IsAlive).ToList();
        bool IsSafe => !Npcs.Any(npc => npc.IsHostile);
        virtual void PutThing(IInteractable thing) => Things.Add(thing);
        void RemoveThing(IInteractable thing) => Things.Remove(thing);
        bool ContainsThing(IInteractable thing) => Things.Contains(thing);
        List<IUpdateable> GetUpdateables => Things.OfType<IUpdateable>().ToList();
        double GetTemperature();

    }
}
