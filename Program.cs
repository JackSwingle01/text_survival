using text_survival.Environments;
using text_survival.Events;
using text_survival.IO;
using text_survival.Items;
using text_survival.Actions;
using text_survival.Crafting;

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
            Zone zone = ZoneFactory.MakeForestZone();
            Container oldBag = new Container("Old bag", 10);
            Location startingArea = new Location("Clearing", zone);
            oldBag.Add(ItemFactory.MakeKnife());
            oldBag.Add(ItemFactory.MakeMoccasins());
            oldBag.Add(ItemFactory.MakeLeatherTunic());
            oldBag.Add(ItemFactory.MakeLeatherPants());
            startingArea.Containers.Add(oldBag);
            zone.Locations.Add(startingArea);
            Player player = new Player(startingArea);
            World.Player = player;
            
            var defaultAction = ActionFactory.Common.MainMenu();
            var context = new GameContext(player)
            {
                CraftingManager = new CraftingSystem(player)
            }; 
            while (true)
            {
                defaultAction.Execute(context);
            }
        }
    }
}