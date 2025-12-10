using text_survival.Actors.Player;
using text_survival.Actors.NPCs;
using text_survival.Crafting;
using text_survival.Environments;

namespace text_survival.Actions;

public class GameContext(Player player)
{
    public Player player = player;
    public Location currentLocation => player.CurrentLocation;
    public IGameAction? NextActionOverride { get; set; }
    public Npc? EngagedEnemy;
    public CraftingSystem CraftingManager = new CraftingSystem(player); 
}
