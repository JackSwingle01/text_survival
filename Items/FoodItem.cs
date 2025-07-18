using text_survival.Bodies;

namespace text_survival.Items
{
    public interface IEdible
    {
        int WaterContent { get; }
        int Calories { get; }
    }

    public class FoodItem : Item, IEdible
    {
        public FoodItem(string name, int calories, int waterContent = 0, double weight = .5) : base(name, weight)
        {
            Calories = calories;
            WaterContent = waterContent;
            NumUses = 1;
        }

        public int WaterContent { get; }
        public int Calories { get; }
        public HealingInfo? HealthEffect {get; set;}
        public DamageInfo? DamageEffect {get; set;}
        public override string ToString() => Name;
    }
}
