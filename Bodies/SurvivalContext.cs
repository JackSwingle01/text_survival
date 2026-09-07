using text_survival.Actions;
using text_survival.Actors;
using text_survival.Environments.Features;
using text_survival.Environments.Surface;
using static text_survival.Actions.GameContext;

namespace text_survival.Bodies;

public record SurvivalContext
{
    public double LocationTemperature { get; init; }
    public bool IsNight { get; init; }
    /// <summary>Whole-body clothing insulation in clo, area-weighted across covered slots.</summary>
    public double ClothingClo { get; init; }
    public double ActivityLevel { get; init; }
    /// <summary>Sleep is the one activity that restores energy rather than spending it.</summary>
    public bool IsSleeping { get; init; }
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

    /// <summary>
    /// Wetness picked up per minute from the ground - slush, mud, standing water - before
    /// clothing or bedding get a say. A separate source from rain: a roof stops one and does
    /// nothing about the other.
    /// </summary>
    public double GroundContactWettingPct { get; init; }

    /// <summary>What is between you and that ground: bedding, mostly. 0-1.</summary>
    public double GroundContactProtectionLevel { get; init; }

    // Bloody accumulation from bleeding
    public double CurrentBleedingPct { get; init; } // 0-1 from Bleeding effect
    public double CurrentBloodyPct { get; init; }   // 0-1 from Bloody effect

    // Clothing thermal mass
    public double ClothingWeightKg { get; init; }        // Total equipment weight for capacity calc
    public double ClothingHeatBuffer { get; init; }      // Current buffer level 0-1


    public static SurvivalContext GetSurvivalContext(Actor actor, Inventory? inventory, ActivityType activity, TimeOfDay timeOfDay)
    {
        double clothingClo = inventory?.ClothingClo ?? 0;

        // Get current wetness
        var wetEffect = actor.EffectRegistry.GetEffectsByKind("Wet").FirstOrDefault();
        double currentWetness = wetEffect?.Severity ?? 0;

        // Get bleeding and bloody severities for bloody accumulation
        double currentBleeding = actor.EffectRegistry.GetSeverity("Bleeding");
        double currentBloody = actor.EffectRegistry.GetSeverity("Bloody");

        // Wetness reduces insulation, but that is applied inside TotalThermalResistance -
        // the one place that knows how clothing, fat and air combine. Only the level travels.

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
        double waterproofingLevel = inventory?.CalculateWaterproofingLevel() ?? 0;

        var activityConfig = ActivityConfig.Get(activity);
        bool isNight = timeOfDay == TimeOfDay.Night;

        var contact = GroundContactFor(activity);
        double groundWetting = actor.CurrentLocation.Surface.GetContactWettingRate(contact, currentWetness);
        double groundProtection = 0;
        if (contact == SurfaceContact.RestingOnGround)
        {
            var bedding = actor.CurrentLocation.GetFeature<BeddingFeature>();
            if (bedding != null)
                groundProtection = Math.Clamp(0.5 + bedding.Quality * 0.4, 0, 0.95);
        }

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
        if (inventory?.HasLitTorch == true)
        {
            fireProximityBonus += inventory.GetTorchHeatBonusF();
        }

        // Ember carriers provide smaller warmth bonus (2-3°F vs torch's 3-5°F)
        fireProximityBonus += inventory?.GetEmberCarrierHeatBonusF() ?? 0;

        return new SurvivalContext
        {
            ActivityLevel = activityConfig.ActivityLevel,
            IsSleeping = activity == ActivityType.Sleeping,
            LocationTemperature = actor.CurrentLocation.GetTemperature(activity),
            ClothingClo = clothingClo,
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
            GroundContactWettingPct = groundWetting,
            GroundContactProtectionLevel = groundProtection,

            // Bloody accumulation context
            CurrentBleedingPct = currentBleeding,
            CurrentBloodyPct = currentBloody,

            // Clothing thermal mass
            ClothingWeightKg = inventory?.TotalEquipmentWeightKg ?? 0,
            ClothingHeatBuffer = actor.Body.ClothingHeatBufferPct,
        };
    }

    /// <summary>
    /// How this activity puts you in touch with the ground. Wading a flooded tile soaks you
    /// faster than kneeling on it, because you go through all of it rather than sit on some.
    /// </summary>
    private static SurfaceContact GroundContactFor(ActivityType activity) => activity switch
    {
        ActivityType.Sleeping => SurfaceContact.RestingOnGround,
        ActivityType.Resting => SurfaceContact.RestingOnGround,
        ActivityType.Incapacitated => SurfaceContact.RestingOnGround,

        ActivityType.Crafting => SurfaceContact.WorkingOnGround,
        ActivityType.Cooking => SurfaceContact.WorkingOnGround,
        ActivityType.Eating => SurfaceContact.WorkingOnGround,
        ActivityType.TendingFire => SurfaceContact.WorkingOnGround,
        ActivityType.Butchering => SurfaceContact.WorkingOnGround,
        ActivityType.Fishing => SurfaceContact.WorkingOnGround,

        _ => SurfaceContact.Walking
    };

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
