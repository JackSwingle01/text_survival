namespace text_survival.Items
{
    public class Item
    {
        public string Name { get; set; }
        public float Weight { get; set; } // in kg
        public Action<Player> UseEffect { get; set; }
        public string Description { get; set; }
        public int Quality { get; set; } // percentage 0% being extremely poor quality, 100% being perfect quality

        public Item(string name, float weight = 1)
        {
            Name = name;
            Weight = weight;
            Description = "";
            Quality = Utils.Rand(0, 100);

            UseEffect = (player) =>
            {
                Utils.Write("Nothing happened.\n");
            };
        }

        public override string ToString()
        {
            return Name;
        }
        public virtual void Use(Player player)
        {
            Utils.Write("You use the ", this, "...\n");
            Thread.Sleep(1000);
            UseEffect?.Invoke(player);
            player.Update(1);
        }
        public virtual void Write()
        {
            Utils.Write(this, "\n", Description);
            Utils.Write("Weight: ", Weight, "\n");
        }


    }
}
