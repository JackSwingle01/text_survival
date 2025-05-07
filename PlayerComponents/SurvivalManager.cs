using System.Buffers;
using text_survival.Actors;
using text_survival.Actors.text_survival.Actors;
using text_survival.Effects;
using text_survival.IO;
using text_survival.Items;
using text_survival.Survival;
namespace text_survival.PlayerComponents;

class SurvivalManager
{
    public SurvivalManager(IActor owner, bool enableSurvivalMechanics, BodyPart body)
    {
        Owner = owner;
        EnableSurvivalMechanics = enableSurvivalMechanics;
        HungerModule = new HungerModule();
        ThirstModule = new ThirstModule();
        ExhaustionModule = new ExhaustionModule();
        TemperatureModule = new TemperatureModule();
        Body = body;
        Effects = [];
    }

    public void AddEffect(IEffect e)
    {
        Effects.Add(e);
        e.Apply(Owner);
    }
    public void RemoveEffect(string effectType)
    {
        List<IEffect> effectsToRemove = Effects.FindAll(e => e.EffectType == effectType && e.IsActive);
        foreach (Effect e in effectsToRemove.Cast<Effect>())
        {
            e.Remove(Owner);
            Effects.Remove(e);
        }
    }
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
        // apply effects first
        foreach (Effect effect in Effects)
        {
            effect.Update(Owner);
        }
        Effects.RemoveAll(e => !e.IsActive);

        if (EnableSurvivalMechanics)
        {
            double feelsLikeTemp = Owner.CurrentZone.GetTemperature() + WarmthBonus;

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
    public double WarmthBonus { get; set; }
    private List<IEffect> Effects;


    // general condition, for now avg of exhaustion and body health
    public double OverallConditionPercent => (ExhaustionModule.ExhaustionPercent + (Body.Health / Body.MaxHealth * 100)) / 2;
    public bool IsAlive => !Body.IsDestroyed;


    internal void Describe()
    {
        HungerModule.Describe();
        ThirstModule.Describe();
        ExhaustionModule.Describe();
        TemperatureModule.Describe();
    }

}
