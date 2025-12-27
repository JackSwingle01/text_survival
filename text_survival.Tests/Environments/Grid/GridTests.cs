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

public class TileGridTests
{
    [Fact]
    public void Constructor_CreatesGridWithCorrectDimensions()
    {
        var grid = new TileGrid(10, 8);

        Assert.Equal(10, grid.Width);
        Assert.Equal(8, grid.Height);
    }

    [Fact]
    public void Indexer_ValidPosition_ReturnsTile()
    {
        var grid = new TileGrid(10, 10);

        var tile = grid[5, 5];

        Assert.NotNull(tile);
        Assert.Equal(5, tile.X);
        Assert.Equal(5, tile.Y);
    }

    [Fact]
    public void Indexer_OutOfBounds_ReturnsNull()
    {
        var grid = new TileGrid(10, 10);

        Assert.Null(grid[-1, 5]);
        Assert.Null(grid[10, 5]);
        Assert.Null(grid[5, -1]);
        Assert.Null(grid[5, 10]);
    }

    [Fact]
    public void IsInBounds_ValidPosition_ReturnsTrue()
    {
        var grid = new TileGrid(10, 10);

        Assert.True(grid.IsInBounds(0, 0));
        Assert.True(grid.IsInBounds(9, 9));
        Assert.True(grid.IsInBounds(5, 5));
    }

    [Fact]
    public void IsInBounds_InvalidPosition_ReturnsFalse()
    {
        var grid = new TileGrid(10, 10);

        Assert.False(grid.IsInBounds(-1, 0));
        Assert.False(grid.IsInBounds(10, 0));
        Assert.False(grid.IsInBounds(0, -1));
        Assert.False(grid.IsInBounds(0, 10));
    }

    [Fact]
    public void SetTerrain_ChangesTileTerrainType()
    {
        var grid = new TileGrid(10, 10);

        grid.SetTerrain(5, 5, TerrainType.Forest);

        Assert.Equal(TerrainType.Forest, grid[5, 5]!.Terrain);
    }

    [Fact]
    public void GetNeighbors_CenterTile_ReturnsFourNeighbors()
    {
        var grid = new TileGrid(10, 10);
        var tile = grid[5, 5]!;

        var neighbors = grid.GetNeighbors(tile).ToList();

        Assert.Equal(4, neighbors.Count);
    }

    [Fact]
    public void GetNeighbors_CornerTile_ReturnsTwoNeighbors()
    {
        var grid = new TileGrid(10, 10);
        var tile = grid[0, 0]!;

        var neighbors = grid.GetNeighbors(tile).ToList();

        Assert.Equal(2, neighbors.Count);
    }

    [Fact]
    public void GetNeighbors_EdgeTile_ReturnsThreeNeighbors()
    {
        var grid = new TileGrid(10, 10);
        var tile = grid[5, 0]!; // Top edge, not corner

        var neighbors = grid.GetNeighbors(tile).ToList();

        Assert.Equal(3, neighbors.Count);
    }

    [Fact]
    public void GetPassableNeighbors_ExcludesImpassableTerrain()
    {
        var grid = new TileGrid(10, 10);
        var centerTile = grid[5, 5]!;

        // Set one neighbor to impassable
        grid.SetTerrain(5, 4, TerrainType.Mountain);

        var passableNeighbors = grid.GetPassableNeighbors(centerTile).ToList();

        Assert.Equal(3, passableNeighbors.Count);
    }

    [Fact]
    public void IsAdjacent_AdjacentTiles_ReturnsTrue()
    {
        var grid = new TileGrid(10, 10);
        var tile1 = grid[5, 5]!;
        var tile2 = grid[5, 6]!;

        Assert.True(grid.IsAdjacent(tile1, tile2));
    }

    [Fact]
    public void IsAdjacent_DiagonalTiles_ReturnsFalse()
    {
        var grid = new TileGrid(10, 10);
        var tile1 = grid[5, 5]!;
        var tile2 = grid[6, 6]!;

        Assert.False(grid.IsAdjacent(tile1, tile2));
    }

    [Fact]
    public void PlaceLocation_AssignsLocationToTile()
    {
        var grid = new TileGrid(10, 10);
        var weather = new Weather();
        var location = new Location("Test", "[test]", weather, 5);

        grid.PlaceLocation(5, 5, location);

        Assert.Equal(location, grid[5, 5]!.NamedLocation);
        Assert.Equal(new GridPosition(5, 5), location.GridPosition);
    }

    [Fact]
    public void AllTiles_ReturnsCorrectCount()
    {
        var grid = new TileGrid(10, 8);

        Assert.Equal(80, grid.AllTiles.Count());
    }
}

public class TileTests
{
    [Fact]
    public void Tile_WithoutLocation_UsesTerrainDefaults()
    {
        var tile = new Tile(5, 5, TerrainType.Forest);

        Assert.Equal("Forest", tile.Name);
        Assert.Equal(TerrainType.Forest.BaseTraversalMinutes(), tile.TraversalMinutes);
        Assert.Equal(TerrainType.Forest.BaseHazardLevel(), tile.TerrainHazardLevel);
        Assert.Equal(TerrainType.Forest.BaseWindFactor(), tile.WindFactor);
    }

    [Fact]
    public void Tile_WithLocation_UsesLocationProperties()
    {
        var weather = new Weather();
        var location = new Location("Test Cave", "[cave]", weather, 15,
            terrainHazardLevel: 0.3, windFactor: 0.2);

        var tile = new Tile(5, 5, TerrainType.Rock) { NamedLocation = location };

        Assert.Equal("Test Cave", tile.Name);
        Assert.Equal(15, tile.TraversalMinutes);
        Assert.Equal(0.3, tile.TerrainHazardLevel);
        Assert.Equal(0.2, tile.WindFactor);
    }

    [Fact]
    public void Tile_IsPassable_DependsOnTerrain()
    {
        var passableTile = new Tile(5, 5, TerrainType.Forest);
        var impassableTile = new Tile(5, 5, TerrainType.Mountain);

        Assert.True(passableTile.IsPassable);
        Assert.False(impassableTile.IsPassable);
    }

    [Fact]
    public void MarkExplored_ChangesVisibility()
    {
        var tile = new Tile(5, 5, TerrainType.Forest);
        Assert.False(tile.IsExplored);

        tile.MarkExplored();

        Assert.True(tile.IsExplored);
    }

    [Fact]
    public void SetVisibility_CannotUnexplore()
    {
        var tile = new Tile(5, 5, TerrainType.Forest);
        tile.MarkExplored();

        tile.SetVisibility(TileVisibility.Unexplored);

        // Should remain explored, not go back to unexplored
        Assert.True(tile.IsExplored);
    }
}

public class TileVisibilityTests
{
    [Fact]
    public void UpdateVisibility_MarksInRangeAsVisible()
    {
        var grid = new TileGrid(10, 10);
        var viewerPos = new GridPosition(5, 5);

        grid.UpdateVisibility(viewerPos, 2);

        Assert.True(grid[5, 5]!.IsVisible);
        Assert.True(grid[5, 6]!.IsVisible);
        Assert.True(grid[6, 5]!.IsVisible);
        Assert.True(grid[5, 7]!.IsVisible); // Range 2
    }

    [Fact]
    public void UpdateVisibility_DowngradesPreviouslyVisibleToExplored()
    {
        var grid = new TileGrid(10, 10);

        // First visibility update at (5, 5)
        grid.UpdateVisibility(new GridPosition(5, 5), 1);
        Assert.True(grid[5, 6]!.IsVisible);

        // Move visibility to (7, 7)
        grid.UpdateVisibility(new GridPosition(7, 7), 1);

        // Previous tile should now be explored but not visible
        Assert.False(grid[5, 6]!.IsVisible);
        Assert.True(grid[5, 6]!.IsExplored);
    }
}

public class GridWorldGeneratorTests
{
    [Fact]
    public void Generate_CreatesCampAtCenter()
    {
        var generator = new GridWorldGenerator { Width = 20, Height = 20 };
        var weather = new Weather();

        var (grid, campTile, camp) = generator.Generate(weather);

        Assert.NotNull(campTile);
        Assert.Equal(camp, campTile.NamedLocation);
        Assert.Equal("Forest Camp", camp.Name);
    }

    [Fact]
    public void Generate_CampIsExplored()
    {
        var generator = new GridWorldGenerator { Width = 20, Height = 20 };
        var weather = new Weather();

        var (grid, campTile, camp) = generator.Generate(weather);

        Assert.True(campTile.IsExplored);
        Assert.True(camp.Explored);
    }

    [Fact]
    public void Generate_SurroundingTilesAreVisible()
    {
        var generator = new GridWorldGenerator { Width = 20, Height = 20 };
        var weather = new Weather();

        var (grid, campTile, camp) = generator.Generate(weather);

        // Camp and immediate surroundings should be visible
        Assert.True(campTile.IsVisible);
        foreach (var neighbor in grid.GetNeighbors(campTile))
        {
            Assert.True(neighbor.IsVisible);
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

        var (grid, campTile, camp) = generator.Generate(weather);

        var namedLocationCount = grid.NamedLocationTiles.Count();
        Assert.True(namedLocationCount > 1, $"Expected multiple named locations, got {namedLocationCount}");
    }

    [Fact]
    public void Generate_HasMountainRangeAtTop()
    {
        var generator = new GridWorldGenerator { Width = 20, Height = 20 };
        var weather = new Weather();

        var (grid, campTile, camp) = generator.Generate(weather);

        // Check top rows have mountains (except for pass)
        int mountainCount = 0;
        for (int x = 0; x < 20; x++)
        {
            if (grid[x, 0]!.Terrain == TerrainType.Mountain)
                mountainCount++;
        }

        Assert.True(mountainCount > 10, "Expected mountains along top edge");
    }
}
