namespace text_survival
{
    internal class Game
    {
        List<Place> places = new List<Place>();
        Place currentPlace;
        Player player;
        public Game()
        {
            places.Add(Places.GetForest());
            places.Add(Places.GetShack());
            places.Add(Places.GetCave());
            places.Add(Places.GetRiver());
            currentPlace = places[0];
            player = new Player(currentPlace);
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
            Utils.Write("Location: ", currentPlace, "\n");
            Utils.Write("Time: ", World.Time, " Temp: ", currentPlace.GetTemperature(), "°F\n");
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
                currentPlace.Explore(player);
            }
            else if (input == 2)
            {
                Utils.Write(player.EquipedItemsToString());
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
            List<Place> options = new List<Place>();
            options.AddRange(places.FindAll(p => p != currentPlace));
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
                    currentPlace = options[index - 1];
                    player.Location = currentPlace;
                    Utils.Write("You are now at ", currentPlace.Name,"\n");
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
