namespace text_survival.Items
{
    public class FoodItem : Item
    {
        public FoodItem(string name, int calories, int waterContent = 0, int weight = 1, int uses = 1) : base(name, weight)
        {
            Calories = calories;
            WaterContent = waterContent;
            UseEffect = (player) => { player.Eat(this); };
        }

        public int WaterContent { get; set; }

        public int Calories { get; set; }

        public string StatsToString()
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
        public override string ToString()
        {
            return Name;
        }
        public override void Write()
        {
            Utils.Write(this, " => ");
            if (Description != "")
            {
                Utils.Write(Description);
            }
            if (Calories > 0)
            {
                Utils.Write("Cal: ", Calories, " ");
            }
            if (WaterContent > 0)
            {
                Utils.Write("Water: ", WaterContent, "ml ");
            }
            Utils.Write("Weight: ", Weight, "kg ");

            Utils.Write("\n");
        }



    }
}
