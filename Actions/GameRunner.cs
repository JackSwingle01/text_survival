using text_survival.Actors.Player;
using text_survival.Actors.NPCs;
using text_survival.Actors;
using text_survival.Bodies;
using text_survival.Combat;
using text_survival.Crafting;
using text_survival.Environments.Features;
using text_survival.IO;
using text_survival.Items;
using text_survival.Actions.Expeditions;
using text_survival.UI;

namespace text_survival.Actions;

public class Choice<T>(string? prompt = null)
{
    public string? Prompt = prompt;
    private readonly Dictionary<string, T> options = [];
    public void AddOption(string label, T item)
    {
        options[label] = item;
    }
    public T GetPlayerChoice()
    {
        if (options.Count == 0)
        {
            throw new InvalidOperationException("No Choices Available");
        }
        string choice = Input.Select(Prompt ?? "Choose:", options.Keys);
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
            GameDisplay.Render(ctx);
            RunCampMenu();
            ctx.Update(1);
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // MAIN MENU
    // ═══════════════════════════════════════════════════════════════════════════

    private void RunCampMenu()
    {
        ExpeditionRunner expeditionRunner = new(ctx);
        ExploreRunner exploreRunner = new (ctx);

        var choice = new Choice<Action>();

        if (HasActiveFire())
            choice.AddOption("Tend fire", TendFire);

        if (CanStartFire())
            choice.AddOption("Start fire", StartFire);

        if (expeditionRunner.GetGatherableLocations().Any() || expeditionRunner.GetHuntableLocations().Any())
            choice.AddOption("Go on expedition", ChooseExpeditionType);

        // if (exploreRunner.HasUnexploredReachable(state))
            choice.AddOption("Explore", exploreRunner.Run);
            
        if (HasItems())
            choice.AddOption("Inventory", RunInventoryMenu);

        if (ctx.player.Body.IsTired)
            choice.AddOption("Sleep", Sleep);

        choice.GetPlayerChoice().Invoke();
    }

    private void ChooseExpeditionType()
    {
        ExpeditionRunner expeditionRunner = new(ctx);
        var choice = new Choice<Action>();
        if (expeditionRunner.GetGatherableLocations().Any())
            choice.AddOption("Gather", expeditionRunner.RunForageExpedition);
        if (expeditionRunner.GetHuntableLocations().Any())
            choice.AddOption("Hunt", expeditionRunner.RunHuntExpedition);

        choice.GetPlayerChoice().Invoke();
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

    private bool CanStartFire()
    {
        var fire = ctx.CurrentLocation.GetFeature<HeatSourceFeature>();

        bool noFire = fire == null;
        bool coldFire = fire != null && !fire.IsActive && !fire.HasEmbers;

        if (!noFire && !coldFire) return false;

        // Need a fire tool and materials
        bool hasTool = ctx.Inventory.Tools.Any(t => t.Type == ToolType.FireStriker);
        return hasTool && ctx.Inventory.CanStartFire;
    }

    private void TendFire()
    {
        var fire = ctx.CurrentLocation.GetFeature<HeatSourceFeature>()!;
        var inv = ctx.Inventory;

        // Display fire status
        string firePhase = fire.GetFirePhase();
        double fireTemp = fire.GetCurrentFireTemperature();
        double heatOutput = fire.GetEffectiveHeatOutput(ctx.CurrentLocation.GetTemperature());
        double fuelMinutes = fire.HoursRemaining * 60;

        GameDisplay.AddNarrative("");
        GameDisplay.AddNarrative($"Fire: {firePhase} ({fireTemp:F0}°F)");

        if (fire.IsActive || fire.HasEmbers)
        {
            GameDisplay.AddNarrative($"  Heat: +{heatOutput:F1}°F | Fuel: {fire.FuelMassKg:F2}kg (~{fuelMinutes:F0} min)");

            if (fire.HasEmbers)
            {
                double emberMinutes = fire.EmberTimeRemaining * 60;
                GameDisplay.AddNarrative($"  Embers will last: {emberMinutes:F0} minutes");
            }
        }

        GameDisplay.AddNarrative($"  Capacity: {fire.FuelMassKg:F2}/{fire.MaxFuelCapacityKg:F1} kg");

        // Build fuel options - only show options that can actually be added
        var fuelChoices = new List<string>();
        var fuelMap = new Dictionary<string, (string name, FuelType type, Func<double> takeFunc)>();

        if (inv.Logs.Count > 0 && fire.CanAddFuel(FuelType.Softwood))
        {
            string label = $"Add log ({inv.Logs.Count} @ {inv.Logs.Sum():F1}kg)";
            fuelChoices.Add(label);
            fuelMap[label] = ("log", FuelType.Softwood, inv.TakeSmallestLog);
        }

        if (inv.Sticks.Count > 0 && fire.CanAddFuel(FuelType.Kindling))
        {
            string label = $"Add stick ({inv.Sticks.Count} @ {inv.Sticks.Sum():F1}kg)";
            fuelChoices.Add(label);
            fuelMap[label] = ("stick", FuelType.Kindling, inv.TakeSmallestStick);
        }

        if (inv.Tinder.Count > 0 && fire.CanAddFuel(FuelType.Tinder))
        {
            string label = $"Add tinder ({inv.Tinder.Count} @ {inv.Tinder.Sum():F2}kg)";
            fuelChoices.Add(label);
            fuelMap[label] = ("tinder", FuelType.Tinder, inv.TakeTinder);
        }

        if (fuelChoices.Count == 0)
        {
            // Check if we have fuel but fire is too cold
            bool hasFuelButTooCold = (inv.Logs.Count > 0 || inv.Sticks.Count > 0) && inv.Tinder.Count == 0;
            if (hasFuelButTooCold)
            {
                GameDisplay.AddWarning("The fire is too cold. You need tinder to build it up first.");
            }
            else
            {
                GameDisplay.AddNarrative("You have no fuel to add.");
            }
            Input.WaitForKey();
            return;
        }

        fuelChoices.Add("Cancel");

        GameDisplay.Render(ctx, addSeparator: false);
        string choice = Input.Select("Add fuel:", fuelChoices);

        if (choice == "Cancel")
            return;

        var (name, fuelType, takeFunc) = fuelMap[choice];
        bool hadEmbers = fire.HasEmbers;

        // Take fuel from inventory and add to fire
        double mass = takeFunc();
        fire.AddFuel(mass, fuelType);

        GameDisplay.AddNarrative($"You add a {name} ({mass:F2}kg) to the fire.");

        double newFuelMinutes = fire.HoursRemaining * 60;
        GameDisplay.AddNarrative($"Fire: {fire.FuelMassKg:F2}kg fuel ({newFuelMinutes:F0} min) | {fire.GetCurrentFireTemperature():F0}°F ({fire.GetFirePhase()})");

        if (hadEmbers && fire.IsActive)
            GameDisplay.AddNarrative("The embers ignite the fuel! The fire springs back to life.");

        ctx.Update(1);
    }

    private void StartFire()
    {
        var inv = ctx.Inventory;
        var existingFire = ctx.CurrentLocation.GetFeature<HeatSourceFeature>();
        bool relightingFire = existingFire != null;

        if (relightingFire)
            GameDisplay.AddNarrative("You prepare to relight the fire.");
        else
            GameDisplay.AddNarrative("You prepare to start a fire.");

        // Get fire-making tools from aggregate inventory
        var fireTools = inv.Tools.Where(t => t.Type == ToolType.FireStriker).ToList();

        if (fireTools.Count == 0)
        {
            GameDisplay.AddWarning("You don't have any fire-making tools!");
            return;
        }

        bool hasTinder = inv.Tinder.Count > 0;
        bool hasKindling = inv.Sticks.Count > 0;

        if (!hasTinder)
        {
            GameDisplay.AddWarning("You don't have any tinder to start a fire!");
            return;
        }

        if (!hasKindling)
        {
            GameDisplay.AddWarning("You don't have any kindling to start a fire!");
            return;
        }

        GameDisplay.AddNarrative($"Materials: {inv.Tinder.Count} tinder, {inv.Sticks.Count} kindling");

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

        GameDisplay.Render(ctx, addSeparator: false);
        string choice = Input.Select("Choose fire-making tool:", toolChoices);

        if (choice == "Cancel")
        {
            GameDisplay.AddNarrative("You decide not to start a fire right now.");
            return;
        }

        var (selectedTool, finalChance) = toolMap[choice];

        GameDisplay.AddNarrative($"You work with the {selectedTool.Name}...");
        ctx.Update(15);

        bool success = Utils.DetermineSuccess(finalChance);

        // Always consume tinder on attempt
        double tinderUsed = inv.TakeTinder();

        if (success)
        {
            // Also consume a stick for kindling
            double kindlingUsed = inv.TakeSmallestStick();

            var playerSkill = ctx.player.Skills.GetSkill("Firecraft");

            if (relightingFire)
            {
                GameDisplay.AddSuccess($"Success! You relight the fire! ({finalChance:P0} chance)");
                existingFire!.AddFuel(tinderUsed, FuelType.Tinder);
                existingFire.AddFuel(kindlingUsed, FuelType.Kindling);
            }
            else
            {
                GameDisplay.AddSuccess($"Success! You start a fire! ({finalChance:P0} chance)");
                var newFire = new HeatSourceFeature();
                newFire.AddFuel(tinderUsed, FuelType.Tinder);
                newFire.AddFuel(kindlingUsed, FuelType.Kindling);
                ctx.CurrentLocation.Features.Add(newFire);
            }

            playerSkill.GainExperience(3);
        }
        else
        {
            GameDisplay.AddWarning($"You failed to start the fire. The tinder was wasted. ({finalChance:P0} chance)");
            ctx.player.Skills.GetSkill("Firecraft").GainExperience(1);
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
    // SLEEP
    // ═══════════════════════════════════════════════════════════════════════════

    private void Sleep()
    {
        GameDisplay.AddNarrative("How many hours would you like to sleep? (1-24)");
        int hours = Input.ReadInt();

        if (hours < 1 || hours > 24)
        {
            GameDisplay.AddWarning("You can only sleep for 1-24 hours at a time.");
            return;
        }

        int minutes = hours * 60;
        ctx.player.Body.Rest(minutes);
        ctx.Update(minutes);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // INVENTORY
    // ═══════════════════════════════════════════════════════════════════════════

    private bool HasItems()
    {
        var inv = ctx.Inventory;
        return inv.HasFuel || inv.HasFood || inv.HasWater || inv.Tools.Count > 0;
    }

    private void RunInventoryMenu()
    {
        // Inventory is shown in the INVENTORY panel - just refresh and wait
        GameDisplay.Render(ctx, addSeparator: false);
        Input.WaitForKey("Press any key to return...");
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // CRAFTING
    // ═══════════════════════════════════════════════════════════════════════════

    // private bool CanCraft() => ctx.CraftingManager.GetAvailableRecipes().Count > 0;

    // private void RunCraftingMenu()
    // {
    //     var availableRecipes = ctx.CraftingManager.GetAvailableRecipes();

    //     if (availableRecipes.Count == 0)
    //     {
    //         GameDisplay.AddNarrative("You don't know how to craft anything here, or you lack the required materials.");
    //         Input.WaitForKey();
    //         return;
    //     }

    //     GameDisplay.AddNarrative("What would you like to craft?");

    //     var itemRecipes = availableRecipes.Where(r => r.ResultType == CraftingResultType.Item).ToList();
    //     var featureRecipes = availableRecipes.Where(r => r.ResultType == CraftingResultType.LocationFeature).ToList();
    //     var shelterRecipes = availableRecipes.Where(r => r.ResultType == CraftingResultType.Shelter).ToList();

    //     var choice = new Choice<Action>();

    //     if (itemRecipes.Count != 0)
    //     {
    //         GameDisplay.AddNarrative("\n--- Items ---");
    //         foreach (var recipe in itemRecipes)
    //             choice.AddOption($"Craft {recipe.Name}", () => CraftItem(recipe));
    //     }

    //     if (featureRecipes.Count != 0)
    //     {
    //         GameDisplay.AddNarrative("\n--- Build Features ---");
    //         foreach (var recipe in featureRecipes)
    //             choice.AddOption($"Craft {recipe.Name}", () => CraftItem(recipe));
    //     }

    //     if (shelterRecipes.Count != 0)
    //     {
    //         GameDisplay.AddNarrative("\n--- Build Shelters ---");
    //         foreach (var recipe in shelterRecipes)
    //             choice.AddOption($"Craft {recipe.Name}", () => CraftItem(recipe));
    //     }

    //     choice.AddOption("View All Known Recipes", ShowAllRecipes);
    //     choice.AddOption("Show My Materials", ShowAvailableMaterials);
    //     choice.AddOption("Stop crafting", () => { });

    //     choice.GetPlayerChoice().Invoke();
    // }

    // private void CraftItem(CraftingRecipe recipe)
    // {
    //     GameDisplay.AddNarrative($"\nCrafting: {recipe.Name}");
    //     GameDisplay.AddNarrative($"Description: {recipe.Description}");
    //     GameDisplay.AddNarrative($"Time required: {recipe.CraftingTimeMinutes} minutes");
    //     GameDisplay.AddNarrative($"Required skill: {recipe.RequiredSkill} (Level {recipe.RequiredSkillLevel})");
    //     GameDisplay.AddNarrative($"Result type: {recipe.ResultType}");

    //     if (recipe.RequiresFire)
    //         GameDisplay.AddNarrative("Requires: Active fire");

    //     GameDisplay.AddNarrative("\nMaterial properties needed:");
    //     foreach (var req in recipe.RequiredProperties)
    //     {
    //         string consumed = req.IsConsumed ? "(consumed)" : "(used)";
    //         GameDisplay.AddNarrative($"- {req.Property}: {req.MinQuantity:F1}+ KG {consumed}");
    //     }

    //     GameDisplay.AddNarrative("\nYour available materials:");
    //     ShowPlayerMaterials(recipe.RequiredProperties);

    //     var preview = recipe.PreviewConsumption(ctx.player);
    //     if (preview.Count > 0)
    //     {
    //         GameDisplay.AddNarrative("\nThis will consume:");
    //         foreach (var (itemName, amount) in preview)
    //         {
    //             GameDisplay.AddNarrative($"  - {itemName} ({amount:F2}kg)");
    //         }
    //     }

    //     GameDisplay.AddNarrative("\nDo you want to attempt this craft?");

    //     if (Input.ReadYesNo())
    //     {
    //         ctx.CraftingManager.Craft(recipe);
    //     }

    //     RunCraftingMenu();
    // }

    // private void ShowAllRecipes()
    // {
    //     GameDisplay.AddNarrative("\n=== [ Known Recipes ] ===");

    //     var craftingManager = new CraftingSystem(ctx);
    //     var allRecipes = craftingManager.Recipes.Values
    //         .GroupBy(r => r.ResultType)
    //         .ToList();

    //     foreach (var group in allRecipes)
    //     {
    //         GameDisplay.AddNarrative($"\n--- [ {group.Key} Recipes ] ---");
    //         foreach (var recipe in group)
    //         {
    //             PrintRecipeTable(recipe);
    //             GameDisplay.AddNarrative("");
    //         }
    //     }

    //     Input.WaitForKey();
    //     RunCraftingMenu();
    // }

    // private void PrintRecipeTable(CraftingRecipe recipe)
    // {
    //     bool canCraft = recipe.CanCraft(ctx.player, ctx.Camp);
    //     string status = canCraft ? "[✓]" : "[✗]";

    //     string header = $">>> {recipe.Name.ToUpper()} {status} <<<";
    //     int tableWidth = 64;

    //     GameDisplay.AddNarrative($"┌{new string('─', tableWidth)}┐");
    //     GameDisplay.AddNarrative($"│{header.PadLeft((tableWidth + header.Length) / 2).PadRight(tableWidth)}│");
    //     GameDisplay.AddNarrative($"├{new string('─', tableWidth)}┤");

    //     string fireReq = recipe.RequiresFire ? " • Fire Required" : "";
    //     string infoRow = $"{recipe.CraftingTimeMinutes} min • {recipe.RequiredSkill} level {recipe.RequiredSkillLevel}{fireReq}";
    //     GameDisplay.AddNarrative($"│ {infoRow.PadRight(tableWidth - 2)} │");

    //     string description = recipe.Description;
    //     if (description.Length > tableWidth - 2)
    //         description = string.Concat(description.AsSpan(0, tableWidth - 5), "...");
    //     GameDisplay.AddNarrative($"│ {description.PadRight(tableWidth - 2)} │");

    //     GameDisplay.AddNarrative($"├───────────────────────────────────┬────────┬─────────┬─────────┤");
    //     GameDisplay.AddNarrative($"│ Material                          │ Qty    │ Quality │ Consumed│");
    //     GameDisplay.AddNarrative($"├───────────────────────────────────┼────────┼─────────┼─────────┤");

    //     foreach (var req in recipe.RequiredProperties)
    //     {
    //         string propertyName = req.Property.ToString();
    //         string material = propertyName.Length > 31 ? propertyName[..28] + "..." : propertyName;
    //         string quantity = $"{req.MinQuantity:F1}x";
    //         string consumed = req.IsConsumed ? "✓" : "✗";

    //         GameDisplay.AddNarrative($"│ {material,-33} │ {quantity,6} │ {0,7} │ {consumed,7} │");
    //     }

    //     GameDisplay.AddNarrative($"└───────────────────────────────────┴────────┴─────────┴─────────┘");
    // }

    // private void ShowAvailableMaterials()
    // {
    //     GameDisplay.AddNarrative("\n=== Your Material Properties ===");

    //     var propertyTotals = new Dictionary<ItemProperty, (double amount, int items)>();

    //     foreach (var stack in ctx.player.inventoryManager.Items)
    //     {
    //         var item = stack.FirstItem;
    //         foreach (var property in item.CraftingProperties)
    //         {
    //             if (!propertyTotals.ContainsKey(property))
    //                 propertyTotals[property] = (0, 0);

    //             var current = propertyTotals[property];
    //             propertyTotals[property] = (
    //                 current.amount + (item.Weight * stack.Count),
    //                 current.items + stack.Count
    //             );
    //         }
    //     }

    //     foreach (var kvp in propertyTotals.OrderBy(x => x.Key))
    //     {
    //         var (amount, items) = kvp.Value;
    //         GameDisplay.AddNarrative($"{kvp.Key}: {amount:F1} total");
    //     }

    //     if (!propertyTotals.Any())
    //         GameDisplay.AddNarrative("You don't have any materials with useful properties.");

    //     Input.WaitForKey();
    //     RunCraftingMenu();
    // }

    // private void ShowPlayerMaterials(List<CraftingPropertyRequirement> requirements)
    // {
    //     foreach (var req in requirements)
    //     {
    //         double totalAmount = 0;
    //         int itemCount = 0;

    //         foreach (var stack in ctx.player.inventoryManager.Items)
    //         {
    //             var property = stack.FirstItem.GetProperty(req.Property);
    //             if (property != null)
    //             {
    //                 totalAmount += stack.TotalWeight;
    //                 itemCount += stack.Count;
    //             }
    //         }

    //         bool sufficient = totalAmount >= req.MinQuantity;
    //         string status = sufficient ? "✓" : "✗";

    //         GameDisplay.AddNarrative($"  {status} {req.Property}: {totalAmount:F1}/{req.MinQuantity:F1}");
    //     }
    // }

    // ═══════════════════════════════════════════════════════════════════════════
    // COMBAT
    // ═══════════════════════════════════════════════════════════════════════════

    private void StartCombat(Npc enemy)
    {
        if (!enemy.IsAlive || !enemy.IsFound) return;

        GameDisplay.AddNarrative("!");
        Thread.Sleep(500);
        GameDisplay.AddNarrative(CombatNarrator.DescribeCombatStart(ctx.player, enemy));

        ctx.player.IsEngaged = true;
        enemy.IsEngaged = true;

        bool enemyFirstStrike = enemy.Speed > ctx.player.Speed;

        if (enemyFirstStrike)
        {
            GameDisplay.AddNarrative($"The {enemy.Name} moves with surprising speed!");
            Thread.Sleep(500);
            EnemyCombatTurn(enemy);
        }
        else
        {
            GameDisplay.AddNarrative("You're quick to react, giving you the initiative!");
            Thread.Sleep(500);
            PlayerCombatTurn(enemy);
        }
    }

    private void PlayerCombatTurn(Npc enemy)
    {
        if (!ctx.player.IsAlive || !enemy.IsAlive || !ctx.player.IsEngaged)
        {
            EndCombat(enemy);
            return;
        }

        GameDisplay.AddNarrative("─────────────────────────────────────");
        DisplayCombatStatus(enemy);

        var choice = new Choice<Action>();
        choice.AddOption($"Attack {enemy.Name}", () => AttackEnemy(enemy));

        if (ctx.player.Skills.Fighting.Level > 1)
            choice.AddOption($"Targeted Attack {enemy.Name}", () => TargetedAttackEnemy(enemy));

        if (ctx.player.Speed > 0.25)
            choice.AddOption("Flee", () => AttemptFlee(enemy));

        choice.GetPlayerChoice().Invoke();
    }

    private void EnemyCombatTurn(Npc enemy)
    {
        if (!ctx.player.IsAlive || !enemy.IsAlive || !enemy.IsEngaged)
        {
            EndCombat(enemy);
            return;
        }

        Thread.Sleep(500);
        enemy.Attack(ctx.player);

        if (!ctx.player.IsAlive || !enemy.IsAlive)
            EndCombat(enemy);
        else
            PlayerCombatTurn(enemy);
    }

    private void AttackEnemy(Npc enemy)
    {
        var weapon = ctx.Inventory.Weapon?.ToWeapon();
        ctx.player.Attack(enemy, weapon);

        if (!enemy.IsAlive)
            EndCombat(enemy);
        else
            EnemyCombatTurn(enemy);
    }

    private void TargetedAttackEnemy(Npc enemy)
    {
        int fightingSkill = ctx.player.Skills.Fighting.Level;
        var targetPart = SelectTargetPart(enemy, fightingSkill);

        if (targetPart != null)
        {
            var weapon = ctx.Inventory.Weapon?.ToWeapon();
            ctx.player.Attack(enemy, weapon, targetPart.Name);

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
            GameDisplay.AddWarning("You don't have enough skill to target an attack");
            return null;
        }

        GameDisplay.AddNarrative($"Where do you want to target your attack on the {enemy.Name}?");

        List<BodyRegion> allParts = [];
        foreach (var part in enemy.Body.Parts)
        {
            if (depth > 0)
                allParts.Add(part);
        }

        return Input.SelectOrCancel("Select target:", allParts);
    }

    private void AttemptFlee(Npc enemy)
    {
        if (CombatUtils.SpeedCheck(ctx.player, enemy))
        {
            GameDisplay.AddNarrative("You got away!");
            enemy.IsEngaged = false;
            ctx.player.IsEngaged = false;
            ctx.player.Skills.Reflexes.GainExperience(2);
        }
        else
        {
            GameDisplay.AddNarrative($"You weren't fast enough to get away from {enemy.Name}!");
            ctx.player.Skills.Reflexes.GainExperience(1);
            EnemyCombatTurn(enemy);
        }
    }

    private void EndCombat(Npc enemy)
    {
        ctx.player.IsEngaged = false;
        enemy.IsEngaged = false;

        if (!ctx.player.IsAlive)
        {
            GameDisplay.AddDanger("Your vision fades to black as you collapse... You have died!");
            Environment.Exit(0);
        }
        else if (!enemy.IsAlive)
        {
            string[] victoryMessages = [
                $"The {enemy.Name} collapses, defeated!",
                $"You stand victorious over the fallen {enemy.Name}!",
                $"With a final blow, you bring down the {enemy.Name}!"
            ];
            GameDisplay.AddNarrative(victoryMessages[Utils.RandInt(0, victoryMessages.Length - 1)]);

            int xpGain = CalculateExperienceGain(enemy);
            GameDisplay.AddNarrative($"You've gained {xpGain} fighting experience!");
            ctx.player.Skills.Fighting.GainExperience(xpGain);
        }
    }

    private static int CalculateExperienceGain(Npc enemy)
    {
        int baseXP = 5;
        double sizeMultiplier = Math.Clamp(enemy.Body.WeightKG / 50, 0.5, 3.0);
        double weaponMultiplier = Math.Clamp(enemy.ActiveWeapon.Damage / 8, 0.5, 2.0);
        return (int)(baseXP * sizeMultiplier * weaponMultiplier);
    }

    private void DisplayCombatStatus(Npc enemy)
    {
        double playerVitality = ctx.player.Vitality;
        string playerStatus = $"You: {Math.Round(playerVitality * 100, 0)}% Vitality";
        AddHealthMessage(playerStatus, playerVitality);

        double enemyVitality = enemy.Vitality;
        string enemyStatus = $"{enemy.Name}: {Math.Round(enemyVitality * 100, 0)}% Vitality";
        AddHealthMessage(enemyStatus, enemyVitality);
    }

    private static void AddHealthMessage(string message, double healthPercentage)
    {
        if (healthPercentage < 0.2)
            GameDisplay.AddDanger(message);
        else if (healthPercentage < 0.5)
            GameDisplay.AddWarning(message);
        else
            GameDisplay.AddSuccess(message);
    }
}