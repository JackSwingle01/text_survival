using text_survival.Actors.Player;
using text_survival.Crafting;
using text_survival.Environments;
using text_survival.Actions.Expeditions;
using text_survival.Core;
using text_survival.Environments.Features;

namespace text_survival.Actions;

public class GameContext(Player player)
{
    public Player player = player;
    public Location CurrentLocation => player.CurrentLocation;
    public CraftingSystem CraftingManager = new CraftingSystem(player);
    public CampManager Camp = player.Camp;
    public Location? CampLocation = player.Camp.CampLocation;
    public bool IsAtCamp => CurrentLocation == CampLocation;
    public Expedition? Expedition;

    public bool Check(EventCondition condition)
    {
        return condition switch
        {
            EventCondition.IsDaytime => World.GetTimeOfDay() == World.TimeOfDay.Morning ||
                         World.GetTimeOfDay() == World.TimeOfDay.Afternoon ||
                         World.GetTimeOfDay() == World.TimeOfDay.Evening,
            EventCondition.Traveling => throw new NotImplementedException(),
            EventCondition.Resting => throw new NotImplementedException(),
            EventCondition.Working => throw new NotImplementedException(),
            EventCondition.HasFood => throw new NotImplementedException(),
            EventCondition.HasMeat => throw new NotImplementedException(),
            EventCondition.HasFirewood => throw new NotImplementedException(),
            EventCondition.HasStones => throw new NotImplementedException(),
            EventCondition.Injured => throw new NotImplementedException(),
            EventCondition.Bleeding => throw new NotImplementedException(),
            EventCondition.Slow => throw new NotImplementedException(),
            EventCondition.FireBurning => throw new NotImplementedException(),
            EventCondition.Inside => CurrentLocation.HasFeature<ShelterFeature>(),
            EventCondition.Outside => !Check(EventCondition.Inside),
            _ => throw new NotImplementedException(),
        };
    }
}
public enum EventCondition
{
    IsDaytime,
    Traveling,
    Resting,
    Working,
    HasFood,
    HasMeat,
    HasFirewood,
    HasStones,
    Injured,
    Bleeding,
    Slow,
    FireBurning,
    Outside,
    Inside,

}