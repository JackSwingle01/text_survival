using System.ComponentModel;

namespace text_survival
{
    public class Place
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public ItemPool Items { get; set; }
        public NPCPool NPCs { get; set; }
        public float BaseTemperature { get; set; }
        public bool IsShelter { get; set; }

        public Place(string name, string description)
        {
            this.Name = name;
            this.Description = description;
            this.Items = new ItemPool();
            this.NPCs = new NPCPool();
        }


        public Place(string name)
        {
            this.Name = name;
            this.Description = "";
            Items = new ItemPool();
            NPCs = new NPCPool();
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
            str += Name;
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
            if (new Random().Next(100) < 50)
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
