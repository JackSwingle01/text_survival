using System.Text.RegularExpressions;
using text_survival.Actions;
using text_survival.Bodies;
using text_survival.Effects;
using text_survival.Environments.Features;
using text_survival.Survival;
using text_survival.UI;

namespace text_survival.Web.Dto;

public record GameStateDto
{
    // Time
    public int DayNumber { get; init; }
    public string ClockTime { get; init; } = "";
    public string TimeOfDay { get; init; } = "";
    public bool IsDaytime { get; init; }
    public double HoursUntilTransition { get; init; }

    // Weather
    public string WeatherFront { get; init; } = "";
    public string WeatherCondition { get; init; } = "";
    public string Wind { get; init; } = "";
    public string Precipitation { get; init; } = "";

    // Season
    public string Season { get; init; } = "";
    public double SeasonIntensity { get; init; }

    // Location
    public string LocationName { get; init; } = "";
    public List<string> LocationTags { get; init; } = [];
    public List<FeatureDto> Features { get; init; } = [];

    // Fire
    public FireDto? Fire { get; init; }

    // Temperature
    public double BodyTemp { get; init; }
    public double AirTemp { get; init; }
    public double FireHeat { get; init; }
    public double TrendPerHour { get; init; }
    public string TempStatus { get; init; } = "";
    public TemperatureCrisisDto? TemperatureCrisis { get; init; }

    // Survival (0-100)
    public int HealthPercent { get; init; }
    public int FoodPercent { get; init; }
    public int WaterPercent { get; init; }
    public int EnergyPercent { get; init; }

    // Condition
    public List<EffectDto> Effects { get; init; } = [];
    public List<InjuryDto> Injuries { get; init; } = [];
    public int? BloodPercent { get; init; }

    // Inventory summary
    public double CarryWeightKg { get; init; }
    public double MaxWeightKg { get; init; }
    public int InsulationPercent { get; init; }
    public double FuelKg { get; init; }
    public string FuelBurnTime { get; init; } = "";
    public GearSummaryDto? GearSummary { get; init; }

    // Storage availability
    public bool HasStorage { get; init; }

    // Narrative log
    public List<LogEntryDto> Log { get; init; } = [];

    // Active tensions (atmospheric display)
    public List<TensionDto> Tensions { get; init; } = [];

    // CSS variable hints (0-1 range)
    public double Warmth { get; init; }
    public double Vitality { get; init; }

    // Debug: raw capacity values
    public Dictionary<string, double> DebugCapacities { get; init; } = [];
    public Dictionary<string, double> DebugEffectModifiers { get; init; } = [];

    public static GameStateDto FromContext(GameContext ctx)
    {
        var body = ctx.player.Body;
        var location = ctx.CurrentLocation;
        var weather = location.Weather;
        var fire = location.GetFeature<HeatSourceFeature>();
        var inventory = ctx.Inventory;

        // Time calculations
        var startDate = new DateTime(2025, 1, 1);
        int dayNumber = (ctx.GameTime - startDate).Days + 1;
        bool isDaytime = weather.IsDaytime(ctx.GameTime);
        double hoursUntilTransition = isDaytime
            ? weather.GetHoursUntilSunset(ctx.GameTime)
            : weather.GetHoursUntilSunrise(ctx.GameTime);

        // Temperature calculations
        double zoneTemp = weather.TemperatureInFahrenheit;
        double locationTemp = location.GetTemperature();
        double fireHeat = fire?.GetEffectiveHeatOutput(zoneTemp) ?? 0;

        // Calculate trend per hour from last survival delta
        double trendPerHour = 0;
        if (ctx.player.LastSurvivalDelta?.TemperatureDelta is double delta && ctx.player.LastUpdateMinutes > 0)
        {
            trendPerHour = (delta / ctx.player.LastUpdateMinutes) * 60;
        }

        // Survival percentages
        int caloriesPercent = (int)(body.CalorieStore / SurvivalProcessor.MAX_CALORIES * 100);
        int hydrationPercent = (int)(body.Hydration / SurvivalProcessor.MAX_HYDRATION * 100);
        int energyPercent = (int)(body.Energy / SurvivalProcessor.MAX_ENERGY_MINUTES * 100);
        int healthPercent = ctx.player.IsAlive
            ? Math.Max(1, (int)Math.Ceiling(ctx.player.Vitality * 100))
            : 0;

        // Blood status
        int? bloodPercent = body.Blood.Condition < 0.95
            ? (int)(body.Blood.Condition * 100)
            : null;

        // Warmth CSS variable: 90F = 0, 102F = 1
        double warmth = Math.Clamp((body.BodyTemperature - 90) / 12, 0, 1);

        // Get log entries from context's log
        var logEntries = ctx.Log.GetVisible()
            .Select(e => new LogEntryDto(e.Text, e.Level.ToString().ToLowerInvariant(), e.Timestamp))
            .ToList();

        return new GameStateDto
        {
            // Time
            DayNumber = dayNumber,
            ClockTime = ctx.GameTime.ToString("h:mm tt"),
            TimeOfDay = ctx.GetTimeOfDay().ToString(),
            IsDaytime = isDaytime,
            HoursUntilTransition = hoursUntilTransition,

            // Weather
            WeatherFront = weather.GetFrontLabel(),
            WeatherCondition = weather.GetConditionLabel(),
            Wind = weather.GetWindLabel(),
            Precipitation = weather.GetPrecipitationLabel(),

            // Season
            Season = weather.GetSeasonLabel(),
            SeasonIntensity = weather.SeasonalIntensity,

            // Location
            LocationName = location.Name,
            LocationTags = ParseTags(location.Tags),
            Features = ExtractFeatures(location),

            // Fire
            Fire = ExtractFire(fire, zoneTemp),

            // Temperature
            BodyTemp = body.BodyTemperature,
            AirTemp = locationTemp,
            FireHeat = fireHeat,
            TrendPerHour = trendPerHour,
            TempStatus = GetTemperatureStatus(body.BodyTemperature),
            TemperatureCrisis = DetectTemperatureCrisis(body.BodyTemperature, trendPerHour, ctx),

            // Survival
            HealthPercent = healthPercent,
            FoodPercent = caloriesPercent,
            WaterPercent = hydrationPercent,
            EnergyPercent = energyPercent,

            // Condition
            Effects = ExtractEffects(ctx.player.EffectRegistry),
            Injuries = ExtractInjuries(body),
            BloodPercent = bloodPercent,

            // Inventory
            CarryWeightKg = inventory.CurrentWeightKg,
            MaxWeightKg = inventory.MaxWeightKg,
            InsulationPercent = (int)(inventory.TotalInsulation * 100),
            FuelKg = inventory.GetWeight(ResourceCategory.Fuel),
            FuelBurnTime = inventory.TorchBurnTimeRemainingMinutes >= 60
                ? $"{(int)inventory.TorchBurnTimeRemainingMinutes / 60}hrs"
                : $"{(int)inventory.TorchBurnTimeRemainingMinutes}min",
            GearSummary = GearSummaryHelper.ComputeGearSummary(inventory),

            // Storage - available when current location has a cache
            HasStorage = location.GetFeature<CacheFeature>() != null,

            // Narrative
            Log = logEntries,

            // Tensions (atmospheric)
            Tensions = ExtractTensions(ctx.Tensions),

            // CSS hints
            Warmth = warmth,
            Vitality = ctx.player.Vitality,

            // Debug: capture raw values
            DebugCapacities = new Dictionary<string, double>
            {
                ["Moving"] = ctx.player.GetCapacities().Moving,
                ["Manipulation"] = ctx.player.GetCapacities().Manipulation,
                ["Consciousness"] = ctx.player.GetCapacities().Consciousness,
                ["Breathing"] = ctx.player.GetCapacities().Breathing,
                ["BloodPumping"] = ctx.player.GetCapacities().BloodPumping,
            },
            DebugEffectModifiers = new Dictionary<string, double>
            {
                ["Moving"] = ctx.player.GetEffectModifiers().GetCapacityModifier(CapacityNames.Moving),
                ["Manipulation"] = ctx.player.GetEffectModifiers().GetCapacityModifier(CapacityNames.Manipulation),
                ["Consciousness"] = ctx.player.GetEffectModifiers().GetCapacityModifier(CapacityNames.Consciousness),
                ["Breathing"] = ctx.player.GetEffectModifiers().GetCapacityModifier(CapacityNames.Breathing),
                ["BloodPumping"] = ctx.player.GetEffectModifiers().GetCapacityModifier(CapacityNames.BloodPumping),
            }
        };
    }

    private static List<FeatureDto> ExtractFeatures(Environments.Location location)
    {
        var features = new List<FeatureDto>();

        var forage = location.GetFeature<ForageFeature>();
        if (forage != null)
        {
            var resources = forage.GetAvailableResourceTypes();
            if (resources.Count > 0)
                features.Add(new FeatureDto("forage", "Forage", string.Join(", ", resources.Take(3))));
        }

        var territory = location.GetFeature<AnimalTerritoryFeature>();
        if (territory != null)
        {
            features.Add(new FeatureDto("game", "Game", territory.GetDescription()));
        }

        var water = location.GetFeature<WaterFeature>();
        if (water != null)
        {
            features.Add(new FeatureDto("water", "Water", null));
        }

        var shelter = location.GetFeature<ShelterFeature>();
        if (shelter != null)
        {
            features.Add(new FeatureDto("shelter", shelter.Name,
                $"ins:{shelter.TemperatureInsulation * 100:F0}%, wind:{shelter.WindCoverage * 100:F0}%"));
        }

        return features;
    }

    private static FireDto? ExtractFire(HeatSourceFeature? fire, double zoneTemp)
    {
        if (fire == null)
            return null;

        string phase;
        int minutesRemaining;

        if (!fire.IsActive && !fire.HasEmbers)
        {
            phase = "Cold";
            minutesRemaining = 0;
        }
        else if (fire.HasEmbers)
        {
            phase = "Embers";
            minutesRemaining = (int)(fire.EmberTimeRemaining * 60);
        }
        else
        {
            phase = fire.GetFirePhase();
            // Use total fuel time when catching, otherwise burning time
            minutesRemaining = fire.UnburnedMassKg > 0.1
                ? (int)(fire.TotalHoursRemaining * 60)
                : (int)(fire.BurningHoursRemaining * 60);
        }

        // Calculate urgency based on minutes remaining
        var urgency = minutesRemaining switch
        {
            < 10 => FireUrgency.Critical,
            < 30 => FireUrgency.Warning,
            < 60 => FireUrgency.Caution,
            _ => FireUrgency.Safe
        };

        return new FireDto(
            Phase: phase,
            MinutesRemaining: minutesRemaining,
            BurningKg: fire.BurningMassKg,
            UnlitKg: fire.UnburnedMassKg,
            TotalKg: fire.TotalMassKg,
            MaxCapacityKg: fire.MaxFuelCapacityKg,
            HeatOutput: fire.GetEffectiveHeatOutput(zoneTemp),
            BurnRateKgPerHour: fire.EffectiveBurnRateKgPerHour,
            Urgency: urgency
        );
    }

    private static List<EffectDto> ExtractEffects(EffectRegistry registry)
    {
        return registry.GetAll()
            .Take(6)
            .Select(e =>
            {
                // Extract capacity impacts (non-zero modifiers only)
                var capacityImpacts = new Dictionary<string, int>();
                foreach (var capName in CapacityNames.All)
                {
                    var mod = e.CapacityModifiers.GetCapacityModifier(capName);
                    if (Math.Abs(mod) > 0.001)
                    {
                        // Apply severity and convert to percent
                        capacityImpacts[capName] = (int)(mod * e.Severity * 100);
                    }
                }

                // Extract stats impact (per-minute -> per-hour)
                EffectStatsDto? statsImpact = null;
                var sd = e.StatsDelta;
                if (sd.TemperatureDelta != 0 || sd.CalorieDelta != 0 ||
                    sd.HydrationDelta != 0 || sd.EnergyDelta != 0 || e.Damage != null)
                {
                    statsImpact = new EffectStatsDto(
                        TemperaturePerHour: sd.TemperatureDelta != 0 ? sd.TemperatureDelta * 60 * e.Severity : null,
                        CaloriesPerHour: sd.CalorieDelta != 0 ? sd.CalorieDelta * 60 * e.Severity : null,
                        HydrationPerHour: sd.HydrationDelta != 0 ? sd.HydrationDelta * 60 * e.Severity : null,
                        EnergyPerHour: sd.EnergyDelta != 0 ? sd.EnergyDelta * 60 * e.Severity : null,
                        DamagePerHour: e.Damage?.PerHour * e.Severity,
                        DamageType: e.Damage?.Type.ToString()
                    );
                }

                return new EffectDto(
                    Name: e.EffectKind,
                    SeverityPercent: Math.Max(1, (int)Math.Ceiling(e.Severity * 100)),
                    Trend: e.HourlySeverityChange > 0 ? "worsening"
                         : e.HourlySeverityChange < 0 ? "improving"
                         : "stable",
                    CapacityImpacts: capacityImpacts,
                    StatsImpact: statsImpact,
                    RequiresTreatment: e.RequiresTreatment
                );
            })
            .ToList();
    }

    private static List<InjuryDto> ExtractInjuries(Body body)
    {
        var injuries = new List<InjuryDto>();

        // Organ damage
        var damagedOrgans = body.Parts
            .SelectMany(p => p.Organs)
            .Where(o => o.Condition < 0.95)
            .OrderBy(o => o.Condition)
            .Take(3);

        foreach (var organ in damagedOrgans)
        {
            // Get capacities this organ contributes to
            var affectedCapacities = CapacityNames.All
                .Where(c => organ._baseCapacities.GetCapacity(c) > 0)
                .ToList();

            injuries.Add(new InjuryDto(
                PartName: organ.Name,
                ConditionPercent: (int)(organ.Condition * 100),
                IsOrgan: true,
                AffectedCapacities: affectedCapacities
            ));
        }

        // Tissue damage (body regions affect Moving and Manipulation via muscle)
        var damagedParts = body.Parts
            .Where(p => p.Condition < 0.95)
            .OrderBy(p => p.Condition)
            .Take(4);

        foreach (var part in damagedParts)
        {
            injuries.Add(new InjuryDto(
                PartName: part.Name,
                ConditionPercent: (int)(part.Condition * 100),
                IsOrgan: false,
                AffectedCapacities: [CapacityNames.Moving, CapacityNames.Manipulation]
            ));
        }

        return injuries;
    }

    private static string GetTemperatureStatus(double temp) => temp switch
    {
        >= 100 => "Feverish",
        >= 99 => "Hot",
        >= 97 => "Normal",
        >= 95 => "Cool",
        _ => "Cold"
    };

    private static TemperatureCrisisDto? DetectTemperatureCrisis(double bodyTemp, double trendPerHour, GameContext ctx)
    {
        const double DANGER_THRESHOLD = 94.0;
        const double HYPOTHERMIA_DAMAGE_THRESHOLD = 90.0;

        // Check if in crisis (body temp below danger threshold)
        var hasDeadlyColdTension = ctx.Tensions.GetAllTensions().Any(t => t.Type == "DeadlyCold");

        if (bodyTemp >= DANGER_THRESHOLD && !hasDeadlyColdTension)
        {
            return null; // No crisis
        }

        // Calculate minutes until hypothermia damage starts (if trending down)
        int? minutesUntilDamage = null;
        if (trendPerHour < 0 && bodyTemp > HYPOTHERMIA_DAMAGE_THRESHOLD)
        {
            var tempDropNeeded = bodyTemp - HYPOTHERMIA_DAMAGE_THRESHOLD;
            var hoursUntilDamage = tempDropNeeded / Math.Abs(trendPerHour);
            minutesUntilDamage = (int)(hoursUntilDamage * 60);
        }

        // Determine action guidance
        var guidance = bodyTemp < 92
            ? "Critical hypothermia - reach fire immediately"
            : hasDeadlyColdTension
                ? "Deadly cold active - seek fire or shelter"
                : "Body temperature dangerously low - find warmth";

        return new TemperatureCrisisDto(
            CurrentTemp: bodyTemp,
            DangerThreshold: DANGER_THRESHOLD,
            TrendPerHour: trendPerHour,
            MinutesUntilDamage: minutesUntilDamage,
            ActionGuidance: guidance
        );
    }

    private static List<string> ParseTags(string? tagString)
    {
        if (string.IsNullOrEmpty(tagString)) return [];
        var matches = Regex.Matches(tagString, @"\[([^\]]+)\]");
        return matches.Select(m => m.Groups[1].Value).ToList();
    }

    private static List<TensionDto> ExtractTensions(Actions.Tensions.TensionRegistry registry)
    {
        return registry.GetAllTensions()
            .Select(t => new TensionDto(
                Message: Actions.Tensions.TensionDisplay.GetMessage(t),
                Category: Actions.Tensions.TensionDisplay.GetCategory(t.Type)
            ))
            .ToList();
    }
}

public record FeatureDto(string Type, string Label, string? Detail);

public enum FireUrgency
{
    Safe,       // 60+ minutes
    Caution,    // 30-60 minutes
    Warning,    // 10-30 minutes
    Critical    // < 10 minutes
}

public record FireDto(
    string Phase,
    int MinutesRemaining,
    double BurningKg,
    double UnlitKg,
    double TotalKg,
    double MaxCapacityKg,
    double HeatOutput,
    double BurnRateKgPerHour,
    FireUrgency Urgency
);

public record EffectDto(
    string Name,
    int SeverityPercent,
    string Trend,
    Dictionary<string, int> CapacityImpacts,
    EffectStatsDto? StatsImpact,
    bool RequiresTreatment
);

public record EffectStatsDto(
    double? TemperaturePerHour,
    double? CaloriesPerHour,
    double? HydrationPerHour,
    double? EnergyPerHour,
    double? DamagePerHour,
    string? DamageType
);

public record InjuryDto(
    string PartName,
    int ConditionPercent,
    bool IsOrgan,
    List<string> AffectedCapacities
);

public record LogEntryDto(string Text, string Level, string Timestamp);

public record TensionDto(string Message, string Category);

public record TemperatureCrisisDto(
    double CurrentTemp,
    double DangerThreshold,
    double TrendPerHour,
    int? MinutesUntilDamage,
    string ActionGuidance
);
