using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace text_survival
{
    public class Place
    {
        private string _name = "";
        private string? _description;
        private ItemPool _items = new();
        
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
        }

        public Place(string name)
        {
            this.Name = name;
        }

        public string Name { get => _name; set => _name = value; }
        public string? Description { get => _description; set => _description = value; }
        internal ItemPool Items { get => _items; set => _items = value; }

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
