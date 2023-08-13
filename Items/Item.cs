using text_survival.Environments;

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

    public class Item : IInteractable
    {
        public string Name { get; set; }
        public double Weight { get; set; } // in kg
        public Action<Player> UseEffect { get; set; }
        public string Description { get; set; }
        public int Quality { get; set; } // percentage 0% being extremely poor quality, 100% being perfect quality

        public Item(string name, double weight = 1, int quality = 50)
        {
            Name = name;
            Weight = weight;
            Description = "";
            Quality = quality;
            UseEffect = (player) =>
            {
                Utils.Write("Nothing happened.\n"); // just a default
            };
        }

        public override string ToString()
        {
            return Name;
        }
        public virtual void Use(Player player)
        {
            //Utils.Write("You use the ", this, "...\n");
            Thread.Sleep(1000);
            UseEffect?.Invoke(player);
            World.Update(1);
        }

        public void Interact(Player player)
        {
            player.TakeItem(this);
        }

    }
}
