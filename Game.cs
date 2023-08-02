using text_survival.Environments;
using text_survival.Items;

namespace text_survival
{
    internal class Game
    {
        Player player;
        public Game()
        {
            player = new Player(World.Areas[0]);
            World.Time = new TimeOnly(hour: 9, minute: 0);
        }
        public void Start()
        {
            Actions actions = new Actions(player);
            while (player.Health > 0)
            {
                Act(actions);
            }
        }
        public void Act(Actions actions)
        {
            actions.UpdatePossibleActions();
            actions.Act();
        }
       
    }
}
