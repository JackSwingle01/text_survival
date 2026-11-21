using System.Text.Json;
using System.Text.Json.Serialization;

namespace Survival;

/// Where generation puts a named place: dealt onto open land, onto a cave floor, or along the pass.
public enum Placement { Named, Cave, Pass }

/// A named place (old LocationFactory / CaveLocationFactory site, or a stage of the crossing): what
/// standing there is like and what is there. Hazard, wind, cover, visibility, temperature and
/// traversal replace the terrain's own on that tile; the features become its pools.
// Edges (old WithEdgesOnAllSides(Climb)) is carried, not applied: the old GameMap never read
// EdgeOverrides, so no climb was ever paid; apply it in Map.EdgeBetween if climbing sites should cost
// 6 minutes a side. Escape and Vantage wait for combat escape terrain and scouting.
public sealed record LocationDef(
    string Id, string Name, Placement Placement,
    double Hazard, double Wind, double Cover, double Visibility,
    double Weight = 0, IReadOnlyList<Terrain>? Terrain = null, bool Elite = false,
    string? Tags = null, string? DiscoveryText = null, IReadOnlyList<string>? Stories = null, string? FirstVisitEvent = null,
    double TemperatureDeltaF = 0, double Traversal = 1, bool Dark = false, bool Escape = false, bool Vantage = false,
    EdgeType? Edges = null, bool IsCrossingExit = false, IReadOnlyList<SiteFeature>? Features = null)
{
    public IReadOnlyList<SiteFeature> All => Features ?? [];
    /// The site's own water (old Location.GetFeature<WaterFeature>: the first one).
    public SiteWater? Water => All.OfType<SiteWater>().FirstOrDefault();
}

/// Something at a named place. Chance is the odds generation puts it on a given tile (old
/// Utils.DetermineSuccess at creation); RevealAtHours 0 is in plain sight, otherwise search hours.
[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind", UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FailSerialization)]
[JsonDerivedType(typeof(SiteForage), "forage")]
[JsonDerivedType(typeof(SiteTrees), "trees")]
[JsonDerivedType(typeof(SiteStock), "stock")]
[JsonDerivedType(typeof(SiteWater), "water")]
[JsonDerivedType(typeof(SiteSmallGame), "smallGame")]
[JsonDerivedType(typeof(SiteShelter), "shelter")]
[JsonDerivedType(typeof(SiteCache), "cache")]
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
    string? Loot = null, ToolType? RequiresTool = null, string? Description = null) : SiteFeature;
/// UnitsMax: units drawn in [Units, UnitsMax] when generated.
public sealed record SiteStockItem(Resource Resource, int Units, double KgPerUnit, double RespawnHoursPerUnit = 0, int? UnitsMax = null);

/// Old WaterFeature. Open water is a source to draw from; ice adds to the ground's hazard.
// Frozen, Ice and Fish wait for ice holes, fishing and ice that grows and thaws (Ground).
public sealed record SiteWater(string Name, bool Frozen, double Fish, double Ice = 0, string? Description = null) : SiteFeature
{
    /// Old WaterFeature.GetTerrainHazardContribution: 0.15 for any ice, 0.2 more below 0.4 thick.
    public double HazardDelta => !Frozen ? 0 : 0.15 + (Ice < 0.4 ? 0.20 : 0);
}

// Small game, caches and discovery events are carried as data until their systems exist (a small-game
// density to hunt in, remote storage - see Shelters' note on old CacheFeature - and moments).
public enum SmallGame { Rabbit, Ptarmigan, Fox }
public sealed record SmallGameEntry(SmallGame Animal, double Weight);
public sealed record SiteSmallGame(double Density, IReadOnlyList<SmallGameEntry> Animals, int[]? PeakHours = null, double PeakFactor = 1) : SiteFeature;
public sealed record SiteCache(string Name, double CapacityKg, bool ProtectsFromPredators = false, bool ProtectsFromWeather = false, bool PreservesFood = false) : SiteFeature;
public sealed record SiteEvent(string Event) : SiteFeature;
/// Old ShelterFeature: what it gives now and the most it can be improved to; a cap left out is the
/// kind's (Shelters.Kinds). Placed by Shelters.
public sealed record SiteShelter(string Name, ShelterKind Type, double Insulation, double Overhead, double Wind,
    double? InsulationCap = null, double? OverheadCap = null, double? WindCap = null) : SiteFeature;

/// One step of a loot table (Content/loot.json says what each field means).
public sealed record LootRoll(double Chance = 1, Resource? Resource = null, double[]? Kg = null, int[]? Pieces = null,
    IReadOnlyList<string>? Gear = null, double[]? DurabilityPct = null, int[]? Pick = null, IReadOnlyList<LootRoll>? Of = null);

/// The game's tables, loaded once from the JSON embedded beside this assembly and checked on load:
/// a bad name or a broken reference throws here, not when a player happens to walk onto it.
public static class Content
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
        Converters = { new JsonStringEnumConverter(allowIntegerValues: false) },
    };

    sealed record LocationsFile(IReadOnlyList<LocationDef> Locations);

    public static IReadOnlyList<LocationDef> Locations { get; }
    public static IReadOnlyDictionary<string, LocationDef> ById { get; }
    public static IReadOnlyDictionary<string, IReadOnlyList<LootRoll>> Loot { get; }
    /// The six stages of the crossing, south-west to far side, in file order.
    public static IReadOnlyList<LocationDef> PassStages { get; }
    public static IReadOnlyList<LocationDef> CaveSites { get; }

    static Content()
    {
        (Locations, Loot) = Parse(Embedded("locations.json"), Embedded("loot.json"));
        ById = Locations.ToDictionary(l => l.Id);
        PassStages = Locations.Where(l => l.Placement == Placement.Pass).ToList();
        CaveSites = Locations.Where(l => l.Placement == Placement.Cave).ToList();
    }

    internal static string Embedded(string file)
    {
        using var stream = typeof(Content).Assembly.GetManifestResourceStream($"Survival.Content.{file}")
            ?? throw new FileNotFoundException($"Content file {file} is not embedded in {typeof(Content).Assembly.GetName().Name}");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    /// Reads and checks both tables; any problem throws InvalidDataException naming it. The game
    /// loads the embedded files through here; tests hand it broken ones.
    internal static (IReadOnlyList<LocationDef> Locations, IReadOnlyDictionary<string, IReadOnlyList<LootRoll>> Loot) Parse(string locationsJson, string lootJson)
    {
        var locations = Deserialize<LocationsFile>(locationsJson, "locations.json").Locations;
        var loot = Deserialize<Dictionary<string, IReadOnlyList<LootRoll>>>(lootJson, "loot.json");
        // Nulls first: every later check would trip over one.
        var problems = NullProblems(locations, loot).ToList();
        if (problems.Count == 0) problems = Problems(locations, loot).ToList();
        if (problems.Count > 0) throw new InvalidDataException("Content is broken:\n" + string.Join("\n", problems));
        return (locations, loot);
    }

    static T Deserialize<T>(string json, string file)
    {
        try { return JsonSerializer.Deserialize<T>(json, Options) ?? throw new InvalidDataException($"{file} is empty"); }
        catch (JsonException e) { throw new InvalidDataException($"{file}: {e.Message}", e); }
    }

    /// Nullable annotations cover properties, not the elements of a list: a stray null in an array
    /// is caught here, by name.
    static IEnumerable<string> NullProblems(IReadOnlyList<LocationDef> locations, IReadOnlyDictionary<string, IReadOnlyList<LootRoll>> loot)
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

    /// Everything enums cannot check by themselves: cross-references, shapes and ranges.
    static IEnumerable<string> Problems(IReadOnlyList<LocationDef> locations, IReadOnlyDictionary<string, IReadOnlyList<LootRoll>> loot)
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
            if (l.Placement == Placement.Named)
            {
                if (l.Weight <= 0) yield return $"{at}: weight {l.Weight}";
                if (l.Terrain is not { Count: > 0 } || l.Terrain.Any(t => !t.IsPassable())) yield return $"{at}: needs passable terrain";
            }
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
                    case SiteShelter s when ShelterLevelProblem(s) is string bad:
                        yield return $"{at}: shelter {s.Name}: {bad}"; break;
                    case SiteForage s when s.Items.Any(i => i.MinKg > i.MaxKg || i.Abundance <= 0):
                        yield return $"{at}: forage has a bad item"; break;
                }
            }
        }
        foreach (var (name, rolls) in loot)
            foreach (var p in RollProblems(rolls)) yield return $"loot {name}: {p}";
    }

    /// Levels and caps are fractions, and work improves a level up to its cap, never down to it.
    static string? ShelterLevelProblem(SiteShelter s)
    {
        var k = Shelters.Kinds[s.Type];
        foreach (var (stat, level, cap) in new[] { ("insulation", s.Insulation, s.InsulationCap ?? k.InsulationCap),
            ("overhead", s.Overhead, s.OverheadCap ?? k.OverheadCap), ("wind", s.Wind, s.WindCap ?? k.WindCap) })
        {
            if (level is < 0 or > 1 || cap is < 0 or > 1) return $"{stat} {level} (cap {cap}) is not in 0-1";
            if (level > cap) return $"{stat} {level} is above its cap {cap}";
        }
        return null;
    }

    static IEnumerable<string> RollProblems(IReadOnlyList<LootRoll> rolls)
    {
        foreach (var r in rolls)
        {
            int kinds = (r.Resource != null ? 1 : 0) + (r.Gear != null ? 1 : 0) + (r.Of != null ? 1 : 0);
            if (kinds != 1) yield return "a roll is exactly one of resource, gear or pick";
            if (r.Chance is <= 0 or > 1) yield return $"chance {r.Chance}";
            if (r.Resource != null && (r.Kg is not [var lo, var hi] || lo <= 0 || hi < lo)) yield return $"{r.Resource}: kg is [min, max]";
            if (r.Pieces is not null and not [> 0, _] || r.Pieces is [var a, var b] && b < a) yield return "pieces is [min, max]";
            foreach (var g in r.Gear ?? []) if (!Items.GearKinds.ContainsKey(g)) yield return $"no gear kind '{g}'";
            if (r.Gear is { Count: 0 }) yield return "gear names none";
            if (r.DurabilityPct is not null and not [>= 0, <= 1] || r.DurabilityPct is [var d0, var d1] && d1 < d0) yield return "durabilityPct is [min, max] in 0-1";
            if (r.Of != null)
            {
                if (r.Pick is not [> 0, var most] || most < r.Pick[0]) yield return "pick is [min, max]";
                foreach (var p in RollProblems(r.Of)) yield return p;
            }
        }
    }
}
