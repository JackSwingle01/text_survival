using text_survival.Actions.Expeditions;
using text_survival.Actors;
using text_survival.Actors.NPCs;
using text_survival.Actors.Player;
using text_survival.Bodies;
using text_survival.Combat;
using text_survival.Core;
using text_survival.Crafting;
using text_survival.Environments;
using text_survival.Environments.Features;
using text_survival.IO;
using text_survival.Items;
using text_survival.Magic;

namespace text_survival.Actions;

public static class ActionFactory
{
    private static ActionBuilder CreateAction(string name) => ActionBuilderExtensions.CreateAction(name);
    public static class Common
    {
        public static IGameAction Return(string name = "Back")
        {
            return CreateAction(name).ThenReturn().Build();
        }
        public static IGameAction BackTo(string description, Func<IGameAction> actionFactory)
        {

            return CreateAction($"Back to {description}")
                    .ThenShow(ctx => [actionFactory()])
                    .Build();
        }

        public static IGameAction MainMenu()
        {
            return CreateAction("Main Menu")
                   .Do(ctx => BodyDescriber.DescribeSurvivalStats(ctx.player.Body, ctx.player.GetSurvivalContext()))
                   .ThenShow(ctx => [
                        Describe.LookAround(ctx.CurrentLocation),
                        Survival.AddFuelToFire(),
                        Survival.StartFire(),
                        // Survival.HarvestResources(),
                        // Survival.Forage(),
                        // Hunting.OpenHuntingMenu(), // MVP Hunting System
                        // Hunting.ViewBloodTrails(), // MVP Hunting System - Phase 4
                        ExpeditionActions.SelectExpedition(ExpeditionType.Forage),
                        Inventory.OpenInventory(),
                        Crafting.OpenCraftingMenu(),
                        Describe.CheckStats(),
                        Survival.Sleep(),
                        // Movement.Move(),
                   ])
                   .Build();
        }
    }

    public static class Survival
    {
        public static IGameAction Forage(string name = "Forage")
        {
            return CreateAction(name)
                .When(ctx => ctx.CurrentLocation.GetFeature<ForageFeature>() != null)
                .ShowMessage("You forage for 15 minutes")
                .Do(ctx =>
                {
                    var forageFeature = ctx.CurrentLocation.GetFeature<ForageFeature>()!;
                    forageFeature.Forage(0.25); // 15 minutes = 0.25 hours
                })
                .AndGainExperience("Foraging")
                .TakesMinutes(0) // ForageFeature.Forage() handles time update internally
                .ThenShow(ctx =>
                {
                    // Get items that were just found (marked with IsFound = true)
                    var foundItems = ctx.CurrentLocation.Items
                        .Where(i => i.IsFound)
                        .ToList();

                    if (foundItems.Count != 0)
                    {
                        // Show collection options
                        return new List<IGameAction>
                        {
                            Inventory.TakeAllFoundItems(),
                            Inventory.SelectFoundItems(),
                            Forage("Keep foraging"),
                            Common.Return("Leave items and finish foraging")
                        };
                    }
                    else
                    {
                        // No items found, continue foraging or return
                        return
                        [
                            Forage("Keep foraging"),
                            Common.Return("Finish foraging")
                        ];
                    }
                })
                .Build();
        }

        public static IGameAction Sleep()
        {
            return CreateAction("Sleep")
            .When(ctx => ctx.player.Body.IsTired)
            .Do(ctx =>
            {
                Output.WriteLine("How many hours would you like to sleep? (1-24)");
                int hours = Input.ReadInt();

                // Validate sleep duration
                if (hours < 1 || hours > 24)
                {
                    Output.WriteWarning("You can only sleep for 1-24 hours at a time.");
                    return;
                }

                int minutes = hours * 60;
                ctx.player.Body.Rest(minutes);
                World.Update(minutes); // Suppress status messages during sleep
            })
            .TakesMinutes(0) // Handles time manually based on user input
            .ThenReturn()
            .Build();
        }

        public static IGameAction AddFuelToFire()
        {
            return CreateAction("Add Fuel to Fire")
            .When(ctx =>
            {
                // Check if there's a fire at the location
                var fire = ctx.CurrentLocation.GetFeature<HeatSourceFeature>();
                if (fire == null) return false;

                // Check if player has any flammable items
                var flammableItems = ctx.player.inventoryManager.Items
                    .Where(stack => stack.FirstItem.CraftingProperties.Contains(ItemProperty.Flammable))
                    .ToList();

                return flammableItems.Count > 0;
            })
            .Do(ctx =>
            {
                var fire = ctx.CurrentLocation.GetFeature<HeatSourceFeature>()!;

                // Display fire status with new physics system
                string firePhase = fire.GetFirePhase();
                double fireTemp = fire.GetCurrentFireTemperature();
                double heatOutput = fire.GetEffectiveHeatOutput();
                double fuelMinutes = fire.HoursRemaining * 60;

                // Color-coded phase display
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

                // Show detailed stats
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

                // Get all fuel items (items with FuelMassKg > 0)
                var fuelStacks = ctx.player.inventoryManager.Items
                    .Where(stack => stack.FirstItem.IsFuel())
                    .ToList();

                Output.WriteLine("Available fuel:");
                var fuelOptions = new List<(ItemStack stack, bool canAdd, bool isSharp)>();

                foreach (var stack in fuelStacks)
                {
                    var firstItem = stack.FirstItem;
                    bool canAdd = fire.CanAddFuel(firstItem);
                    bool isSharp = firstItem.CraftingProperties.Contains(ItemProperty.Sharp);

                    var fuelType = firstItem.GetFuelType();
                    string fuelInfo = fuelType.HasValue ? $"[{fuelType.Value}]" : "";

                    // Show fuel mass and estimated burn time
                    double massKg = firstItem.FuelMassKg;
                    double burnHours = massKg; // Default 1kg/hr
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

                // Check if fire is hot enough for this fuel
                if (!canAddFuel)
                {
                    Output.WriteWarning("The fire isn't hot enough to burn that fuel type!");
                    Output.WriteLine("Try adding tinder or kindling first to build up the fire's temperature.");
                    return;
                }

                // Warn if burning sharp tool
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

                // Calculate overflow based on mass capacity
                double maxCanAddKg = fire.MaxFuelCapacityKg - fire.FuelMassKg;
                double fuelMassKg = selectedStack.FirstItem.FuelMassKg;
                bool overflow = fuelMassKg > maxCanAddKg;

                // Remove item from inventory
                var itemToRemove = selectedStack.Pop();
                ctx.player.inventoryManager.RemoveFromInventory(itemToRemove);

                // Track ember state before adding fuel
                bool hadEmbers = fire.HasEmbers;

                // Add fuel to fire (auto-relights from embers)
                // Note: This uses legacy method - will be refactored later with proper fuel system
                fire.AddFuel(itemToRemove);

                // Show result
                Output.WriteLine($"\nYou add the {itemToRemove.Name} to the {fire.Name}.");
                if (overflow)
                {
                    double wastedKg = fuelMassKg - maxCanAddKg;
                    Output.WriteWarning($"The fire was already near capacity. {wastedKg:F2} kg of fuel was wasted.");
                }

                double newFuelMinutes = fire.HoursRemaining * 60;
                Output.WriteLine($"The fire now has {fire.FuelMassKg:F2} kg of fuel ({newFuelMinutes:F0} minutes).");
                Output.WriteLine($"Fire temperature: {fire.GetCurrentFireTemperature():F0}°F ({fire.GetFirePhase()})");

                // Show status after adding fuel
                if (hadEmbers && fire.IsActive)
                {
                    Output.WriteLine("The embers ignite the new fuel! The fire springs back to life.");
                }
                else if (!fire.IsActive)
                {
                    Output.WriteWarning("\nThe fire is cold. You need to use 'Start Fire' to light it with proper fire-making materials.");
                }

                World.Update(1); // Takes 1 minute to add fuel
            })
            .TakesMinutes(0) // Handled manually via World.Update(1)
            .ThenReturn()
            .Build();
        }

        public static IGameAction StartFire()
        {
            return CreateAction("Start Fire")
            .When(ctx =>
            {
                var fire = ctx.CurrentLocation.GetFeature<HeatSourceFeature>();

                // Show if no fire exists OR if fire is fully cold (not embers)
                bool noFire = fire == null;
                bool fullyColdFire = fire != null && fire.HoursRemaining == 0 && !fire.HasEmbers;

                if (!noFire && !fullyColdFire) return false;

                // Check if player has fire-making tools and materials
                var inventory = ctx.player.inventoryManager;

                // Need a fire-making tool (identified by name)
                bool hasTool = inventory.Items.Any(s => IsFireMakingTool(s.FirstItem));

                // Need tinder and kindling
                double tinder = inventory.Items
                    .Where(s => s.FirstItem.HasProperty(ItemProperty.Tinder, 0))
                    .Sum(s => s.TotalWeight);
                double kindling = inventory.Items
                    .Where(s => s.FirstItem.HasProperty(ItemProperty.Wood, 0))
                    .Sum(s => s.TotalWeight);

                return hasTool && tinder >= 0.05 && kindling >= 0.3;
            })
            .Do(ctx =>
            {
                var inventory = ctx.player.inventoryManager;
                var existingFire = ctx.CurrentLocation.GetFeature<HeatSourceFeature>();
                bool relightingFire = existingFire != null;

                if (relightingFire)
                {
                    Output.WriteLine($"You prepare to relight the fire.");
                }
                else
                {
                    Output.WriteLine("You prepare to start a fire.");
                }

                // Find available fire-making tools
                var availableTools = inventory.Items
                    .Where(s => IsFireMakingTool(s.FirstItem) && s.FirstItem.NumUses > 0)
                    .Select(s => s.FirstItem)
                    .ToList();

                if (!availableTools.Any())
                {
                    Output.WriteWarning("You don't have any working fire-making tools!");
                    return;
                }

                // Check for tinder (provides +15% bonus)
                bool hasTinder = inventory.Items.Any(s => s.FirstItem.HasProperty(ItemProperty.Tinder));
                double tinderBonus = hasTinder ? 0.15 : 0.0;

                // Show tool options
                Output.WriteLine("\nChoose your fire-making tool:");
                if (hasTinder)
                {
                    Output.WriteLine("  [Tinder available: +15% success bonus]");
                }
                int optionNum = 1;
                foreach (var tool in availableTools)
                {
                    // Calculate success chance
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
                        // No DC requirement, skill still helps
                        double skillModifier = skill.Level * 0.1;
                        successChance += skillModifier;
                    }

                    // Add tinder bonus
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

                // Calculate success chance using SkillCheckCalculator
                var (baseChance, skillDC) = GetToolSkillParameters(selectedTool);
                var playerSkill = ctx.player.Skills.GetSkill("Firecraft");
                double finalSuccessChance = SkillCheckCalculator.CalculateSuccessChance(
                    baseChance,
                    playerSkill.Level,
                    skillDC);

                // Add tinder bonus (fire-making specific)
                finalSuccessChance += tinderBonus;
                finalSuccessChance = Math.Clamp(finalSuccessChance, 0.05, 0.95);

                Output.WriteLine($"\nYou work with the {selectedTool.Name}...");
                World.Update(15); // 15 minutes per attempt

                // Roll for success
                bool success = Utils.DetermineSuccess(finalSuccessChance);

                if (success)
                {
                    // Success: Consume tinder + kindling + tool durability
                    ConsumeMaterial(ctx.player, ItemProperty.Tinder, 0.05);
                    ConsumeMaterial(ctx.player, ItemProperty.Wood, 0.3);

                    bool toolBroke = selectedTool.UseOnce();
                    if (toolBroke)
                    {
                        inventory.RemoveFromInventory(selectedTool);
                    }

                    // Create or relight fire
                    if (relightingFire)
                    {
                        Output.WriteSuccess($"\nSuccess! You relight the fire! ({finalSuccessChance:P0} chance)");
                        var tinderFuel = ItemFactory.MakeTinderBundle(); // 0.03kg tinder
                        var kindlingFuel = ItemFactory.MakeStick(); // 0.5kg kindling
                        existingFire!.AddFuel(tinderFuel, 0.03); // Add tinder
                        existingFire.AddFuel(kindlingFuel, 0.3); // Add kindling
                    }
                    else
                    {
                        Output.WriteSuccess($"\nSuccess! You start a fire! ({finalSuccessChance:P0} chance)");
                        var newFire = new HeatSourceFeature(ctx.CurrentLocation);
                        var tinderFuel = ItemFactory.MakeTinderBundle(); // 0.03kg tinder
                        var kindlingFuel = ItemFactory.MakeStick(); // 0.5kg kindling
                        newFire.AddFuel(tinderFuel, 0.03); // Add tinder
                        newFire.AddFuel(kindlingFuel, 0.3); // Add kindling
                        ctx.CurrentLocation.Features.Add(newFire);
                    }

                    playerSkill.GainExperience(3); // Success XP
                }
                else
                {
                    // Failure: Consume only tinder, tool still loses durability
                    ConsumeMaterial(ctx.player, ItemProperty.Tinder, 0.05);

                    bool toolBroke = selectedTool.UseOnce();
                    if (toolBroke)
                    {
                        inventory.RemoveFromInventory(selectedTool);
                    }

                    Output.WriteWarning($"\nYou failed to start the fire. The tinder was wasted. ({finalSuccessChance:P0} chance)");
                    playerSkill.GainExperience(1); // Failure XP (learning from mistakes)
                }
            })
            .TakesMinutes(0) // Handled manually via World.Update
            .ThenReturn()
            .Build();
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

        private static bool IsFireMakingTool(Item item)
        {
            return item.Name is "Hand Drill" or "Bow Drill" or "Flint and Steel";
        }

        private static (double baseChance, int skillDC) GetToolSkillParameters(Item tool)
        {
            return tool.Name switch
            {
                "Hand Drill" => (0.30, 0),      // 30% base, no DC
                "Bow Drill" => (0.50, 1),       // 50% base, DC 1
                "Flint and Steel" => (0.90, 0), // 90% base, no DC
                _ => (0.30, 0)                   // Default
            };
        }

        // Harvestable Feature Actions

        public static IGameAction HarvestResources()
        {
            return CreateAction("Harvest Resources")
                .When(ctx => ctx.CurrentLocation.Features
                    .OfType<HarvestableFeature>()
                    .Any(f => f.IsDiscovered))
                .ThenShow(ctx =>
                {
                    var harvestables = ctx.CurrentLocation.Features
                        .OfType<HarvestableFeature>()
                        .Where(f => f.IsDiscovered)
                        .Select(f => InspectHarvestable(f))
                        .ToList<IGameAction>();

                    harvestables.Add(Common.Return("Back to Main Menu"));
                    return harvestables;
                })
                .Build();
        }

        public static IGameAction HarvestFromFeature(HarvestableFeature feature)
        {
            return CreateAction($"Harvest from {feature.DisplayName}")
                .When(_ => feature.IsDiscovered && feature.HasAvailableResources())
                .Do(ctx =>
                {
                    Output.WriteLine("Harvesting will take 5 minutes...");
                    Output.WriteLine();

                    var items = feature.Harvest();

                    if (items.Count > 0)
                    {
                        foreach (var item in items)
                        {
                            ctx.player.TakeItem(item);
                        }

                        // Group items by name for cleaner display
                        var grouped = items.GroupBy(i => i.Name)
                            .Select(g => $"{g.Key} ({g.Count()})");
                        Output.WriteSuccess($"You spent 5 minutes harvesting and gathered: {string.Join(", ", grouped)}");
                    }
                    else
                    {
                        Output.WriteLine($"The {feature.DisplayName} has been depleted.");
                    }
                })
                .TakesMinutes(5) // Harvesting is quick
                .ThenReturn()
                .Build();
        }

        public static IGameAction InspectHarvestable(HarvestableFeature feature)
        {
            return CreateAction($"Inspect {feature.DisplayName}")
                .When(_ => feature.IsDiscovered)
                .Do(ctx =>
                {
                    Output.WriteLine(feature.Description);
                    Output.WriteLine();
                    Output.WriteLine($"Status: {feature.GetStatusDescription()}");
                })
                .ThenShow(ctx => [
                    HarvestFromFeature(feature),
                    Common.Return()
                ])
                .Build();
        }
    }

    public static class Movement
    {
        public static IGameAction GoToLocation(Location location)
        {
            return CreateAction($"Go to {location.Name}{(location.Visited ? " (Visited)" : "")}")
            .When(_ => location.IsFound)
            .Do(ctx => location.Interact(ctx.player))
            .ThenReturn()
            .Build();
        }

        public static IGameAction GoToLocationByMap()
        {
            return CreateAction("Use map to navigate locally")
            .Do(ctx =>
            {
                // Show the unified map
                string mapDisplay = UI.MapRenderer.RenderUnifiedMap(
                    ctx.player.GetWorldMap(),
                    ctx.player.CurrentZone,
                    ctx.CurrentLocation
                );
                Output.WriteLine(mapDisplay);

                // Get directional input
                string direction = UI.MapController.GetDirectionalInput();

                if (direction == "Q")
                {
                    Output.WriteLine("You decide to stay where you are.");
                    return;
                }

                // Try to travel locally first, then zone if no local location
                var localDestination = UI.MapController.GetLocationInDirection(
                    ctx.player.CurrentZone,
                    ctx.CurrentLocation,
                    direction
                );

                if (localDestination != null)
                {
                    // Local travel within zone
                    ctx.player.TravelToLocalLocation(direction);
                }
                else
                {
                    // No local location in that direction - ask if they want to travel to adjacent zone
                    Output.WriteLine($"There is no nearby location to the {direction.ToLower()}.");
                    Output.WriteLine("Would you like to travel to the adjacent region instead? (y/n)");
                    if (Input.ReadYesNo())
                    {
                        ctx.player.TravelToAdjacentZone(direction);
                    }
                }
            })
            .ThenReturn()
            .Build();
        }

        public static IGameAction Travel()
        {
            return CreateAction("Travel to a different area")
            .When(ctx => ctx.CurrentLocation.GetFeature<ShelterFeature>() == null) // when not inside a shelter
            .Do(ctx => ctx.player.Travel()) // todo move out of player class
            .ThenReturn()
            .Build();
        }

        public static IGameAction Move()
        {
            return CreateAction("Go somewhere else")
            .When(ctx => AbilityCalculator.CalculateVitality(ctx.player.Body) > .2)
            .Do(ctx => // just determine what text to display
            {
                var locations = ctx.CurrentLocation.GetNearbyLocations().Where(l => l.IsFound).ToList();
                bool inside = ctx.player.CurrentLocation.GetFeature<ShelterFeature>() != null;
                if (inside)
                {
                    Output.WriteLine($"You can leave the shelter and go outside.");
                }
                if (locations.Count == 0)
                {
                    Output.WriteLine("You don't see anywhere noteworthy nearby; you can stay here or travel to a new area.");
                    return;
                }
                else if (locations.Count == 1)
                {
                    Output.WriteLine($"You can go to the {locations[0].Name} or pack up and leave the region.");
                }
                else
                {
                    Output.WriteLine("You see several places that you can go to from here, or you can pack up and leave the region.");
                }
            })
            .ThenShow(ctx =>
            {
                var options = new List<IGameAction>();

                // Add new map-based navigation as first option
                options.Add(GoToLocationByMap());

                // Keep traditional location list for backward compatibility
                foreach (var location in ctx.CurrentLocation.GetNearbyLocations())
                {
                    options.Add(GoToLocation(location));
                }
                options.Add(Travel());
                options.Add(Common.Return("Stay Here..."));
                return options;
            })
            .Build();
        }
    }

    public static class Inventory
    {
        public static IGameAction DescribeItem(Item item)
        {
            return CreateAction($"Inspect {item}")
            .Do(_ => item.Describe())
            .ThenShow(_ => [OpenInventory()])
            .Build();
        }

        public static IGameAction DropItem(Item item)
        {
            return CreateAction($"Drop {item}")
            .ShowMessage($"You drop the {item}")
            .Do(ctx => ctx.player.DropItem(item))
            .ThenShow(_ => [OpenInventory()])
            .Build();
        }

        public static IGameAction UseItem(Item item)
        {
            return CreateAction($"Use {item}")
            .Do(ctx => ctx.player.UseItem(item))
            .ThenShow(_ => [OpenInventory()])
            .Build();
        }

        public static IGameAction TakeAllFoundItems()
        {
            return CreateAction("Take all items")
            .Do(ctx =>
            {
                var foundItems = ctx.CurrentLocation.Items
                    .Where(i => i.IsFound)
                    .ToList();

                foreach (var item in foundItems)
                {
                    ctx.player.TakeItem(item);
                }

                Output.WriteLine($"You collected {foundItems.Count} item(s).");
            })
            .ThenShow(_ => [Survival.Forage("Keep foraging"), Common.Return("Finish foraging")])
            .Build();
        }

        public static IGameAction SelectFoundItems()
        {
            return CreateAction("Select items to take")
            .ThenShow(ctx =>
            {
                var foundItems = ctx.CurrentLocation.Items
                    .Where(i => i.IsFound)
                    .ToList();

                var actions = new List<IGameAction>();
                foreach (var item in foundItems)
                {
                    actions.Add(PickUpItemFromForaging(item));
                }
                actions.Add(Survival.Forage("Keep foraging"));
                actions.Add(Common.Return("Finish foraging"));
                return actions;
            })
            .Build();
        }

        public static IGameAction PickUpItemFromForaging(Item item)
        {
            return CreateAction($"Take {item.Name}")
            .When(_ => item.IsFound)
            .ShowMessage($"You take the {item}")
            .Do(ctx => ctx.player.TakeItem(item))
            .ThenShow(ctx =>
            {
                // Check if there are more items to collect
                var remainingItems = ctx.CurrentLocation.Items
                    .Where(i => i.IsFound)
                    .ToList();

                if (remainingItems.Any())
                {
                    // Still items left, show select menu again
                    return new List<IGameAction> { SelectFoundItems() };
                }
                else
                {
                    // No more items, continue foraging or return
                    return new List<IGameAction>
                    {
                        Survival.Forage("Keep foraging"),
                        Common.Return("Finish foraging")
                    };
                }
            })
            .Build();
        }

        public static IGameAction OpenInventory()
        {
            return CreateAction($"Open inventory")
            .OnlyIfHasItems()
            .Do(ctx => ctx.player.inventoryManager.Describe())
            .ThenShow(ctx =>
            {
                var options = new List<IGameAction>();
                foreach (ItemStack stack in ctx.player.inventoryManager.Items)
                {
                    options.Add(DecideInventoryAction(stack));
                }
                options.Add(Common.Return("Close Inventory"));
                return options;
            })
            .WithPrompt("Select an item:")
            .Build();
        }

        public static IGameAction OpenContainer(Container container)
        {
            return CreateAction($"Look in {container}{(container.IsEmpty ? " (Empty)" : "")}")
            .When(ctx => container.IsFound && !container.IsEmpty)
            .OnlyIfCanBypassHostiles()
            .ShowMessage($"You open the {container}")
            .ThenShow(ctx =>
            {
                var options = new List<IGameAction>();
                var itemStacks = ItemStack.CreateStacksFromItems(container.Items);
                foreach (var stack in itemStacks)
                {
                    options.Add(TakeStackFromContainer(container, stack));
                }
                options.Add(TakeAllFromContainer(container));
                options.Add(Common.Return($"Close {container.Name}"));
                return options;
            })
            .WithPrompt("Select an item:")
            .Build();
        }

        public static IGameAction TryToReachContainer(Container container)
        {
            return CreateAction($"Try to reach {container.Name}")
                 .When(ctx => container.IsFound &&
                            !container.IsEmpty)
                 .OnlyIfBlockedByHostiles()
                 .Do(ctx => Output.WriteLine("You couldn't get past the ", CombatUtils.GetFastestHostileNpc(ctx.CurrentLocation)!, "!"))
                 .ThenShow(ctx => [Combat.StartCombat(CombatUtils.GetFastestHostileNpc(ctx.CurrentLocation)!)])
                 .Build();
        }

        public static IGameAction DecideInventoryAction(ItemStack stack)
        {
            Item item = stack.Peek();
            return CreateAction(stack.DisplayName)
            .ThenShow(_ => [UseItem(item),
                            DescribeItem(item),
                            DropItem(item),
                            Common.BackTo("inventory", OpenInventory)]
                            )
            .WithPrompt($"What would you like to do with the {item.Name}")
            .Build();
        }

        public static IGameAction TakeAllFromContainer(Container container)
        {
            return CreateAction("Take all")
            .When(_ => container.Count() > 1)
            .Do(ctx =>
            {
                while (!container.IsEmpty)
                {
                    var item = container.Items.First();
                    container.Remove(item);
                    ctx.player.TakeItem(item);
                }
            })
            .ThenReturn()
            .Build();
        }


        public static IGameAction TakeStackFromContainer(Container container, ItemStack stack)
        {
            return CreateAction($"Take {stack.DisplayName}")
            .Do(ctx =>
            {
                while (stack.Count > 0)
                {
                    var item = stack.Pop();
                    container.Remove(item);
                    ctx.player.TakeItem(item);
                }
            })
            .ThenShow(_ => [OpenContainer(container)]) // will be auto selected until the container is empty then it will auto return
            .Build();
        }

        public static IGameAction PickUpItem(Item item)
        {
            return CreateAction($"Pick up {item.Name}")
            .When(_ => item.IsFound)
            .OnlyIfCanBypassHostiles()
            .ShowMessage($"You take the {item}")
            .Do(ctx => ctx.player.TakeItem(item))
            .ThenLookAround()
            .Build();
        }

        public static IGameAction LootNpc(Npc npc)
        {
            return CreateAction($"Loot {npc.Name}")
            .When(_ => npc.IsFound)
            .When(_ => !npc.IsAlive)
            .When(_ => !npc.Loot.IsEmpty)
            .OnlyIfCanBypassHostiles()
            .ThenShow(_ => [OpenContainer(npc.Loot)])
            .Build();
        }

    }

    public static class Combat
    {
        public static IGameAction StartCombat(Npc enemy)
        {
            return CreateAction($"Fight {enemy.Name}")
            .When(ctx => enemy.IsAlive && enemy.IsFound)
            .Do(ctx =>
            {
                Output.WriteLine("!");
                Thread.Sleep(500);
                Output.WriteLine(CombatNarrator.DescribeCombatStart(ctx.player, enemy));

                ctx.player.IsEngaged = true;
                enemy.IsEngaged = true;
                ctx.EngagedEnemy = enemy;

                // First strike determination
                bool enemyFirstStrike = AbilityCalculator.CalculateSpeed(enemy.Body) > AbilityCalculator.CalculateSpeed(ctx.player.Body);

                if (enemyFirstStrike)
                {
                    Output.WriteLine($"The {enemy.Name} moves with surprising speed!");
                    Thread.Sleep(500);
                    ctx.NextActionOverride = EnemyCombatTurn(enemy);
                    return;
                }
                else
                {
                    Output.WriteLine("You're quick to react, giving you the initiative!");
                    Thread.Sleep(500);
                }
            })
            .ThenShow(_ => [PlayerCombatTurn(enemy)])
            .Build();
        }

        public static IGameAction AttackEnemy(Npc enemy)
        {
            return CreateAction($"Attack {enemy.Name}")
            .Do(ctx => ctx.player.Attack(enemy))
            .ThenShow(ctx => [EnemyCombatTurn(enemy), EndCombat(enemy)])
            .Build();
        }


        public static IGameAction TargetedAttackEnemy(Npc enemy)
        {
            return CreateAction($"Targeted Attack {enemy.Name}")
            .When(ctx => ctx.player.Skills.Fighting.Level > 1)
            .Do(ctx =>
            {
                int fightingSkill = ctx.player.Skills.Fighting.Level;
                var targetPart = SelectTargetPart(enemy, fightingSkill);
                if (targetPart != null)
                {
                    ctx.player.Attack(enemy, targetPart.Name);
                }
                else
                {
                    ctx.NextActionOverride = PlayerCombatTurn(enemy);
                }
            })
            .ThenShow(ctx => [EnemyCombatTurn(enemy), EndCombat(enemy)])
            .Build();
        }
        private static BodyRegion? SelectTargetPart(Actor enemy, int depth)
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
                // if (depth > 1)
                //     allParts.Add(part.Skin);
                // if (depth > 2)
                //     allParts.Add(part.Muscle);
                // if (depth > 3)
                //     allParts.Add(part.Bone);
                // if (depth > 4)
                //     allParts.AddRange(part.Organs);
            }

            var choice = Input.GetSelectionFromList(allParts, true);
            if (choice == null)
                return null;

            // todo return part itself
            return choice;
        }

        public static IGameAction EndCombat(Npc enemy)
        {
            return CreateAction("End Combat")
            .When(ctx => !enemy.IsEngaged || !ctx.player.IsEngaged || !ctx.player.IsAlive || !enemy.IsAlive)
            .Do(ctx =>
            {
                ctx.player.IsEngaged = false;
                enemy.IsEngaged = false;

                // Combat end
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

                    // Calculate experience based on enemy difficulty
                    int xpGain = CalculateExperienceGain(enemy);
                    Output.WriteLine($"You've gained {xpGain} fighting experience!");
                    ctx.player.Skills.Fighting.GainExperience(xpGain);
                }
            })
            .ThenReturn()
            .Build();
        }
        private static int CalculateExperienceGain(Npc enemy)
        {
            int baseXP = 5;

            // Adjust based on enemy weight/size
            double sizeMultiplier = Math.Clamp(enemy.Body.Weight / 50, 0.5, 3.0);

            // Adjust based on enemy weapon damage
            double weaponMultiplier = Math.Clamp(enemy.ActiveWeapon.Damage / 8, 0.5, 2.0);

            return (int)(baseXP * sizeMultiplier * weaponMultiplier);
        }

        public static IGameAction Flee(Npc enemy)
        {
            return CreateAction("Flee")
            .When(ctx => AbilityCalculator.CalculateSpeed(ctx.player.Body) > .25)
            .Do(ctx =>
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
                }
            })
            .ThenShow(_ => [EnemyCombatTurn(enemy), EndCombat(enemy)])
            .Build();
        }
        public static IGameAction EnemyCombatTurn(Npc enemy)
        {
            return CreateAction("Enemy Turn")
            .When(ctx => ctx.player.IsAlive && enemy.IsAlive && enemy.IsEngaged)
            .Do(ctx =>
            {
                Thread.Sleep(500); // Pause before enemy attack
                enemy.Attack(ctx.player);
            })
            .ThenShow(ctx => [PlayerCombatTurn(enemy), EndCombat(enemy)])
            .Build();
        }

        public static IGameAction PlayerCombatTurn(Npc enemy)
        {
            return CreateAction("Player Turn")
            .When(ctx => ctx.player.IsAlive && enemy.IsAlive && ctx.player.IsEngaged)
            .Do(ctx =>
            {
                Output.WriteLine("─────────────────────────────────────");
                DisplayCombatStatus(ctx.player, enemy);
            })
            .ThenShow(ctx => [AttackEnemy(enemy), TargetedAttackEnemy(enemy), Magic.SelectSpell(enemy), Flee(enemy)])
            .Build();
        }

        private static void DisplayCombatStatus(Player player, Actor enemy)
        {
            // Player status
            ConsoleColor playerHealthColor = GetHealthColor(player.Body.Health / player.Body.MaxHealth);
            Output.WriteLineColored(playerHealthColor, $"You: {Math.Round(player.Body.Health * 100, 0)}/{Math.Round(player.Body.MaxHealth * 100, 1)} HP");

            // Enemy status
            ConsoleColor enemyHealthColor = GetHealthColor(enemy.Body.Health / enemy.Body.MaxHealth);
            Output.WriteLineColored(enemyHealthColor, $"{enemy.Name}: {Math.Round(enemy.Body.Health * 100, 0)}/{Math.Round(enemy.Body.MaxHealth * 100, 0)} HP");
        }

        private static ConsoleColor GetHealthColor(double healthPercentage)
        {
            if (healthPercentage < 0.2) return ConsoleColor.Red;
            if (healthPercentage < 0.5) return ConsoleColor.Yellow;
            return ConsoleColor.Green;
        }
    }


    public static class Magic
    {
        public static IGameAction SelectSpell(Npc enemy)
        {
            return CreateAction("Cast Spell")
            .When(ctx => ctx.player._spells.Count > 0)
            .ThenShow(ctx =>
            {
                List<IGameAction> options = [];
                foreach (Spell spell in ctx.player._spells)
                {
                    options.Add(SelectSpellTarget(spell, enemy));
                }
                var back = Combat.PlayerCombatTurn(enemy);
                back.Name = "Choose a different action.";
                options.Add(back);
                return options;
            })
            .WithPrompt("Which spell would you like to cast?")
            .Build();
        }

        public static IGameAction SelectSpellTarget(Spell spell, Npc enemy)
        {
            return CreateAction($"Cast {spell}")
            .ThenShow(ctx =>
            {
                var options = new List<IGameAction>
                {
                CastSpellAtTarget(spell, enemy, ctx.player),
                CastSpellAtTarget(spell, enemy, enemy)
                };

                if (ctx.player.Skills.Magic?.Level > 1)
                {
                    options.Add(TargetedCastSpellAtTarget(spell, enemy, ctx.player));
                    options.Add(TargetedCastSpellAtTarget(spell, enemy, enemy));
                }

                return options;
            })
            .WithPrompt($"Which target would you like to cast {spell} on?")
            .Build();
        }

        public static IGameAction CastSpellAtTarget(Spell spell, Npc enemy, Actor target)
        {
            return CreateAction($"Cast {spell} on {target}")
            .Do(ctx => spell.Cast(target))
            .ThenShow(ctx => [Combat.EnemyCombatTurn(enemy), Combat.EndCombat(enemy)])
            .Build();
        }

        public static IGameAction TargetedCastSpellAtTarget(Spell spell, Npc enemy, Actor target)
        {
            return CreateAction($"Targeted Cast {spell} on {target}")
            .When(ctx => ctx.player.Skills.Magic.Level > 1)
            .Do(ctx =>
            {
                int magicSkill = ctx.player.Skills.Magic.Level;
                BodyRegion? targetPart = SelectSpellTargetPart(target, magicSkill);
                if (targetPart != null)
                {
                    spell.Cast(target, targetPart);
                }
                else
                {
                    ctx.NextActionOverride = Combat.PlayerCombatTurn(enemy);
                }
            })
            .ThenShow(ctx => [Combat.EnemyCombatTurn(enemy), Combat.EndCombat(enemy)])
            .Build();
        }

        private static BodyRegion? SelectSpellTargetPart(Actor target, int depth)
        {
            if (depth <= 0)
            {
                Output.WriteWarning("You don't have enough magical skill to target a specific body part");
                return null;
            }

            Output.WriteLine($"Select a part to target on the {target.Name}:");
            var parts = target.Body.Parts;
            return Input.GetSelectionFromList(parts, true);
        }
    }

    public static class Crafting
    {
        public static IGameAction OpenCraftingMenu()
        {
            return CreateAction("Craft Items")
                .ThenShow(ctx =>
                {
                    var actions = new List<IGameAction>();
                    var availableRecipes = ctx.CraftingManager.GetAvailableRecipes();

                    if (availableRecipes.Count == 0)
                    {
                        Output.WriteLine("You don't know how to craft anything here, or you lack the required materials.");
                        return [ShowAllRecipes(), Common.Return("Go back")];
                    }

                    Output.WriteLine("What would you like to craft?");

                    // Group recipes by type
                    var itemRecipes = availableRecipes.Where(r => r.ResultType == CraftingResultType.Item).ToList();
                    var featureRecipes = availableRecipes.Where(r => r.ResultType == CraftingResultType.LocationFeature).ToList();
                    var shelterRecipes = availableRecipes.Where(r => r.ResultType == CraftingResultType.Shelter).ToList();

                    if (itemRecipes.Count != 0)
                    {
                        Output.WriteLine("\n--- Items ---");
                        foreach (var recipe in itemRecipes)
                            actions.Add(CraftItem(recipe));
                    }

                    if (featureRecipes.Count != 0)
                    {
                        Output.WriteLine("\n--- Build Features ---");
                        foreach (var recipe in featureRecipes)
                            actions.Add(CraftItem(recipe));
                    }

                    if (shelterRecipes.Count != 0)
                    {
                        Output.WriteLine("\n--- Build Shelters ---");
                        foreach (var recipe in shelterRecipes)
                            actions.Add(CraftItem(recipe));
                    }

                    actions.Add(ShowAllRecipes());
                    actions.Add(ShowAvailableProperties());
                    actions.Add(Common.Return("Stop crafting"));

                    return actions;
                })
                .When(ctx => ctx.CraftingManager.GetAvailableRecipes().Count > 0)
                .Build();
        }

        public static IGameAction CraftItem(CraftingRecipe recipe)
        {
            return CreateAction($"Craft {recipe.Name}")
                .Do(ctx =>
                {
                    // Show recipe details
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

                    // Show what player has
                    Output.WriteLine("\nYour available materials:");
                    ShowPlayerProperties(ctx.player, recipe.RequiredProperties);

                    // Show preview of what will be consumed
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
                })
                .TakesMinutes(0) // CraftingManager.Craft() handles time update internally
                .ThenShow(ctx => [OpenCraftingMenu()])
                .Build();
        }

        public static IGameAction ShowAllRecipes()
        {
            return CreateAction("View All Known Recipes")
                .Do(ctx =>
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
                            PrintRecipeTable(recipe, ctx.player);
                            Output.WriteLine();
                        }
                    }

                })
                .WaitForUserInputToContinue()
                .ThenShow(ctx => [OpenCraftingMenu()])
                .Build();
        }

        private static void PrintRecipeTable(CraftingRecipe recipe, Player player)
        {
            bool canCraft = recipe.CanCraft(player);
            string status = canCraft ? "[✓]" : "[✗]";

            // Header with recipe name
            string header = $">>> {recipe.Name.ToUpper()} {status} <<<";
            int tableWidth = 64;

            Output.WriteLine($"┌{new string('─', tableWidth)}┐");
            Output.WriteLine($"│{header.PadLeft((tableWidth + header.Length) / 2).PadRight(tableWidth)}│");
            Output.WriteLine($"├{new string('─', tableWidth)}┤");

            // Info row
            string fireReq = recipe.RequiresFire ? " • Fire Required" : "";
            string infoRow = $"{recipe.CraftingTimeMinutes} min • {recipe.RequiredSkill} level {recipe.RequiredSkillLevel}{fireReq}";
            Output.WriteLine($"│ {infoRow.PadRight(tableWidth - 2)} │");

            // Description row
            string description = recipe.Description;
            if (description.Length > tableWidth - 2)
            {
                description = string.Concat(description.AsSpan(0, tableWidth - 5), "...");
            }
            Output.WriteLine($"│ {description.PadRight(tableWidth - 2)} │");

            // Materials table header
            Output.WriteLine($"├───────────────────────────────────┬────────┬─────────┬─────────┤");
            Output.WriteLine($"│ Material                          │ Qty    │ Quality │ Consumed│");
            Output.WriteLine($"├───────────────────────────────────┼────────┼─────────┼─────────┤");

            // Materials rows
            foreach (var req in recipe.RequiredProperties)
            {
                string propertyName = req.Property.ToString();
                string material = propertyName.Length > 31
                    ? propertyName[..28] + "..."
                    : propertyName;

                string quantity = $"{req.MinQuantity:F1}x";
                string consumed = req.IsConsumed ? "✓" : "✗";

                Output.WriteLine($"│ {material,-33} │ {quantity,6} │ {0,7} │ {consumed,7} │");
            }

            Output.WriteLine($"└───────────────────────────────────┴────────┴─────────┴─────────┘");
        }

        public static IGameAction ShowAvailableProperties()
        {
            return CreateAction("Show My Materials")
                .Do(ctx =>
                {
                    Output.WriteLine("\n=== Your Material Properties ===");

                    var propertyTotals = new Dictionary<ItemProperty, (double amount, int items)>();

                    foreach (var stack in ctx.player.inventoryManager.Items)
                    {
                        var item = stack.FirstItem;
                        foreach (var property in item.CraftingProperties)
                        {
                            if (!propertyTotals.ContainsKey(property))
                            {
                                propertyTotals[property] = (0, 0);
                            }

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
                    {
                        Output.WriteLine("You don't have any materials with useful properties.");
                    }
                })
                .WaitForUserInputToContinue()
                .ThenShow(ctx => [OpenCraftingMenu()])
                .Build();
        }

        private static void ShowPlayerProperties(Player player, List<CraftingPropertyRequirement> requirements)
        {
            foreach (var req in requirements)
            {
                double totalAmount = 0;
                int itemCount = 0;

                foreach (var stack in player.inventoryManager.Items)
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
    }
    public class Describe
    {
        public static IGameAction LookAround(Location location)
        {
            return CreateAction($"Look around {location.Name}")
            .Do(ctx =>
            {
                // Calculate box width (54 chars like stats display)
                const int boxWidth = 54;
                string locationName = location.Name.ToUpper();

                // Top border
                Output.WriteLine("┌" + new string('─', boxWidth - 2) + "┐");

                // Location name (centered)
                int namePadding = (boxWidth - 4 - locationName.Length) / 2;
                Output.Write("│ " + new string(' ', namePadding));
                Output.WriteColored(ConsoleColor.Cyan, locationName);
                int rightPadding = boxWidth - 4 - namePadding - locationName.Length;
                Output.WriteLine(new string(' ', rightPadding) + " │");

                // Zone • Time • Temperature
                string subheader = $"{location.Parent.Name} • {World.GetTimeOfDay()} • {location.GetTemperature():F1}°F";
                int subPadding = boxWidth - 4 - subheader.Length;
                Output.Write("│ ");
                Output.WriteColored(ConsoleColor.Gray, subheader);
                Output.WriteLine(new string(' ', subPadding) + " │");

                // Divider
                Output.WriteLine("├" + new string('─', boxWidth - 2) + "┤");

                // Content section - prioritize features (fires, shelters)
                bool hasContent = false;

                // Display LocationFeatures FIRST (most important)
                int deadFiresShown = 0;
                foreach (var feature in location.Features)
                {
                    if (feature is HeatSourceFeature heat)
                    {
                        // Show active fires with status and warmth
                        if (heat.IsActive && heat.HoursRemaining > 0)
                        {
                            string status = heat.HoursRemaining < 0.5 ? "Dying" : "Burning";
                            int minutesLeft = (int)(heat.HoursRemaining * 60);
                            string timeStr = minutesLeft >= 60 ? $"{heat.HoursRemaining:F1} hr" : $"{minutesLeft} min";
                            double effectiveHeat = heat.GetEffectiveHeatOutput();
                            string fireInfo = $"{heat.Name}: {status} ({timeStr}) +{effectiveHeat:F0}°F";

                            Output.Write("│ ");
                            ConsoleColor fireColor = heat.HoursRemaining < 0.5 ? ConsoleColor.Yellow : ConsoleColor.Red;
                            Output.WriteColored(fireColor, fireInfo);
                            Output.WriteLine(new string(' ', boxWidth - 4 - fireInfo.Length) + " │");
                            hasContent = true;
                        }
                        // Show embers with remaining time and reduced warmth
                        else if (heat.HasEmbers && heat.EmberTimeRemaining > 0)
                        {
                            int minutesLeft = (int)(heat.EmberTimeRemaining * 60);
                            string timeStr = minutesLeft >= 60 ? $"{heat.EmberTimeRemaining:F1} hr" : $"{minutesLeft} min";
                            double effectiveHeat = heat.GetEffectiveHeatOutput();
                            string fireInfo = $"{heat.Name}: Embers ({timeStr}) +{effectiveHeat:F1}°F";

                            Output.Write("│ ");
                            Output.WriteColored(ConsoleColor.DarkYellow, fireInfo);
                            Output.WriteLine(new string(' ', boxWidth - 4 - fireInfo.Length) + " │");
                            hasContent = true;
                        }
                        // Show fires with fuel but not lit
                        else if (heat.HoursRemaining > 0 && !heat.IsActive)
                        {
                            int minutesLeft = (int)(heat.HoursRemaining * 60);
                            string fireInfo = $"{heat.Name}: Cold ({minutesLeft} min fuel ready)";

                            Output.Write("│ ");
                            Output.WriteColored(ConsoleColor.DarkGray, fireInfo);
                            Output.WriteLine(new string(' ', boxWidth - 4 - fireInfo.Length) + " │");
                            hasContent = true;
                        }
                        // Show max 1 dead fire per location
                        else if (deadFiresShown == 0)
                        {
                            string fireInfo = $"{heat.Name}: Cold";

                            Output.Write("│ ");
                            Output.WriteColored(ConsoleColor.DarkGray, fireInfo);
                            Output.WriteLine(new string(' ', boxWidth - 4 - fireInfo.Length) + " │");
                            hasContent = true;
                            deadFiresShown++;
                        }
                    }
                    else if (feature is ShelterFeature shelter)
                    {
                        string shelterInfo = $"{shelter.Name} [shelter]";
                        Output.Write("│ ");
                        Output.WriteColored(ConsoleColor.Green, shelterInfo);
                        Output.WriteLine(new string(' ', boxWidth - 4 - shelterInfo.Length) + " │");
                        hasContent = true;
                    }
                    else if (feature is HarvestableFeature harvestable && harvestable.IsDiscovered)
                    {
                        string displayText = $"{harvestable.DisplayName} (harvestable)";
                        Output.Write("│ ");
                        Output.WriteColored(ConsoleColor.Yellow, displayText);
                        // Truncate if too long
                        if (displayText.Length > boxWidth - 4)
                        {
                            Output.WriteLine(" │");
                        }
                        else
                        {
                            Output.WriteLine(new string(' ', boxWidth - 4 - displayText.Length) + " │");
                        }
                        hasContent = true;
                    }
                }

                // Items, containers, NPCs
                foreach (var item in location.Items.Where(i => i.IsFound))
                {
                    string itemStr = item.ToString();
                    Output.Write("│ ");
                    Output.WriteColored(ConsoleColor.Cyan, itemStr);
                    Output.WriteLine(new string(' ', boxWidth - 4 - itemStr.Length) + " │");
                    hasContent = true;
                }

                foreach (var container in location.Containers.Where(c => c.IsFound))
                {
                    string containerStr = $"{container.Name} [container]";
                    Output.Write("│ ");
                    Output.WriteColored(ConsoleColor.Yellow, containerStr);
                    Output.WriteLine(new string(' ', boxWidth - 4 - containerStr.Length) + " │");
                    hasContent = true;
                }

                foreach (var npc in location.Npcs.Where(n => n.IsFound))
                {
                    string npcStr = $"{npc.Name} [creature]";
                    Output.Write("│ ");
                    Output.WriteColored(ConsoleColor.Red, npcStr);
                    Output.WriteLine(new string(' ', boxWidth - 4 - npcStr.Length) + " │");
                    hasContent = true;
                }

                // Empty location
                if (!hasContent)
                {
                    Output.WriteLine("│" + new string(' ', boxWidth - 2) + "│");
                }

                // Exits section
                var nearbyLocations = location.GetNearbyLocations();
                if (nearbyLocations.Count > 0)
                {
                    Output.WriteLine("│" + new string(' ', boxWidth - 2) + "│");

                    // Build exits on one line
                    string exitsLine = "Exits: ";
                    for (int i = 0; i < nearbyLocations.Count; i++)
                    {
                        exitsLine += "→ " + nearbyLocations[i].Name;
                        nearbyLocations[i].IsFound = true;
                        if (i < nearbyLocations.Count - 1) exitsLine += "  ";
                    }

                    // Wrap if too long
                    if (exitsLine.Length <= boxWidth - 4)
                    {
                        Output.Write("│ ");
                        Output.WriteColored(ConsoleColor.DarkCyan, exitsLine);
                        Output.WriteLine(new string(' ', boxWidth - 4 - exitsLine.Length) + " │");
                    }
                    else
                    {
                        // Split across multiple lines if needed
                        Output.Write("│ ");
                        Output.WriteColored(ConsoleColor.DarkCyan, "Exits:");
                        Output.WriteLine(new string(' ', boxWidth - 10) + " │");

                        foreach (var nearbyLocation in nearbyLocations)
                        {
                            string exitStr = "  → " + nearbyLocation.Name;
                            Output.Write("│ ");
                            Output.WriteColored(ConsoleColor.DarkCyan, exitStr);
                            Output.WriteLine(new string(' ', boxWidth - 4 - exitStr.Length) + " │");
                        }
                    }
                }

                // Bottom border
                Output.WriteLine("└" + new string('─', boxWidth - 2) + "┘");
                Output.WriteLine();
            })
            .ThenShow(ctx =>
            {
                var actions = new List<IGameAction>();
                foreach (Npc npc in location.Npcs)
                {
                    actions.Add(Combat.StartCombat(npc));
                    actions.Add(Inventory.LootNpc(npc));
                }
                foreach (Item item in location.Items)
                {
                    actions.Add(Inventory.PickUpItem(item));
                }
                foreach (var container in location.Containers)
                {
                    actions.Add(Inventory.OpenContainer(container));
                    actions.Add(Inventory.TryToReachContainer(container));
                }
                actions.Add(Common.Return());
                return actions;
            })
            .WaitForUserInputToContinue()
            .Build();
        }

        public static IGameAction CheckStats()
        {
            return CreateAction("Check Stats")
            .Do(ctx =>
            {
                BodyDescriber.Describe(ctx.player.Body);
                ctx.player.Skills.Describe();
            })
            .WaitForUserInputToContinue()
            .Build();
        }
    }

    public static class Hunting
    {
        /// <summary>
        /// Main hunting menu - shows available animals to hunt.
        /// </summary>
        public static IGameAction OpenHuntingMenu()
        {
            return CreateAction("Hunt")
            .When(ctx =>
            {
                // Only show if there are living animals in the location
                var animals = ctx.CurrentLocation.Npcs
                    .OfType<Animal>()
                    .Where(a => a.IsAlive && a.CurrentLocation == ctx.CurrentLocation)
                    .ToList();
                return animals.Any();
            })
            .Do(ctx => Output.WriteLine("You scan the area for prey..."))
            .ThenShow(ctx =>
            {
                var animals = ctx.CurrentLocation.Npcs
                    .OfType<Animal>()
                    .Where(a => a.IsAlive && a.CurrentLocation == ctx.CurrentLocation)
                    .ToList();

                var actions = new List<IGameAction>();

                // Add action for each animal
                foreach (var animal in animals)
                {
                    actions.Add(BeginHunt(animal));
                }

                actions.Add(Common.Return("Cancel"));
                return actions;
            })
            .Build();
        }

        /// <summary>
        /// Start hunting a specific animal.
        /// </summary>
        private static IGameAction BeginHunt(Animal animal)
        {
            return CreateAction($"Stalk {animal.Name}")
            .Do(ctx =>
            {
                ctx.player.stealthManager.StartHunting(animal);

                // Show weapon range info and warnings
                var currentDistance = animal.DistanceFromPlayer;

                if (ctx.player.inventoryManager.Weapon is RangedWeapon rangedWeapon)
                {
                    Output.WriteLine($"Distance: {currentDistance}m | Your {rangedWeapon.Name} effective range: {rangedWeapon.EffectiveRange}m (max: {rangedWeapon.MaxRange}m)");
                }
                else
                {
                    Output.WriteWarning("You have no ranged weapon equipped. You'll need to get very close to attack with melee weapons.");
                }
            })
            .ThenShow(_ => [HuntingSubMenu()])
            .Build();
        }

        /// <summary>
        /// Hunting submenu - shown while actively hunting an animal.
        /// </summary>
        private static IGameAction HuntingSubMenu()
        {
            return CreateAction("Hunting...")
            .When(ctx => ctx.player.stealthManager.IsHunting)
            .Do(ctx =>
            {
                // Display distance and shooting range information
                var target = ctx.player.stealthManager.GetCurrentTarget();
                if (target != null)
                {
                    var currentDistance = target.DistanceFromPlayer;
                    Output.WriteLine($"Distance: {currentDistance}m");
                    Output.WriteLine($"Animal state: {target.State}");
                    Output.WriteLine();

                    // Show distance until in shooting range if ranged weapon equipped
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
            })
            .ThenShow(ctx =>
            {
                if (!ctx.player.stealthManager.IsTargetValid())
                {
                    // Target is no longer valid, return to main menu
                    return new List<IGameAction> { Common.MainMenu() };
                }

                return new List<IGameAction>
                {
                    ApproachAnimal(),
                    ShootTarget(),
                    AssessTarget(),
                    StopHunting(),
                };
            })
            .Build();
        }

        /// <summary>
        /// Approach the target animal, reducing distance and checking for detection.
        /// </summary>
        private static IGameAction ApproachAnimal()
        {
            return CreateAction("Approach")
            .When(ctx => ctx.player.stealthManager.IsHunting)
            .Do(ctx =>
            {
                bool success = ctx.player.stealthManager.AttemptApproach();

                if (!success)
                {
                    // Animal detected us, StealthManager handled the response
                    // Exit hunting mode
                    return;
                }

                // Award XP for successful stealth approach
                ctx.player.Skills.GetSkill("Hunting").GainExperience(1);
            })
            .TakesMinutes(7) // Approach takes 5-10 minutes (average 7)
            .ThenShow(ctx =>
            {
                // If still hunting, return to hunting submenu
                if (ctx.player.stealthManager.IsHunting)
                {
                    return new List<IGameAction> { HuntingSubMenu() };
                }
                else
                {
                    // Detection occurred, return to main menu
                    return new List<IGameAction> { Common.MainMenu() };
                }
            })
            .Build();
        }

        /// <summary>
        /// Assess the target animal without approaching.
        /// </summary>
        private static IGameAction AssessTarget()
        {
            return CreateAction("Assess Target")
            .When(ctx => ctx.player.stealthManager.IsHunting)
            .Do(ctx => ctx.player.stealthManager.AssessTarget())
            .WaitForUserInputToContinue()
            .ThenShow(_ => [HuntingSubMenu()])
            .Build();
        }

        /// <summary>
        /// Shoot the target animal with a ranged weapon.
        /// </summary>
        private static IGameAction ShootTarget()
        {
            return CreateAction("Shoot")
            .When(ctx =>
            {
                if (!ctx.player.stealthManager.IsHunting)
                    return false;

                // Check if player has a ranged weapon and ammunition
                return ctx.player.ammunitionManager.CanShoot(out _);
            })
            .Do(ctx =>
            {
                Animal? target = ctx.player.stealthManager.GetCurrentTarget();
                if (target == null)
                {
                    Output.WriteLine("You no longer have a target.");
                    return;
                }

                // Attempt to shoot the target
                bool shotFired = ctx.player.huntingManager.ShootTarget(target);

                if (!shotFired)
                {
                    // HuntingManager already output the reason
                    return;
                }

                // Check if target is still alive and hunting session is still active
                // (HuntingManager handles stopping the session on kills or detection)
            })
            .TakesMinutes(1) // Taking a shot takes about 1 minute
            .ThenShow(ctx =>
            {
                // If still hunting, return to hunting submenu
                // Otherwise go back to main menu (combat initiated or animal dead)
                if (ctx.player.stealthManager.IsHunting)
                {
                    return new List<IGameAction> { HuntingSubMenu() };
                }
                else
                {
                    return new List<IGameAction> { Common.MainMenu() };
                }
            })
            .Build();
        }

        /// <summary>
        /// Stop hunting the current target.
        /// </summary>
        private static IGameAction StopHunting()
        {
            return CreateAction("Stop Hunting")
            .When(ctx => ctx.player.stealthManager.IsHunting)
            .Do(ctx =>
            {
                ctx.player.stealthManager.StopHunting("You give up the hunt.");
            })
            .ThenReturn()
            .Build();
        }

        /// <summary>
        /// View available blood trails in current location (Phase 4).
        /// </summary>
        public static IGameAction ViewBloodTrails()
        {
            return CreateAction("Track Blood Trail")
            .When(ctx => ctx.CurrentLocation.BloodTrails.Any())
            .Do(ctx => Output.WriteLine("You search for blood trails..."))
            .ThenShow(ctx =>
            {
                var trails = ctx.CurrentLocation.BloodTrails
                    .Where(trail => trail.GetFreshness() > 0.0) // Only show trackable trails
                    .ToList();

                var actions = new List<IGameAction>();

                foreach (var trail in trails)
                {
                    actions.Add(FollowBloodTrail(trail));
                }

                if (!actions.Any())
                {
                    Output.WriteLine("All trails have faded beyond tracking...");
                    return new List<IGameAction> { Common.Return("Back") };
                }

                actions.Add(Common.Return("Cancel"));
                return actions;
            })
            .Build();
        }

        /// <summary>
        /// Attempt to follow a specific blood trail (Phase 4).
        /// </summary>
        private static IGameAction FollowBloodTrail(BloodTrail trail)
        {
            return CreateAction($"Follow {trail.GetTrailDescription()}")
            .Do(ctx =>
            {
                int huntingSkill = ctx.player.Skills.GetSkill("Hunting").Level;
                double trackingChance = trail.GetTrackingSuccessChance(huntingSkill);

                Output.WriteLine($"\n{trail.GetTrailDescription()}");
                Output.WriteLine(trail.GetSeverityDescription());
                Output.WriteLine($"\nTracking success chance: {trackingChance * 100:F0}%");
                Thread.Sleep(500);

                double trackingRoll = Utils.RandDouble(0, 1);
                bool success = trackingRoll < trackingChance;

                if (success)
                {
                    Output.WriteLine($"\nYou successfully follow the trail!");
                    Output.WriteLine($"(Rolled {trackingRoll * 100:F0}% vs {trackingChance * 100:F0}%)");

                    // Award XP for successful tracking
                    ctx.player.Skills.GetSkill("Hunting").GainExperience(3);
                    Output.WriteLine("You gain 3 Hunting XP.");

                    trail.IsTracked = true;

                    // Check if animal should have bled out
                    bool bledOut = CheckBleedOut(trail.SourceAnimal);

                    // Check if animal is still alive
                    if (trail.SourceAnimal.IsAlive && !bledOut)
                    {
                        Output.WriteLine($"\nYou find the wounded {trail.SourceAnimal.Name}!");
                        // Re-add animal to location so player can engage
                        trail.SourceAnimal.CurrentLocation = ctx.CurrentLocation;
                        Output.WriteLine("The animal is too weak to flee. You can approach or shoot it.");
                    }
                    else
                    {
                        // Animal died from wounds
                        if (!trail.SourceAnimal.IsAlive || bledOut)
                        {
                            if (bledOut && trail.SourceAnimal.IsAlive)
                            {
                                // Kill the animal from bleed-out
                                var bleedDamage = new DamageInfo(trail.SourceAnimal.Body.Health, DamageType.Bleed, "Blood loss");
                                trail.SourceAnimal.Body.Damage(bleedDamage);
                            }

                            Output.WriteLine($"\nYou find the {trail.SourceAnimal.Name}'s corpse.");
                            Output.WriteLine("It bled out from its wounds.");
                            // Re-add corpse to location for butchering
                            trail.SourceAnimal.CurrentLocation = ctx.CurrentLocation;
                        }
                    }
                }
                else
                {
                    Output.WriteLine($"\nYou lose the trail...");
                    Output.WriteLine($"(Rolled {trackingRoll * 100:F0}% vs {trackingChance * 100:F0}%)");

                    // Award small XP for attempt
                    ctx.player.Skills.GetSkill("Hunting").GainExperience(1);
                }
            })
            .TakesMinutes(15) // Tracking takes 15 minutes
            .WaitForUserInputToContinue()
            .ThenReturn()
            .Build();
        }

        /// <summary>
        /// Checks if a wounded animal has bled out over time (Phase 4).
        /// </summary>
        private static bool CheckBleedOut(Animal animal)
        {
            if (!animal.IsBleeding || animal.WoundedTime == null)
                return false;

            TimeSpan elapsed = World.GameTime - animal.WoundedTime.Value;
            double hoursElapsed = elapsed.TotalHours;

            // Bleed-out time depends on wound severity
            // Critical wounds (0.7+): 1-2 hours
            // Moderate wounds (0.4-0.7): 2-4 hours
            // Minor wounds (< 0.4): 4-6 hours
            double bleedOutTime = animal.CurrentWoundSeverity switch
            {
                >= 0.7 => 1.5,  // Critical: 1.5 hours
                >= 0.4 => 3.0,  // Moderate: 3 hours
                _ => 5.0        // Minor: 5 hours
            };

            return hoursElapsed >= bleedOutTime;
        }
    }

}