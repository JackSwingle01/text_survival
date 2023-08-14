using text_survival.Actors;

using text_survival.Environments;
using text_survival.Items;
using static text_survival.Level.Attributes;

namespace text_survival
{
    public enum ActionType
    {
        Fight,
        Interact,
        OpenInventory,
        Travel,
        Sleep,
        CheckStats,
        CheckGear,
        LookAround,
        Quit,
        LevelUp,
    }

 
    public class Actions
    {
        private readonly Player _player;
        //private readonly Dictionary<ActionType, Action> _actionDict;

        public List<ICommand> AvailableActions { get; private set; }

        public Actions(Player player)
        {
            this._player = player;
            this.AvailableActions = new List<ICommand>();
        }

        private Command<Player> LookAroundCommand => new Command<Player>("Look Around", LookAround);
        private Command<Player> CheckStatsCommand => new Command<Player>("Check Stats", CheckStats);
        private Command<Player> LevelUpCommand => new Command<Player>("Level Up", LevelUp);
        private Command<Player, ICombatant> FightCommand => new Command<Player, ICombatant>("Fight", Fight);
        private Command<Player, IInteractable> InteractCommand => new Command<Player, IInteractable>("Interact", Interact);
        private Command<Player> OpenInventoryCommand => new Command<Player>("Open Inventory", OpenInventory);
        private Command<Player> TravelCommand => new Command<Player>("Travel", Travel);
        private Command<Player> SleepCommand => new Command<Player>("Sleep", Sleep);
        private Command<Player> CheckGearCommand => new Command<Player>("Check Gear", CheckGear);
        private Command<Player> QuitCommand => new Command<Player>("Quit", Quit);



        public void UpdatePossibleActions()
        {
            // Clear the available actions
            AvailableActions.Clear();

            // always available actions
            var lookCommand = LookAroundCommand;
            lookCommand.Player = _player;
            AvailableActions.Add(lookCommand);
            
            var checkStats = CheckStatsCommand;
            checkStats.Player = _player;
            AvailableActions.Add(checkStats);


            // conditional actions
            foreach (var npc in _player.CurrentArea.Npcs)
            {
                var command = FightCommand;
                command.Name = "Fight " + npc.Name;
                command.Player = _player;
                command.Arg = npc;
                AvailableActions.Add(command);
            }
            
            foreach (var thing in _player.CurrentArea.Things)
            {
                var command = InteractCommand;
                command.Name = "Check out " + thing.Name;
                command.Player = _player;
                command.Arg = thing;
                AvailableActions.Add(command);
            }

            if (_player.InventoryCount > 0)
            {
                var openInventoryCommand = OpenInventoryCommand;
                openInventoryCommand.Player = _player;
                AvailableActions.Add(openInventoryCommand);
            }

            var travelCommand = TravelCommand;
            travelCommand.Player = _player;
            AvailableActions.Add(travelCommand);

            if (_player.ExhaustionPercent > 0)
            {
                var sleepCommand = SleepCommand;
                sleepCommand.Player = _player;
                AvailableActions.Add(sleepCommand);
            }

            if (_player.Armor.Count > 0 || _player.IsArmed)
            {
                var checkGearCommand = CheckGearCommand;
                checkGearCommand.Player = _player;
                AvailableActions.Add(checkGearCommand);
            }

            // last action
            //AvailableActions.Add(ActionType.Quit);
        }

        public void Act()
        {
            UpdatePossibleActions();
            Output.WriteLine();
            Examine.ExamineSurvivalStats(_player);
            Output.WriteLine();
            Output.WriteLine("What would you like to do?");
            List<string> actionNames = AvailableActions.Select(action => action.Name).ToList();
            int input = Input.GetSelectionFromList(actionNames);
            var command = AvailableActions[input-1 ];
            command.Execute();
        }

        private void LevelUp(Player player)
        {
            Output.WriteLine("You have ", player.SkillPoints, " points.");
            while (player.SkillPoints > 0)
            {
                Output.WriteLine("Select an attribute to improve:");
                Output.WriteLine("1. ", PrimaryAttributes.Strength);
                Output.WriteLine("2. ", PrimaryAttributes.Intelligence);
                Output.WriteLine("3. ", PrimaryAttributes.Speed);
                Output.WriteLine("4. ", PrimaryAttributes.Endurance);
                Output.WriteLine("5. ", PrimaryAttributes.Agility);
                Output.WriteLine("6. ", PrimaryAttributes.Luck);
                Output.WriteLine("7. ", PrimaryAttributes.Willpower);
                Output.WriteLine("8. ", PrimaryAttributes.Personality);
                Output.WriteLine("0. Cancel");
                int input = Input.ReadInt(0, 8);
                if (input == 0) return;
                var attribute = input switch
                {
                    1 => PrimaryAttributes.Strength,
                    2 => PrimaryAttributes.Intelligence,
                    3 => PrimaryAttributes.Speed,
                    4 => PrimaryAttributes.Endurance,
                    5 => PrimaryAttributes.Agility,
                    6 => PrimaryAttributes.Luck,
                    7 => PrimaryAttributes.Willpower,
                    8 => PrimaryAttributes.Personality,
                    _ => throw new ArgumentOutOfRangeException()
                };
                player.SpendPointToUpgradeAttribute(attribute);
            }
        }

        private void CheckStats(Player player)
        {
            Examine.ExamineLevel(player);
            Examine.ExaminePrimaryAttributes(player);
            Examine.ExamineSecondaryAttributes(player);
            Examine.ExamineSkills(player);
            Output.WriteLine("Press any key to continue...");
            Console.ReadKey(true);
        }

        private void Fight(Player player, ICombatant enemy)
        {
            Combat.CombatLoop(player, enemy);
        }

        private void Interact(Player player, IInteractable thing)
        {
            if (player.CurrentArea.Npcs.Count > 0)
            {
                // compare player to fastest enemy
                double playerCheck = player.Attributes.Speed + player.Attributes.Agility / 2 + player.Attributes.Luck / 3;
                double enemyCheck = 0;
                Npc fastestNpc = player.CurrentArea.Npcs.First();
                foreach (Npc npc in player.CurrentArea.Npcs)
                {
                    var currentNpcCheck = npc.Attributes.Speed + npc.Attributes.Agility / 2 + npc.Attributes.Luck / 3;
                    if (!(currentNpcCheck >= enemyCheck)) continue;
                    fastestNpc = npc;
                    enemyCheck = currentNpcCheck;
                }

                if (playerCheck < enemyCheck)
                {
                    Output.WriteLine("You weren't fast enough to get past the ", fastestNpc.Name, "!");
                    Fight(player, fastestNpc);
                    return;
                }
            }
            thing.Interact(player);
        }

        private void OpenInventory(Player player)
        {
            player.OpenInventory();
        }

        private void Travel(Player player)
        {
            Output.WriteLine("Where would you like to go?");
            List<Area> options = new();

            // find all nearby areas that are not the current area
            options.AddRange(player
                .CurrentArea
                .NearbyAreas);

            options.ForEach(opt =>
            {
                Output.Write(options.IndexOf(opt) + 1, ". ", opt); // "1. Name"
                if (opt.Visited)
                    Output.Write(" (Visited)");
                Output.WriteLine();
            });

            Output.WriteLine("0. Cancel");
            int input = Input.ReadInt(0, options.Count);

            if (input == 0) return;

            int minutes = Utils.RandInt(30, 60);
            Output.WriteLine("You travel for ", minutes, " minutes...");
            World.Update(minutes);
            player.Enter(options[input - 1]);
        }


        private void Sleep(Player player)
        {
            Output.WriteLine("How many hours would you like to sleep?");
            player.Sleep(Input.ReadInt() * 60);
        }

        private void CheckGear(Player player)
        {
            player.CheckGear();
        }

        private void LookAround(Player player)
        {
            Output.WriteLine("You take in your surroundings");
            Output.WriteLine("You're in a ", player.CurrentArea, ", ", player.CurrentArea.Description);
            Output.WriteLine("Its ", World.GetTimeOfDay(), " and ", player.CurrentArea.GetTemperature(), " degrees.");
            if (player.CurrentArea.Npcs.Count == 0 && player.CurrentArea.Things.Count == 0)
            {
                Output.WriteLine("You see nothing of interest, time to move on.");
                return;
            }

            Output.WriteLine("You see the following things:");
            foreach (var thing in player.CurrentArea.Things)
            {
                Output.WriteLine(thing);
            }
            foreach (var npc in player.CurrentArea.Npcs)
            {
                Output.WriteLine(npc);
            }
        }
        private void Quit(Player player)
        {
            player.Damage(999);
        }
    }


}
