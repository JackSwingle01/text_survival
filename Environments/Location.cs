using text_survival.Environments.Features;
using text_survival.Actors.NPCs;
using text_survival.Items;
using text_survival.Bodies;


namespace text_survival.Environments;

public class Location
{

    // Identity
    public string Name { get; }
    public string Description { get; set; } = "";
    public Zone Parent { get; }

    // Graph - just references, no wrapper
    public List<Location> Connections { get; } = [];

    // Environment
    public TerrainType Terrain { get; set; } = TerrainType.Clear;
    public double Exposure { get; set; } = 0.5;

    // Traversal - 0 for sites, >0 for paths
    public int BaseTraversalMinutes { get; set; } = 0;

    // Discovery
    public bool Explored { get; private set; } = false;

    // Features, items, etc. (unchanged)
    public List<LocationFeature> Features { get; } = [];
    public List<Item> Items { get; } = [];

    // Derived
    public bool IsPath => BaseTraversalMinutes > 0;
    public bool IsSite => BaseTraversalMinutes == 0;

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
    public IReadOnlyList<Npc> Npcs => _npcs.AsReadOnly();
    private List<Npc> _npcs = [];
    public List<BloodTrail> BloodTrails = []; // MVP Hunting System - Phase 4


    #region Initialization

    public Location(string name, Zone parent)
    {
        Name = name;
        Parent = parent;
        NpcSpawner = new();
    }

    public T? GetFeature<T>() where T : LocationFeature => Features.OfType<T>().FirstOrDefault();
    public bool HasFeature<T>() where T : LocationFeature => GetFeature<T>() is not null;

    public List<Location> GetUnexploredConnections()
        => Connections.Where(l => !l.Explored).ToList();

    public void SpawnNpcs(int numNpcs)
    {
        for (int i = 0; i < numNpcs; i++)
        {
            var npc = NpcSpawner.GenerateRandom();
            if (npc is not null)
            {
                _npcs.Add(npc);
            }
        }
    }

    public void RemoveNpc(Npc npc)
    {
        _npcs.Remove(npc);
    }

    public virtual NpcTable NpcSpawner { get; set; }


    #endregion Initialization

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
        SurvivalContext context = new()
        {
            ActivityLevel = 1,
            LocationTemperature = GetTemperature(),
        };
        _npcs.ForEach(n => n.Update(minutes, context));

        // Update location features (fires consume fuel, etc.)
        foreach (var feature in Features)
        {
            feature.Update(minutes);
        }
    }


    public void Explore()
    {
        Explored = true;
    }

    public string GetGatherSummary()
    {
        var parts = new List<string>();

        var forage = GetFeature<ForageFeature>();
        if (forage != null)
        {
            var categories = forage.GetTopCategories(3);  // "food, wood, stone"
            parts.Add($"foraging: {string.Join(", ", categories)}");
        }

        var harvestables = Features
            .OfType<HarvestableFeature>()
            .Where(h => h.IsDiscovered && h.HasAvailableResources())
            .Select(h => h.DisplayName);

        if (harvestables.Any())
            parts.AddRange(harvestables);

        return string.Join(" | ", parts);
    }
}
public enum TerrainType
{
    Clear,      // Easy travel
    Rough,      // Slower
    Snow,       // Weather dependent
    Steep,      // Directional difficulty
    Water,       // Requires crossing/swimming
    Hazardous
}