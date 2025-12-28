using text_survival.Bodies;
using text_survival.Environments.Features;
using text_survival.IO;
using text_survival.Items;
using text_survival.UI;

namespace text_survival.Actions.Handlers;

/// <summary>
/// Handles fire starting and tending actions.
/// Static handler that takes GameContext and mutates state directly.
/// </summary>
public static class FireHandler
{
    public static void TendFire(GameContext ctx)
    {
        var fire = ctx.CurrentLocation.GetFeature<HeatSourceFeature>()!;
        var inv = ctx.Inventory;

        while (true)
        {
            // Build fuel options - only show options that can actually be added
            var fuelChoices = new List<string>();
            var fuelMap = new Dictionary<string, (string name, FuelType type, Func<double> takeFunc)>();

            // Typed wood fuels - each with distinct burn characteristics
            // Pine: fast/hot for cooking, Birch: moderate general use, Oak: slow overnight burns
            AddWoodFuelOption(fuelChoices, fuelMap, fire, inv[Resource.Pine], "pine", FuelType.PineWood, () => inv.Pop(Resource.Pine), "burns fast");
            AddWoodFuelOption(fuelChoices, fuelMap, fire, inv[Resource.Birch], "birch", FuelType.BirchWood, () => inv.Pop(Resource.Birch), "steady burn");
            AddWoodFuelOption(fuelChoices, fuelMap, fire, inv[Resource.Oak], "oak", FuelType.OakWood, () => inv.Pop(Resource.Oak), "long burn");

            if (inv.Count(Resource.Stick) > 0 && fire.CanAddFuel(FuelType.Kindling))
            {
                string label = $"Add stick ({inv.Count(Resource.Stick)} @ {inv.Weight(Resource.Stick):F1}kg)";
                fuelChoices.Add(label);
                fuelMap[label] = ("stick", FuelType.Kindling, () => inv.Pop(Resource.Stick));
            }

            // Birch bark as excellent tinder
            if (inv.Count(Resource.BirchBark) > 0 && fire.CanAddFuel(FuelType.BirchBark))
            {
                string label = $"Add birch bark ({inv.Count(Resource.BirchBark)} @ {inv.Weight(Resource.BirchBark):F2}kg) - great tinder";
                fuelChoices.Add(label);
                fuelMap[label] = ("birch bark", FuelType.BirchBark, () => inv.Pop(Resource.BirchBark));
            }

            if (inv.Count(Resource.Tinder) > 0 && fire.CanAddFuel(FuelType.Tinder))
            {
                string label = $"Add tinder ({inv.Count(Resource.Tinder)} @ {inv.Weight(Resource.Tinder):F2}kg)";
                fuelChoices.Add(label);
                fuelMap[label] = ("tinder", FuelType.Tinder, () => inv.Pop(Resource.Tinder));
            }

            // Show charcoal collection option if available
            bool hasCharcoal = fire.HasCharcoal;
            if (hasCharcoal)
            {
                string charcoalLabel = $"Collect charcoal ({fire.CharcoalAvailableKg:F2}kg)";
                fuelChoices.Add(charcoalLabel);
                fuelMap[charcoalLabel] = ("charcoal", FuelType.Tinder, () =>
                {
                    double collected = fire.CollectCharcoal();
                    var loot = new Inventory();
                    loot.Add(Resource.Charcoal, collected);
                    GameDisplay.AddSuccess(ctx, $"You collect {collected:F2}kg of charcoal from the fire pit.");
                    InventoryCapacityHelper.CombineAndReport(ctx, loot);
                    return collected;
                }
                );
            }

            if (fuelChoices.Count == 0 && !hasCharcoal)
            {
                // Check if we have fuel but fire is too cold
                bool hasFuelButTooCold = (inv.HasLogs || inv.Count(Resource.Stick) > 0) && inv.Count(Resource.Tinder) == 0;
                if (hasFuelButTooCold)
                    GameDisplay.AddWarning(ctx, "The fire is too cold. You need tinder to build it up first.");
                else
                    GameDisplay.AddNarrative(ctx, "You have no fuel to add.");
                return;
            }

            fuelChoices.Add("Done");

            GameDisplay.Render(ctx, addSeparator: false, statusText: "Tending fire.");
            string choice = Input.Select(ctx, "Add fuel:", fuelChoices);

            if (choice == "Done")
                return;

            var (name, fuelType, takeFunc) = fuelMap[choice];

            // Handle disabled option (fire too small for logs)
            if (name == "disabled")
            {
                GameDisplay.AddWarning(ctx, "The fire needs to be bigger before you can add logs. Add more kindling first.");
                continue;
            }

            // Handle charcoal collection (already handled in takeFunc)
            if (name == "charcoal")
            {
                takeFunc(); // Collects charcoal and shows message
                ctx.Update(1, ActivityType.TendingFire);
                continue;
            }

            bool hadEmbers = fire.HasEmbers;

            // Take fuel from inventory and add to fire
            double mass = takeFunc();
            fire.AddFuel(mass, fuelType);

            GameDisplay.AddNarrative(ctx, $"You add a {name} ({mass:F2}kg) to the fire.");

            if (hadEmbers && fire.IsActive)
                GameDisplay.AddNarrative(ctx, "The embers ignite the fuel! The fire springs back to life.");

            ctx.Update(1, ActivityType.TendingFire);
        }
    }

    /// <summary>
    /// Helper to add a typed wood fuel option to the TendFire menu.
    /// Shows disabled option with reason if fire is too cold.
    /// </summary>
    private static void AddWoodFuelOption(
        List<string> choices,
        Dictionary<string, (string name, FuelType type, Func<double> takeFunc)> map,
        HeatSourceFeature fire,
        Stack<double> woodStack,
        string woodName,
        FuelType fuelType,
        Func<double> takeFunc,
        string burnDescription)
    {
        if (woodStack.Count == 0) return;

        if (fire.CanAddFuel(fuelType))
        {
            string label = $"Add {woodName} ({woodStack.Count} @ {woodStack.Sum():F1}kg) - {burnDescription}";
            choices.Add(label);
            map[label] = (woodName, fuelType, takeFunc);
        }
        else
        {
            // Show greyed-out option explaining why it can't be added yet
            string disabledLabel = $"[dim]Add {woodName} (fire too small)[/]";
            choices.Add(disabledLabel);
            map[disabledLabel] = ("disabled", fuelType, () => 0);
        }
    }

    public static void StartFire(GameContext ctx)
    {
        var inv = ctx.Inventory;
        var existingFire = ctx.CurrentLocation.GetFeature<HeatSourceFeature>();
        bool relightingFire = existingFire != null;

        if (relightingFire)
            GameDisplay.AddNarrative(ctx, "You prepare to relight the fire.");
        else
            GameDisplay.AddNarrative(ctx, "You prepare to start a fire.");

        // Get fire-making tools from aggregate inventory
        var fireTools = inv.Tools.Where(t =>
            t.ToolType == ToolType.FireStriker ||
            t.ToolType == ToolType.HandDrill ||
            t.ToolType == ToolType.BowDrill).ToList();

        if (fireTools.Count == 0)
        {
            GameDisplay.AddWarning(ctx, "You don't have any fire-making tools!");
            return;
        }

        bool hasTinder = inv.Count(Resource.Tinder) > 0 || inv.Count(Resource.BirchBark) > 0 || inv.Count(Resource.Amadou) > 0;
        bool hasKindling = inv.Count(Resource.Stick) > 0;
        bool hasBirchBark = inv.Count(Resource.BirchBark) > 0;
        bool hasAmadou = inv.Count(Resource.Amadou) > 0;

        if (!hasTinder)
        {
            GameDisplay.AddWarning(ctx, "You don't have any tinder to start a fire!");
            return;
        }

        if (!hasKindling)
        {
            GameDisplay.AddWarning(ctx, "You don't have any kindling to start a fire!");
            return;
        }

        // Show materials with special tinder types highlighted
        var tinderParts = new List<string>();
        if (inv.Count(Resource.Tinder) > 0) tinderParts.Add($"{inv.Count(Resource.Tinder)} tinder");
        if (hasBirchBark) tinderParts.Add($"{inv.Count(Resource.BirchBark)} birch bark");
        if (hasAmadou) tinderParts.Add($"{inv.Count(Resource.Amadou)} amadou");
        GameDisplay.AddNarrative(ctx, $"Materials: {string.Join(", ", tinderParts)}, {inv.Count(Resource.Stick)} kindling");

        // Build tool options with success chances
        var toolChoices = new List<string>();
        var toolMap = new Dictionary<string, (Gear tool, double chance)>();

        foreach (var tool in fireTools)
        {
            double baseChance = GetToolBaseChance(tool);
            var skill = ctx.player.Skills.GetSkill("Firecraft");
            double successChance = baseChance + (skill.Level * 0.1);
            successChance = Math.Clamp(successChance, 0.05, 0.95);

            string label = $"{tool.Name} - {successChance:P0} success chance";
            toolChoices.Add(label);
            toolMap[label] = (tool, successChance);
        }

        toolChoices.Add("Cancel");

        GameDisplay.Render(ctx, addSeparator: false, statusText: "Preparing.");
        string choice = Input.Select(ctx, "Choose fire-making tool:", toolChoices);

        if (choice == "Cancel")
        {
            GameDisplay.AddNarrative(ctx, "You decide not to start a fire right now.");
            return;
        }

        var (selectedTool, _) = toolMap[choice];

        // Check impairments once before the loop
        var capacities = ctx.player.GetCapacities();
        var isImpaired = AbilityCalculator.IsConsciousnessImpaired(capacities.Consciousness);
        var isClumsy = AbilityCalculator.IsManipulationImpaired(capacities.Manipulation);
        var wetness = ctx.player.EffectRegistry.GetEffectsByKind("Wet").FirstOrDefault()?.Severity ?? 0;
        var isWet = wetness > 0.3;

        if (isImpaired)
            GameDisplay.AddWarning(ctx, "Your foggy mind makes this harder.");
        if (isClumsy)
            GameDisplay.AddWarning(ctx, "Your unsteady hands make this harder.");
        if (isWet)
            GameDisplay.AddWarning(ctx, "Your wet hands make this harder.");

        while (true)
        {
            GameDisplay.AddNarrative(ctx, $"You work with the {selectedTool.Name}...");
            // ActivityType.TendingFire has EventMultiplier=0, so no events can interrupt
            GameDisplay.UpdateAndRenderProgress(ctx, "Starting fire...", 10, ActivityType.TendingFire);

            double baseChance = GetToolBaseChance(selectedTool);
            var skill = ctx.player.Skills.GetSkill("Firecraft");
            double finalChance = baseChance + (skill.Level * 0.1);

            // Consciousness impairment penalty (-20%)
            if (isImpaired)
                finalChance -= 0.2;

            // Manipulation impairment penalty (-25%)
            if (isClumsy)
                finalChance -= 0.25;

            // Wetness penalty - wet hands fumble with tinder (up to -25% at full wetness)
            if (isWet)
                finalChance -= wetness * 0.25;

            // Select best available tinder and get its ignition bonus
            // Priority: Amadou (best) > BirchBark (great) > Regular Tinder
            double tinderUsed;
            FuelType tinderType;
            string tinderName;
            if (inv.Count(Resource.Amadou) > 0)
            {
                tinderUsed = inv.Pop(Resource.Amadou);
                tinderType = FuelType.Tinder; // Amadou burns like tinder
                tinderName = "amadou";
                finalChance += 0.20; // Amadou is the best fire-starting material
            }
            else if (inv.Count(Resource.BirchBark) > 0)
            {
                tinderUsed = inv.Pop(Resource.BirchBark);
                tinderType = FuelType.BirchBark;
                tinderName = "birch bark";
                finalChance += FuelDatabase.Get(FuelType.BirchBark).IgnitionBonus;
            }
            else
            {
                tinderUsed = inv.Pop(Resource.Tinder);
                tinderType = FuelType.Tinder;
                tinderName = "tinder";
                finalChance += FuelDatabase.Get(FuelType.Tinder).IgnitionBonus;
            }

            finalChance = Math.Clamp(finalChance, 0.05, 0.95);

            bool success = Utils.DetermineSuccess(finalChance);

            if (success)
            {
                // Also consume a stick for kindling
                double kindlingUsed = inv.Pop(Resource.Stick);

                var playerSkill = ctx.player.Skills.GetSkill("Firecraft");

                if (relightingFire)
                {
                    GameDisplay.AddSuccess(ctx, $"Success! You relight the fire! ({finalChance:P0} chance)");
                    existingFire!.AddFuel(tinderUsed, tinderType);
                    existingFire.AddFuel(kindlingUsed, FuelType.Kindling);
                    existingFire.IgniteAll(); // Ignite all fuel that can ignite from cold (tinder + kindling)
                }
                else
                {
                    GameDisplay.AddSuccess(ctx, $"Success! You start a fire! ({finalChance:P0} chance)");
                    var newFire = new HeatSourceFeature();
                    newFire.AddFuel(tinderUsed, tinderType);
                    newFire.AddFuel(kindlingUsed, FuelType.Kindling);
                    newFire.IgniteAll(); // Ignite all fuel that can ignite from cold (tinder + kindling)
                    ctx.CurrentLocation.Features.Add(newFire);
                }

                playerSkill.GainExperience(3);

                // Tutorial: Fire mechanics after first fire start (Day 1 only)
                if (ctx.DaysSurvived == 0 && ctx.TryShowTutorial("first_fire_started"))
                {
                    GameDisplay.AddNarrative(ctx, "");
                    GameDisplay.AddWarning(ctx, "A night fire needs to burn 10-12 hours.");
                    GameDisplay.AddWarning(ctx, "Figure a good log burns about an hour per kilogram.");
                    GameDisplay.AddWarning(ctx, "You do the math.");
                }

                break;
            }
            else
            {
                GameDisplay.AddWarning(ctx, $"You failed to start the fire. The {tinderName} was wasted. ({finalChance:P0} chance)");
                ctx.player.Skills.GetSkill("Firecraft").GainExperience(1);

                // Check if retry is possible with any tinder type
                bool canRetry = (inv[Resource.Tinder].Count > 0 || inv[Resource.BirchBark].Count > 0 || inv[Resource.Amadou].Count > 0) && inv[Resource.Stick].Count > 0;
                if (canRetry)
                {
                    GameDisplay.Render(ctx, statusText: "Thinking.");
                    if (Input.Confirm(ctx, $"Try again with {selectedTool.Name}?"))
                        continue;
                }
                break;
            }
        }
    }

    public static double GetToolBaseChance(Gear tool)
    {
        return tool.Name switch
        {
            "Hand Drill" => 0.30,
            "Bow Drill" => 0.50,
            "Fire Striker" or "Flint and Steel" => 0.90,
            _ => 0.50  // Default for generic fire strikers
        };
    }
}
