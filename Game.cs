using text_survival.Environments;
using text_survival.Items;

namespace text_survival
{
    internal class Game
    {
        List<Area> areas = new List<Area>();


        Player player;
        public Game()
        {
            areas.Add(AreaFactory.GetForest());
            areas.Add(AreaFactory.GetShack());
            areas.Add(AreaFactory.GetCave());
            areas.Add(AreaFactory.GetRiver());
            player = new Player(areas[0]);
            World.Time = new TimeOnly(hour:9, minute:0);
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
            Utils.Write("CurrentArea: ", player.CurrentArea, "\n");
            Utils.Write("Time: ", World.Time, " Temp: ", player.CurrentArea.GetTemperature(), "°F\n");
            Utils.Write("What would you like to do?\n");
            Utils.Write("1. Explore\n");
            Utils.Write("2. Use an item\n");
            Utils.Write("3. Travel\n");
            Utils.Write("4. Sleep\n");
            Utils.Write("8. Check Equipment\n");
            Utils.Write("9. Quit\n");
            int input = Utils.ReadInt();
            if (input == 1)
            {
                if (player.CurrentLocation == null)
                {
                    player.CurrentArea.Explore(player);
                } else
                {
                    player.CurrentLocation.Explore(player);
                }
            }
            else if (input == 2)
            {
                Item? item = player.Inventory.Open();
                item?.Use(player);
            }
            else if (input == 3)
            {
                Travel(player);
            }
            else if (input == 4)
            {
                Utils.Write("How many hours would you like to sleep?\n");
                player.Sleep(Utils.ReadInt()*60);
            }
            else if (input == 8)
            {
                Utils.Write(player.EquipedItemsToString(),"\n");
                Utils.Write("Press any key to continue\n");
                Utils.Read();
            }
            else if (input == 9)
            {
                player.Damage(999);
            }
            else
            {
                Utils.Write("Invalid input\n");
            }

        }
        public void Travel(Player player)
        {
            Utils.Write("Where would you like to go?\n");
            List<Area> options = new List<Area>();
            options.AddRange(areas.FindAll(p => p != player.CurrentArea));
            for (int i = 0; i < options.Count; i++)
            {
                Utils.Write((i + 1) + ". ", options[i],"\n");
            }
            string? input = Utils.Read();
            if (int.TryParse(input, out int index))
            {
                if (index > 0 && index <= options.Count)
                {
                    Utils.Write("You travel for 1 hour\n");
                    player.Update(60);
                    player.CurrentArea = options[index - 1];
                    player.CurrentArea = player.CurrentArea;
                    Utils.Write("You are now at ", player.CurrentArea.Name,"\n");
                }
                else
                {
                    Utils.Write("Invalid input\n");
                }
            }
            else
            {
                Utils.Write("Invalid input\n");
            }
        }
    }
}
