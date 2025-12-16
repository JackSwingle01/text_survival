using text_survival.Actions;
using text_survival.Actors;
using text_survival.Effects;
using text_survival.IO;
using text_survival.Survival;

namespace text_survival.Bodies
{
    public static class BodyDescriber
    {
        public static void Describe(Actor actor)
        {
            var body = actor.Body;

            const int barWidth = 10;
            const int boxWidth = 65;
            // Header
            Output.WriteLine("┌", new string('─', boxWidth), "┐");
            PrintCenteredHeader("BODY STATUS");

            // Health and Body Composition (3 columns)
            Output.WriteLine("├─────────────────────┬─────────────────────┬─────────────────────┤");

            // Health row
            string healthBar = CreateProgressBar((int)(body.Health * 100), barWidth);
            string healthValueStr = $"[{healthBar}] {(int)(body.Health * 100)}%";
            string healthStatus = GetHealthStatus((int)(body.Health * 100));
            Output.WriteLine("│ Health              │ ", healthValueStr.PadRight(19), " │ ", healthStatus.PadRight(19), " │");

            // Body composition row
            string bodyCompValue = $"{Math.Round(body.WeightKG * 2.2, 1)} lbs";
            string bodyCompStatus = $"{(int)(body.BodyFatPercentage * 100)}% fat, {(int)(body.MusclePercentage * 100)}% muscle";
            Output.WriteLine("│ Body Composition    │ ", bodyCompValue.PadRight(19), " │ ", bodyCompStatus.PadRight(19), " │");

            // Capabilities section
            PrintDivider(boxWidth);
            PrintCenteredHeader("CAPABILITIES");
            PrintDivider(boxWidth);

            // Use actor properties now
            PrintCapabilityRow("Strength", actor.Strength);
            PrintCapabilityRow("Speed", actor.Speed);
            PrintCapabilityRow("Vitality", actor.Vitality);
            PrintCapabilityRow("Perception", actor.Perception);
            PrintCapabilityRow("Cold Resistance", actor.ColdResistance);

            // Body parts section (if any damaged)
            var damagedParts = body.Parts.Where(p => p.Condition < 1.0).ToList();
            if (damagedParts.Count > 0)
            {
                PrintDivider(boxWidth);
                PrintCenteredHeader("BODY PARTS");
                PrintDivider(boxWidth);

                foreach (var part in damagedParts)
                {
                    PrintBodyPartRow(part);
                }
            }

            // Active effects from actor
            var activeEffects = actor.EffectRegistry.GetAll();
            if (activeEffects.Count > 0)
            {
                PrintDivider(boxWidth);
                PrintCenteredHeader("ACTIVE EFFECTS");
                PrintDivider(boxWidth);

                foreach (var effect in activeEffects)
                {
                    PrintEffectRow(effect);
                }
            }

            Output.WriteLine("└─────────────────────┴─────────────────────┴─────────────┴───────┘");
        }

        public static void DescribeSurvivalStats(Actor actor, SurvivalContext context)
        {
            var body = actor.Body;

            const int boxWidth = 53;
            const int barWidth = 20;

            int caloriesPercent = (int)(body.CalorieStore / SurvivalProcessor.MAX_CALORIES * 100);
            int hydrationPercent = (int)(body.Hydration / SurvivalProcessor.MAX_HYDRATION * 100);
            int energyPercent = (int)(body.Energy / SurvivalProcessor.MAX_ENERGY_MINUTES * 100);

            string caloriesBar = CreateProgressBar(caloriesPercent, barWidth);
            string hydrationBar = CreateProgressBar(hydrationPercent, barWidth);
            string energyBar = CreateProgressBar(energyPercent, barWidth);

            string caloriesStatus = GetCaloriesStatus(caloriesPercent);
            string hydrationStatus = GetHydrationStatus(hydrationPercent);
            string exhaustionStatus = GetEnergyStatus(energyPercent);
            string tempStatus = GetTemperatureStatus(body.BodyTemperature);

            string healthLine = $"Food:    [{caloriesBar}] {caloriesPercent}% - {caloriesStatus}";
            string waterLine = $"Water:   [{hydrationBar}] {hydrationPercent}% - {hydrationStatus}";
            string energyLine = $"Energy:  [{energyBar}] {energyPercent}% - {exhaustionStatus}";
            string tempLine = $"Temp:    {body.BodyTemperature:F1}°F ({tempStatus}) - Feels like {context.LocationTemperature:F1}°F";

            Output.WriteLine($"┌{new string('─', boxWidth)}┐");
            Output.WriteLine($"│ {healthLine,-(boxWidth - 2)} │");
            Output.WriteLine($"│ {waterLine,-(boxWidth - 2)} │");
            Output.WriteLine($"│ {energyLine,-(boxWidth - 2)} │");
            Output.WriteLine($"│ {tempLine,-(boxWidth - 2)} │");
            Output.WriteLine($"└{new string('─', boxWidth)}┘");
        }

        private static void PrintCenteredHeader(string text, int width = 65)
        {
            Output.WriteLine("├", text.PadLeft((width + text.Length) / 2).PadRight(width), "┤");
        }

        private static void PrintDivider(int width)
        {
            string line = "│" + new string('─', width) + "│";
            Output.WriteLine(line);
        }
        private static void PrintCapabilityRow(string name, double value)
        {
            const int barWidth = 10;
            string bar = CreateProgressBar((int)(value * 100), barWidth);
            string valueStr = $"[{bar}] {(int)(value * 100)}%";
            string status = GetCapabilityStatus(value);

            Output.WriteLine("│ ", name.PadRight(19), " │ ", valueStr.PadRight(19), " │ ", status.PadRight(11), " │       │");
        }

        private static void PrintBodyPartRow(BodyRegion part)
        {
            const int barWidth = 10;
            string bar = CreateProgressBar((int)(part.Condition * 100), barWidth);
            string valueStr = $"[{bar}] {(int)(part.Condition * 100)}%";
            string status = GetDamageDescription(part.Condition);

            Output.WriteLine("│ ", part.Name.PadRight(19), " │ ", valueStr.PadRight(19), " │ ", status.PadRight(11), " │       │");
        }

        private static void PrintEffectRow(Effect effect)
        {
            const int barWidth = 10;
            string bar = CreateProgressBar((int)(effect.Severity * 100), barWidth);
            string trendIndicator = GetTrendIndicator(effect);
            string valueStr = $"[{bar}] {(int)(effect.Severity * 100)}%{trendIndicator}";
            string status = effect.Severity.ToString();
            string target = GetEffectTarget(effect);

            Output.WriteLine("│ ", effect.EffectKind.PadRight(19), " │ ", valueStr.PadRight(19), " │ ", target.PadRight(5), " │");
        }

        private static string CreateProgressBar(int percent, int width)
        {
            int filled = (int)(percent / 100.0 * width);
            int empty = width - filled;
            return new string('█', filled) + new string('░', empty);
        }

        private static string GetHealthStatus(int percent)
        {
            return percent switch
            {
                >= 90 => "Excellent",
                >= 75 => "Good",
                >= 50 => "Fair",
                >= 25 => "Poor",
                _ => "Critical"
            };
        }

        private static string GetCapabilityStatus(double value)
        {
            return value switch
            {
                >= 0.9 => "Excellent",
                >= 0.75 => "Good",
                >= 0.5 => "Fair",
                >= 0.25 => "Poor",
                _ => "Critical"
            };
        }

        private static string GetDamageDescription(double condition)
        {
            return condition switch
            {
                <= 0 => "Destroyed",
                < 0.2 => "Critical",
                < 0.4 => "Severe",
                < 0.6 => "Moderate",
                < 0.8 => "Light",
                _ => "Minor"
            };
        }

        private static string GetTrendIndicator(Effect effect)
        {
            if (effect.HourlySeverityChange > 0)
                return " +";
            else if (effect.HourlySeverityChange < 0)
                return " -";
            else
                return "";
        }

        private static string GetEffectTarget(Effect effect)
        {
            if (string.IsNullOrEmpty(effect.TargetBodyPart))
                return "Core";
            else
                return effect.TargetBodyPart;
        }



        public static void DescribeSurvivalStats(Body body, GameContext context)
        {
            const int boxWidth = 53;
            const int barWidth = 20;

            // Calculate percentages
            int caloriesPercent = (int)(body.CalorieStore / SurvivalProcessor.MAX_CALORIES * 100);
            int hydrationPercent = (int)(body.Hydration / SurvivalProcessor.MAX_HYDRATION * 100);
            int energyPercent = (int)(body.Energy / SurvivalProcessor.MAX_ENERGY_MINUTES * 100);

            // Create progress bars
            string caloriesBar = CreateProgressBar(caloriesPercent, barWidth);
            string hydrationBar = CreateProgressBar(hydrationPercent, barWidth);
            string energyBar = CreateProgressBar(energyPercent, barWidth);

            // Status descriptions
            string caloriesStatus = GetCaloriesStatus(caloriesPercent);
            string hydrationStatus = GetHydrationStatus(hydrationPercent);
            string exhaustionStatus = GetEnergyStatus(energyPercent);
            string tempStatus = GetTemperatureStatus(body.BodyTemperature);

            // Build status lines
            string healthLine = $"Food:    [{caloriesBar}] {caloriesPercent}% - {caloriesStatus}";
            string waterLine = $"Water:   [{hydrationBar}] {hydrationPercent}% - {hydrationStatus}";
            string energyLine = $"Energy:  [{energyBar}] {energyPercent}% - {exhaustionStatus}";
            string tempLine = $"Temp:    {body.BodyTemperature:F1}°F ({tempStatus}) - Feels like {context.CurrentLocation.GetTemperature():F1}°F";

            // Display with proper padding
            Output.WriteLine($"┌{new string('─', boxWidth)}┐");
            Output.WriteLine($"│ {healthLine,-(boxWidth - 2)} │");
            Output.WriteLine($"│ {waterLine,-(boxWidth - 2)} │");
            Output.WriteLine($"│ {energyLine,-(boxWidth - 2)} │");
            Output.WriteLine($"│ {tempLine,-(boxWidth - 2)} │");
            Output.WriteLine($"└{new string('─', boxWidth)}┘");
        }

        private static string GetCaloriesStatus(int percent)
        {
            return percent switch
            {
                >= 80 => "Well Fed",
                >= 60 => "Satisfied",
                >= 40 => "Peckish",
                >= 20 => "Hungry",
                _ => "Starving"
            };
        }

        private static string GetHydrationStatus(int percent)
        {
            return percent switch
            {
                >= 80 => "Hydrated",
                >= 60 => "Fine",
                >= 40 => "Thirsty",
                >= 20 => "Parched",
                _ => "Dehydrated"
            };
        }

        private static string GetEnergyStatus(int percent)
        {
            return percent switch
            {
                >= 90 => "Energized",
                >= 80 => "Alert",
                >= 40 => "Normal",
                >= 30 => "Tired",
                >= 20 => "Very Tired",
                _ => "Exhausted"
            };
        }

        private static string GetTemperatureStatus(double temp)
        {
            return temp switch
            {
                >= 100 => "Feverish",
                >= 99 => "Hot",
                >= 97 => "Normal",
                >= 95 => "Cool",
                _ => "Cold"
            };
        }
    }

}