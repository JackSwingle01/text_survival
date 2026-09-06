using text_survival.Environments.Grid;

namespace text_survival.Tests.Companions;

[Trait("Suite", "CompanionAcceptance")]
public class NavigationAcceptanceTests
{
    [Fact]
    [Trait("Scenario", "F22")]
    public void Route_CanInitiallyMoveAwayFromTheDestinationToGetAroundAnObstacle()
    {
        var world = new CompanionWorld(5, 3);
        world.Tile(2, 1).Terrain = TerrainType.Mountain;
        var from = new GridPosition(1, 1);
        var to = new GridPosition(3, 1);

        var route = world.FindRoute(from, to);

        Assert.NotNull(route);
        Assert.Equal(to, route[^1]);
        Assert.DoesNotContain(new GridPosition(2, 1), route);
        Assert.True(route.Count >= 4);
        AssertLegalRoute(world, from, route);
    }

    [Fact]
    [Trait("Scenario", "F23")]
    public void Route_RespectsAnImpassableEdgeEvenWhenTheDestinationTileIsPassable()
    {
        var world = new CompanionWorld(3, 1);
        var from = new GridPosition(0, 0);
        var to = new GridPosition(2, 0);
        world.Map.AddEdge(from, new GridPosition(1, 0), new TileEdge(EdgeType.River) { Impassable = true });

        var route = world.FindRoute(from, to);

        Assert.Null(route);
    }

    [Fact]
    [Trait("Scenario", "F23")]
    public void Route_RespectsSeasonalBarriers()
    {
        var world = new CompanionWorld(3, 1);
        var from = new GridPosition(0, 0);
        var to = new GridPosition(2, 0);
        world.Map.AddEdge(from, new GridPosition(1, 0), new TileEdge(EdgeType.River)
        {
            BlockedSeason = world.Game.Weather.CurrentSeason
        });

        Assert.Null(world.FindRoute(from, to));
    }

    [Fact]
    [Trait("Scenario", "F22")]
    public void RoutePlanning_DoesNotMoveAnyoneRevealTilesOrLeaveTracks()
    {
        var world = new CompanionWorld(5, 3);
        var playerPosition = world.Map.CurrentPosition;
        var visibility = world.Map.AllLocations.Select(l => l.Visibility).ToArray();

        var route = world.FindRoute(new GridPosition(1, 1), new GridPosition(4, 1));

        Assert.NotNull(route);
        Assert.Equal(new GridPosition(4, 1), route[^1]);
        AssertLegalRoute(world, new GridPosition(1, 1), route);
        Assert.Equal(playerPosition, world.Map.CurrentPosition);
        Assert.Equal(visibility, world.Map.AllLocations.Select(l => l.Visibility));
        Assert.Empty(world.Map.Tracks.Marks);
        Assert.Equal(0, world.Game.TotalMinutesElapsed);
    }

    private static void AssertLegalRoute(CompanionWorld world, GridPosition from, IReadOnlyList<GridPosition> route)
    {
        foreach (var step in route)
        {
            Assert.Contains(world.Map.GetLocationAt(step), world.Map.GetTravelOptionsFrom(world.Map.GetLocationAt(from)!));
            from = step;
        }
    }
}
