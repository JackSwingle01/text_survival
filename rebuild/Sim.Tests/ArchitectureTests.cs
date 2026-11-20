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

    static string SimSourceDir()
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

    // ---- the save graph (plan: entities are referenced by id, never by object) ----

    /// Nothing in a saved world is reachable twice. An object shared by two owners (one Mind for
    /// two agents, one Gear in a pack and a fight) is one object in memory and two after a load,
    /// so the loaded world behaves differently from the saved one.
    [Fact]
    public void ABusyWorld_HasNoObjectReachableTwice()
    {
        var (world, _) = Phase3GateTests.Run(160);
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
        Assert.True(violations.Count == 0, string.Join("\n", violations));
    }
}
