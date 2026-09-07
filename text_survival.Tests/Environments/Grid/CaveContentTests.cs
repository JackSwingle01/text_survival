using System.Text.Json;
using text_survival.Actors.Animals;
using text_survival.Environments;
using text_survival.Environments.Factories;
using text_survival.Environments.Features;
using text_survival.Environments.Grid;
using text_survival.Items;
using text_survival.Persistence;

namespace text_survival.Tests.Environments.Grid;

public class CaveContentTests
{
    [Fact]
    public void CaveForageSeparatesUndergroundMaterialsFromEntranceFuel()
    {
        Assert.True(CaveLocationFactory.CreateForage().CanForage());
        var inside = CaveLocationFactory.CreateForage().ProvidedResources();
        Assert.Contains(Resource.Bone, inside);
        Assert.Contains(Resource.Flint, inside);
        Assert.Contains(Resource.Pyrite, inside);
        Assert.DoesNotContain(Resource.Stick, inside);
        Assert.DoesNotContain(Resource.Tinder, inside);
        Assert.Contains(Resource.Stick, CaveLocationFactory.CreateForage(true).ProvidedResources());
    }

    [Fact]
    public void FourSitesHaveDistinctFeaturesAndShelteredUndergroundEnvironment()
    {
        var weather = new Weather();
        var dry = CaveLocationFactory.MakeDryChamber(weather);
        var damp = CaveLocationFactory.MakeDampPassage(weather);
        var rubble = CaveLocationFactory.MakeRubblePassage(weather);
        var minerals = CaveLocationFactory.MakeMineralPocket(weather);
        Assert.NotNull(dry.GetFeature<ShelterFeature>());
        var seep = Assert.IsType<WaterFeature>(damp.GetFeature<WaterFeature>());
        Assert.False(seep.IsFrozen);
        Assert.Equal(0, seep.FishAbundance);
        Assert.True(rubble.BaseTraversalMinutes > dry.BaseTraversalMinutes);
        Assert.True(rubble.TerrainHazardLevel > dry.TerrainHazardLevel);
        Assert.Contains(minerals.Features, f => f.Name == "flint_outcrop");
        foreach (var site in new[] { dry, damp, rubble, minerals })
        {
            Assert.True(site.IsCaveInterior);
            Assert.True(site.IsDark);
            Assert.False(site.IsTerrainOnly);
            Assert.Equal(0, site.WindFactor);
            Assert.Equal(1, site.OverheadCoverLevel);
        }
    }

    [Fact]
    public void CaveDiscoveryPoolIsVariedAndSeeded()
    {
        var names = new HashSet<string>();
        for (int seed = 0; seed < 100; seed++)
        {
            var first = new DiscoveryGenerator(seed).GenerateForCave();
            var second = new DiscoveryGenerator(seed).GenerateForCave();
            Assert.Equal(first.Select(h => (h.Feature.Name, h.RevealAtHours)),
                second.Select(h => (h.Feature.Name, h.RevealAtHours)));
            foreach (var hidden in first)
            {
                names.Add(hidden.Feature.Name);
                Assert.True(hidden.RevealAtHours > 0);
            }
        }
        Assert.Equal(8, names.Count);
    }

    [Theory]
    [InlineData(0.2, null)]
    [InlineData(0.6, AnimalType.CaveBear)]
    [InlineData(0.9, AnimalType.Hyena)]
    public void OccupancyRollIsPerSystemWithConnectedTerritoryAndEntranceWarnings(double roll, AnimalType? expected)
    {
        var map = new GameMap(7, 3) { Weather = new Weather() };
        for (int x = 0; x < map.Width; x++)
            for (int y = 0; y < map.Height; y++)
                map.SetLocation(x, y, LocationFactory.MakeTerrainLocation(TerrainType.Mountain, map.Weather));
        for (int x = 1; x <= 5; x++)
        {
            var cave = LocationFactory.MakeCaveTile(map.Weather, x is 1 or 5, x);
            cave.CaveId = 1;
            map.SetLocation(x, 1, cave);
        }
        var herds = new List<Herd>();
        HerdPopulator.PopulateCaves(herds, map, new FixedRollRandom(roll));
        if (expected == null)
        {
            Assert.Empty(herds);
            return;
        }
        var herd = Assert.Single(herds);
        Assert.Equal(expected, herd.AnimalType);
        Assert.True(herd.CurrentLocation.IsCaveInterior);
        Assert.InRange(herd.Count, expected == AnimalType.CaveBear ? 1 : 2,
            expected == AnimalType.CaveBear ? 1 : 4);
        Assert.Equal(5, herd.HomeTerritory.Count);
        Assert.All(herd.HomeTerritory, p => Assert.Equal(1, map.GetLocationAt(p)!.CaveId));
        foreach (int x in new[] { 1, 5 })
            Assert.Contains(map.GetLocationAt(x, 1)!.Features, f => f is EnvironmentalDetail);
    }

    [Fact]
    public void GeneratedCaveSitesAndDiscoveriesSurviveSaveRoundTrip()
    {
        var (map, _) = new GridWorldGenerator().Generate(new Weather(), 17);
        var caves = map.AllLocations.Where(l => l.IsCaveInterior).ToList();
        Assert.NotEmpty(caves);
        Assert.Contains(caves, l => l.IsTerrainOnly);
        Assert.Contains(caves, l => !l.IsTerrainOnly);
        Assert.Contains(caves, l => l.HiddenFeatures.Count > 0);
        foreach (var cave in caves.GroupBy(l => l.CaveId))
            Assert.InRange(cave.Count(l => !l.IsTerrainOnly), 1, 2);
        foreach (var site in caves.Where(l => !l.IsTerrainOnly))
        {
            var restored = JsonSerializer.Deserialize<Location>(JsonSerializer.Serialize(site, SaveManager.Options), SaveManager.Options)!;
            Assert.Equal(site.Name, restored.Name);
            Assert.Equal(site.CaveId, restored.CaveId);
            Assert.True(restored.IsCaveInterior);
            Assert.Equal(site.Features.Count, restored.Features.Count);
            Assert.Equal(site.HiddenFeatures.Count, restored.HiddenFeatures.Count);
        }
    }

    private sealed class FixedRollRandom(double roll) : Random(1)
    {
        public override double NextDouble() => roll;
    }
}
