using text_survival.Actors;
using text_survival.Items;

namespace text_survival.Environments
{
    public class Area
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public ItemPool Items { get; set; }
        public NPCPool NPCs { get; set; }
        public List<Location> Locations { get; set; }
        public float BaseTemperature { get; set; }
        public bool IsShelter { get; set; }

        public Area(string name, string description)
        {
            Name = name;
            Description = description;
            Items = new ItemPool();
            NPCs = new NPCPool();
            Locations = new List<Location>();
        }


        public Area(string name)
        {
            Name = name;
            Description = "";
            Items = new ItemPool();
            NPCs = new NPCPool();
            Locations = new List<Location>();
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

        }
        public void Explore(Player player)
        {
            int minutes = new Random().Next(1, 60);
            Utils.Write("You explore for ", minutes, " minutes\n");
            Utils.Write("...\n");
            Thread.Sleep(1000);
            player.Update(minutes);
            int roll = Utils.Roll(4);
            if (roll == 1)
            {
                Utils.Write("You don't find anything interesting.\n");
                return;
            }
            else if (roll == 2)
            {
                // find item
                Item item = Items.GetRandomItem();
                Utils.Write("You found: ", item, "!\n");
                player.Inventory.Add(item);
                return;
            }
            else if (roll == 3)
            {
                // find enemy
                NPC npc = NPCs.GetRandomNPC();
                Combat.CombatLoop(player, npc);
                if (npc.Health <= 0)
                {
                    NPCs.Kill(npc);
                }
            }
            else if (roll == 4)
            {
                // find location
                if (Locations.Count == 0)
                {
                    Utils.Write("You don't find anything interesting.\n");
                    return;
                }
                Location location = Locations[Utils.Rand(0, Locations.Count - 1)];
                location.Enter(player);
            }
        }

    }
}
