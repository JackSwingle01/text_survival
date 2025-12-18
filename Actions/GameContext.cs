using text_survival.Actors.Player;
using text_survival.Crafting;
using text_survival.Environments;
using text_survival.Actions.Expeditions;
using text_survival.Core;
using text_survival.Environments.Features;
using text_survival.UI;
using System.Transactions;

namespace text_survival.Actions;

public class GameContext(Player player, Camp camp)
{
    public Player player = player;
    public Location CurrentLocation => Expedition?.CurrentLocation ?? Camp.Location;
    public Camp Camp = camp;
    public bool IsAtCamp => CurrentLocation == Camp.Location;
    public Expedition? Expedition;
    public Zone Zone => CurrentLocation.Parent;
    public bool Check(EventCondition condition)
    {
        return condition switch
        {
            EventCondition.IsDaytime => GetTimeOfDay() == TimeOfDay.Morning ||
                         GetTimeOfDay() == TimeOfDay.Afternoon ||
                         GetTimeOfDay() == TimeOfDay.Evening,
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
    public DateTime GameTime { get; set; } = new DateTime(2025, 1, 1, 9, 0, 0); // Full date/time for resource respawn tracking
    public void Update(int minutes)
    {
        player.Update(minutes, player.GetSurvivalContext(CurrentLocation));
        CurrentLocation.Parent.Update(minutes, GameTime);
        GameTime = GameTime.AddMinutes(minutes); // Keep GameTime in sync

        var logs = player?.GetFlushLogs();
        if (logs is not null && logs.Count != 0)
            GameDisplay.AddNarrative(logs);
    }

    public enum TimeOfDay
    {
        Night,
        Dawn,
        Morning,
        Afternoon,
        Noon,
        Evening,
        Dusk
    }

    public TimeOfDay GetTimeOfDay()
    {
        return GameTime.Hour switch
        {
            < 5 => TimeOfDay.Night,
            < 6 => TimeOfDay.Dawn,
            < 11 => TimeOfDay.Morning,
            < 13 => TimeOfDay.Noon,
            < 17 => TimeOfDay.Afternoon,
            < 20 => TimeOfDay.Evening,
            < 21 => TimeOfDay.Dusk,
            _ => TimeOfDay.Night
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