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
            NPCPool = new NPCPool();
            Items = new ItemPool();
            Containers = new List<Container>();
        }
        public string Name { get; set; }
        public string Description { get; set; }
        public NPCPool NPCPool { get; set; }
        public ItemPool Items { get; set; }
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
            foreach (var npc in NPCPool)
            {
                Utils.Write(npc, "\n");
            }
        }
        public void Explore(Player player)
        {
            if (NPCPool.Count() == 0 && Items.Count() == 0 && Containers.Count == 0)
            {
                Utils.Write("There is nothing to do here.\n");
                return;
            }
            if (NPCPool.Count() > 0)
            {
                Combat.CombatLoop(player, NPCPool.GetRandomNPC());
                return;
            }
            Utils.Write("You see:\n");
            Items.Write();
            foreach (var c in Containers)
            {
                Utils.Write(c, "\n");
            }
            Utils.Write("Enter the name of the item you want or the name of the container you want to open.\n");
            string input = Utils.Read();
            Item? item = null;
            Container? container = null;
            item = Items.GetItemByName(input);
            if (item is not null)
            {
                Items.Remove(item);
                player.Inventory.Add(item);
                Utils.Write("You pick up ", item, ".\n");
                return;
            }
            foreach (var c in Containers)
            {
                if (c.Name == input)
                {
                    container = c;
                    break;
                }
            }
            if (container is not null)
            {
                item = container.Open();
                if (item is not null)
                {
                    container.Remove(item);
                    player.Inventory.Add(item);
                    Utils.Write("You take the ", item, ".\n");
                }
            }
            return;
        }
        public void Exit(Player player)
        {
            Utils.Write("You leave the ", this, ".\n");
            player.CurrentLocation = null;
        }
    }
}
