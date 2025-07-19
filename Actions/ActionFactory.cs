using text_survival.Actors;
using text_survival.Bodies;
using text_survival.Crafting;
using text_survival.Environments;
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
                   .Do(ctx => BodyDescriber.DescribeSurvivalStats(ctx.player.Body.BundleSurvivalData(), ctx.player.GetSurvivalContext()))
                   .ThenShow(ctx => [
                        Describe.LookAround(ctx.currentLocation),
                        Survival.Forage(),
                        Inventory.OpenInventory(),
                        Crafting.OpenCraftingMenu(),
                        Describe.CheckStats(),
                        Survival.Sleep(),
                        Movement.Move(),
                   ])
                   .Build();
        }
    }

    public static class Survival
    {
        public static IGameAction Forage(string name = "Forage")
        {
            return CreateAction("Forage")
                .When(ctx => ctx.currentLocation.GetFeature<ForageFeature>() != null)
                .ShowMessage("You forage for 1 hour")
                .Do(ctx =>
                {
                    var forageFeature = ctx.currentLocation.GetFeature<ForageFeature>()!;
                    forageFeature.Forage(1);
                })
                .AndGainExperience("Foraging")
                .ThenShow(_ => [Forage("Keep foraging"), Common.Return("Finish foraging")])
                .Build();
        }

        public static IGameAction Sleep()
        {
            return CreateAction("Sleep")
            .When(ctx => ctx.player.Body.IsTired)
            .Do(ctx =>
            {
                Output.WriteLine("How many hours would you like to sleep?");
                ctx.player.Body.Rest(Input.ReadInt() * 60);
            })
            .ThenReturn()
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

        public static IGameAction Travel()
        {
            return CreateAction("Travel to a different area")
            .When(ctx => ctx.currentLocation.GetFeature<ShelterFeature>() == null) // when not inside a shelter
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
                var locations = ctx.currentLocation.GetNearbyLocations().Where(l => l.IsFound).ToList();
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
                foreach (var location in ctx.currentLocation.GetNearbyLocations())
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
            return CreateAction($"Inspect {item}")
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
                 .Do(ctx => Output.WriteLine("You couldn't get past the ", CombatUtils.GetFastestHostileNpc(ctx.currentLocation)!, "!"))
                 .ThenShow(ctx => [Combat.StartCombat(CombatUtils.GetFastestHostileNpc(ctx.currentLocation)!)])
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
            ConsoleColor oldColor = Console.ForegroundColor;
            // Player status
            Console.ForegroundColor = GetHealthColor(player.Body.Health / player.Body.MaxHealth);
            Output.WriteLine($"You: {Math.Round(player.Body.Health, 0)}/{Math.Round(player.Body.MaxHealth, 0)} HP");
            // Enemy status
            Console.ForegroundColor = GetHealthColor(enemy.Body.Health / enemy.Body.MaxHealth);
            Output.WriteLine($"{enemy.Name}: {Math.Round(enemy.Body.Health, 0)}/{Math.Round(enemy.Body.MaxHealth, 0)} HP");
            Console.ForegroundColor = oldColor;
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

                    Output.WriteLine("\nDo you want to attempt this craft?");

                    if (Input.ReadYesNo())
                    {
                        ctx.CraftingManager.Craft(recipe);
                    }
                })
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
                // Header with location info
                Output.WriteSuccess($"\t>>> {location.Name.ToUpper()} <<<");
                Output.WriteLine("(", location.Parent, " • ", World.GetTimeOfDay(), " • ", location.GetTemperature(), "°F)");
                Output.WriteLine("\nYou see:");

                // Items and objects found here
                bool hasItems = false;

                foreach (var item in location.Items)
                {
                    Output.WriteLine("\t", item);
                    item.IsFound = true;
                    hasItems = true;
                }

                foreach (var container in location.Containers)
                {
                    Output.WriteLine("\t", container, " [container]");
                    container.IsFound = true;
                    hasItems = true;
                }

                foreach (var npc in location.Npcs)
                {
                    Output.WriteLine("\t", npc, " [creature]");
                    npc.IsFound = true;
                    hasItems = true;
                }

                if (!hasItems)
                {
                    Output.WriteLine("Nothing...");
                }

                // Add spacing before exits if there were items
                var nearbyLocations = location.GetNearbyLocations();
                if (hasItems && nearbyLocations.Count > 0)
                {
                    Output.WriteLine();
                }

                // Exits
                if (nearbyLocations.Count > 0)
                {
                    Output.WriteLine("Nearby Places:");
                }
                foreach (var nearbyLocation in nearbyLocations)
                {
                    Output.WriteLine("\t→ ", nearbyLocation);
                    nearbyLocation.IsFound = true;
                }
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


}