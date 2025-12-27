namespace text_survival.Environments.Grid;

/// <summary>
/// The 2D tile grid that represents the game world.
/// Manages tile storage, adjacency queries, and visibility calculations.
/// </summary>
public class TileGrid
{
    private readonly Tile[,] _tiles;

    public int Width { get; }
    public int Height { get; }

    /// <summary>
    /// Create a new empty grid of the specified size.
    /// All tiles initialized to Plain terrain and unexplored.
    /// </summary>
    public TileGrid(int width, int height)
    {
        Width = width;
        Height = height;
        _tiles = new Tile[width, height];

        // Initialize all tiles with default terrain
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                _tiles[x, y] = new Tile(x, y, TerrainType.Plain);
            }
        }
    }

    // Indexers //

    /// <summary>
    /// Get tile by coordinates. Returns null if out of bounds.
    /// </summary>
    public Tile? this[int x, int y] =>
        IsInBounds(x, y) ? _tiles[x, y] : null;

    /// <summary>
    /// Get tile by position. Returns null if out of bounds.
    /// </summary>
    public Tile? this[GridPosition pos] =>
        this[pos.X, pos.Y];

    // Bounds Checking //

    /// <summary>
    /// Check if coordinates are within grid bounds.
    /// </summary>
    public bool IsInBounds(int x, int y) =>
        x >= 0 && x < Width && y >= 0 && y < Height;

    /// <summary>
    /// Check if position is within grid bounds.
    /// </summary>
    public bool IsInBounds(GridPosition pos) =>
        IsInBounds(pos.X, pos.Y);

    // Tile Modification //

    /// <summary>
    /// Set a tile at the specified position.
    /// Used by world generator.
    /// </summary>
    public void SetTile(int x, int y, Tile tile)
    {
        if (IsInBounds(x, y))
            _tiles[x, y] = tile;
    }

    /// <summary>
    /// Set terrain type at the specified position.
    /// Creates a new tile with the given terrain.
    /// </summary>
    public void SetTerrain(int x, int y, TerrainType terrain)
    {
        if (IsInBounds(x, y))
            _tiles[x, y] = new Tile(x, y, terrain);
    }

    /// <summary>
    /// Place a named location on a tile.
    /// </summary>
    public void PlaceLocation(int x, int y, Location location)
    {
        var tile = this[x, y];
        if (tile != null)
        {
            tile.NamedLocation = location;
            location.GridPosition = new GridPosition(x, y);
        }
    }

    // Adjacency //

    /// <summary>
    /// Check if two tiles are adjacent (4-way cardinal).
    /// </summary>
    public bool IsAdjacent(Tile a, Tile b) =>
        a.Position.IsAdjacentTo(b.Position);

    /// <summary>
    /// Check if a position is adjacent to another position.
    /// </summary>
    public bool IsAdjacent(GridPosition a, GridPosition b) =>
        a.IsAdjacentTo(b);

    /// <summary>
    /// Get the 4 cardinal neighbors of a tile.
    /// Only returns tiles that exist (within bounds).
    /// </summary>
    public IEnumerable<Tile> GetNeighbors(Tile tile) =>
        GetNeighbors(tile.Position);

    /// <summary>
    /// Get the 4 cardinal neighbors of a position.
    /// Only returns tiles that exist (within bounds).
    /// </summary>
    public IEnumerable<Tile> GetNeighbors(GridPosition position)
    {
        foreach (var neighborPos in position.GetCardinalNeighbors())
        {
            var tile = this[neighborPos];
            if (tile != null)
                yield return tile;
        }
    }

    /// <summary>
    /// Get passable neighbors (for movement options).
    /// </summary>
    public IEnumerable<Tile> GetPassableNeighbors(Tile tile) =>
        GetNeighbors(tile).Where(t => t.IsPassable);

    // Visibility //

    /// <summary>
    /// Get all tiles within a given range of a position.
    /// Uses Manhattan distance.
    /// </summary>
    public IEnumerable<Tile> GetTilesInRange(GridPosition center, int range)
    {
        foreach (var pos in center.GetPositionsInRange(range))
        {
            var tile = this[pos];
            if (tile != null)
                yield return tile;
        }
    }

    /// <summary>
    /// Update visibility from a given position.
    /// Marks tiles in range as visible, others as explored (if previously visible).
    /// </summary>
    public void UpdateVisibility(GridPosition viewerPosition, int sightRange)
    {
        // First, downgrade all visible tiles to explored
        for (int x = 0; x < Width; x++)
        {
            for (int y = 0; y < Height; y++)
            {
                if (_tiles[x, y].Visibility == TileVisibility.Visible)
                    _tiles[x, y].SetVisibility(TileVisibility.Explored);
            }
        }

        // Then mark tiles in range as visible
        foreach (var tile in GetTilesInRange(viewerPosition, sightRange))
        {
            tile.SetVisibility(TileVisibility.Visible);
        }
    }

    /// <summary>
    /// Calculate sight range from a tile based on visibility factor.
    /// Base range is 2 tiles, modified by location visibility.
    /// </summary>
    public int GetSightRange(Tile tile)
    {
        int baseRange = 2;
        double visibilityBonus = tile.VisibilityFactor - 1.0;  // 0 at baseline
        return Math.Max(1, baseRange + (int)(visibilityBonus * 2));
    }

    // Enumeration //

    /// <summary>
    /// Enumerate all tiles in the grid.
    /// </summary>
    public IEnumerable<Tile> AllTiles
    {
        get
        {
            for (int x = 0; x < Width; x++)
            {
                for (int y = 0; y < Height; y++)
                {
                    yield return _tiles[x, y];
                }
            }
        }
    }

    /// <summary>
    /// Enumerate all explored tiles.
    /// </summary>
    public IEnumerable<Tile> ExploredTiles =>
        AllTiles.Where(t => t.IsExplored);

    /// <summary>
    /// Enumerate all visible tiles.
    /// </summary>
    public IEnumerable<Tile> VisibleTiles =>
        AllTiles.Where(t => t.IsVisible);

    /// <summary>
    /// Find all tiles with named locations.
    /// </summary>
    public IEnumerable<Tile> NamedLocationTiles =>
        AllTiles.Where(t => t.HasNamedLocation);

    /// <summary>
    /// Find a tile by its named location.
    /// </summary>
    public Tile? FindByLocation(Location location) =>
        location.GridPosition.HasValue ? this[location.GridPosition.Value] : null;
}
