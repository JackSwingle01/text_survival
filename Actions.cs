using System;
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
        Explore,
        OpenInventory,
        Travel,
        Sleep,
        CheckGear,
        Quit

        // Add more actions as necessary
    }

    public class Actions
    {
        private Player _player;
        private Dictionary<ActionType, Action> actionDict;

        public List<ActionType> AvailableActions { get; private set; }

        public Actions(Player player)
        {
            this._player = player;
            this.AvailableActions = new List<ActionType>();
            this.actionDict = new Dictionary<ActionType, Action>()
            {
                { ActionType.Explore, this.Explore },
                { ActionType.OpenInventory, this.OpenInventory },
                { ActionType.Travel , this.Travel },
                { ActionType.Sleep, this.Sleep },
                { ActionType.CheckGear, this.CheckGear },
                { ActionType.Quit, this.Quit }
                // Add more actions as necessary
            };
        }

        public void UpdatePossibleActions()
        {
            // Clear the available actions
            AvailableActions.Clear();
            AvailableActions.Add(ActionType.Explore);
            AvailableActions.Add(ActionType.OpenInventory);
            AvailableActions.Add(ActionType.Travel);
            if (_player.Exhaustion > 0)
                AvailableActions.Add(ActionType.Sleep);
            AvailableActions.Add(ActionType.CheckGear);
            AvailableActions.Add(ActionType.Quit);
            // Check conditions and add actions as necessary
            //if (_player.CurrentLocation is null) return;
            //if (_player.CurrentLocation.Items.Count == 0)
            //    AvailableActions.Add(ActionType.PickUpItem);

            //if (_player.CurrentLocation.Npcs.Count == 0)
            //    AvailableActions.Add(ActionType.EnterCombat);

        }

        public void Act()
        {
            // Display the available actions
            for (int i = 0; i < AvailableActions.Count; i++)
            {
                Utils.Write($"{i + 1}. {AvailableActions[i]}\n");
            }


            // Get the _player's choice
            int choice = Utils.ReadInt(1, AvailableActions.Count);

            // Execute the chosen action
            ActionType actionType = AvailableActions[choice - 1];
            actionDict[actionType].Invoke();
        }


        private void Explore()
        {
            _player.CurrentArea.Explore(_player);
        }

        private void OpenInventory()
        {
            Item? item = _player.Inventory.Open(); 
            item?.Use(_player);
               
        }

        private void Travel()
        {
            Utils.Write("Where would you like to go?\n");
            List<Area> options = new List<Area>();
            options.AddRange(World.Areas.FindAll(p => p != _player.CurrentArea));
            for (int i = 0; i < options.Count; i++)
            {
                Utils.Write((i + 1) + ". ", options[i], "\n");
            }
            string? input = Utils.Read();
            if (int.TryParse(input, out int index))
            {
                if (index > 0 && index <= options.Count)
                {
                    Utils.Write("You travel for 1 hour\n");
                    _player.Update(60);
                    if (_player.CurrentLocation is not null)
                    {
                        _player.CurrentLocation.Exit(_player);
                    }
                    _player.CurrentArea = options[index - 1];
                    _player.CurrentArea = _player.CurrentArea;
                    Utils.Write("You are now at ", _player.CurrentArea.Name, "\n");
                }
                else
                {
                    Utils.Write("Invalid input\n");
                }
            }
            else
            {
                Utils.Write("Invalid input\n");
            }
        }

        private void Sleep()
        {
            Utils.Write("How many hours would you like to sleep?\n");
            _player.Sleep(Utils.ReadInt()* 60);
        }

        private void CheckGear()
        {
            _player.WriteEquipedItems();
            Utils.Write("Press any key to continue\n");
            Utils.Read();
        }

        private void Quit()
        {
            _player.Damage(999);
        }
}


}
