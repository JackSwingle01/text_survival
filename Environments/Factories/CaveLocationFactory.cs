using text_survival.Environments.Features;
using text_survival.Environments.Grid;
using text_survival.Items;

namespace text_survival.Environments.Factories;

/// <summary>Cave passages and authored underground sites share a dedicated discovery pool.</summary>
public static class CaveLocationFactory
{
    public static ForageFeature CreateForage(bool entrance = false, double density = ForageDensity.Sparse)
    {
        var forage = new ForageFeature(density)
            .AddStone(1.0, 0.15, 0.45)
            .AddShale(0.4, 0.15, 0.35)
            .AddFlint(0.15, 0.1, 0.25)
            .AddBone(0.15, 0.1, 0.3)
            .AddPyrite(0.03, 0.02, 0.08);
        if (entrance)
            forage.AddSticks(0.3, 0.1, 0.25).AddTinder(0.4, 0.01, 0.04);
        return forage;
    }

    public static Location MakePassage(Weather weather, bool entrance, int? positionSeed = null)
    {
        var location = new Location(
            entrance ? "Cave Entrance" : "Cave Passage",
            entrance ? "[Cave] [Entrance]" : "[Cave] [Sheltered] [Dark]", weather,
            terrainHazardLevel: entrance ? 0.2 : 0.3,
            windFactor: entrance ? 0.3 : 0,
            overheadCoverLevel: entrance ? 0.6 : 1,
            visibilityFactor: entrance ? 0.8 : 0.7)
        {
            Terrain = TerrainType.Rock,
            Structure = entrance ? TileStructure.CaveEntrance : TileStructure.CaveFloor,
            IsTerrainOnly = !entrance,
            IsDark = !entrance,
            DiscoveryText = entrance
                ? "A dark opening leads into the mountain. Windblown debris gathers beneath the lip."
                : "Rock walls close around a winding passage. Grit and old bone fragments crunch underfoot."
        };
        location.Features.Add(CreateForage(entrance));
        if (positionSeed.HasValue)
            location.HiddenFeatures.AddRange(new DiscoveryGenerator(
                unchecked(positionSeed.Value + LocationFactory.DiscoverySeedOffset)).GenerateForCave());
        return location;
    }

    private static Location CreateSite(Weather weather, string name, string description,
        double hazard, double traversal, double density)
    {
        var site = new Location(name, "[Cave] [Sheltered] [Dark]", weather,
            terrainHazardLevel: hazard, windFactor: 0, overheadCoverLevel: 1, visibilityFactor: 0.7)
        {
            Terrain = TerrainType.Rock,
            Structure = TileStructure.CaveFloor,
            IsDark = true,
            DiscoveryText = description,
            TraversalModifier = traversal
        };
        site.Features.Add(CreateForage(density: density));
        return site;
    }

    public static Location MakeDryChamber(Weather weather)
    {
        var site = CreateSite(weather, "Dry Cave Chamber",
            "The passage opens into a quiet chamber. A raised, dry ledge offers room to rest above the cold stone floor.",
            0.1, 0.9, ForageDensity.Sparse);
        site.Features.Add(CreateSleepingRecess());
        return site;
    }

    public static Location MakeDampPassage(Weather weather)
    {
        var site = CreateSite(weather, "Damp Cave Passage",
            "Water beads on the walls. A steady drip feeds a shallow pool beside the slippery path.",
            0.4, 1.15, ForageDensity.Sparse);
        site.Features.Add(CreateSeep());
        return site;
    }

    public static Location MakeRubblePassage(Weather weather)
    {
        var site = CreateSite(weather, "Rubble Cave Passage",
            "Fallen slabs choke the passage. Loose shale shifts underfoot; each step needs care.",
            0.55, 1.4, ForageDensity.Light);
        site.Features.Add(CreateLooseShale());
        return site;
    }

    public static Location MakeMineralPocket(Weather weather)
    {
        var site = CreateSite(weather, "Cave Mineral Pocket",
            "Pale bands cut across the exposed rock. Flint nodules protrude from the wall, with darker seams between them.",
            0.3, 1.1, ForageDensity.Moderate);
        site.Features.Add(DiscoveryFeatureFactory.CreateFlintOutcrop());
        return site;
    }

    public static WaterFeature CreateSeep() => new WaterFeature("cave_seep", "Cave Seep")
        .AsOpenWater().WithFishAbundance(0)
        .WithDescription("A trickle from deep in the rock collects in a shallow, fishless pool.");

    public static ShelterFeature CreateSleepingRecess() => new("Dry Sleeping Recess", ShelterType.Cave);

    public static HarvestableFeature CreateLooseShale()
    {
        var shale = new HarvestableFeature("cave_shale", "Loose Shale")
        {
            Description = "Thin plates of shale have split from the cave wall. Usable pieces lie beneath the dust.",
            MinutesToHarvest = 5
        };
        shale.AddResource("shale plates", Resource.Shale, maxQuantity: 4, weightPerUnit: 0.25, respawnHoursPerUnit: 0);
        return shale;
    }
}
