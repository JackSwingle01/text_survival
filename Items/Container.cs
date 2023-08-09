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

        public Item? GetItem(int index)
        {
            if (index < 0 || index >= Items.Count)
            {
                return null;
            }
            return Items[index];
        }

        public Item? Open()
        {
            Write();
            if (Items.Count == 0)
            {
                return null;
            }
            Utils.Write("Enter the number of the item you want or type '0' to exit.\n");
            int input = Utils.ReadInt(0, Items.Count);
            if (input == 0)
            {
                return null;
            }
            int index = input - 1;
            return GetItem(index) as Item;
        }

        public override string ToString()
        {
            return Name;
        }
        public void Write()
        {
            Utils.WriteLine(Name + ":");
            Utils.WriteLine("Weight: ",GetWeight(),"/" + MaxWeight);
            if (Items.Count == 0)
            {
                Utils.Write(Name, " is empty!\n");
            }
            int count = 1;
            foreach (Item item in Items)
            {
                Utils.Write(count, ". ");
                item.Write();
                count++;
            }
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
