using text_survival.Actors.Player;
using text_survival.Bodies;
using text_survival.Environments;
using text_survival.Actions.Expeditions;
using text_survival.Environments.Features;
using text_survival.Items;
using text_survival.UI;

namespace text_survival.Actions;

public class GameContext(Player player, Camp camp)
{
    public Player player = player;
    public Location CurrentLocation => Expedition?.CurrentLocation ?? Camp.Location;
    public Camp Camp = camp;
    public bool IsAtCamp => CurrentLocation == Camp.Location;

    // Player's carried inventory (aggregate-based)
    public Inventory Inventory { get; } = Inventory.CreatePlayerInventory(15.0);
    public Expedition? Expedition;
    public Zone Zone => CurrentLocation.Parent;
    public bool Check(EventCondition condition)
    {
        return condition switch
        {
            EventCondition.IsDaytime => GetTimeOfDay() == TimeOfDay.Morning ||
                         GetTimeOfDay() == TimeOfDay.Afternoon ||
                         GetTimeOfDay() == TimeOfDay.Evening,
            EventCondition.Traveling => Expedition != null,
            EventCondition.Resting => false, // TODO: implement when rest system exists
            EventCondition.Working => Expedition?.State == Expeditions.ExpeditionState.Working,
            EventCondition.HasFood => Inventory.HasFood,
            EventCondition.HasMeat => Inventory.CookedMeat.Count > 0 || Inventory.RawMeat.Count > 0,
            EventCondition.HasFirewood => Inventory.HasFuel,
            EventCondition.HasStones => false, // TODO: implement if stones become a resource
            EventCondition.Injured => player.Body.Parts.Any(p => p.Condition < 1.0),
            EventCondition.Bleeding => player.EffectRegistry.GetAll().Any(e => e.EffectKind.Equals("Bleeding", StringComparison.OrdinalIgnoreCase)),
            EventCondition.Slow => Bodies.CapacityCalculator.GetCapacities(player.Body, player.GetEffectModifiers()).Moving < 0.7,
            EventCondition.FireBurning => Camp.HasActiveFire,
            EventCondition.Inside => CurrentLocation.HasFeature<ShelterFeature>(),
            EventCondition.Outside => !Check(EventCondition.Inside),
            EventCondition.InAnimalTerritory => CurrentLocation.HasFeature<AnimalTerritoryFeature>(),
            EventCondition.HasPredators => CurrentLocation.GetFeature<AnimalTerritoryFeature>()?.HasPredators() ?? false,

            // Weather conditions
            EventCondition.IsSnowing => Zone.Weather.CurrentCondition is ZoneWeather.WeatherCondition.LightSnow
                                     or ZoneWeather.WeatherCondition.Blizzard,
            EventCondition.IsBlizzard => Zone.Weather.CurrentCondition == ZoneWeather.WeatherCondition.Blizzard,
            EventCondition.IsRaining => Zone.Weather.CurrentCondition == ZoneWeather.WeatherCondition.Rainy,
            EventCondition.IsStormy => Zone.Weather.CurrentCondition == ZoneWeather.WeatherCondition.Stormy,
            EventCondition.HighWind => Zone.Weather.WindSpeed > 0.6,
            EventCondition.IsClear => Zone.Weather.CurrentCondition == ZoneWeather.WeatherCondition.Clear,
            EventCondition.IsMisty => Zone.Weather.CurrentCondition == ZoneWeather.WeatherCondition.Misty,
            EventCondition.ExtremelyCold => Zone.Weather.BaseTemperature < -25,
            EventCondition.WeatherWorsening => Zone.Weather.WeatherJustChanged && IsWeatherWorsening(Zone.Weather),

            _ => false,
        };
    }

    /// <summary>
    /// Determines if weather transitioned to a worse condition.
    /// </summary>
    private static bool IsWeatherWorsening(ZoneWeather w)
    {
        if (w.PreviousCondition == null) return false;

        return (w.PreviousCondition, w.CurrentCondition) switch
        {
            // Any non-clear is worse than clear
            (ZoneWeather.WeatherCondition.Clear, _) when w.CurrentCondition != ZoneWeather.WeatherCondition.Clear => true,
            // Precipitation is worse than cloudy
            (ZoneWeather.WeatherCondition.Cloudy, ZoneWeather.WeatherCondition.LightSnow or ZoneWeather.WeatherCondition.Rainy
                or ZoneWeather.WeatherCondition.Blizzard or ZoneWeather.WeatherCondition.Stormy) => true,
            // Blizzard is worse than light snow
            (ZoneWeather.WeatherCondition.LightSnow, ZoneWeather.WeatherCondition.Blizzard) => true,
            // Storm is worse than rain
            (ZoneWeather.WeatherCondition.Rainy, ZoneWeather.WeatherCondition.Stormy) => true,
            _ => false
        };
    }
    public DateTime GameTime { get; set; } = new DateTime(2025, 1, 1, 9, 0, 0); // Full date/time for resource respawn tracking
    public SurvivalContext GetSurvivalContext() => new SurvivalContext
    {
        ActivityLevel = 1.5,
        LocationTemperature = CurrentLocation.GetTemperature(),
        ClothingInsulation = Inventory.TotalInsulation,
    };

    public void Update(int minutes)
    {
        player.Update(minutes, GetSurvivalContext());
        CurrentLocation.Parent.Update(minutes, GameTime);
        GameTime = GameTime.AddMinutes(minutes); // Keep GameTime in sync

        var logs = player?.GetFlushLogs();
        if (logs is not null && logs.Count != 0)
            GameDisplay.AddNarrative(logs);
    }

    public void Update(int minutes, double activityLevel)
    {
        var context = GetSurvivalContext();
        context.ActivityLevel = activityLevel;
        player.Update(minutes, context);
        CurrentLocation.Parent.Update(minutes, GameTime);
        GameTime = GameTime.AddMinutes(minutes);

        var logs = player?.GetFlushLogs();
        if (logs is not null && logs.Count != 0)
            GameDisplay.AddNarrative(logs);
    }

    public void Update(int minutes, double activityLevel, double fireProximityMultiplier)
    {
        var context = GetSurvivalContext();
        context.ActivityLevel = activityLevel;

        // Calculate fire proximity bonus if there's an active fire
        var fire = CurrentLocation.GetFeature<HeatSourceFeature>();
        if (fire != null && fire.IsActive)
        {
            double fireHeat = fire.GetEffectiveHeatOutput(CurrentLocation.GetTemperature());
            context.FireProximityBonus = fireHeat * fireProximityMultiplier;
        }

        player.Update(minutes, context);
        CurrentLocation.Parent.Update(minutes, GameTime);
        GameTime = GameTime.AddMinutes(minutes);

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
    InAnimalTerritory,
    HasPredators,

    // Weather conditions
    IsSnowing,
    IsBlizzard,
    IsRaining,
    IsStormy,
    HighWind,
    IsClear,
    IsMisty,
    ExtremelyCold,

    // Weather transitions
    WeatherWorsening,
}