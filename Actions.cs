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
        // private Command<Player> LevelUpCommand => new Command<Player>("Level Up", LevelUp);
        //private Command<Player, IInteractable> InteractCommand => new Command<Player, IInteractable>("Interact", Interact);
        private Command<Player> OpenInventoryCommand => new Command<Player>("Open Inventory", OpenInventory);
        private Command<Player> TravelCommand => new Command<Player>("Travel", Travel);
        private Command<Player> SleepCommand => new Command<Player>("Sleep", Sleep);
        private Command<Player> CheckGearCommand => new Command<Player>("Check Gear", CheckGear);
        private Command<Player> ForageCommand => new Command<Player>("Forage", Forage);
        //private Command<Player> QuitCommand => new Command<Player>("Quit", Quit);
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


            if (_player.CurrentLocation.Parent is not null)
            {
                var leaveCommand = _player.LeaveCommand;
                leaveCommand.Player = _player;
                AvailableActions.Add(leaveCommand);
            }

            var travelCommand = TravelCommand;
            travelCommand.Player = _player;
            AvailableActions.Add(travelCommand);


            var sleepCommand = SleepCommand;
            sleepCommand.Player = _player;
            AvailableActions.Add(sleepCommand);


            if (_player.IsArmored || _player.IsArmed)
            {
                var checkGearCommand = CheckGearCommand;
                checkGearCommand.Player = _player;
                AvailableActions.Add(checkGearCommand);
            }

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

        private void Forage(Player player)
        {
            var forageFeature = player.CurrentLocation.GetFeature<ForageFeature>();
            if (forageFeature == null)
            {
                Output.WriteLine("You can't forage here");
                return;
            }
            Output.WriteLine("How many hours would you like to forage?");
            int hours = Input.ReadInt();
            forageFeature.Forage(hours);


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

        // private void LevelUp(Player player)
        // {
        //     Output.WriteLine("You have ", player.SkillPoints, " points.");
        //     while (player.SkillPoints > 0)
        //     {
        //         Output.WriteLine("Select an attribute to improve:");
        //         Output.WriteLine("1. ", Attributes.PrimaryAttributes.Strength);
        //         Output.WriteLine("3. ", Attributes.PrimaryAttributes.Speed);
        //         Output.WriteLine("4. ", Attributes.PrimaryAttributes.Endurance);
        //         Output.WriteLine("6. ", Attributes.PrimaryAttributes.Luck);

        //         Output.WriteLine("0. Cancel");
        //         int input = Input.ReadInt(0, 8);
        //         if (input == 0) return;
        //         var attribute = input switch
        //         {
        //             1 => Attributes.PrimaryAttributes.Strength,
        //             3 => Attributes.PrimaryAttributes.Speed,
        //             4 => Attributes.PrimaryAttributes.Endurance,
        //             6 => Attributes.PrimaryAttributes.Luck,
        //             _ => throw new NotImplementedException(),
        //         };
        //         // player.SpendPointToUpgradeAttribute(attribute);
        //     }
        // }

        private void CheckStats(Player player)
        {
            // Describe.DescribeLevel(player);
            Describe.DescribePrimaryAttributes(player);
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
            // player.CheckGear();
            //todo
        }

        private void LookAround(Player player)
        {
            Output.WriteLine("You look around the ", player.CurrentLocation);
            Output.WriteLine("You are in a ", player.CurrentLocation, " in a ", player.CurrentZone);
            Output.WriteLine("Its ", World.GetTimeOfDay(), " and ", player.CurrentLocation.GetTemperature(), " degrees.");
            // if (player.CurrentLocation.Things.Count == 0)
            // {
            //     Output.WriteLine("You see nothing of interest, time to move on.");
            //     return;
            // }
            // Output.WriteLine("You see:");
            // foreach (var thing in player.CurrentLocation.Things)
            // {
            //     Output.WriteLine(thing);
            //     thing.IsFound = true;
            // }

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
