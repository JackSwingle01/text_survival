using text_survival.Environments;
using text_survival.Environments.Grid;
using text_survival.Environments.Factories;

namespace text_survival.Tests.Environments.Grid;

public class GridPositionTests
{
    [Fact]
    public void ManhattanDistance_SamePosition_ReturnsZero()
    {
        var pos = new GridPosition(5, 5);
        Assert.Equal(0, pos.ManhattanDistance(pos));
    }

    [Fact]
    public void ManhattanDistance_AdjacentPositions_ReturnsOne()
    {
        var pos = new GridPosition(5, 5);
        Assert.Equal(1, pos.ManhattanDistance(new GridPosition(5, 6)));
        Assert.Equal(1, pos.ManhattanDistance(new GridPosition(6, 5)));
        Assert.Equal(1, pos.ManhattanDistance(new GridPosition(5, 4)));
        Assert.Equal(1, pos.ManhattanDistance(new GridPosition(4, 5)));
    }

    [Fact]
    public void ManhattanDistance_DiagonalPositions_ReturnsTwo()
    {
        var pos = new GridPosition(5, 5);
        Assert.Equal(2, pos.ManhattanDistance(new GridPosition(6, 6)));
        Assert.Equal(2, pos.ManhattanDistance(new GridPosition(4, 4)));
    }

    [Fact]
    public void IsAdjacentTo_CardinalNeighbors_ReturnsTrue()
    {
        var pos = new GridPosition(5, 5);
        Assert.True(pos.IsAdjacentTo(new GridPosition(5, 6)));
        Assert.True(pos.IsAdjacentTo(new GridPosition(6, 5)));
        Assert.True(pos.IsAdjacentTo(new GridPosition(5, 4)));
        Assert.True(pos.IsAdjacentTo(new GridPosition(4, 5)));
    }

    [Fact]
    public void IsAdjacentTo_DiagonalNeighbors_ReturnsFalse()
    {
        var pos = new GridPosition(5, 5);
        Assert.False(pos.IsAdjacentTo(new GridPosition(6, 6)));
        Assert.False(pos.IsAdjacentTo(new GridPosition(4, 4)));
    }

    [Fact]
    public void IsAdjacentTo_SamePosition_ReturnsFalse()
    {
        var pos = new GridPosition(5, 5);
        Assert.False(pos.IsAdjacentTo(pos));
    }

    [Fact]
    public void GetCardinalNeighbors_ReturnsFourPositions()
    {
        var pos = new GridPosition(5, 5);
        var neighbors = pos.GetCardinalNeighbors().ToList();

        Assert.Equal(4, neighbors.Count);
        Assert.Contains(new GridPosition(5, 4), neighbors); // North
        Assert.Contains(new GridPosition(6, 5), neighbors); // East
        Assert.Contains(new GridPosition(5, 6), neighbors); // South
        Assert.Contains(new GridPosition(4, 5), neighbors); // West
    }

    [Fact]
    public void GetPositionsInRange_RangeZero_ReturnsSelf()
    {
        var pos = new GridPosition(5, 5);
        var positions = pos.GetPositionsInRange(0).ToList();

        Assert.Single(positions);
        Assert.Contains(pos, positions);
    }

    [Fact]
    public void GetPositionsInRange_RangeOne_ReturnsFivePositions()
    {
        var pos = new GridPosition(5, 5);
        var positions = pos.GetPositionsInRange(1).ToList();

        Assert.Equal(5, positions.Count);
        Assert.Contains(pos, positions);
        Assert.Contains(new GridPosition(5, 4), positions);
        Assert.Contains(new GridPosition(6, 5), positions);
        Assert.Contains(new GridPosition(5, 6), positions);
        Assert.Contains(new GridPosition(4, 5), positions);
    }
}

public class GameMapTests
{
    private static GameMap CreateTestMap(int width = 10, int height = 10)
    {
        var map = new GameMap(width, height);
        var weather = new Weather();

        // Fill with passable terrain locations
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                var terrain = TerrainType.Plain;
                var location = LocationFactory.MakeTerrainLocation(terrain, weather);
                map.SetLocation(x, y, location);
            }
        }

        return map;
    }

    [Fact]
    public void Constructor_CreatesMapWithCorrectDimensions()
    {
        var map = new GameMap(10, 8);

        Assert.Equal(10, map.Width);
        Assert.Equal(8, map.Height);
    }

    [Fact]
    public void GetLocationAt_ValidPosition_ReturnsLocation()
    {
        var map = CreateTestMap();

        var location = map.GetLocationAt(5, 5);

        Assert.NotNull(location);
    }

    [Fact]
    public void GetLocationAt_OutOfBounds_ReturnsNull()
    {
        var map = CreateTestMap();

        Assert.Null(map.GetLocationAt(-1, 5));
        Assert.Null(map.GetLocationAt(10, 5));
        Assert.Null(map.GetLocationAt(5, -1));
        Assert.Null(map.GetLocationAt(5, 10));
    }

    [Fact]
    public void IsInBounds_ValidPosition_ReturnsTrue()
    {
        var map = CreateTestMap();

        Assert.True(map.IsInBounds(0, 0));
        Assert.True(map.IsInBounds(9, 9));
        Assert.True(map.IsInBounds(5, 5));
    }

    [Fact]
    public void IsInBounds_InvalidPosition_ReturnsFalse()
    {
        var map = CreateTestMap();

        Assert.False(map.IsInBounds(-1, 0));
        Assert.False(map.IsInBounds(10, 0));
        Assert.False(map.IsInBounds(0, -1));
        Assert.False(map.IsInBounds(0, 10));
    }

    [Fact]
    public void SetLocation_StoresLocationAtPosition()
    {
        var map = new GameMap(10, 10);
        var weather = new Weather();
        var location = new Location("Test", "[test]", weather, 5);

        map.SetLocation(5, 5, location);

        Assert.Equal(location, map.GetLocationAt(5, 5));
    }

    [Fact]
    public void GetTravelOptions_CenterPosition_ReturnsFourNeighbors()
    {
        var map = CreateTestMap();
        map.CurrentPosition = new GridPosition(5, 5);

        var options = map.GetTravelOptions();

        Assert.Equal(4, options.Count);
    }

    [Fact]
    public void GetTravelOptions_CornerPosition_ReturnsTwoNeighbors()
    {
        var map = CreateTestMap();
        map.CurrentPosition = new GridPosition(0, 0);

        var options = map.GetTravelOptions();

        Assert.Equal(2, options.Count);
    }

    [Fact]
    public void GetTravelOptions_ExcludesImpassableTerrain()
    {
        var map = CreateTestMap();
        var weather = new Weather();
        map.CurrentPosition = new GridPosition(5, 5);

        // Set one neighbor to impassable
        var mountainLocation = LocationFactory.MakeTerrainLocation(TerrainType.Mountain, weather);
        map.SetLocation(5, 4, mountainLocation);

        var options = map.GetTravelOptions();

        Assert.Equal(3, options.Count);
    }

    [Fact]
    public void MoveTo_UpdatesCurrentPosition()
    {
        var map = CreateTestMap();
        map.CurrentPosition = new GridPosition(5, 5);
        var destination = map.GetLocationAt(5, 6)!;

        map.MoveTo(destination);

        Assert.Equal(new GridPosition(5, 6), map.CurrentPosition);
    }

    [Fact]
    public void MoveTo_MarksLocationAsExplored()
    {
        var map = CreateTestMap();
        map.CurrentPosition = new GridPosition(5, 5);
        var destination = map.GetLocationAt(5, 6)!;
        Assert.False(destination.Explored);

        map.MoveTo(destination);

        Assert.True(destination.Explored);
    }

    [Fact]
    public void CanMoveTo_AdjacentPassable_ReturnsTrue()
    {
        var map = CreateTestMap();
        map.CurrentPosition = new GridPosition(5, 5);

        Assert.True(map.CanMoveTo(5, 6));
        Assert.True(map.CanMoveTo(6, 5));
    }

    [Fact]
    public void CanMoveTo_Diagonal_ReturnsFalse()
    {
        var map = CreateTestMap();
        map.CurrentPosition = new GridPosition(5, 5);

        Assert.False(map.CanMoveTo(6, 6));
    }

    [Fact]
    public void CanMoveTo_NotAdjacent_ReturnsFalse()
    {
        var map = CreateTestMap();
        map.CurrentPosition = new GridPosition(5, 5);

        Assert.False(map.CanMoveTo(7, 5)); // 2 tiles away
    }

    [Fact]
    public void GetPosition_ReturnsLocationPosition()
    {
        var map = new GameMap(10, 10);
        var weather = new Weather();
        var location = new Location("Test", "[test]", weather, 5);
        map.SetLocation(5, 5, location);

        var position = map.GetPosition(location);

        Assert.NotNull(position);
        Assert.Equal(new GridPosition(5, 5), position.Value);
    }

    [Fact]
    public void Contains_LocationOnMap_ReturnsTrue()
    {
        var map = new GameMap(10, 10);
        var weather = new Weather();
        var location = new Location("Test", "[test]", weather, 5);
        map.SetLocation(5, 5, location);

        Assert.True(map.Contains(location));
    }

    [Fact]
    public void Contains_LocationNotOnMap_ReturnsFalse()
    {
        var map = new GameMap(10, 10);
        var weather = new Weather();
        var location = new Location("Test", "[test]", weather, 5);

        Assert.False(map.Contains(location));
    }
}

public class LocationTests
{
    [Fact]
    public void Location_Terrain_WithoutSeed_HasNoEnvironmentalDetails()
    {
        var weather = new Weather();
        var location = LocationFactory.MakeTerrainLocation(TerrainType.Forest, weather);

        // Terrain locations without a position seed have basic terrain features (forage)
        // but no environmental details
        Assert.DoesNotContain(location.Features,
            f => f is text_survival.Environments.Features.EnvironmentalDetail);
    }

    [Fact]
    public void Location_Named_HasProperties()
    {
        var weather = new Weather();
        var location = new Location("Test Cave", "[cave]", weather, 15,
            terrainHazardLevel: 0.3, windFactor: 0.2);

        Assert.Equal("Test Cave", location.Name);
        Assert.Equal(15, location.BaseTraversalMinutes);
        Assert.Equal(0.3, location.TerrainHazardLevel);
        Assert.Equal(0.2, location.WindFactor);
    }

    [Fact]
    public void Location_IsPassable_DependsOnTerrain()
    {
        var weather = new Weather();
        var passableLocation = LocationFactory.MakeTerrainLocation(TerrainType.Forest, weather);
        var impassableLocation = LocationFactory.MakeTerrainLocation(TerrainType.Mountain, weather);

        Assert.True(passableLocation.IsPassable);
        Assert.False(impassableLocation.IsPassable);
    }

    [Fact]
    public void MarkExplored_SetsExplored()
    {
        var weather = new Weather();
        var location = LocationFactory.MakeTerrainLocation(TerrainType.Forest, weather);
        Assert.False(location.Explored);

        location.MarkExplored();

        Assert.True(location.Explored);
    }

    [Fact]
    public void Visibility_CannotGoBackToUnexplored()
    {
        var weather = new Weather();
        var location = LocationFactory.MakeTerrainLocation(TerrainType.Forest, weather);
        location.Visibility = TileVisibility.Explored;

        location.Visibility = TileVisibility.Unexplored;

        // Should remain explored, not go back to unexplored
        Assert.Equal(TileVisibility.Explored, location.Visibility);
    }
}

public class VisibilityTests
{
    private static GameMap CreateInitializedMap(int width, int height)
    {
        var map = new GameMap(width, height);
        var weather = new Weather();
        map.Weather = weather;

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                var location = LocationFactory.MakeTerrainLocation(TerrainType.Plain, weather);
                map.SetLocation(x, y, location);
            }
        }
        return map;
    }

    [Fact]
    public void UpdateVisibility_MarksInRangeAsVisible()
    {
        var map = CreateInitializedMap(10, 10);
        map.CurrentPosition = new GridPosition(5, 5);

        map.UpdateVisibility();

        Assert.Equal(TileVisibility.Visible, map.GetLocationAt(5, 5)!.Visibility);
        Assert.Equal(TileVisibility.Visible, map.GetLocationAt(5, 6)!.Visibility);
        Assert.Equal(TileVisibility.Visible, map.GetLocationAt(6, 5)!.Visibility);
    }

    [Fact]
    public void UpdateVisibility_DowngradesPreviouslyVisibleToExplored()
    {
        var map = CreateInitializedMap(10, 10);
        map.CurrentPosition = new GridPosition(5, 5);

        // First visibility update at (5, 5)
        map.UpdateVisibility();
        Assert.Equal(TileVisibility.Visible, map.GetLocationAt(5, 6)!.Visibility);

        // Move to (7, 7)
        map.CurrentPosition = new GridPosition(7, 7);
        map.UpdateVisibility();

        // Previous tile should now be explored but not visible
        Assert.Equal(TileVisibility.Explored, map.GetLocationAt(5, 6)!.Visibility);
    }
}

public class GridWorldGeneratorTests
{
    [Fact]
    public void Generate_CreatesCamp()
    {
        var generator = new GridWorldGenerator { Width = 20, Height = 20 };
        var weather = new Weather();

        var (map, camp) = generator.Generate(weather);

        Assert.NotNull(camp);
        Assert.Equal("Forest Camp", camp.Name);
    }

    [Fact]
    public void Generate_CampIsExplored()
    {
        var generator = new GridWorldGenerator { Width = 20, Height = 20 };
        var weather = new Weather();

        var (map, camp) = generator.Generate(weather);

        Assert.True(camp.Explored);
    }

    [Fact]
    public void Generate_CampIsOnMap()
    {
        var generator = new GridWorldGenerator { Width = 20, Height = 20 };
        var weather = new Weather();

        var (map, camp) = generator.Generate(weather);

        Assert.True(map.Contains(camp));
        Assert.Equal(camp, map.CurrentLocation);
    }

    [Fact]
    public void Generate_SurroundingLocationsAreVisible()
    {
        var generator = new GridWorldGenerator { Width = 20, Height = 20 };
        var weather = new Weather();

        var (map, camp) = generator.Generate(weather);
        var campPos = map.GetPosition(camp)!.Value;

        // Camp and immediate surroundings should be visible
        Assert.Equal(TileVisibility.Visible, camp.Visibility);
        foreach (var neighborPos in campPos.GetCardinalNeighbors())
        {
            var neighbor = map.GetLocationAt(neighborPos);
            if (neighbor != null)
            {
                Assert.True(neighbor.Visibility == TileVisibility.Visible,
                    $"Neighbor at {neighborPos} should be visible");
            }
        }
    }

    [Fact]
    public void Generate_PlacesMultipleNamedLocations()
    {
        var generator = new GridWorldGenerator
        {
            Width = 32,
            Height = 32,
            TargetNamedLocations = 20
        };
        var weather = new Weather();

        var (map, camp) = generator.Generate(weather);

        var namedLocationCount = map.NamedLocations.Count();
        Assert.True(namedLocationCount > 1, $"Expected multiple named locations, got {namedLocationCount}");
    }

    [Fact]
    public void Generate_HasMountainRangeAtTop()
    {
        var generator = new GridWorldGenerator { Width = 20, Height = 20 };
        var weather = new Weather();

        var (map, camp) = generator.Generate(weather);

        // Check top rows have mountains (except for pass)
        int mountainCount = 0;
        for (int x = 0; x < 20; x++)
        {
            var loc = map.GetLocationAt(x, 0);
            if (loc != null && loc.Terrain == TerrainType.Mountain)
                mountainCount++;
        }

        Assert.True(mountainCount > 10, "Expected mountains along top edge");
    }
}
