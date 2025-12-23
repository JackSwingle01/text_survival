using text_survival.Environments.Features;
using text_survival.UI;


namespace text_survival.Environments;

public class Location(string name, string tags, Zone parent, int traversalMinutes, double terrainHazardLevel = 0, double windFactor = 1, double overheadCoverLevel = 0, double visibilityFactor = 1)
{

    // Identity //
    public string Name { get; } = name;

    /// <summary>
    /// Short hints for the player. "[forest] [river] [wolves]"
    /// </summary>
    public string Tags { get; } = tags;
    

    public Zone ParentZone { get; } = parent;
    public List<Location> Connections { get; } = [];

    // Environment //
    public double WindFactor { get; } = windFactor;
    public double TemperatureDeltaF { get; } = 0;
    public double OverheadCoverLevel { get; } = overheadCoverLevel;

    /// <summary>
    /// How far you can see/be seen.
    /// 0-2: 0 = deep narrow cave, .5 = thick forest, 1 = open plain, 2 = high overlook
    /// </summary>
    public double VisibilityFactor { get; set; } = visibilityFactor;

    /// <summary>
    /// The traversal time can be thought of as the radius - the time in or out.
    /// </summary>
    public int BaseTraversalMinutes { get; set; } = traversalMinutes;

    /// <summary>
    /// Injury risk and traversal time modifier
    /// 0-1, 0 = grass, .5 = thick undergrowth, 1 = icy boulder field
    /// </summary>
    public double TerrainHazardLevel { get; set; } = terrainHazardLevel;

    /// <summary>
    /// Require fire or torch to work
    /// </summary>
    public bool IsDark { get; set; } = false;

    /// <summary>
    /// First-visit discovery text (flavor text shown once).
    /// </summary>
    public string? DiscoveryText { get; set; }

    // Discovery //
    public bool Explored { get; private set; } = false;
    public List<LocationFeature> Features { get; } = [];

    public void AddConnection(Location other)
    {
        if (!Connections.Contains(other))
            Connections.Add(other);
    }

    public void AddBidirectionalConnection(Location other)
    {
        AddConnection(other);
        other.AddConnection(this);
    }
    public List<BloodTrail> BloodTrails = []; // MVP Hunting System - Phase 4


    public T? GetFeature<T>() where T : LocationFeature => Features.OfType<T>().FirstOrDefault();
    public bool HasFeature<T>() where T : LocationFeature => GetFeature<T>() is not null;

    /// <summary>
    /// Remove a feature by type. Returns true if removed.
    /// </summary>
    public bool RemoveFeature<T>() where T : LocationFeature
    {
        var feature = GetFeature<T>();
        if (feature != null)
        {
            Features.Remove(feature);
            return true;
        }
        return false;
    }

    /// <summary>
    /// Remove a specific feature instance.
    /// </summary>
    public bool RemoveFeature(LocationFeature feature)
    {
        return Features.Remove(feature);
    }

    /// <summary>
    /// Add a feature to this location.
    /// </summary>
    public void AddFeature(LocationFeature feature)
    {
        Features.Add(feature);
    }

    /// <summary>
    /// Get the effective temperature at this location.
    /// </summary>
    /// <param name="isStationary">If true, apply structural shelter effects (resting, crafting).
    /// If false, only apply environmental shelter (foraging, hunting, traveling).</param>
    public double GetTemperature(bool isStationary = true)
    {
        // Get zone's weather temperature (in Fahrenheit)
        double zoneTemp = ParentZone.Weather.TemperatureInFahrenheit;

        // Start with this base temperature
        double locationTemp = zoneTemp;

        // ------ STEP 1: Apply inherent location modifiers ------
        locationTemp += TemperatureDeltaF;

        // ------ STEP 2: Apply weather exposure effects ------
        // Wind chill when windy
        double effectiveWindSpeed = 0;
        if (ParentZone.Weather.WindSpeed > 0.1) // Only significant wind
        {
            effectiveWindSpeed = ParentZone.Weather.WindSpeed * WindFactor;
            double windSpeedMph = effectiveWindSpeed * 30; // Scale 0-1 to approx mph
            locationTemp = CalculateWindChillNWS(locationTemp, windSpeedMph);
        }

        // Sun warming effects during daytime with clear skies
        double sunIntensity = ParentZone.Weather.SunlightIntensity;
        double sunExposure = 1 - OverheadCoverLevel;
        // Sun can add up to 10°F on a cold day
        double sunWarming = sunIntensity * sunExposure * 10;

        // Sun effect is more noticeable when cold
        double temperatureAdjustment = sunWarming * Math.Max(0.5, Math.Min(1, (50 - locationTemp) / 30));
        locationTemp += temperatureAdjustment;

        // Precipitation effects
        double precipitation = ParentZone.Weather.Precipitation;
        precipitation *= (1 - OverheadCoverLevel);
        // todo, determine if this effects temp directly or if we use this elsewhere
        double precipitationCooling = precipitation * 5; //  simple up to 5°F cooling for now
        locationTemp -= precipitationCooling;

        // ------ STEP 3: Apply shelter effects if present ------
        // Structural shelter only applies when stationary (resting, crafting, etc.)
        // When moving (foraging, hunting, traveling), you're not benefiting from the shelter
        double insulation = 0;
        var shelter = GetFeature<ShelterFeature>();
        if (shelter != null && isStationary)
        {
            // Start with minimum temperature a shelter can maintain (in °F)
            double minShelterTemp = 40; // About 4.4°C, what a good shelter can maintain from body heat
            // Calculate warming effect based on insulation quality
            double tempDifference = minShelterTemp - locationTemp;
            insulation = Math.Clamp(shelter.TemperatureInsulation, 0, .9); // cap at 90%
            insulation *= 1 - (precipitation * .3); // precipitation can reduce insulation up to 30%
            insulation *= 1 - (effectiveWindSpeed * .3); // and wind another 30 on top of that

            locationTemp += tempDifference * insulation;
        }

        // If there's a heat source, add its effect (including embers)
        // Heat sources benefit you regardless of activity - you're still in the area
        var heatSource = GetFeature<HeatSourceFeature>();
        if (heatSource != null)
        {
            // Insulation increases effectiveness of heat sources
            double effectiveHeat = heatSource.GetEffectiveHeatOutput(locationTemp);
            double heatEffect = effectiveHeat * Math.Max(insulation, .40); // heat sources are less effective outside
            locationTemp += heatEffect;
        }

        return locationTemp;
    }

    public double CalculateWindChillNWS(double temperatureF, double windSpeedMph)
    {
        // NWS formula is only valid for temperatures <= 50°F and wind speeds >= 3 mph
        if (temperatureF > 50 || windSpeedMph < 3)
        {
            return temperatureF;
        }
        // Calculate using the NWS Wind Chill Temperature (WCT) formula
        // https://www.weather.gov/media/epz/wxcalc/windChill.pdf
        // WCT = 35.74 + 0.6215T - 35.75(V^0.16) + 0.4275T(V^0.16)
        // Where T = Air Temperature (°F), V = Wind Speed (mph)
        double windPowFactor = Math.Pow(windSpeedMph, 0.16);
        return 35.74 + (0.6215 * temperatureF) - (35.75 * windPowFactor) + (0.4275 * temperatureF * windPowFactor);
    }

    public void Update(int minutes)
    {
        // Update location features (fires consume fuel, etc.)
        foreach (var feature in Features)
        {
            feature.Update(minutes);
        }
    }


    public void Explore()
    {
        GameDisplay.AddNarrative(Name + " - " + Tags); // todo first discovery detailed description
        Explored = true;
    }

    public string GetUnexploredHint(Actors.Player.Player player)
    {
        int minutes = TravelProcessor.GetTraversalMinutes(this, player);
        int rounded = ((minutes + 7) / 15) * 15;  // Round to nearest 15
        rounded = Math.Max(15, rounded);  // Minimum 15 min
        // todo 
        return $"??? (~{rounded} min)";
    }

    public string GetGatherSummary()
    {
        var parts = new List<string>();

        var forage = GetFeature<ForageFeature>();
        if (forage != null)
        {
            var resources = forage.GetAvailableResourceTypes();
            if (resources.Count > 0)
                parts.Add($"Foraging: {string.Join(", ", resources.Take(3))}");
        }

        var harvestables = Features
            .OfType<HarvestableFeature>()
            .Where(h => h.IsDiscovered && h.HasAvailableResources())
            .Select(h => h.DisplayName);

        if (harvestables.Any())
            parts.AddRange(harvestables);

        return string.Join(" | ", parts);
    }

    #region Description Helpers

    private string GetShelterClause(ShelterFeature s)
    {
        double quality = (s.TemperatureInsulation + s.OverheadCoverage + s.WindCoverage) / 3;
        string name = s.Name.ToLower();
        string article = "aeiou".Contains(name[0]) ? "an" : "a";
        return quality switch
        {
            >= 0.7 => $"well-sheltered by {article} {name}",
            >= 0.4 => $"beneath {article} {name}",
            _ => $"with only {article} {name} for cover"
        };
    }

    private string GetFireClause(HeatSourceFeature f)
    {
        if (!f.IsActive && f.HasEmbers) return "with glowing embers";
        return f.GetFirePhase() switch
        {
            "Igniting" => "with a small fire starting",
            "Building" => "with a growing fire",
            "Roaring" => "with a roaring fire",
            "Steady" => "with a steady fire",
            "Dying" => "with a dying fire",
            _ => "with a fire"
        };
    }

    #endregion
}