using text_survival_rpg_web.Interfaces;

namespace text_survival_rpg_web.Environments
{
    public interface IPlace
    {
        public string Name { get; set; }
        public void Enter(Player player);
        public void Leave(Player player);
        public List<IInteractable> Things { get; }
    }
}
