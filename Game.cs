using text_survival.IO;

namespace text_survival
{
    public class Game
    {
        public Game()
        {
            OutputQueue = new Queue<string>();
        }

        public Queue<string> OutputQueue;
        public Queue<string> InputQueue;
        public async Task StartGame()
        {

            var game = new Game();
            Output.WriteLine("You have been banished from your home city.");
            Thread.Sleep(1000);
            Output.WriteLine("Stripped of your possessions, you've been left to fend for yourself in the unforgiving wilderness.");
            Thread.Sleep(1000);
            Output.WriteLine("The ancient laws, however, grant one path to redemption:");
            Thread.Sleep(1000);
            Output.WriteLine("To kill a Dragon...\n");
            Thread.Sleep(2000);

            Player player = World.Player;
            Actions actions = new(player);
            while (player.Health > 0)
            {
                actions.Act();
            }
        }

        public void OnWriteEvent(WriteEvent e)
        {
            OutputQueue.Enqueue(e.Message);
        }

        public void OnInputEvent(InputEvent e)
        {
            InputQueue.Enqueue(e.Input);
        }

    }
}
