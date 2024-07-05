using text_survival.IO;

namespace text_survival
{
    public class Program
    {
        static void Main()
        {
            Output.SleepTime = 500;
            Output.WriteLine("You have been banished from your home city.");
            Output.WriteLine("Stripped of your possessions, you've been left to fend for yourself in the unforgiving wilderness.");
            Output.WriteLine("The ancient laws, however, grant one path to redemption:");
            Output.WriteLine("To kill a Dragon...\n");
            Output.SleepTime = 10;

            Player player = World.Player;
            Actions actions = new(player);
            while (player.IsAlive)
            {
                actions.Act();
            }
        }
    }
}