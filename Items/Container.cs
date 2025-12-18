using text_survival.IO;
using text_survival.UI;

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
   
        public override string ToString() => Name;

        public void Add(Item item)
        {
            if (item.Weight + Weight() > MaxWeight)
            {
                GameDisplay.AddNarrative($"The {this} is full!\n");
                return;
            }
            Items.Add(item);
        }

        public void Remove(Item item) => Items.Remove(item);
        public int Count() => Items.Count;

    }
}
