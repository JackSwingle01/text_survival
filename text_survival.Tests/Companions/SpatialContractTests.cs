using text_survival.Environments;
using text_survival.Environments.Grid;
using text_survival.Environments.Navigation;
using text_survival.Environments.Perception;

namespace text_survival.Tests.Companions;

public class SpatialContractTests
{
    [Fact]
    public void SightUsesObserversLocationWithoutChangingPlayerDiscovery()
    {
        var world = new CompanionWorld(30, 3);
        var observer = world.AddNpc("Observer", 25, 1);
        var target = world.AddNpc("Target", 26, 1);
        var visibility = world.Map.AllLocations.Select(l => l.Visibility).ToArray();

        Assert.True(Sight.CanSeeActor(observer, target));
        Assert.False(Sight.CanSeeActor(world.Game.player, target));
        Assert.False(Sight.CanSeeTile(world.Map, world.Position(observer), world.Position(target), 0));
        Assert.Equal(visibility, world.Map.AllLocations.Select(l => l.Visibility));
    }

    [Fact]
    public void SearchBudgetIsDistinctFromAnUnreachableDestination()
    {
        var world = new CompanionWorld();
        var result = Navigation.FindRoute(world.Map, new(0, 0), new(7, 0), maxExpandedNodes: 1);
        Assert.Equal(PathStatus.BudgetExceeded, result.Status);
        Assert.Empty(result.Steps);
        world.Tile(7, 0).Terrain = TerrainType.Mountain;
        Assert.Equal(PathStatus.NoRoute, Navigation.FindRoute(world.Map, new(0, 0), new(7, 0)).Status);
    }

    [Fact]
    public void SearchPrefersALongerButCheaperRoute()
    {
        var world = new CompanionWorld(3, 2);
        world.Map.AddEdge(new(0, 0), new(1, 0), new TileEdge(EdgeType.River) { TraversalModifierMinutes = 100 });
        var result = Navigation.FindRoute(world.Map, new(0, 0), new(2, 0));
        Assert.Equal(PathStatus.Found, result.Status);
        Assert.Equal(new GridPosition(0, 1), result.Steps[0]);
        Assert.Equal(48, result.CostMinutes);
    }

    [Fact]
    public void MovementRevalidatesCrossingAndIsIdempotent()
    {
        var world = new CompanionWorld();
        var npc = world.AddNpc("Walker");
        var destination = world.Tile(2, 1);
        var edge = new TileEdge(EdgeType.River) { Impassable = true };
        world.Map.AddEdge(world.Position(npc), new(2, 1), edge);
        Assert.False(ActorMovement.CompleteCrossing(npc, destination));
        Assert.Empty(world.Map.Tracks.Marks);
        edge.Impassable = false;
        Assert.True(ActorMovement.CompleteCrossing(npc, destination));
        var traffic = world.Map.Tracks.Marks.Sum(m => m.Traffic);
        Assert.True(ActorMovement.CompleteCrossing(npc, destination));
        Assert.Equal(traffic, world.Map.Tracks.Marks.Sum(m => m.Traffic));
    }
}
