using text_survival.Actions;
using text_survival.Actors;
using text_survival.Environments.Features;
using static text_survival.Actions.GameContext;

namespace text_survival.Bodies;

public class SurvivalContext
{
    public double LocationTemperature;
    public bool IsNight;
    public double ClothingInsulation;
    public double ActivityLevel;
    public double FireProximityBonus; // Direct radiant heat from fire proximity (0-2 scale multiplied by fire heat)

    // Wetness system context
    public double OverheadCoverLevel;
    public double PrecipitationPct;
    public double WindSpeedLevel;
    public bool IsRaining;
    public bool IsSnowing;                 // Weather condition flag (light snow)
    public bool IsBlizzard;
    public double CurrentWetnessPct;  // 0-1 current wetness from effect

    // Waterproofing from resin-treated equipment (0-1 scale)
    public double WaterproofingLevel;      // Reduces wetness accumulation by this factor

    // Bloody accumulation from bleeding
    public double CurrentBleedingPct; // 0-1 from Bleeding effect
    public double CurrentBloodyPct;   // 0-1 from Bloody effect

    // Clothing thermal mass
    public double ClothingWeightKg;        // Total equipment weight for capacity calc
    public double ClothingHeatBuffer;      // Current buffer level 0-1


    public static SurvivalContext GetSurvivalContext(Actor actor, Inventory inventory, ActivityType activity, TimeOfDay timeOfDay)
    {
        double clothingInsulation = inventory.TotalInsulation;

        // Get current wetness
        var wetEffect = actor.EffectRegistry.GetEffectsByKind("Wet").FirstOrDefault();
        double currentWetness = wetEffect?.Severity ?? 0;

        // Get bleeding and bloody severities for bloody accumulation
        double currentBleeding = actor.EffectRegistry.GetSeverity("Bleeding");
        double currentBloody = actor.EffectRegistry.GetSeverity("Bloody");

        // Wetness reduces insulation effectiveness
        if (wetEffect != null)
        {
            // At full wetness (severity 1.0), clothing loses 70% effectiveness
            double insulationLossFactor = wetEffect.Severity * 0.70;
            clothingInsulation *= (1 - insulationLossFactor);
        }

        // Calculate overhead cover (environmental + shelter if stationary)
        bool isStationary = IsActivityStationary(activity);
        double overheadCover = actor.CurrentLocation.OverheadCoverLevel;
        if (isStationary)
        {
            var shelter = actor.CurrentLocation.GetFeature<ShelterFeature>();
            if (shelter != null)
                overheadCover = Math.Max(overheadCover, shelter.OverheadCoverage);
        }

        // Extract weather conditions
        bool isRaining = actor.CurrentLocation.Weather.CurrentCondition == Weather.WeatherCondition.Rainy ||
                         actor.CurrentLocation.Weather.CurrentCondition == Weather.WeatherCondition.Stormy;
        bool isBlizzard = actor.CurrentLocation.Weather.CurrentCondition == Weather.WeatherCondition.Blizzard;
        bool isSnowing = actor.CurrentLocation.Weather.CurrentCondition == Weather.WeatherCondition.LightSnow;

        // Calculate waterproofing level from resin-treated equipment
        double waterproofingLevel = inventory.CalculateWaterproofingLevel();

        var activityConfig = ActivityConfig.Get(activity);
        bool isNight = timeOfDay == TimeOfDay.Night;

        // Calculate fire proximity bonus if there's an active fire
        // Skip if hyperthermic - player would back away from fire
        double fireProximityBonus = 0;
        var fire = actor.CurrentLocation.GetFeature<HeatSourceFeature>();
        if (fire != null && fire.IsActive && !actor.EffectRegistry.HasEffect("Hyperthermia"))
        {
            double fireHeat = fire.GetEffectiveHeatOutput(actor.CurrentLocation.GetTemperature(IsActivityStationary(activity)));
            double fireProximityMultiplier = activityConfig.FireProximity;
            fireProximityBonus = fireHeat * fireProximityMultiplier;
        }

        // Torch provides warmth during expeditions (when away from fire)
        if (inventory.HasLitTorch)
        {
            fireProximityBonus += inventory.GetTorchHeatBonusF();
        }

        // Ember carriers provide smaller warmth bonus (2-3°F vs torch's 3-5°F)
        fireProximityBonus += inventory.GetEmberCarrierHeatBonusF();

        return new SurvivalContext
        {
            ActivityLevel = activityConfig.ActivityLevel,
            LocationTemperature = actor.CurrentLocation.GetTemperature(isStationary),
            ClothingInsulation = clothingInsulation,
            FireProximityBonus = fireProximityBonus,
            IsNight = isNight,


            // Wetness context
            OverheadCoverLevel = overheadCover,
            PrecipitationPct = actor.CurrentLocation.Weather.Precipitation,
            WindSpeedLevel = actor.CurrentLocation.Weather.WindSpeed,
            IsRaining = isRaining,
            IsSnowing = isSnowing,
            IsBlizzard = isBlizzard,
            CurrentWetnessPct = currentWetness,
            WaterproofingLevel = waterproofingLevel,

            // Bloody accumulation context
            CurrentBleedingPct = currentBleeding,
            CurrentBloodyPct = currentBloody,

            // Clothing thermal mass
            ClothingWeightKg = inventory.TotalEquipmentWeightKg,
            ClothingHeatBuffer = actor.Body.ClothingHeatBufferPct,
        };
    }

    public static bool IsActivityStationary(ActivityType activity) => activity switch
    {
        // Stationary activities - shelter applies
        ActivityType.Idle => true,
        ActivityType.Fighting => true,
        ActivityType.Encounter => true,
        ActivityType.Sleeping => true,
        ActivityType.Resting => true,
        ActivityType.TendingFire => true,
        ActivityType.Eating => true,
        ActivityType.Cooking => true,
        ActivityType.Crafting => true,

        // Moving activities - no structural shelter
        ActivityType.Traveling => false,
        ActivityType.Foraging => false,
        ActivityType.Hunting => false,
        ActivityType.Exploring => false,
        ActivityType.Chopping => false,

        _ => true // Default to stationary
    };
}
