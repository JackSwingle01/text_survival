using text_survival.Environments;
using text_survival.Items;

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
            Utils.WriteLine("The ancient laws, however, grant one path to redemption:");
            Thread.Sleep(1000);
            Utils.WriteLine("To kill a Dragon...\n");
            Thread.Sleep(2000);
            Area startingArea = new Area("Clearing", "A small clearing in the forest.");
            startingArea.Items.Add(new Weapon(WeaponType.Dagger, WeaponMaterial.Iron, "Old dagger", 40));
            Player player = new Player(startingArea);
            World.Time = new TimeOnly(hour: 9, minute: 0);
            Actions actions = new(player);
            while (player.Health > 0)
            {
                actions.Act();
            }
        }
    }
}