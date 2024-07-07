﻿using text_survival.Environments.Locations;
using text_survival.Environments;
using text_survival.IO;
using text_survival.Items;

namespace text_survival
{
    public class Program
    {
        static void Main()
        {
            Output.SleepTime = 500;
            Output.WriteLine("You wake up in the forest, with no memory of how you got there.");
            Output.WriteLine("Light snow is falling, and you feel the air getting colder.");
            Output.WriteLine("You need to find shelter, food, and water to survive.");
            Output.SleepTime = 10;

            Area startingArea = new Area("Clearing", "A small clearing in the forest.");
            Container oldBag = new Container("Old bag", 10);
            Location log = new Location("Hollow log", startingArea);
            oldBag.Add(ItemFactory.MakeApple());
            oldBag.Add(ItemFactory.MakeClothShirt());
            oldBag.Add(ItemFactory.MakeClothPants());
            oldBag.Add(ItemFactory.MakeBoots());
            oldBag.Add(new Weapon(WeaponType.Dagger, WeaponMaterial.Iron, "Old dagger", 40));
            log.PutThing(oldBag);
            startingArea.PutThing(log);
            startingArea.PutThing(new Cave(startingArea, 1, 1));
            Player player = new Player(startingArea);
            World.Player = player;
            Actions actions = new(player);
            while (player.IsAlive)
            {
                actions.Act();
            }
        }
    }
}