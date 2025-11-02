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
            if (Output.TestMode)
            {
                TestModeIO.Initialize();
            }
            Output.SleepTime = 500;
            Output.WriteLine();
            Output.WriteLine();
            Output.WriteLine();
            Output.WriteLine();
            Output.WriteLine();
            Output.WriteDanger("You wake up in the forest, with no memory of how you got there.");
            Output.WriteDanger("Light snow is falling, and you feel the air getting colder.");
            Output.WriteDanger("The last embers of your campfire are fading...");
            Output.WriteDanger("You need to gather fuel, find food and water, and survive.");
            Output.WriteLine();
            Output.SleepTime = 10;
            Zone zone = ZoneFactory.MakeForestZone();
            Container oldBag = new Container("Tattered Sack", 10);
            Location startingArea = new Location("Clearing", zone);

            // Add starting equipment - basic fur wraps (Ice Age appropriate)
            oldBag.Add(ItemFactory.MakeWornFurChestWrap());
            oldBag.Add(ItemFactory.MakeFurLegWraps());
            startingArea.Containers.Add(oldBag);

            // Make the starting clearing forageable (CRITICAL for survival)
            // 1.75x density provides tutorial generosity (5-8 fire attempts before critical depletion)
            ForageFeature forageFeature = new ForageFeature(startingArea, 1.75);
            // Forest clearing materials - basic fire-starting and crafting
            forageFeature.AddResource(ItemFactory.MakeDryGrass, 0.5);
            forageFeature.AddResource(ItemFactory.MakeBarkStrips, 0.6);
            forageFeature.AddResource(ItemFactory.MakePlantFibers, 0.5);
            forageFeature.AddResource(ItemFactory.MakeStick, 0.7);
            forageFeature.AddResource(ItemFactory.MakeFirewood, 0.3);
            forageFeature.AddResource(ItemFactory.MakeTinderBundle, 0.15);
            // Food items (improved for early-game survival)
            forageFeature.AddResource(ItemFactory.MakeBerry, 0.4);
            forageFeature.AddResource(ItemFactory.MakeMushroom, 0.6);
            forageFeature.AddResource(ItemFactory.MakeNuts, 0.3);
            forageFeature.AddResource(ItemFactory.MakeGrubs, 0.4);
            forageFeature.AddResource(ItemFactory.MakeEggs, 0.2);
            startingArea.Features.Add(forageFeature);

            // Add environment feature
            startingArea.Features.Add(new EnvironmentFeature(startingArea, EnvironmentFeature.LocationType.Forest));

            // Add starting campfire (1 hour of warmth remaining)
            HeatSourceFeature campfire = new HeatSourceFeature(startingArea, heatOutput: 15.0);
            campfire.AddFuel(1.0); // 60 minutes = 1.0 hour
            campfire.SetActive(true);
            startingArea.Features.Add(campfire);

            zone.Locations.Add(startingArea);
            Player player = new Player(startingArea);
            World.Player = player;
            
            var defaultAction = ActionFactory.Common.MainMenu();
            var context = new GameContext(player);
            while (true)
            {
                defaultAction.Execute(context);
            }
        }
    }
}