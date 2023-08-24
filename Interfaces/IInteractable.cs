namespace text_survival_rpg_web.Interfaces
{
    public interface IInteractable
    {
        string Name { get; }
        public void Interact(Player player);
        public Command<Player> InteractCommand { get; }
        public bool IsFound { get; set; }
    }
}
