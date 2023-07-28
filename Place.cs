namespace text_survival
{
    public class Place
    {


        public string Name { get; set; }
        public string Description { get; set; }
        private ItemPool Items { get; set; }
        public float BaseTemperature { get; set; }

        public Place(string name, string description, ItemPool items)
        {
            this.Name = name;
            this.Description = description;
            this.Items = items;
        }

        public Place(string name, string description)
        {
            this.Name = name;
            this.Description = description;
            Items = new ItemPool();
        }

        public Place(string name)
        {
            this.Name = name;
            this.Description = "";
            Items = new ItemPool();
        }


        public override string ToString()
        {
            string str = "";
            str += "Name: " + Name;
            str += "\n";
            str += "Description: " + Description;
            str += "\n";
            return str;
        }
        public void Forage(Player player)
        {
            int minutes = new Random().Next(1, 60);
            Utils.Write("You looked for " + minutes + " minutes");
            player.Update(minutes);
            if (new Random().Next(100) < 75)
            {
                Utils.Write("You found nothing");
                return;
            }
            Item item = this.Items.GetRandomItem();
            Utils.Write("You found " + item.Name);
            player.Inventory.Add(item);
        }


    }
}
