namespace text_survival.Items
{
    public interface IEdible
    {
        int WaterContent { get; set; }
        int Calories { get; set; }

    }

    public class FoodItem : Item, IEdible
    {
        public FoodItem(string name, int calories, int waterContent = 0, double weight = .5) : base(name, weight)
        {

            Quality = 100;
            Calories = calories;
            WaterContent = waterContent;
            UseEffect = (player) => { player.Eat(this); };
        }


        public int WaterContent { get; set; }

        public int Calories { get; set; }


        public override string ToString()
        {
            return Name;
        }
    }
}
