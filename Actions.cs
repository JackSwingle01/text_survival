using text_survival.Actors;

using text_survival.Environments;
using text_survival.Items;

namespace text_survival
{
    public enum ActionType
    {
        Fight,
        PickUpItem,
        OpenInventory,
        Travel,
        Sleep,
        CheckStats,
        CheckGear,
        LookAround,
        Quit,
        Help,

        // Add more actions as necessary
    }

    public class Actions
    {
        private Player _player;
        private Dictionary<ActionType, Action> _actionDict;

        public List<ActionType> AvailableActions { get; private set; }

        public Actions(Player player)
        {
            this._player = player;
            this.AvailableActions = new List<ActionType>();
            this._actionDict = new Dictionary<ActionType, Action>()
            {
                { ActionType.Fight, this.Fight },
                { ActionType.OpenInventory, this.OpenInventory },
                { ActionType.Travel , this.Travel },
                { ActionType.Sleep, this.Sleep },
                { ActionType.CheckGear, this.CheckGear },
                { ActionType.Quit, this.Quit },
                { ActionType.PickUpItem , this.PickUpItem },
                { ActionType.LookAround, this.LookAround },
                { ActionType.CheckStats, this.CheckStats },
                { ActionType.Help, this.Help },

                // Add more actions as necessary
            };
        }

        public Dictionary<string, ActionType> InputToActionTypes = new Dictionary<string, ActionType>()
        {
            { "inventory", ActionType.OpenInventory },
            { "inv", ActionType.OpenInventory },
            { "i", ActionType.OpenInventory },
            { "travel", ActionType.Travel },
            { "t", ActionType.Travel },
            { "sleep", ActionType.Sleep },
            { "s", ActionType.Sleep },
            { "check gear", ActionType.CheckGear },
            { "gear", ActionType.CheckGear },
            { "g", ActionType.CheckGear },
            { "check stats", ActionType.CheckStats },
            { "stats", ActionType.CheckStats },
            { "stat", ActionType.CheckStats },
            { "look around", ActionType.LookAround },
            { "look", ActionType.LookAround },
            { "l", ActionType.LookAround },
            { "quit", ActionType.Quit },
            { "q", ActionType.Quit },
            { "pick up item", ActionType.PickUpItem },
            { "pick up", ActionType.PickUpItem },
            { "pickup", ActionType.PickUpItem },
            { "pick", ActionType.PickUpItem },
            { "p", ActionType.PickUpItem },
            { "fight", ActionType.Fight },
            { "f", ActionType.Fight },
            { "help", ActionType.Help },
            { "h", ActionType.Help },
            { "?", ActionType.Help }
        };

        public void UpdatePossibleActions()
        {
            // Clear the available actions
            AvailableActions.Clear();

            // always available actions
            //AvailableActions.Add(ActionType.Help);
            AvailableActions.Add(ActionType.LookAround);
            AvailableActions.Add(ActionType.CheckStats);


            // conditional actions
            if (_player.CurrentArea.Npcs.Count > 0)
            {
                AvailableActions.Add(ActionType.Fight);
            }
            if (_player.CurrentArea.Items.Count > 0)
            {
                AvailableActions.Add(ActionType.PickUpItem);
            }
            if (_player.Inventory.Count() > 0)
                AvailableActions.Add(ActionType.OpenInventory);
            AvailableActions.Add(ActionType.Travel);
            if (_player.Exhaustion.Amount > 0)
                AvailableActions.Add(ActionType.Sleep);
            if (_player.Gear.Count > 0)
                AvailableActions.Add(ActionType.CheckGear);

            // last action
            AvailableActions.Add(ActionType.Quit);
        }

        public void Act()
        {
            UpdatePossibleActions();
            //CheckStats();
            Utils.WriteLine("What would you like to do?");
            int input = Utils.GetSelectionFromList(AvailableActions);
            ActionType actionType = AvailableActions[input - 1];

            if (AvailableActions.Contains(actionType))
            {
                _actionDict[actionType].Invoke();
                _player.Update(1);
            }
            else
            {
                Utils.WriteLine("You can't do that right now.");
            }
        }

        private ActionType? GetActionsFreeform()
        {
            // Try to get the ActionType from the input
            string input = Utils.Read();
            if (!InputToActionTypes.TryGetValue(input, out var actionType))
            {
                // If the input is not a valid action, return ActionType.None
                return null;
            }
            // Return the ActionType
            return actionType;
        }

        private void Help()
        {
            Utils.WriteLine("Help:");
            Utils.WriteLine("Type the name of the action you want to perform.");
            Utils.WriteLine("Common actions include: look, fight, pick up, travel, sleep, etc... you can also just type the first letter of an action.");
            Utils.WriteLine("Available actions:");
            PrintActionsAndInputs();

        }
        private void PrintActionsAndInputs()
        {
            var groups = InputToActionTypes.GroupBy(x => x.Value)
                .OrderBy(x => x.Key);

            foreach (var group in groups)
            {
                var action = group.Key;
                var inputs = string.Join(", ", group.Select(x => $"'{x.Key}'").ToArray());

                Console.WriteLine($"{action} => {inputs}");
            }
        }
        public void PrintActionsAndInputs(List<ActionType> actionTypesToPrint)
        {
            // Group by ActionType
            var groups = InputToActionTypes.GroupBy(kv => kv.Value);

            foreach (var group in groups)
            {
                // Check if this ActionType should be printed
                if (!actionTypesToPrint.Contains(group.Key)) continue;
                // Use group.Key for ActionType
                Console.Write($"{group.Key} => ");

                // Get a list of inputs for this ActionType
                var inputs = group.Select(kv => $"'{kv.Key}'").ToList();

                // Use string.Join to concatenate inputs into a string
                Console.WriteLine(string.Join(", ", inputs));
            }
        }

        private void CheckStats()
        {
            Examine.ExamineSurvivalStats(_player);
            Examine.ExamineCombatStats(_player);
        }

        private void Fight()
        {
            Npc enemy = _player.CurrentArea.Npcs.First();
            Combat.CombatLoop(_player, enemy);
        }

        private void PickUpItem()
        {
            if (_player.CurrentArea.Npcs.Count > 0)
            {
                Utils.WriteLine("Its too dangerous with enemies nearby!");
                return;
            }
            var items = _player.CurrentArea.Items;
            Item item = items.First();
            if (items.Count > 1)
            {
                Utils.WriteLine("Which item would you like to pick up?");
                int input = Utils.GetSelectionFromList(items, cancelOption: true);
                if (input == 0)
                    return;
                item = items[input - 1];

            }
            _player.Inventory.Add(item);
        }

        private void OpenInventory()
        {
            Item? item = _player.Inventory.Open();
            item?.Use(_player);
        }

        private void Travel()
        {
            Utils.WriteLine("Where would you like to go?");
            List<Area> options = new();

            // find all nearby areas that are not the current area
            options.AddRange(_player
                            .CurrentArea
                            .NearbyAreas
                            .FindAll(p =>
                                p != _player.CurrentArea));

            options.ForEach(opt =>
            {
                Utils.Write(options.IndexOf(opt) + 1, ". ", opt); // "1. Name"
                if (opt.Visited)
                    Utils.Write(" (Visited)");
                Utils.WriteLine();
            });

            Utils.WriteLine("0. Cancel");
            int input = Utils.ReadInt(0, options.Count);

            if (input == 0) return;

            int minutes = Utils.RandInt(30, 60);
            Utils.WriteLine("You travel for ", minutes, " minutes...");
            _player.Update(minutes);
            options[input - 1].Enter(_player);
        }


        private void Sleep()
        {
            Utils.WriteLine("How many hours would you like to sleep?");
            _player.Sleep(Utils.ReadInt() * 60);
        }

        private void CheckGear()
        {
            Examine.ExamineGear(_player);
            Utils.WriteLine("Press any key to continue");
            Utils.Read();
        }

        private void LookAround()
        {
            Utils.WriteLine("You take in your surroundings");
            Utils.WriteLine("You're in a ", _player.CurrentArea, ", ", _player.CurrentArea.Description);
            Utils.WriteLine("Its ", World.GetTimeOfDay(), " and ", _player.CurrentArea.GetTemperature(), " degrees.");
            if (_player.CurrentArea.Npcs.Count == 0 && _player.CurrentArea.Items.Count == 0)
            {
                Utils.WriteLine("You see nothing of interest, time to move on.");
                return;
            }

            Utils.WriteLine("You see the following things:");
            foreach (var item in _player.CurrentArea.Items)
            {
                Utils.WriteLine(item);
            }
            foreach (var npc in _player.CurrentArea.Npcs)
            {
                Utils.WriteLine(npc);
            }
        }
        private void Quit()
        {
            _player.Damage(999);
        }
    }


}
