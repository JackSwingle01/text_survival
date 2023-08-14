using text_survival.Environments;

namespace text_survival.Items
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
                int index = Input.GetSelectionFromList(Items, true) - 1;
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
                        Examine.ExamineItem(item);
                        break;
                    case 3:
                        this.Remove(item);
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
