using System.Collections.Concurrent;
using Survival;

/// Where tests get their worlds. Each seed's world is built once, saved, and kept with its generated map;
/// a test gets a load of that save with the map attached. Generating a map costs ~40 ms and populating the
/// animals more, and every load generates the map again (a save holds no terrain); a load with the map
/// attached costs ~6 ms. The game keeps no such cache: a map is read-only once generated, and sharing one
/// across worlds and threads depends on that staying true.
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

    /// Save.FromJson with the shared map of the save's seed attached: the map Map.Of would generate again.
    public static World Load(string json)
    {
        var w = Save.FromJson(json);
        w.GeneratedMap = Map(w.Seed);
        return w;
    }

    /// The seed's generated map, shared: read it, never write it.
    public static GeneratedMap Map(int seed) => TemplateOf(seed).Map;

    /// How many times this seed's world has been built: once at most.
    public static int Builds(int seed) => BuildCounts.GetValueOrDefault(seed);
}
