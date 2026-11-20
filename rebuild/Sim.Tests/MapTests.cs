using System.Text.Json;

namespace Survival.Tests;

/// The generated map. Spec: old GridTests and ValleyWorldTests, plus the old generator's exact
/// output for a few seeds (captured by running old GridWorldGenerator.Generate(weather, seed)).
public class MapTests
{
    /// FNV-1a over every tile's terrain, x-major - the same hash the golden harness took of the old map.
    static string TerrainHash(GeneratedMap m)
    {
        ulong h = 14695981039346656037;
        for (int x = 0; x < m.Width; x++)
            for (int y = 0; y < m.Height; y++) { h ^= (ulong)m.TerrainAt(new(x, y)); h *= 1099511628211; }
        return h.ToString("x");
    }

    static HashSet<TileId> Reachable(GeneratedMap m, TileId start, TileId? blocked = null)
    {
        var seen = new HashSet<TileId> { start };
        var queue = new Queue<TileId>([start]);
        while (queue.TryDequeue(out var p))
            foreach (var d in Enum.GetValues<Direction>())
            {
                var n = Map.Step(p, d);
                if (n != blocked && Map.Blocked(m, p, n) == null && seen.Add(n)) queue.Enqueue(n);
            }
        return seen;
    }

    // Old map, terrain hash, camp, exit row. Old terrain enum order equals ours, so the hashes compare.
    public static readonly TheoryData<int, string, int, int, int> OldMaps = new()
    {
        { 1, "f547b626a5f183d2", 3, 40, 41 },
        { 17, "c0f502f420c06373", 7, 42, 43 },
        { 2048, "6d9e0e3d5d0bb899", 6, 43, 43 },
        { 91237, "122c5e52e2733105", 7, 39, 45 },
    };

    [Theory, MemberData(nameof(OldMaps))]
    public void Generate_MatchesOldGeneratorExactly(int seed, string hash, int campX, int campY, int exitY)
    {
        var m = Map.Generate(seed);
        Assert.Equal((224, 82), (m.Width, m.Height));
        Assert.Equal(hash, TerrainHash(m));
        Assert.Equal(new TileId(campX, campY), m.Camp);
        Assert.Equal(new TileId(223, exitY), m.Exit);
    }

    // Old GridWorldGenerator.Generate, hashed in the same encoding (tmp harness p2fix/oldmaphash):
    // caves (structure, cave id + 1), the generated edge east and south of every tile (type + 1, 0 none),
    // and the river set (the old _riverAdjacentPositions less lakes, cave floors and the pass stages).
    public static readonly TheoryData<int, string, string, string> OldStructure = new()
    {
        { 1, "1f22870bc0d34cb7", "f86435bc99f5b763", "5d9aa28d57116876" },
        { 17, "706ec701d2f818f7", "732b886ecc984665", "b1f1b7bd164715d2" },
        { 2048, "9ebe6b834dcb340e", "cefc6f4c68f71bb0", "a24b4d3803325642" },
        { 91237, "4f786a6614ea03db", "6b05e07e2fdf75d8", "cbb364bdecfc50de" },
    };

    [Theory, MemberData(nameof(OldStructure))]
    public void Generate_CavesEdgesAndRiversMatchOldExactly(int seed, string caves, string edges, string river)
    {
        var m = Map.Generate(seed);
        ulong c = 14695981039346656037, e = c, r = c;
        static void Mix(ref ulong h, ulong v) { h ^= v; h *= 1099511628211; }
        for (int x = 0; x < m.Width; x++)
            for (int y = 0; y < m.Height; y++)
            {
                var t = new TileId(x, y);
                Mix(ref c, (ulong)m.CaveAt(t));
                Mix(ref c, (ulong)((m.CaveIdAt(t) ?? -1) + 1));
                Mix(ref r, m.BesideRiver(t) ? 1UL : 0UL);
                foreach (var n in new[] { new TileId(x + 1, y), new TileId(x, y + 1) })
                    if (m.InBounds(n)) Mix(ref e, m.EdgeBetween(t, n) is EdgeType type ? (ulong)type + 1 : 0UL);
            }
        Assert.Equal((caves, edges, river), (c.ToString("x"), e.ToString("x"), r.ToString("x")));
    }

    [Fact]
    public void Generate_TerrainCountsMatchOld_Seed17()
    {
        var m = Map.Generate(17);
        var counts = new Dictionary<Terrain, int>();
        for (int x = 0; x < m.Width; x++)
            for (int y = 0; y < m.Height; y++) counts[m.TerrainAt(new(x, y))] = counts.GetValueOrDefault(m.TerrainAt(new(x, y))) + 1;
        var old = new Dictionary<Terrain, int>
        {
            [Terrain.Forest] = 2085, [Terrain.Clearing] = 74, [Terrain.Plain] = 2197, [Terrain.Hills] = 33, [Terrain.Water] = 86,
            [Terrain.Marsh] = 261, [Terrain.Rock] = 361, [Terrain.Mountain] = 12912, [Terrain.DeepWater] = 359,
        };
        Assert.Equal(old.OrderBy(k => k.Key), counts.OrderBy(k => k.Key));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(17)]
    [InlineData(-5)]
    [InlineData(int.MinValue)]
    [InlineData(int.MaxValue)]
    public void LegacyRng_DrawsExactlyWhatTheBclRandomDraws(int seed)
    {
        var mine = new LegacyRng(seed);
        var bcl = new Random(seed);
        for (int i = 0; i < 2000; i++)
        {
            Assert.Equal(bcl.Next(), mine.Next());
            Assert.Equal(bcl.NextDouble(), mine.NextDouble());
            Assert.Equal(bcl.Next(37), mine.Next(37));
            Assert.Equal(bcl.Next(-3, 92), mine.Next(-3, 92));
        }
    }

    [Theory]
    [InlineData(17)]
    [InlineData(2048)]
    [InlineData(91237)]
    public void Generate_ReachableExitAndNoBypassAroundThePass(int seed)
    {
        var m = Map.Generate(seed);
        Assert.True(m.Camp.X < m.Width / 5);
        Assert.Equal(Terrain.Forest, m.TerrainAt(m.Camp));
        Assert.All(Enum.GetValues<Direction>(), d => Assert.Null(Map.Blocked(m, m.Camp, Map.Step(m.Camp, d))));

        var reached = Reachable(m, m.Camp);
        for (int x = 0; x < m.Width; x++)
            for (int y = 0; y < m.Height; y++)
                if (m.TerrainAt(new(x, y)).IsPassable()) Assert.Contains(new TileId(x, y), reached);

        var stages = m.PassStageIndex.OrderBy(s => s.Value).Select(s => s.Key).ToList();
        Assert.Equal(Map.PassStages.Length, stages.Count);
        Assert.Single(stages.Select(s => s.Y).Distinct()); // one corridor, not six scattered tiles
        Assert.Equal(stages.OrderBy(s => s.X), stages);     // walked in narrative order, eastward
        Assert.Equal(m.Exit, stages[^1]);
        Assert.Equal(m.Width - 1, m.Exit.X);
        foreach (var stage in stages.SkipLast(1))
            Assert.DoesNotContain(m.Exit, Reachable(m, m.Camp, stage));
        for (int x = stages[0].X; x < m.Width; x++)
            Assert.True(m.TerrainAt(new(x, m.Exit.Y)).IsPassable(), $"Pass blocked at ({x}, {m.Exit.Y})");
    }

    [Fact]
    public void Generate_OnlyTheFarSideIsTheExit()
    {
        var m = Map.Generate(17);
        Assert.Single(Map.PassStages, s => s.IsCrossingExit);
        Assert.Equal("Far Side", m.StageAt(m.Exit)!.Name);
    }

    [Fact]
    public void Generate_MountainsWallTheEdges()
    {
        var m = Map.Generate(17);
        int mountains = Enumerable.Range(0, m.Width).Count(x => m.TerrainAt(new(x, 0)) == Terrain.Mountain);
        Assert.True(mountains > m.Width * 0.9, $"only {mountains} mountain tiles along the top edge");
    }

    [Fact]
    public void Generate_IsPureInTheSeed()
    {
        Assert.Equal(TerrainHash(Map.Generate(5)), TerrainHash(Map.Generate(5)));
        Assert.NotEqual(TerrainHash(Map.Generate(5)), TerrainHash(Map.Generate(6)));
    }

    // ---- passability on a hand-built map (old ValleyWorldTests.CaveWallsBlockMovement, GridTests travel options) ----

    /// 4 x 3 plain, with a cave mouth at (1,1), floor at (2,1), a ravine between (0,0) and (1,0),
    /// and deep water at (3,2).
    static GeneratedMap Tiny()
    {
        var terrain = new Terrain[4, 3];
        var caves = new Cave[4, 3];
        var ids = new int?[4, 3];
        terrain[1, 1] = terrain[2, 1] = Terrain.Rock;
        caves[1, 1] = Cave.Entrance; caves[2, 1] = Cave.Floor;
        ids[1, 1] = ids[2, 1] = 7;
        terrain[3, 2] = Terrain.DeepWater;
        terrain[3, 0] = Terrain.Forest;
        var edges = new Dictionary<(TileId, TileId), EdgeType> { [MapGen.EdgeKey(new(1, 0), new(0, 0))] = EdgeType.Ravine };
        return new GeneratedMap(4, 3, terrain, caves, ids, edges, [], new(0, 1), new() { [new(3, 0)] = 5 });
    }

    [Fact]
    public void Blocked_CaveFloorConnectsOnlyToItsOwnCaveAndMouth()
    {
        var m = Tiny();
        Assert.NotNull(Map.Blocked(m, new(2, 0), new(2, 1)));
        Assert.NotNull(Map.Blocked(m, new(2, 1), new(2, 2)));
        Assert.Null(Map.Blocked(m, new(1, 1), new(2, 1)));
        Assert.Null(Map.Blocked(m, new(0, 1), new(1, 1))); // the mouth opens onto the surface
    }

    [Fact]
    public void Blocked_ByEdgeOfWorldImpassableTerrainAndBarriersBothWays()
    {
        var m = Tiny();
        Assert.Equal("the edge of the world", Map.Blocked(m, new(0, 0), new(-1, 0)));
        Assert.Equal("deep water blocks the way", Map.Blocked(m, new(2, 2), new(3, 2)));
        Assert.Equal("a deep ravine blocks the way", Map.Blocked(m, new(0, 0), new(1, 0)));
        Assert.Equal("a deep ravine blocks the way", Map.Blocked(m, new(1, 0), new(0, 0)));
        Assert.Null(Map.Blocked(m, new(0, 1), new(0, 2)));
        Assert.Throws<ArgumentException>(() => Map.Blocked(m, new(0, 0), new(1, 1))); // diagonal
    }

    // ---- per-tile ground, captured from old terrain Locations on seed 17 ----

    // tile, old BaseTraversalMinutes, old TraversalModifier, old effective hazard
    public static readonly TheoryData<int, int, int, double, double> OldTiles = new()
    {
        { 2, 33, 1, 0.9103466751567771, 0.1 },                 // Forest
        { 2, 34, 3, 0.9165632594919593, 0.3 },                 // Rock
        { 2, 46, 3, 1.0270427742167576, 0.4 },                 // Water
        { 2, 47, 3, 1.0332593585519398, 0.3 },                 // Marsh
        { 2, 51, 1, 1.0760659472439746, 0 },                   // Plain
        { 5, 49, 1, 0.8828542385636151, 0.05 },                // Clearing
        { 14, 41, 3, 1.108148923473502, 0.25 },                // Hills
        { 26, 2, 3, 0.9634111293421179, 0.6499999999999999 },  // Marsh beside a river
        { 26, 24, 4, 1.172825519914192, 0.6499999999999999 },  // Rock beside a river
        { 85, 11, 1, 1.1771365515781271, 0.35 },               // Plain beside a river
    };

    [Theory, MemberData(nameof(OldTiles))]
    public void TileGround_MatchesOldTerrainLocation(int x, int y, int baseMinutes, double modifier, double hazard)
    {
        var m = Map.Generate(17);
        var t = new TileId(x, y);
        Assert.Equal(modifier, Map.TraversalModifier(m, t), 12);
        Assert.Equal(baseMinutes, Map.BaseTraversalMinutes(m, t));
        Assert.Equal(hazard, Map.TravelHazardLevel(m, t), 12);
    }

    [Fact]
    public void PassStagesAndCamp_UseTheirOwnGround()
    {
        var m = Map.Generate(17);
        var proper = m.PassStageIndex.Single(s => s.Value == 2).Key;
        Assert.Equal(1.0, Map.TravelHazardLevel(m, proper));
        Assert.Equal(7, Map.BaseTraversalMinutes(m, proper)); // old: (Rock 2 + floor(1.0 * 5)) x 1.0
        Assert.Equal(-15, Map.TemperatureDeltaF(m, proper));
        Assert.Equal(0, Map.TravelHazardLevel(m, m.Camp));
        Assert.Equal(1, Map.BaseTraversalMinutes(m, m.Camp));
        Assert.Equal(0.4, Map.WindFactor(m, m.Camp));
    }

    // ---- the save holds deltas only ----

    [Fact]
    public void Save_HoldsNoTerrain_AndLoadRegeneratesTheSameMap()
    {
        var world = World.New(17);
        string json = Save.ToJson(world);
        Assert.True(json.Length < 1_000_000, $"Save is {json.Length} bytes");
        Assert.True(json.Length < 10_000, $"Save is {json.Length} bytes: generated terrain leaked into it?");
        Assert.DoesNotContain("Mountain", json);
        Assert.Contains("\"Tiles\":{}", json);

        var loaded = Save.FromJson(json);
        Assert.Null(loaded.GeneratedMap); // rebuilt lazily, not read from the file
        var m = Map.Of(loaded);
        Assert.Equal(TerrainHash(Map.Of(world)), TerrainHash(m));
        Assert.Equal(Map.Of(world).Camp, m.Camp);
    }

    [Fact]
    public void Save_HoldsNoCampTile_AndClothingBelongsToEachBody()
    {
        var world = World.New(17);
        string json = Save.ToJson(world);
        Assert.DoesNotContain("CampTile", json); // derived from the seed's map, as terrain is
        Assert.Equal(Map.Generate(17).Camp, Map.Of(Save.FromJson(json)).Camp);
        // Agent carries no human-only fields: a herd's members wear nothing, a person wears their own.
        Assert.Null(typeof(Agent).GetProperty("ClothingClo"));
        Assert.Null(typeof(Agent).GetProperty("ClothingWeightKg"));
        // Clothing is what each body wears, and its clo is derived from that, never stored.
        var herd = new Agent { Id = new AgentId(9), Tile = world.Agent(AgentId.Player).Tile, Members = [BodyState.BaselineHuman(), BodyState.BaselineHuman()] };
        herd.Members[1].Worn.RemoveAll(g => Items.Def(g).Slot != EquipSlot.Chest);
        Assert.Equal([1.33, 0.8], herd.Members.Select(b => Math.Round(b.ClothingClo, 2)));
        world.Agents[herd.Id] = herd;
        Assert.Equal(0.8, Save.FromJson(Save.ToJson(world)).Agent(herd.Id).Members[1].ClothingClo, 9);
        Assert.DoesNotContain("ClothingClo", Save.ToJson(world));
    }

    [Fact]
    public void TileIdKeys_RoundTrip()
    {
        var world = World.New(1);
        world.Tiles[new TileId(3, -4)] = new TileState();
        string json = Save.ToJson(world);
        Assert.Contains("\"3,-4\":{}", json);
        Assert.Equal(json, Save.ToJson(Save.FromJson(json)));
    }

    // ---- the agent model (plan decision 2) ----

    [Fact]
    public void Agent_IsMembersAndAMind_BodyDerivedForOneBodyAgents()
    {
        var world = World.New(1);
        var player = world.Agent(AgentId.Player);
        Assert.Same(player.Members.Single(), player.Body);
        Assert.IsType<PlayerMind>(player.Mind);
        Assert.Equal(Map.Of(world).Camp, player.Tile);

        string json = Save.ToJson(world);
        Assert.Contains("\"Mind\":{\"kind\":\"player\",\"Pack\":", json);
        Assert.DoesNotContain("\"Body\":", json); // derived, never saved twice
        Assert.IsType<PlayerMind>(Save.FromJson(json).Agent(AgentId.Player).Mind);

        var herd = new Agent { Id = new AgentId(5), Tile = Map.Of(world).Camp, Members = [BodyState.BaselineHuman(), BodyState.BaselineHuman()] };
        Assert.Throws<InvalidOperationException>(() => herd.Body);
    }
}
