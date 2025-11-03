
using text_survival.Actors;
using text_survival.IO;
using text_survival.Items;

namespace text_survival.Environments;

public class Location
{
    public string Name;
    public bool Visited = false;
    public bool IsFound { get; set; } = false;
    public IReadOnlyList<Npc> Npcs =>_npcs.AsReadOnly();
    private List<Npc> _npcs = [];
    public List<Item> Items = [];
    public List<Container> Containers = [];
    public List<BloodTrail> BloodTrails = []; // MVP Hunting System - Phase 4
    virtual public Zone Parent { get; }
    public List<LocationFeature> Features = [];

    // Map UI coordinates
    public int CoordinateX { get; set; } = 0;
    public int CoordinateY { get; set; } = 0;

    #region Initialization

    public Location(string name, Zone parent)
    {
        Name = name;
        Parent = parent;
        NpcSpawner = new();
    }

    public T? GetFeature<T>() where T : LocationFeature => Features.OfType<T>().FirstOrDefault();

    public void SpawnNpcs(int numNpcs)
    {
        for (int i = 0; i < numNpcs; i++)
        {
            var npc = NpcSpawner.GenerateRandom();
            if (npc is not null)
            {
                _npcs.Add(npc);
                npc.CurrentLocation = this;
            }
        }
    }

    public void RemoveNpc(Npc npc)
    {
        _npcs.Remove(npc);
    }

    public virtual NpcTable NpcSpawner { get; set; }


    #endregion Initialization
    public void Interact(Player player)
    {
        Output.WriteLine("You consider heading to the " + Name + "...");
        Output.WriteLine("Do you want to go there? (y/n)");
        if (Input.ReadYesNo())
        {
            player.CurrentLocation = this;
        }
        else
        {
            Output.WriteLine("You decide to stay.");
        }
    }
    
    public double GetTemperature()
    {
        // Get zone's weather temperature (in Fahrenheit)
        double zoneTemp = Parent.Weather.TemperatureInFahrenheit;

        // Start with this base temperature
        double locationTemp = zoneTemp;

        // ------ STEP 1: Apply inherent location modifiers ------
        double overheadCoverage = 0;
        double windProtection = 0;
        var locationType = GetFeature<EnvironmentFeature>();
        if (locationType != null)
        {
            locationTemp += locationType.TemperatureModifier;
            overheadCoverage = locationType.NaturalOverheadCoverage;
            windProtection = locationType.NaturalWindProtection;
        }

        // ------ STEP 2: Apply weather exposure effects ------
        // Wind chill when windy
        double effectiveWindSpeed = 0;
        if (Parent.Weather.WindSpeed > 0.1) // Only significant wind
        {
            effectiveWindSpeed = Parent.Weather.WindSpeed * (1 - windProtection);
            double windSpeedMph = effectiveWindSpeed * 30; // Scale 0-1 to approx mph
            locationTemp = CalculateWindChillNWS(locationTemp, windSpeedMph);
        }

        // Sun warming effects during daytime with clear skies
        double sunIntensity = Parent.Weather.SunlightIntensity;
        double sunExposure = 1 - overheadCoverage;
        // Sun can add up to 10°F on a cold day
        double sunWarming = sunIntensity * sunExposure * 10;

        // Sun effect is more noticeable when cold
        double temperatureAdjustment = sunWarming * Math.Max(0.5, Math.Min(1, (50 - locationTemp) / 30));
        locationTemp += temperatureAdjustment;

        // Precipitation effects
        double precipitation = Parent.Weather.Precipitation;
        precipitation *= 1 - overheadCoverage;
        // todo, determine if this effects temp directly or if we use this elsewhere 
        double precipitationCooling = precipitation * 5; //  simple up to 5°F cooling for now
        locationTemp -= precipitationCooling * (1 - overheadCoverage);

        // ------ STEP 3: Apply shelter effects if present ------
        double insulation = 0;
        var shelter = GetFeature<ShelterFeature>();
        if (shelter != null)
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
        var heatSource = GetFeature<HeatSourceFeature>();
        if (heatSource != null)
        {
            // Insulation increases effectiveness of heat sources
            double effectiveHeat = heatSource.GetEffectiveHeatOutput();
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

    public void Update()
    {
        // Locations.ForEach(i => i.Update());
        _npcs.ForEach(n => n.Update());

        // Update location features (fires consume fuel, etc.)
        foreach (var feature in Features)
        {
            if (feature is HeatSourceFeature heatSource)
            {
                heatSource.Update(TimeSpan.FromMinutes(1));
            }
        }
    }

    public List<Location> GetNearbyLocations()
    {
        List<Location> nearbyLocations = [];
        if (Parent.Locations.Count > 0)
        {
            foreach (var location in Parent.Locations)
            {
                if (location == this)
                    continue;
                nearbyLocations.Add(location);
            }
        }
        return nearbyLocations;
    }

    #region Map UI Helpers

    /// <summary>Returns fire status string for map display, or null if no fire</summary>
    public string? GetActiveFireStatus()
    {
        var heatSource = GetFeature<HeatSourceFeature>();
        if (heatSource == null) return null;

        var phase = heatSource.GetFirePhase();
        if (phase == "Cold") return null;

        string fireIcon = "🔥";
        if (phase == "Embers")
        {
            return $"{fireIcon} Embers ({heatSource.EmberTimeRemaining:F0}m)";
        }
        else if (phase == "Dying")
        {
            return $"{fireIcon} Dying ({heatSource.FuelRemaining:F1}h)";
        }
        else if (heatSource.IsActive)
        {
            return $"{fireIcon} Burning ({heatSource.FuelRemaining:F1}h)";
        }

        return null;
    }

    /// <summary>Returns shelter status string for map display, or null if no shelter</summary>
    public string? GetShelterStatus()
    {
        var shelter = GetFeature<ShelterFeature>();
        if (shelter == null) return null;

        double tempBonus = GetTemperature() - Parent.Weather.TemperatureInFahrenheit;
        return $"🏠 Shelter: +{tempBonus:F0}°F";
    }

    /// <summary>Returns nearby threats string for map display, or null if none</summary>
    public string? GetNearbyThreats()
    {
        if (_npcs.Count == 0) return null;

        var hostileNpcs = _npcs.Where(n => n.IsHostile).ToList();
        if (hostileNpcs.Count == 0) return null;

        if (hostileNpcs.Count == 1)
        {
            return $"⚠ {hostileNpcs[0].Name}";
        }
        else
        {
            return $"⚠ {hostileNpcs.Count} threats";
        }
    }

    /// <summary>Returns wildlife traces string for map display, or null if none</summary>
    public string? GetWildlifeTraces()
    {
        if (BloodTrails.Count > 0)
        {
            var freshTrails = BloodTrails.Where(t => t.GetFreshness() > 0.1).ToList();
            if (freshTrails.Count > 0)
            {
                return $"🩸 {freshTrails.Count} blood trail(s)";
            }
        }

        // Check for non-hostile NPCs (wildlife)
        var wildlife = _npcs.Where(n => !n.IsHostile).ToList();
        if (wildlife.Count > 0)
        {
            if (wildlife.Count == 1)
            {
                return $"🦌 {wildlife[0].Name} seen";
            }
            else
            {
                return $"🦌 {wildlife.Count} animals";
            }
        }

        return null;
    }

    /// <summary>Returns item summary string for map display, or null if no items</summary>
    public string? GetItemSummary()
    {
        if (Items.Count == 0 && Containers.Count == 0) return null;

        List<string> summary = [];

        if (Items.Count > 0)
        {
            if (Items.Count <= 2)
            {
                summary.Add(string.Join(", ", Items.Select(i => i.Name)));
            }
            else
            {
                summary.Add($"{Items.Count} items");
            }
        }

        if (Containers.Count > 0)
        {
            summary.Add($"{Containers.Count} container(s)");
        }

        return summary.Count > 0 ? $"📦 {string.Join(", ", summary)}" : null;
    }

    #endregion Map UI Helpers

    public override string ToString() => Name;
}