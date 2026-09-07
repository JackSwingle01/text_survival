using System.Text.Json;
using text_survival.Environments;
using text_survival.Environments.Factories;
using text_survival.Environments.Grid;
using text_survival.Persistence;

namespace text_survival.Tests.Environments.Grid;

public class ValleyWorldTests
{
    [Theory]
    [InlineData(17)]
    [InlineData(2048)]
    [InlineData(91237)]
    public void GeneratedWorldHasReachableEasternExitAndNoBypassAroundPass(int seed)
    {
        var (map, camp) = new GridWorldGenerator().Generate(new Weather(), seed);
        Assert.Equal(224, map.Width);
        Assert.Equal(82, map.Height);
        Assert.True(map.GetPosition(camp).X < map.Width / 5);
        var exit = Assert.Single(map.AllLocations, l => l.IsCrossingExit);
        Assert.Equal(map.Width - 1, map.GetPosition(exit).X);
        var campPos = map.GetPosition(camp);
        Assert.Equal(TerrainType.Forest, camp.Terrain);
        Assert.All(campPos.GetCardinalNeighbors(), n =>
            Assert.False(map.IsEdgeBlocked(campPos, n, map.Weather.CurrentSeason)));
        var reached = Reachable(map, camp);
        Assert.All(map.AllLocations.Where(l => l.IsPassable), l => Assert.Contains(l, reached));
        string[] stages = ["Pass Approach", "Lower Pass", "The Pass Proper", "Upper Descent", "Lower Descent"];
        foreach (var name in stages)
        {
            var stage = Assert.Single(map.NamedLocations, l => l.Name == name);
            Assert.DoesNotContain(exit, Reachable(map, camp, stage));
        }
    }

    [Fact]
    public void CaveWallsBlockMovementAndRoofsConcealExploredFloorsAfterLeaving()
    {
        var map = new GameMap(4, 3) { Weather = new Weather(), CurrentPosition = new(0, 1) };
        for (int x = 0; x < map.Width; x++)
            for (int y = 0; y < map.Height; y++)
                map.SetLocation(x, y, LocationFactory.MakeTerrainLocation(TerrainType.Plain, map.Weather));
        var mouth = LocationFactory.MakeCaveTile(map.Weather, true);
        mouth.Structure = TileStructure.CaveEntrance; mouth.CaveId = 7;
        var floor = LocationFactory.MakeCaveTile(map.Weather, false);
        floor.Structure = TileStructure.CaveFloor; floor.CaveId = 7;
        map.SetLocation(1, 1, mouth); map.SetLocation(2, 1, floor);
        Assert.True(map.IsEdgeBlocked(new(2, 0), new(2, 1), Weather.Season.Winter));
        Assert.False(map.IsEdgeBlocked(new(1, 1), new(2, 1), Weather.Season.Winter));
        map.CurrentPosition = new(1, 1); map.UpdateVisibility();
        Assert.Equal(TileVisibility.Visible, map.GetVisibility(2, 1));
        Assert.Equal(TerrainType.Rock, map.DisplayTerrain(floor));
        map.CurrentPosition = new(0, 1); map.UpdateVisibility();
        Assert.True(map.IsCaveConcealed(floor));
        Assert.Equal(TerrainType.Mountain, map.DisplayTerrain(floor));
        Assert.DoesNotContain(floor, map.VisibleLocations);
        Assert.False(text_survival.Environments.Perception.Sight.CanSeeTile(map, new(0, 1), new(2, 1)));
    }

    [Fact]
    public void CaveAndBarrierMetadataSurviveSaveRoundTrip()
    {
        var map = new GameMap(2, 1) { Weather = new Weather() };
        var floor = LocationFactory.MakeCaveTile(map.Weather, false);
        floor.Structure = TileStructure.CaveFloor; floor.CaveId = 3;
        floor.CaveRoofVisibility = TileVisibility.Explored;
        map.SetLocation(0, 0, floor);
        var ravine = LocationFactory.MakeTerrainLocation(TerrainType.Forest, map.Weather);

        map.SetLocation(1, 0, ravine);
        map.AddEdge(new(0, 0), new(1, 0), new TileEdge(EdgeType.Ravine));
        var restored = JsonSerializer.Deserialize<GameMap>(JsonSerializer.Serialize(map, SaveManager.Options), SaveManager.Options)!;
        Assert.Equal(3, restored.GetLocationAt(0, 0)!.CaveId);
        Assert.True(restored.GetLocationAt(0, 0)!.IsDark);
        Assert.Equal(TileVisibility.Explored, restored.GetLocationAt(0, 0)!.CaveRoofVisibility);
        Assert.True(restored.GetLocationAt(1, 0)!.IsPassable);
        var barrier = Assert.Single(restored.GetEdgesBetween(new(0, 0), new(1, 0)));
        Assert.Equal(EdgeType.Ravine, barrier.Type);
        Assert.True(barrier.Impassable);
        Assert.True(barrier.Bidirectional);
    }

    private static HashSet<Location> Reachable(GameMap map, Location start, Location? blocked = null)
    {
        var reached = new HashSet<Location> { start };
        var queue = new Queue<Location>(); queue.Enqueue(start);
        while (queue.TryDequeue(out var current))
            foreach (var next in map.GetTravelOptionsFrom(current))
                if (next != blocked && reached.Add(next)) queue.Enqueue(next);
        return reached;
    }
}
