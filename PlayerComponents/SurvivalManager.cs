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
        _effectRegistry = effectRegistry;
        TemperatureModule = new TemperatureModule(owner.Body, _effectRegistry);
    }

    private readonly EffectRegistry _effectRegistry;
    // public void AddEffect(IEffect effect) => _effectRegistry.AddEffect(effect);
    // public void RemoveEffect(string effectType) => _effectRegistry.RemoveEffect(effectType);

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

        Owner.Body.Heal(healing);
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
            Owner.Body.Heal(food.HealthEffect);
        }
        if (food.DamageEffect != null)
        {
            Owner.Body.Damage(food.DamageEffect);
        }
    }

    public void Update()
    {
        if (Owner is Player player)
        {
            HungerModule.Update();
            ThirstModule.Update();
            ExhaustionModule.Update();
            TemperatureModule.Update(player.CurrentLocation.GetTemperature(), player.EquipmentWarmth);

            if (HungerModule.IsStarving)
            {
                var damage = new DamageInfo()
                {
                    Amount = 1,
                    Type = "starvation",
                    IsPenetrating = true
                };
                player.Damage(damage);
            }

            var tempDamage = TemperatureModule.GetTemperatureDamage();
            if (tempDamage != null)
            {
                player.Damage(tempDamage);
            }

            // Update water loss from sweating effects
            var sweatingEffects = _effectRegistry.GetEffectsByKind("Sweating")
                .Where(e => e.IsActive)
                .Cast<SweatingEffect>();

            double totalWaterLoss = 0;
            foreach (var sweating in sweatingEffects)
            {
                totalWaterLoss += sweating.GetWaterLossForPeriod(TimeSpan.FromMinutes(1));
            }

            // Apply water loss to thirst system
            if (totalWaterLoss > 0)
            {
                ThirstModule.AddHydration(-totalWaterLoss);
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
