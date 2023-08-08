﻿using text_survival.Actors;

using text_survival.Environments;
using text_survival.Items;
using static text_survival.Attributes;

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
        LevelUp,

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
                { ActionType.LevelUp, this.LevelUp },
                // Add more actions as necessary
            };
        }

        public void UpdatePossibleActions()
        {
            // Clear the available actions
            AvailableActions.Clear();

            // always available actions
            AvailableActions.Add(ActionType.LookAround);
            AvailableActions.Add(ActionType.CheckStats);


            // conditional actions
            if (_player.SkillPoints > 0)
            {
                AvailableActions.Add(ActionType.LevelUp);
            }
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
            if (_player.Armor.Count > 0)
                AvailableActions.Add(ActionType.CheckGear);

            // last action
            AvailableActions.Add(ActionType.Quit);
        }

        public void Act()
        {
            UpdatePossibleActions();
            Examine.ExamineSurvivalStats(_player);
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

        private void LevelUp()
        {
            Utils.WriteLine("You have ", _player.SkillPoints, " points.");
            while (_player.SkillPoints > 0)
            {
                Utils.WriteLine("Select an attribute to improve:");
                Utils.WriteLine("1. ", _player.Attributes.Strength);
                Utils.WriteLine("2. ", _player.Attributes.Intelligence);
                Utils.WriteLine("3. ", _player.Attributes.Speed);
                Utils.WriteLine("4. ", _player.Attributes.Endurance);
                Utils.WriteLine("5. ", _player.Attributes.Agility);
                Utils.WriteLine("6. ", _player.Attributes.Luck);
                Utils.WriteLine("7. ", _player.Attributes.Willpower);
                Utils.WriteLine("8. ", _player.Attributes.Personality);
                Utils.WriteLine("0. Cancel");
                int input = Utils.ReadInt(0, 8);
                switch (input)
                {
                    case 0:
                        return;
                    case 1:
                        _player.Attributes.IncreaseBase(PrimaryAttributes.Strength, 1);
                        break;
                    case 2:
                        _player.Attributes.IncreaseBase(PrimaryAttributes.Intelligence, 1);
                        break;
                    case 3:
                        _player.Attributes.IncreaseBase(PrimaryAttributes.Speed, 1);
                        break;
                    case 4:
                        _player.Attributes.IncreaseBase(PrimaryAttributes.Endurance, 1);
                        break;
                    case 5:
                        _player.Attributes.IncreaseBase(PrimaryAttributes.Agility, 1);
                        break;
                    case 6:
                        _player.Attributes.IncreaseBase(PrimaryAttributes.Luck, 1);
                        break;
                    case 7:
                        _player.Attributes.IncreaseBase(PrimaryAttributes.Willpower, 1);
                        break;
                    case 8:
                        _player.Attributes.IncreaseBase(PrimaryAttributes.Personality, 1);
                        break;
                }
                _player.SkillPoints--;
            }
        }

        private void CheckStats()
        {
            Examine.ExamineLevel(_player);
            Examine.ExaminePrimaryAttributes(_player);
            Examine.ExamineSecondaryAttributes(_player);
            Examine.ExamineSkills(_player);
            Utils.WriteLine("Press any key to continue...");
            Console.ReadKey(true);
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
