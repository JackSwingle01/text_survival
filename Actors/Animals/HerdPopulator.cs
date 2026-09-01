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
    public static void Populate(List<Herd> registry, GameMap map)
    {
        // Get all valid positions (exclude map edges to keep animals in playable area)
        var allPositions = GetInteriorPositions(map);

        // Avoid placing herds too close to camp (give player safe zone)
        var campPos = map.CurrentPosition;
        var safeZone = 24; // Tiles from camp (~2.4km)

        var availablePositions = allPositions
            .Where(p => p.ManhattanDistance(campPos) > safeZone)
            .ToList();

        // Population targets from plan:
        // Wolf packs: 1-2 packs of 3-8 wolves, 3-5 tile territories
        // Bears: 3-5 solitary, 2-3 tile territories
        // Caribou herds: 1-2 herds of 5-15, 8-12 tile territories
        // Large individual prey: 5-10 (megaloceros, bison)

        // Dens are authored locations; the predator that lives there is a herd anchored on it.
        PopulateWolves(registry, availablePositions, 1 + _rng.Next(2), map, dens: FindLocations(map, "Wolf Den")); // 1-2 roaming packs + one per den
        PopulateBears(registry, availablePositions, 3 + _rng.Next(3), map, dens: FindLocations(map, "Bear Cave")); // 3-5 roaming bears + one per cave
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
    private static void AddTerritoryDetails(List<Herd> registry, GameMap map)
    {
        foreach (var herd in registry)
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
    /// <summary>
    /// Start positions for a population: every den first, then random picks up to the requested count.
    /// </summary>
    private static List<GridPosition> PickStarts(List<GridPosition> dens, List<GridPosition> available, int count)
    {
        var starts = new List<GridPosition>(dens);
        while (starts.Count < Math.Max(count, dens.Count) && available.Count > 0)
        {
            var pos = available[_rng.Next(available.Count)];
            if (!starts.Contains(pos)) starts.Add(pos);
        }
        return starts;
    }

    /// <summary>
    /// Tiles a territory may grow into. Den-anchored herds use the map around the den;
    /// roaming herds use the safe-zone-filtered pool.
    /// </summary>
    private static List<GridPosition> TerritoryCandidates(GridPosition start, List<GridPosition> dens, List<GridPosition> available, GameMap map, int radius)
        => dens.Contains(start) ? GetPositionsInRadius(map, start, radius) : available;

    /// <summary>
    /// Positions of every named location matching the given name.
    /// </summary>
    private static List<GridPosition> FindLocations(GameMap map, string name)
    {
        var result = new List<GridPosition>();
        for (int x = 0; x < map.Width; x++)
            for (int y = 0; y < map.Height; y++)
                if (map.GetLocationAt(x, y)?.Name == name)
                    result.Add(new GridPosition(x, y));
        return result;
    }

    private static List<GridPosition> GetInteriorPositions(GameMap map)
    {
        var positions = new List<GridPosition>();
        for (int x = 24; x < map.Width - 24; x++)
        {
            for (int y = 24; y < map.Height - 24; y++)
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
    private static void PopulateWolves(List<Herd> registry, List<GridPosition> available, int packCount, GameMap map, List<GridPosition> dens)
    {
        foreach (var startPos in PickStarts(dens, available, packCount))
        {
            // Create territory of 48-80 adjacent tiles. Dens may sit inside the camp safe zone,
            // so their territory grows from the map rather than the safe-zone-filtered list.
            var territory = CreateContiguousTerritory(startPos, TerritoryCandidates(startPos, dens, available, map, 10), 48 + _rng.Next(33));

            if (territory.Count < 3) continue; // Need minimum territory

            var location = map.GetLocationAt(startPos);
            if (location == null) continue;

            // Create pack with 3-8 wolves
            var herd = Herd.Create(AnimalType.Wolf, location, map, territory);
            int packSize = 3 + _rng.Next(6);

            for (int j = 0; j < packSize; j++)
            {
                var wolf = AnimalFactory.MakeWolf(location, map);
                if (wolf != null)
                {
                    herd.AddMember(wolf);
                }
            }

            registry.Add(herd);

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
    private static void PopulateBears(List<Herd> registry, List<GridPosition> available, int bearCount, GameMap map, List<GridPosition> dens)
    {
        foreach (var startPos in PickStarts(dens, available, bearCount))
        {
            // Bears have moderate territories (64-128 tiles) to spread out foraging impact
            var territory = CreateContiguousTerritory(startPos, TerritoryCandidates(startPos, dens, available, map, 12), 64 + _rng.Next(65));

            if (territory.Count < 3) continue;

            var location = map.GetLocationAt(startPos);
            if (location == null) continue;

            // Create "herd" of 1 bear
            var herd = Herd.Create(AnimalType.Bear, location, map, territory);

            // 50% chance of cave bear vs regular bear
            var bear = _rng.NextDouble() < 0.5 ? AnimalFactory.MakeCaveBear(location, map) : AnimalFactory.MakeBear(location, map);
            if (bear != null)
            {
                herd.AddMember(bear);
            }

            registry.Add(herd);

            // Remove only the center tile (bears can overlap with prey)
            available.Remove(startPos);
        }
    }

    /// <summary>
    /// Create caribou herds with large grazing territories.
    /// </summary>
    private static void PopulateCaribou(List<Herd> registry, List<GridPosition> available, int herdCount, GameMap map)
    {
        for (int i = 0; i < herdCount; i++)
        {
            if (available.Count < 8) break;

            var startPos = available[_rng.Next(available.Count)];

            // Caribou have large territories (128-192 tiles)
            var territory = CreateContiguousTerritory(startPos, available, 128 + _rng.Next(65));

            if (territory.Count < 6) continue;

            var location = map.GetLocationAt(startPos);
            if (location == null) continue;

            var herd = Herd.Create(AnimalType.Caribou, location, map, territory);

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

            registry.Add(herd);
        }
    }

    /// <summary>
    /// Create individual large prey animals (megaloceros, bison).
    /// </summary>
    private static void PopulateLargePrey(List<Herd> registry, List<GridPosition> available, int count, GameMap map)
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
    private static void PopulateMegaloceros(List<Herd> registry, List<GridPosition> available, int count, GameMap map)
    {
        for (int i = 0; i < count; i++)
        {
            if (available.Count == 0) break;

            var startPos = available[_rng.Next(available.Count)];

            // Megaloceros: 48-96 tile territories
            var territory = CreateContiguousTerritory(startPos, available, 48 + _rng.Next(49));

            if (territory.Count < 3) continue;

            var location = map.GetLocationAt(startPos);
            if (location == null) continue;

            var herd = Herd.Create(AnimalType.Megaloceros, location, map, territory);

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

            registry.Add(herd);
        }
    }

    /// <summary>
    /// Create bison herds (larger groups needing expansive grazing territories).
    /// </summary>
    private static void PopulateBison(List<Herd> registry, List<GridPosition> available, int count, GameMap map)
    {
        for (int i = 0; i < count; i++)
        {
            if (available.Count == 0) break;

            var startPos = available[_rng.Next(available.Count)];

            // Bison: 240-400 tile territories (large grazers need space)
            var territory = CreateContiguousTerritory(startPos, available, 240 + _rng.Next(161));

            if (territory.Count < 3) continue;

            var location = map.GetLocationAt(startPos);
            if (location == null) continue;

            var herd = Herd.Create(AnimalType.Bison, location, map, territory);

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

            registry.Add(herd);
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

        var location = ctx.Map.GetLocationAt(position);
        if (location == null) return null;

        // Create herd
        var herd = Herd.Create(animalType, location, ctx.Map, territory);

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
            ctx.Herds.Add(herd);
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
    private static void PopulateSaberTooths(List<Herd> registry, List<GridPosition> available, int count, GameMap map)
    {
        // Get wolf territories to avoid overlap
        var wolfTerritories = registry
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

            // Saber-tooths have large territories (96-160 tiles)
            var territory = CreateContiguousTerritory(startPos, available, 96 + _rng.Next(65));

            if (territory.Count < 4) continue;

            var location = map.GetLocationAt(startPos);
            if (location == null) continue;

            // Create "herd" of 1 saber-tooth
            var herd = Herd.Create(AnimalType.SaberTooth, location, map, territory);

            var cat = AnimalFactory.MakeSaberToothTiger(location, map);
            if (cat != null)
            {
                herd.AddMember(cat);
            }

            registry.Add(herd);

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
    private static void PopulateHyenas(List<Herd> registry, List<GridPosition> available, int packCount, GameMap map)
    {
        // Get wolf territories to spawn hyenas nearby
        var wolfTerritories = registry
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

            // Hyena territories: 64-112 tiles
            var territory = CreateContiguousTerritory(startPos, available, 64 + _rng.Next(49));

            if (territory.Count < 3) continue;

            var location = map.GetLocationAt(startPos);
            if (location == null) continue;

            var herd = Herd.Create(AnimalType.Hyena, location, map, territory);

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

            registry.Add(herd);

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
    private static void PopulateMammoths(List<Herd> registry, GameMap map)
    {
        GridPosition? boneHollowPos = FindLocations(map, "Bone Hollow").Cast<GridPosition?>().FirstOrDefault();

        if (boneHollowPos == null)
        {
            // Bone Hollow not found - skip mammoth population
            return;
        }

        // Get available positions around Bone Hollow
        var available = GetPositionsInRadius(map, boneHollowPos.Value, 40);

        // Create large territory centered on Bone Hollow (192-288 tiles)
        var territory = CreateContiguousTerritory(boneHollowPos.Value, available, 192 + _rng.Next(97));

        if (territory.Count < 8)
        {
            // Not enough space - minimal territory
            territory = [boneHollowPos.Value];
        }

        var location = map.GetLocationAt(boneHollowPos.Value);
        if (location == null) return;

        var herd = Herd.Create(AnimalType.Mammoth, location, map, territory);

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

        registry.Add(herd);
    }

}
