using text_survival.Actions;
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
        var safeZone = 6; // Tiles from camp (~1.5 miles)

        var availablePositions = allPositions
            .Where(p => p.ManhattanDistance(campPos) > safeZone)
            .ToList();

        // Population targets from plan:
        // Wolf packs: 1-2 packs of 3-8 wolves, 3-5 tile territories
        // Bears: 3-5 solitary, 2-3 tile territories
        // Caribou herds: 1-2 herds of 5-15, 8-12 tile territories
        // Large individual prey: 5-10 (megaloceros, bison)

        PopulateWolves(registry, availablePositions, 1 + _rng.Next(2), map); // 1-2 packs
        PopulateBears(registry, availablePositions, 3 + _rng.Next(3), map); // 3-5 bears
        PopulateCaribou(registry, availablePositions, 2 + _rng.Next(2), map); // 2-3 herds
        PopulateLargePrey(registry, availablePositions, 8 + _rng.Next(8), map); // 8-15 individuals

        // New animals
        PopulateSaberTooths(registry, availablePositions, 1 + _rng.Next(2), map); // 1-2 apex predators
        PopulateHyenas(registry, availablePositions, 1 + _rng.Next(2), map); // 1-2 packs
        PopulateMammoths(registry, map); // Single herd centered on Bone Hollow

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
                // 10% chance per territory tile to add a detail
                if (_rng.NextDouble() > 0.10) continue;

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
    private static EnvironmentalDetail? CreateTerritoryDetail(AnimalType animalType, bool isPredator)
    {
        // Vary the type of detail
        double roll = _rng.NextDouble();

        if (roll < 0.5)
        {
            // Tracks are most common
            return EnvironmentalDetail.AnimalTracks(animalType);
        }
        else if (roll < 0.8)
        {
            // Droppings are moderately common
            return EnvironmentalDetail.AnimalDroppings(animalType);
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
        for (int x = 6; x < map.Width - 6; x++)
        {
            for (int y = 6; y < map.Height - 6; y++)
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
    private static void PopulateWolves(HerdRegistry registry, List<GridPosition> available, int packCount, GameMap map)
    {
        for (int i = 0; i < packCount; i++)
        {
            if (available.Count == 0) break;

            // Pick a random starting position
            var startPos = available[_rng.Next(available.Count)];

            // Create territory of 12-20 adjacent tiles
            var territory = CreateContiguousTerritory(startPos, available, 12 + _rng.Next(9));

            if (territory.Count < 3) continue; // Need minimum territory

            // Create pack with 3-8 wolves
            var herd = Herd.Create(AnimalType.Wolf, startPos, territory);
            int packSize = 3 + _rng.Next(6);

            var location = map.GetLocationAt(startPos);

            for (int j = 0; j < packSize; j++)
            {
                var wolf = AnimalFactory.MakeWolf(location, map);
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
    private static void PopulateBears(HerdRegistry registry, List<GridPosition> available, int bearCount, GameMap map)
    {
        for (int i = 0; i < bearCount; i++)
        {
            if (available.Count == 0) break;

            var startPos = available[_rng.Next(available.Count)];

            // Bears have moderate territories (16-32 tiles) to spread out foraging impact
            var territory = CreateContiguousTerritory(startPos, available, 16 + _rng.Next(17));

            if (territory.Count < 3) continue;

            // Create "herd" of 1 bear
            var herd = Herd.Create(AnimalType.Bear, startPos, territory);

            var location = map.GetLocationAt(startPos);

            // 50% chance of cave bear vs regular bear
            var bear = _rng.NextDouble() < 0.5 ? AnimalFactory.MakeCaveBear(location, map) : AnimalFactory.MakeBear(location, map);
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
    private static void PopulateCaribou(HerdRegistry registry, List<GridPosition> available, int herdCount, GameMap map)
    {
        for (int i = 0; i < herdCount; i++)
        {
            if (available.Count < 8) break;

            var startPos = available[_rng.Next(available.Count)];

            // Caribou have large territories (32-48 tiles)
            var territory = CreateContiguousTerritory(startPos, available, 32 + _rng.Next(17));

            if (territory.Count < 6) continue;

            var herd = Herd.Create(AnimalType.Caribou, startPos, territory);

            var location = map.GetLocationAt(startPos);

            // Herd size 5-15
            int herdSize = 5 + _rng.Next(11);
            for (int j = 0; j < herdSize; j++)
            {
                var caribou = AnimalFactory.MakeCaribou(location, map);
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
    private static void PopulateLargePrey(HerdRegistry registry, List<GridPosition> available, int count, GameMap map)
    {
        // Split count between types
        int megalocerosCount = count / 2;
        int bisonCount = count - megalocerosCount;

        PopulateMegaloceros(registry, available, megalocerosCount, map);
        PopulateBison(registry, available, bisonCount, map);
    }

    /// <summary>
    /// Create megaloceros herds (small groups in medium territories).
    /// </summary>
    private static void PopulateMegaloceros(HerdRegistry registry, List<GridPosition> available, int count, GameMap map)
    {
        for (int i = 0; i < count; i++)
        {
            if (available.Count == 0) break;

            var startPos = available[_rng.Next(available.Count)];

            // Megaloceros: 12-24 tile territories
            var territory = CreateContiguousTerritory(startPos, available, 12 + _rng.Next(13));

            if (territory.Count < 3) continue;

            var herd = Herd.Create(AnimalType.Megaloceros, startPos, territory);

            var location = map.GetLocationAt(startPos);

            // Small groups (1-3)
            int groupSize = 1 + _rng.Next(3);
            for (int j = 0; j < groupSize; j++)
            {
                var animal = AnimalFactory.MakeMegaloceros(location, map);
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
    private static void PopulateBison(HerdRegistry registry, List<GridPosition> available, int count, GameMap map)
    {
        for (int i = 0; i < count; i++)
        {
            if (available.Count == 0) break;

            var startPos = available[_rng.Next(available.Count)];

            // Bison: 60-100 tile territories (large grazers need space)
            var territory = CreateContiguousTerritory(startPos, available, 60 + _rng.Next(41));

            if (territory.Count < 3) continue;

            var herd = Herd.Create(AnimalType.Bison, startPos, territory);

            var location = map.GetLocationAt(startPos);

            // Larger groups (3-8)
            int groupSize = 3 + _rng.Next(6);
            for (int j = 0; j < groupSize; j++)
            {
                var animal = AnimalFactory.MakeSteppeBison(location, map);
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

    /// <summary>
    /// Spawn a herd at a specific location. Used by discovery events.
    /// </summary>
    /// <param name="ctx">Game context</param>
    /// <param name="animalType">Animal type name (Wolf, Bear, Caribou, etc.)</param>
    /// <param name="count">Number of animals</param>
    /// <param name="position">Position to spawn at</param>
    /// <param name="territoryRadius">Approximate radius for territory</param>
    /// <returns>The created herd, or null if creation failed</returns>
    public static Herd? SpawnHerdAt(GameContext ctx, AnimalType animalType, int count, GridPosition position, int territoryRadius)
    {
        if (ctx.Map == null) return null;

        // Get available positions for territory (passable tiles within radius)
        var available = GetPositionsInRadius(ctx.Map, position, territoryRadius + 2);

        // Create territory
        int targetSize = Math.Max(2, territoryRadius * 2);
        var territory = CreateContiguousTerritory(position, available, targetSize);

        if (territory.Count == 0)
        {
            territory = [position]; // Fallback to just the spawn position
        }

        // Create herd
        var herd = Herd.Create(animalType, position, territory);

        var location = ctx.Map.GetLocationAt(position);

        // Add members
        for (int i = 0; i < count; i++)
        {
            var animal = AnimalFactory.FromType(animalType, location, ctx.Map);
            if (animal != null)
            {
                herd.AddMember(animal);
            }
        }

        // Only add if we have at least one member
        if (herd.Count > 0)
        {
            ctx.Herds.AddHerd(herd);
            return herd;
        }

        return null;
    }

    /// <summary>
    /// Get passable positions within a radius of a center point.
    /// </summary>
    private static List<GridPosition> GetPositionsInRadius(GameMap map, GridPosition center, int radius)
    {
        var positions = new List<GridPosition>();

        for (int dx = -radius; dx <= radius; dx++)
        {
            for (int dy = -radius; dy <= radius; dy++)
            {
                var pos = new GridPosition(center.X + dx, center.Y + dy);
                var loc = map.GetLocationAt(pos);
                if (loc != null && loc.IsPassable)
                {
                    positions.Add(pos);
                }
            }
        }

        return positions;
    }

    /// <summary>
    /// Create solitary saber-tooth tigers (rare apex predators).
    /// </summary>
    private static void PopulateSaberTooths(HerdRegistry registry, List<GridPosition> available, int count, GameMap map)
    {
        // Get wolf territories to avoid overlap
        var wolfTerritories = registry._herds
            .Where(h => h.AnimalType == AnimalType.Wolf)
            .SelectMany(h => h.HomeTerritory)
            .ToHashSet();

        for (int i = 0; i < count; i++)
        {
            if (available.Count == 0) break;

            // Find positions not in wolf territories (apex predators don't share)
            var validPositions = available.Where(p => !wolfTerritories.Contains(p)).ToList();
            if (validPositions.Count == 0) validPositions = available;

            var startPos = validPositions[_rng.Next(validPositions.Count)];

            // Saber-tooths have large territories (24-40 tiles)
            var territory = CreateContiguousTerritory(startPos, available, 24 + _rng.Next(17));

            if (territory.Count < 4) continue;

            // Create "herd" of 1 saber-tooth
            var herd = Herd.Create(AnimalType.SaberTooth, startPos, territory);

            var location = map.GetLocationAt(startPos);
            var cat = AnimalFactory.MakeSaberToothTiger(location, map);
            if (cat != null)
            {
                herd.AddMember(cat);
            }

            registry.AddHerd(herd);

            // Remove territory from available (apex predator)
            foreach (var pos in territory)
            {
                available.Remove(pos);
            }
        }
    }

    /// <summary>
    /// Create hyena packs near wolf territories (scavengers follow predators).
    /// </summary>
    private static void PopulateHyenas(HerdRegistry registry, List<GridPosition> available, int packCount, GameMap map)
    {
        // Get wolf territories to spawn hyenas nearby
        var wolfTerritories = registry._herds
            .Where(h => h.AnimalType == AnimalType.Wolf)
            .SelectMany(h => h.HomeTerritory)
            .ToHashSet();

        // Find positions adjacent to wolf territories but not inside
        var hyenaSpawnZone = available
            .Where(p => !wolfTerritories.Contains(p) &&
                        p.GetCardinalNeighbors().Any(n => wolfTerritories.Contains(n)))
            .ToList();

        // Fallback to any available if no adjacent positions
        if (hyenaSpawnZone.Count < 3) hyenaSpawnZone = available;

        for (int i = 0; i < packCount; i++)
        {
            if (hyenaSpawnZone.Count == 0) break;

            var startPos = hyenaSpawnZone[_rng.Next(hyenaSpawnZone.Count)];

            // Hyena territories: 16-28 tiles
            var territory = CreateContiguousTerritory(startPos, available, 16 + _rng.Next(13));

            if (territory.Count < 3) continue;

            var herd = Herd.Create(AnimalType.Hyena, startPos, territory);

            var location = map.GetLocationAt(startPos);

            // Pack size: 3-6
            int packSize = 3 + _rng.Next(4);
            for (int j = 0; j < packSize; j++)
            {
                var hyena = AnimalFactory.MakeCaveHyena(location, map);
                if (hyena != null)
                {
                    herd.AddMember(hyena);
                }
            }

            registry.AddHerd(herd);

            // Remove spawn zone positions used
            foreach (var pos in territory)
            {
                hyenaSpawnZone.Remove(pos);
            }
        }
    }

    /// <summary>
    /// Create mammoth herd centered on Bone Hollow location.
    /// </summary>
    private static void PopulateMammoths(HerdRegistry registry, GameMap map)
    {
        // Find Bone Hollow by name
        GridPosition? boneHollowPos = null;
        for (int x = 0; x < map.Width; x++)
        {
            for (int y = 0; y < map.Height; y++)
            {
                var loc = map.GetLocationAt(x, y);
                if (loc?.Name == "Bone Hollow")
                {
                    boneHollowPos = new GridPosition(x, y);
                    break;
                }
            }
            if (boneHollowPos != null) break;
        }

        if (boneHollowPos == null)
        {
            // Bone Hollow not found - skip mammoth population
            return;
        }

        // Get available positions around Bone Hollow
        var available = GetPositionsInRadius(map, boneHollowPos.Value, 10);

        // Create large territory centered on Bone Hollow (48-72 tiles)
        var territory = CreateContiguousTerritory(boneHollowPos.Value, available, 48 + _rng.Next(25));

        if (territory.Count < 8)
        {
            // Not enough space - minimal territory
            territory = [boneHollowPos.Value];
        }

        var herd = Herd.Create(AnimalType.Mammoth, boneHollowPos.Value, territory);

        var location = map.GetLocationAt(boneHollowPos.Value);

        // Herd size: 8-12 (realistic matriarchal family group)
        int herdSize = 8 + _rng.Next(5);
        for (int i = 0; i < herdSize; i++)
        {
            var mammoth = AnimalFactory.MakeWoollyMammoth(location, map);
            if (mammoth != null)
            {
                herd.AddMember(mammoth);
            }
        }

        registry.AddHerd(herd);
    }

}
