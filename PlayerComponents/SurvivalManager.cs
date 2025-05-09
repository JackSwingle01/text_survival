using text_survival.Actors;
using text_survival.Effects;
using text_survival.IO;
using text_survival.Items;
using text_survival.Survival;
namespace text_survival.PlayerComponents;

class SurvivalManager : ISurvivalSystem
{
    public SurvivalManager(IActor owner, EffectRegistry effectRegistry, bool enableSurvivalMechanics, BodyPart body)
    {
        Owner = owner;
        EnableSurvivalMechanics = enableSurvivalMechanics;
        HungerModule = new HungerModule();
        ThirstModule = new ThirstModule();
        ExhaustionModule = new ExhaustionModule();
        TemperatureModule = new TemperatureModule();
        Body = body;
        _effectRegistry = effectRegistry;
    }

    private readonly EffectRegistry _effectRegistry;
    // public void AddEffect(IEffect effect) => _effectRegistry.AddEffect(effect);
    // public void RemoveEffect(string effectType) => _effectRegistry.RemoveEffect(effectType);
    
    public void Heal(double heal) => Body.Heal(heal);
    public void Damage(double damage)
    {
        Body.Damage(damage);

        if (Body.IsDestroyed)
        {
            Output.WriteLine(Owner, " died!");
        }
    }


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

        if (food.HealthEffect > 0)
        {
            Heal(food.HealthEffect);
        }
        else if (food.HealthEffect < 0)
        {
            Damage(food.HealthEffect);
        }
    }



    public void Update()
    {


        if (EnableSurvivalMechanics)
        {
            double feelsLikeTemp = Owner.CurrentZone.GetTemperature() + Owner.EquipmentWarmth;

            HungerModule.Update();
            ThirstModule.Update();
            ExhaustionModule.Update();
            TemperatureModule.Update(feelsLikeTemp);

            if (HungerModule.IsStarving || TemperatureModule.IsDangerousTemperature)
            {
                Owner.Damage(1);
            }
        }
    }

    private IActor Owner { get; }
    private bool EnableSurvivalMechanics { get; }
    public BodyPart Body { get; }
    private HungerModule HungerModule { get; }
    private ThirstModule ThirstModule { get; }
    private ExhaustionModule ExhaustionModule { get; }
    private TemperatureModule TemperatureModule { get; }

    // general condition, for now avg of exhaustion and body health
    public double ConditionPercent => (ExhaustionModule.ExhaustionPercent + (Body.Health / Body.MaxHealth * 100)) / 2;
    public bool IsAlive => !Body.IsDestroyed;


    public void Describe()
    {
        HungerModule.Describe();
        ThirstModule.Describe();
        ExhaustionModule.Describe();
        TemperatureModule.Describe();
    }

}
