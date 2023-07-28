using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
            player = new Player();
            currentPlace = places[0];
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
            Utils.Write(player.GetStats(),100);
            Utils.Write("You are in a " + currentPlace.Name,100);
            Utils.Write("What would you like to do?");
            Utils.Write("1. Forage",100);
            Utils.Write("2. Use an item",100);
            Utils.Write("3. Travel",100);
            Utils.Write("4. Quit",100);
            string? input = Utils.Read();
            if (input == "1")
            {
                currentPlace.Forage(player);
            }
            else if (input == "2")
            {
                Item? item = player.OpenInventory();
                item?.Use(player);
            }
            else if (input == "3")
            {
                Travel();
            }
            else if (input == "4")
            {
                player.Damage(999);
            }
            else
            {
                Utils.Write("Invalid input");
            }

        }
        public void Travel()
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
                    currentPlace = options[index - 1];
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
            
            Utils.Write("You travel for 1 hour");
            player.Update(60);
            Utils.Write("You are now at " + currentPlace.Name);

        }
    }
}
