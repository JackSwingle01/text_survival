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
        protected List<Item> Items { get; set; }
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

                var options = GetStackedItemList();
                if (Items.Count > 1)
                {
                    options.Add("Take all");
                }

                int index = Input.GetSelectionFromList(options, true, "Close " + this) - 1;
                if (index == -1) return;
                string itemName = options[index];
                itemName = ExtractStackedItemName(itemName);

                if (itemName == "Take all")
                {
                    TakeAll(player);
                    return;
                }

                Item item = Items.First(i => i.Name.StartsWith(itemName));
                Output.WriteLine("What would you like to do with ", item);
                int choice = Input.GetSelectionFromList(new List<string>() { "Take", "Inspect", "Use" }, true);
                switch (choice)
                {
                    case 0:
                        continue;
                    case 1:
                        player.TakeItem(item);
                        Remove(item);
                        break;
                    case 2:
                        Describe.DescribeItem(item);
                        break;
                    case 3:
                        Remove(item);
                        item.UseEffect.Invoke(player);
                        break;
                }
            }
            Output.WriteLine(this, " is empty.");
        }
        public string ExtractStackedItemName(string name)
        {
            // This regex pattern looks for the item name followed by an optional space and "x" followed by one or more digits.
            Match match = Regex.Match(name, @"^(.*?)\s*x\d*$");
            if (match.Success)
            {
                return match.Groups[1].Value.Trim();
            }
            return name;
        }

        public List<string> GetStackedItemList()
        {
            var items = new List<string>();
            var counts = new Dictionary<string, int>();
            foreach (var item in Items)
            {
                if (counts.ContainsKey(item.Name))
                {
                    counts[item.Name]++;
                }
                else
                {
                    counts.Add(item.Name, 1);
                }
            }

            foreach (var item in counts)
            {
                if (item.Value == 1)
                {
                    items.Add(item.Key);
                }
                else
                {
                    items.Add(item.Key + " x" + item.Value);
                }
            }
            return items;
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
