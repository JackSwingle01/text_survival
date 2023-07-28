﻿namespace text_survival
{
    public class Place
    {


        public string Name { get; set; }
        public string Description { get; set; }
        private ItemPool Items { get; set; }
        public float BaseTemperature { get; set; }
        public bool IsShelter { get; set; }

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

        public float GetTemperature()
        {
            float effect = 0;
            if (World.GetTimeOfDay() == World.TimeOfDay.Morning)
            {
                effect -= 5;
            }
            else if (World.GetTimeOfDay() == World.TimeOfDay.Afternoon)
            {
                effect += 10;
            }
            else if (World.GetTimeOfDay() == World.TimeOfDay.Evening)
            {
                effect += 5;
            }
            else if (World.GetTimeOfDay() == World.TimeOfDay.Night)
            {
                effect -= 10;
            }
            effect += new Random().Next(-3, 3);
            if (IsShelter)
            {
                effect = effect / 2;
            }
           
            return effect + BaseTemperature;
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
