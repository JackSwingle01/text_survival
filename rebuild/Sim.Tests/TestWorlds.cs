using System.Collections.Concurrent;
using Survival;

/// Where tests get their worlds. Each seed's world is built once, saved, and kept with its generated map;
/// a test gets a load of that save with the map attached. Generating a map costs ~40 ms and populating the
/// animals more, and every load generates the map again (a save holds no terrain); a load with the map
/// attached costs ~6 ms. The game keeps no such cache: a map is read-only once generated, and sharing one
/// across worlds and threads depends on that staying true.
///
/// Ask for Bare unless the animals are the test's subject or it needs a populated valley around it.
///
/// Call World.New, Save.FromJson and Map.Generate directly only where generation or loading is the subject,
/// or where seeds don't repeat (TestWorldSourceTests).
static class TestWorlds
{
    sealed record Template(string Json, GeneratedMap Map);

    // One Lazy per seed: concurrent tests asking for the same seed wait for one build. A build that throws is
    // cached too, so every later test on that seed fails with the same error.
    static readonly ConcurrentDictionary<int, Lazy<Template>> Templates = new();
    static readonly ConcurrentDictionary<int, int> BuildCounts = new();

    static Template TemplateOf(int seed) => Templates.GetOrAdd(seed, s => new Lazy<Template>(() => Build(s))).Value;

    static Template Build(int seed)
    {
        BuildCounts.AddOrUpdate(seed, 1, (_, n) => n + 1);
        var w = World.New(seed);
        return new(Save.ToJson(w), Survival.Map.Of(w));
    }

    /// A world that saves the same as World.New(seed), on the seed's shared map.
    public static World New(int seed) => Load(TemplateOf(seed).Json);

    // The same once-per-seed rule for the bare world's save.
    static readonly ConcurrentDictionary<int, Lazy<string>> BareJson = new();

    /// The seed's world with nobody in it but the player: World.New(seed) with every other agent removed,
    /// and the camp's starting firewood taken away, so the camp is an empty tile to set a scene on.
    /// For a test whose subject is not the animals. They are nearly all of a world's save (about 500 KB
    /// of 520 on seed 17), so this loads in a fiftieth of the time, saves as fast, and no minute the test
    /// lives pays for a herd. Herds still arrive in an empty valley, at midnight, as they would in the game.
    public static World Bare(int seed) => Load(BareJson.GetOrAdd(seed, s => new Lazy<string>(() =>
    {
        var w = New(s);
        foreach (var id in w.Agents.Keys.Where(id => id != AgentId.Player).ToList()) Agents.Remove(w, id);
        EmptyCamp(w);
        return Save.ToJson(w);
    })).Value);

    /// Gives the player a fresh body of their kind (SpeciesTable.Average), not the autumn-fat one they are
    /// born with, so work takes exactly its whole-body time: for a test whose subject is a job's minutes.
    public static World FreshPlayer(World w)
    {
        var player = w.Agent(AgentId.Player);
        player.Members[0] = SpeciesTable.Average(Species.Human, player.Body.Sex);
        return w;
    }

    /// Takes the starting firewood off the camp's pile (World.New lays it), leaving the camp tile as bare as the valley's.
    public static void EmptyCamp(World w)
    {
        var camp = Survival.Map.Of(w).Camp;
        if (!w.Tiles.TryGetValue(camp, out var t)) return;
        t.Pile = null;
        t.PileCause = null;
        t.PileId = null;
        if (t.IsEmpty) w.Tiles.Remove(camp);
    }

    /// Save.FromJson with the shared map of the save's seed attached: the map Map.Of would generate again.
    /// map: a hand-built world's own, which the save does not hold; otherwise the seed's.
    public static World Load(string json, GeneratedMap? map = null)
    {
        var w = Save.FromJson(json);
        w.GeneratedMap = map ?? Map(w.Seed);
        return w;
    }

    /// The seed's generated map, shared: read it, never write it.
    public static GeneratedMap Map(int seed) => TemplateOf(seed).Map;

    /// How many times this seed's world has been built: once at most.
    public static int Builds(int seed) => BuildCounts.GetValueOrDefault(seed);
}
