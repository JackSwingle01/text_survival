using text_survival.Actions;
using text_survival.Actors.Player;
using text_survival.Environments;
using text_survival.Environments.Factories;
using text_survival.Environments.Features;
using text_survival.IO;
using text_survival.Items;

namespace text_survival.Core
{
    public class Program
    {
        static void DisplayDeathScreen(Player player)
        {
            Output.WriteLine("\n\n");
            Output.WriteLine("═══════════════════════════════════════════════════════════");
            Output.WriteDanger("                       YOU DIED                            ");
            Output.WriteLine("═══════════════════════════════════════════════════════════");
            Output.WriteLine("\n");

            // Determine cause of death
            string causeOfDeath = DetermineCauseOfDeath(player);
            Output.WriteLineColored(ConsoleColor.Red, $"Cause of Death: {causeOfDeath}");
            Output.WriteLine("\n");

            // Get survival data
            var survivalData = player.Body.BundleSurvivalData();

            // Show survival stats at time of death
            Output.WriteLine("═══ Final Survival Stats ═══");
            Output.WriteLine($"Health: {player.Body.Health * 100:F1}%");
            Output.WriteLine($"Calories: {survivalData.Calories:F0}/{Survival.SurvivalProcessor.MAX_CALORIES:F0} ({survivalData.Calories / Survival.SurvivalProcessor.MAX_CALORIES * 100:F1}%)");
            Output.WriteLine($"Hydration: {survivalData.Hydration:F0}/{Survival.SurvivalProcessor.MAX_HYDRATION:F0} ({survivalData.Hydration / Survival.SurvivalProcessor.MAX_HYDRATION * 100:F1}%)");
            Output.WriteLine($"Energy: {survivalData.Energy:F0}/{Survival.SurvivalProcessor.MAX_ENERGY_MINUTES:F0} ({survivalData.Energy / Survival.SurvivalProcessor.MAX_ENERGY_MINUTES * 100:F1}%)");
            Output.WriteLine($"Body Temperature: {player.Body.BodyTemperature:F1}°F");
            Output.WriteLine("\n");

            // Show body composition
            Output.WriteLine("═══ Body Composition ═══");
            Output.WriteLine($"Weight: {player.Body.Weight:F1} kg");
            Output.WriteLine($"Body Fat: {player.Body.BodyFat:F1} kg ({player.Body.BodyFatPercentage * 100:F1}%)");
            Output.WriteLine($"Muscle Mass: {player.Body.Muscle:F1} kg ({player.Body.MusclePercentage * 100:F1}%)");
            Output.WriteLine("\n");

            // Show time survived
            Output.WriteLine("═══ Time Survived ═══");
            var startTime = new DateTime(2025, 1, 1, 9, 0, 0); // Game start time
            var timeSurvived = World.GameTime - startTime;
            int days = timeSurvived.Days;
            int hours = timeSurvived.Hours;
            int minutes = timeSurvived.Minutes;
            Output.WriteLine($"You survived for {days} days, {hours} hours, and {minutes} minutes.");
            Output.WriteLine("\n");

            Output.WriteLine("═══════════════════════════════════════════════════════════");
            Output.WriteLine("                    GAME OVER                              ");
            Output.WriteLine("═══════════════════════════════════════════════════════════");
            Output.WriteLine("\n");
        }

        static string DetermineCauseOfDeath(Player player)
        {
            var body = player.Body;
            var survivalData = body.BundleSurvivalData();

            // Check critical organ failure
            var brain = body.Parts.SelectMany(p => p.Organs).FirstOrDefault(o => o.Name == "Brain");
            var heart = body.Parts.SelectMany(p => p.Organs).FirstOrDefault(o => o.Name == "Heart");
            var lungs = body.Parts.SelectMany(p => p.Organs).FirstOrDefault(o => o.Name == "Lungs");
            var liver = body.Parts.SelectMany(p => p.Organs).FirstOrDefault(o => o.Name == "Liver");

            if (brain?.Condition <= 0) return "Brain death";
            if (heart?.Condition <= 0) return "Cardiac arrest";
            if (lungs?.Condition <= 0) return "Respiratory failure";
            if (liver?.Condition <= 0) return "Liver failure";

            // Check survival stat contexts
            if (body.BodyTemperature < 89.6)
                return "Severe hypothermia (core body temperature too low)";

            if (survivalData.Hydration <= 0)
                return "Severe dehydration";

            if (survivalData.Calories <= 0 && body.BodyFatPercentage < 0.05)
                return "Starvation (complete organ failure)";

            // Default
            return "Multiple organ failure";
        }

        static void Main()
        {
            if (Output.TestMode)
            {
                TestModeIO.Initialize();
            }
            Output.SleepTime = 500;
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

            // Add starting campfire (with 4.5kg kindling fuel for 3 hours of warmth)
            // Note: Must use kindling (0°F requirement) for initial fuel, not softwood (400°F requirement)
            // Kindling burns at 1.5 kg/hr, so 4.5kg = 3 hours burn time
            HeatSourceFeature campfire = new HeatSourceFeature(startingArea);
            var startingFuel = ItemFactory.MakeStick(); // Large Stick = kindling (0°F requirement)
            campfire.AddFuel(startingFuel, 4.5); // Add 4.5kg of kindling (auto-lights since MinFireTemp = 0°F)
            startingArea.Features.Add(campfire);

            // Add guaranteed fire-starting materials on ground (set IsFound=true so they're visible)
            for (int i = 0; i < 3; i++)
            {
                var stick = ItemFactory.MakeStick();
                stick.IsFound = true;
                startingArea.Items.Add(stick);
            }
            for (int i = 0; i < 2; i++)
            {
                var tinder = ItemFactory.MakeDryGrass();
                tinder.IsFound = true;
                startingArea.Items.Add(tinder);
            }

            zone.Locations.Add(startingArea);
            Player player = new Player(startingArea);
            World.Player = player;
            
            var defaultAction = ActionFactory.Common.MainMenu();
            var context = new GameContext(player);
            while (true)
            {
                defaultAction.Execute(context);

                // Check for death after each action
                if (player.Body.IsDestroyed)
                {
                    DisplayDeathScreen(player);
                    break;
                }
            }
        }
    }
}