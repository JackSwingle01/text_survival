using text_survival_rpg_web.Actors;
using text_survival_rpg_web.Interfaces;

namespace text_survival_rpg_web.Environments
{
    public interface IPlace : IUpdateable
    {
        public string Name { get; set; }
        public void Enter(Player player);
        public void Leave(Player player);
        public List<IInteractable> Things { get; }
        public List<Npc> Npcs { get; }
        public bool IsSafe { get; }
        public void PutThing(IInteractable thing);
        public bool ContainsThing(IInteractable thing);
        public List<IUpdateable> GetUpdateables { get; }
    }
}
