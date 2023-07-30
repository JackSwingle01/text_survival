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
            return Name;
        }
        public void WriteInfo()
        {
            Utils.Write(Name);
            Utils.Write(Description);
            Utils.Write("Temperature: ", GetTemperature());
            Utils.Write("Items: ");
            foreach (Item item in Items)
            {
               item.Write();
            }
            Utils.Write("NPCs: ");
            foreach (NPC npc in NPCs)
            {
                npc.Write();
            }
        }
        public void Explore(Player player)
        {
            int minutes = new Random().Next(1, 60);
            Utils.Write("You explore for ", minutes, " minutes\n");
            Utils.Write("...\n");
            Thread.Sleep(1000);
            player.Update(minutes);
            if (Utils.FlipCoin())
            {
                Utils.Write("You don't find anything interesting.\n");
                return;
            }
            if (Utils.FlipCoin())
            {
                // find item
                Item item = this.Items.GetRandomItem();
                Utils.Write("You found: ", item, "!\n");
                player.Inventory.Add(item);
                return;
            }
            else
            {
                // find enemy
                NPC npc = this.NPCs.GetRandomNPC();
                Combat.CombatLoop(player, npc);
                if (npc.Health <= 0)
                {
                    NPCs.Kill(npc);
                }
            }
        }

    }
}
