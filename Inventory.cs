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
            while (true)
            {
                Output.WriteLine(this, " (", Weight(), "/", MaxWeight, "):");
                int index = Input.GetSelectionFromList(Items, true, "Close") - 1;
                if (index == -1) return;
                Item item = GetItem(index);
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
                        Examine.ExamineItem(item);
                        break;
                    case 3:
                        player.DropItem(item);
                        break;
                }
            }

        }
    }
}
