using text_survival.Environments.Grid;

namespace text_survival.Actors.Animals;

/// <summary>
/// Populates the game world with persistent herds during world generation.
/// </summary>
public static class HerdPopulator
{
    private static readonly Random _rng = new();

    /// <summary>
    /// Populate the world with herds. Called from GameContext.CreateNewGame().
    /// </summary>
    public static void Populate(HerdRegistry registry, GameMap map)
    {
        // Get all valid positions (exclude map edges to keep animals in playable area)
        var allPositions = GetInteriorPositions(map);

        // Avoid placing herds too close to camp (give player safe zone)
        var campPos = map.CurrentPosition;
        var safeZone = 3; // Tiles from camp

        var availablePositions = allPositions
            .Where(p => p.ManhattanDistance(campPos) > safeZone)
            .ToList();

        // Population targets from plan:
        // Wolf packs: 1-2 packs of 3-8 wolves, 3-5 tile territories
        // Bears: 3-5 solitary, 2-3 tile territories
        // Caribou herds: 1-2 herds of 5-15, 8-12 tile territories
        // Large individual prey: 5-10 (megaloceros, bison)

        PopulateWolves(registry, availablePositions, 1 + _rng.Next(2)); // 1-2 packs
        PopulateBears(registry, availablePositions, 3 + _rng.Next(3)); // 3-5 bears
        PopulateCaribou(registry, availablePositions, 1 + _rng.Next(2)); // 1-2 herds
        PopulateLargePrey(registry, availablePositions, 5 + _rng.Next(6)); // 5-10 individuals
    }

    /// <summary>
    /// Get all grid positions that are not on the map edges.
    /// </summary>
    private static List<GridPosition> GetInteriorPositions(GameMap map)
    {
        var positions = new List<GridPosition>();
        for (int x = 2; x < map.Width - 2; x++)
        {
            for (int y = 2; y < map.Height - 2; y++)
            {
                var loc = map.GetLocationAt(x, y);
                if (loc != null && loc.IsPassable)
                {
                    positions.Add(new GridPosition(x, y));
                }
            }
        }
        return positions;
    }

    /// <summary>
    /// Create wolf packs with overlapping patrol territories.
    /// </summary>
    private static void PopulateWolves(HerdRegistry registry, List<GridPosition> available, int packCount)
    {
        for (int i = 0; i < packCount; i++)
        {
            if (available.Count == 0) break;

            // Pick a random starting position
            var startPos = available[_rng.Next(available.Count)];

            // Create territory of 3-5 adjacent tiles
            var territory = CreateContiguousTerritory(startPos, available, 3 + _rng.Next(3));

            if (territory.Count < 3) continue; // Need minimum territory

            // Create pack with 3-8 wolves
            var herd = Herd.Create("Wolf", startPos, territory);
            int packSize = 3 + _rng.Next(6);

            for (int j = 0; j < packSize; j++)
            {
                var wolf = AnimalFactory.MakeWolf();
                if (wolf != null)
                {
                    herd.AddMember(wolf);
                }
            }

            registry.AddHerd(herd);

            // Remove territory from available (prevents overlapping predator territories)
            foreach (var pos in territory)
            {
                available.Remove(pos);
            }
        }
    }

    /// <summary>
    /// Create solitary bears with small home ranges.
    /// </summary>
    private static void PopulateBears(HerdRegistry registry, List<GridPosition> available, int bearCount)
    {
        for (int i = 0; i < bearCount; i++)
        {
            if (available.Count == 0) break;

            var startPos = available[_rng.Next(available.Count)];

            // Bears have smaller territories (2-3 tiles)
            var territory = CreateContiguousTerritory(startPos, available, 2 + _rng.Next(2));

            if (territory.Count < 2) continue;

            // Create "herd" of 1 bear
            var herd = Herd.Create("Bear", startPos, territory);

            // 50% chance of cave bear vs regular bear
            var bear = _rng.NextDouble() < 0.5 ? AnimalFactory.MakeCaveBear() : AnimalFactory.MakeBear();
            if (bear != null)
            {
                herd.AddMember(bear);
            }

            registry.AddHerd(herd);

            // Remove only the center tile (bears can overlap with prey)
            available.Remove(startPos);
        }
    }

    /// <summary>
    /// Create caribou herds with large grazing territories.
    /// </summary>
    private static void PopulateCaribou(HerdRegistry registry, List<GridPosition> available, int herdCount)
    {
        for (int i = 0; i < herdCount; i++)
        {
            if (available.Count < 8) break;

            var startPos = available[_rng.Next(available.Count)];

            // Caribou have large territories (8-12 tiles)
            var territory = CreateContiguousTerritory(startPos, available, 8 + _rng.Next(5));

            if (territory.Count < 6) continue;

            var herd = Herd.Create("Caribou", startPos, territory);

            // Herd size 5-15
            int herdSize = 5 + _rng.Next(11);
            for (int j = 0; j < herdSize; j++)
            {
                var caribou = AnimalFactory.MakeCaribou();
                if (caribou != null)
                {
                    herd.AddMember(caribou);
                }
            }

            registry.AddHerd(herd);
        }
    }

    /// <summary>
    /// Create individual large prey animals (megaloceros, bison).
    /// </summary>
    private static void PopulateLargePrey(HerdRegistry registry, List<GridPosition> available, int count)
    {
        for (int i = 0; i < count; i++)
        {
            if (available.Count == 0) break;

            var startPos = available[_rng.Next(available.Count)];

            // Small territory (1-2 tiles)
            var territory = CreateContiguousTerritory(startPos, available, 1 + _rng.Next(2));

            // Alternate between megaloceros and bison
            string animalType = i % 2 == 0 ? "Megaloceros" : "Bison";

            var herd = Herd.Create(animalType, startPos, territory);

            // Small groups (1-3 for megaloceros, 3-8 for bison)
            int groupSize = animalType == "Megaloceros"
                ? 1 + _rng.Next(3)
                : 3 + _rng.Next(6);

            for (int j = 0; j < groupSize; j++)
            {
                var animal = animalType == "Megaloceros"
                    ? AnimalFactory.MakeMegaloceros()
                    : AnimalFactory.MakeSteppeBison();

                if (animal != null)
                {
                    herd.AddMember(animal);
                }
            }

            registry.AddHerd(herd);
        }
    }

    /// <summary>
    /// Create a contiguous territory of tiles using flood-fill from a starting position.
    /// </summary>
    private static List<GridPosition> CreateContiguousTerritory(
        GridPosition start,
        List<GridPosition> available,
        int targetSize)
    {
        var territory = new List<GridPosition> { start };
        var frontier = new List<GridPosition> { start };

        while (territory.Count < targetSize && frontier.Count > 0)
        {
            // Pick a random frontier tile
            var current = frontier[_rng.Next(frontier.Count)];
            frontier.Remove(current);

            // Try to expand to neighbors
            foreach (var neighbor in current.GetCardinalNeighbors())
            {
                if (territory.Count >= targetSize) break;

                if (available.Contains(neighbor) && !territory.Contains(neighbor))
                {
                    territory.Add(neighbor);
                    frontier.Add(neighbor);
                }
            }
        }

        return territory;
    }
}
