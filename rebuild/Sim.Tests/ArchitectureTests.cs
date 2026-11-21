using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace Survival.Tests;

/// The core stays deterministic and headless: no UI, no async, no threads.
public class ArchitectureTests
{
    static readonly string[] ForbiddenPatterns =
    [
        @"\bRaylib", @"\bImGui", @"\brlImGui", @"\bSystem\.Threading\b", @"\basync\b", @"\bawait\b",
        @"\bTask\b", @"\bValueTask\b", @"\bThread\b", @"\bParallel\b", @"\bDateTime\b", @"\bEnvironment\.TickCount",
        // Determinism: every random draw goes through World.Rng, and nothing hashes by process-random seeds.
        @"\bSystem\.Random\b", @"\bnew Random\b", @"\bRandom\.Shared\b", @"\bGuid\.NewGuid\b", @"\.GetHashCode\(",
    ];

    internal static string SimSourceDir()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir != null; dir = dir.Parent)
        {
            string candidate = Path.Combine(dir.FullName, "Sim");
            if (File.Exists(Path.Combine(candidate, "Sim.csproj"))) return candidate;
        }
        throw new DirectoryNotFoundException("Could not find rebuild/Sim above " + AppContext.BaseDirectory);
    }

    [Fact]
    public void SimSource_HasNoUiAsyncOrThreading()
    {
        var violations = Directory.EnumerateFiles(SimSourceDir(), "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                     && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
            .SelectMany(f => File.ReadLines(f).Select((line, i) => (f, i, line)))
            .SelectMany(x => ForbiddenPatterns.Where(p => Regex.IsMatch(x.line, p))
                .Select(p => $"{Path.GetFileName(x.f)}:{x.i + 1} matches {p}: {x.line.Trim()}"))
            .ToList();
        Assert.True(violations.Count == 0, string.Join("\n", violations));
    }

    [Fact]
    public void SimAssembly_ReferencesNoUiAssembly()
    {
        var refs = typeof(World).Assembly.GetReferencedAssemblies().Select(a => a.Name ?? "").ToList();
        Assert.DoesNotContain(refs, n => n.Contains("Raylib") || n.Contains("ImGui"));
    }
    /// Work reads the fire (light, a fire to cook on); the fire must not read work back, or the two
    /// systems become one. The ability context both use lives with the light it depends on.
    [Fact]
    public void Fire_DoesNotDependOnWork()
    {
        var uses = File.ReadLines(Path.Combine(SimSourceDir(), "Fire.cs"))
            .Select((line, i) => (line, i)).Where(x => !x.line.TrimStart().StartsWith("//") && Regex.IsMatch(x.line, @"\bWork\."))
            .Select(x => $"Fire.cs:{x.i + 1}: {x.line.Trim()}").ToList();
        Assert.True(uses.Count == 0, string.Join("\n", uses));
    }

    /// Review 9: a herd's feeding wrote the carcass's meat directly. What is left on a carcass changes
    /// only through Carcasses, so butchering's ledgers and anything else it keeps cannot be skipped.
    [Fact]
    public void OnlyCarcasses_ChangesWhatIsLeftOnACarcass()
    {
        var writes = Directory.GetFiles(SimSourceDir(), "*.cs").Where(f => Path.GetFileName(f) != "Carcass.cs")
            .SelectMany(f => File.ReadLines(f).Select((line, i) => (f, line, i)))
            .Where(x => !x.line.TrimStart().StartsWith("//") && Regex.IsMatch(x.line, @"\.Remaining(\[[^\]]*\]\s*[-+*/]?=[^=]|\.(Add|Remove|Clear|TryAdd)\()"))
            .Select(x => $"{Path.GetFileName(x.f)}:{x.i + 1}: {x.line.Trim()}").ToList();
        Assert.True(writes.Count == 0, string.Join("\n", writes));
    }

    /// One exposure calculation (decision 2026-11-21, emergent-world-design §3.4): only Exposure.cs
    /// combines the sky with a tile's wind, cover or cold, a pit's walls or a shelter's, or computes the
    /// wind chill. Each rule names the files that may still say it and why; anything else that does
    /// is a second exposure model, the class of bug this replaced (tile wind reached only the chill).
    static readonly (string Pattern, string What, string[] Allowed)[] ExposureRules =
    [
        (@"\bMap\.WindFactor\(", "reads a tile's wind factor",
            ["Map.cs", // its definition
             "Ground.cs", // the key of a never-observed tile's ground class, not a wind
             "Travel.cs"]), // walking pace against the wind (rebuild-deferred: route through Exposure)
        (@"\bMap\.OverheadCover\(", "reads a tile's overhead cover", ["Map.cs", "Ground.cs"]),
        (@"\bMap\.TemperatureDeltaF\(", "reads a tile's own cold",
            ["Map.cs", "Ground.cs", "Remains.cs", "Carcass.cs"]), // keys handed on to Exposure.SiteAirF
        (@"\.TemperatureF\s*[-+]", "adds to the sky's air", ["Weather.cs"]),
        (@"\bWindChillF\(", "computes a wind chill", ["Weather.cs"]),
        (@"\.WindMph\b", "reads the wind in mph", ["Weather.cs"]),
        (@"\(1 - [^)]*(WindProtectionLevel|WallsLevel|\.WindLevel)\)", "cuts a wind by walls or shelter", []),
        (@"\b(FeltAirF|ShelterStep|SiteAirF|WindAt|OpenAirF)\(", "calls exposure's parts directly", ["Remains.cs"]),
    ];

    [Fact]
    public void OnlyExposure_CombinesSkyTileShelterAndFire()
    {
        var violations = Directory.GetFiles(SimSourceDir(), "*.cs")
            .Where(f => Path.GetFileName(f) != "Exposure.cs")
            .SelectMany(f => File.ReadLines(f).Select((line, i) => (File: Path.GetFileName(f), line, i)))
            .Where(x => !x.line.TrimStart().StartsWith("//"))
            .SelectMany(x => ExposureRules.Where(r => !r.Allowed.Contains(x.File) && Regex.IsMatch(x.line, r.Pattern))
                .Select(r => $"{x.File}:{x.i + 1} {r.What}: {x.line.Trim()}"))
            .ToList();
        Assert.True(violations.Count == 0, string.Join("\n", violations));
    }

    // ---- world events: every one names its tile, and every one can be filed ----

    static IReadOnlyList<Type> EventTypes() =>
        typeof(WorldEvent).Assembly.GetTypes().Where(t => t.IsSubclassOf(typeof(WorldEvent)) && !t.IsAbstract).OrderBy(t => t.Name).ToList();

    /// Every WorldEvent has a Tile, so it can be filed in a tile journal (short-loop plan, Asks of
    /// the Rebuild). The base record demands one; this checks each event is given it, from a
    /// TileId or a PoolId of its own, never left to default.
    [Fact]
    public void EveryWorldEvent_IsGivenItsTile()
    {
        Assert.True(EventTypes().Count >= 30);
        var missing = EventTypes()
            .Where(t => !t.GetConstructors().Any(c => c.GetParameters().Any(p => p.ParameterType == typeof(TileId) || p.ParameterType == typeof(PoolId))))
            .Select(t => t.Name).ToList();
        Assert.True(missing.Count == 0, "no tile given: " + string.Join(", ", missing));
    }

    /// Journals are saved: an event type with no JsonDerivedType line would throw the first time one is filed.
    [Fact]
    public void EveryWorldEvent_IsRegisteredForTheSave()
    {
        var registered = typeof(WorldEvent).GetCustomAttributes<JsonDerivedTypeAttribute>().Select(a => a.DerivedType).ToHashSet();
        var missing = EventTypes().Where(t => !registered.Contains(t)).Select(t => t.Name).ToList();
        Assert.True(missing.Count == 0, "not registered: " + string.Join(", ", missing));
    }

    /// In a busy run every event raised carries a real tile on the map: none left at its default.
    [Fact]
    public void ABusyRun_RaisesNoEventWithoutATile()
    {
        var events = Phase3GateTests.RunCollecting(200);
        var m = Map.Of(World.New(17));
        Assert.True(events.Select(e => e.GetType()).Distinct().Count() >= 12, string.Join(", ", events.Select(e => e.GetType().Name).Distinct()));
        var bad = events.Where(e => e.Tile == default || !m.InBounds(e.Tile)).Select(e => e.ToString()).Take(5).ToList();
        Assert.True(bad.Count == 0, string.Join("\n", bad));
    }

    // ---- the save graph (plan: entities are referenced by id, never by object) ----

    /// Nothing in a saved world is reachable twice. An object shared by two owners (one Mind for
    /// two agents, one Gear in a pack and a fight) is one object in memory and two after a load,
    /// so the loaded world behaves differently from the saved one.
    [Fact]
    public void ABusyWorld_HasNoObjectReachableTwice()
    {
        var (world, _) = Phase3GateTests.Run(160);
        // The NPC is in it, caught with work in hand: its mind and its work session are walked too.
        var npc = world.Agents.Values.Single(a => a.Mind is NpcMind);
        for (int i = 0; i < 24 * 60 && npc.Doing?.Work == null; i++) Sim.Advance(world, 1);
        Assert.NotNull(npc.Doing?.Work);
        string json = JsonSerializer.Serialize(world, new JsonSerializerOptions { ReferenceHandler = ReferenceHandler.Preserve });
        int at = json.IndexOf("\"$ref\"", StringComparison.Ordinal);
        Assert.True(at < 0, at < 0 ? "" : $"an object reachable twice: ...{json.Substring(Math.Max(0, at - 300), 400)}...");
    }

    /// Saved state names other entities by id: no saved property is typed World or Agent (or a
    /// collection of them), except World.Agents itself, where agents live.
    [Fact]
    public void SavedState_ReferencesNoEntityByObject()
    {
        static IEnumerable<Type> Parts(Type t) =>
            t.IsArray ? [t.GetElementType()!] : t.IsGenericType ? t.GetGenericArguments() : [];
        static bool Mentions(Type t, Type entity) => t == entity || Parts(t).Any(p => Mentions(p, entity));
        static IEnumerable<Type> Closure(Type t) => Parts(t).SelectMany(Closure).Prepend(t);

        var seen = new HashSet<Type>();
        var queue = new Queue<Type>([typeof(World)]);
        var violations = new List<string>();
        while (queue.TryDequeue(out var type))
        {
            if (!seen.Add(type) || type.Namespace != typeof(World).Namespace || type.IsEnum) continue;
            foreach (var derived in type.GetCustomAttributes<JsonDerivedTypeAttribute>()) queue.Enqueue(derived.DerivedType);
            foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (prop.GetMethod?.IsPublic != true) continue;
                if (prop.GetCustomAttribute<JsonIgnoreAttribute>() is { Condition: JsonIgnoreCondition.Always }) continue;
                bool home = type == typeof(World) && prop.Name == nameof(World.Agents);
                if (!home && (Mentions(prop.PropertyType, typeof(World)) || Mentions(prop.PropertyType, typeof(Agent))))
                    violations.Add($"{type.Name}.{prop.Name} is {prop.PropertyType.Name}: name it by id");
                foreach (var part in Closure(prop.PropertyType)) queue.Enqueue(part);
            }
        }
        Assert.Contains(typeof(Carcass), seen); // the walk reached the tiles' contents
        Assert.Contains(typeof(HerdMind), seen); // and through the polymorphic mind
        Assert.Contains(typeof(NpcMind), seen);
        Assert.Contains(typeof(Doing), seen); // and what an agent is doing, work in hand and all
        Assert.Contains(typeof(WorkSession), seen);
        Assert.True(violations.Count == 0, string.Join("\n", violations));
    }
}
