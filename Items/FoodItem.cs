using text_survival.Interfaces;

namespace text_survival.Items
{
    public interface IEdible
    {
        int WaterContent { get; }
        int Calories { get; }
    }

    public class FoodItem : Item, IEdible, IUpdateable
    {
        public FoodItem(string name, int calories, int waterContent = 0, double weight = .5) : base(name, weight)
        {
            Quality = 100;
            Calories = calories;
            WaterContent = waterContent;
            UseEffect = (player) => { player.Eat(this); };
        }

        public int WaterContent { get; }
        public int Calories { get; }

        public void Update()
        {
            Quality -= .1; //TODO: Add spoilage
        }
        public override string ToString() => Name;
    }
}
