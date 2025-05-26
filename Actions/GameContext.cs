using text_survival.Environments;

namespace text_survival.Actions;

public class GameContext(Player player)
{
    public Player player = player;
    public Location currentLocation => player.CurrentLocation;
}
