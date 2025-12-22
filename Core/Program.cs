using text_survival.Actions;
using text_survival.Actors.Player;
using text_survival.Environments;
using text_survival.Environments.Factories;
using text_survival.Environments.Features;
using text_survival.IO;
using text_survival.Items;
using text_survival.UI;

namespace text_survival.Core
{
    public class Program
    {
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

        static void Main()
        {
            if (Output.TestMode)
            {
                TestModeIO.Initialize();
            }

            Zone zone = ZoneFactory.MakeForestZone();

            // Initialize weather for game start time (9:00 AM, Jan 1)
            var gameStartTime = new DateTime(2025, 1, 1, 9, 0, 0);
            zone.Weather.Update(gameStartTime);

            Location startingArea = zone.Graph.All.First(s => s.Name == "Forest Camp");

            // Starting equipment - basic fur wraps (Ice Age appropriate)
            // (Equipment is equipped directly to player inventory below)

            HeatSourceFeature campfire = new HeatSourceFeature();
            campfire.AddFuel(2, FuelType.Kindling); // Unlit - player must start it
            startingArea.Features.Add(campfire);

            Player player = new Player();
            Camp camp = new Camp(startingArea);
            GameContext context = new GameContext(player, camp);

            // Equip starting clothing
            context.Inventory.Equip(Equipment.WornFurChestWrap());
            context.Inventory.Equip(Equipment.FurLegWraps());
            context.Inventory.Equip(Equipment.FurBoots());

            // Add starting supplies to player's aggregate inventory
            context.Inventory.Tools.Add(Tool.HandDrill());  // Fire-starting tool
            context.Inventory.Sticks.Add(0.3);  // A stick for kindling
            context.Inventory.Sticks.Add(0.25);
            context.Inventory.Sticks.Add(0.35);
            context.Inventory.Tinder.Add(0.05); // Some tinder
            context.Inventory.Tinder.Add(0.04);

            GameDisplay.AddDanger("You wake up in the forest, shivering. You don't remember how you got here.");
            GameDisplay.AddDanger("Snow drifts down through the pines. The cold is already seeping into your bones.");
            GameDisplay.AddDanger("There's a fire pit nearby with some kindling. You need to get it lit — fast.");
            GameDisplay.AddDanger("You need to gather fuel, find food and water, and survive.");

            GameRunner runner = new GameRunner(context);
            runner.Run();
            DisplayDeathScreen(context);
        }
    }
}
