using System.Text.RegularExpressions;
using text_survival.Actors;
using text_survival.Interfaces;
using text_survival.IO;

namespace text_survival.Items
{
    public class Container : IInteractable
    {
        private string _name;
        public string Name { get => (IsEmpty && HasBeenOpened) ? _name + " (Empty)" : _name; set => _name = value; }
        public double Weight() => Items.Sum(item => item.Weight);
        public float MaxWeight { get; set; }
        public List<Item> Items { get; }
        public bool IsEmpty => Items.Count == 0;
        protected bool HasBeenOpened { get; set; }
        public bool IsFound { get; set; }

        public Container(string name, float maxWeight)
        {
            _name = name;
            MaxWeight = maxWeight;
            Items = [];
        }

        public Item GetItem(int index) => Items[index];
        public Item GetItemByName(string itemName) => Items.First(i => i.Name.Equals(itemName));

        public void Interact(Player player)
        {
            if (!Combat.SpeedCheck(player))
            {
                Npc npc = Combat.GetFastestNpc(player.CurrentLocation);
                Output.WriteLine("You couldn't get past the ", npc, "!");
                npc.Interact(player);
                return;
            }
            Output.WriteLine("You open the ", this);
            Open(player);
        }

        public Command<Player> InteractCommand => new("Look in " + Name, Interact);

        public virtual void Open(Player player)
        {
            HasBeenOpened = true;
            while (!IsEmpty)
            {
                Output.WriteLine(this, ":");

                var items = new List<Item>(Items);
                Item takeAll = new Item("Take all");
                if (items.Count > 1)
                {
                    items.Add(takeAll);
                }

                var itemStacks = ItemStack.CreateStacksFromItems(items);

                var selection = Input.GetSelectionFromList(itemStacks, true, "Close " + this);
                if (selection == null) return;

                Item selectedItem = selection.Take();

                if (selectedItem == takeAll)
                {
                    TakeAll(player);
                    return;
                }

                Output.WriteLine("What would you like to do with ", selectedItem);
                string? choice = Input.GetSelectionFromList(["Take", "Inspect"], true);
                switch (choice)
                {
                    case null:
                        continue;
                    case "Take":
                        Remove(selectedItem);
                        player.TakeItem(selectedItem);
                        break;
                    case "Inspect":
                        Describe.DescribeItem(selectedItem);
                        break;
                    case "Use":
                        Remove(selectedItem);
                        player.TakeItem(selectedItem);
                        player.UseItem(selectedItem);
                        break;
                }
            }
            Output.WriteLine(this, " is empty.");
        }


        private void TakeAll(Player player)
        {
            while (!IsEmpty)
            {
                var item = Items.First();
                Remove(item);
                player.TakeItem(item);
            }
        }

        public override string ToString() => Name;

        public void Add(Item item)
        {
            if (item.Weight + Weight() > MaxWeight)
            {
                Output.Write("The ", this, "is full!\n");
                return;
            }
            Items.Add(item);
        }

        public void Remove(Item item) => Items.Remove(item);
        public int Count() => Items.Count;

    }
}
