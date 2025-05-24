using text_survival.Actors;
using text_survival.Environments;
using text_survival.IO;
using text_survival.Items;

namespace text_survival
{

    public class Actions
    {
        private readonly Player _player;
        public List<ICommand> AvailableActions { get; private set; }

        public Actions(Player player)
        {
            _player = player;
            AvailableActions = [];
        }

        private Command<Player> LookAroundCommand => new Command<Player>($"Look Around {_player.CurrentLocation}", LookAround);
        private Command<Player> CheckStatsCommand => new Command<Player>("Check Stats", CheckStats);
        private Command<Player> OpenInventoryCommand => new Command<Player>("Open Inventory", OpenInventory);
        private Command<Player> TravelCommand => new Command<Player>("Travel", Travel);
        private Command<Player> SleepCommand => new Command<Player>("Sleep", Sleep);
        private Command<Player> CheckGearCommand => new Command<Player>("Check Gear", CheckGear);
        private Command<Player> ForageCommand => new Command<Player>("Forage", Forage);
        public void UpdatePossibleActions()
        {
            // Clear the available actions
            AvailableActions.Clear();

            // always available actions
            var lookCommand = LookAroundCommand;
            lookCommand.Player = _player;
            AvailableActions.Add(lookCommand);

            var forageFeature = _player.CurrentLocation.GetFeature<ForageFeature>();
            if (forageFeature != null)
            {
                var forageCommand = ForageCommand;
                forageCommand.Player = _player;
                AvailableActions.Add(forageCommand);
            }


            // conditional actions
            foreach (Item item in _player.CurrentLocation.Items)
            {
                if (item.IsFound)
                {
                    var interactCommand = item.InteractCommand;
                    interactCommand.Player = _player;
                    AvailableActions.Add(interactCommand);
                }
            }
            foreach (Container container in _player.CurrentLocation.Containers)
            {
                if (container.IsFound)
                {
                    var interactCommand = container.InteractCommand;
                    interactCommand.Player = _player;
                    AvailableActions.Add(interactCommand);
                }
            }
            foreach (Npc npc in _player.CurrentLocation.Npcs)
            {
                if (npc.IsFound)
                {
                    var interactCommand = npc.InteractCommand;
                    interactCommand.Player = _player;
                    AvailableActions.Add(interactCommand);
                }
            }
            foreach (Location location in GetNearbyLocations(_player))
            {
                if (location.IsFound)
                {
                    var interactCommand = location.InteractCommand;
                    interactCommand.Player = _player;
                    AvailableActions.Add(interactCommand);
                }
            }

            var openInventoryCommand = OpenInventoryCommand;
            openInventoryCommand.Player = _player;
            AvailableActions.Add(openInventoryCommand);

            var travelCommand = TravelCommand;
            travelCommand.Player = _player;
            AvailableActions.Add(travelCommand);


            var sleepCommand = SleepCommand;
            sleepCommand.Player = _player;
            AvailableActions.Add(sleepCommand);

            // var checkGearCommand = CheckGearCommand;
            // checkGearCommand.Player = _player;
            // AvailableActions.Add(checkGearCommand);

            var checkStats = CheckStatsCommand;
            checkStats.Player = _player;
            AvailableActions.Add(checkStats);

            // if (_player.SkillPoints > 0)
            // {
            //     var levelUpCommand = LevelUpCommand;
            //     levelUpCommand.Player = _player;
            //     AvailableActions.Add(levelUpCommand);
            // }
        }

        public void DisplayActions()
        {
            int i = 1;
            Dictionary<int, ICommand> actions = new();

            // always available actions
            var lookCommand = LookAroundCommand;
            lookCommand.Player = _player;
            actions[i++] = lookCommand;

            var forageFeature = _player.CurrentLocation.GetFeature<ForageFeature>();
            if (forageFeature != null)
            {
                var forageCommand = ForageCommand;
                forageCommand.Player = _player;
                actions[i++] = forageCommand;
            }


            // conditional actions
            foreach (Item item in _player.CurrentLocation.Items)
            {
                if (item.IsFound)
                {
                    var interactCommand = item.InteractCommand;
                    interactCommand.Player = _player;
                    actions[i++] = interactCommand;
                }
            }
            foreach (Container container in _player.CurrentLocation.Containers)
            {
                if (container.IsFound)
                {
                    var interactCommand = container.InteractCommand;
                    interactCommand.Player = _player;
                    actions[i++] = interactCommand;
                    
                }
            }
            foreach (Npc npc in _player.CurrentLocation.Npcs)
            {
                if (npc.IsFound)
                {
                    var interactCommand = npc.InteractCommand;
                    interactCommand.Player = _player;
                    actions[i++] = interactCommand;
                }
            }
            foreach (Location location in GetNearbyLocations(_player))
            {
                if (location.IsFound)
                {
                    var interactCommand = location.InteractCommand;
                    interactCommand.Player = _player;
                    actions[i++] = interactCommand;
                }
            }

            var openInventoryCommand = OpenInventoryCommand;
            openInventoryCommand.Player = _player;
            actions[i++] = openInventoryCommand;

            var travelCommand = TravelCommand;
            travelCommand.Player = _player;
            actions[i++] = travelCommand;

            var sleepCommand = SleepCommand;
            sleepCommand.Player = _player;
            actions[i++] = sleepCommand;

            // var checkGearCommand = CheckGearCommand;
            // checkGearCommand.Player = _player;
            // AvailableActions.Add(checkGearCommand);

            var checkStats = CheckStatsCommand;
            checkStats.Player = _player;
            actions[i++] = checkStats;

            Output.WriteLine();
            _player.DescribeSurvivalStats();
            Output.WriteLine();
            Output.WriteLine("What would you like to do?");
        }

        private void Forage(Player player)
        {
            var forageFeature = player.CurrentLocation.GetFeature<ForageFeature>();
            if (forageFeature == null)
            {
                Output.WriteLine("You can't forage here");
                return;
            }

            // Output.WriteLine("How many hours would you like to forage?");
            // int hours = Input.ReadInt(); 
            Output.WriteLine("You forage for 1 hour");
            forageFeature.Forage(1);
        }

        public void Act()
        {
            UpdatePossibleActions();
            Output.WriteLine();
            _player.DescribeSurvivalStats();
            Output.WriteLine();
            Output.WriteLine("What would you like to do?");
            ICommand? command = Input.GetSelectionFromList(AvailableActions);
            command?.Execute();
        }

        private void CheckStats(Player player)
        {
            // Describe.DescribeLevel(player);
            player.Body.Describe();
            Describe.DescribeSkills(player);
            Output.WriteLine("Press any key to continue...");
            Console.ReadKey(true);
        }

        private void OpenInventory(Player player)
        {
            player.OpenInventory();
        }

        private void Travel(Player player)
        {
            player.Travel();
        }
        private void Sleep(Player player)
        {
            Output.WriteLine("How many hours would you like to sleep?");
            player.Sleep(Input.ReadInt() * 60);
        }

        private void CheckGear(Player player)
        {
            // player.in();
            //todo
        }

        private void LookAround(Player player)
        {
            Output.WriteLine("You look around the ", player.CurrentLocation);
            Output.WriteLine("You are in a ", player.CurrentLocation, " in a ", player.CurrentZone);
            Output.WriteLine("Its ", World.GetTimeOfDay(), " and ", player.CurrentLocation.GetTemperature(), " degrees.");
            Output.WriteLine("You see:");
            foreach (var thing in player.CurrentLocation.Items)
            {
                Output.WriteLine(thing);
                thing.IsFound = true;
            }
            foreach (var thing in player.CurrentLocation.Containers)
            {
                Output.WriteLine(thing);
                thing.IsFound = true;
            }
            foreach (var thing in player.CurrentLocation.Npcs)
            {
                Output.WriteLine(thing);
                thing.IsFound = true;
            }


            var nearbyLocations = GetNearbyLocations(player);
            if (nearbyLocations.Count == 0)
                return;
            Output.WriteLine("Nearby, you see some other places: ");
            foreach (var location in nearbyLocations)
            {
                Output.WriteLine(location);
                location.IsFound = true;
            }
        }
        private List<Location> GetNearbyLocations(Player player)
        {
            List<Location> nearbyLocations = [];
            if (player.CurrentZone.Locations.Count > 0)
            {
                foreach (var location in player.CurrentZone.Locations)
                {
                    if (location == player.CurrentLocation)
                        continue;
                    nearbyLocations.Add(location);
                }
            }
            return nearbyLocations;
        }
    }
}
