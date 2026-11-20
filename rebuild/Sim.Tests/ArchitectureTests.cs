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
}
