using text_survival.Actors;
using text_survival.Interfaces;
using text_survival.IO;

namespace text_survival.Items
{
    //public interface IItem
    //{
    //    string Name { get; set; }
    //    double Weight { get; set; } // in kg
    //    Action<Player> UseEffect { get; set; }
    //    string Description { get; set; }
    //    int Quality { get; set; } // percentage 0% being extremely poor quality, 100% being perfect quality
    //    string ToString();
    //    void Use(Player player);
    //}

    public class Item : IInteractable, IClonable<Item>
    {
        public string Name { get; set; }
        public double Weight { get; set; } // in kg
        public Action<Player> UseEffect { get; set; }
        public string Description { get; set; } = "";
        public double Quality { get; set; } // percentage 0% being extremely poor quality, 100% being perfect quality
        public bool IsFound { get; set; }
        public IClonable<Item>.CloneDelegate Clone { get; set; }

        public Item(string name, double weight = 1, int quality = 50)
        {
            Name = name;
            Weight = weight;
            Quality = quality;
            UseEffect = (player) =>
            {
                Output.Write("Nothing happened.\n"); // just a default
            };
            Clone = () => new Item(name, weight, quality);
        }

        public override string ToString()
        {
            return Name;
        }
        public virtual void Use(Player player)
        {
            //Utils.Write("You use the ", this, "...\n");
            UseEffect?.Invoke(player);
            World.Update(1);
        }

        public void Interact(Player player)
        {
            if (!Combat.SpeedCheck(player))
            {
                Npc npc = Combat.GetFastestNpc(player.CurrentLocation);
                Output.WriteLine("You couldn't get past the ", npc, "!");
                npc.Interact(player);
                return;
            }
            player.TakeItem(this);
        }
        public Command<Player> InteractCommand => new Command<Player>("Pick up " + Name, Interact);

    }
}
