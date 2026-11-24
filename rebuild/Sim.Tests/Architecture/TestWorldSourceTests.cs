using System.Text.RegularExpressions;

namespace Survival.Tests;

/// Tests get their worlds, loads and maps from TestWorlds, which builds each seed once. A direct call
/// to World.New, Save.FromJson or Map.Generate is counted per test file and must be listed: in
/// MayCallDirectly when generation or loading is the subject or the seeds don't repeat, or in
/// NotYetOnTestWorlds, a ratchet (dev/ratchets.md, "test-worlds") that only shrinks.
public class TestWorldSourceTests
{
    static readonly Regex DirectCall = new(@"\b(World\.New|Save\.FromJson|Map\.Generate)\(", RegexOptions.Compiled);

    /// Files that call the real thing on purpose, with how many calls. Not a ratchet.
    static readonly Dictionary<string, (int Count, string Why)> MayCallDirectly = new()
    {
        ["TestWorlds.cs"] = (2, "the source itself: it builds and loads"),
        ["TestWorldsTests.cs"] = (1, "checks the source against World.New"),
        ["SaveTests.cs"] = (8, "the save format and loading are the subject"),
        ["ContentTests.cs"] = (1, "a search over seeds"),
        ["PoolsTests.cs"] = (5, "searches over seeds 1-40"),
        ["HerdTests.cs"] = (4, "searches over 24 and 200 seeds, and the valley's population is the subject"),
        ["WeatherTests.cs"] = (1, "a search over 20 seeds"),
        ["CombatTests.cs"] = (1, "a search over 20 seeds"),
        ["CombatFlightTests.cs"] = (2, "searches over 60 and 300 seeds"),
        ["CombatReachTests.cs"] = (3, "searches over 30 and 60 seeds"),
        ["IndividualsTests.cs"] = (5, "a populated World.New against a save midway is the subject"),
        ["NpcTests.cs"] = (1, "a load midway against a straight run is the subject"),
        ["CoreTests.cs"] = (1, "a world reloaded every chunk against a straight run is the subject"),
        ["TravelTests.cs"] = (1, "a load mid-crossing against a straight run is the subject"),
        ["NpcBenchReport.cs"] = (1, "a benchmark of the real world, run only with NPC_BENCH=1"),
        ["PlanBenchReport.cs"] = (1, "a benchmark of the real world over its own seeds"),
        ["SpeciesBodyTests.cs"] = (2, "two World.New of one seed draw the same fat: determinism is the subject"),
        ["MemoryTests.cs"] = (1, "a seed's World.New looks the same: determinism is the subject"),
    };

    /// Files still to move onto TestWorlds. Only shrinks.
    static readonly Dictionary<string, (int Count, string Why)> NotYetOnTestWorlds = new()
    {
        ["MapTests.cs"] = (27, "generation is the subject of some of its tests, not of those reading the seed's map"),
    };

    static string TestsDir => Path.Combine(Path.GetDirectoryName(ArchitectureTests.SimSourceDir())!, "Sim.Tests");

    [Fact]
    public void TestsGetTheirWorlds_FromTestWorlds()
    {
        char sep = Path.DirectorySeparatorChar;
        var hits = Directory.EnumerateFiles(TestsDir, "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{sep}obj{sep}") && !f.Contains($"{sep}bin{sep}"))
            .Order(StringComparer.Ordinal)
            .SelectMany(f => WhereThingsGoTests.Hits(Path.GetFileName(f), CommentHygieneTests.CodeOnly(File.ReadAllText(f)), DirectCall));
        var known = MayCallDirectly.Concat(NotYetOnTestWorlds).ToDictionary(kv => kv.Key, kv => kv.Value);
        var problems = WhereThingsGoTests.CountRatchet(hits, known, "MayCallDirectly or NotYetOnTestWorlds",
            "direct World.New/Save.FromJson/Map.Generate calls",
            "Use TestWorlds.New, TestWorlds.Load or TestWorlds.Map; call the real one only where generation or loading is the subject, or the seeds don't repeat, and list the file in MayCallDirectly with why");
        Assert.True(problems.Count == 0, string.Join("\n", problems));
        Assert.Empty(MayCallDirectly.Keys.Intersect(NotYetOnTestWorlds.Keys));
    }
}
