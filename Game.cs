﻿namespace text_survival
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
            World.Time = new TimeOnly(hour:12, minute:0);
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
            Utils.Write(player.SurvivalStatsToString(), 100);
            Utils.Write("You are in a " + currentPlace.Name, 100);
            Utils.Write("Its " + World.Time + " and " + currentPlace.GetTemperature() + " degrees");
            Utils.Write("What would you like to do?");
            Utils.Write("1. Explore", 100);
            Utils.Write("2. Use an item", 100);
            Utils.Write("3. Travel", 100);
            Utils.Write("4. Sleep", 100);
            Utils.Write("8. Check Equipment.", 100);
            Utils.Write("9. Quit", 100);
            int input = Utils.ReadInt();
            if (input == 1)
            {
                currentPlace.Explore(player);
            }
            else if (input == 2)
            {
                Utils.Write(player.EquipedItemsToString(), 100);
                Item? item = player.Inventory.Open();
                item?.Use(player);
            }
            else if (input == 3)
            {
                Travel(player);
            }
            else if (input == 4)
            {
                Utils.Write("How many hours would you like to sleep?");
                player.Sleep(Utils.ReadInt()*60);
            }
            else if (input == 8)
            {
                Utils.Write(player.EquipedItemsToString(), 100);
                Utils.Write("Press any key to continue");
                Utils.Read();
            }
            else if (input == 9)
            {
                player.Damage(999);
            }
            else
            {
                Utils.Write("Invalid input");
            }

        }
        public void Travel(Player player)
        {
            Utils.Write("Where would you like to go?");
            List<Place> options = new List<Place>();
            options.AddRange(places.FindAll(p => p != currentPlace));
            for (int i = 0; i < options.Count; i++)
            {
                Utils.Write((i + 1) + ". " + options[i].ToString());
            }
            string? input = Utils.Read();
            if (int.TryParse(input, out int index))
            {
                if (index > 0 && index <= options.Count)
                {
                    Utils.Write("You travel for 1 hour");
                    player.Update(60);
                    currentPlace = options[index - 1];
                    player.Location = currentPlace;
                    Utils.Write("You are now at " + currentPlace.Name);
                }
                else
                {
                    Utils.Write("Invalid input");
                }
            }
            else
            {
                Utils.Write("Invalid input");
            }
        }
    }
}
