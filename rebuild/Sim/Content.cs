using System.Text.Json;
using System.Text.Json.Serialization;

namespace Survival;

/// Where generation puts a named place: dealt onto open land, onto a cave floor, or along the pass.
public enum Placement { Named, Cave, Pass }

/// A named place or a stage of the crossing. Hazard, wind, cover, visibility, temperature and
/// traversal replace the terrain's own on that tile; the features become its pools.
// Edges is carried, not applied: apply it in Map.EdgeBetween if climbing sites should cost
// 6 minutes a side. Escape and Vantage wait for combat escape terrain and scouting.
public sealed record LocationDef(
    string Id, string Name, Placement Placement,
    double Hazard, double Wind, double Cover, double Visibility,
    double Weight = 0, IReadOnlyList<Terrain>? Terrain = null, bool Elite = false,
    string? Tags = null, string? DiscoveryText = null, IReadOnlyList<string>? Stories = null, string? FirstVisitEvent = null,
    double TemperatureDeltaF = 0, double Traversal = 1, bool Dark = false, bool Escape = false, bool Vantage = false,
    EdgeType? Edges = null, bool IsCrossingExit = false, IReadOnlyList<SiteFeature>? Features = null,
    int AtLeast = 0)
{
    public IReadOnlyList<SiteFeature> All => Features ?? NoFeatures;
    static readonly IReadOnlyList<SiteFeature> NoFeatures = Array.AsReadOnly(Array.Empty<SiteFeature>());
    public SiteWater? Water => All.OfType<SiteWater>().FirstOrDefault();
}

/// Something at a named place. Chance is the odds generation puts it on a given tile;
/// RevealAtHours 0 is in plain sight, otherwise search hours.
[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind", UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FailSerialization)]
[JsonDerivedType(typeof(SiteForage), "forage")]
[JsonDerivedType(typeof(SiteTrees), "trees")]
[JsonDerivedType(typeof(SiteStock), "stock")]
[JsonDerivedType(typeof(SiteWater), "water")]
[JsonDerivedType(typeof(SiteSmallGame), "smallGame")]
[JsonDerivedType(typeof(SiteShelter), "shelter")]
[JsonDerivedType(typeof(SiteCache), "cache")]
[JsonDerivedType(typeof(SiteFind), "find")]
[JsonDerivedType(typeof(SiteEvent), "event")]
public abstract record SiteFeature
{
    public double Chance { get; init; } = 1;
    public double RevealAtHours { get; init; }
}

public sealed record SiteForage(double Density, IReadOnlyList<ForageItem> Items) : SiteFeature;
public sealed record SiteTrees(string Name, double MinutesToFell, Resource? Wood = null, int? Trees = null, double RespawnHoursPerTree = 168) : SiteFeature;
/// Harvest takes Items one cycle at a time; Salvage rolls a Loot table once, when generated.
public sealed record SiteStock(string Name, StockKind Work, int Minutes, IReadOnlyList<SiteStockItem>? Items = null,
    string? Loot = null, string? Description = null) : SiteFeature;
/// UnitsMax: units drawn in [Units, UnitsMax] when generated. RequiresTool: only someone carrying it works this item.
public sealed record SiteStockItem(Resource Resource, int Units, double KgPerUnit, double RespawnHoursPerUnit, int? UnitsMax = null, ToolType? RequiresTool = null);

/// Open water is a source to draw from; ice adds to the ground's hazard.
/// Frozen: ice in its season (Map.IsFrozen); water that is not never freezes. Ice and Fish wait for ice holes and fishing.
public sealed record SiteWater(string Name, bool Frozen, double Fish, double Ice = 0, string? Description = null) : SiteFeature
{
    /// While its ice lies: 0.15 for any ice, 0.2 more below 0.4 thick.
    public double HazardDelta => !Frozen ? 0 : 0.15 + (Ice < 0.4 ? 0.20 : 0);
}

// Small game, caches and discovery events are carried as data until their systems exist.
public enum SmallGame { Hare, Ptarmigan, Fox }
public sealed record SmallGameEntry(SmallGame Animal, double Weight);
public sealed record SiteSmallGame(double Density, IReadOnlyList<SmallGameEntry> Animals, IReadOnlyList<int>? PeakHours = null, double PeakFactor = 1) : SiteFeature;
public sealed record SiteCache(string Name, double CapacityKg, bool ProtectsFromPredators = false, bool ProtectsFromWeather = false, bool PreservesFood = false) : SiteFeature;
public sealed record SiteEvent(string Event) : SiteFeature;
public sealed record SiteFind(string Find) : SiteFeature;
/// What it gives now and the most it can be improved to; a cap left out is the kind's (shelters.json kinds). Placed by Shelters.
public sealed record SiteShelter(string Name, ShelterKind Type, double Insulation, double Overhead, double Wind,
    double? InsulationCap = null, double? OverheadCap = null, double? WindCap = null) : SiteFeature;

/// One step of a loot table (Content/loot.json says what each field means).
public sealed record LootRoll(double Chance = 1, Resource? Resource = null, IReadOnlyList<double>? Kg = null, IReadOnlyList<int>? Pieces = null,
    IReadOnlyList<string>? Gear = null, IReadOnlyList<double>? DurabilityPct = null, IReadOnlyList<int>? Pick = null, IReadOnlyList<LootRoll>? Of = null);

/// The game's tables, loaded once from the JSON embedded beside this assembly and checked on load:
/// a bad name or a broken reference throws here, not when a player happens to walk onto it.
/// Content never lands in a save: the save holds kinds and ids, and the tables say what they are.
/// Numbers to change in the tables before Content first loads them, so a tuner can try other constants
/// in its own process without editing a file. Kept apart from Content because
/// touching Content loads it: set these first, or not at all.
public static class ContentOverrides
{
    static IReadOnlyDictionary<string, double> pending = new Dictionary<string, double>();
    static bool loaded;

    /// Paths as Content.Override reads them ("species.json:species.Wolf.boldness"). Throws once
    /// Content has loaded: a late override would change nothing and say nothing.
    public static void Set(IReadOnlyDictionary<string, double> overrides)
    {
        if (loaded) throw new InvalidOperationException("Content is already loaded; set overrides before anything reads it");
        pending = new Dictionary<string, double>(overrides);
    }

    internal static IReadOnlyDictionary<string, double> Take()
    {
        loaded = true;
        return pending;
    }

    public static IReadOnlyDictionary<string, double> Applied => loaded ? pending : throw new InvalidOperationException("Content has not loaded yet");
}

public static partial class Content
{
    static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        AllowOutOfOrderMetadataProperties = true,
        AllowDuplicateProperties = false,
        RespectNullableAnnotations = true,
        RespectRequiredConstructorParameters = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        Converters = { new StrictEnumConverter(), new ReadOnlyListConverter(), new PieceRangeConverter() },
    };

    /// A piece range is written [min, max], as every range in the files is.
    sealed class PieceRangeConverter : JsonConverter<PieceRange>
    {
        public override PieceRange Read(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options) =>
            JsonSerializer.Deserialize<double[]>(ref reader, options) is [var min, var max] ? new(min, max) : throw new JsonException("pieceKg is [min, max]");

        public override void Write(Utf8JsonWriter writer, PieceRange value, JsonSerializerOptions options) =>
            JsonSerializer.Serialize(writer, new[] { value.Min, value.Max }, options);
    }

    /// Every list in a file is read into an array behind a read-only view: nothing outside can cast a
    /// table's list back to List and change it.
    sealed class ReadOnlyListConverter : JsonConverterFactory
    {
        public override bool CanConvert(Type type) => type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IReadOnlyList<>);

        public override JsonConverter CreateConverter(Type type, JsonSerializerOptions options) =>
            (JsonConverter)Activator.CreateInstance(typeof(Frozen<>).MakeGenericType(type.GetGenericArguments()[0]))!;

        sealed class Frozen<T> : JsonConverter<IReadOnlyList<T>>
        {
            public override IReadOnlyList<T> Read(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options) =>
                Array.AsReadOnly(JsonSerializer.Deserialize<T[]>(ref reader, options) ?? throw new JsonException("a list is null"));

            public override void Write(Utf8JsonWriter writer, IReadOnlyList<T> value, JsonSerializerOptions options) =>
                JsonSerializer.Serialize(writer, value.ToArray(), options);
        }
    }

    /// Every enum in a file is written as one of its names, exactly as the enum spells it. The stock
    /// string-enum reader would take "chest" for Chest, OR "Prey, Predator" together, and turn
    /// "SphagnumMoss, Usnea" into a value no kind has; this one refuses all of those, and numbers.
    sealed class StrictEnumConverter : JsonConverterFactory
    {
        public override bool CanConvert(Type type) => type.IsEnum;

        public override JsonConverter CreateConverter(Type type, JsonSerializerOptions options) =>
            (JsonConverter)Activator.CreateInstance(typeof(Strict<>).MakeGenericType(type))!;

        sealed class Strict<TEnum> : JsonConverter<TEnum> where TEnum : struct, Enum
        {
            static readonly Dictionary<string, TEnum> ByName = Enum.GetValues<TEnum>().ToDictionary(v => v.ToString(), StringComparer.Ordinal);

            static TEnum Named(string? name) =>
                name != null && ByName.TryGetValue(name, out var value) ? value
                : throw new JsonException($"no {typeof(TEnum).Name} '{name}' (one of {string.Join(", ", ByName.Keys)})");

            public override TEnum Read(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options) =>
                reader.TokenType == JsonTokenType.String ? Named(reader.GetString())
                : throw new JsonException($"a {typeof(TEnum).Name} is written as its name, not {reader.TokenType}");

            public override void Write(Utf8JsonWriter writer, TEnum value, JsonSerializerOptions options) =>
                writer.WriteStringValue(Enum.IsDefined(value) ? value.ToString() : throw new JsonException($"no {typeof(TEnum).Name} {value}"));

            public override TEnum ReadAsPropertyName(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options) => Named(reader.GetString());

            public override void WriteAsPropertyName(Utf8JsonWriter writer, TEnum value, JsonSerializerOptions options) =>
                writer.WritePropertyName(Enum.IsDefined(value) ? value.ToString() : throw new JsonException($"no {typeof(TEnum).Name} {value}"));
        }
    }

    sealed record LocationsFile(IReadOnlyList<LocationDef> Locations);

    internal static readonly string[] Files =
        ["locations.json", "loot.json", "items.json", "fire.json", "species.json", "effects.json", "shelters.json",
         "butchering.json", "terrain.json", "forage.json", "discoveries.json", "accidents.json", "recipes.json", "names.json", "foraging.json"];

    /// Everything, as loaded. The systems that own a table read it from here once, into their own statics.
    internal static ContentTables Tables { get; }
    internal static double LoadMilliseconds { get; }

    public static IReadOnlyList<LocationDef> Locations { get; }
    public static IReadOnlyDictionary<string, LocationDef> ById { get; }
    public static IReadOnlyDictionary<string, IReadOnlyList<LootRoll>> Loot { get; }
    /// South-west to far side, in file order.
    public static IReadOnlyList<LocationDef> PassStages { get; }
    public static IReadOnlyList<LocationDef> CaveSites { get; }

    static Content()
    {
        var clock = System.Diagnostics.Stopwatch.StartNew();
        Tables = Parse(Override(EmbeddedFiles(), ContentOverrides.Take()));
        LoadMilliseconds = clock.Elapsed.TotalMilliseconds;
        (Locations, Loot) = (Tables.Locations, Tables.Loot);
        ById = Locations.ToDictionary(l => l.Id).AsReadOnly();
        PassStages = Locations.Where(l => l.Placement == Placement.Pass).ToList().AsReadOnly();
        CaveSites = Locations.Where(l => l.Placement == Placement.Cave).ToList().AsReadOnly();
    }

    internal static string Embedded(string file)
    {
        using var stream = typeof(Content).Assembly.GetManifestResourceStream($"Survival.Content.{file}")
            ?? throw new FileNotFoundException($"Content file {file} is not embedded in {typeof(Content).Assembly.GetName().Name}");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    internal static Dictionary<string, string> EmbeddedFiles() => Files.ToDictionary(f => f, Embedded);

    /// Path is the file, then a dotted path of object keys (or array indexes) to an existing number.
    /// A path that names nothing, or anything but a number, throws: a mistyped name must not quietly tune nothing.
    internal static Dictionary<string, string> Override(IReadOnlyDictionary<string, string> files, IReadOnlyDictionary<string, double> overrides)
    {
        var result = files.ToDictionary(f => f.Key, f => f.Value);
        foreach (var group in overrides.GroupBy(o => FileOf(o.Key)))
        {
            if (!result.TryGetValue(group.Key, out var text)) throw new InvalidDataException($"Override: no content file {group.Key}");
            var root = System.Text.Json.Nodes.JsonNode.Parse(text, documentOptions: new JsonDocumentOptions
            { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true })
                ?? throw new InvalidDataException($"Override: {group.Key} is empty");
            foreach (var (path, value) in group) Set(root, path, value);
            result[group.Key] = root.ToJsonString();
        }
        return result;
    }

    static string FileOf(string path) =>
        path.IndexOf(':') is int i and > 0 ? path[..i] : throw new InvalidDataException($"Override '{path}' is not file:path.to.number");

    static void Set(System.Text.Json.Nodes.JsonNode root, string path, double value)
    {
        if (!double.IsFinite(value)) throw new InvalidDataException($"Override {path}: {value} is not a number");
        var keys = path[(path.IndexOf(':') + 1)..].Split('.');
        var node = root;
        for (int k = 0; k < keys.Length - 1; k++) node = Child(node, keys[k], path);
        string last = keys[^1];
        var old = Child(node, last, path) as System.Text.Json.Nodes.JsonValue;
        if (old == null || old.GetValueKind() != JsonValueKind.Number) throw new InvalidDataException($"Override {path}: not a number in the file");
        // A whole value is written as one, so an int field takes it; a fraction where the table wants
        // an int fails in Parse, by name.
        System.Text.Json.Nodes.JsonNode replacement = value == Math.Round(value) && Math.Abs(value) < 1e15
            ? System.Text.Json.Nodes.JsonValue.Create((long)value) : System.Text.Json.Nodes.JsonValue.Create(value);
        if (node is System.Text.Json.Nodes.JsonArray array) array[int.Parse(last)] = replacement;
        else node.AsObject()[last] = replacement;
    }

    static System.Text.Json.Nodes.JsonNode Child(System.Text.Json.Nodes.JsonNode node, string key, string path) => node switch
    {
        System.Text.Json.Nodes.JsonObject o when o.TryGetPropertyValue(key, out var c) && c != null => c,
        System.Text.Json.Nodes.JsonArray a when int.TryParse(key, out int i) && i >= 0 && i < a.Count && a[i] != null => a[i]!,
        _ => throw new InvalidDataException($"Override {path}: no '{key}' there"),
    };

    /// Any problem throws InvalidDataException naming it. Tests hand it broken tables.
    internal static (IReadOnlyList<LocationDef> Locations, IReadOnlyDictionary<string, IReadOnlyList<LootRoll>> Loot) Parse(string locationsJson, string lootJson)
    {
        var files = EmbeddedFiles();
        files["locations.json"] = locationsJson;
        files["loot.json"] = lootJson;
        var t = Parse(files);
        return (t.Locations, t.Loot);
    }

    static T Deserialize<T>(string json, string file)
    {
        try { return JsonSerializer.Deserialize<T>(json, Options) ?? throw new InvalidDataException($"{file} is empty"); }
        catch (JsonException e) { throw new InvalidDataException($"{file}: {e.Message}", e); }
    }

    /// Nullable annotations cover properties, not the elements of a list: a stray null in an array
    /// is caught here, by name.
    static IEnumerable<string> LocationNullProblems(IReadOnlyList<LocationDef> locations, IReadOnlyDictionary<string, IReadOnlyList<LootRoll>> loot)
    {
        for (int i = 0; i < locations.Count; i++)
        {
            var l = locations[i];
            if (l == null) { yield return $"locations[{i}] is null"; continue; }
            string at = $"location {l.Id}";
            if (l.Stories?.Any(x => x == null) == true) yield return $"{at}: a null story";
            var features = l.All;
            for (int j = 0; j < features.Count; j++)
            {
                var f = features[j];
                if (f == null) { yield return $"{at}: features[{j}] is null"; continue; }
                bool nullInside = f switch
                {
                    SiteForage fo => fo.Items.Any(x => x == null),
                    SiteStock st => st.Items?.Any(x => x == null) == true,
                    SiteSmallGame sg => sg.Animals.Any(x => x == null),
                    _ => false,
                };
                if (nullInside) yield return $"{at}: features[{j}] has a null entry";
            }
        }
        foreach (var (name, rolls) in loot)
            if (HasNullRoll(rolls)) yield return $"loot {name}: a null roll";
    }

    static bool HasNullRoll(IReadOnlyList<LootRoll>? rolls) =>
        rolls == null || rolls.Any(r => r == null || r.Gear?.Any(g => g == null) == true || r.Of != null && HasNullRoll(r.Of));

    static IEnumerable<string> LocationProblems(IReadOnlyList<LocationDef> locations, IReadOnlyDictionary<string, IReadOnlyList<LootRoll>> loot,
        IReadOnlyDictionary<string, GearDef> gear, IReadOnlyDictionary<ShelterKind, ShelterKindProps> shelterKinds,
        IReadOnlyDictionary<Terrain, TerrainRow> terrains)
    {
        var passStages = locations.Where(l => l.Placement == Placement.Pass).ToList();
        foreach (var dup in locations.GroupBy(l => l.Id).Where(g => g.Count() > 1)) yield return $"location id {dup.Key} is used {dup.Count()} times";
        if (passStages.Count != 6) yield return $"{passStages.Count} pass stages, not 6";
        if (passStages.Count(s => s.IsCrossingExit) != 1 || passStages.Count > 0 && !passStages[^1].IsCrossingExit)
            yield return "the last pass stage, and only it, is the crossing exit";
        if (locations.Any(l => l.IsCrossingExit && l.Placement != Placement.Pass)) yield return "only a pass stage is a crossing exit";
        if (!locations.Any(l => l.Placement == Placement.Cave)) yield return "no cave sites";
        foreach (var l in locations)
        {
            string at = $"location {l.Id}";
            if (Ground(l.Hazard, l.Wind, l.Cover, l.Visibility, maxWind: 3) is string ground) yield return $"{at}: {ground}";
            if (l.Traversal <= 0) yield return $"{at}: traversal {l.Traversal}";
            if (l.Placement == Placement.Named)
            {
                if (l.Weight <= 0) yield return $"{at}: weight {l.Weight}";
                if (l.AtLeast < 0) yield return $"{at}: atLeast {l.AtLeast}";
                if (l.Terrain is not { Count: > 0 } || l.Terrain.Any(t => !terrains[t].Passable)) yield return $"{at}: needs passable terrain";
            }
            else if (l.AtLeast != 0) yield return $"{at}: atLeast is for a named place";
            if (l.DiscoveryText?.Contains("{story}") == true && l.Stories is not { Count: > 0 }) yield return $"{at}: {{story}} with no stories";
            var features = l.All;
            int firstOther = features.ToList().FindIndex(f => f is not SiteForage);
            if (firstOther >= 0 && features.Skip(firstOther).Any(f => f is SiteForage)) yield return $"{at}: forage must come first (pool 0 is a tile's forage)";
            if (features.OfType<SiteShelter>().Count() > 1) yield return $"{at}: one shelter to a tile";
            foreach (var f in features)
            {
                if (f.Chance is <= 0 or > 1) yield return $"{at}: chance {f.Chance}";
                if (f.RevealAtHours < 0) yield return $"{at}: revealAtHours {f.RevealAtHours}";
                switch (f)
                {
                    case SiteStock { Work: StockKind.Harvest } s when s.Items is not { Count: > 0 } || s.Loot != null:
                        yield return $"{at}: harvest {s.Name} needs items and no loot"; break;
                    case SiteStock { Work: StockKind.Salvage } s when s.Loot == null || !loot.ContainsKey(s.Loot) || s.Items != null:
                        yield return $"{at}: salvage {s.Name} needs a known loot table ({s.Loot}) and no items"; break;
                    case SiteStock { Work: StockKind.Detail } s:
                        yield return $"{at}: {s.Name} is a detail; details are terrain's"; break;
                    case SiteStock s when s.Items?.Any(i => i.Units <= 0 || i.UnitsMax < i.Units || i.KgPerUnit <= 0) == true:
                        yield return $"{at}: {s.Name} has a bad item"; break;
                    case SiteShelter s when s.Chance < 1 || s.RevealAtHours > 0:
                        yield return $"{at}: shelter {s.Name} is placed when the world is made, so always and in sight"; break;
                    case SiteShelter s when ShelterLevelProblem(s, shelterKinds) is string bad:
                        yield return $"{at}: shelter {s.Name}: {bad}"; break;
                    case SiteForage s when s.Density <= 0 || s.Items.Count == 0 || s.Items.Any(i => i.MinKg <= 0 || i.MinKg > i.MaxKg || i.Abundance <= 0):
                        yield return $"{at}: forage needs a density and good items"; break;
                    case SiteStock s when s.Minutes <= 0 || s.Items?.Any(i => i.RespawnHoursPerUnit < 0) == true:
                        yield return $"{at}: {s.Name} needs minutes, and items that regrow in 0 hours (never) or more"; break;
                    case SiteTrees t when t.MinutesToFell <= 0 || t.Trees <= 0 || t.RespawnHoursPerTree <= 0:
                        yield return $"{at}: trees {t.Name} need minutesToFell, trees and respawnHoursPerTree above 0"; break;
                    case SiteWater w when w.Fish is < 0 or > 1 || w.Ice < 0 || (!w.Frozen && w.Ice > 0):
                        yield return $"{at}: water {w.Name}: fish in 0-1, and ice only on frozen water"; break;
                    case SiteSmallGame g when g.Density <= 0 || g.Animals.Count == 0 || g.Animals.Any(a => a.Weight <= 0) || g.PeakFactor < 1
                        || g.PeakHours is not (null or [>= 0 and < 24, >= 0 and < 24]):
                        yield return $"{at}: small game needs a density, animals of weight above 0, peak hours [from, to] in 0-23 and a peak factor of 1 or more"; break;
                    case SiteCache c when c.CapacityKg <= 0:
                        yield return $"{at}: cache {c.Name} capacity {c.CapacityKg}"; break;
                }
            }
        }
        foreach (var (name, rolls) in loot)
            foreach (var p in RollProblems(rolls, gear)) yield return $"loot {name}: {p}";
    }

    /// Levels and caps are fractions, and work improves a level up to its cap, never down to it.
    static string? ShelterLevelProblem(SiteShelter s, IReadOnlyDictionary<ShelterKind, ShelterKindProps> kinds)
    {
        var k = kinds[s.Type];
        foreach (var (stat, level, cap) in new[] { ("insulation", s.Insulation, s.InsulationCap ?? k.InsulationCap),
            ("overhead", s.Overhead, s.OverheadCap ?? k.OverheadCap), ("wind", s.Wind, s.WindCap ?? k.WindCap) })
        {
            if (level is < 0 or > 1 || cap is < 0 or > 1) return $"{stat} {level} (cap {cap}) is not in 0-1";
            if (level > cap) return $"{stat} {level} is above its cap {cap}";
        }
        return null;
    }

    static IEnumerable<string> RollProblems(IReadOnlyList<LootRoll> rolls, IReadOnlyDictionary<string, GearDef> gear)
    {
        foreach (var r in rolls)
        {
            int kinds = (r.Resource != null ? 1 : 0) + (r.Gear != null ? 1 : 0) + (r.Of != null ? 1 : 0);
            if (kinds != 1) yield return "a roll is exactly one of resource, gear or pick";
            if (r.Chance is <= 0 or > 1) yield return $"chance {r.Chance}";
            if (r.Resource != null && (r.Kg is not [var lo, var hi] || lo <= 0 || hi < lo)) yield return $"{r.Resource}: kg is [min, max]";
            if (r.Pieces is not null and not [> 0, _] || r.Pieces is [var a, var b] && b < a) yield return "pieces is [min, max]";
            foreach (var g in r.Gear ?? []) if (!gear.ContainsKey(g)) yield return $"no gear kind '{g}'";
            if (r.Gear is { Count: 0 }) yield return "gear names none";
            if (r.DurabilityPct is not null and not [>= 0, <= 1] || r.DurabilityPct is [var d0, var d1] && d1 < d0) yield return "durabilityPct is [min, max] in 0-1";
            if (r.Of != null)
            {
                if (r.Pick is not [> 0, var most] || most < r.Pick[0]) yield return "pick is [min, max]";
                foreach (var p in RollProblems(r.Of, gear)) yield return p;
            }
        }
    }
}
