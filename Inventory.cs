using text_survival.Items;

namespace text_survival
{
    public class Inventory : Container
    {
        public Inventory(string name = "Backpack", int weightCap = 10) : base(name, weightCap)
        {
        }

        public override void Open(Player player)
        {
            HasBeenOpened = true;
            while (!IsEmpty)
            {
                Output.WriteLine(this, " (", Weight(), "/", MaxWeight, "):");
                var options = GetStackedItemList();
                int index = Input.GetSelectionFromList(options, true, "Close " + this) - 1;
                if (index == -1) return;
                string itemName = options[index];
                itemName = ExtractStackedItemName(itemName);
                Item item = Items.First(i => i.Name.StartsWith(itemName));
                Output.WriteLine("What would you like to do with ", item);
                int choice = Input.GetSelectionFromList(new List<string>() { "Use", "Inspect", "Drop" }, true);
                switch (choice)
                {
                    case 0:
                        continue;
                    case 1:
                        item.Use(player);
                        break;
                    case 2:
                        Describe.DescribeItem(item);
                        break;
                    case 3:
                        player.DropItem(item);
                        break;
                }
            }
            Output.WriteLine(this, " is empty.");

        }
    }
}
