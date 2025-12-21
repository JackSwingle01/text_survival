using text_survival.Actions;
using text_survival.Environments.Features;

namespace text_survival.Environments.Factories;

/// <summary>
/// Creates locations from templates, used for event-driven location discovery.
/// </summary>
public static class LocationTemplateFactory
{
    private static readonly Random _random = new();

    /// <summary>
    /// Create a location from a template.
    /// </summary>
    public static Location Create(LocationTemplate template, Zone zone, Location connectFrom)
    {
        // Generate name from pattern
        string name = GenerateName(template.NamePattern);

        var location = new Location(name, zone)
        {
            BaseTraversalMinutes = template.TravelTime,
            Terrain = TerrainType.Clear
        };

        // Add features based on name pattern hints
        AddFeaturesFromPattern(location, template.NamePattern);

        // Connect to the source location
        connectFrom.AddBidirectionalConnection(location);
        zone.Graph.Add(location);

        return location;
    }

    private static string GenerateName(string pattern)
    {
        // Handle {Details} substitution with random descriptors
        if (pattern.Contains("{Details}"))
        {
            var descriptors = new[] { "Hidden", "Old", "Abandoned", "Sheltered", "Remote", "Quiet" };
            string descriptor = descriptors[_random.Next(descriptors.Length)];
            return pattern.Replace("{Details}", descriptor);
        }

        return pattern;
    }

    private static void AddFeaturesFromPattern(Location location, string pattern)
    {
        string lower = pattern.ToLower();

        // Environment type based on keywords
        if (lower.Contains("cave") || lower.Contains("hollow") || lower.Contains("shelter"))
        {
            location.Features.Add(new EnvironmentFeature(EnvironmentFeature.LocationType.Cave));
            location.Features.Add(new ForageFeature(0.3).AddSticks().AddTinder());
        }
        else if (lower.Contains("stream") || lower.Contains("creek") || lower.Contains("pond") || lower.Contains("water"))
        {
            location.Features.Add(new EnvironmentFeature(EnvironmentFeature.LocationType.RiverBank));
            location.Features.Add(new ForageFeature(0.6).AddSticks().AddTinder().AddPlantFiber());
            location.Features.Add(new WaterFeature("water_source", "Water Source"));
        }
        else if (lower.Contains("forest") || lower.Contains("grove") || lower.Contains("woods") || lower.Contains("birch"))
        {
            location.Features.Add(new EnvironmentFeature(EnvironmentFeature.LocationType.Forest));
            location.Features.Add(new ForageFeature(0.8).AddLogs().AddSticks().AddTinder().AddBerries());
        }
        else if (lower.Contains("ridge") || lower.Contains("overlook") || lower.Contains("high"))
        {
            location.Features.Add(new EnvironmentFeature(EnvironmentFeature.LocationType.HighGround));
            location.Features.Add(new ForageFeature(0.4).AddSticks().AddStone());
        }
        else if (lower.Contains("cliff") || lower.Contains("rock"))
        {
            location.Features.Add(new EnvironmentFeature(EnvironmentFeature.LocationType.Cliff));
            location.Features.Add(new ForageFeature(0.5).AddSticks().AddStone());
        }
        else
        {
            // Default: open plain with basic foraging
            location.Features.Add(new EnvironmentFeature(EnvironmentFeature.LocationType.OpenPlain));
            location.Features.Add(new ForageFeature(0.5).AddSticks().AddTinder());
        }

        // Add animal territory for hunting-related discoveries
        if (lower.Contains("game") || lower.Contains("hunting") || lower.Contains("trail") || lower.Contains("carcass"))
        {
            location.Features.Add(AnimalTerritoryFeature.CreateMixedTerritory());
        }
    }

    /// <summary>
    /// Common location templates for event-driven discoveries.
    /// </summary>
    public static class Templates
    {
        public static LocationTemplate ShelterHollow => new("Sheltered Hollow", 15);
        public static LocationTemplate GameTrail => new("Game Trail", 20);
        public static LocationTemplate FrozenStream => new("Frozen Stream", 25);
        public static LocationTemplate RockyOverhang => new("Rocky Overhang", 20);
        public static LocationTemplate DenseGrove => new("Dense Grove", 15);
        public static LocationTemplate HighRidge => new("High Ridge", 30);
        public static LocationTemplate CarcassSite => new("Carcass Site", 20);
        public static LocationTemplate AbandonedCamp => new("Old Campsite", 15);
    }
}
