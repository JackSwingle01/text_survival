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
        static void DisplayDeathScreen(GameContext ctx)
        {
            Player player = ctx.player;
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
            var body = player.Body;
            // Show survival stats at time of death
            Output.WriteLine("═══ Final Survival Stats ═══");
            Output.WriteLine($"Health: {player.Body.Health * 100:F1}%");
            Output.WriteLine($"Calories: {body.CalorieStore:F0}/{Survival.SurvivalProcessor.MAX_CALORIES:F0} ({body.CalorieStore / Survival.SurvivalProcessor.MAX_CALORIES * 100:F1}%)");
            Output.WriteLine($"Hydration: {body.Hydration:F0}/{Survival.SurvivalProcessor.MAX_HYDRATION:F0} ({body.Hydration / Survival.SurvivalProcessor.MAX_HYDRATION * 100:F1}%)");
            Output.WriteLine($"Energy: {body.Energy:F0}/{Survival.SurvivalProcessor.MAX_ENERGY_MINUTES:F0} ({body.Energy / Survival.SurvivalProcessor.MAX_ENERGY_MINUTES * 100:F1}%)");
            Output.WriteLine($"Body Temperature: {player.Body.BodyTemperature:F1}°F");
            Output.WriteLine("\n");

            // Show body composition
            Output.WriteLine("═══ Body Composition ═══");
            Output.WriteLine($"Weight: {player.Body.WeightKG:F1} kg");
            Output.WriteLine($"Body Fat: {player.Body.BodyFatKG:F1} kg ({player.Body.BodyFatPercentage * 100:F1}%)");
            Output.WriteLine($"Muscle Mass: {player.Body.MuscleKG:F1} kg ({player.Body.MusclePercentage * 100:F1}%)");
            Output.WriteLine("\n");

            // Show time survived
            Output.WriteLine("═══ Time Survived ═══");
            var startTime = new DateTime(2025, 1, 1, 9, 0, 0); // Game start time
            var timeSurvived = ctx.GameTime - startTime;
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

            if (body.Hydration <= 0)
                return "Severe dehydration";

            if (body.CalorieStore <= 0 && body.BodyFatPercentage < 0.05)
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
            Location startingArea = zone.Graph.Sites.First(s => s.Name == "Forest Clearing");

            // Add starting equipment - basic fur wraps (Ice Age appropriate)
            startingArea.Items.Add(ItemFactory.MakeWornFurChestWrap());
            startingArea.Items.Add(ItemFactory.MakeFurLegWraps());

            // Add environment feature
            startingArea.Features.Add(new EnvironmentFeature(EnvironmentFeature.LocationType.Forest));

            // Add starting campfire (with 4.5kg kindling fuel for 3 hours of warmth)
            // Note: Must use kindling (0°F requirement) for initial fuel, not softwood (400°F requirement)
            // Kindling burns at 1.5 kg/hr, so 4.5kg = 3 hours burn time
            HeatSourceFeature campfire = new HeatSourceFeature();
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

            Player player = new Player();
            Camp camp = new Camp(startingArea);
            GameContext context = new GameContext(player, camp);

            GameRunner runner = new GameRunner(context);
            runner.Run();
            DisplayDeathScreen(context);
        }
    }
}
