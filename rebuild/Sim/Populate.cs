namespace Survival;

/// Fills the valley with herds at world creation, seeded by its own stream.
// Dens are authored sites (Content) that herds are anchored on. A megafauna herd killed off stays
// dead for the run. Herds leave real prints, so no territory details are placed as pools.
public static partial class Fauna
{
    /// Herds start more than 2.4 km from camp.
    public const int SafeZoneTiles = 24;

    public static void Populate(World world)
    {
        var m = Map.Of(world);
        var rng = world.Rng("populate");
        var interior = InteriorTiles(m);
        // Predators keep out of each other's land; prey graze anywhere outside the safe zone.
        var available = interior.Where(p => MapGen.Manhattan(p, m.Camp) > SafeZoneTiles).ToList();
        // Prey land may overlap a pack's, so packs meet prey on their own land.
        var preyLand = available.ToList();
        var wolfLand = new HashSet<TileId>();

        void Herd(Species species, TileId start, List<TileId> territory, int members) =>
            Agents.Spawn(world, species, start, members, territory);

        // Every Wolf Den has a pack, first, its land grown around the den, so a den may sit inside camp's safe zone.
        var wolfDens = SitesNamed(m, "wolf_den");
        for (int i = 0, packs = Math.Max(1 + rng.NextInt(2), wolfDens.Count); i < packs; i++)
        {
            bool den = i < wolfDens.Count;
            if (!den && available.Count == 0) break;
            var start = den ? wolfDens[i] : available[rng.NextInt(available.Count)];
            var land = Territory(m, start, den ? Around(m, start, 10) : available, 48 + rng.NextInt(33), rng);
            if (land.Count < 3) continue;
            Herd(Species.Wolf, start, land, 3 + rng.NextInt(6));
            wolfLand.UnionWith(land);
            available.RemoveAll(land.ToHashSet().Contains);
        }
        // Only the start is taken: bears overlap. Every Bear Cave holds one.
        var bearCaves = SitesNamed(m, "bear_cave");
        for (int i = 0, bears = Math.Max(3 + rng.NextInt(3), bearCaves.Count); i < bears; i++)
        {
            bool den = i < bearCaves.Count;
            if (!den && available.Count == 0) break;
            var start = den ? bearCaves[i] : available[rng.NextInt(available.Count)];
            var land = Territory(m, start, den ? Around(m, start, 12) : available, 64 + rng.NextInt(65), rng);
            if (land.Count < 3) continue;
            Herd(rng.NextDouble() < 0.5 ? Species.CaveBear : Species.Bear, start, land, 1);
            available.Remove(start);
        }
        for (int i = 0, herds = 2 + rng.NextInt(2); i < herds && preyLand.Count >= 8; i++)
        {
            var start = preyLand[rng.NextInt(preyLand.Count)];
            var land = Territory(m, start, preyLand, 128 + rng.NextInt(65), rng);
            if (land.Count < 6) continue;
            Herd(Species.Caribou, start, land, 5 + rng.NextInt(11));
        }
        int large = 8 + rng.NextInt(8);
        for (int i = 0; i < large / 2 && preyLand.Count > 0; i++)
        {
            var start = preyLand[rng.NextInt(preyLand.Count)];
            var land = Territory(m, start, preyLand, 48 + rng.NextInt(49), rng);
            if (land.Count >= 3) Herd(Species.Megaloceros, start, land, 1 + rng.NextInt(3));
        }
        for (int i = 0; i < large - large / 2 && preyLand.Count > 0; i++)
        {
            var start = preyLand[rng.NextInt(preyLand.Count)];
            var land = Territory(m, start, preyLand, 240 + rng.NextInt(161), rng);
            if (land.Count >= 3) Herd(Species.Bison, start, land, 3 + rng.NextInt(6));
        }
        // Saber-tooths stay off the packs' land.
        for (int i = 0, cats = 1 + rng.NextInt(2); i < cats && available.Count > 0; i++)
        {
            var valid = available.Where(p => !wolfLand.Contains(p)).ToList();
            if (valid.Count == 0) valid = available;
            var start = valid[rng.NextInt(valid.Count)];
            var land = Territory(m, start, available, 96 + rng.NextInt(65), rng);
            if (land.Count < 4) continue;
            Herd(Species.SaberTooth, start, land, 1);
            available.RemoveAll(land.ToHashSet().Contains);
        }
        // Hyenas start just outside a wolf pack's land.
        var hyenaZone = available.Where(p => !wolfLand.Contains(p) && MapGen.Neighbors(p).Any(wolfLand.Contains)).ToList();
        if (hyenaZone.Count < 3) hyenaZone = available.ToList();
        // A start boxed in by the packs' land and ravines is struck off and another drawn, so the valley still gets its packs.
        for (int i = 0, packs = 1 + rng.NextInt(2); i < packs && hyenaZone.Count > 0;)
        {
            var start = hyenaZone[rng.NextInt(hyenaZone.Count)];
            var land = Territory(m, start, available, 64 + rng.NextInt(49), rng);
            if (land.Count < 3) { hyenaZone.Remove(start); continue; }
            i++;
            Herd(Species.Hyena, start, land, 3 + rng.NextInt(4));
            hyenaZone.RemoveAll(land.ToHashSet().Contains);
        }
        if (SitesNamed(m, "bone_hollow") is [var hollow, ..])
        {
            var land = Territory(m, hollow, Around(m, hollow, 40), 192 + rng.NextInt(97), rng);
            Herd(Species.Mammoth, hollow, land.Count < 8 ? [hollow] : land, 8 + rng.NextInt(5));
        }
        PopulateCaves(world, m, rng);
        PopulateCarrion(world, m, interior);
    }

    /// Carrion flocks: one per ~1,500 open tiles. Drawn on their own stream after every other herd, so
    /// they move nothing else's placement.
    static void PopulateCarrion(World world, GeneratedMap m, List<TileId> interior)
    {
        var rng = world.Rng("populate carrion");
        for (int i = 0, flocks = Math.Max(2, interior.Count / 1500); i < flocks && interior.Count > 0; i++)
        {
            var start = interior[rng.NextInt(interior.Count)];
            var land = Territory(m, start, interior, 150 + rng.NextInt(101), rng);
            if (land.Count >= 3) Agents.Spawn(world, Species.Raven, start, 3 + rng.NextInt(4), land);
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

    /// One roll per cave system. Half stand empty; of the rest, most hold a cave bear and some a few
    /// hyenas, denned on a floor tile and ranging only where the cave connects.
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
        foreach (var (caveId, tiles) in caves)
        {
            var floors = tiles.Where(t => m.CaveAt(t) == Cave.Floor).ToList();
            if (floors.Count == 0) continue;
            double roll = rng.NextDouble();
            if (roll < 0.5) continue;
            var species = roll < 0.8 ? Species.CaveBear : Species.Hyena;
            var den = floors[rng.NextInt(floors.Count)];
            // Grown through real connections, so no territory crosses a cave wall.
            var land = new List<TileId> { den };
            var reached = new HashSet<TileId> { den };
            for (int i = 0; i < land.Count; i++)
                foreach (var n in MapGen.Neighbors(land[i]))
                    if (m.InBounds(n) && m.CaveIdAt(n) == caveId && Map.Blocked(m, land[i], n) == null && reached.Add(n)) land.Add(n);
            Agents.Spawn(world, species, den, species == Species.CaveBear ? 1 : 2 + rng.NextInt(3), land);
        }
    }

    /// A random flood fill from the start through the allowed tiles, up to the target size, across only
    /// the edges a herd can walk, so no territory takes in land beyond a ravine or cliff its herd cannot reach.
    static List<TileId> Territory(GeneratedMap m, TileId start, List<TileId> allowed, int targetSize, Rng rng)
    {
        var open = allowed.ToHashSet();
        var land = new List<TileId> { start };
        var taken = new HashSet<TileId> { start };
        var frontier = new List<TileId> { start };
        while (land.Count < targetSize && frontier.Count > 0)
        {
            int pick = rng.NextInt(frontier.Count);
            var current = frontier[pick];
            frontier.RemoveAt(pick);
            foreach (var n in MapGen.Neighbors(current))
            {
                if (land.Count >= targetSize) break;
                if (!open.Contains(n) || taken.Contains(n) || Map.Blocked(m, current, n) != null) continue;
                taken.Add(n);
                land.Add(n);
                frontier.Add(n);
            }
        }
        return land;
    }
}
