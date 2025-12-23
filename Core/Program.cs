using text_survival.Actions;
using text_survival.Actors.Player;
using text_survival.Environments;
using text_survival.Environments.Factories;
using text_survival.Environments.Features;
using text_survival.IO;
using text_survival.Items;
using text_survival.Persistence;
using text_survival.UI;
using text_survival.Web;

namespace text_survival.Core
{
    public class Program
    {
        static async Task Main(string[] args)
        {
            // Check for web mode
            if (args.Contains("--web"))
            {
                int port = 5000;
                var portArg = args.FirstOrDefault(a => a.StartsWith("--port="));
                if (portArg != null && int.TryParse(portArg.Split('=')[1], out int parsedPort))
                    port = parsedPort;

                await WebServer.Run(port);
                return;
            }

            // Console mode
            RunConsoleGame();
        }

        static void RunConsoleGame()
        {
            if (Output.TestMode)
            {
                TestModeIO.Initialize();
            }

            // Auto-load if save exists, otherwise create new game
            bool isNewGame = !SaveManager.HasSaveFile();
            GameContext context = GameInitializer.LoadOrCreateNew();

            // Only show intro text for new games
            if (isNewGame)
            {
                GameDisplay.AddDanger("You wake up in the forest, shivering. You don't remember how you got here.");
                GameDisplay.AddDanger("Snow drifts down through the pines. The cold is already seeping into your bones.");
                GameDisplay.AddDanger("There's a fire pit nearby with some kindling. You need to get it lit - fast.");
                GameDisplay.AddDanger("You need to gather fuel, find food and water, and survive.");
            }
            else
            {
                GameDisplay.AddNarrative("Game loaded.");
            }

            GameRunner runner = new GameRunner(context);
            runner.Run();

            // Delete save on death
            SaveManager.DeleteSave();
            DisplayDeathScreen(context);
        }

        static void DisplayDeathScreen(GameContext ctx)
        {
            Player player = ctx.player;
            GameDisplay.AddNarrative("\n\n");
            GameDisplay.AddNarrative("═══════════════════════════════════════════════════════════");
            GameDisplay.AddDanger("                       YOU DIED                            ");
            GameDisplay.AddNarrative("═══════════════════════════════════════════════════════════");
            GameDisplay.AddNarrative("\n");

            // Determine cause of death
            string causeOfDeath = DetermineCauseOfDeath(player);
            GameDisplay.AddDanger($"Cause of Death: {causeOfDeath}");
            GameDisplay.AddNarrative("\n");

            // Get survival data
            var body = player.Body;
            // Show survival stats at time of death
            GameDisplay.AddNarrative("═══ Final Survival Stats ═══");
            GameDisplay.AddNarrative($"Vitality: {player.Vitality * 100:F1}%");
            GameDisplay.AddNarrative($"Calories: {body.CalorieStore:F0}/{Survival.SurvivalProcessor.MAX_CALORIES:F0} ({body.CalorieStore / Survival.SurvivalProcessor.MAX_CALORIES * 100:F1}%)");
            GameDisplay.AddNarrative($"Hydration: {body.Hydration:F0}/{Survival.SurvivalProcessor.MAX_HYDRATION:F0} ({body.Hydration / Survival.SurvivalProcessor.MAX_HYDRATION * 100:F1}%)");
            GameDisplay.AddNarrative($"Energy: {body.Energy:F0}/{Survival.SurvivalProcessor.MAX_ENERGY_MINUTES:F0} ({body.Energy / Survival.SurvivalProcessor.MAX_ENERGY_MINUTES * 100:F1}%)");
            GameDisplay.AddNarrative($"Body Temperature: {player.Body.BodyTemperature:F1}°F");
            GameDisplay.AddNarrative("\n");

            // Show body composition
            GameDisplay.AddNarrative("═══ Body Composition ═══");
            GameDisplay.AddNarrative($"Weight: {player.Body.WeightKG:F1} kg");
            GameDisplay.AddNarrative($"Body Fat: {player.Body.BodyFatKG:F1} kg ({player.Body.BodyFatPercentage * 100:F1}%)");
            GameDisplay.AddNarrative($"Muscle Mass: {player.Body.MuscleKG:F1} kg ({player.Body.MusclePercentage * 100:F1}%)");
            GameDisplay.AddNarrative("\n");

            // Show time survived
            GameDisplay.AddNarrative("═══ Time Survived ═══");
            var startTime = new DateTime(2025, 1, 1, 9, 0, 0); // Game start time
            var timeSurvived = ctx.GameTime - startTime;
            int days = timeSurvived.Days;
            int hours = timeSurvived.Hours;
            int minutes = timeSurvived.Minutes;
            GameDisplay.AddNarrative($"You survived for {days} days, {hours} hours, and {minutes} minutes.");
            GameDisplay.AddNarrative("\n");

            GameDisplay.AddNarrative("═══════════════════════════════════════════════════════════");
            GameDisplay.AddNarrative("                    GAME OVER                              ");
            GameDisplay.AddNarrative("═══════════════════════════════════════════════════════════");
            GameDisplay.AddNarrative("\n");
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
    }
}
