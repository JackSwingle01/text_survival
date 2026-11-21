namespace Survival;

/// Filling the valley with herds when the world is made (port of old HerdPopulator). Seeded by its
/// own stream, so the same seed gives the same herds on the same land.
// Dens are authored sites (Content): the Wolf Den's pack, the Bear Cave's bear and Bone Hollow's
// mammoths are herds anchored on them, as the old HerdPopulator anchored them. Nothing repopulates
// the valley, so a herd killed off stays dead for the run.
// Old territory details (tracks, droppings, bones as pools) are not ported: the herds leave real prints now.
public static partial class Fauna
{
    /// Old safeZone: herds start more than 2.4 km from camp.
    public const int SafeZoneTiles = 24;

    public static void Populate(World world)
    {
        var m = Map.Of(world);
        var rng = world.Rng("populate");
        // Interior tiles: off the map's edge, above ground, off the pass (old: off only its far side).
        var interior = new List<TileId>();
        for (int x = 2; x < m.Width - 2; x++)
            for (int y = 2; y < m.Height - 2; y++)
            {
                var t = new TileId(x, y);
                if (m.TerrainAt(t).IsPassable() && m.CaveAt(t) == Cave.None && m.StageAt(t) == null) interior.Add(t);
            }
        // Predators keep out of each other's land; prey graze anywhere outside the safe zone.
        var available = interior.Where(p => MapGen.Manhattan(p, m.Camp) > SafeZoneTiles).ToList();
        // old game: prey drew their land from what the wolf packs had left, so no prey herd ever lived on a
        // pack's land and a pack met prey only when one strayed through (judgment: prey land may overlap a pack's).
        var preyLand = available.ToList();
        var wolfLand = new HashSet<TileId>();

        void Herd(Species species, TileId start, List<TileId> territory, int members) =>
            Agents.Spawn(world, species, start, members, territory);

        // Wolves: 1-2 packs of 3-8 on 48-80 tiles each, apart from one another. Every Wolf Den has one,
        // first, its land grown from the map around the den, so a den may sit inside camp's safe zone.
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
        // Bears: 3-5 alone on 64-128 tiles; half are cave bears. Only the start is taken: bears overlap.
        // Every Bear Cave holds one of them.
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
        // Caribou: 2-3 herds of 5-15 on 128-192 tiles.
        for (int i = 0, herds = 2 + rng.NextInt(2); i < herds && preyLand.Count >= 8; i++)
        {
            var start = preyLand[rng.NextInt(preyLand.Count)];
            var land = Territory(m, start, preyLand, 128 + rng.NextInt(65), rng);
            if (land.Count < 6) continue;
            Herd(Species.Caribou, start, land, 5 + rng.NextInt(11));
        }
        // Large prey, 8-15 herds split between megaloceros (1-3 on 48-96 tiles) and bison (3-8 on 240-400).
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
        // Saber-tooths: 1-2 alone on 96-160 tiles, off the packs' land and holding their own.
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
        // Hyenas: 1-2 packs of 3-6 on 64-112 tiles, starting just outside a wolf pack's land.
        var hyenaZone = available.Where(p => !wolfLand.Contains(p) && MapGen.Neighbors(p).Any(wolfLand.Contains)).ToList();
        if (hyenaZone.Count < 3) hyenaZone = available.ToList();
        // The zone hugs the packs' land, so a start there can be boxed in by it and by ravines: such a
        // start is struck off and another drawn, so the valley still gets its packs.
        for (int i = 0, packs = 1 + rng.NextInt(2); i < packs && hyenaZone.Count > 0;)
        {
            var start = hyenaZone[rng.NextInt(hyenaZone.Count)];
            var land = Territory(m, start, available, 64 + rng.NextInt(49), rng);
            if (land.Count < 3) { hyenaZone.Remove(start); continue; }
            i++;
            Herd(Species.Hyena, start, land, 3 + rng.NextInt(4));
            hyenaZone.RemoveAll(land.ToHashSet().Contains);
        }
        // Mammoths: one herd of 8-12 on 192-288 tiles around the first Bone Hollow, if the valley has one.
        if (SitesNamed(m, "bone_hollow") is [var hollow, ..])
        {
            var land = Territory(m, hollow, Around(m, hollow, 40), 192 + rng.NextInt(97), rng);
            Herd(Species.Mammoth, hollow, land.Count < 8 ? [hollow] : land, 8 + rng.NextInt(5));
        }
        PopulateCaves(world, m, rng);
    }

    /// Tiles holding this site, x-major (old FindLocations scanned by name).
    static List<TileId> SitesNamed(GeneratedMap m, string id) =>
        [.. m.Sites.Where(s => s.Value.Id == id).Select(s => s.Key).OrderBy(t => t)];

    /// Old GetPositionsInRadius: open ground within a square around a den, off caves and the pass.
    /// old game: only the far side was left out, so a den near the mountains could range up the
    /// pass stages (bug, fixed).
    static List<TileId> Around(GeneratedMap m, TileId center, int radius)
    {
        var tiles = new List<TileId>();
        for (int dx = -radius; dx <= radius; dx++)
            for (int dy = -radius; dy <= radius; dy++)
            {
                var t = new TileId(center.X + dx, center.Y + dy);
                if (m.InBounds(t) && m.TerrainAt(t).IsPassable() && m.CaveAt(t) == Cave.None && m.StageAt(t) == null) tiles.Add(t);
            }
        return tiles;
    }

    /// Old PopulateCaves: one roll per cave system. Half stand empty; of the rest, most hold a cave
    /// bear and some a few hyenas, denned on a floor tile and ranging only where the cave connects.
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

    /// Old CreateContiguousTerritory: a random flood fill from the start through the allowed tiles,
    /// up to the target size, across only the edges a herd can walk. old game: the fill went by
    /// adjacency alone, so a territory could take in land beyond a ravine or cliff its herd could
    /// never reach (bug, fixed).
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
