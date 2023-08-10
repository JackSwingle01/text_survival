namespace text_survival.Items
{
    public class Container
    {
        public float MaxWeight { get; set; }
        protected List<Item> Items { get; set; }
        public string Name { get; set; }

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

        public virtual void Open(Player player)
        {
            while (true)
            {
                Utils.WriteLine(this,":");
                int index = Utils.GetSelectionFromList(Items, true)-1;
                if (index == -1) return;
                Item item = GetItem(index);
                Utils.WriteLine("What would you like to do with ", item);
                int choice = Utils.GetSelectionFromList(new List<string>() { "Take", "Inspect", "Use" }, true);
                switch (choice)
                {
                    case 0:
                        continue;
                    case 1:
                        player.Inventory.Add(item);
                        break;
                    case 2:
                        Examine.ExamineItem(item);
                        break;
                    case 3:
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
            if (item.Weight + GetWeight() > MaxWeight)
            {
                Utils.Write("The ", this, "is full!\n");
                return;
            }
            Items.Add(item);
            EventAggregator.Publish(new ItemTakenEvent(item));
            Utils.WriteLine("You put the ", item, " in your ", this);
        }

        public void Remove(Item item)
        {
            Items.Remove(item);
        }

        public double GetWeight()
        {
            return Items.Sum(item => item.Weight);
        }

        public int Count()
        {
            return Items.Count;
        }


    }
}
