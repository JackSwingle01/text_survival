using text_survival.Actors;
using text_survival.Items;

namespace text_survival.Environments
{
    public class Location
    {
        public Location(string name, string description)
        {
            Name = name;
            Description = description;
            NpcPool = new NpcPool();
            ItemPool = new ItemPool();
            Containers = new List<Container>();
            Items = new List<Item>();
            Npcs = new List<Npc>();
            
        }
        public string Name { get; set; }
        public string Description { get; set; }
        public List<Npc> Npcs { get; set; }
        public List<Item> Items { get; set; }
        public ItemPool ItemPool { get; set; }
        public NpcPool NpcPool { get; set; }
        public List<Container> Containers { get; set; }

        public void Write()
        {
            Utils.Write(this);
        }
        public override string ToString()
        {
            return Name;
        }
        public void Enter(Player player)
        {
            player.CurrentLocation = this;
            Utils.Write("You enter ", this, ".\n");
            Utils.Write(Description, "\n");
            Utils.Write("You see:\n");
            foreach (var item in Items)
            {
                Utils.Write(item, "\n");
            }
            foreach (var container in Containers)
            {
                Utils.Write(container, "\n");
            }
            Utils.Write("You see:\n");
            foreach (var npc in Npcs)
            {
                Utils.Write(npc, "\n");
            }
        }
        public void Explore(Player player)
        {
            switch (Npcs.Count)
            {
                case 0 when Items.Count == 0 && Containers.Count == 0:
                    Utils.Write("There is nothing to do here.\n");
                    return;
                case > 0:
                    Combat.CombatLoop(player, NpcPool.GetRandomNpc());
                    return;
            }

            Utils.Write("You see:\n");
            Items.ForEach(item => Utils.Write(item, "\n"));
            foreach (var c in Containers)
            {
                Utils.Write(c, "\n");
            }
            Utils.Write("Enter the name of the item you want or the name of the container you want to open.\n");
            string input = Utils.Read();
            Item? item = null;
            item = Items.FirstOrDefault(i => i.Name == input);
            if (item is not null)
            {
                Items.Remove(item);
                player.Inventory.Add(item);
                Utils.Write("You pick up ", item, ".\n");
                return;
            }

            Container? container = Containers.FirstOrDefault(c => c.Name == input);
            if (container is null) return;

            item = container.Open();
            if (item is null) return;

            container.Remove(item);
            player.Inventory.Add(item);
            Utils.Write("You take the ", item, ".\n");
        }
        public void Exit(Player player)
        {
            Utils.Write("You leave the ", this, ".\n");
            player.CurrentLocation = null;
        }
    }
}
