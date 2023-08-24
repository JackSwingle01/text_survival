using text_survival_rpg_web.Interfaces;

namespace text_survival_rpg_web.Items
{
    public class Container : IInteractable
    {
        public string Name { get; set; }
        public double Weight() => Items.Sum(item => item.Weight);
        public float MaxWeight { get; set; }
        protected List<Item> Items { get; set; }

        public Container(string name, float maxWeight)
        {
            Name = name;
            MaxWeight = maxWeight;
            Items = new List<Item>();
        }

        public Item GetItem(int index)
        {
            return Items[index];
        }

        public void Interact(Player player)
        {
            Output.WriteLine("You open the ", this);
            Open(player);
        }

        public virtual void Open(Player player)
        {
            while (true)
            {
                Output.WriteLine(this, ":");
                var options = new List<string>();
                Items.ForEach(item => options.Add(item.Name));
                int index;
                if (Items.Count > 1)
                {
                    options.Add("Take all");
                    index = Input.GetSelectionFromList(options, true, "Close " + this) - 1;

                    if (index == options.Count - 1)
                    {
                        while (Items.Count > 0)
                        {
                            player.TakeItem(Items[0]);
                        }
                        return;
                    }
                }
                else
                {
                    index = Input.GetSelectionFromList(options, true, "Close " + this) - 1;
                }
                if (index == -1) return;
                Item item = GetItem(index);
                Output.WriteLine("What would you like to do with ", item);
                int choice = Input.GetSelectionFromList(new List<string>() { "Take", "Inspect", "Use" }, true);
                switch (choice)
                {
                    case 0:
                        continue;
                    case 1:
                        player.TakeItem(item);
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
        }

        public override string ToString()
        {
            return Name;
        }

        public void Add(Item item)
        {
            if (item.Weight + Weight() > MaxWeight)
            {
                Output.Write("The ", this, "is full!\n");
                return;
            }
            Items.Add(item);
            //EventHandler.Publish(new ItemTakenEvent(item));
        }

        public void Remove(Item item)
        {
            Items.Remove(item);
        }


        public int Count()
        {
            return Items.Count;
        }

        public bool Contains(Item item)
        {
            return Items.Contains(item);
        }

    }
}
