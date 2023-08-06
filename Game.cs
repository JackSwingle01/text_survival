using text_survival.Environments;
using text_survival.Items;

namespace text_survival
{
    internal class Game
    {
        private Player _player;
        public Game()
        {
            _player = new Player(AreaFactory.GenerateArea(Area.EnvironmentType.Forest));
            World.Time = new TimeOnly(hour: 9, minute: 0);
        }
        public void Start()
        {
            Actions actions = new Actions(_player);
            while (_player.Health > 0)
            {
                actions.Act();
            }
        }
      
       
    }
}
