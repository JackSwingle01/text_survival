using ArchUnitNET.Domain;
using ArchUnitNET.Domain.Dependencies;

namespace Survival.Tests;

/// A body is read in tiers, each reading only the ones below it (Sim/BodyTiers.cs; the decision record "A body
/// is read in tiers, and the world reads only its faculties"). Read from the compiled code (ArchUnitNET):
/// capacities do not depend on faculties, faculties do not depend on vitality, and nothing in the tiers reads
/// Body, the interface over them, so no tier reaches a higher one through it. There are no cycles among them.
/// The screen alone reads a body's capacities, blood and breath, through BodyReadOut.
public class BodyTierTests
{
    /// The tiers, lowest first. Types of one rank are read beside each other, and neither reads the other.
    static readonly (Type Tier, int Rank)[] Tiers =
    [
        (typeof(TissueReads), 0),
        (typeof(BloodAndBreath), 1),
        (typeof(Capacities), 2),
        (typeof(Faculties), 3),
        (typeof(Alive), 3),
        (typeof(Vitality), 4),
        (typeof(Body), 5),
    ];

    /// The body's own code: its tiers, and the screen's read-out of them.
    internal static readonly Type[] TheBodysTypes = [.. Tiers.Select(t => t.Tier), typeof(BodyReadOut)];

    /// The type itself or one the compiler made inside it (a lambda's closure). A by-ref type is its element.
    internal static bool IsOrIsIn(IType t, Type of) =>
        t.FullName.TrimEnd('&') == of.FullName || t.FullName.StartsWith(of.FullName + "+", StringComparison.Ordinal);

    static string Short(Type t) => t.Name;

    /// Every dependency of a tier type on a tier of its rank or above, as "Tier.Member -> Tier".
    internal static IEnumerable<string> Breaches(IEnumerable<IType> types, IReadOnlyList<(Type Tier, int Rank)> tiers)
    {
        int? RankOf(Type? tier) => tier is null ? null : tiers.First(x => x.Tier == tier).Rank;
        Type? TierOf(IType t) => tiers.Select(x => x.Tier).FirstOrDefault(x => IsOrIsIn(t, x));
        foreach (var type in types)
        {
            if (TierOf(type) is not Type from) continue;
            foreach (var member in type.Members)
                foreach (var d in member.MemberDependencies.Cast<ITypeDependency>().Concat(member.Dependencies))
                    if (TierOf(d.Target) is Type to && to != from && RankOf(to) >= RankOf(from))
                        yield return $"{Short(from)}.{member.Name.Split('(')[0]} -> {Short(to)}";
        }
    }

    /// The tier-to-tier edges, every dependency counted, listed or not.
    static IEnumerable<(string From, string To)> Edges(IEnumerable<IType> types, IReadOnlyList<(Type Tier, int Rank)> tiers)
    {
        Type? TierOf(IType t) => tiers.Select(x => x.Tier).FirstOrDefault(x => IsOrIsIn(t, x));
        foreach (var type in types)
        {
            if (TierOf(type) is not Type from) continue;
            foreach (var d in type.Dependencies.Concat(type.Members.SelectMany(m => m.MemberDependencies.Cast<ITypeDependency>())))
                if (TierOf(d.Target) is Type to && to != from) yield return (Short(from), Short(to));
        }
    }

    /// Each cycle as "A -> B -> A", from a depth-first walk over the edges.
    internal static List<string> Cycles(IEnumerable<(string From, string To)> edges)
    {
        var next = edges.Distinct().GroupBy(e => e.From).ToDictionary(g => g.Key, g => g.Select(e => e.To).Order(StringComparer.Ordinal).ToList());
        var cycles = new List<string>();
        var done = new HashSet<string>();
        var path = new List<string>();
        void Walk(string node)
        {
            int at = path.IndexOf(node);
            if (at >= 0) { cycles.Add(string.Join(" -> ", path.Skip(at).Append(node))); return; }
            if (!done.Add(node)) return;
            path.Add(node);
            foreach (var to in next.GetValueOrDefault(node) ?? []) Walk(to);
            path.RemoveAt(path.Count - 1);
        }
        foreach (var node in next.Keys.Order(StringComparer.Ordinal)) Walk(node);
        return cycles;
    }

    static IEnumerable<IType> AllTypes => CapacityReadTests.Compiled.Value.Types;

    const string TierFix = "A tier reads only the tiers below it (Sim/BodyTiers.cs): move the read down, or the formula up";

    static List<string> TierProblems(IEnumerable<string> breaches, IEnumerable<string> listed) =>
        WhereThingsGoTests.SetRatchet(breaches.Distinct().Select(b => (b, $"{b} reads a tier of its rank or above")),
            listed.ToDictionary(k => k, _ => ""), nameof(TierBreachesToday), TierFix);

    [Fact]
    public void EachTier_ReadsOnlyTheTiersBelowIt_ButTheListedBreaches()
    {
        var problems = TierProblems(Breaches(AllTypes, Tiers), TierBreachesToday);
        Assert.True(problems.Count == 0, string.Join("\n", problems));
    }

    [Fact]
    public void TheTiers_HaveNoCycles()
    {
        var cycles = Cycles(Edges(AllTypes, Tiers));
        Assert.True(cycles.Count == 0, string.Join("\n", cycles));
    }

    /// What reads a tier of its rank or above today. Only shrinks: the faculties' vitality goes in #200.
    static readonly string[] TierBreachesToday =
    [
        "Faculties.DexterityCosts -> Vitality",
        "Faculties.Dexterity -> Vitality",
        "Faculties.Perception -> Vitality",
        "Faculties.Speed -> Vitality",
        "Faculties.Strength -> Vitality",
    ];

    /// Deliberately breaching tiers: the low one reads the high one through a lambda and an in parameter.
    static class SampleLow
    {
        public static int ThroughALambda(IEnumerable<int> xs) => xs.Sum(x => SampleHigh.Read(x));
        public static int ThroughAnInParameter(in SampleHighValue v) => v.X;
    }
    static class SampleHigh
    {
        public static int Read(int x) => SampleLowValue.Same(x);
    }
    readonly record struct SampleHighValue(int X);
    static class SampleLowValue
    {
        public static int Same(int x) => x;
        public static int Back() => SampleHigh.Read(0);
    }

    static readonly (Type, int)[] SampleTiers =
        [(typeof(SampleLow), 0), (typeof(SampleLowValue), 0), (typeof(SampleHigh), 1), (typeof(SampleHighValue), 1)];

    static IEnumerable<IType> SampleTypes => AllTypes.Where(t => SampleTiers.Any(s => IsOrIsIn(t, s.Item1)));

    [Fact]
    public void AReadOfAHigherTier_IsFound_ThroughALambdaAndAnInParameter()
    {
        var breaches = Breaches(SampleTypes, SampleTiers).Distinct().Order(StringComparer.Ordinal).ToList();
        Assert.Equal(["SampleLow.ThroughALambda -> SampleHigh", "SampleLow.ThroughAnInParameter -> SampleHighValue",
            "SampleLowValue.Back -> SampleHigh"], breaches);
        Assert.Single(TierProblems(breaches, breaches.Skip(1)), p => p.StartsWith(breaches[0], StringComparison.Ordinal));
        Assert.Single(TierProblems(breaches, [.. breaches, "Gone.Member -> SampleHigh"]), p => p.StartsWith("Gone.Member -> SampleHigh is fixed", StringComparison.Ordinal));
    }

    [Fact]
    public void ACycle_IsFound()
    {
        Assert.Contains("SampleHigh -> SampleLowValue -> SampleHigh", Cycles(Edges(SampleTypes, SampleTiers)));
    }

    // The read-out.

    /// The screen's members that show a body's capacities, blood and breath: the health panel and its chips.
    static readonly string[] ReadOutCallers = ["Web.ViewBuilder.Alerts", "Web.ViewBuilder.BodyOf"];

    internal static IEnumerable<string> ReadOutCalls(IEnumerable<IType> types)
    {
        foreach (var type in types.Where(t => !TheBodysTypes.Any(b => IsOrIsIn(t, b))))
            foreach (var member in type.Members)
                if (member.MemberDependencies.Any(d => IsOrIsIn(d.Target, typeof(BodyReadOut))))
                {
                    string typeName = type.FullName.StartsWith("Survival.", StringComparison.Ordinal) ? type.FullName["Survival.".Length..] : type.FullName;
                    yield return $"{typeName}.{member.Name.Split('(')[0]}";
                }
    }

    static List<string> ReadOutProblems(IEnumerable<string> calls) =>
        WhereThingsGoTests.SetRatchet(calls.Distinct().Select(c => (c, $"{c} calls BodyReadOut")),
            ReadOutCallers.ToDictionary(k => k, _ => ""), nameof(ReadOutCallers),
            "BodyReadOut is the screen's alone: read a faculty instead (Body.Speed, Body.Strength, Body.Dexterity, Body.Perception)");

    /// Every call but this rule's own class, which names the type to look for it.
    [Fact]
    public void OnlyTheScreen_CallsTheReadOut()
    {
        var problems = ReadOutProblems(ReadOutCalls(AllTypes.Where(t => t.FullName != typeof(BodyTierTests).FullName && !IsOrIsIn(t, typeof(ReadOutSample)))));
        Assert.True(problems.Count == 0, string.Join("\n", problems));
    }

    static class ReadOutSample
    {
        public static double Moving(BodyState b) => BodyReadOut.Of(b)[0].Pct;
    }

    [Fact]
    public void AnyOtherCallOfTheReadOut_Fails()
    {
        var calls = ReadOutCalls(AllTypes.Where(t => IsOrIsIn(t, typeof(ReadOutSample)))).ToList();
        Assert.Equal(["Tests.BodyTierTests+ReadOutSample.Moving"], calls);
        Assert.Contains("Tests.BodyTierTests+ReadOutSample.Moving calls BodyReadOut. BodyReadOut is the screen's alone: read a faculty instead (Body.Speed, Body.Strength, Body.Dexterity, Body.Perception)",
            ReadOutProblems(calls));
    }
}
