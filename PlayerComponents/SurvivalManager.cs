using text_survival.Actors;
using text_survival.Actors.text_survival.Actors;
using text_survival.IO;
using text_survival.Items;
using text_survival.Survival;
namespace text_survival.PlayerComponents;

class SurvivalManager
{
    public bool IsAlive => !Body.IsDestroyed;
    public BodyPart Body { get; }
    public SurvivalManager()
    {
        HungerModule = new HungerModule();
        ThirstModule = new ThirstModule();
        ExhaustionModule = new ExhaustionModule();
        TemperatureModule = new TemperatureModule();
        Body = BodyPartFactory.CreateHumanBody("Player", 100);
    }

    // general condition, for now avg of exhaustion and body health
    public double OverallConditionPercent => (ExhaustionModule.ExhaustionPercent + (Body.Health / Body.MaxHealth *100)) / 2;

    public void Damage(double damage)
    {
        Body.Damage(damage);
        if (Body.IsDestroyed)
        {
            Output.WriteLine("You died!");
            // end program
            Environment.Exit(0);
        }
    }

    public void Heal(double heal)
    {
        Body.Heal(heal);
    }

    private HungerModule HungerModule { get; }
    private ThirstModule ThirstModule { get; }
    private ExhaustionModule ExhaustionModule { get; }
    private TemperatureModule TemperatureModule { get; }

    public double WarmthBonus { get; set; }

    public void Sleep(int minutes)
    {
        for (int i = 0; i < minutes; i++)
        {
            ExhaustionModule.Rest(1);
            World.Update(1);
            if (ExhaustionModule.IsFullyRested)
            {
                Output.Write("You wake up feeling refreshed.\n");
                Heal(i / 6);
                return;
            }
        }
        Heal(minutes / 6);
    }

    public void ConsumeFood(FoodItem food)
    {
        Output.Write("You eat the ", food, ".\n");
        // if (HungerModule.Amount - food.Calories < 0)
        // {
        //     Output.Write("You are too full to finish it.\n");
        //     int percentageEaten = (int)(HungerModule.Amount / food.Calories) * 100;
        //     double calories = food.Calories * (100 - percentageEaten);
        //     double waterContent = food.WaterContent * (100 - percentageEaten);
        //     double weight = food.Weight * (100 - percentageEaten);
        //     food.calories = (int)calories;
        //     food.waterContent = (int)waterContent;
        //     food.weight
        //     // food = new FoodItem(food.Name, (int)calories, (int)waterContent, weight);
        //     HungerModule.Amount = 0;
        //     return;
        // }
        HungerModule.AddCalories(food.Calories);
        ThirstModule.AddHydration(food.WaterContent);
    }

    public void Update(Player player)
    {
        HungerModule.Update();
        ThirstModule.Update();
        ExhaustionModule.Update();
        double feelsLikeTemp = player.CurrentZone.GetTemperature() + WarmthBonus;
        TemperatureModule.Update(feelsLikeTemp);
        if (HungerModule.IsStarving || TemperatureModule.IsDangerousTemperature)
        {
            player.Damage(1);
        }
    }

    internal void Describe()
    {
        HungerModule.Describe();
        ThirstModule.Describe();
        ExhaustionModule.Describe();
        TemperatureModule.Describe();
    }
}