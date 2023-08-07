﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
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
            if (_player.Exhaustion > 0)
                AvailableActions.Add(ActionType.Sleep);
            if (_player.EquippedItems.Count > 0)
                AvailableActions.Add(ActionType.CheckGear);

            AvailableActions.Add(ActionType.Quit);
        }
        
        public void Act()
        {
            UpdatePossibleActions();
            //CheckStats();
            Utils.WriteLine("What would you like to do?");
            ActionType actionType = GetActionsByNum();//GetActionsFreeform();
            
            //if (actionType == null)
            //{
            //    Utils.WriteLine("Hmmm, I don't know how to do that, use 'help', 'h', or '?' To see available actions.");
            //    return;
            //}
            // Execute the chosen action
            if (AvailableActions.Contains(actionType))
            {
                _actionDict[actionType].Invoke();
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
            ActionType actionType;
            if (!InputToActionTypes.TryGetValue(input, out actionType))
            {
                // If the input is not a valid action, return ActionType.None
                return null;
            }
            // Return the ActionType
            return actionType;
        }

        private ActionType GetActionsByNum()
        {
            foreach (var action in AvailableActions)
            {
                Utils.WriteLine($"{AvailableActions.IndexOf(action) + 1}. {action}");
            }
            int input = Utils.ReadInt(1, AvailableActions.Count);
            return AvailableActions[input - 1];
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
                if (actionTypesToPrint.Contains(group.Key))
                {
                    // Use group.Key for ActionType
                    Console.Write($"{group.Key} => ");

                    // Get a list of inputs for this ActionType
                    var inputs = group.Select(kv => $"'{kv.Key}'").ToList();

                    // Use string.Join to concatenate inputs into a string
                    Console.WriteLine(string.Join(", ", inputs));
                }
            }
        }

        private void CheckStats()
        {
            _player.WriteSurvivalStats();
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
            Item item = _player.CurrentArea.Items.First();
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
                Utils.WriteLine(options.IndexOf(opt)+1, ". ", opt)); // "1. Name"
            Utils.WriteLine("0. Cancel");
            int input = Utils.ReadInt(0,options.Count);

            if (input == 0) return;
            
            int minutes = Utils.Rand(30, 60);
            Utils.WriteLine("You travel for ", minutes, " minutes...");
            _player.Update(minutes);
            options[input - 1].Enter(_player);

            Utils.WriteLine("You are now at ", _player.CurrentArea.Name);
        }
    

        private void Sleep()
        {
            Utils.WriteLine("How many hours would you like to sleep?");
            _player.Sleep(Utils.ReadInt()* 60);
        }

        private void CheckGear()
        {
            _player.WriteEquippedItems();
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