using text_survival.Actions;
using text_survival.Actors;
using text_survival.Environments.Features;
using static text_survival.Actions.GameContext;

namespace text_survival.Bodies;

public record SurvivalContext
{
    public double LocationTemperature { get; init; }
    public bool IsNight { get; init; }
    public double ClothingInsulation { get; init; }
    public double ActivityLevel { get; init; }
    public double FireProximityBonus { get; init; } // Direct radiant heat from fire proximity (0-2 scale multiplied by fire heat)

    // Wetness system context
    public double OverheadCoverLevel { get; init; }
    public double PrecipitationPct { get; init; }
    public double WindSpeedLevel { get; init; }
    public bool IsRaining { get; init; }
    public bool IsSnowing { get; init; }                 // Weather condition flag (light snow)
    public bool IsBlizzard { get; init; }
    public double CurrentWetnessPct { get; init; }  // 0-1 current wetness from effect

    // Waterproofing from resin-treated equipment (0-1 scale)
    public double WaterproofingLevel { get; init; }      // Reduces wetness accumulation by this factor

    // Bloody accumulation from bleeding
    public double CurrentBleedingPct { get; init; } // 0-1 from Bleeding effect
    public double CurrentBloodyPct { get; init; }   // 0-1 from Bloody effect

    // Clothing thermal mass
    public double ClothingWeightKg { get; init; }        // Total equipment weight for capacity calc
    public double ClothingHeatBuffer { get; init; }      // Current buffer level 0-1


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
        bool isStationary = ActivityConfig.IsStationary(activity);
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
            double fireHeat = fire.GetEffectiveHeatOutput(actor.CurrentLocation.GetTemperature(activity));
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
            LocationTemperature = actor.CurrentLocation.GetTemperature(activity),
            ClothingInsulation = clothingInsulation,
            FireProximityBonus = fireProximityBonus,
            IsNight = isNight,


            // Wetness context
            OverheadCoverLevel = overheadCover,
            PrecipitationPct = actor.CurrentLocation.Weather.PrecipitationPct,
            WindSpeedLevel = CalculateEffectiveWindSpeed(actor.CurrentLocation),
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

    private static double CalculateEffectiveWindSpeed(Environments.Location location)
    {
        double baseWind = location.Weather.WindSpeedPct;
        var fire = location.GetFeature<HeatSourceFeature>();

        // Active fire with wind protection reduces effective wind
        if (fire != null && fire.IsActive)
        {
            double protection = fire.WindProtectionFactor;
            return baseWind * (1 - protection);
        }

        return baseWind;
    }
}
