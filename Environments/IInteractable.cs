namespace text_survival_rpg_web.Environments
{
    public interface IInteractable
    {
        string Name { get; }
        public void Interact(Player player);
    }
}
