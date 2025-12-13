using text_survival.Actors.Player;
using text_survival.Actors.NPCs;
using text_survival.Actors;
using text_survival.Bodies;
using text_survival.Combat;
using text_survival.Crafting;
using text_survival.Environments.Features;
using text_survival.IO;
using text_survival.Items;
using text_survival.Core;
using text_survival.Actions.Expeditions;

namespace text_survival.Actions;

public class Choice<T>(string? propmt = null)
{
    public string? Prompt = propmt;
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
        if (Prompt is not null)
        {
            Output.WriteLine(Prompt);
        }
        string choice = Input.GetSelectionFromList(options.Keys.ToList())!;
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
            ShowStatus();
            RunCampMenu();
            World.Update(1);
        }
    }

    private void ShowStatus()
    {
        BodyDescriber.DescribeSurvivalStats(ctx.player.Body, ctx.player.GetSurvivalContext());
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // MAIN MENU
    // ═══════════════════════════════════════════════════════════════════════════

    private void RunCampMenu()
    {
        ExpeditionRunner expeditionRunner = new(ctx);

        var choice = new Choice<Action>();
        choice.AddOption("Look around", LookAround);

        if (HasActiveFire())
            choice.AddOption("Tend fire", TendFire);

        if (CanStartFire())
            choice.AddOption("Start fire", StartFire);

        if (CanForage())
            choice.AddOption("Forage", expeditionRunner.RunForageExpedition);

        if (CanHunt())
            choice.AddOption("Hunt", RunHuntingMenu);

        if (CanHarvest())
            choice.AddOption("Harvest resources", expeditionRunner.RunHarvestExpedition);

        if (CanCraft())
            choice.AddOption("Craft", RunCraftingMenu);

        if (HasItems())
            choice.AddOption("Inventory", RunInventoryMenu);

        choice.AddOption("Check stats", CheckStats);

        if (ctx.player.Body.IsTired)
            choice.AddOption("Sleep", Sleep);

        choice.GetPlayerChoice().Invoke();
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // LOOK AROUND
    // ═══════════════════════════════════════════════════════════════════════════
    private void LookAround()
    {
        var location = ctx.CurrentLocation;
        Output.WriteLine();

        // Location and conditions
        Output.WriteLine($"You're at {location.Name}. It's {World.GetTimeOfDay().ToString().ToLower()}, {location.GetTemperature():F0}°F.");
        Output.WriteLine();

        // Fire status - most important
        var fire = location.GetFeature<HeatSourceFeature>();
        if (fire != null)
            Output.WriteLine(DescribeFire(fire));

        // Shelter
        var shelter = location.GetFeature<ShelterFeature>();
        if (shelter != null)
            Output.WriteLine($"You have a {shelter.Name.ToLower()} here.");

        // Harvestables
        foreach (var h in location.Features.OfType<HarvestableFeature>().Where(f => f.IsDiscovered))
            Output.WriteLine($"There's a {h.DisplayName.ToLower()} nearby.");

        // Items on ground
        var foundItems = location.Items.Where(i => i.IsFound).ToList();
        if (foundItems.Count == 1)
            Output.WriteLine($"You notice a {foundItems[0].Name.ToLower()} on the ground.");
        else if (foundItems.Count > 1)
            Output.WriteLine($"On the ground you see: {string.Join(", ", foundItems.Select(i => i.Name.ToLower()))}.");

        // Containers
        foreach (var c in location.Containers.Where(c => c.IsFound && !c.IsEmpty))
            Output.WriteLine($"There's a {c.Name.ToLower()} here.");

        // NPCs
        foreach (var npc in location.Npcs.Where(n => n.IsFound))
        {
            if (npc.IsAlive)
                Output.WriteLineColored(ConsoleColor.Red, $"A {npc.Name.ToLower()} is here.");
            else
                Output.WriteLine($"The body of a {npc.Name.ToLower()} lies here.");
        }

        // Exits
        var nearby = location.GetNearbyLocations();
        if (nearby.Count > 0)
        {
            nearby.ForEach(n => n.IsFound = true);
            Output.WriteLine();
            Output.WriteLine($"From here you could reach {FormatList(nearby.Select(n => n.Name))}.");
        }

        Output.WriteLine();

        // Interactions
        var choice = new Choice<Action>();

        foreach (var npc in location.Npcs.Where(n => n.IsFound))
        {
            if (npc.IsAlive)
                choice.AddOption($"Fight the {npc.Name.ToLower()}", () => StartCombat(npc));
            else if (!npc.Loot.IsEmpty)
                choice.AddOption($"Search the {npc.Name.ToLower()}", () => OpenContainer(npc.Loot));
        }

        foreach (var item in foundItems)
            choice.AddOption($"Pick up the {item.Name.ToLower()}", () => PickUpItem(item));

        foreach (var container in location.Containers.Where(c => c.IsFound && !c.IsEmpty))
            choice.AddOption($"Open the {container.Name.ToLower()}", () => OpenContainer(container));

        choice.AddOption("Back", () => { });

        choice.GetPlayerChoice().Invoke();
    }

    private static string DescribeFire(HeatSourceFeature fire)
    {
        if (fire.IsActive && fire.HoursRemaining > 0)
        {
            int minutes = (int)(fire.HoursRemaining * 60);
            if (fire.HoursRemaining < 0.25)
                return $"Your fire is dying down, maybe {minutes} minutes left.";
            if (fire.HoursRemaining < 0.5)
                return $"The fire is getting low. About {minutes} minutes of fuel left.";
            return $"Your fire is burning steadily. You have about {minutes} minutes of fuel.";
        }
        if (fire.HasEmbers)
        {
            int minutes = (int)(fire.EmberTimeRemaining * 60);
            return $"The fire has burned down to embers. They'll last maybe {minutes} more minutes.";
        }
        return "Your fire pit is cold.";
    }

    private static string FormatList(IEnumerable<string> items)
    {
        var list = items.ToList();
        return list.Count switch
        {
            0 => "",
            1 => list[0],
            2 => $"{list[0]} or {list[1]}",
            _ => $"{string.Join(", ", list.Take(list.Count - 1))}, or {list.Last()}"
        };
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // FIRE MANAGEMENT
    // ═══════════════════════════════════════════════════════════════════════════

    private bool HasActiveFire()
    {
        var fire = ctx.CurrentLocation.GetFeature<HeatSourceFeature>();
        if (fire == null) return false;

        var flammableItems = ctx.player.inventoryManager.Items
            .Where(stack => stack.FirstItem.CraftingProperties.Contains(ItemProperty.Flammable))
            .ToList();

        return flammableItems.Count > 0;
    }

    private bool CanStartFire()
    {
        var fire = ctx.CurrentLocation.GetFeature<HeatSourceFeature>();

        bool noFire = fire == null;
        bool fullyColdFire = fire != null && fire.HoursRemaining == 0 && !fire.HasEmbers;

        if (!noFire && !fullyColdFire) return false;

        var inventory = ctx.player.inventoryManager;

        bool hasTool = inventory.Items.Any(s => IsFireMakingTool(s.FirstItem));

        double tinder = inventory.Items
            .Where(s => s.FirstItem.HasProperty(ItemProperty.Tinder, 0))
            .Sum(s => s.TotalWeight);
        double kindling = inventory.Items
            .Where(s => s.FirstItem.HasProperty(ItemProperty.Wood, 0))
            .Sum(s => s.TotalWeight);

        return hasTool && tinder >= 0.05 && kindling >= 0.3;
    }

    private static bool IsFireMakingTool(Item item)
    {
        return item.Name is "Hand Drill" or "Bow Drill" or "Flint and Steel";
    }

    private void TendFire()
    {
        var fire = ctx.CurrentLocation.GetFeature<HeatSourceFeature>()!;

        // Display fire status
        string firePhase = fire.GetFirePhase();
        double fireTemp = fire.GetCurrentFireTemperature();
        double heatOutput = fire.GetEffectiveHeatOutput();
        double fuelMinutes = fire.HoursRemaining * 60;

        ConsoleColor phaseColor = firePhase switch
        {
            "Roaring" => ConsoleColor.Red,
            "Building" or "Steady" => ConsoleColor.Yellow,
            "Igniting" or "Dying" => ConsoleColor.DarkYellow,
            "Embers" => ConsoleColor.DarkYellow,
            _ => ConsoleColor.DarkGray
        };

        Output.Write("\n");
        Output.WriteLineColored(phaseColor, $"🔥 {fire.Name}: {firePhase} ({fireTemp:F0}°F)");

        if (fire.IsActive || fire.HasEmbers)
        {
            Output.WriteLine($"   Heat contribution: +{heatOutput:F1}°F to location");
            Output.WriteLine($"   Fuel remaining: {fire.FuelMassKg:F2} kg (~{fuelMinutes:F0} min)");

            if (fire.HasEmbers)
            {
                double emberMinutes = fire.EmberTimeRemaining * 60;
                Output.WriteLine($"   Embers will last: {emberMinutes:F0} minutes");
            }
        }
        else
        {
            Output.WriteLine($"   Status: Cold fire (no fuel)");
        }

        Output.WriteLine($"   Capacity: {fire.FuelMassKg:F2}/{fire.MaxFuelCapacityKg:F1} kg");
        Output.WriteLine();

        // Get all fuel items
        var fuelStacks = ctx.player.inventoryManager.Items
            .Where(stack => stack.FirstItem.IsFuel())
            .ToList();

        if (fuelStacks.Count == 0)
        {
            Output.WriteLine("You have no fuel to add.");
            Input.WaitForKey();
            return;
        }

        Output.WriteLine("Available fuel:");
        var fuelOptions = new List<(ItemStack stack, bool canAdd, bool isSharp)>();

        foreach (var stack in fuelStacks)
        {
            var firstItem = stack.FirstItem;
            bool canAdd = fire.CanAddFuel(firstItem);
            bool isSharp = firstItem.CraftingProperties.Contains(ItemProperty.Sharp);

            var fuelType = firstItem.GetFuelType();
            string fuelInfo = fuelType.HasValue ? $"[{fuelType.Value}]" : "";

            double massKg = firstItem.FuelMassKg;
            double burnHours = massKg;
            double minTempRequired = 0;

            if (fuelType.HasValue)
            {
                var props = FuelDatabase.Get(fuelType.Value);
                burnHours = massKg / props.BurnRateKgPerHour;
                minTempRequired = props.MinFireTemperature;
            }

            double burnMinutes = burnHours * 60;

            string statusIcon = canAdd ? "✓" : "✗";
            string warning = isSharp ? " ⚠ SHARP TOOL" : "";
            string tempWarning = !canAdd && minTempRequired > 0 ? $" (needs {minTempRequired}°F fire)" : "";

            Output.WriteLine($"  {statusIcon} {fuelOptions.Count + 1}. {stack.DisplayName} {fuelInfo} - {massKg:F2}kg (~{burnMinutes:F0} min){warning}{tempWarning}");

            fuelOptions.Add((stack, canAdd, isSharp));
        }

        Output.WriteLine($"  {fuelOptions.Count + 1}. Cancel");
        Output.WriteLine();
        Output.WriteLine("Select an item to add as fuel:");

        int choice = Input.ReadInt();

        if (choice < 1 || choice > fuelOptions.Count + 1)
        {
            Output.WriteWarning("Invalid selection.");
            return;
        }

        if (choice == fuelOptions.Count + 1)
        {
            Output.WriteLine("You decide not to add fuel to the fire.");
            return;
        }

        var selected = fuelOptions[choice - 1];
        var selectedStack = selected.stack;
        bool canAddFuel = selected.canAdd;
        bool selectedIsSharp = selected.isSharp;

        if (!canAddFuel)
        {
            Output.WriteWarning("The fire isn't hot enough to burn that fuel type!");
            Output.WriteLine("Try adding tinder or kindling first to build up the fire's temperature.");
            return;
        }

        if (selectedIsSharp)
        {
            Output.WriteWarning($"Warning: {selectedStack.FirstItem.Name} is a sharp tool!");
            Output.WriteLine("Are you sure you want to burn it? (yes/no)");
            if (!Input.ReadYesNo())
            {
                Output.WriteLine("You decide not to burn it.");
                return;
            }
        }

        double maxCanAddKg = fire.MaxFuelCapacityKg - fire.FuelMassKg;
        double fuelMassKg = selectedStack.FirstItem.FuelMassKg;
        bool overflow = fuelMassKg > maxCanAddKg;

        var itemToRemove = selectedStack.Pop();
        ctx.player.inventoryManager.RemoveFromInventory(itemToRemove);

        bool hadEmbers = fire.HasEmbers;

        fire.AddFuel(itemToRemove);

        Output.WriteLine($"\nYou add the {itemToRemove.Name} to the {fire.Name}.");
        if (overflow)
        {
            double wastedKg = fuelMassKg - maxCanAddKg;
            Output.WriteWarning($"The fire was already near capacity. {wastedKg:F2} kg of fuel was wasted.");
        }

        double newFuelMinutes = fire.HoursRemaining * 60;
        Output.WriteLine($"The fire now has {fire.FuelMassKg:F2} kg of fuel ({newFuelMinutes:F0} minutes).");
        Output.WriteLine($"Fire temperature: {fire.GetCurrentFireTemperature():F0}°F ({fire.GetFirePhase()})");

        if (hadEmbers && fire.IsActive)
            Output.WriteLine("The embers ignite the new fuel! The fire springs back to life.");
        else if (!fire.IsActive)
            Output.WriteWarning("\nThe fire is cold. You need to use 'Start Fire' to light it with proper fire-making materials.");

        World.Update(1);
    }

    private void StartFire()
    {
        var inventory = ctx.player.inventoryManager;
        var existingFire = ctx.CurrentLocation.GetFeature<HeatSourceFeature>();
        bool relightingFire = existingFire != null;

        if (relightingFire)
            Output.WriteLine($"You prepare to relight the fire.");
        else
            Output.WriteLine("You prepare to start a fire.");

        var availableTools = inventory.Items
            .Where(s => IsFireMakingTool(s.FirstItem) && s.FirstItem.NumUses > 0)
            .Select(s => s.FirstItem)
            .ToList();

        if (!availableTools.Any())
        {
            Output.WriteWarning("You don't have any working fire-making tools!");
            return;
        }

        bool hasTinder = inventory.Items.Any(s => s.FirstItem.HasProperty(ItemProperty.Tinder));
        double tinderBonus = hasTinder ? 0.15 : 0.0;

        Output.WriteLine("\nChoose your fire-making tool:");
        if (hasTinder)
            Output.WriteLine("  [Tinder available: +15% success bonus]");

        int optionNum = 1;
        foreach (var tool in availableTools)
        {
            var toolParams = GetToolSkillParameters(tool);
            double successChance = toolParams.baseChance;
            var skill = ctx.player.Skills.GetSkill("Firecraft");
            if (toolParams.skillDC > 0)
            {
                double skillModifier = (skill.Level - toolParams.skillDC) * 0.1;
                successChance += skillModifier;
            }
            else
            {
                double skillModifier = skill.Level * 0.1;
                successChance += skillModifier;
            }

            successChance += tinderBonus;
            successChance = Math.Clamp(successChance, 0.05, 0.95);

            Output.WriteLine($"  {optionNum}. {tool} - {successChance:P0} success chance");
            optionNum++;
        }
        Output.WriteLine($"  {optionNum}. Cancel");

        Output.WriteLine("\nSelect a tool:");
        int choice = Input.ReadInt();

        if (choice < 1 || choice > availableTools.Count + 1)
        {
            Output.WriteWarning("Invalid selection.");
            return;
        }

        if (choice == availableTools.Count + 1)
        {
            Output.WriteLine("You decide not to start a fire right now.");
            return;
        }

        var selectedTool = availableTools[choice - 1];

        var (baseChance, skillDC) = GetToolSkillParameters(selectedTool);
        var playerSkill = ctx.player.Skills.GetSkill("Firecraft");
        double finalSuccessChance = SkillCheckCalculator.CalculateSuccessChance(
            baseChance,
            playerSkill.Level,
            skillDC);

        finalSuccessChance += tinderBonus;
        finalSuccessChance = Math.Clamp(finalSuccessChance, 0.05, 0.95);

        Output.WriteLine($"\nYou work with the {selectedTool.Name}...");
        World.Update(15);

        bool success = Utils.DetermineSuccess(finalSuccessChance);

        if (success)
        {
            ConsumeMaterial(ctx.player, ItemProperty.Tinder, 0.05);
            ConsumeMaterial(ctx.player, ItemProperty.Wood, 0.3);

            bool toolBroke = selectedTool.UseOnce();
            if (toolBroke)
                inventory.RemoveFromInventory(selectedTool);

            if (relightingFire)
            {
                Output.WriteSuccess($"\nSuccess! You relight the fire! ({finalSuccessChance:P0} chance)");
                var tinderFuel = ItemFactory.MakeTinderBundle();
                var kindlingFuel = ItemFactory.MakeStick();
                existingFire!.AddFuel(tinderFuel, 0.03);
                existingFire.AddFuel(kindlingFuel, 0.3);
            }
            else
            {
                Output.WriteSuccess($"\nSuccess! You start a fire! ({finalSuccessChance:P0} chance)");
                var newFire = new HeatSourceFeature(ctx.CurrentLocation);
                var tinderFuel = ItemFactory.MakeTinderBundle();
                var kindlingFuel = ItemFactory.MakeStick();
                newFire.AddFuel(tinderFuel, 0.03);
                newFire.AddFuel(kindlingFuel, 0.3);
                ctx.CurrentLocation.Features.Add(newFire);
            }

            playerSkill.GainExperience(3);
        }
        else
        {
            ConsumeMaterial(ctx.player, ItemProperty.Tinder, 0.05);

            bool toolBroke = selectedTool.UseOnce();
            if (toolBroke)
                inventory.RemoveFromInventory(selectedTool);

            Output.WriteWarning($"\nYou failed to start the fire. The tinder was wasted. ({finalSuccessChance:P0} chance)");
            playerSkill.GainExperience(1);
        }
    }

    private static void ConsumeMaterial(Player player, ItemProperty property, double amount)
    {
        double remaining = amount;
        var eligibleStacks = player.inventoryManager.Items
            .Where(stack => stack.FirstItem.HasProperty(property, 0))
            .ToList();

        foreach (var stack in eligibleStacks)
        {
            while (stack.Count > 0 && remaining > 0)
            {
                var item = stack.FirstItem;
                if (item.Weight <= remaining)
                {
                    remaining -= item.Weight;
                    var consumed = stack.Pop();
                    player.inventoryManager.RemoveFromInventory(consumed);
                }
                else
                {
                    item.Weight -= remaining;
                    remaining = 0;
                }
            }
            if (remaining <= 0) break;
        }
    }

    private static (double baseChance, int skillDC) GetToolSkillParameters(Item tool)
    {
        return tool.Name switch
        {
            "Hand Drill" => (0.30, 0),
            "Bow Drill" => (0.50, 1),
            "Flint and Steel" => (0.90, 0),
            _ => (0.30, 0)
        };
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // SLEEP
    // ═══════════════════════════════════════════════════════════════════════════

    private void Sleep()
    {
        Output.WriteLine("How many hours would you like to sleep? (1-24)");
        int hours = Input.ReadInt();

        if (hours < 1 || hours > 24)
        {
            Output.WriteWarning("You can only sleep for 1-24 hours at a time.");
            return;
        }

        int minutes = hours * 60;
        ctx.player.Body.Rest(minutes);
        World.Update(minutes);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // INVENTORY
    // ═══════════════════════════════════════════════════════════════════════════

    private bool HasItems() => ctx.player.inventoryManager.Items.Any();

    private void RunInventoryMenu()
    {
        ctx.player.inventoryManager.Describe();

        var choice = new Choice<Action>();

        foreach (ItemStack stack in ctx.player.inventoryManager.Items)
        {
            choice.AddOption(stack.DisplayName, () => RunItemMenu(stack));
        }

        choice.AddOption("Close Inventory", () => { });

        Output.WriteLine("\nSelect an item:");
        choice.GetPlayerChoice().Invoke();
    }

    private void RunItemMenu(ItemStack stack)
    {
        Item item = stack.Peek();

        var choice = new Choice<Action>();
        choice.AddOption($"Use {item}", () => UseItem(item));
        choice.AddOption($"Inspect {item}", () => InspectItem(item));
        choice.AddOption($"Drop {item}", () => DropItem(item));
        choice.AddOption("Back to inventory", RunInventoryMenu);

        Output.WriteLine($"\nWhat would you like to do with the {item.Name}?");
        choice.GetPlayerChoice().Invoke();
    }

    private void UseItem(Item item)
    {
        ctx.player.UseItem(item);
        RunInventoryMenu();
    }

    private void InspectItem(Item item)
    {
        item.Describe();
        Input.WaitForKey();
        RunInventoryMenu();
    }

    private void DropItem(Item item)
    {
        Output.WriteLine($"You drop the {item}");
        ctx.player.DropItem(item);
        RunInventoryMenu();
    }

    private void PickUpItem(Item item)
    {
        if (!item.IsFound) return;

        Output.WriteLine($"You take the {item}");
        ctx.player.TakeItem(item);
    }

    private void OpenContainer(Container container)
    {
        Output.WriteLine($"You open the {container}");

        var itemStacks = ItemStack.CreateStacksFromItems(container.Items);

        while (!container.IsEmpty)
        {
            var choice = new Choice<Action>();
            string selectedLabel = "";

            itemStacks = ItemStack.CreateStacksFromItems(container.Items);
            foreach (var stack in itemStacks)
            {
                choice.AddOption($"Take {stack.DisplayName}", () => TakeStackFromContainer(container, stack));
            }

            if (itemStacks.Count > 1)
                choice.AddOption("Take all", () => TakeAllFromContainer(container));

            string closeLabel = $"Close {container.Name}";
            choice.AddOption(closeLabel, () => { selectedLabel = closeLabel; });

            Output.WriteLine("\nSelect an item:");
            var action = choice.GetPlayerChoice();
            action.Invoke();

            if (selectedLabel.StartsWith("Close") || container.IsEmpty)
                break;
        }
    }

    private void TakeStackFromContainer(Container container, ItemStack stack)
    {
        while (stack.Count > 0)
        {
            var item = stack.Pop();
            container.Remove(item);
            ctx.player.TakeItem(item);
        }
    }

    private void TakeAllFromContainer(Container container)
    {
        while (!container.IsEmpty)
        {
            var item = container.Items.First();
            container.Remove(item);
            ctx.player.TakeItem(item);
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // FORAGING / EXPEDITIONS
    // ═══════════════════════════════════════════════════════════════════════════

    private bool CanForage()
    {
        // Basic check - can always forage if you have energy
        return ctx.player.Vitality > 0.2;
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // HARVESTING
    // ═══════════════════════════════════════════════════════════════════════════

    private bool CanHarvest()
    {
        return ctx.CurrentLocation.GetNearbyLocations()
            .Any(l => l.Features
                .OfType<HarvestableFeature>()
                .Any(f => f.IsDiscovered && f.HasAvailableResources()));
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // CRAFTING
    // ═══════════════════════════════════════════════════════════════════════════

    private bool CanCraft() => ctx.CraftingManager.GetAvailableRecipes().Count > 0;

    private void RunCraftingMenu()
    {
        var availableRecipes = ctx.CraftingManager.GetAvailableRecipes();

        if (availableRecipes.Count == 0)
        {
            Output.WriteLine("You don't know how to craft anything here, or you lack the required materials.");
            Input.WaitForKey();
            return;
        }

        Output.WriteLine("What would you like to craft?");

        var itemRecipes = availableRecipes.Where(r => r.ResultType == CraftingResultType.Item).ToList();
        var featureRecipes = availableRecipes.Where(r => r.ResultType == CraftingResultType.LocationFeature).ToList();
        var shelterRecipes = availableRecipes.Where(r => r.ResultType == CraftingResultType.Shelter).ToList();

        var choice = new Choice<Action>();

        if (itemRecipes.Count != 0)
        {
            Output.WriteLine("\n--- Items ---");
            foreach (var recipe in itemRecipes)
                choice.AddOption($"Craft {recipe.Name}", () => CraftItem(recipe));
        }

        if (featureRecipes.Count != 0)
        {
            Output.WriteLine("\n--- Build Features ---");
            foreach (var recipe in featureRecipes)
                choice.AddOption($"Craft {recipe.Name}", () => CraftItem(recipe));
        }

        if (shelterRecipes.Count != 0)
        {
            Output.WriteLine("\n--- Build Shelters ---");
            foreach (var recipe in shelterRecipes)
                choice.AddOption($"Craft {recipe.Name}", () => CraftItem(recipe));
        }

        choice.AddOption("View All Known Recipes", ShowAllRecipes);
        choice.AddOption("Show My Materials", ShowAvailableMaterials);
        choice.AddOption("Stop crafting", () => { });

        choice.GetPlayerChoice().Invoke();
    }

    private void CraftItem(CraftingRecipe recipe)
    {
        Output.WriteLine($"\nCrafting: {recipe.Name}");
        Output.WriteLine($"Description: {recipe.Description}");
        Output.WriteLine($"Time required: {recipe.CraftingTimeMinutes} minutes");
        Output.WriteLine($"Required skill: {recipe.RequiredSkill} (Level {recipe.RequiredSkillLevel})");
        Output.WriteLine($"Result type: {recipe.ResultType}");

        if (recipe.RequiresFire)
            Output.WriteLine("Requires: Active fire");

        Output.WriteLine("\nMaterial properties needed:");
        foreach (var req in recipe.RequiredProperties)
        {
            string consumed = req.IsConsumed ? "(consumed)" : "(used)";
            Output.WriteLine($"- {req.Property}: {req.MinQuantity:F1}+ KG {consumed}");
        }

        Output.WriteLine("\nYour available materials:");
        ShowPlayerMaterials(recipe.RequiredProperties);

        var preview = recipe.PreviewConsumption(ctx.player);
        if (preview.Count > 0)
        {
            Output.WriteLine("\nThis will consume:");
            foreach (var (itemName, amount) in preview)
            {
                Output.WriteLine($"  - {itemName} ({amount:F2}kg)");
            }
        }

        Output.WriteLine("\nDo you want to attempt this craft?");

        if (Input.ReadYesNo())
        {
            ctx.CraftingManager.Craft(recipe);
        }

        RunCraftingMenu();
    }

    private void ShowAllRecipes()
    {
        Output.WriteLine("\n=== [ Known Recipes ] ===");

        var craftingManager = new CraftingSystem(ctx.player);
        var allRecipes = craftingManager.Recipes.Values
            .GroupBy(r => r.ResultType)
            .ToList();

        foreach (var group in allRecipes)
        {
            Output.WriteLine($"\n--- [ {group.Key} Recipes ] ---");
            foreach (var recipe in group)
            {
                PrintRecipeTable(recipe);
                Output.WriteLine();
            }
        }

        Input.WaitForKey();
        RunCraftingMenu();
    }

    private void PrintRecipeTable(CraftingRecipe recipe)
    {
        bool canCraft = recipe.CanCraft(ctx.player);
        string status = canCraft ? "[✓]" : "[✗]";

        string header = $">>> {recipe.Name.ToUpper()} {status} <<<";
        int tableWidth = 64;

        Output.WriteLine($"┌{new string('─', tableWidth)}┐");
        Output.WriteLine($"│{header.PadLeft((tableWidth + header.Length) / 2).PadRight(tableWidth)}│");
        Output.WriteLine($"├{new string('─', tableWidth)}┤");

        string fireReq = recipe.RequiresFire ? " • Fire Required" : "";
        string infoRow = $"{recipe.CraftingTimeMinutes} min • {recipe.RequiredSkill} level {recipe.RequiredSkillLevel}{fireReq}";
        Output.WriteLine($"│ {infoRow.PadRight(tableWidth - 2)} │");

        string description = recipe.Description;
        if (description.Length > tableWidth - 2)
            description = string.Concat(description.AsSpan(0, tableWidth - 5), "...");
        Output.WriteLine($"│ {description.PadRight(tableWidth - 2)} │");

        Output.WriteLine($"├───────────────────────────────────┬────────┬─────────┬─────────┤");
        Output.WriteLine($"│ Material                          │ Qty    │ Quality │ Consumed│");
        Output.WriteLine($"├───────────────────────────────────┼────────┼─────────┼─────────┤");

        foreach (var req in recipe.RequiredProperties)
        {
            string propertyName = req.Property.ToString();
            string material = propertyName.Length > 31 ? propertyName[..28] + "..." : propertyName;
            string quantity = $"{req.MinQuantity:F1}x";
            string consumed = req.IsConsumed ? "✓" : "✗";

            Output.WriteLine($"│ {material,-33} │ {quantity,6} │ {0,7} │ {consumed,7} │");
        }

        Output.WriteLine($"└───────────────────────────────────┴────────┴─────────┴─────────┘");
    }

    private void ShowAvailableMaterials()
    {
        Output.WriteLine("\n=== Your Material Properties ===");

        var propertyTotals = new Dictionary<ItemProperty, (double amount, int items)>();

        foreach (var stack in ctx.player.inventoryManager.Items)
        {
            var item = stack.FirstItem;
            foreach (var property in item.CraftingProperties)
            {
                if (!propertyTotals.ContainsKey(property))
                    propertyTotals[property] = (0, 0);

                var current = propertyTotals[property];
                propertyTotals[property] = (
                    current.amount + (item.Weight * stack.Count),
                    current.items + stack.Count
                );
            }
        }

        foreach (var kvp in propertyTotals.OrderBy(x => x.Key))
        {
            var (amount, items) = kvp.Value;
            Output.WriteLine($"{kvp.Key}: {amount:F1} total");
        }

        if (!propertyTotals.Any())
            Output.WriteLine("You don't have any materials with useful properties.");

        Input.WaitForKey();
        RunCraftingMenu();
    }

    private void ShowPlayerMaterials(List<CraftingPropertyRequirement> requirements)
    {
        foreach (var req in requirements)
        {
            double totalAmount = 0;
            int itemCount = 0;

            foreach (var stack in ctx.player.inventoryManager.Items)
            {
                var property = stack.FirstItem.GetProperty(req.Property);
                if (property != null)
                {
                    totalAmount += stack.TotalWeight;
                    itemCount += stack.Count;
                }
            }

            bool sufficient = totalAmount >= req.MinQuantity;
            string status = sufficient ? "✓" : "✗";

            Output.WriteLine($"  {status} {req.Property}: {totalAmount:F1}/{req.MinQuantity:F1}");
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // HUNTING
    // ═══════════════════════════════════════════════════════════════════════════

    private bool CanHunt()
    {
        var animals = ctx.CurrentLocation.Npcs
            .OfType<Animal>()
            .Where(a => a.IsAlive && a.CurrentLocation == ctx.CurrentLocation)
            .ToList();
        return animals.Any();
    }

    private void RunHuntingMenu()
    {
        Output.WriteLine("You scan the area for prey...");

        var animals = ctx.CurrentLocation.Npcs
            .OfType<Animal>()
            .Where(a => a.IsAlive && a.CurrentLocation == ctx.CurrentLocation)
            .ToList();

        var choice = new Choice<Action>();

        foreach (var animal in animals)
        {
            choice.AddOption($"Stalk {animal.Name}", () => BeginHunt(animal));
        }

        choice.AddOption("Cancel", () => { });

        choice.GetPlayerChoice().Invoke();
    }

    private void BeginHunt(Animal animal)
    {
        ctx.player.stealthManager.StartHunting(animal);

        var currentDistance = animal.DistanceFromPlayer;

        if (ctx.player.inventoryManager.Weapon is RangedWeapon rangedWeapon)
        {
            Output.WriteLine($"Distance: {currentDistance}m | Your {rangedWeapon.Name} effective range: {rangedWeapon.EffectiveRange}m (max: {rangedWeapon.MaxRange}m)");
        }
        else
        {
            Output.WriteWarning("You have no ranged weapon equipped. You'll need to get very close to attack with melee weapons.");
        }

        RunHuntingSubMenu();
    }

    private void RunHuntingSubMenu()
    {
        if (!ctx.player.stealthManager.IsHunting)
            return;

        var target = ctx.player.stealthManager.GetCurrentTarget();
        if (target != null)
        {
            var currentDistance = target.DistanceFromPlayer;
            Output.WriteLine($"Distance: {currentDistance}m");
            Output.WriteLine($"Animal state: {target.State}");
            Output.WriteLine();

            if (ctx.player.inventoryManager.Weapon is RangedWeapon rangedWeapon)
            {
                var distanceToRange = currentDistance - rangedWeapon.MaxRange;
                if (distanceToRange > 0)
                {
                    Output.WriteColored(ConsoleColor.Yellow, $"Distance until shooting range: {distanceToRange}m");
                    Output.WriteLine();
                }
                else
                {
                    Output.WriteSuccess("Within shooting range!");
                    Output.WriteLine();
                }
            }
        }

        if (!ctx.player.stealthManager.IsTargetValid())
            return;

        var choice = new Choice<Action>();
        choice.AddOption("Approach", ApproachAnimal);
        choice.AddOption("Assess Target", AssessTarget);

        if (ctx.player.ammunitionManager.CanShoot(out _))
            choice.AddOption("Shoot", ShootTarget);

        choice.AddOption("Stop Hunting", StopHunting);

        choice.GetPlayerChoice().Invoke();
    }

    private void ApproachAnimal()
    {
        bool success = ctx.player.stealthManager.AttemptApproach();

        if (success)
            ctx.player.Skills.GetSkill("Hunting").GainExperience(1);

        World.Update(7);

        if (ctx.player.stealthManager.IsHunting)
            RunHuntingSubMenu();
    }

    private void AssessTarget()
    {
        ctx.player.stealthManager.AssessTarget();
        Input.WaitForKey();
        RunHuntingSubMenu();
    }

    private void ShootTarget()
    {
        Animal? target = ctx.player.stealthManager.GetCurrentTarget();
        if (target == null)
        {
            Output.WriteLine("You no longer have a target.");
            return;
        }

        ctx.player.huntingManager.ShootTarget(target);
        World.Update(1);

        if (ctx.player.stealthManager.IsHunting)
            RunHuntingSubMenu();
    }

    private void StopHunting()
    {
        ctx.player.stealthManager.StopHunting("You give up the hunt.");
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // COMBAT
    // ═══════════════════════════════════════════════════════════════════════════

    private void StartCombat(Npc enemy)
    {
        if (!enemy.IsAlive || !enemy.IsFound) return;

        Output.WriteLine("!");
        Thread.Sleep(500);
        Output.WriteLine(CombatNarrator.DescribeCombatStart(ctx.player, enemy));

        ctx.player.IsEngaged = true;
        enemy.IsEngaged = true;

        bool enemyFirstStrike = enemy.Speed > ctx.player.Speed;

        if (enemyFirstStrike)
        {
            Output.WriteLine($"The {enemy.Name} moves with surprising speed!");
            Thread.Sleep(500);
            EnemyCombatTurn(enemy);
        }
        else
        {
            Output.WriteLine("You're quick to react, giving you the initiative!");
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

        Output.WriteLine("─────────────────────────────────────");
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
        ctx.player.Attack(enemy);

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
            ctx.player.Attack(enemy, targetPart.Name);

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
            Output.WriteWarning("You don't have enough skill to target an attack");
            return null;
        }

        Output.WriteLine($"Where do you want to target your attack on the {enemy.Name}?");

        List<BodyRegion> allParts = [];
        foreach (var part in enemy.Body.Parts)
        {
            if (depth > 0)
                allParts.Add(part);
        }

        return Input.GetSelectionFromList(allParts, true);
    }

    private void AttemptFlee(Npc enemy)
    {
        if (CombatUtils.SpeedCheck(ctx.player, enemy))
        {
            Output.WriteLine("You got away!");
            enemy.IsEngaged = false;
            ctx.player.IsEngaged = false;
            ctx.player.Skills.Reflexes.GainExperience(2);
        }
        else
        {
            Output.WriteLine("You weren't fast enough to get away from ", enemy, "!");
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
            Output.WriteDanger("Your vision fades to black as you collapse... You have died!");
            Environment.Exit(0);
        }
        else if (!enemy.IsAlive)
        {
            string[] victoryMessages = [
                $"The {enemy.Name} collapses, defeated!",
                $"You stand victorious over the fallen {enemy.Name}!",
                $"With a final blow, you bring down the {enemy.Name}!"
            ];
            Output.WriteLine(victoryMessages[Utils.RandInt(0, victoryMessages.Length - 1)]);

            int xpGain = CalculateExperienceGain(enemy);
            Output.WriteLine($"You've gained {xpGain} fighting experience!");
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
        ConsoleColor playerHealthColor = GetHealthColor(ctx.player.Body.Health / ctx.player.Body.MaxHealth);
        Output.WriteLineColored(playerHealthColor, $"You: {Math.Round(ctx.player.Body.Health * 100, 0)}/{Math.Round(ctx.player.Body.MaxHealth * 100, 1)} HP");

        ConsoleColor enemyHealthColor = GetHealthColor(enemy.Body.Health / enemy.Body.MaxHealth);
        Output.WriteLineColored(enemyHealthColor, $"{enemy.Name}: {Math.Round(enemy.Body.Health * 100, 0)}/{Math.Round(enemy.Body.MaxHealth * 100, 0)} HP");
    }

    private static ConsoleColor GetHealthColor(double healthPercentage)
    {
        if (healthPercentage < 0.2) return ConsoleColor.Red;
        if (healthPercentage < 0.5) return ConsoleColor.Yellow;
        return ConsoleColor.Green;
    }


    // ═══════════════════════════════════════════════════════════════════════════
    // STATS
    // ═══════════════════════════════════════════════════════════════════════════

    private void CheckStats()
    {
        BodyDescriber.Describe(ctx.player);
        ctx.player.Skills.Describe();
        Input.WaitForKey();
    }
}