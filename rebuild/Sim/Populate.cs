namespace Survival;

/// Fills the valley with herds at world creation, seeded by its own stream.
// Dens are authored sites (Content) that predators are put at; a megafauna herd killed off stays dead for the run.
public static partial class Fauna
{
    /// Herds start more than 2.4 km from camp.
    public const int SafeZoneTiles = 24;

    /// Predators are put this far apart: the ground around one is off-limits to the next.
    public const int SpacingTiles = 6;

    public static void Populate(World world)
    {
        var m = Map.Of(world);
        var rng = world.Rng("populate");
        var interior = InteriorTiles(m);
        var available = interior.Where(p => MapGen.Manhattan(p, m.Camp) > SafeZoneTiles).ToList();
        var preyLand = available.ToList();
        var wolfGround = new HashSet<TileId>();

        // Every Wolf Den has a pack, first, so a den may sit inside camp's safe zone.
        var wolfDens = SitesNamed(m, "wolf_den");
        for (int i = 0, packs = Math.Max(1 + rng.NextInt(2), wolfDens.Count); i < packs; i++)
        {
            bool den = i < wolfDens.Count;
            if (!den && available.Count == 0) break;
            var start = den ? wolfDens[i] : available[rng.NextInt(available.Count)];
            var ground = Reach(m, start, den ? Around(m, start, SpacingTiles) : available, SpacingTiles);
            if (ground.Count < 3) continue;
            Agents.SpawnGroup(world, Species.Wolf, start, 3 + rng.NextInt(6));
            wolfGround.UnionWith(ground);
            available.RemoveAll(ground.ToHashSet().Contains);
        }
        // Only the start is taken: bears overlap. Every Bear Cave holds one.
        var bearCaves = SitesNamed(m, "bear_cave");
        for (int i = 0, bears = Math.Max(3 + rng.NextInt(3), bearCaves.Count); i < bears; i++)
        {
            bool den = i < bearCaves.Count;
            if (!den && available.Count == 0) break;
            var start = den ? bearCaves[i] : available[rng.NextInt(available.Count)];
            if (Reach(m, start, den ? Around(m, start, SpacingTiles) : available, SpacingTiles).Count < 3) continue;
            Agents.SpawnGroup(world, rng.NextDouble() < 0.5 ? Species.CaveBear : Species.Bear, start, 1);
            available.Remove(start);
        }
        for (int i = 0, herds = 2 + rng.NextInt(2); i < herds && preyLand.Count >= 8; i++)
        {
            var start = preyLand[rng.NextInt(preyLand.Count)];
            if (Reach(m, start, preyLand, SpacingTiles).Count >= 6) Agents.SpawnGroup(world, Species.Caribou, start, 5 + rng.NextInt(11));
        }
        int large = 8 + rng.NextInt(8);
        for (int i = 0; i < large / 2 && preyLand.Count > 0; i++)
        {
            var start = preyLand[rng.NextInt(preyLand.Count)];
            if (Reach(m, start, preyLand, SpacingTiles).Count >= 3) Agents.SpawnGroup(world, Species.Megaloceros, start, 1 + rng.NextInt(3));
        }
        for (int i = 0; i < large - large / 2 && preyLand.Count > 0; i++)
        {
            var start = preyLand[rng.NextInt(preyLand.Count)];
            if (Reach(m, start, preyLand, SpacingTiles).Count >= 3) Agents.SpawnGroup(world, Species.Bison, start, 3 + rng.NextInt(6));
        }
        // Saber-tooths stay off the packs' ground.
        for (int i = 0, cats = 1 + rng.NextInt(2); i < cats && available.Count > 0; i++)
        {
            var valid = available.Where(p => !wolfGround.Contains(p)).ToList();
            if (valid.Count == 0) valid = available;
            var start = valid[rng.NextInt(valid.Count)];
            var ground = Reach(m, start, available, SpacingTiles);
            if (ground.Count < 4) continue;
            Agents.SpawnGroup(world, Species.SaberTooth, start, 1);
            available.RemoveAll(ground.ToHashSet().Contains);
        }
        // Hyenas start just outside a wolf pack's ground.
        var hyenaZone = available.Where(p => !wolfGround.Contains(p) && MapGen.Neighbors(p).Any(wolfGround.Contains)).ToList();
        if (hyenaZone.Count < 3) hyenaZone = available.ToList();
        // A start boxed in by the packs' ground and ravines is struck off and another drawn, so the valley still gets its packs.
        for (int i = 0, packs = 1 + rng.NextInt(2); i < packs && hyenaZone.Count > 0;)
        {
            var start = hyenaZone[rng.NextInt(hyenaZone.Count)];
            var ground = Reach(m, start, available, SpacingTiles);
            if (ground.Count < 3) { hyenaZone.Remove(start); continue; }
            i++;
            Agents.SpawnGroup(world, Species.Hyena, start, 3 + rng.NextInt(4));
            hyenaZone.RemoveAll(ground.ToHashSet().Contains);
        }
        if (SitesNamed(m, "bone_hollow") is [var hollow, ..]) Agents.SpawnGroup(world, Species.Mammoth, hollow, 8 + rng.NextInt(5));
        PopulateCaves(world, m, rng);
        PopulateOpportunists(world, m, interior);
    }

    /// Ravens, then foxes: a group per ~1,500 open tiles, sized as one arriving, on their own stream so they move nothing else.
    static void PopulateOpportunists(World world, GeneratedMap m, List<TileId> interior)
    {
        var rng = world.Rng("populate carrion");
        foreach (var species in Enum.GetValues<Species>().Where(s => s != Species.Human && BehaviorOf(s) == HerdBehavior.Opportunist).OrderBy(s => !Flies(s)))
        {
            if (RenewalOf(species)?.Arrival is not ArrivalRow arrival) continue;
            for (int i = 0, groups = Math.Max(2, interior.Count / 1500); i < groups && interior.Count > 0; i++)
            {
                var start = interior[rng.NextInt(interior.Count)];
                int members = arrival.MinMembers + rng.NextInt(arrival.MaxMembers - arrival.MinMembers + 1);
                if (Reach(m, start, interior, SpacingTiles).Count >= 3) Agents.SpawnGroup(world, species, start, members);
            }
        }
    }

    /// Where herds are put, and where they arrive (Arrive): off the map's edge, caves and pass corridor.
    static List<TileId> InteriorTiles(GeneratedMap m)
    {
        var interior = new List<TileId>();
        for (int x = 2; x < m.Width - 2; x++)
            for (int y = 2; y < m.Height - 2; y++)
            {
                var t = new TileId(x, y);
                if (m.TerrainAt(t).IsPassable() && m.CaveAt(t) == Cave.None && !m.OnPass(t)) interior.Add(t);
            }
        return interior;
    }

    static List<TileId> SitesNamed(GeneratedMap m, string id) =>
        [.. m.Sites.Where(s => s.Value.Id == id).Select(s => s.Key).OrderBy(t => t)];

    /// Open ground within a square around a den, off caves and the whole pass, so no den ranges up it.
    static List<TileId> Around(GeneratedMap m, TileId center, int radius)
    {
        var tiles = new List<TileId>();
        for (int dx = -radius; dx <= radius; dx++)
            for (int dy = -radius; dy <= radius; dy++)
            {
                var t = new TileId(center.X + dx, center.Y + dy);
                if (m.InBounds(t) && m.TerrainAt(t).IsPassable() && m.CaveAt(t) == Cave.None && !m.OnPass(t)) tiles.Add(t);
            }
        return tiles;
    }

    /// One roll per cave system: half stand empty, most of the rest hold a cave bear, some a few hyenas, on a floor tile.
    static void PopulateCaves(World world, GeneratedMap m, Rng rng)
    {
        var caves = new SortedDictionary<int, List<TileId>>();
        for (int x = 0; x < m.Width; x++)
            for (int y = 0; y < m.Height; y++)
            {
                var t = new TileId(x, y);
                if (m.CaveIdAt(t) is int id && m.CaveAt(t) != Cave.None)
                    (caves.TryGetValue(id, out var list) ? list : caves[id] = []).Add(t);
            }
        foreach (var (_, tiles) in caves)
        {
            var floors = tiles.Where(t => m.CaveAt(t) == Cave.Floor).ToList();
            if (floors.Count == 0) continue;
            double roll = rng.NextDouble();
            if (roll < 0.5) continue;
            var species = roll < 0.8 ? Species.CaveBear : Species.Hyena;
            var den = floors[rng.NextInt(floors.Count)];
            Agents.SpawnGroup(world, species, den, species == Species.CaveBear ? 1 : 2 + rng.NextInt(3));
        }
    }

    /// The allowed tiles within radius a walker reaches from the start, breadth first so no roll is spent: a penned start reaches few.
    static List<TileId> Reach(GeneratedMap m, TileId start, List<TileId> allowed, int radius)
    {
        var open = allowed.ToHashSet();
        var reached = new List<TileId> { start };
        var seen = new HashSet<TileId> { start };
        for (int i = 0; i < reached.Count; i++)
            foreach (var n in MapGen.Neighbors(reached[i]))
                if (open.Contains(n) && MapGen.Manhattan(n, start) <= radius && Map.Blocked(m, reached[i], n) == null && seen.Add(n)) reached.Add(n);
        return reached;
    }
}
