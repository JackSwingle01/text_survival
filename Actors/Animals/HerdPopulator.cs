using text_survival.Environments.Features;
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

        // Create prey FIRST so predators can be placed to overlap their territories
        PopulateCaribou(registry, availablePositions, 1 + _rng.Next(2)); // 1-2 herds
        PopulateLargePrey(registry, availablePositions, 5 + _rng.Next(6)); // 5-10 individuals

        // Create predators AFTER prey, biasing toward prey territories
        PopulateWolves(registry, availablePositions, 1 + _rng.Next(2)); // 1-2 packs
        PopulateBears(registry, availablePositions, 3 + _rng.Next(3)); // 3-5 bears

        // Add environmental details based on territories
        AddTerritoryDetails(registry, map);
    }

    /// <summary>
    /// Adds environmental details to tiles within herd territories.
    /// Gives players hints about animal presence through tracks, droppings, etc.
    /// </summary>
    private static void AddTerritoryDetails(HerdRegistry registry, GameMap map)
    {
        foreach (var herd in registry._herds)
        {
            // Skip empty herds
            if (herd.IsEmpty) continue;

            // Add details to territory tiles (but not all - sparse placement)
            foreach (var pos in herd.HomeTerritory)
            {
                // 30% chance per territory tile to add a detail
                if (_rng.NextDouble() > 0.30) continue;

                var location = map.GetLocationAt(pos);
                if (location == null) continue;

                // Create appropriate detail based on animal type
                var detail = CreateTerritoryDetail(herd.AnimalType, herd.IsPredator);
                if (detail != null)
                {
                    location.Features.Add(detail);
                }
            }
        }
    }

    /// <summary>
    /// Creates an environmental detail appropriate for an animal's territory.
    /// </summary>
    private static EnvironmentalDetail? CreateTerritoryDetail(string animalType, bool isPredator)
    {
        // Vary the type of detail
        double roll = _rng.NextDouble();

        if (roll < 0.5)
        {
            // Tracks are most common
            return EnvironmentalDetail.AnimalTracks(animalType.ToLower());
        }
        else if (roll < 0.8)
        {
            // Droppings are moderately common
            return EnvironmentalDetail.AnimalDroppings(animalType.ToLower());
        }
        else if (isPredator)
        {
            // Predator territories have scattered bones from kills
            return EnvironmentalDetail.ScatteredBones();
        }
        else
        {
            // Prey territories have bent branches from browsing
            return EnvironmentalDetail.BentBranches();
        }
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
    /// Create wolf packs with territories that overlap prey ranges.
    /// </summary>
    private static void PopulateWolves(HerdRegistry registry, List<GridPosition> available, int packCount)
    {
        // Get all prey territory tiles for biased placement
        var preyTerritories = registry._herds
            .Where(h => !h.IsPredator)
            .SelectMany(h => h.HomeTerritory)
            .Distinct()
            .ToList();

        for (int i = 0; i < packCount; i++)
        {
            if (available.Count == 0) break;

            // 80% chance to start in prey territory if available
            GridPosition startPos;
            var availablePreyTiles = preyTerritories.Where(p => available.Contains(p)).ToList();

            if (availablePreyTiles.Count > 0 && _rng.NextDouble() < 0.8)
            {
                startPos = availablePreyTiles[_rng.Next(availablePreyTiles.Count)];
            }
            else
            {
                startPos = available[_rng.Next(available.Count)];
            }

            // Create territory of 3-5 adjacent tiles
            var territory = CreateContiguousTerritory(startPos, available, 3 + _rng.Next(3));

            if (territory.Count < 3) continue;

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

            // Only remove center tile to prevent wolf packs stacking on same spot
            available.Remove(startPos);
        }
    }

    /// <summary>
    /// Create solitary bears with territories overlapping prey ranges.
    /// </summary>
    private static void PopulateBears(HerdRegistry registry, List<GridPosition> available, int bearCount)
    {
        // Get all prey territory tiles for biased placement
        var preyTerritories = registry._herds
            .Where(h => !h.IsPredator)
            .SelectMany(h => h.HomeTerritory)
            .Distinct()
            .ToList();

        for (int i = 0; i < bearCount; i++)
        {
            if (available.Count == 0) break;

            // 60% chance to start in prey territory (bears are opportunistic)
            GridPosition startPos;
            var availablePreyTiles = preyTerritories.Where(p => available.Contains(p)).ToList();

            if (availablePreyTiles.Count > 0 && _rng.NextDouble() < 0.6)
            {
                startPos = availablePreyTiles[_rng.Next(availablePreyTiles.Count)];
            }
            else
            {
                startPos = available[_rng.Next(available.Count)];
            }

            // Bears have moderate territories (4-8 tiles)
            var territory = CreateContiguousTerritory(startPos, available, 4 + _rng.Next(5));

            if (territory.Count < 3) continue;

            var herd = Herd.Create("Bear", startPos, territory);

            // 50% chance of cave bear vs regular bear
            var bear = _rng.NextDouble() < 0.5 ? AnimalFactory.MakeCaveBear() : AnimalFactory.MakeBear();
            if (bear != null)
            {
                herd.AddMember(bear);
            }

            registry.AddHerd(herd);

            // Remove only the center tile
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
        // Split count between types
        int megalocerosCount = count / 2;
        int bisonCount = count - megalocerosCount;

        PopulateMegaloceros(registry, available, megalocerosCount);
        PopulateBison(registry, available, bisonCount);
    }

    /// <summary>
    /// Create megaloceros herds (small groups in medium territories).
    /// </summary>
    private static void PopulateMegaloceros(HerdRegistry registry, List<GridPosition> available, int count)
    {
        for (int i = 0; i < count; i++)
        {
            if (available.Count == 0) break;

            var startPos = available[_rng.Next(available.Count)];

            // Megaloceros: 3-6 tile territories
            var territory = CreateContiguousTerritory(startPos, available, 3 + _rng.Next(4));

            if (territory.Count < 3) continue;

            var herd = Herd.Create("Megaloceros", startPos, territory);

            // Small groups (1-3)
            int groupSize = 1 + _rng.Next(3);
            for (int j = 0; j < groupSize; j++)
            {
                var animal = AnimalFactory.MakeMegaloceros();
                if (animal != null)
                {
                    herd.AddMember(animal);
                }
            }

            registry.AddHerd(herd);
        }
    }

    /// <summary>
    /// Create bison herds (larger groups needing expansive grazing territories).
    /// </summary>
    private static void PopulateBison(HerdRegistry registry, List<GridPosition> available, int count)
    {
        for (int i = 0; i < count; i++)
        {
            if (available.Count == 0) break;

            var startPos = available[_rng.Next(available.Count)];

            // Bison: 15-25 tile territories (large grazers need space)
            var territory = CreateContiguousTerritory(startPos, available, 15 + _rng.Next(11));

            if (territory.Count < 3) continue;

            var herd = Herd.Create("Bison", startPos, territory);

            // Larger groups (3-8)
            int groupSize = 3 + _rng.Next(6);
            for (int j = 0; j < groupSize; j++)
            {
                var animal = AnimalFactory.MakeSteppeBison();
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
