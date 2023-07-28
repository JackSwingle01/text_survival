namespace text_survival
{
    public class FoodItem : Item
    {
        private int _calories;
        private int _waterContent;

        public FoodItem(string name, int calories, int waterContent = 0, int weight = 1) : base(name, weight)
        {
            this.Calories = calories;
            this.WaterContent = waterContent;
            UseEffect = (player) => { player.Eat(this); };
        }

        public int WaterContent { get => _waterContent; set => _waterContent = value; }
        public int Calories { get => _calories; set => _calories = value; }

        public override string ToString()
        {
            string str = "";
            str += "Name: " + Name;
            str += "\n";
            str += "Calories: " + Calories;
            str += "\n";
            if (WaterContent > 0)
            {
                str += "Water Content (ml): " + WaterContent;
                str += "\n";
            }
            return str;
        }

        public override void Use(Player player)
        {
            Utils.Write("You consume the " + Name);
            UseEffect?.Invoke(player);
        }

    }
}
