using text_survival.Environments;

namespace text_survival
{
    public class Program
    {
        static void Main()
        {
            Utils.WriteLine("You have been banished from your home city.");
            Thread.Sleep(1000);
            Utils.WriteLine("Stripped of your possessions, you've been left to fend for yourself in the unforgiving wilderness.");
            Thread.Sleep(1000);
            Utils.WriteLine("The ancient laws, however, grant one path to redemption.");
            Thread.Sleep(1000);
            Utils.WriteLine("You must defeat a Dragon...\n");
            Thread.Sleep(2000);
            Player player = new Player(AreaFactory.GenerateArea(Area.EnvironmentType.Forest));
            World.Time = new TimeOnly(hour: 9, minute: 0);
            Actions actions = new(player);
            while (player.Health > 0)
            {
                actions.Act();
            }
        }
    }
}