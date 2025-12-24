using text_survival.Actors.Animals;
using text_survival.Actors;
using text_survival.Bodies;
using text_survival.Combat;
using text_survival.Environments.Features;
using text_survival.IO;
using text_survival.Items;
using text_survival.Actions.Expeditions;
using text_survival.Persistence;
using text_survival.UI;
using text_survival.Survival;

namespace text_survival.Actions;

public class Choice<T>(string? prompt = null)
{
    public string? Prompt = prompt;
    private readonly Dictionary<string, T> options = [];
    public void AddOption(string label, T item)
    {
        options[label] = item;
    }
    public T GetPlayerChoice(GameContext ctx)
    {
        if (options.Count == 0)
        {
            throw new InvalidOperationException("No Choices Available");
        }
        string choice = Input.Select(ctx, Prompt ?? "Choose:", options.Keys);
        return options[choice];
    }
}

public partial class GameRunner(GameContext ctx)
{
    private readonly GameContext ctx = ctx;

    public void Run()
    {
        while (ctx.player.IsAlive)
        {
            GameDisplay.Render(ctx, statusText: "Resting.");
            CheckFireWarning();
            RunCampMenu();
        }

        // Player died from survival conditions - show death message
        GameDisplay.AddDanger(ctx, "Your vision fades to black as you collapse...");
        GameDisplay.AddDanger(ctx, "You have died.");
        GameDisplay.Render(ctx, addSeparator: false);
        Input.WaitForKey(ctx);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // MAIN MENU
    // ═══════════════════════════════════════════════════════════════════════════

    private void RunCampMenu()
    {
        // Auto-save when at camp menu
        SaveManager.Save(ctx);

        var choice = new Choice<Action>();
        var capacities = ctx.player.GetCapacities();
        var isImpaired = AbilityCalculator.IsConsciousnessImpaired(capacities.Consciousness);
        var isLimping = AbilityCalculator.IsMovingImpaired(capacities.Moving);
        var isClumsy = AbilityCalculator.IsManipulationImpaired(capacities.Manipulation);
        var isFoggy = AbilityCalculator.IsPerceptionImpaired(
            AbilityCalculator.CalculatePerception(ctx.player.Body, ctx.player.EffectRegistry.GetCapacityModifiers()));
        var isWinded = AbilityCalculator.IsBreathingImpaired(capacities.Breathing);

        if (CanRestByFire())
            choice.AddOption("Wait", Wait);

        if (HasActiveFire())
            choice.AddOption("Tend fire", TendFire);

        if (CanStartFire())
            choice.AddOption("Start fire", StartFire);

        // Work around camp - foraging, exploring without leaving
        if (HasCampWork())
        {
            string workLabel = "Work near camp";
            if (isFoggy && isWinded)
                workLabel = "Work near camp (foggy, winded)";
            else if (isFoggy)
                workLabel = "Work near camp (your senses are dulled)";
            else if (isWinded)
                workLabel = "Work near camp (you're short of breath)";
            choice.AddOption(workLabel, WorkAroundCamp);
        }

        // Leave camp - travel to other locations (only show if destinations exist)
        if (ctx.Camp.Location.Connections.Count > 0)
        {
            // Build descriptors for travel-affecting impairments
            var travelImpairments = new List<string>();
            if (isImpaired) travelImpairments.Add("impaired");
            if (isLimping) travelImpairments.Add("limping");
            if (isWinded) travelImpairments.Add("winded");

            string leaveLabel = travelImpairments.Count switch
            {
                0 => "Leave camp",
                1 => travelImpairments[0] switch
                {
                    "impaired" => "Leave camp (you're not thinking clearly)",
                    "limping" => "Leave camp (your movement is limited)",
                    "winded" => "Leave camp (you're short of breath)",
                    _ => "Leave camp"
                },
                _ => $"Leave camp ({string.Join(", ", travelImpairments)})"
            };
            choice.AddOption(leaveLabel, LeaveCamp);
        }

        // Eat/Drink - consume food and water
        if (ctx.Inventory.HasFood || ctx.Inventory.HasWater)
            choice.AddOption("Eat/Drink", EatDrink);

        // Cook/Melt - requires active fire
        if (HasActiveFire() && (ctx.Inventory.RawMeat.Count > 0 || true)) // Snow always available (Ice Age)
            choice.AddOption("Cook/Melt", CookMelt);

        // Torch management
        if (CanLightTorch())
            choice.AddOption("Light torch", LightTorch);
        if (ctx.Inventory.HasLitTorch)
        {
            int mins = (int)ctx.Inventory.TorchBurnTimeRemainingMinutes;
            choice.AddOption($"Extinguish torch ({mins} min remaining)", ExtinguishTorch);
        }

        // Crafting - make tools from available materials
        if (ctx.Inventory.HasCraftingMaterials)
        {
            string craftLabel = isClumsy ? "Crafting (your hands are unsteady)" : "Crafting";
            choice.AddOption(craftLabel, RunCrafting);
        }

        if (HasItems() || ctx.Camp.Storage.CurrentWeightKg > 0)
            choice.AddOption("Inventory", RunInventoryMenu);

        // Curing rack - if player has one at camp
        if (ctx.Camp.CuringRack != null)
        {
            var rack = ctx.Camp.CuringRack;
            string rackLabel = rack.HasReadyItems
                ? "Curing rack (items ready!)"
                : rack.ItemCount > 0
                    ? $"Curing rack ({rack.ItemCount} items curing)"
                    : "Curing rack (empty)";
            choice.AddOption(rackLabel, UseCuringRack);
        }

        // Direct treatments - when player has treatable conditions and materials
        if (CanApplyDirectTreatment())
            choice.AddOption("Treat wounds", ApplyDirectTreatment);

        // Sleep emphasized when impaired
        if (isImpaired || ctx.player.Body.IsTired)
        {
            string sleepLabel = isImpaired ? "Sleep (you need rest)" : "Sleep";
            choice.AddOption(sleepLabel, Sleep);
        }

        choice.GetPlayerChoice(ctx).Invoke();
    }

    private bool HasCampWork() =>
        WorkRunner.HasWorkOptions(ctx, ctx.Camp.Location);


    private void WorkAroundCamp()
    {
        var campLocation = ctx.Camp.Location;
        var work = new WorkRunner(ctx);

        var choice = new Choice<string>("What do you want to do?");

        if (campLocation.HasFeature<ForageFeature>())
        {
            var forage = campLocation.GetFeature<ForageFeature>()!;
            choice.AddOption($"Forage nearby ({forage.GetQualityDescription()})", "forage");
        }

        if (ctx.Zone.HasUnrevealedLocations())
            choice.AddOption("Scout the area (discover new locations)", "scout");

        choice.AddOption("Cancel", "cancel");

        string action = choice.GetPlayerChoice(ctx);

        switch (action)
        {
            case "forage":
                work.DoForage(campLocation);
                break;
            case "scout":
                work.DoExplore(campLocation);
                break;
        }
    }

    private void RunCrafting()
    {
        var craftingRunner = new CraftingRunner(ctx);
        craftingRunner.Run();
    }

    private void LeaveCamp()
    {
        var expeditionRunner = new ExpeditionRunner(ctx);
        expeditionRunner.Run();
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // FIRE MANAGEMENT
    // ═══════════════════════════════════════════════════════════════════════════

    private bool HasActiveFire()
    {
        var fire = ctx.CurrentLocation.GetFeature<HeatSourceFeature>();
        if (fire == null) return false;

        // Show "Tend fire" if there's an active fire AND we have fuel to add
        return (fire.IsActive || fire.HasEmbers) && ctx.Inventory.HasFuel;
    }

    private void CheckFireWarning()
    {
        var fire = ctx.CurrentLocation.GetFeature<HeatSourceFeature>();
        if (fire == null || (!fire.IsActive && !fire.HasEmbers))
            return;

        // Don't warn when fire is growing
        string phase = fire.GetFirePhase();
        if (phase == "Igniting" || phase == "Building")
            return;

        int minutes = (int)(fire.HoursRemaining * 60);

        if (minutes <= 5)
            GameDisplay.AddDanger(ctx, $"Your fire will die in {minutes} minutes!");
        else if (minutes <= 15)
            GameDisplay.AddWarning(ctx, $"Fire burning low - {minutes} minutes remaining.");
    }

    private bool CanStartFire()
    {
        var fire = ctx.CurrentLocation.GetFeature<HeatSourceFeature>();

        bool noFire = fire == null;
        bool coldFire = fire != null && !fire.IsActive && !fire.HasEmbers;

        if (!noFire && !coldFire) return false;

        // Need a fire tool and materials
        bool hasTool = ctx.Inventory.Tools.Any(t =>
            t.Type == ToolType.FireStriker ||
            t.Type == ToolType.HandDrill ||
            t.Type == ToolType.BowDrill);
        return hasTool && ctx.Inventory.CanStartFire;
    }

    private bool CanRestByFire()
    {
        var fire = ctx.CurrentLocation.GetFeature<HeatSourceFeature>();
        return fire != null && fire.IsActive;
    }

    private void TendFire()
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
            AddWoodFuelOption(fuelChoices, fuelMap, fire, inv.Pine, "pine", FuelType.PineWood, () => inv.Pine.Pop(), "burns fast");
            AddWoodFuelOption(fuelChoices, fuelMap, fire, inv.Birch, "birch", FuelType.BirchWood, () => inv.Birch.Pop(), "steady burn");
            AddWoodFuelOption(fuelChoices, fuelMap, fire, inv.Oak, "oak", FuelType.OakWood, () => inv.Oak.Pop(), "long burn");

            // Generic logs fallback (for legacy inventory)
            if (inv.Logs.Count > 0)
            {
                if (fire.CanAddFuel(FuelType.Softwood))
                {
                    string label = $"Add log ({inv.Logs.Count} @ {inv.Logs.Sum():F1}kg)";
                    fuelChoices.Add(label);
                    fuelMap[label] = ("log", FuelType.Softwood, () => inv.Logs.Pop());
                }
                else
                {
                    string disabledLabel = "[dim]Add log (fire too small)[/]";
                    fuelChoices.Add(disabledLabel);
                    fuelMap[disabledLabel] = ("disabled", FuelType.Softwood, () => 0);
                }
            }

            if (inv.Sticks.Count > 0 && fire.CanAddFuel(FuelType.Kindling))
            {
                string label = $"Add stick ({inv.Sticks.Count} @ {inv.Sticks.Sum():F1}kg)";
                fuelChoices.Add(label);
                fuelMap[label] = ("stick", FuelType.Kindling, () => inv.Sticks.Pop());
            }

            // Birch bark as excellent tinder
            if (inv.BirchBark.Count > 0 && fire.CanAddFuel(FuelType.BirchBark))
            {
                string label = $"Add birch bark ({inv.BirchBark.Count} @ {inv.BirchBark.Sum():F2}kg) - great tinder";
                fuelChoices.Add(label);
                fuelMap[label] = ("birch bark", FuelType.BirchBark, () => inv.BirchBark.Pop());
            }

            if (inv.Tinder.Count > 0 && fire.CanAddFuel(FuelType.Tinder))
            {
                string label = $"Add tinder ({inv.Tinder.Count} @ {inv.Tinder.Sum():F2}kg)";
                fuelChoices.Add(label);
                fuelMap[label] = ("tinder", FuelType.Tinder, () => inv.Tinder.Pop());
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
                    inv.Charcoal += collected;
                    GameDisplay.AddSuccess(ctx, $"You collect {collected:F2}kg of charcoal from the fire pit.");
                    return collected;
                });
            }

            if (fuelChoices.Count == 0 && !hasCharcoal)
            {
                // Check if we have fuel but fire is too cold
                bool hasFuelButTooCold = (inv.Logs.Count > 0 || inv.Sticks.Count > 0) && inv.Tinder.Count == 0;
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

    private void StartFire()
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
            t.Type == ToolType.FireStriker ||
            t.Type == ToolType.HandDrill ||
            t.Type == ToolType.BowDrill).ToList();

        if (fireTools.Count == 0)
        {
            GameDisplay.AddWarning(ctx, "You don't have any fire-making tools!");
            return;
        }

        bool hasTinder = inv.Tinder.Count > 0 || inv.BirchBark.Count > 0 || inv.Amadou.Count > 0;
        bool hasKindling = inv.Sticks.Count > 0;
        bool hasBirchBark = inv.BirchBark.Count > 0;
        bool hasAmadou = inv.Amadou.Count > 0;

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
        if (inv.Tinder.Count > 0) tinderParts.Add($"{inv.Tinder.Count} tinder");
        if (hasBirchBark) tinderParts.Add($"{inv.BirchBark.Count} birch bark");
        if (hasAmadou) tinderParts.Add($"{inv.Amadou.Count} amadou");
        GameDisplay.AddNarrative(ctx, $"Materials: {string.Join(", ", tinderParts)}, {inv.Sticks.Count} kindling");

        // Build tool options with success chances
        var toolChoices = new List<string>();
        var toolMap = new Dictionary<string, (Tool tool, double chance)>();

        foreach (var tool in fireTools)
        {
            double baseChance = GetFireToolBaseChance(tool);
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

        if (isImpaired)
            GameDisplay.AddWarning(ctx, "Your foggy mind makes this harder.");
        if (isClumsy)
            GameDisplay.AddWarning(ctx, "Your unsteady hands make this harder.");

        while (true)
        {
            GameDisplay.AddNarrative(ctx, $"You work with the {selectedTool.Name}...");
            GameDisplay.UpdateAndRenderProgress(ctx, "Starting fire...", 10, ActivityType.TendingFire);

            double baseChance = GetFireToolBaseChance(selectedTool);
            var skill = ctx.player.Skills.GetSkill("Firecraft");
            double finalChance = baseChance + (skill.Level * 0.1);

            // Consciousness impairment penalty (-20%)
            if (isImpaired)
                finalChance -= 0.2;

            // Manipulation impairment penalty (-25%)
            if (isClumsy)
                finalChance -= 0.25;

            // Select best available tinder and get its ignition bonus
            // Priority: Amadou (best) > BirchBark (great) > Regular Tinder
            double tinderUsed;
            FuelType tinderType;
            string tinderName;
            if (inv.Amadou.Count > 0)
            {
                tinderUsed = inv.Amadou.Pop();
                tinderType = FuelType.Tinder; // Amadou burns like tinder
                tinderName = "amadou";
                finalChance += 0.20; // Amadou is the best fire-starting material
            }
            else if (inv.BirchBark.Count > 0)
            {
                tinderUsed = inv.BirchBark.Pop();
                tinderType = FuelType.BirchBark;
                tinderName = "birch bark";
                finalChance += FuelDatabase.Get(FuelType.BirchBark).IgnitionBonus;
            }
            else
            {
                tinderUsed = inv.Tinder.Pop();
                tinderType = FuelType.Tinder;
                tinderName = "tinder";
                finalChance += FuelDatabase.Get(FuelType.Tinder).IgnitionBonus;
            }

            finalChance = Math.Clamp(finalChance, 0.05, 0.95);

            bool success = Utils.DetermineSuccess(finalChance);

            if (success)
            {
                // Also consume a stick for kindling
                double kindlingUsed = inv.Sticks.Pop();

                var playerSkill = ctx.player.Skills.GetSkill("Firecraft");

                if (relightingFire)
                {
                    GameDisplay.AddSuccess(ctx, $"Success! You relight the fire! ({finalChance:P0} chance)");
                    existingFire!.AddFuel(tinderUsed, tinderType);
                    existingFire.AddFuel(kindlingUsed, FuelType.Kindling);
                    existingFire.IgniteFuel(tinderType, tinderUsed);
                }
                else
                {
                    GameDisplay.AddSuccess(ctx, $"Success! You start a fire! ({finalChance:P0} chance)");
                    var newFire = new HeatSourceFeature();
                    newFire.AddFuel(tinderUsed, tinderType);
                    newFire.AddFuel(kindlingUsed, FuelType.Kindling);
                    newFire.IgniteFuel(tinderType, tinderUsed);
                    ctx.CurrentLocation.Features.Add(newFire);
                }

                playerSkill.GainExperience(3);
                break;
            }
            else
            {
                GameDisplay.AddWarning(ctx, $"You failed to start the fire. The {tinderName} was wasted. ({finalChance:P0} chance)");
                ctx.player.Skills.GetSkill("Firecraft").GainExperience(1);

                // Check if retry is possible with any tinder type
                bool canRetry = (inv.Tinder.Count > 0 || inv.BirchBark.Count > 0 || inv.Amadou.Count > 0) && inv.Sticks.Count > 0;
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

    private static double GetFireToolBaseChance(Tool tool)
    {
        return tool.Name switch
        {
            "Hand Drill" => 0.30,
            "Bow Drill" => 0.50,
            "Fire Striker" or "Flint and Steel" => 0.90,
            _ => 0.50  // Default for generic fire strikers
        };
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // TORCH MANAGEMENT
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Check if player can light a torch.
    /// Requires: unlit torch AND (active fire OR lit torch OR tinder+firestarter)
    /// </summary>
    private bool CanLightTorch()
    {
        if (!ctx.Inventory.HasUnlitTorch) return false;

        // Can light from active fire (free)
        var fire = ctx.CurrentLocation.GetFeature<HeatSourceFeature>();
        if (fire?.IsActive == true) return true;

        // Can light from another lit torch (free)
        if (ctx.Inventory.HasLitTorch) return true;

        // Can light with tinder + firestarter (consumes tinder)
        bool hasTinder = ctx.Inventory.HasTinder;
        bool hasFirestarter = ctx.Inventory.Tools.Any(t =>
            t.Type is ToolType.FireStriker or ToolType.HandDrill or ToolType.BowDrill);
        return hasTinder && hasFirestarter;
    }

    /// <summary>
    /// Light a torch from available flame source.
    /// From fire/torch: FREE. From tinder+firestarter: consumes tinder.
    /// </summary>
    private void LightTorch()
    {
        var fire = ctx.CurrentLocation.GetFeature<HeatSourceFeature>();
        bool hasActiveFire = fire?.IsActive == true;
        bool hasLitTorch = ctx.Inventory.HasLitTorch;

        // Determine lighting method
        if (hasActiveFire)
        {
            // Free lighting from fire
            ctx.Inventory.LightTorch();
            GameDisplay.AddSuccess(ctx, "You light a torch from the fire. It burns steadily.");
            ctx.Update(1, ActivityType.Idle);
        }
        else if (hasLitTorch)
        {
            // Free lighting from existing torch
            ctx.Inventory.LightTorch();
            GameDisplay.AddSuccess(ctx, "You light a fresh torch from your dying flame.");
            ctx.Update(1, ActivityType.Idle);
        }
        else
        {
            // Need to use firestarter + tinder (same mechanics as fire starting but simpler)
            LightTorchWithFirestarter();
        }
    }

    /// <summary>
    /// Light a torch using firestarter and tinder (similar to fire starting but simpler).
    /// </summary>
    private void LightTorchWithFirestarter()
    {
        var inv = ctx.Inventory;

        // Get fire-making tools
        var fireTools = inv.Tools.Where(t =>
            t.Type is ToolType.FireStriker or ToolType.HandDrill or ToolType.BowDrill).ToList();

        if (fireTools.Count == 0)
        {
            GameDisplay.AddWarning(ctx, "You don't have a fire-making tool!");
            return;
        }

        // Build tool options
        var toolChoices = new List<string>();
        var toolMap = new Dictionary<string, (Tool tool, double chance)>();

        foreach (var tool in fireTools)
        {
            double baseChance = GetFireToolBaseChance(tool);
            var skill = ctx.player.Skills.GetSkill("Firecraft");
            double successChance = baseChance + (skill.Level * 0.1);
            successChance = Math.Clamp(successChance, 0.05, 0.95);

            string label = $"{tool.Name} - {successChance:P0} success";
            toolChoices.Add(label);
            toolMap[label] = (tool, successChance);
        }
        toolChoices.Add("Cancel");

        GameDisplay.Render(ctx, statusText: "Preparing.");
        string choice = Input.Select(ctx, "Light torch with:", toolChoices);

        if (choice == "Cancel")
        {
            GameDisplay.AddNarrative(ctx, "You decide not to light the torch.");
            return;
        }

        var (selectedTool, _) = toolMap[choice];

        // Consume tinder
        inv.Take("Tinder");

        double finalChance = GetFireToolBaseChance(selectedTool);
        var playerSkill = ctx.player.Skills.GetSkill("Firecraft");
        finalChance += playerSkill.Level * 0.1;
        finalChance = Math.Clamp(finalChance, 0.05, 0.95);

        GameDisplay.AddNarrative(ctx, $"You work with the {selectedTool.Name}...");
        ctx.Update(2, ActivityType.TendingFire);

        if (Utils.DetermineSuccess(finalChance))
        {
            inv.LightTorch();
            GameDisplay.AddSuccess(ctx, $"Success! The torch catches fire. ({finalChance:P0} chance)");
            playerSkill.GainExperience(1);
        }
        else
        {
            GameDisplay.AddWarning(ctx, $"The tinder fizzles out. The torch didn't light. ({finalChance:P0} chance)");
            playerSkill.GainExperience(1);

            // Offer retry if materials available
            if (inv.HasTinder)
            {
                GameDisplay.Render(ctx, statusText: "Thinking.");
                if (Input.Confirm(ctx, "Try again?"))
                    LightTorchWithFirestarter();
            }
        }
    }

    /// <summary>
    /// Extinguish an active torch. The torch is consumed (cannot be relit).
    /// </summary>
    private void ExtinguishTorch()
    {
        if (ctx.Inventory.ActiveTorch == null) return;

        GameDisplay.AddNarrative(ctx, "You snuff out the torch. It crumbles to ash.");
        ctx.Inventory.ActiveTorch = null;
        ctx.Inventory.TorchBurnTimeRemainingMinutes = 0;
    }

    private void Sleep()
    {
        int hours = Input.ReadInt(ctx, "How many hours would you like to sleep?", 1, 12);

        int totalMinutes = hours * 60;
        int slept = 0;

        while (slept < totalMinutes && ctx.player.IsAlive)
        {
            // Sleep in 60-minute chunks, checking for events
            int chunkMinutes = Math.Min(60, totalMinutes - slept);
            ctx.player.Body.Rest(chunkMinutes);

            int minutes = ctx.Update(chunkMinutes, ActivityType.Sleeping, render: true);
            slept += minutes;
        }

        if (slept > 0)
            GameDisplay.AddNarrative(ctx, $"You slept for {slept / 60} hours.");
    }

    private void Wait()
    {
        GameDisplay.UpdateAndRenderProgress(ctx, "Resting", 5, ActivityType.Resting);
    }

    private bool HasItems()
    {
        var inv = ctx.Inventory;
        return inv.HasFuel || inv.HasFood || inv.HasWater || inv.Tools.Count > 0;
    }

    private void RunInventoryMenu()
    {
        bool atCamp = ctx.CurrentLocation == ctx.Camp.Location;

        if (!atCamp)
        {
            // Not at camp - just show read-only inventory view
            GameDisplay.RenderInventoryScreen(ctx);
            Input.WaitForKey("Press any key to return...");
            if (ctx.SessionId != null)
                Web.WebIO.ClearInventory(ctx);
            return;
        }

        // At camp - use shared transfer helper
        InventoryTransferHelper.RunTransferMenu(ctx, ctx.Camp.Storage, "CAMP STORAGE");
    }

    private void EatDrink()
    {
        var inv = ctx.Inventory;
        var body = ctx.player.Body;

        while (true)
        {
            int caloriesPercent = (int)(body.CalorieStore / Survival.SurvivalProcessor.MAX_CALORIES * 100);
            int hydrationPercent = (int)(body.Hydration / Survival.SurvivalProcessor.MAX_HYDRATION * 100);
            GameDisplay.AddNarrative(ctx, $"Food: {caloriesPercent}% | Water: {hydrationPercent}%");
            GameDisplay.Render(ctx, statusText: "Eating.");

            var options = new List<string>();
            var consumeActions = new Dictionary<string, Action>();

            // Add food options
            if (inv.CookedMeat.Count > 0)
            {
                double w = inv.CookedMeat.Peek();
                string opt = $"Cooked meat ({w:F1}kg) - ~{(int)(w * 2500)} cal";
                options.Add(opt);
                consumeActions[opt] = () =>
                {
                    double eaten = inv.CookedMeat.Pop();
                    body.AddCalories(eaten * 2500);
                    GameDisplay.AddSuccess(ctx, $"You eat the cooked meat. (+{(int)(eaten * 2500)} cal)");
                };
            }

            if (inv.RawMeat.Count > 0)
            {
                double w = inv.RawMeat.Peek();
                string opt = $"Raw meat ({w:F1}kg) - ~{(int)(w * 1500)} cal [risk of illness]";
                options.Add(opt);
                consumeActions[opt] = () =>
                {
                    double eaten = inv.RawMeat.Pop();
                    body.AddCalories(eaten * 1500);
                    GameDisplay.AddWarning(ctx, $"You eat the raw meat. (+{(int)(eaten * 1500)} cal)");
                    // TODO: Add chance of food poisoning
                };
            }

            if (inv.Berries.Count > 0)
            {
                double w = inv.Berries.Peek();
                string opt = $"Berries ({w:F2}kg) - ~{(int)(w * 500)} cal";
                options.Add(opt);
                consumeActions[opt] = () =>
                {
                    double eaten = inv.Berries.Pop();
                    body.AddCalories(eaten * 500);
                    body.AddHydration(eaten * 200); // Berries have some water content
                    GameDisplay.AddSuccess(ctx, $"You eat the berries. (+{(int)(eaten * 500)} cal)");
                };
            }

            if (inv.HasWater)
            {
                // Drink up to 1L, but don't waste water over hydration max
                double hydrationRoom = (SurvivalProcessor.MAX_HYDRATION - body.Hydration) / 1000.0; // convert to liters
                double toDrink = Math.Min(1.0, Math.Min(inv.WaterLiters, hydrationRoom));
                toDrink = Math.Round(toDrink, 2); // clean up floating point

                if (toDrink >= 0.01)
                {
                    string opt = $"Drink water ({toDrink:F2}L)";
                    options.Add(opt);
                    consumeActions[opt] = () =>
                    {
                        inv.WaterLiters -= toDrink;
                        body.AddHydration(toDrink * 1000);

                        // Drinking water helps cool down when overheating (scales with amount)
                        var hyperthermia = ctx.player.EffectRegistry.GetEffectsByKind("Hyperthermia").FirstOrDefault();
                        if (hyperthermia != null)
                        {
                            double cooldown = 0.15 * (toDrink / 0.25); // 0.15 per 0.25L
                            hyperthermia.Severity = Math.Max(0, hyperthermia.Severity - cooldown);
                            GameDisplay.AddSuccess(ctx, "You drink some water. The cool water helps you cool down.");
                        }
                        else
                        {
                            GameDisplay.AddSuccess(ctx, "You drink some water.");
                        }
                    };
                }
            }

            options.Add("Done");

            if (options.Count == 1)
            {
                GameDisplay.AddNarrative(ctx, "You have nothing to eat or drink.");
                GameDisplay.Render(ctx);
                break;
            }

            string choice = Input.Select(ctx, "What would you like to consume?", options);

            if (choice == "Done")
                break;

            consumeActions[choice]();
            ctx.Update(5, ActivityType.Eating, render: true);
        }
    }

    private void CookMelt()
    {
        var inv = ctx.Inventory;

        while (true)
        {
            GameDisplay.Render(ctx, statusText: "Cooking.");

            var options = new List<string>();
            var actions = new Dictionary<string, Action>();

            // Cook raw meat
            if (inv.RawMeat.Count > 0)
            {
                double w = inv.RawMeat.Peek();
                string opt = $"Cook raw meat ({w:F1}kg) - 15 min";
                options.Add(opt);
                actions[opt] = () =>
                {
                    GameDisplay.UpdateAndRenderProgress(ctx, "Cooking meat...", 15, ActivityType.Cooking);
                    double cooked = inv.RawMeat.Pop();
                    inv.CookedMeat.Push(cooked);
                    GameDisplay.AddSuccess(ctx, $"Cooked {cooked:F1}kg of meat.");
                };
            }

            // Melt snow (always available in Ice Age)
            string snowOpt = "Melt snow for water - 10 min";
            options.Add(snowOpt);
            actions[snowOpt] = () =>
            {
                GameDisplay.UpdateAndRenderProgress(ctx, "Melting snow...", 10, ActivityType.Cooking);
                inv.WaterLiters += 0.5;
                GameDisplay.AddSuccess(ctx, "Melted snow into 0.5L of water.");
            };

            options.Add("Done");

            string choice = Input.Select(ctx, "What would you like to do?", options);

            if (choice == "Done")
                break;

            actions[choice]();
            GameDisplay.Render(ctx, statusText: "Cooking.");
            Input.WaitForKey(ctx);
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // CURING RACK
    // ═══════════════════════════════════════════════════════════════════════════

    private void UseCuringRack()
    {
        var rack = ctx.Camp.CuringRack!;
        var inv = ctx.Inventory;

        while (true)
        {
            GameDisplay.AddNarrative(ctx, rack.GetDescription());
            GameDisplay.Render(ctx, statusText: "Checking rack.");

            var options = new List<string>();
            var actions = new Dictionary<string, Action>();

            // Collect finished items
            if (rack.HasReadyItems)
            {
                string collectOpt = "Collect finished items";
                options.Add(collectOpt);
                actions[collectOpt] = () =>
                {
                    int collected = rack.CollectFinished(inv);
                    GameDisplay.AddSuccess(ctx, $"You collected {collected} item(s) from the rack.");
                };
            }

            // Add items to rack (if space available)
            if (rack.HasSpace)
            {
                // Scraped hide -> Cured hide
                if (inv.ScrapedHide.Count > 0)
                {
                    double w = inv.ScrapedHide.Peek();
                    string opt = $"Hang scraped hide ({w:F1}kg) - 2 days to cure";
                    options.Add(opt);
                    actions[opt] = () =>
                    {
                        double weight = inv.ScrapedHide.Pop();
                        rack.AddItem(CurableItemType.ScrapedHide, weight);
                        GameDisplay.AddSuccess(ctx, "You hang the hide on the rack to cure.");
                    };
                }

                // Raw meat -> Dried meat
                if (inv.RawMeat.Count > 0)
                {
                    double w = inv.RawMeat.Peek();
                    string opt = $"Hang raw meat ({w:F1}kg) - 1 day to dry";
                    options.Add(opt);
                    actions[opt] = () =>
                    {
                        double weight = inv.RawMeat.Pop();
                        rack.AddItem(CurableItemType.RawMeat, weight);
                        GameDisplay.AddSuccess(ctx, "You hang the meat on the rack to dry.");
                    };
                }

                // Berries -> Dried berries
                if (inv.Berries.Count > 0)
                {
                    double w = inv.Berries.Peek();
                    string opt = $"Spread berries ({w:F2}kg) - 12 hours to dry";
                    options.Add(opt);
                    actions[opt] = () =>
                    {
                        double weight = inv.Berries.Pop();
                        rack.AddItem(CurableItemType.Berries, weight);
                        GameDisplay.AddSuccess(ctx, "You spread the berries on the rack to dry.");
                    };
                }
            }
            else if (!rack.HasReadyItems)
            {
                GameDisplay.AddNarrative(ctx, "The rack is full.");
            }

            options.Add("Done");

            if (options.Count == 1)
            {
                // Nothing to do
                break;
            }

            string choice = Input.Select(ctx, "Curing rack:", options);

            if (choice == "Done")
                break;

            actions[choice]();
            ctx.Update(2, ActivityType.Crafting); // Brief time to add/collect
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // COMBAT
    // ═══════════════════════════════════════════════════════════════════════════

    private void StartCombat(Animal enemy)
    {
        if (!enemy.IsAlive) return;

        GameDisplay.AddNarrative(ctx, "!");
        Thread.Sleep(500);
        GameDisplay.AddNarrative(ctx, CombatNarrator.DescribeCombatStart(ctx.player, enemy));

        ctx.player.IsEngaged = true;
        enemy.IsEngaged = true;

        bool enemyFirstStrike = enemy.Speed > ctx.player.Speed;

        if (enemyFirstStrike)
        {
            GameDisplay.AddNarrative(ctx, $"The {enemy.Name} moves with surprising speed!");
            Thread.Sleep(500);
            EnemyCombatTurn(enemy);
        }
        else
        {
            GameDisplay.AddNarrative(ctx, "You're quick to react, giving you the initiative!");
            Thread.Sleep(500);
            PlayerCombatTurn(enemy);
        }
    }

    private void PlayerCombatTurn(Animal enemy)
    {
        if (!ctx.player.IsAlive || !enemy.IsAlive || !ctx.player.IsEngaged)
        {
            EndCombat(enemy);
            return;
        }

        GameDisplay.AddNarrative(ctx, "─────────────────────────────────────");
        DisplayCombatStatus(enemy);

        var choice = new Choice<Action>();
        choice.AddOption($"Attack {enemy.Name}", () => AttackEnemy(enemy));

        if (ctx.player.Skills.Fighting.Level > 1)
            choice.AddOption($"Targeted Attack {enemy.Name}", () => TargetedAttackEnemy(enemy));

        if (ctx.player.Speed > 0.25)
            choice.AddOption("Flee", () => AttemptFlee(enemy));

        choice.GetPlayerChoice(ctx).Invoke();
    }

    private void EnemyCombatTurn(Animal enemy)
    {
        if (!ctx.player.IsAlive || !enemy.IsAlive || !enemy.IsEngaged)
        {
            EndCombat(enemy);
            return;
        }

        Thread.Sleep(500);
        enemy.Attack(ctx.player, null, null, ctx);

        if (!ctx.player.IsAlive || !enemy.IsAlive)
            EndCombat(enemy);
        else
            PlayerCombatTurn(enemy);
    }

    private void AttackEnemy(Animal enemy)
    {
        ctx.player.Attack(enemy, ctx.Inventory.Weapon, null, ctx);

        if (!enemy.IsAlive)
            EndCombat(enemy);
        else
            EnemyCombatTurn(enemy);
    }

    private void TargetedAttackEnemy(Animal enemy)
    {
        int fightingSkill = ctx.player.Skills.Fighting.Level;
        var targetPart = SelectTargetPart(enemy, fightingSkill);

        if (targetPart != null)
        {
            ctx.player.Attack(enemy, ctx.Inventory.Weapon, targetPart.Name, ctx);

            if (!enemy.IsAlive)
                EndCombat(enemy);
            else
                EnemyCombatTurn(enemy);
        }
        else
        {
            PlayerCombatTurn(enemy);
        }
    }

    private BodyRegion? SelectTargetPart(Actor enemy, int depth)
    {
        if (depth <= 0)
        {
            GameDisplay.AddWarning(ctx, "You don't have enough skill to target an attack");
            return null;
        }

        GameDisplay.AddNarrative(ctx, $"Where do you want to target your attack on the {enemy.Name}?");

        List<BodyRegion> allParts = [];
        foreach (var part in enemy.Body.Parts)
        {
            if (depth > 0)
                allParts.Add(part);
        }

        return Input.SelectOrCancel("Select target:", allParts);
    }

    private void AttemptFlee(Animal enemy)
    {
        if (CombatUtils.SpeedCheck(ctx.player, enemy))
        {
            GameDisplay.AddNarrative(ctx, "You got away!");
            enemy.IsEngaged = false;
            ctx.player.IsEngaged = false;
            ctx.player.Skills.Reflexes.GainExperience(2);
        }
        else
        {
            GameDisplay.AddNarrative(ctx, $"You weren't fast enough to get away from {enemy.Name}!");
            ctx.player.Skills.Reflexes.GainExperience(1);
            EnemyCombatTurn(enemy);
        }
    }

    private void EndCombat(Animal enemy)
    {
        ctx.player.IsEngaged = false;
        enemy.IsEngaged = false;

        if (!ctx.player.IsAlive)
        {
            GameDisplay.AddDanger(ctx, "Your vision fades to black as you collapse... You have died!");
        }
        else if (!enemy.IsAlive)
        {
            string[] victoryMessages = [
                $"The {enemy.Name} collapses, defeated!",
                $"You stand victorious over the fallen {enemy.Name}!",
                $"With a final blow, you bring down the {enemy.Name}!"
            ];
            GameDisplay.AddNarrative(ctx, victoryMessages[Utils.RandInt(0, victoryMessages.Length - 1)]);

            int xpGain = CalculateExperienceGain(enemy);
            GameDisplay.AddNarrative(ctx, $"You've gained {xpGain} fighting experience!");
            ctx.player.Skills.Fighting.GainExperience(xpGain);
        }
    }

    private static int CalculateExperienceGain(Animal enemy)
    {
        int baseXP = 5;
        double sizeMultiplier = Math.Clamp(enemy.Body.WeightKG / 50, 0.5, 3.0);
        double damageMultiplier = Math.Clamp(enemy.AttackDamage / 8, 0.5, 2.0);
        return (int)(baseXP * sizeMultiplier * damageMultiplier);
    }

    private void DisplayCombatStatus(Animal enemy)
    {
        double playerVitality = ctx.player.Vitality;
        string playerStatus = $"You: {Math.Round(playerVitality * 100, 0)}% Vitality";
        AddHealthMessage(playerStatus, playerVitality);

        double enemyVitality = enemy.Vitality;
        string enemyStatus = $"{enemy.Name}: {Math.Round(enemyVitality * 100, 0)}% Vitality";
        AddHealthMessage(enemyStatus, enemyVitality);
    }

    private void AddHealthMessage(string message, double healthPercentage)
    {
        if (healthPercentage < 0.2)
            GameDisplay.AddDanger(ctx, message);
        else if (healthPercentage < 0.5)
            GameDisplay.AddWarning(ctx, message);
        else
            GameDisplay.AddSuccess(ctx, message);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // DIRECT TREATMENTS
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Direct treatments: apply resources directly to wounds/conditions.
    /// Different from crafted treatments (teas/poultices) which go through crafting.
    /// </summary>
    private static readonly List<(string Resource, string EffectKind, string Description, double EffectReduction)> DirectTreatments =
    [
        ("Amadou", "Bleeding", "Press amadou into wound to stop bleeding", 0.5),
        ("Sphagnum", "Bleeding", "Pack sphagnum moss as absorbent dressing", 0.4),
        ("PineResin", "Bleeding", "Seal wound with pine resin", 0.3),
        ("Usnea", "Fever", "Apply usnea as antimicrobial dressing", 0.25),
        ("BirchPolypore", "Fever", "Use birch polypore for infection", 0.2),
    ];

    private bool CanApplyDirectTreatment()
    {
        var effects = ctx.player.EffectRegistry.GetAll();
        var inv = ctx.Inventory;

        foreach (var (resource, effectKind, _, _) in DirectTreatments)
        {
            // Check if player has this effect
            if (!effects.Any(e => e.EffectKind.Equals(effectKind, StringComparison.OrdinalIgnoreCase)))
                continue;

            // Check if player has the resource
            if (inv.GetCount(resource) > 0)
                return true;
        }
        return false;
    }

    private void ApplyDirectTreatment()
    {
        var effects = ctx.player.EffectRegistry.GetAll();
        var inv = ctx.Inventory;

        // Build list of available treatments
        var available = new List<(string Resource, string EffectKind, string Description, double EffectReduction)>();

        foreach (var treatment in DirectTreatments)
        {
            // Check if player has this effect
            if (!effects.Any(e => e.EffectKind.Equals(treatment.EffectKind, StringComparison.OrdinalIgnoreCase)))
                continue;

            // Check if player has the resource
            if (inv.GetCount(treatment.Resource) > 0)
                available.Add(treatment);
        }

        if (available.Count == 0)
        {
            GameDisplay.AddNarrative(ctx, "You don't have the right materials to treat your wounds.");
            GameDisplay.Render(ctx, statusText: "Thinking.");
            Input.WaitForKey(ctx);
            return;
        }

        // Show current conditions
        var treatable = effects.Where(e => DirectTreatments.Any(t =>
            t.EffectKind.Equals(e.EffectKind, StringComparison.OrdinalIgnoreCase))).ToList();

        GameDisplay.AddNarrative(ctx, "Current conditions:");
        foreach (var effect in treatable)
        {
            string severity = effect.Severity switch
            {
                < 0.33 => "mild",
                < 0.66 => "moderate",
                _ => "severe"
            };
            GameDisplay.AddNarrative(ctx, $"  {effect.EffectKind} ({severity})");
        }

        // Build choice menu
        var choice = new Choice<(string, string, string, double)?>("Apply treatment:");

        foreach (var t in available)
        {
            string label = $"{t.Description} ({inv.GetCount(t.Resource)} available)";
            choice.AddOption(label, t);
        }
        choice.AddOption("Cancel", null);

        GameDisplay.Render(ctx, statusText: "Deciding.");
        var selected = choice.GetPlayerChoice(ctx);

        if (selected == null)
            return;

        var (resource, effectKind, description, reduction) = selected.Value;

        // Consume resource
        inv.Take(resource);

        // Find and reduce the effect
        var targetEffect = effects.FirstOrDefault(e =>
            e.EffectKind.Equals(effectKind, StringComparison.OrdinalIgnoreCase));

        if (targetEffect != null)
        {
            double oldSeverity = targetEffect.Severity;
            targetEffect.Severity = Math.Max(0, targetEffect.Severity - reduction);

            // Time cost for treatment
            ctx.Update(5, ActivityType.TendingFire); // Using TendingFire as closest match

            // Messages based on treatment effectiveness
            string resourceName = resource switch
            {
                "Amadou" => "amadou",
                "Sphagnum" => "sphagnum moss",
                "PineResin" => "pine resin",
                "Usnea" => "usnea lichen",
                "BirchPolypore" => "birch polypore",
                _ => resource.ToLower()
            };

            if (targetEffect.Severity <= 0)
            {
                ctx.player.EffectRegistry.RemoveEffect(targetEffect);
                GameDisplay.AddSuccess(ctx, $"You apply the {resourceName}. The {effectKind.ToLower()} has stopped.");
            }
            else if (targetEffect.Severity < oldSeverity * 0.5)
            {
                GameDisplay.AddSuccess(ctx, $"You apply the {resourceName}. The {effectKind.ToLower()} is much better.");
            }
            else
            {
                GameDisplay.AddNarrative(ctx, $"You apply the {resourceName}. The {effectKind.ToLower()} is slightly better.");
            }
        }

        GameDisplay.Render(ctx, statusText: "Treating.");
        Input.WaitForKey(ctx);
    }
}