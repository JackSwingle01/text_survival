
using text_survival.Actors;
using text_survival.IO;
using text_survival.Items;

namespace text_survival.Environments;

public class Location
{
    public string Name;
    public bool Visited = false;
    public bool IsFound { get; set; } = false;
    public List<Npc> Npcs = [];
    public List<Item> Items = [];
    public List<Container> Containers = [];
    virtual public Zone Parent { get; }
    public List<LocationFeature> Features = [];

    #region Initialization

    public Location(string name, Zone parent)
    {
        Name = name;
        Parent = parent;
        NpcSpawner = new();
        // InitializeNpcs(numNpcs);
    }

    // public static readonly List<string> genericLocationAdjectives = ["", "Old", "Dusty", "Cool", "Breezy", "Quiet", "Ancient", "Ominous", "Sullen", "Forlorn", "Desolate", "Secret", "Hidden", "Forgotten", "Cold", "Dark", "Damp", "Wet", "Dry", "Warm", "Icy", "Snowy", "Frozen"];

    public T? GetFeature<T>() where T : LocationFeature => Features.OfType<T>().FirstOrDefault();

    public void SpawnNpcs(int numNpcs)
    {
        for (int i = 0; i < numNpcs; i++)
        {
            var npc = NpcSpawner.GenerateRandomNpc();
            if (npc is not null)
                Npcs.Add(npc);
        }
    }
    protected virtual NpcSpawner NpcSpawner { get; }


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
    public Command<Player> InteractCommand => new("Go to " + Name + (Visited ? " (Visited)" : ""), Interact);
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

        // If there's a heat source, add its effect
        var heatSource = GetFeature<HeatSourceFeature>();
        if (heatSource != null && heatSource.IsActive)
        {
            // Insulation increases effectiveness of heat sources
            double heatEffect = heatSource.HeatOutput * Math.Max(insulation, .40); // heat sources are less effective outside
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
        Npcs.ForEach(n => n.Update());
    }

    public override string ToString() => Name;
}