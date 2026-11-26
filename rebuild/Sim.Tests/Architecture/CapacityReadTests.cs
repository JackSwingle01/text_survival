using System.Reflection;
using ArchUnitNET.Domain;
using ArchUnitNET.Domain.Dependencies;
using CompiledCode = ArchUnitNET.Domain.Architecture;

namespace Survival.Tests;

/// Everything outside a body reads its faculties (Body.Speed, Strength, Dexterity, Perception, Vision), never its
/// capacities: "Where things go" in documentation/clean-room-architecture.md, and the decision record
/// "A body is read in tiers, and the world reads only its faculties". Read from the compiled sim, screen
/// and tests (ArchUnitNET), not their source. The reads that exist today are listed by member: a ratchet.
public class CapacityReadTests
{
    /// The projects that use the sim but that Sim.Tests builds without referencing (Sim.Tests.csproj).
    static readonly (string Project, string Assembly)[] NotReferenced =
        [("Cli", "Cli"), ("Tools/Tune", "Tune"), ("Tools/TuneViewer", "TuneViewer"), ("Tools/Tune.Tests", "Tune.Tests"), ("Web/Host.Tests", "Host.Tests")];

    /// Its own load context, so a tool's copy of an assembly never stands in for the one the tests run.
    static readonly System.Runtime.Loader.AssemblyLoadContext Scanned = new(nameof(CapacityReadTests), isCollectible: true);

    /// A project's own build, in this build's configuration: a missing one fails rather than leaving its reads unread.
    static string BuiltAssembly(string project, string assembly)
    {
        string config = typeof(CapacityReadTests).Assembly.GetCustomAttribute<AssemblyConfigurationAttribute>()!.Configuration;
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "Sim", "Sim.csproj"))) dir = dir.Parent;
        string path = Path.Combine(dir!.FullName, project, "bin", config, "net10.0", assembly + ".dll");
        return File.Exists(path) ? path : throw new FileNotFoundException($"no build of {project} at {path}");
    }

    internal static readonly Lazy<CompiledCode> Compiled = new(() => NotReferenced.Aggregate(new ArchUnitNET.Loader.ArchLoader()
            .LoadAssemblies(typeof(World).Assembly, typeof(Survival.Web.ViewDto).Assembly, typeof(CapacityReadTests).Assembly),
            (loader, p) => loader.LoadAssemblies(Scanned.LoadFromAssemblyPath(BuiltAssembly(p.Project, p.Assembly))))
        .Build());

    static readonly string CapacitiesType = typeof(Capacities).FullName!;
    static readonly string BodyType = typeof(Body).FullName!;
    static readonly string EffectsType = typeof(Effects).FullName!;
    static readonly HashSet<string> CapacityNames = [.. typeof(Capacities).GetProperties().Select(p => p.Name)];

    /// The body's own code: its tiers (BodyTierTests), which derive the capacities, and the screen's read-out of them.
    static bool IsTheBodys(IType t) => BodyTierTests.TheBodysTypes.Any(b => BodyTierTests.IsOrIsIn(t, b));

    /// The tiers below the capacities: reading them from outside is reading a capacity by another door.
    static readonly Type[] BelowTheCapacities = [typeof(TissueReads), typeof(BloodAndBreath)];

    /// What one dependency reads: a capacity by name, the whole record, a region's capacities, or the effects'
    /// per-capacity deltas. Null for anything else.
    const string TheWholeRecord = "Capacities";

    static string? ReadBy(ITypeDependency d)
    {
        if (BelowTheCapacities.FirstOrDefault(t => BodyTierTests.IsOrIsIn(d.Target, t)) is Type below) return below.Name;
        if (d.Target.FullName == CapacitiesType)
        {
            string? member = (d as IMemberMemberDependency)?.TargetMember.Name;
            string name = member is null ? "" : member.Split('(')[0].Replace("get_", "");
            return CapacityNames.Contains(name) ? name : TheWholeRecord;
        }
        if (d.Target.FullName == EffectsType && d.Origin.FullName != EffectsType && d is IMemberMemberDependency deltas
            && deltas.TargetMember.Name.StartsWith(nameof(Effects.CapacityDeltas) + "(")) return nameof(Effects.CapacityDeltas);
        if (d.Target.FullName == BodyType && d is IMemberMemberDependency call)
        {
            if (call.TargetMember is MethodMember { ReturnType.FullName: var returns } && returns == CapacitiesType) return TheWholeRecord;
            if (call.TargetMember.Name.StartsWith(nameof(Body.RegionCapacities) + "(")) return nameof(Body.RegionCapacities);
        }
        return null;
    }

    /// Each member of these types that reads a capacity, as "Type.Member: what it reads". Lambdas, local
    /// functions and iterators count as the member they are written in.
    internal static IEnumerable<string> CapacityReads(IEnumerable<IType> types)
    {
        foreach (var type in types.Where(t => !IsTheBodys(t)))
            foreach (var member in type.Members)
            {
                var reads = member.MemberDependencies.Select(ReadBy).OfType<string>().ToHashSet();
                if (reads.Count == 0) continue;
                // A member that reads a capacity by name is listed by those names, not also by the record they came in.
                if (reads.Overlaps(CapacityNames) || reads.Contains(nameof(Body.RegionCapacities))) reads.Remove(TheWholeRecord);
                string typeName = type.FullName.StartsWith("Survival.") ? type.FullName["Survival.".Length..] : type.FullName;
                yield return $"{typeName}.{member.Name.Split('(')[0]}: {string.Join(", ", reads.Order(StringComparer.Ordinal))}";
            }
    }

    /// Every read outside the body but this rule's and the tier rules' own classes, which name the types to look for.
    static readonly Lazy<List<string>> ReadsToday = new(() => [.. CapacityReads(Compiled.Value.Types
        .Where(t => t.FullName != typeof(CapacityReadTests).FullName && t.FullName != typeof(BodyTierTests).FullName
            && t.FullName != typeof(BreachingSample).FullName))]);

    const string Fix = "Read a faculty instead (Body.Speed, Body.Strength, Body.Dexterity, Body.Perception, Body.Vision)";

    static List<string> Problems(IEnumerable<string> reads, IEnumerable<string> listed) =>
        WhereThingsGoTests.SetRatchet(reads.Distinct().Select(r => (r, $"{r} reads a capacity")),
            listed.ToDictionary(k => k, _ => ""), nameof(CapacityReadsToday), Fix);

    [Fact]
    public void OnlyTheBodyReadsACapacity()
    {
        var problems = Problems(ReadsToday.Value, CapacityReadsToday);
        Assert.True(problems.Count == 0, string.Join("\n", problems));
    }

    /// Deliberately breaching: each method reads a capacity from outside the body.
    static class BreachingSample
    {
        public static double Listed(BodyState b) => Body.GetCapacities(b).Moving;
        public static double Unlisted(BodyState b) => Body.GetCapacities(b).Sight;
        public static double InALambda(IEnumerable<BodyState> bodies) => bodies.Min(b => Body.GetCapacities(b).Moving);
        public static double InALocalFunction(BodyState b)
        {
            return Ears();
            double Ears() => Body.GetCapacities(b).Hearing;
        }
        public static IEnumerable<double> InAnIterator(BodyState b)
        {
            yield return Body.GetCapacities(b).Consciousness;
        }
        public static Capacities TheWholeRecord(BodyState b) => Body.GetCapacities(b);
        public static double ARegion(BodyState b) => Body.RegionCapacities(b.Regions[0])[0];
        public static double EffectDeltas(BodyState b) => Effects.CapacityDeltas(b)[0];
    }

    static string Sample(string member) => $"Tests.CapacityReadTests+BreachingSample.{member}";

    static IEnumerable<string> SampleReads() =>
        CapacityReads(Compiled.Value.Types.Where(t => t.FullName == typeof(BreachingSample).FullName));

    [Fact]
    public void TheToolsAndTheOtherTests_AreRead()
    {
        var assemblies = Compiled.Value.Types.Select(t => t.Assembly.Name.Split(',')[0]).ToHashSet();
        Assert.All(NotReferenced, p => Assert.Contains(p.Assembly, assemblies));
    }

    [Fact]
    public void EveryKindOfRead_IsFound_UnderTheMemberItIsWrittenIn()
    {
        Assert.Equal(
            [Sample("ARegion: RegionCapacities"), Sample("EffectDeltas: CapacityDeltas"), Sample("InALambda: Moving"), Sample("InALocalFunction: Hearing"),
             Sample("InAnIterator: Consciousness"), Sample("Listed: Moving"), Sample("TheWholeRecord: Capacities"), Sample("Unlisted: Sight")],
            SampleReads().Order(StringComparer.Ordinal));
    }

    [Fact]
    public void AnUnlistedRead_Fails_EvenBesideAListedOne()
    {
        var problems = Problems(SampleReads().Where(r => r.Contains(".Listed:") || r.Contains(".Unlisted:")), [Sample("Listed: Moving")]);
        Assert.Equal([$"{Sample("Unlisted: Sight")} reads a capacity. {Fix}"], problems);
    }

    [Fact]
    public void AnEntryTakenOffWhileItsReadRemains_Fails_AndSoDoesOneWhoseReadIsGone()
    {
        var reads = ReadsToday.Value;
        string first = CapacityReadsToday[0];
        Assert.Single(Problems(reads, CapacityReadsToday.Skip(1)), p => p.StartsWith(first));
        Assert.Single(Problems(reads, [.. CapacityReadsToday, "Travel.Gone: Moving"]), p => p.StartsWith("Travel.Gone: Moving is fixed"));
    }

    /// Every member outside the body that reads a capacity today. Only shrinks: each entry goes when its
    /// member reads a faculty instead (the tickets under #55).
    static readonly string[] CapacityReadsToday =
    [
        // The tests.
        "Tests.BodyTests.DestroyedLegs_LeaveNoMoving: Moving",
        "Tests.BodyTests.Discomforts_Saturate_ButDamageBloodLossAndColdStillTakeCapacitiesToNothing: Moving",
        "Tests.BodyTests.Discomforts_StackWithDiminishingReturns_NeverBelowAboutAFifth: Consciousness, Hearing, Manipulation, Moving, Sight",
        "Tests.BodyTests.Effects_DisableButNeverKill: Consciousness",
        "Tests.BodyTests.Effects_ModifyCapacities: Moving",
        "Tests.BodyTests.HalfDamagedArm_RoughlyHalvesItsManipulation: RegionCapacities",
        "Tests.BodyTests.HealthyBody_AllCapacitiesNearPerfect: Consciousness, Hearing, Manipulation, Moving, Sight",
        "Tests.BodyTests.LostOrgans_TakeTheirCapacityWithThem: Consciousness, RegionCapacities, Sight",
        "Tests.BodyTests.LoweringAnyTissue_NeverRaisesAnyCapacity: Consciousness, Hearing, Manipulation, Moving, Sight",
        "Tests.BodyTests.Unconscious_CannotMoveOrManipulate: Consciousness, Manipulation, Moving",
        "Tests.DirtyTests.Dirty_TakesNothingOffAnyCapacity: CapacityDeltas",
        "Tests.SurvivalTests.EveryEffectKind_HasADefinition: CapacityDeltas",
        "Tests.SurvivalTests.Hunger_AnEmptyStomachWithFatToSpare_IsAPang: Consciousness, Manipulation, Moving",
        "Tests.SurvivalTests.Hunger_RisesWithoutACliff_AsFatAndMuscleNearTheirFloors: Moving",
        "Tests.SurvivalTests.LowStats_RaiseHungryThirstyTired: Moving",
        "Tests.SurvivalTests.WellFed_WarmsAndHealsFivePercentMore_OnlyWhileItHolds: Capacities",
    ];
}
