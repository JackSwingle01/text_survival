using text_survival.Actors;
using text_survival.IO;
using text_survival.Items;
using text_survival.Survival;
using text_survival.Effects;
using text_survival.Bodies;

namespace text_survival.PlayerComponents;

public class SurvivalManager
{
    public SurvivalManager(Actor owner, EffectRegistry effectRegistry)
    {
        Owner = owner;
        HungerModule = new HungerModule();
        ThirstModule = new ThirstModule();
        ExhaustionModule = new ExhaustionModule();
        TemperatureModule = new TemperatureModule();
        _effectRegistry = effectRegistry;
    }

    private readonly EffectRegistry _effectRegistry;
    // public void AddEffect(IEffect effect) => _effectRegistry.AddEffect(effect);
    // public void RemoveEffect(string effectType) => _effectRegistry.RemoveEffect(effectType);

    public void Heal(HealingInfo heal) => Owner.Heal(heal);
    public void Damage(DamageInfo damage)
    {
        Owner.Damage(damage);

        if (!Owner.IsAlive)
        {
            Output.WriteLine(Owner, " died!");
        }
    }


    public void Sleep(int minutes)
    {
        int minutesSlept = 0;
        while (minutesSlept < minutes)
        {
            ExhaustionModule.Rest(1);
            World.Update(1);
            minutesSlept++;
            if (ExhaustionModule.IsFullyRested)
            {
                Output.Write("You wake up feeling refreshed.\n");
                break;
            }
        }
        HealingInfo healing = new HealingInfo()
        {
            Amount = minutesSlept / 10,
            Type = "natural",
            TargetPart = "Body",
            Quality = ExhaustionModule.IsFullyRested ? 1 : .7, // healing quality is better after a full night's sleep
        };

        Heal(healing);
    }


    public void ConsumeFood(FoodItem food)
    {
        // todo handle eating half an item
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

        if (food.HealthEffect != null)
        {
            Heal(food.HealthEffect);
        }
        if (food.DamageEffect != null)
        {
            Damage(food.DamageEffect);
        }
    }



    public void Update()
    {
        if (Owner is Player player)
        {
            // todo add more detailed equipment stats like warmth, wind, and water resistance
            double feelsLikeTemp = player.CurrentLocation.GetTemperature() + player.EquipmentWarmth;

            HungerModule.Update();
            ThirstModule.Update();
            ExhaustionModule.Update();
            TemperatureModule.Update(feelsLikeTemp);

            if (HungerModule.IsStarving || TemperatureModule.IsDangerousTemperature)
            {
                var damage = new DamageInfo()
                {
                    Amount = 1,
                    Type = TemperatureModule.IsDangerousTemperature ? "thermal" : "starvation",
                    IsPenetrating = HungerModule.IsStarving,
                };
                player.Damage(damage);
            }
        }
    }

    private Actor Owner { get; }
    private HungerModule HungerModule { get; }
    private ThirstModule ThirstModule { get; }
    private ExhaustionModule ExhaustionModule { get; }
    private TemperatureModule TemperatureModule { get; }

    // general condition, for now avg of exhaustion and body health
    public double ConditionPercent => (ExhaustionModule.ExhaustionPercent + (Owner.Body.Health / Owner.Body.MaxHealth * 100)) / 2;

    public void Describe()
    {
        HungerModule.Describe();
        ThirstModule.Describe();
        ExhaustionModule.Describe();
        TemperatureModule.Describe();
    }

}
