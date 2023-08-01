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
            while (player.Health > 0)
            {
                Act();
            }
        }
        public void Act()
        {
            player.WriteSurvivalStats();
            Actions actions = new Actions(player);
            actions.UpdatePossibleActions();
            actions.Act();
            //Utils.Write("CurrentArea: ", player.CurrentArea, "\n");
            //Utils.Write("Time: ", World.Time, " Temp: ", player.CurrentArea.GetTemperature(), "°F\n");
            //Utils.Write("What would you like to do?\n");
            //Utils.Write("1. Explore\n");
            //Utils.Write("2. Use an item\n");
            //Utils.Write("3. Travel\n");
            //Utils.Write("4. Sleep\n");
            //Utils.Write("8. Check Equipment\n");
            //Utils.Write("9. Quit\n");
            //int input = Utils.ReadInt();
            //switch (input)
            //{
            //    case 1 when player.CurrentLocation == null:
            //        player.CurrentArea.Explore(player);
            //        break;
            //    case 1:
            //        player.CurrentLocation.Explore(player);
            //        break;
            //    case 2:
            //    {
            //        Item? item = player.Inventory.Open();
            //        item?.Use(player);
            //        break;
            //    }
            //    case 3:
            //        Travel(player);
            //        break;
            //    case 4:
            //        Utils.Write("How many hours would you like to sleep?\n");
            //        player.Sleep(Utils.ReadInt() * 60);
            //        break;
            //    case 8:
            //        player.WriteEquipedItems();
            //        Utils.Write("Press any key to continue\n");
            //        Utils.Read();
            //        break;
            //    case 9:
            //        player.Damage(999);
            //        break;
            //    default:
            //        Utils.Write("Invalid input\n");
            //        break;
            //}

        }
       
    }
}
