namespace text_survival
{
    public class Item
    {
        public string Name { get; set; }
        public int Weight { get; set; }
        public Action<Player> UseEffect { get; set; }

        public Item(string name, int weight = 1)
        {
            this.Name = name;
            this.Weight = weight;
            UseEffect = (player) => { Utils.Write("Nothing happened."); };
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
            player.Update(1);
        }
    }
}
