using text_survival.Actors;
using text_survival.Environments;
using text_survival.IO;
using text_survival.Items;

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
                   .Do(ctx => ctx.player.DescribeSurvivalStats())
                   .ThenShow(ctx => [
                        Describe.LookAround(ctx.currentLocation),
                        Survival.Forage(),
                        Inventory.OpenInventory(),
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
                ctx.player.Sleep(Input.ReadInt() * 60);
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
            .When(ctx => ctx.player.Body.CalculateVitality() > .2)
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
                 .ThenShow(ctx => [Combat.StartFight(CombatUtils.GetFastestHostileNpc(ctx.currentLocation)!)])
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


    }

    public class Describe
    {
        public static IGameAction LookAround(Location location)
        {
            return CreateAction($"Look around {location.Name}")
            .Do(ctx =>
            {
                Output.WriteLine("You look around the ", location);
                Output.WriteLine("You are in a ", location, " in a ", location.Parent);
                Output.WriteLine("Its ", World.GetTimeOfDay(), " and ", location.GetTemperature(), " degrees.");
                Output.WriteLine("You see:");
                foreach (var thing in location.Items)
                {
                    Output.WriteLine(thing);
                    thing.IsFound = true;
                }
                foreach (var thing in location.Containers)
                {
                    Output.WriteLine(thing);
                    thing.IsFound = true;
                }
                foreach (var thing in location.Npcs)
                {
                    Output.WriteLine(thing);
                    thing.IsFound = true;
                }

                var nearbyLocations = location.GetNearbyLocations();
                if (nearbyLocations.Count == 0)
                    return;
                Output.WriteLine("Nearby, you see some other places: ");
                foreach (var location in nearbyLocations)
                {
                    Output.WriteLine(location);
                    location.IsFound = true;
                }
            })
            .ThenShow(ctx =>
            {
                var actions = new List<IGameAction>();
                foreach (Npc npc in location.Npcs)
                {
                    actions.Add(new StartCombatAction(npc));
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
                ctx.player.Body.Describe();
                ctx.player.Skills.Describe();
            })
            .WaitForUserInputToContinue()
            .Build();
        }
    }


}