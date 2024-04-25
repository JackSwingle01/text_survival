using text_survival.IO;

namespace text_survival
{
    public class Program
    {
        static void Main()
        {
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
            while (player.IsAlive)
            {
                actions.Act();
            }
        }
    }
}