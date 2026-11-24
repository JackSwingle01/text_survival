using System.Reflection;
using System.Text.RegularExpressions;

namespace Survival.Tests;

/// One mind serves every animal (spec "The animal mind", #86): animals decide through Fauna.Decide alone, which
/// sees to what is due through the shared planner, with no herd-mind state, no territory and no decide function
/// per behaviour. Species behaviour comes from role and content rows (NoSpeciesNamedInCode keeps names out).
public class AnimalMindTests
{
    static readonly Assembly Sim = typeof(Fauna).Assembly;

    static string Code(string file) =>
        CommentHygieneTests.CodeOnly(File.ReadAllText(Path.Combine(ArchitectureTests.SimSourceDir(), file)));

    /// The files of the animal mind: Fauna's partials.
    static readonly string[] MindFiles = ["Fauna.cs", "Needs.cs", "Kin.cs", "Scavenging.cs"];

    [Fact]
    public void AnimalsDecide_ThroughTheSharedPlannerOnly()
    {
        // No herd-mind state type, and nothing on the mind that names a state or a territory.
        Assert.Null(Sim.GetType("Survival.HerdState"));
        var fields = typeof(HerdMind).GetProperties().Select(p => p.Name).ToList();
        Assert.DoesNotContain(fields, f => f is "State" or "StateSinceMinute" || f.Contains("Territory"));

        // One decide function, the one the minds' seam calls.
        const BindingFlags all = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.DeclaredOnly;
        var decides = typeof(Fauna).GetMethods(all).Where(m => m.Name.StartsWith("Decide", StringComparison.Ordinal)).Select(m => m.Name).ToList();
        Assert.Equal(["Decide"], decides);
        Assert.Matches(@"HerdMind\s*=>\s*Fauna\.Decide\(", Code("Sim.cs"));

        // Fauna.Decide sees to what is due through Needs, and Needs plans through the shared planner.
        var fauna = Code("Fauna.cs");
        var decide = fauna[fauna.IndexOf("public static (Activity Activity, int Minutes) Decide(", StringComparison.Ordinal)..];
        decide = decide[..decide.IndexOf("\n    }", StringComparison.Ordinal)];
        Assert.Contains("Needs(world", decide);
        Assert.Matches(@"Planner\.Choose\(", Code("Needs.cs"));

        // No branching on a behaviour to pick a way of deciding: roles gate rules, they do not switch minds.
        foreach (var file in MindFiles)
            Assert.DoesNotMatch(new Regex(@"BehaviorOf\([^)]*\)\s*switch|HerdBehavior\.\w+\s*=>"), Code(file));
    }
}
