namespace text_survival.Environments;

using text_survival.Environments.Features;

public static class ZoneFactory
{
    public static Zone MakeForestZone()
    {
        var zone = new Zone("Pine Forest", "A dense forest of evergreens.", baseTemp: 25);

        // Create sites
        var clearing = new Location("Forest Clearing", zone)
        {
            Description = "A small clearing among the pines.",
            Exposure = 0.4,
        };
        clearing.Explore();
        clearing.Features.Add(new ForageFeature(.5));
        zone.Graph.Add(clearing);

        var grove = new Location("Birch Grove", zone)
        {
            Description = "A stand of white birch trees.",
            Exposure = 0.3,
        };
        grove.Features.Add(new ForageFeature(.8));
        zone.Graph.Add(grove);

        var outcrop = new Location("Rocky Outcrop", zone)
        {
            Description = "Exposed rocks jutting from the hillside.",
            Exposure = 0.9,
        };
        zone.Graph.Add(outcrop);

        var cave = new Location("Shallow Cave", zone)
        {
            Description = "A depression in the rock face.",
            Exposure = 0.1,
        };
        cave.Features.Add(new ShelterFeature(.4, .2, .4));
        zone.Graph.Add(cave);

        // Create paths
        zone.Graph.CreatePath(
                  "Winding Path", zone, clearing, grove,
                  traversalMinutes: 8,
                  terrain: TerrainType.Clear,
                  exposure: 0.5);

        zone.Graph.CreatePath(
                   "Steep Trail", zone, clearing, outcrop,
                   traversalMinutes: 12,
                   terrain: TerrainType.Steep,
                   exposure: 0.7);

        zone.Graph.CreatePath(
                "Overgrown Trail", zone, grove, cave,
                traversalMinutes: 10,
                terrain: TerrainType.Rough,
                exposure: 0.3);

        return zone;
    }


}