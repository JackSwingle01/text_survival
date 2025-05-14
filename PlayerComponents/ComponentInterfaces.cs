using text_survival.Items;
using text_survival.Effects;
using text_survival.Actors;
using text_survival.Environments;
using text_survival.Bodies;

namespace text_survival.PlayerComponents;


public interface ISurvivalSystem
{
    bool IsAlive { get; }
    double ConditionPercent { get; }
    void Update();
    void Damage(DamageInfo damageInfo);
    void Heal(HealingInfo healingInfo);
    void Sleep(int minutes);
    void ConsumeFood(FoodItem food);
    void Describe();
}


public interface ILocationSystem
{
    Location CurrentLocation { get; set; }
    Zone CurrentZone { get; set; }
    void Leave();
    bool RemoveItemFromLocation(Item item);
    void AddItemToLocation(Item item);
}
