﻿namespace text_survival
{
    public class Item
    {
        public string Name { get; set; }
        public float Weight { get; set; }
        public Action<Player> UseEffect { get; set; }
        public bool IsConsumable { get; set; }
        public int Uses { get; set; }

        public Item(string name, float weight = 1, int uses = 1)
        {
            this.Name = name;
            this.Weight = weight;
            this.Uses = uses;
            UseEffect = (player) =>
            {
                Utils.Write("Nothing happened.");
            };
        }

        public override string ToString()
        {
            string str = "";
            str += "Name: " + Name;
            str += "\n";
            str += "Weight: " + Weight;
            str += "\n";
            return str;
        }
        public virtual void Use(Player player)
        {
            Utils.Write("You use the " + Name);
            UseEffect?.Invoke(player);
            if (Uses == 0)
            { 
                player.Inventory.Remove(this);
            }
            player.Update(1);
        }

    }
}
