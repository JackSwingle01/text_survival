using text_survival.Bodies;
using text_survival.Environments.Features;
using text_survival.IO;
using text_survival.Items;
using text_survival.UI;
using text_survival.Web;

namespace text_survival.Actions.Handlers;

public static class FireHandler
{
    public static void ManageFire(GameContext ctx, HeatSourceFeature? fire = null)
    {
        // Get existing fire or create temporary for starting mode
        fire ??= ctx.CurrentLocation.GetFeature<HeatSourceFeature>() ?? new HeatSourceFeature();
        WebIO.RunFireUI(ctx, fire);
        return;
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

        // Check for lit ember carrier - 100% success fire start
        var litCarrier = inv.Tools.FirstOrDefault(t => t.IsEmberCarrier && t.IsEmberLit);
        if (litCarrier != null)
        {
            // Need kindling at minimum
            if (inv.Count(Resource.Stick) <= 0)
            {
                GameDisplay.AddNarrative(ctx, $"Your {litCarrier.Name} smolders with an ember, but you have no kindling to start a fire.");
            }
            else
            {
                GameDisplay.Render(ctx, statusText: "Thinking.");
                if (Input.Confirm(ctx, $"Use your {litCarrier.Name} ({litCarrier.EmberBurnHoursRemaining:F1}h remaining) to start the fire?"))
                {
                    StartFireFromEmberCarrier(ctx, litCarrier, existingFire);
                    return;
                }
            }
        }

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

        bool hasBirchBark = inv.Count(Resource.BirchBark) > 0;
        bool hasAmadou = inv.Count(Resource.Amadou) > 0;
        bool hasUsnea = inv.Count(Resource.Usnea) > 0;
        bool hasChaga = inv.Count(Resource.Chaga) > 0;
        bool hasTinder = inv.Count(Resource.Tinder) > 0 || hasBirchBark || hasAmadou || hasUsnea || hasChaga;
        bool hasKindling = inv.Count(Resource.Stick) > 0;

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
        if (hasUsnea) tinderParts.Add($"{inv.Count(Resource.Usnea)} usnea");
        if (hasChaga) tinderParts.Add($"{inv.Count(Resource.Chaga)} chaga");
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
            // Priority: BirchBark (+25%) > Amadou (+20%) > Usnea (+18%) > Chaga/Tinder (+15%)
            double tinderUsed;
            FuelType tinderType;
            string tinderName;
            if (inv.Count(Resource.BirchBark) > 0)
            {
                tinderUsed = inv.Pop(Resource.BirchBark);
                tinderType = FuelType.BirchBark;
                tinderName = "birch bark";
                finalChance += FuelDatabase.Get(FuelType.BirchBark).IgnitionBonus;
            }
            else if (inv.Count(Resource.Amadou) > 0)
            {
                tinderUsed = inv.Pop(Resource.Amadou);
                tinderType = FuelType.Amadou;
                tinderName = "amadou";
                finalChance += FuelDatabase.Get(FuelType.Amadou).IgnitionBonus;
            }
            else if (inv.Count(Resource.Usnea) > 0)
            {
                tinderUsed = inv.Pop(Resource.Usnea);
                tinderType = FuelType.Usnea;
                tinderName = "usnea";
                finalChance += FuelDatabase.Get(FuelType.Usnea).IgnitionBonus;
            }
            else if (inv.Count(Resource.Chaga) > 0)
            {
                tinderUsed = inv.Pop(Resource.Chaga);
                tinderType = FuelType.Chaga;
                tinderName = "chaga";
                finalChance += FuelDatabase.Get(FuelType.Chaga).IgnitionBonus;
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
                string failureMessage = $"You failed to start the fire. The {tinderName} was wasted. ({finalChance:P0} chance)";
                GameDisplay.AddWarning(ctx, failureMessage);
                ctx.player.Skills.GetSkill("Firecraft").GainExperience(1);

                // Check if retry is possible with any tinder type
                bool canRetry = (inv[Resource.Tinder].Count > 0 || inv[Resource.BirchBark].Count > 0 ||
                    inv[Resource.Amadou].Count > 0 || inv[Resource.Usnea].Count > 0 ||
                    inv[Resource.Chaga].Count > 0) && inv[Resource.Stick].Count > 0;
                if (canRetry)
                {
                    GameDisplay.Render(ctx, statusText: "Thinking.");
                    if (Input.Confirm(ctx, $"{failureMessage}\n\nTry again with {selectedTool.Name}?"))
                        continue;
                }
                break;
            }
        }
    }

    private static void StartFireFromEmberCarrier(GameContext ctx, Gear carrier, HeatSourceFeature? existingFire)
    {
        var inv = ctx.Inventory;

        // Time cost
        GameDisplay.UpdateAndRenderProgress(ctx, "Starting fire from ember...", 5, ActivityType.TendingFire);

        // Consume kindling
        double kindlingUsed = inv.Pop(Resource.Stick);

        // Consume the carrier (remove from inventory)
        inv.Tools.Remove(carrier);

        if (existingFire != null)
        {
            GameDisplay.AddSuccess(ctx, $"The ember from your {carrier.Name} catches the kindling. The fire springs back to life!");
            existingFire.AddFuel(kindlingUsed, FuelType.Kindling);
            existingFire.IgniteAll();
        }
        else
        {
            GameDisplay.AddSuccess(ctx, $"The ember from your {carrier.Name} catches the kindling. You have a fire!");
            var newFire = new HeatSourceFeature();
            newFire.AddFuel(kindlingUsed, FuelType.Kindling);
            newFire.IgniteAll();
            ctx.CurrentLocation.Features.Add(newFire);
        }

        ctx.player.Skills.GetSkill("Firecraft").GainExperience(2);
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
