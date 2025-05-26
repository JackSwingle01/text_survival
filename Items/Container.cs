using text_survival.Actors;
using text_survival.IO;

namespace text_survival.Items
{
    public class Container(string name, float maxWeight)
    {
        private string _name = name;
        public string Name { get => IsEmpty ? _name + " (Empty)" : _name; set => _name = value; }
        public double Weight() => Items.Sum(item => item.Weight);
        public float MaxWeight { get; set; } = maxWeight;
        public List<Item> Items { get; } = [];
        public bool IsEmpty => Items.Count == 0;
        public bool IsFound { get; set; }

        public Item GetItem(int index) => Items[index];
        public Item GetItemByName(string itemName) => Items.First(i => i.Name.Equals(itemName));

        // public virtual void Open(Player player)
        // {
        //     while (!IsEmpty)
        //     {
        //         Output.WriteLine(this, ":");

        //         var items = new List<Item>(Items);
        //         Item takeAll = new Item("Take all");
        //         if (items.Count > 1)
        //         {
        //             items.Add(takeAll);
        //         }

        //         var itemStacks = ItemStack.CreateStacksFromItems(items);

        //         var selection = Input.GetSelectionFromList(itemStacks, true, "Close " + this);
        //         if (selection == null) return;

        //         Item selectedItem = selection.Pop();

        //         if (selectedItem == takeAll)
        //         {
        //             TakeAll(player);
        //             return;
        //         }

        //         Output.WriteLine("What would you like to do with ", selectedItem);
        //         string? choice = Input.GetSelectionFromList(["Take", "Inspect"], true);
        //         switch (choice)
        //         {
        //             case null:
        //                 continue;
        //             case "Take":
        //                 Remove(selectedItem);
        //                 player.TakeItem(selectedItem);
        //                 break;
        //             case "Inspect":
        //                 selectedItem.Describe();
        //                 break;
        //             case "Use":
        //                 Remove(selectedItem);
        //                 player.TakeItem(selectedItem);
        //                 player.UseItem(selectedItem);
        //                 break;
        //         }
        //     }
        //     Output.WriteLine(this, " is empty.");
        // }


     
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
