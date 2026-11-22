using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace Survival.Tests;

/// The rules in "Where things go" (documentation/clean-room-architecture.md), checked by source scans and
/// reflection. Where today's code breaks a rule, the breaches are listed by name with a reason: a ratchet.
/// A new breach fails, and so does a listed one that has been fixed, so each list only shrinks.
public class WhereThingsGoTests
{
    const string Guide = "documentation/clean-room-architecture.md, \"Where things go\"";

    static string RebuildDir => Path.GetDirectoryName(ArchitectureTests.SimSourceDir())!;

    static string CodeOf(string path) => CommentHygieneTests.CodeOnly(File.ReadAllText(path));

    static List<(string File, string Code)> SimCode() =>
        [.. CommentHygieneTests.SimFiles().Select(f => (Path.GetFileName(f), CodeOf(f)))];

    static int LineOf(string src, int index) => src.AsSpan(0, index).Count('\n') + 1;

    static string LineAt(string src, int line) => src.Split('\n')[line - 1].Trim();

    /// The ratchet over a set of named breaches: a new one fails with the fix, a listed one no longer
    /// found fails until it leaves the list.
    static List<string> SetRatchet(IEnumerable<(string Key, string Detail)> found, IReadOnlyDictionary<string, string> known,
        string listName, string fix)
    {
        var problems = new List<string>();
        var keys = new HashSet<string>();
        foreach (var (key, detail) in found)
        {
            keys.Add(key);
            if (!known.ContainsKey(key)) problems.Add($"{detail}. {fix}");
        }
        foreach (var gone in known.Keys.Where(k => !keys.Contains(k)).Order(StringComparer.Ordinal))
            problems.Add($"{gone} is fixed: take it off {listName}");
        return problems;
    }

    /// The ratchet over counts per file: a file over its count fails with each hit, under it fails until its
    /// count is lowered, so the count is always today's.
    static List<string> CountRatchet(IEnumerable<(string File, int Line, string Text)> hits, IReadOnlyDictionary<string, (int Count, string Why)> known,
        string listName, string rule, string fix)
    {
        var problems = new List<string>();
        var byFile = hits.GroupBy(h => h.File).ToDictionary(g => g.Key, g => g.ToList());
        foreach (var (file, list) in byFile.OrderBy(x => x.Key, StringComparer.Ordinal))
        {
            int ceiling = known.TryGetValue(file, out var k) ? k.Count : 0;
            if (list.Count > ceiling)
                problems.Add($"{file} has {list.Count} {rule} (allowed {ceiling}). {fix}\n" +
                    string.Join("\n", list.Select(h => $"    {h.File}:{h.Line}: {h.Text}")));
            else if (list.Count < ceiling)
                problems.Add($"{file} is down to {list.Count} {rule}: lower its count in {listName} to {list.Count}");
        }
        foreach (var gone in known.Keys.Where(f => !byFile.ContainsKey(f)).Order(StringComparer.Ordinal))
            problems.Add($"{gone} has no {rule} left: take it off {listName}");
        return problems;
    }

    static IEnumerable<(string File, int Line, string Text)> Hits(string file, string code, Regex pattern) =>
        pattern.Matches(code).Select(m => (file, LineOf(code, m.Index), LineAt(code, LineOf(code, m.Index))));

    static void AssertNone(List<string> problems) => Assert.True(problems.Count == 0, string.Join("\n", problems));

    // ---- EveryActivity_HasEveryFacet ----

    static IReadOnlyList<Type> ActivityTypes() =>
        [.. typeof(Activity).Assembly.GetTypes().Where(t => t.IsSubclassOf(typeof(Activity)) && !t.IsAbstract).OrderBy(t => t.Name, StringComparer.Ordinal)];

    /// A herd's activities: herd members do not live the person survival model (Sim.Surroundings), the page
    /// never offers them, and the action-sequence law drives a person. Not a breach, so not a ratchet.
    static readonly Dictionary<string, string> HerdActivities = new()
    {
        ["Graze"] = "a herd eating forage",
        ["Feed"] = "a herd eating a carcass",
        ["Raid"] = "a herd raiding a stash",
    };

    static readonly Dictionary<string, string> NoBodyProfileYet = new();

    static readonly Dictionary<string, string> NoUiKindYet = new();

    static readonly Dictionary<string, string> NotInActionGenYet = new()
    {
        ["GatherCharcoal"] = "no Act for taking a dead fire's charcoal",
        ["Hunt"] = "no Act for setting out on a stalk",
        ["PackTent"] = "no Act for striking a pitched tent",
        ["TakeOff"] = "no Act for taking off a garment",
        ["Wear"] = "no Act for putting on a garment",
    };

    /// The text from `start` up to the first `close` after it: one member's body.
    static string Body(string code, string start, string close, string where)
    {
        int at = code.IndexOf(start, StringComparison.Ordinal);
        Assert.True(at >= 0, $"{where}: '{start}' not found; the facet moved, so point this test at its new home");
        int end = code.IndexOf(close, at, StringComparison.Ordinal);
        Assert.True(end > at, $"{where}: the end of '{start}' not found");
        return code[at..end];
    }

    static bool NamesType(string text, string type) => Regex.IsMatch(text, $@"(?<![\w.])(Survival\.)?{type}\b(?!\s*\()");

    [Fact]
    public void EveryActivity_HasEveryFacet()
    {
        var types = ActivityTypes().Select(t => t.Name).ToList();
        Assert.True(types.Count >= 25);
        var problems = new List<string>();
        foreach (var name in HerdActivities.Keys.Where(n => !types.Contains(n)))
            problems.Add($"{name} is not an Activity any more: take it off HerdActivities");
        var person = types.Where(n => !HerdActivities.ContainsKey(n)).ToList();

        string holdOf = Body(CodeOf(Path.Combine(ArchitectureTests.SimSourceDir(), "Sim.cs")), "static Hold HoldOf(", "\n    }\n", "Sim.cs");
        problems.AddRange(SetRatchet(person.Where(n => !NamesType(holdOf, n)).Select(n => (n, $"{n} has no body profile in Sim.HoldOf")),
            NoBodyProfileYet, nameof(NoBodyProfileYet), "Give it an arm in Sim.HoldOf's profile switch (Sim/Sim.cs), and a ground contact if it is not walking"));

        // The kinds are string literals, so this one reads the source as written, not the code-only text.
        string kindOf = Body(File.ReadAllText(Path.Combine(RebuildDir, "Web", "Host", "ViewBuilder.cs")), "KindOf(Activity a)", "};", "Web/Host/ViewBuilder.cs");
        string? KindFor(string n) =>
            Regex.Match(kindOf, $@"(?<![\w.])(?:Survival\.)?{n}\b(?:\s+or\s+(?:Survival\.)?\w+)*\s*=>\s*""(\w+)""") is { Success: true } m ? m.Groups[1].Value : null;
        problems.AddRange(SetRatchet(person.Where(n => KindFor(n) is null or "other").Select(n => (n, $"{n} has no UI kind: ViewBuilder.KindOf gives it \"other\"")),
            NoUiKindYet, nameof(NoUiKindYet), "Give it an arm in ViewBuilder.KindOf (Web/Host/ViewBuilder.cs) and its icon on the page"));

        string actionGen = CodeOf(Path.Combine(RebuildDir, "Sim.Tests", "Verification", "ActionGen.cs"));
        problems.AddRange(SetRatchet(person.Where(n => !Regex.IsMatch(actionGen, $@"\bnew\s+{n}\s*\(")).Select(n => (n, $"{n} is never made by ActionGen.Resolve")),
            NotInActionGenYet, nameof(NotInActionGenYet),
            "Add an Act for it and resolve it in ActionGen (Sim.Tests/Verification/ActionGen.cs), and in Splittable if its minutes split, so the action-sequence law drives it"));
        AssertNone(problems);
    }

    // ---- EverySavedPolymorphicType_IsRegistered ----

    static readonly Type[] SimTypes = typeof(World).Assembly.GetTypes();

    /// The save's type graph from World: property types, collection parts and registered derived types.
    /// A type with its own converter is written by that converter, so its properties are not walked.
    static IEnumerable<(Type Owner, PropertyInfo Prop)> SavedProperties()
    {
        var seen = new HashSet<Type>();
        var queue = new Queue<Type>([typeof(World)]);
        while (queue.TryDequeue(out var type))
        {
            if (!seen.Add(type) || type.Namespace != typeof(World).Namespace || type.IsEnum) continue;
            if (type.GetCustomAttribute<JsonConverterAttribute>() != null) continue;
            foreach (var derived in type.GetCustomAttributes<JsonDerivedTypeAttribute>()) queue.Enqueue(derived.DerivedType);
            foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (prop.GetMethod?.IsPublic != true || prop.GetIndexParameters().Length > 0) continue;
                if (prop.GetCustomAttribute<JsonIgnoreAttribute>() is { Condition: JsonIgnoreCondition.Always }) continue;
                yield return (type, prop);
                foreach (var part in Closure(prop.PropertyType)) queue.Enqueue(part);
            }
        }
    }

    static IEnumerable<Type> Parts(Type t) =>
        t.IsArray ? [t.GetElementType()!] : t.IsGenericType ? t.GetGenericArguments() : [];

    static IEnumerable<Type> Closure(Type t) => Parts(t).SelectMany(Closure).Prepend(t);

    [Fact]
    public void EverySavedPolymorphicType_IsRegistered()
    {
        var problems = new List<string>();
        var roots = SimTypes.Where(t => t.GetCustomAttribute<JsonPolymorphicAttribute>() != null).ToList();
        Assert.Contains(typeof(Mind), roots);
        Assert.Contains(typeof(Activity), roots);
        Assert.Contains(typeof(WorldEvent), roots);
        foreach (var root in roots)
        {
            var registered = root.GetCustomAttributes<JsonDerivedTypeAttribute>().Select(a => a.DerivedType).ToHashSet();
            foreach (var sub in SimTypes.Where(t => t != root && !t.IsAbstract && root.IsAssignableFrom(t) && !registered.Contains(t)).OrderBy(t => t.Name))
                problems.Add($"{sub.Name} is a {root.Name} with no [JsonDerivedType(typeof({sub.Name}), \"...\")] on {root.Name}: " +
                    "saving or loading one throws. Register it beside the others");
        }
        // A saved property typed as a base with subtypes keeps only the base's fields unless the base is polymorphic.
        foreach (var t in SavedProperties().SelectMany(p => Closure(p.Prop.PropertyType)).Distinct().OrderBy(t => t.Name))
        {
            if (t.Namespace != typeof(World).Namespace || t.IsSealed || t.IsValueType || t.IsEnum) continue;
            if (t.GetCustomAttribute<JsonPolymorphicAttribute>() != null || t.GetCustomAttribute<JsonConverterAttribute>() != null) continue;
            var subs = SimTypes.Where(s => s != t && t.IsAssignableFrom(s)).Select(s => s.Name).Order().ToList();
            if (t.IsAbstract || subs.Count > 0)
                problems.Add($"{t.Name} is saved and has subtypes ({string.Join(", ", subs)}) but no [JsonPolymorphic]: " +
                    "mark it and register each subtype, or save the concrete type");
        }
        AssertNone(problems);
    }

    // ---- SavedEnums_AreWrittenByName ----

    /// Enums saved as numbers today: reordering one rewrites every save that holds it. The fix is one
    /// string-enum converter in Save's options, which takes them all off at once.
    const string Unconverted = "Save has no string-enum converter yet";

    static readonly Dictionary<string, string> EnumsSavedAsNumbers = new()
    {
        ["BeddingKind"] = Unconverted,
        ["BodyKind"] = Unconverted,
        ["ButcherMode"] = Unconverted,
        ["Capacity"] = Unconverted,
        ["ChangeKind"] = Unconverted,
        ["CombatAction"] = Unconverted,
        ["CombatResult"] = Unconverted,
        ["DamageType"] = Unconverted,
        ["Danger"] = Unconverted,
        ["Direction"] = Unconverted,
        ["EffectKind"] = Unconverted,
        ["FighterState"] = Unconverted,
        ["FirePhase"] = Unconverted,
        ["ForageFocus"] = Unconverted,
        ["Formation"] = Unconverted,
        ["FrontType"] = Unconverted,
        ["HerdState"] = Unconverted,
        ["Pace"] = Unconverted,
        ["PitType"] = Unconverted,
        ["Precipitation"] = Unconverted,
        ["Resource"] = Unconverted,
        ["Sex"] = Unconverted,
        ["ShelterKind"] = Unconverted,
        ["ShelterStat"] = Unconverted,
        ["Skill"] = Unconverted,
        ["Species"] = Unconverted,
        ["Stat"] = Unconverted,
        ["Structure"] = Unconverted,
        ["SurfaceMaterial"] = Unconverted,
        ["TissueKind"] = Unconverted,
        ["ToolType"] = Unconverted,
        ["TrackMaker"] = Unconverted,
        ["TrafficPattern"] = Unconverted,
        ["WeatherCondition"] = Unconverted,
        ["WindDirection"] = Unconverted,
    };

    static JsonSerializerOptions SaveOptions()
    {
        var fields = typeof(Save).GetFields(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
            .Where(f => f.FieldType == typeof(JsonSerializerOptions)).ToList();
        Assert.True(fields.Count == 1, $"Save holds {fields.Count} JsonSerializerOptions fields; this test reads the one the save is written with");
        return (JsonSerializerOptions)fields[0].GetValue(null)!;
    }

    /// The enum types a save writes as values: dictionary keys are written by name already.
    static IEnumerable<(Type Enum, string Where)> SavedEnumValues()
    {
        IEnumerable<Type> Values(Type t)
        {
            var u = Nullable.GetUnderlyingType(t) ?? t;
            if (u.IsEnum) return [u];
            if (u.IsArray) return Values(u.GetElementType()!);
            if (!u.IsGenericType) return [];
            var args = u.GetGenericArguments();
            bool dictionary = u.GetInterfaces().Append(u).Any(i => i.IsGenericType &&
                i.GetGenericTypeDefinition() is var d && (d == typeof(IDictionary<,>) || d == typeof(IReadOnlyDictionary<,>)));
            return (dictionary ? args.Skip(1) : args).SelectMany(Values);
        }
        foreach (var (owner, prop) in SavedProperties())
        {
            if (prop.GetCustomAttribute<JsonConverterAttribute>() != null) continue;
            foreach (var e in Values(prop.PropertyType)) yield return (e, $"{owner.Name}.{prop.Name}");
        }
    }

    [Fact]
    public void SavedEnums_AreWrittenByName()
    {
        var options = SaveOptions();
        var saved = SavedEnumValues().GroupBy(x => x.Enum).ToList();
        Assert.Contains(saved, g => g.Key == typeof(Resource)); // the walk reached a pack
        var numeric = saved
            .Where(g => JsonSerializer.Serialize(Enum.GetValues(g.Key).GetValue(0), g.Key, options).StartsWith('"') == false)
            .OrderBy(g => g.Key.Name, StringComparer.Ordinal)
            .Select(g => (g.Key.Name, $"{g.Key.Name} is saved as a number (in {string.Join(", ", g.Select(x => x.Where).Distinct().Order().Take(3))})"));
        AssertNone(SetRatchet(numeric, EnumsSavedAsNumbers, nameof(EnumsSavedAsNumbers),
            $"Write it by name: [JsonConverter(typeof(JsonStringEnumConverter<T>))] on the enum, or a string-enum converter in Save's options ({Guide}, Saves)"));
    }

    // ---- NoSpeciesNamedInCode ----

    /// Every number that varies per species is a species.json field, and behaviour comes from Role and
    /// HerdBehavior. Human is left out: a person's body is made as one, and people are told apart by their mind.
    static readonly Dictionary<string, (int Count, string Why)> SpeciesNamedInCode = new()
    {
        ["Combat.cs"] = (2, "SelectFormation picks a formation per species"),
        ["CombatAftermath.cs"] = (1, "mammoth ivory and hide are not in butchering.json"),
        ["Fauna.cs"] = (11, "stealth, the scavenger flee list and DetectionRangeTiles per species"),
        ["Populate.cs"] = (13, "herd counts and placements per species"),
        ["Renewal.cs"] = (14, "the population table and IsMegafauna per species"),
    };

    static readonly string[] SpeciesLoadingFiles = ["Species.cs", "Content.cs", "Content.Tables.cs"];

    [Fact]
    public void NoSpeciesNamedInCode()
    {
        var names = Enum.GetNames<Species>().Where(n => n != nameof(Species.Human));
        var pattern = new Regex($@"\bSpecies\.({string.Join("|", names)})\b");
        var hits = SimCode().Where(f => !SpeciesLoadingFiles.Contains(f.File)).SelectMany(f => Hits(f.File, f.Code, pattern));
        AssertNone(CountRatchet(hits, SpeciesNamedInCode, nameof(SpeciesNamedInCode), "species names in code",
            $"Make it a field of Sim/Content/species.json (or read Role or HerdBehavior), not a Species.<Name> in code ({Guide}, Species or animal)"));
    }

    // ---- NoWeatherConditionNumbersInCode ----

    /// Per-condition numbers belong in content data, read by condition.
    static readonly Dictionary<string, (int Count, string Why)> WeatherConditionNumbers = new()
    {
        ["Travel.cs"] = (2, "the crossing's weather hazard per condition"),
        ["Weather.cs"] = (14, "visibility, sunlight cut, snowfall and rainfall per condition, and a random front's ranges by condition"),
    };

    /// A condition (or several joined by `or`, with a guard) that yields a number: a switch arm, a ternary or
    /// a table entry.
    static readonly Regex ConditionNumber = new(
        @"WeatherCondition\.\w+(?:\s+or\s+WeatherCondition\.\w+)*(?:\s+when\b[^=;]*)?\s*=>\s*\(?\s*[-+]?(?<![\w.])\d" +
        @"|WeatherCondition\.\w+(?:\s+or\s+WeatherCondition\.\w+)*\s*\?[^:;]*?(?<![\w.])\d" +
        @"|\[\s*WeatherCondition\.\w+\s*\]\s*=\s*[-+]?\d");

    [Fact]
    public void NoWeatherConditionNumbersInCode()
    {
        var hits = SimCode().SelectMany(f => Hits(f.File, f.Code, ConditionNumber));
        AssertNone(CountRatchet(hits, WeatherConditionNumbers, nameof(WeatherConditionNumbers), "numbers chosen by weather condition",
            $"Put the per-condition number in content data and read it by condition ({Guide}, Weather type)"));
    }

    // ---- OneNightRule ----

    /// Night is the weather's to say: Weather publishes the light and the rest read it.
    static readonly Dictionary<string, (int Count, string Why)> ClockHourNights = new();

    static readonly Regex ClockHourNight = new(
        @"\bNightHour\b" +
        @"|(?<![\w.])(?:hour|Hour|hourOfDay|HourOfDay)\s*(?:<=?|>=?)\s*\d+\b" +
        @"|\b\d+\s*(?:<=?|>=?)\s*(?:hour|Hour|hourOfDay|HourOfDay)\b" +
        @"|MinuteOfDay\([^()]*\)\s*(?:/\s*60\s*)?(?:<=?|>=?)\s*\d");

    [Fact]
    public void OneNightRule()
    {
        var hits = SimCode().Where(f => f.File != "Weather.cs").SelectMany(f => Hits(f.File, f.Code, ClockHourNight));
        AssertNone(CountRatchet(hits, ClockHourNights, nameof(ClockHourNights), "clock-hour night windows",
            $"Read the light from Weather (Weather.IsDaytime, or a light read added there), not a clock hour ({Guide}, Rule inside a system)"));
    }

    // ---- OnlyFire_EstimatesBurnTime ----

    /// Members outside Fire.cs that work out hours of fire themselves, by file and member.
    static readonly Dictionary<string, string> BurnTimeOutsideFire = new()
    {
        ["Npc.cs:BelievedCampFuelHours"] = "a test's read of CampFuelHours",
        ["Npc.cs:CampFireHours"] = "the NPC ages the remembered fire's hours itself",
        ["Npc.cs:CampFuelHours"] = "the NPC's own sum of the camp's fuel hours",
        ["Npc.cs:FireHours"] = "the NPC's own sum of a pile's fuel hours",
        ["Npc.cs:ReckonedCampFuelHours"] = "a test's read of CampFuelHours",
        ["Report.cs:FireHoursPerDensity"] = "the distribution report turns forage into fire hours by burn rate",
        ["Report.cs:FuelFireHoursPerWorkHour"] = "the same, for a tile's stock",
        ["Report.cs:Logs"] = "the same, for a felled tree's logs (in TreeFireHours)",
        ["Report.cs:TreeFireHours"] = "the same, for a felled tree",
    };

    static readonly string[] FireFiles = ["Fire.cs", "Content.cs", "Content.Tables.cs"];

    static readonly Regex BurnRate = new(@"\bBurnRateKgPerHour\b");
    static readonly Regex BurnHoursName = new(@"^\w*(?:Fuel|Fire|Burn\w*)Hours\w*$");

    [Fact]
    public void OnlyFire_EstimatesBurnTime()
    {
        var found = new List<(string Key, string Detail)>();
        foreach (var (file, code) in SimCode().Where(f => !FireFiles.Contains(f.File)))
        {
            string member = "";
            var lines = code.Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                if (ArchitectureTests.MemberDeclaration.Match(lines[i]) is { Success: true } decl)
                {
                    member = decl.Groups[1].Value;
                    if (BurnHoursName.IsMatch(member)) found.Add(($"{file}:{member}", $"{file}:{i + 1} ({member}) estimates hours of fire: {lines[i].Trim()}"));
                }
                if (BurnRate.IsMatch(lines[i])) found.Add(($"{file}:{member}", $"{file}:{i + 1} ({member}) reads a fuel's burn rate: {lines[i].Trim()}"));
            }
        }
        AssertNone(SetRatchet(found.DistinctBy(f => f.Key), BurnTimeOutsideFire, nameof(BurnTimeOutsideFire),
            $"Ask Fire for the hours (Fire.FuelHours, Fire.HoursItWillBurn) instead of working them out ({Guide}, Forecast)"));
    }

    // ---- OwnedWorldState_IsWrittenOnlyByItsOwner ----

    static readonly Regex OwnedBy = new(@"Owned by (?:the )?(\w+)(?: system)?(?: and (\w+))?");

    /// World members whose doc comment names an owner, with the owners' names.
    static List<(string Member, string[] Owners)> OwnedWorldMembers()
    {
        var lines = File.ReadAllLines(Path.Combine(ArchitectureTests.SimSourceDir(), "World.cs"));
        var owned = new List<(string, string[])>();
        var doc = new List<string>();
        foreach (var line in lines)
        {
            string t = line.Trim();
            if (t.StartsWith("///")) { doc.Add(t); continue; }
            if (t.StartsWith("//") || t.Length == 0) continue;
            var decl = Regex.Match(t, @"^(?:public|internal)\s+(?!static\b)[\w<>\[\],.?\s]+?\s(\w+)\s*(?:\{|;|=)");
            if (decl.Success && OwnedBy.Match(string.Join(" ", doc)) is { Success: true } m)
                owned.Add((decl.Groups[1].Value, [.. new[] { m.Groups[1].Value, m.Groups[2].Value }.Where(s => s.Length > 0)]));
            doc.Clear();
        }
        return owned;
    }

    /// Writes to world.<member>: assignment (compound and ??= included), ++ or --, a collection's mutators,
    /// and writes into one of its elements.
    static Regex WriteTo(string member)
    {
        const string world = @"(?:(?<![\w.])(?:world|w)|\bWorld)\.";
        const string assign = @"\s*(?:[-+*/]|\?\?)?=(?![=>])";
        const string mutators = @"\.(?:Add|AddRange|Remove|RemoveAll|RemoveWhere|RemoveAt|Clear|TryAdd|Insert|Sort|Reverse)\s*\(";
        return new Regex(
            $@"{world}{member}\b(?:\s*\[[^\]]*\])?{assign}" +
            $@"|{world}{member}\b\s*(?:\+\+|--)|(?:\+\+|--)\s*{world}{member}\b" +
            $@"|{world}{member}\b{mutators}" +
            $@"|{world}{member}\b\s*\[[^\]]*\]\.\w+(?:\s*\[[^\]]*\])?(?:{assign}|{mutators})");
    }

    [Fact]
    public void OwnedWorldState_IsWrittenOnlyByItsOwner()
    {
        var owned = OwnedWorldMembers();
        Assert.True(owned.Count >= 10, "owned members found: " + string.Join(", ", owned.Select(o => o.Member)));
        var code = SimCode();
        var problems = new List<string>();
        foreach (var (member, owners) in owned)
        {
            var homes = code.Where(f => owners.Any(o => Regex.IsMatch(f.Code, $@"\bclass\s+{o}\b"))).Select(f => f.File).ToHashSet();
            if (homes.Count == 0)
            {
                problems.Add($"World.{member} is \"Owned by {string.Join(" and ", owners)}\" but no file declares that class: name the owning system as its class is named");
                continue;
            }
            var write = WriteTo(member);
            foreach (var (file, src) in code.Where(f => !homes.Contains(f.File)))
                foreach (var (_, line, text) in Hits(file, src, write))
                    problems.Add($"{file}:{line} writes World.{member}, owned by {string.Join(" and ", owners)} ({string.Join(", ", homes.Order())}): {text}. " +
                        "Ask the owner to make the change through one of its functions");
        }
        AssertNone(problems);
    }

    // ---- NpcUsesOnlyThePublicInterface ----

    /// Internal Sim members Npc.cs reaches today. The NPC is to use only verbs, refusals, forecasts and memory.
    static readonly Dictionary<string, string> NpcInternalUses = new()
    {
        ["Combat.ResolveHeadless"] = "the NPC fights its own hunt out instead of stepping Fight rounds",
        ["Fauna.BeginHunt"] = "the NPC opens a hunt itself instead of choosing the Hunt activity",
        ["Sim.Held"] = "the NPC asks whether a herd is held in a fight",
        ["Sim.Surroundings"] = "the NPC reads the survival context to forecast its own cooling",
    };

    static readonly Regex NpcForbiddenCalls = new(@"\bSim\.Start\b|\bFauna\.BeginHunt\b|\bCombat\.ResolveHeadless\b");

    /// Internal members of Sim types, by type and name, leaving out the NPC's own and compiler-made ones.
    static List<(Type Type, string Name, bool Static)> InternalMembers(ISet<string> npcTypes)
    {
        const BindingFlags all = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly;
        static bool Internal(MethodBase? m) => m != null && (m.IsAssembly || m.IsFamilyOrAssembly);
        var found = new List<(Type, string, bool)>();
        foreach (var t in SimTypes.Where(t => !npcTypes.Contains(OuterMost(t).Name) && t.GetCustomAttribute<System.Runtime.CompilerServices.CompilerGeneratedAttribute>() == null))
            foreach (var m in t.GetMembers(all))
            {
                if (m.Name.Contains('<') || m.GetCustomAttribute<System.Runtime.CompilerServices.CompilerGeneratedAttribute>() != null) continue;
                switch (m)
                {
                    case MethodInfo mi when !mi.IsSpecialName && Internal(mi): found.Add((t, mi.Name, mi.IsStatic)); break;
                    case PropertyInfo pi when Internal(pi.GetMethod) || Internal(pi.SetMethod) && pi.GetMethod?.IsPublic != true:
                        found.Add((t, pi.Name, (pi.GetMethod ?? pi.SetMethod)!.IsStatic)); break;
                    case FieldInfo fi when fi.IsAssembly || fi.IsFamilyOrAssembly: found.Add((t, fi.Name, fi.IsStatic)); break;
                }
            }
        return found;
    }

    static Type OuterMost(Type t) => t.DeclaringType is { } d ? OuterMost(d) : t;

    [Fact]
    public void NpcUsesOnlyThePublicInterface()
    {
        string npc = CodeOf(Path.Combine(ArchitectureTests.SimSourceDir(), "Npc.cs"));
        var npcTypes = Regex.Matches(npc, @"\b(?:class|record|struct|enum)\s+(\w+)").Select(m => m.Groups[1].Value).ToHashSet();
        Assert.Contains("Npc", npcTypes);
        const BindingFlags publicMembers = BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance;
        var publicNames = SimTypes.Where(t => t.IsPublic || t.IsNestedPublic).SelectMany(t => t.GetMembers(publicMembers)).Select(m => m.Name).ToHashSet();
        var npcOwn = SimTypes.Where(t => npcTypes.Contains(OuterMost(t).Name)).SelectMany(t => t.GetMembers(publicMembers | BindingFlags.NonPublic)).Select(m => m.Name).ToHashSet();
        var found = new List<(string Key, string Detail)>();
        foreach (var (type, name, isStatic) in InternalMembers(npcTypes).DistinctBy(x => (x.Type, x.Name)))
        {
            // A static member is named with its type. An instance member is matched by name only when no public
            // member and none of the NPC's own share it, so a same-named member elsewhere is not taken for it.
            if (!isStatic && (publicNames.Contains(name) || npcOwn.Contains(name))) continue;
            var pattern = isStatic ? $@"\b{OuterMost(type).Name}\.{name}\b" : $@"\.{name}\b";
            foreach (Match m in Regex.Matches(npc, pattern))
            {
                int line = LineOf(npc, m.Index);
                found.Add(($"{type.Name}.{name}", $"Npc.cs:{line} uses internal {type.Name}.{name}: {LineAt(npc, line)}"));
            }
        }
        foreach (Match m in NpcForbiddenCalls.Matches(npc))
        {
            int line = LineOf(npc, m.Index);
            found.Add((m.Value, $"Npc.cs:{line} calls {m.Value}, which runs the world for the NPC: {LineAt(npc, line)}"));
        }
        AssertNone(SetRatchet(found.DistinctBy(f => f.Key), NpcInternalUses, nameof(NpcInternalUses),
            $"Choose an activity and let Sim.Step run it; read the world through verbs, refusals, forecasts and memory ({Guide}, Agent behaviour)"));
    }

    // ---- BannedGlossaryWords_Ratchet ----

    /// Identifiers using words GLOSSARY.md avoids, counted across the Sim, each held at today's count.
    static readonly Dictionary<string, (int Count, string Why, string Say)> BannedWords = new()
    {
        ["Beliefs"] = (25, "the code name for memory until the rename", "memory"),
        ["Guarded"] = (13, "the condition the glossary calls occupied", "occupied"),
        ["Margin"] = (53, "the word the glossary replaces with resolve", "resolve"),
        ["Thing"] = (168, "location features are still things in the code (ThingRef, ThingKind, Things)", "location feature"),
    };

    static readonly Regex Identifier = new(@"(?<![\w@])[A-Za-z_]\w*");
    static readonly Regex WordPart = new(@"[A-Z]?[a-z]+|[A-Z]+(?![a-z])|\d+");

    static string? BannedWordIn(string identifier)
    {
        foreach (Match part in WordPart.Matches(identifier))
            foreach (var word in BannedWords.Keys)
            {
                string singular = word.EndsWith('s') ? word[..^1] : word;
                if (part.Value.Equals(singular, StringComparison.OrdinalIgnoreCase) || part.Value.Equals(singular + "s", StringComparison.OrdinalIgnoreCase))
                    return word;
            }
        return null;
    }

    [Fact]
    public void BannedGlossaryWords_Ratchet()
    {
        var uses = SimCode()
            .SelectMany(f => Identifier.Matches(f.Code).Select(m => (f.File, Word: BannedWordIn(m.Value))))
            .Where(x => x.Word != null)
            .ToLookup(x => x.Word!, x => x.File);
        var problems = new List<string>();
        foreach (var (word, (ceiling, _, say)) in BannedWords)
        {
            int count = uses[word].Count();
            var files = uses[word].GroupBy(f => f).OrderBy(g => g.Key, StringComparer.Ordinal).Select(g => $"{g.Key} {g.Count()}");
            if (count > ceiling)
                problems.Add($"{count} identifiers use '{word}' (allowed {ceiling}); GLOSSARY.md says {say}. Name it that way. By file: {string.Join(", ", files)}");
            else if (count < ceiling)
                problems.Add($"identifiers using '{word}' are down to {count}: lower its count in {nameof(BannedWords)} to {count}");
        }
        AssertNone(problems);
    }
}
