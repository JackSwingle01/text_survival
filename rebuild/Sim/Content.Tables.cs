using System.Collections;
using System.Collections.ObjectModel;

namespace Survival;

/// Every table, as loaded and checked. Immutable: read-only views over collections nothing else holds.
/// Tables keyed by an enum are built in the enum's order, whatever order the file lists them in.
internal sealed record ContentTables(
    IReadOnlyList<LocationDef> Locations,
    IReadOnlyDictionary<string, IReadOnlyList<LootRoll>> Loot,
    IReadOnlyDictionary<Resource, ResourceDef> Resources,
    IReadOnlyDictionary<string, GearDef> Gear,
    IReadOnlyDictionary<EquipSlot, double> SlotCoverage,
    StartingPackDef StartingPack,
    IReadOnlyList<KitGear> StartingClothes,
    IReadOnlyDictionary<Resource, FuelProps> Fuels,
    IReadOnlyDictionary<PitType, PitProps> Pits,
    DeterrenceProps Deterrence,
    IReadOnlyDictionary<Species, SpeciesStats> Species,
    IReadOnlyDictionary<Diet, IReadOnlyList<Resource>> Diets,
    SightingPools Sightings,
    IReadOnlyDictionary<Species, IReadOnlyDictionary<Species, IReadOnlyList<IReadOnlyList<double>>>> KillChances,
    IReadOnlyDictionary<EffectKind, EffectDef> Effects,
    IReadOnlyDictionary<ShelterKind, ShelterKindProps> ShelterKinds,
    IReadOnlyDictionary<string, ShelterDef> Tents,
    IReadOnlyDictionary<Resource, ShelterMaterial> ShelterMaterials,
    IReadOnlyDictionary<BeddingKind, BeddingProps> Beddings,
    IReadOnlyDictionary<ButcherMode, ButcherModeProps> Butchering,
    IReadOnlyDictionary<Terrain, TerrainRow> Terrains,
    IReadOnlyDictionary<Cave, CaveRow> Caves,
    IReadOnlyDictionary<string, SubstrateProfile> Substrates,
    IReadOnlyDictionary<EdgeType, EdgeRow> Edges,
    Felling Felling,
    Footing Footing,
    WaterIce WaterIce,
    IReadOnlyDictionary<string, IReadOnlyList<ForageItem>> Forage,
    DiscoveryTables Discoveries,
    IReadOnlyDictionary<string, IReadOnlyList<Accident>> Accidents,
    IReadOnlyList<Recipe> MakeRecipes,
    IReadOnlyDictionary<Enum, string> Names,
    IReadOnlyDictionary<Resource, IReadOnlyList<string>> PieceWords,
    ForagingTables Foraging);

// ---- rows that have no home in a system's own file ----

/// The starting pack (items.json): kilos listed piece by piece, summed in order, and its gear.
public sealed record StartingPackDef(IReadOnlyDictionary<Resource, IReadOnlyList<double>> Kg, IReadOnlyList<KitGear> Gear);
/// A piece of kit by gear kind; durability, when given, is both its condition and its maximum.
public sealed record KitGear(string Kind, int? Durability = null);

public static partial class Content
{
    // ---- the files as written: dictionaries keyed by name, checked and turned into tables below ----

    sealed record ItemsFile(Dictionary<string, ResourceDef> Resources, Dictionary<string, GearDef> Gear,
        Dictionary<string, double> SlotCoverage, PackFile StartingPack, IReadOnlyList<KitGear> StartingClothes);
    sealed record PackFile(Dictionary<string, IReadOnlyList<double>> Kg, IReadOnlyList<KitGear> Gear);
    sealed record FireFile(Dictionary<string, FuelProps> Fuels, Dictionary<string, PitProps> Pits, DeterrenceProps Deterrence);
    sealed record SpeciesFile(Dictionary<string, SpeciesRow> Species, Dictionary<string, Temperament> Temperaments,
        Dictionary<string, IReadOnlyList<Resource>> Diets, SightingPools Sightings,
        Dictionary<string, Dictionary<string, IReadOnlyList<IReadOnlyList<double>>>> KillChances);
    sealed record SpeciesRow(BodyKind Body, double WeightKg, double FatPct, double MusclePct, Role Role, bool IsSmall,
        double AttackDamage, DamageType AttackType, double Threat, double Boldness, double Aggression, double Cohesion, Group Group,
        int MoveAllowanceM, double ReachM, string Temperament, HerdBehavior Behavior, Diet Diet,
        double TopSpeedMps, double EnduranceSpeedMps, double SprintSeconds, double RunCostJPerKgM, double BurstPct, double GrazeKcalPerMinute,
        double StrikeBackResolve, double ChaseLimitSeconds, double SleepHoursPerDay, ActivityPattern Activity,
        double SmellFactor, double ScentLevel,
        FatRange? AutumnFatPct = null, PersonBuild? Female = null, int? FlightMinutesPerTile = null, double PerceptionFactor = 1,
        double StealthFactor = 1);
    sealed record EffectRow(double SeverityPerHour, double? TreatmentAboveSeverity, double CoreTempFPerHour = 0,
        double BleedMlPerHour = 0, Dictionary<string, double>? Capacities = null, bool Discomfort = false,
        double MetabolicHeatDelta = 0, double HealingDelta = 0, double? HungerFreeHours = null);
    sealed record SheltersFile(Dictionary<string, ShelterKindProps> Kinds, Dictionary<string, ShelterDef> Tents,
        Dictionary<string, ShelterMaterial> Materials, Dictionary<string, BeddingProps> Beddings);
    sealed record ButcherFile(double TimeFactor, double ScentIncrease, double BloodySeverity, Dictionary<string, double> YieldFactors);
    sealed record TerrainFile(Dictionary<string, TerrainRow> Terrains, Dictionary<string, CaveRow> Caves,
        Dictionary<string, SubstrateProfile> Substrates, Dictionary<string, EdgeRow> Edges, FellingFile Felling, Footing Footing, WaterIce WaterIce);
    sealed record FellingFile(Pieces Logs, Dictionary<string, double> LogKg, double LogKgSpreadPct, Dictionary<string, double> MixedWood,
        Pieces Kindling, Pieces Tinder);
    sealed record DiscoveriesFile(Dictionary<string, FindDef> Finds, Dictionary<string, FindDef> Details,
        Dictionary<string, IReadOnlyList<DiscoveryDraw>> Terrains, IReadOnlyList<DiscoveryDraw> Cave,
        Dictionary<string, IReadOnlyList<string>> DetailDraws, IReadOnlyList<string> ExamineOnly);
    sealed record ForagingFile(IReadOnlyList<QualityRow> Quality, IReadOnlyList<FailureRow> Failures, Dictionary<string, string> Luck,
        Dictionary<string, string> Hints, IReadOnlyList<ClueDef> Clues, Dictionary<string, string> Tutorials, IReadOnlyList<SearchBand> SearchBands);
    sealed record RecipeFileRow(string Name, MakeCategory Category, IReadOnlyList<InputRow> Inputs, IReadOnlyList<Capability> Tools,
        IReadOnlyList<Requirement> Requires, int Minutes, ResultRow Result, IReadOnlyList<string> Outcomes, IReadOnlyList<Capability>? Faster = null,
        double? FasterFactor = null);
    /// Kg is read only to refuse it: a discrete input is a count, a continuous one grams.
    sealed record InputRow(string? Item = null, string? Of = null, int Count = 0, double G = 0, double Kg = 0);
    sealed record ResultRow(string? Gear = null, Resource? Resource = null, int Count = 0, double SharePct = 0, double G = 0, double Kg = 0,
        ShelterKind? Shelter = null, string? ShelterName = null, BeddingKind? Bedding = null, IReadOnlyList<string>? Repairs = null, double RestoresPct = 0);

    /// Names the game's code asks for by name: they must be in the files.
    static readonly string[] CodeSubstrates = ["loam", "forestFloor", "slope", "rock", "peat", "iceSheet", "impassable"],
        CodeForage = ["MixedForest"],
        CodeAccidents = ["TripStumble", "SharpHazards", "IceSlip", "RockyTerrain", "FallImpact", "Sprains"],
        CodeGear = ["stone_axe", "flint_knife"], CodeLocations = ["wolf_den", "bear_cave", "bone_hollow"];

    /// Reads and checks every table; any problem throws InvalidDataException naming it. The game
    /// loads the embedded files through here; tests hand it broken ones.
    internal static ContentTables Parse(IReadOnlyDictionary<string, string> files)
    {
        string Text(string file) => files.TryGetValue(file, out var json) ? json : throw new InvalidDataException($"Content is broken:\nno {file}");
        var locations = Deserialize<LocationsFile>(Text("locations.json"), "locations.json").Locations;
        var loot = Deserialize<Dictionary<string, IReadOnlyList<LootRoll>>>(Text("loot.json"), "loot.json");
        var items = Deserialize<ItemsFile>(Text("items.json"), "items.json");
        var fire = Deserialize<FireFile>(Text("fire.json"), "fire.json");
        var species = Deserialize<SpeciesFile>(Text("species.json"), "species.json");
        var effects = Deserialize<Dictionary<string, EffectRow>>(Text("effects.json"), "effects.json");
        var shelters = Deserialize<SheltersFile>(Text("shelters.json"), "shelters.json");
        var butchering = Deserialize<Dictionary<string, ButcherFile>>(Text("butchering.json"), "butchering.json");
        var terrain = Deserialize<TerrainFile>(Text("terrain.json"), "terrain.json");
        var forage = Deserialize<Dictionary<string, IReadOnlyList<ForageItem>>>(Text("forage.json"), "forage.json");
        var discoveries = Deserialize<DiscoveriesFile>(Text("discoveries.json"), "discoveries.json");
        var accidents = Deserialize<Dictionary<string, IReadOnlyList<Accident>>>(Text("accidents.json"), "accidents.json");
        var recipeRows = Deserialize<Dictionary<string, RecipeFileRow>>(Text("recipes.json"), "recipes.json");
        var nameRows = Deserialize<Dictionary<string, Dictionary<string, string>>>(Text("names.json"), "names.json");
        var foraging = Deserialize<ForagingFile>(Text("foraging.json"), "foraging.json");

        // Nulls first: every later check would trip over one.
        var problems = LocationNullProblems(locations, loot).ToList();
        foreach (var (file, root) in new (string, object)[] { ("items.json", items), ("fire.json", fire), ("species.json", species),
            ("effects.json", effects), ("shelters.json", shelters), ("butchering.json", butchering), ("terrain.json", terrain),
            ("forage.json", forage), ("discoveries.json", discoveries), ("accidents.json", accidents), ("recipes.json", recipeRows), ("names.json", nameRows), ("foraging.json", foraging) })
            problems.AddRange(NullsIn(root, file));
        Fail(problems);

        // Enum names next: a key the game has no kind for, or a kind with no row.
        var resources = Keyed<Resource, ResourceDef>(items.Resources, "items.json resources", problems, complete: true);
        var slots = Keyed<EquipSlot, double>(items.SlotCoverage, "items.json slotCoverage", problems, complete: true);
        var packKg = Keyed<Resource, IReadOnlyList<double>>(items.StartingPack.Kg, "items.json startingPack", problems);
        var fuels = Keyed<Resource, FuelProps>(fire.Fuels, "fire.json fuels", problems);
        var pits = Keyed<PitType, PitProps>(fire.Pits, "fire.json pits", problems, complete: true);
        var speciesRows = Keyed<Species, SpeciesRow>(species.Species, "species.json species", problems, complete: true);
        var diets = Keyed<Diet, IReadOnlyList<Resource>>(species.Diets, "species.json diets", problems, complete: true);
        var killChances = Keyed<Species, Dictionary<string, IReadOnlyList<IReadOnlyList<double>>>>(species.KillChances, "species.json killChances", problems)
            .ToDictionary(p => p.Key, p => (IReadOnlyDictionary<Species, IReadOnlyList<IReadOnlyList<double>>>)ReadOnly(
                Keyed<Species, IReadOnlyList<IReadOnlyList<double>>>(p.Value, $"species.json killChances {p.Key}", problems)));
        problems.AddRange(KillChanceProblems(killChances));
        var effectRows = Keyed<EffectKind, EffectRow>(effects, "effects.json", problems, complete: true);
        var effectCaps = effectRows.ToDictionary(e => e.Key,
            e => Keyed<Capacity, double>(e.Value.Capacities ?? new Dictionary<string, double>(), $"effects.json {e.Key} capacities", problems));
        var kinds = Keyed<ShelterKind, ShelterKindProps>(shelters.Kinds, "shelters.json kinds", problems, complete: true);
        var materials = Keyed<Resource, ShelterMaterial>(shelters.Materials, "shelters.json materials", problems);
        var beddings = Keyed<BeddingKind, BeddingProps>(shelters.Beddings, "shelters.json beddings", problems, complete: true);
        var butcherRows = Keyed<ButcherMode, ButcherFile>(butchering, "butchering.json", problems, complete: true);
        var yields = butcherRows.ToDictionary(b => b.Key,
            b => Keyed<Resource, double>(b.Value.YieldFactors, $"butchering.json {b.Key} yieldFactors", problems));
        var terrains = Keyed<Terrain, TerrainRow>(terrain.Terrains, "terrain.json terrains", problems, complete: true);
        var caves = Keyed<Cave, CaveRow>(terrain.Caves, "terrain.json caves", problems);
        var edges = Keyed<EdgeType, EdgeRow>(terrain.Edges, "terrain.json edges", problems, complete: true);
        var logKg = Keyed<Resource, double>(terrain.Felling.LogKg, "terrain.json felling logKg", problems);
        var mixedWood = Keyed<Resource, double>(terrain.Felling.MixedWood, "terrain.json felling mixedWood", problems);
        var draws = Keyed<Terrain, IReadOnlyList<DiscoveryDraw>>(discoveries.Terrains, "discoveries.json terrains", problems);
        var detailDraws = Keyed<Terrain, IReadOnlyList<string>>(discoveries.DetailDraws, "discoveries.json detailDraws", problems);
        var pieceWords = PieceWordTable(nameRows, items.Resources, problems);
        var names = NameTable(nameRows, problems);
        var luck = Keyed<LuckTier, string>(foraging.Luck, "foraging.json luck", problems);
        var hints = Keyed<Resource, string>(foraging.Hints, "foraging.json hints", problems, complete: true);
        var tutorials = Keyed<Tutorial, string>(foraging.Tutorials, "foraging.json tutorials", problems, complete: true);
        Fail(problems);

        // Then references, shapes and ranges.
        problems.AddRange(LocationProblems(locations, loot, items.Gear, kinds, terrains));
        problems.AddRange(ItemProblems(items, resources, slots, packKg));
        problems.AddRange(FireProblems(fuels, pits, fire.Deterrence));
        problems.AddRange(SpeciesProblems(species, speciesRows));
        problems.AddRange(EffectProblems(effectRows, effectCaps));
        problems.AddRange(ShelterProblems(shelters, items.Gear, kinds, materials, beddings));
        problems.AddRange(ButcherProblems(butcherRows, yields));
        problems.AddRange(TerrainProblems(terrain, terrains, caves, forage));
        problems.AddRange(FellingProblems(terrain.Felling, logKg, mixedWood, fuels, locations));
        problems.AddRange(ForageProblems(forage));
        problems.AddRange(DiscoveryProblems(discoveries, draws, detailDraws, terrains, items.Gear, loot, locations));
        problems.AddRange(AccidentProblems(accidents));
        problems.AddRange(ForagingProblems(foraging, luck));
        var makeRecipes = MakeRecipes(recipeRows, items.Gear, resources, problems);
        problems.AddRange(PieceProblems(resources, forage, locations, discoveries, loot, terrain.Felling, logKg, packKg));
        problems.AddRange(DeadRows(locations, loot, terrain, forage, discoveries, accidents));
        foreach (var kind in CodeGear) if (!items.Gear.ContainsKey(kind)) problems.Add($"items.json: no gear kind '{kind}', which the code names");
        foreach (var id in CodeLocations) if (!locations.Any(l => l.Id == id)) problems.Add($"locations.json: no location '{id}', which the code names");
        Fail(problems);

        return new ContentTables(
            locations, new ReadOnlyDictionary<string, IReadOnlyList<LootRoll>>(loot),
            ReadOnly(resources), new ReadOnlyDictionary<string, GearDef>(items.Gear), ReadOnly(slots),
            new StartingPackDef(ReadOnly(packKg), items.StartingPack.Gear), items.StartingClothes,
            ReadOnly(fuels), ReadOnly(pits), fire.Deterrence,
            ReadOnly(speciesRows.ToDictionary(s => s.Key, s => ToStats(s.Value, species.Temperaments[s.Value.Temperament]))),
            ReadOnly(diets), species.Sightings, ReadOnly(killChances),
            ReadOnly(effectRows.ToDictionary(e => e.Key, e => new EffectDef(e.Value.SeverityPerHour, e.Value.TreatmentAboveSeverity,
                e.Value.CoreTempFPerHour, e.Value.BleedMlPerHour, effectCaps[e.Key].Select(c => (c.Key, c.Value)).ToList().AsReadOnly(), e.Value.Discomfort,
                e.Value.MetabolicHeatDelta, e.Value.HealingDelta, e.Value.HungerFreeHours))),
            ReadOnly(kinds), new ReadOnlyDictionary<string, ShelterDef>(shelters.Tents), ReadOnly(materials),
            ReadOnly(beddings),
            ReadOnly(butcherRows.ToDictionary(b => b.Key, b => new ButcherModeProps(b.Value.TimeFactor, ReadOnly(yields[b.Key]), b.Value.ScentIncrease, b.Value.BloodySeverity))),
            ReadOnly(terrains), ReadOnly(caves), new ReadOnlyDictionary<string, SubstrateProfile>(terrain.Substrates), ReadOnly(edges),
            new Felling(terrain.Felling.Logs, ReadOnly(logKg), terrain.Felling.LogKgSpreadPct,
                mixedWood.Select(m => (m.Key, m.Value)).ToList().AsReadOnly(), terrain.Felling.Kindling, terrain.Felling.Tinder),
            terrain.Footing,
            terrain.WaterIce,
            new ReadOnlyDictionary<string, IReadOnlyList<ForageItem>>(forage),
            new DiscoveryTables(new ReadOnlyDictionary<string, FindDef>(discoveries.Finds), new ReadOnlyDictionary<string, FindDef>(discoveries.Details),
                ReadOnly(draws), discoveries.Cave, ReadOnly(detailDraws), discoveries.ExamineOnly),
            new ReadOnlyDictionary<string, IReadOnlyList<Accident>>(accidents),
            makeRecipes, ReadOnly(names), ReadOnly(pieceWords),
            new ForagingTables(foraging.Quality, foraging.Failures, ReadOnly(luck), ReadOnly(hints), foraging.Clues, ReadOnly(tutorials), foraging.SearchBands));
    }

    /// foraging.json: words for every band, highest first; one failure band for every word; a mood for every
    /// luck but Normal; each clue with exactly what its kind reads.
    static IEnumerable<string> ForagingProblems(ForagingFile f, Dictionary<LuckTier, string> luck)
    {
        for (int i = 1; i < f.Quality.Count; i++)
            if (f.Quality[i].AtLeast >= f.Quality[i - 1].AtLeast) yield return $"foraging.json quality: {f.Quality[i].Word} is not below {f.Quality[i - 1].Word}";
        if (f.Quality.Count == 0 || f.Quality[^1].AtLeast != 0) yield return "foraging.json quality: the last word starts at 0";
        foreach (var q in f.Quality)
            if (f.Failures.Count(b => b.Words.Contains(q.Word)) != 1) yield return $"foraging.json failures: {q.Word} is in {f.Failures.Count(b => b.Words.Contains(q.Word))} bands";
        foreach (var b in f.Failures)
        {
            if (b.Lines.Count == 0) yield return "foraging.json failures: a band with no lines";
            foreach (var w in b.Words) if (f.Quality.All(q => q.Word != w)) yield return $"foraging.json failures: no quality word '{w}'";
        }
        foreach (var t in Enum.GetValues<LuckTier>())
            if ((t == LuckTier.Normal) == luck.ContainsKey(t)) yield return $"foraging.json luck: {t} {(t == LuckTier.Normal ? "says nothing" : "needs its line")}";
        for (int i = 1; i < f.SearchBands.Count; i++)
            if (f.SearchBands[i].BelowPct <= f.SearchBands[i - 1].BelowPct) yield return "foraging.json searchBands: not rising";
        foreach (var c in f.Clues)
        {
            string at = $"foraging.json clues '{c.Text}'";
            if (c.Weight <= 0 || c.Yield <= 0) yield return $"{at}: weight and yield are positive";
            bool resources = c.Resources is { Count: > 0 }, game = c.Game != null, scavenge = c.HarvestedPct != null;
            switch (c.Kind)
            {
                case ClueKind.Resource when !resources || game || scavenge: yield return $"{at}: a resource clue names resources and nothing else"; break;
                case ClueKind.Game when !game || resources || scavenge: yield return $"{at}: a game clue names its game and nothing else"; break;
                case ClueKind.Negative when resources || game || scavenge || c.Yield >= 1: yield return $"{at}: a negative clue cuts the yield, nothing else"; break;
                case ClueKind.Scavenge when c.HarvestedPct is not [var lo, var hi] || lo < 0 || hi < lo || hi >= 1 || c.Remains == null || resources || game:
                    yield return $"{at}: a scavenge clue has harvestedPct [min, max] in 0-1 and its remains"; break;
            }
            if (c.Animals?.Any(a => a.Weight <= 0) == true) yield return $"{at}: an animal with no weight";
        }
        if (f.Clues.Count(c => c.Kind == ClueKind.Resource && c.When == ClueWhen.Always) != 1) yield return "foraging.json clues: exactly one generic (Always) resource clue";
    }

    const string PiecesTable = "Pieces";

    /// names.json Pieces: "one|many" for every discrete resource, and for nothing else.
    static Dictionary<Resource, IReadOnlyList<string>> PieceWordTable(Dictionary<string, Dictionary<string, string>> rows,
        Dictionary<string, ResourceDef> resources, List<string> problems)
    {
        var table = new Dictionary<Resource, IReadOnlyList<string>>();
        var words = rows.GetValueOrDefault(PiecesTable) ?? [];
        foreach (var r in Enum.GetValues<Resource>())
        {
            bool discrete = resources.TryGetValue(r.ToString(), out var def) && def.Discrete;
            if (!words.TryGetValue(r.ToString(), out var w)) { if (discrete) problems.Add($"names.json Pieces: no words for discrete {r}"); continue; }
            if (!discrete && def?.Form != null) problems.Add($"names.json Pieces: {r} is not discrete");
            var both = w.Split('|');
            if (both.Length != 2 || both.Any(x => string.IsNullOrWhiteSpace(x) || x != x.Trim())) problems.Add($"names.json Pieces.{r}: '{w}' is not 'one|many'");
            else table[r] = Array.AsReadOnly(both);
        }
        foreach (var key in words.Keys)
            if (!Enum.GetNames<Resource>().Contains(key, StringComparer.Ordinal)) problems.Add($"names.json Pieces: no Resource '{key}'");
        return table;
    }

    /// names.json: every value of every enum Names.Tables lists, and nothing else, each with its words.
    static Dictionary<Enum, string> NameTable(Dictionary<string, Dictionary<string, string>> rows, List<string> problems)
    {
        var table = new Dictionary<Enum, string>();
        foreach (var name in rows.Keys.Where(n => n != PiecesTable))
            if (!Survival.Names.Tables.Any(t => t.Name == name)) problems.Add($"names.json: {name} is not a table of names (Names.Tables)");
        foreach (var type in Survival.Names.Tables)
        {
            if (!rows.TryGetValue(type.Name, out var words)) { problems.Add($"names.json: no {type.Name} table"); continue; }
            foreach (var key in words.Keys)
                if (!Enum.GetNames(type).Contains(key, StringComparer.Ordinal)) problems.Add($"names.json {type.Name}: no {type.Name} '{key}'");
            foreach (Enum value in Enum.GetValues(type))
            {
                if (!words.TryGetValue(value.ToString(), out var w)) problems.Add($"names.json {type.Name}: no words for {value}");
                else if (string.IsNullOrWhiteSpace(w) || w != w.Trim()) problems.Add($"names.json {type.Name}.{value}: '{w}' is not words");
                else table[value] = w;
            }
        }
        return table;
    }

    static void Fail(List<string> problems)
    {
        if (problems.Count > 0) throw new InvalidDataException("Content is broken:\n" + string.Join("\n", problems));
    }

    static IReadOnlyDictionary<TKey, T> ReadOnly<TKey, T>(Dictionary<TKey, T> d) where TKey : notnull => new ReadOnlyDictionary<TKey, T>(d);

    /// A table keyed by enum name, exactly as the enum spells it (the JSON reader alone would take
    /// "tinder" or "Pine, Oak"), built in the enum's order. complete: every value must have a row.
    static Dictionary<TEnum, T> Keyed<TEnum, T>(IReadOnlyDictionary<string, T> rows, string at, List<string> problems, bool complete = false)
        where TEnum : struct, Enum
    {
        foreach (var name in rows.Keys)
            if (!Enum.GetNames<TEnum>().Contains(name, StringComparer.Ordinal)) problems.Add($"{at}: no {typeof(TEnum).Name} '{name}'");
        var table = new Dictionary<TEnum, T>();
        foreach (var value in Enum.GetValues<TEnum>())
        {
            if (rows.TryGetValue(value.ToString(), out var row)) table[value] = row;
            else if (complete) problems.Add($"{at}: no row for {value}");
        }
        return table;
    }

    /// Nullable annotations cover properties, not the elements of a list or the values of a
    /// dictionary: a stray null anywhere in a file is caught here, by its path.
    static IEnumerable<string> NullsIn(object node, string path)
    {
        if (node is string or Enum || node.GetType().IsPrimitive) yield break;
        if (node is IDictionary d)
        {
            foreach (DictionaryEntry e in d)
            {
                if (e.Value == null) { yield return $"{path}.{e.Key} is null"; continue; }
                foreach (var p in NullsIn(e.Value, $"{path}.{e.Key}")) yield return p;
            }
            yield break;
        }
        if (node is IEnumerable list)
        {
            int i = 0;
            foreach (var x in list)
            {
                if (x == null) yield return $"{path}[{i}] is null";
                else foreach (var p in NullsIn(x, $"{path}[{i}]")) yield return p;
                i++;
            }
            yield break;
        }
        if (node.GetType().Namespace != nameof(Survival)) yield break;
        foreach (var prop in node.GetType().GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance))
            if (prop.GetIndexParameters().Length == 0 && prop.CanWrite && prop.GetValue(node) is { } value) // read, not derived
                foreach (var p in NullsIn(value, $"{path}.{char.ToLowerInvariant(prop.Name[0])}{prop.Name[1..]}")) yield return p;
    }

    static SpeciesStats ToStats(SpeciesRow r, Temperament t) =>
        new(r.Body, r.WeightKg, r.FatPct, r.MusclePct, r.Role, r.IsSmall, r.AttackDamage, r.AttackType,
            r.Threat, r.Boldness, r.Aggression, r.Cohesion, r.Group, r.MoveAllowanceM, r.ReachM, t, r.Behavior, r.Diet,
            new Motion(r.TopSpeedMps, r.EnduranceSpeedMps, r.SprintSeconds, r.RunCostJPerKgM, r.BurstPct), r.GrazeKcalPerMinute, r.StrikeBackResolve, r.ChaseLimitSeconds, r.SleepHoursPerDay, r.Activity, r.AutumnFatPct, r.Female, r.SmellFactor, r.ScentLevel, r.FlightMinutesPerTile, r.PerceptionFactor, r.StealthFactor);

    // ---- checks, one family at a time ----

    /// The Make recipes, in file order, each checked: names resolve, outcomes parse and name only the
    /// recipe's own inputs and tools, and the odds leave most of the work to the plain result.
    static IReadOnlyList<Recipe> MakeRecipes(Dictionary<string, RecipeFileRow> rows, IReadOnlyDictionary<string, GearDef> gear,
        IReadOnlyDictionary<Resource, ResourceDef> resources, List<string> problems)
    {
        var recipes = new List<Recipe>();
        var repaired = new HashSet<string>();
        foreach (var (id, row) in rows)
        {
            string at = $"recipes.json {id}";
            int before = problems.Count;
            var materials = new List<MaterialInput>();
            var items = new List<string>();
            foreach (var input in row.Inputs)
            {
                if ((input.Item != null) == (input.Of != null)) { problems.Add($"{at}: an input is an item or a material (of), exactly one"); continue; }
                if (input.Kg != 0) problems.Add($"{at}: {input.Item ?? input.Of} names kg: a discrete input is a count, a continuous one grams (g)");
                if (input.Item != null)
                {
                    if (!gear.ContainsKey(input.Item)) problems.Add($"{at}: no gear kind '{input.Item}'");
                    if (input.G != 0 || input.Count != 0) problems.Add($"{at}: an item input {input.Item} is one piece, with no count or g");
                    items.Add(input.Item);
                    continue;
                }
                var anyOf = new List<Resource>();
                foreach (var name in input.Of!.Split('|'))
                    if (Enum.GetNames<Resource>().Contains(name, StringComparer.Ordinal)) anyOf.Add(Enum.Parse<Resource>(name));
                    else problems.Add($"{at}: no Resource '{name}'");
                bool anyDiscrete = anyOf.Any(x => resources[x].Discrete), anyContinuous = anyOf.Any(x => !resources[x].Discrete);
                if (anyDiscrete != input.Count > 0) problems.Add($"{at}: {input.Of} count {input.Count}: a discrete input, and only one, is a count of pieces");
                if (anyContinuous != input.G > 0) problems.Add(anyContinuous ? $"{at}: {input.Of} g {input.G}: a continuous input is grams"
                    : $"{at}: {input.Of} is discrete: a count of pieces, never a weight (g)");
                if (input.Count < 0) problems.Add($"{at}: {input.Of} count {input.Count}");
                materials.Add(new MaterialInput(anyOf.AsReadOnly(), input.G / 1000, input.Count));
            }
            var inputResources = materials.SelectMany(m => m.AnyOf).ToList();
            if (inputResources.Distinct().Count() != inputResources.Count) problems.Add($"{at}: a resource in two inputs");
            if (row.Minutes <= 0) problems.Add($"{at}: minutes {row.Minutes}");
            var r = row.Result;
            bool placed = r.Shelter != null || r.Bedding != null;
            if (new[] { r.Gear != null, r.Resource != null, r.Shelter != null, r.Bedding != null, r.Repairs != null }.Count(x => x) != 1)
                problems.Add($"{at}: the result is a gear kind, a resource, a shelter, a bedding or a repair, exactly one");
            else if (r.Repairs != null) problems.AddRange(RepairProblems(at, row, items, gear, repaired));
            else if (placed) problems.AddRange(PlacedProblems(at, row, items));
            else if (r.Gear != null && !gear.ContainsKey(r.Gear)) problems.Add($"{at}: no gear kind '{r.Gear}'");
            else if (r.Gear != null && (r.Kg != 0 || r.G != 0 || r.Count != 0 || r.SharePct != 0)) problems.Add($"{at}: a gear result is one piece, with no amount");
            else if (r.Resource != null && items.Count > 0) problems.Add($"{at}: a resource result is worked from materials, not items");
            else if (r.Resource is Resource made && resources[made].Discrete && (r.Count <= 0 || r.SharePct is <= 0 or > 1 || r.G != 0 || r.Kg != 0))
                problems.Add($"{at}: a discrete result {made} is a count of pieces and a sharePct (0-1] of the inputs' weight");
            else if (r.Resource is Resource made2 && !resources[made2].Discrete && (r.G <= 0 || r.Count != 0 || r.SharePct != 0 || r.Kg != 0))
                problems.Add($"{at}: a continuous result {made2} is grams (g)");
            var faster = row.Faster ?? Array.AsReadOnly(Array.Empty<Capability>());
            if (row.FasterFactor is double by && (by <= 1 || faster.Count == 0)) problems.Add($"{at}: fasterFactor {by} is over 1, and only with a faster tool");
            foreach (var (name, caps) in new[] { ("tools", row.Tools), ("faster", faster) })
                if (caps.Contains(Capability.None) || caps.Distinct().Count() != caps.Count) problems.Add($"{at}: {name} names None or a capability twice");
            if (row.Requires.Distinct().Count() != row.Requires.Count) problems.Add($"{at}: a requirement listed twice");
            if (!placed && row.Requires.Any(q => q is Requirement.NoShelter or Requirement.Tent)) problems.Add($"{at}: NoShelter and Tent are for a placed result");
            var outcomes = new List<Outcome>();
            foreach (var line in row.Outcomes)
            {
                try { outcomes.AddRange(Outcomes.Parse(line)); }
                catch (FormatException e) { problems.Add($"{at}: {e.Message}"); }
            }
            foreach (var o in outcomes)
                foreach (var e in o.Effects)
                    switch (e)
                    {
                        case WasteMaterial w when !inputResources.Contains(w.Input): problems.Add($"{at}: {w.Input} is not an input"); break;
                        case SaveMaterial s when !inputResources.Contains(s.Input): problems.Add($"{at}: {s.Input} is not an input"); break;
                        case BreakTool b when !row.Tools.Contains(b.Capability) && !faster.Contains(b.Capability):
                            problems.Add($"{at}: {b.Capability} is not a tool it uses"); break;
                        case ExtraProduct x when !gear.ContainsKey(x.What) && !Enum.GetNames<Resource>().Contains(x.What, StringComparer.Ordinal):
                            problems.Add($"{at}: extra_product {x.What} is no gear kind or resource"); break;
                        case ExtraProduct x when (gear.ContainsKey(x.What) || Enum.TryParse<Resource>(x.What, out var whole) && resources[whole].Discrete)
                            && x.Amount != Math.Floor(x.Amount):
                            problems.Add($"{at}: extra_product {x.What} is whole pieces"); break;
                        case ExtraProduct x when Enum.TryParse<Resource>(x.What, out var xr) && resources[xr].Discrete && xr != r.Resource:
                            problems.Add($"{at}: extra_product {x.What} is discrete: an extra piece is of the recipe's own result, and an extra resource otherwise an amount"); break;
                    }
            if (problems.Count == before) problems.AddRange(WeightProblems(at, materials, items, r, outcomes, gear, resources));
            double sum = outcomes.Sum(o => o.BasePct);
            if (sum > MaxOutcomePct + 1e-12) problems.Add($"{at}: outcomes' odds add up to {sum}, above {MaxOutcomePct}");
            if (problems.Count > before) continue;
            recipes.Add(new Recipe(id, row.Name, row.Category, materials.AsReadOnly(), items.AsReadOnly(), row.Tools, faster, row.Requires, row.Minutes,
                new RecipeResult(r.Gear, r.Resource, r.G / 1000, r.Shelter, r.ShelterName, r.Bedding, r.Count, r.SharePct,
                    r.Repairs is { } kinds ? new RepairResult(kinds, r.RestoresPct) : null), outcomes.AsReadOnly(),
                row.FasterFactor ?? Making.FasterToolFactor));
        }
        return recipes.AsReadOnly();
    }

    /// The lightest and heaviest one input can be: for each alternative, its count of the lightest or heaviest
    /// pieces, or its grams; null heaviest for nothing to bound it.
    static (double Lightest, double Heaviest) InputKg(MaterialInput m, IReadOnlyDictionary<Resource, ResourceDef> resources, int? pieces = null)
    {
        var each = m.AnyOf.Select(x => resources[x].Discrete
            ? ((pieces ?? m.Count) * resources[x].MinPieceKg, (pieces ?? m.Count) * resources[x].MaxPieceKg)
            : (m.Kg, m.Kg)).ToList();
        return (each.Min(e => e.Item1), each.Max(e => e.Item2));
    }

    /// Gear weighs no more than its lightest possible inputs. A resource result never weighs more than what it is
    /// worked from, less any saving; a discrete one's pieces stay in their kind's range, however light or heavy the
    /// pieces that went in and whatever a succeeding outcome wastes.
    static IEnumerable<string> WeightProblems(string at, List<MaterialInput> materials, List<string> items, ResultRow r, List<Outcome> outcomes,
        IReadOnlyDictionary<string, GearDef> gear, IReadOnlyDictionary<Resource, ResourceDef> resources)
    {
        double lightest = materials.Sum(m => InputKg(m, resources).Lightest) + items.Sum(i => gear[i].BaseWeightKg);
        if (r.Gear != null && gear[r.Gear].BaseWeightKg > lightest + 1e-9)
            yield return $"{at}: {r.Gear} weighs {gear[r.Gear].BaseWeightKg} kg, above its lightest possible inputs, {lightest:0.###} kg";
        if (r.Resource is not Resource made) yield break;
        foreach (var o in outcomes.Prepend(null))
        {
            double ShareOf(Resource? input, IEnumerable<Effect> effects) => effects.Where(e => e is WasteMaterial w && w.Input == input || e is SaveMaterial s && s.Input == input)
                .Sum(e => e is WasteMaterial w ? w.SharePct : ((SaveMaterial)e).SharePct);
            var effects = o?.Effects ?? [];
            if (o?.Kind == OutcomeKind.Fail) continue;
            double lo = 0, hi = 0, saved = 0;
            foreach (var m in materials)
            {
                double share = Math.Min(1, m.AnyOf.Sum(x => ShareOf(x, effects.OfType<WasteMaterial>())));
                double kept = m.AnyOf.Sum(x => ShareOf(x, effects.OfType<SaveMaterial>()));
                var (l, h) = InputKg(m, resources);
                lo += l * (1 - share);
                hi += h;
                saved += l * kept;
            }
            if (resources[made].Discrete)
            {
                int pieces = r.Count + Making.ExtraPieces(effects, made);
                double light = lo * r.SharePct / pieces, heavy = hi * r.SharePct / pieces;
                if (light < resources[made].MinPieceKg - 1e-9 || heavy > resources[made].MaxPieceKg + 1e-9)
                    yield return $"{at}: a piece of {made} would weigh {light:0.###}-{heavy:0.###} kg{(o == null ? "" : $" ({o.Text})")}, outside {resources[made].MinPieceKg}-{resources[made].MaxPieceKg}";
                if (r.SharePct * lightest > lightest - saved + 1e-9) yield return $"{at}: {made} at {r.SharePct} of the inputs is more than is left after saving{(o == null ? "" : $" ({o.Text})")}";
            }
            else if (r.G / 1000 > lightest - saved + 1e-9)
                yield return $"{at}: {r.G} g of {made} from {lightest - saved:0.###} kg{(o == null ? "" : $" ({o.Text})")}";
        }
    }

    /// A repair works on pieces that wear, each kind in one repair recipe, none of them a tool of its own work; it
    /// restores a share (0-1] of the missing condition and takes no items.
    static IEnumerable<string> RepairProblems(string at, RecipeFileRow row, List<string> items, IReadOnlyDictionary<string, GearDef> gear, HashSet<string> repaired)
    {
        var r = row.Result;
        if (items.Count > 0) yield return $"{at}: a repair takes no items";
        if (r.RestoresPct is <= 0 or > 1) yield return $"{at}: restoresPct {r.RestoresPct} is a share (0-1] of the missing condition";
        if (r.Kg != 0 || r.G != 0 || r.Count != 0 || r.SharePct != 0) yield return $"{at}: a repair has no amount";
        if (r.Repairs!.Count == 0) yield return $"{at}: a repair names the gear kinds it works on";
        var uses = row.Tools.Concat(row.Faster ?? []).ToList();
        foreach (var kind in r.Repairs)
        {
            if (!gear.TryGetValue(kind, out var def)) { yield return $"{at}: no gear kind '{kind}'"; continue; }
            if (def.MaxDurability <= 0) yield return $"{at}: {kind} never wears, so has nothing to repair";
            if (!repaired.Add(kind)) yield return $"{at}: {kind} is repaired by another recipe too";
            if (def.Capabilities.Intersect(uses).Any()) yield return $"{at}: {kind} gives a capability its own repair uses";
        }
    }

    /// Something placed on the tile is camp work, takes no items, rolls no outcomes, and a shelter
    /// goes only where none stands. A tent is the shelter of the tent carried; any other shelter is named.
    static IEnumerable<string> PlacedProblems(string at, RecipeFileRow row, List<string> items)
    {
        var r = row.Result;
        if (row.Category != MakeCategory.Camp) yield return $"{at}: a placed result is Camp work";
        if (items.Count > 0) yield return $"{at}: a placed result takes no items";
        if (row.Outcomes.Count > 0) yield return $"{at}: a placed result has no outcomes";
        if (r.Kg != 0 || r.G != 0 || r.Count != 0 || r.SharePct != 0) yield return $"{at}: a placed result has no amount";
        bool tent = row.Requires.Contains(Requirement.Tent);
        if (r.Bedding != null && (r.ShelterName != null || tent || row.Requires.Contains(Requirement.NoShelter))) yield return $"{at}: a bedding is not named, needs no tent and goes beside a shelter";
        if (r.Shelter != null && !row.Requires.Contains(Requirement.NoShelter)) yield return $"{at}: a shelter needs NoShelter";
        if (r.Shelter != null && tent != (r.Shelter == ShelterKind.HideTent)) yield return $"{at}: a tent's shelter is HideTent, and only a tent's";
        if (r.Shelter != null && tent == (r.ShelterName != null)) yield return $"{at}: a shelter is named, unless it is the tent carried";
    }

    /// The most a recipe's outcomes may take of the odds before skill and hands move them: the plain
    /// result stays the likeliest thing by far.
    internal const double MaxOutcomePct = 0.5;

    static IEnumerable<string> ItemProblems(ItemsFile f, Dictionary<Resource, ResourceDef> resources, Dictionary<EquipSlot, double> slots,
        Dictionary<Resource, IReadOnlyList<double>> packKg)
    {
        foreach (var (r, def) in resources)
        {
            if (def.Category == ResourceCategory.Food && def.KcalPerKg <= 0) yield return $"items.json: food {r} gives no kcal";
            if (def.Category == ResourceCategory.Water && def.HydrationMlPerKg <= 0) yield return $"items.json: water {r} needs its hydrationMlPerKg";
            if (def.Category is not (ResourceCategory.Food or ResourceCategory.Water) && (def.KcalPerKg != 0 || def.HydrationMlPerKg != 0))
                yield return $"items.json: {r} is not food or water and gives kcal or water";
            if (def.KcalPerKg < 0) yield return $"items.json: {r} kcalPerKg {def.KcalPerKg}";
            if (def.Form == null) yield return $"items.json: {r} is neither discrete nor continuous (form)";
            else if (def.Discrete && !(def.PieceKg is { Min: > 0 } range && range.Min <= range.Max))
                yield return $"items.json: discrete {r} needs pieceKg [min, max], above 0";
            else if (!def.Discrete && def.PieceKg != null) yield return $"items.json: continuous {r} has no pieces (pieceKg)";
            if ((def.Category == ResourceCategory.Food) != (def.ScentLevel != null)) yield return $"items.json: {r}: food, and only food, states its scentLevel";
            if (def.ScentLevel is < 0 or > 1) yield return $"items.json: {r} scentLevel {def.ScentLevel} is not in 0-1";
        }
        foreach (var (kind, g) in f.Gear)
        {
            string at = $"items.json gear {kind}";
            if (g.BaseWeightKg <= 0) yield return $"{at}: baseWeightKg {g.BaseWeightKg}";
            if (g.MaxDurability is 0 or < -1) yield return $"{at}: maxDurability {g.MaxDurability} (-1 never wears out)";
            if ((g.Category == GearCategory.Clothing) != (g.Slot != null)) yield return $"{at}: clothing, and only clothing, has a slot";
            if (g.Category == GearCategory.Tool && g.Tool == null && g.Weapon == null) yield return $"{at}: a tool needs its tool type, unless it is only a weapon";
            if (g.Category != GearCategory.Tool && (g.Tool != null || g.Weapon != null)) yield return $"{at}: only a tool is a tool or a weapon";
            if ((g.Category == GearCategory.Accessory) != (g.CapacityBonusKg > 0)) yield return $"{at}: accessories, and only accessories, add capacity";
            foreach (var (name, v) in new[] { ("insulation", g.Insulation), ("cushioning", g.Cushioning), ("toughness", g.Toughness), ("waterproofLevel", g.WaterproofLevel) })
                if (v is < 0 or > 1) yield return $"{at}: {name} {v} is not in 0-1";
            if (g.CapacityBonusKg < 0) yield return $"{at}: capacityBonusKg {g.CapacityBonusKg}";
            if (g.Weapon is { } w && (w.Damage <= 0 || w.BlockChance is < 0 or > 1 || w.ReachM <= 0))
                yield return $"{at}: weapon damage {w.Damage} (above 0), blockChance {w.BlockChance}, reachM {w.ReachM}";
            if (g.Weapon?.Thrown is { } t && (t.RangeM <= 0 || t.Accuracy is <= 0 or > 1 || t.Damage <= 0))
                yield return $"{at}: thrown rangeM {t.RangeM}, accuracy {t.Accuracy} and damage {t.Damage} (above 0)";
            if (g.Capabilities.Contains(Capability.None)) yield return $"{at}: capabilities names None (an empty list is nothing)";
            if (g.Capabilities.Distinct().Count() != g.Capabilities.Count) yield return $"{at}: a capability listed twice";
        }
        foreach (var (slot, share) in slots) if (share <= 0) yield return $"items.json: slotCoverage {slot} {share}";
        if (Math.Abs(slots.Values.Sum() - 1) > 1e-9) yield return $"items.json: slotCoverage sums to {slots.Values.Sum()}, not 1";
        foreach (var (r, pieces) in packKg)
            if (pieces.Count == 0 || pieces.Any(kg => kg <= 0)) yield return $"items.json startingPack: {r} needs pieces of more than 0 kg";
        foreach (var k in f.StartingPack.Gear.Concat(f.StartingClothes))
        {
            if (!f.Gear.TryGetValue(k.Kind, out var g)) { yield return $"items.json: no gear kind '{k.Kind}' for the starting kit"; continue; }
            if (k.Durability is <= 0) yield return $"items.json: starting {k.Kind} durability {k.Durability}";
        }
        foreach (var k in f.StartingClothes)
            if (f.Gear.TryGetValue(k.Kind, out var g) && g.Slot == null) yield return $"items.json: starting clothes {k.Kind} is not clothing";
        if (f.StartingClothes.GroupBy(k => f.Gear.GetValueOrDefault(k.Kind)?.Slot).Any(s => s.Key != null && s.Count() > 1))
            yield return "items.json: two starting clothes on one slot";
    }

    static IEnumerable<string> FireProblems(Dictionary<Resource, FuelProps> fuels, Dictionary<PitType, PitProps> pits, DeterrenceProps deterrence)
    {
        if (deterrence.FireFear < 0 || deterrence.TorchIntensityPct is < 0 or > 1 || deterrence.FullIntensityHeatF <= 0 || deterrence.FullWithinM <= 0)
            yield return "fire.json deterrence: out of range";
        foreach (var (r, p) in fuels)
        {
            if (p.PeakTempF <= 0 || p.BurnRateKgPerHour <= 0 || p.MinFireTempF < 0) yield return $"fire.json fuels {r}: temperatures and burn rate";
            if (p.MinFireTempF >= p.PeakTempF) yield return $"fire.json fuels {r}: minFireTempF {p.MinFireTempF} is not below peakTempF {p.PeakTempF}";
            if (p.IgnitionBonusPct is < 0 or > 1) yield return $"fire.json fuels {r}: ignitionBonusPct {p.IgnitionBonusPct}";
        }
        foreach (var (pit, p) in pits)
            if (p.MaxFuelKg <= 0 || p.WindProtectionLevel is < 0 or > 1 || p.FuelEfficiencyFactor <= 0) yield return $"fire.json pits {pit}: out of range";
    }

    static string? RunningProblem(double topSpeedMps, double enduranceSpeedMps) =>
        enduranceSpeedMps <= 0 || topSpeedMps <= enduranceSpeedMps ? "a top speed above an endurance speed above 0" : null;

    /// Autumn fat is drawn over a build's lean mass: above the build's own fat, and leaving room for its muscle.
    static string? FatRangeProblem(FatRange fat, double fatPct, double musclePct) =>
        fat.Min < fatPct || fat.Min > fat.Max || fat.Max + musclePct * (1 - fat.Max) / (1 - fatPct) >= 1
            ? "a range from the build's fat up, min not above max, that leaves room for the muscle" : null;

    /// species.json killChances: a predator's row for each count of hunters, 1 to Fauna.MaxHunters, each a chance 0-1 for
    /// each herd size 1 to Combat.KillChanceHerds.
    static IEnumerable<string> KillChanceProblems(Dictionary<Species, IReadOnlyDictionary<Species, IReadOnlyList<IReadOnlyList<double>>>> table)
    {
        foreach (var (predator, quarries) in table)
            foreach (var (quarry, rows) in quarries)
                if (rows.Count != Fauna.MaxHunters || rows.Any(r => r.Count != Combat.KillChanceHerds || r.Any(p => p is < 0 or > 1)))
                    yield return $"species.json killChances {predator} {quarry}: {Fauna.MaxHunters} rows of {Combat.KillChanceHerds} chances 0-1";
    }

    static IEnumerable<string> SpeciesProblems(SpeciesFile f, Dictionary<Species, SpeciesRow> rows)
    {
        foreach (var (s, r) in rows)
        {
            string at = $"species.json {s}";
            if (!f.Temperaments.ContainsKey(r.Temperament)) yield return $"{at}: no temperament '{r.Temperament}'";
            if (r.WeightKg <= 0 || r.FatPct <= 0 || r.MusclePct <= 0 || r.FatPct + r.MusclePct >= 1) yield return $"{at}: build";
            if (r.MoveAllowanceM <= 0) yield return $"{at}: moveAllowanceM {r.MoveAllowanceM}";
            if (r.ReachM <= 0) yield return $"{at}: reachM {r.ReachM}";
            if (r.AttackDamage <= 0 || r.Threat <= 0 || r.Boldness < 0 || r.Aggression is < 0 or > 1 || r.Cohesion < 0)
                yield return $"{at}: attackDamage and threat above 0, boldness and cohesion not below, aggression in 0-1";
            if (HerdBehaviorProblem(r) is string bad) yield return $"{at}: {bad}";
            if (RunningProblem(r.TopSpeedMps, r.EnduranceSpeedMps) is string slow) yield return $"{at}: {slow}";
            if (r.StrikeBackResolve is < -1 or > 0) yield return $"{at}: strikeBackResolve in -1 to 0 (a giving-ground resolve)";
            if (r.ChaseLimitSeconds <= 0) yield return $"{at}: chaseLimitSeconds above 0";
            if (r.SmellFactor <= 0 || r.ScentLevel is < 0 or > 1) yield return $"{at}: smellFactor above 0, scentLevel in 0-1";
            if (r.SprintSeconds <= 0 || r.RunCostJPerKgM <= 0 || r.BurstPct is <= 0 or > 1 || r.GrazeKcalPerMinute < 0)
                yield return $"{at}: sprintSeconds and runCostJPerKgM above 0, burstPct in 0-1, grazeKcalPerMinute not below 0";
            bool person = s == Survival.Species.Human;
            if (person != (r.AutumnFatPct != null) || person != (r.Female != null))
                yield return $"{at}: autumnFatPct and female are a person's, and a person has both";
            if (r.AutumnFatPct is FatRange fat && FatRangeProblem(fat, r.FatPct, r.MusclePct) is string lean) yield return $"{at} autumnFatPct: {lean}";
            if (r.Female is PersonBuild woman)
            {
                if (woman.WeightKg <= 0 || woman.FatPct <= 0 || woman.MusclePct <= 0 || woman.FatPct + woman.MusclePct >= 1) yield return $"{at} female: build";
                if (RunningProblem(woman.TopSpeedMps, woman.EnduranceSpeedMps) is string fslow) yield return $"{at} female: {fslow}";
                if (FatRangeProblem(woman.AutumnFatPct, woman.FatPct, woman.MusclePct) is string flean) yield return $"{at} female autumnFatPct: {flean}";
            }
            if ((r.Role == Role.Human) != (s == Survival.Species.Human) || (r.Body == BodyKind.Human) != (s == Survival.Species.Human))
                yield return $"{at}: only a human has the human role and body";
        }
        foreach (var (name, t) in f.Temperaments)
        {
            if (t.Cap is < 0 or > 1 || t.HungryAt > t.StarvingAt || t.HungryAt <= 0) yield return $"species.json temperament {name}: out of range";
            if (new[] { t.Base, t.PerMember, t.HungerBonus, t.DefendBonus }.Any(v => v < 0)) yield return $"species.json temperament {name}: a negative bonus";
            if (!rows.Values.Any(r => r.Temperament == name)) yield return $"species.json temperament {name}: no species has it";
        }
        foreach (var (name, pool) in new[] { ("grazing", f.Sightings.Grazing), ("resting", f.Sightings.Resting), ("wounded", f.Sightings.Wounded),
            ("alert", f.Sightings.Alert), ("moving", f.Sightings.Moving) })
            if (pool.Count == 0 || pool.Any(x => x <= 0)) yield return $"species.json sightings {name}: needs detection factors above 0";
    }

    /// A herd lives as its role meets a threat: prey graze and flee, predators hunt in a pack or alone,
    /// big scavengers scavenge, and small ones (ravens, foxes) are opportunists. People and hares
    /// never form herds of their own and keep the default.
    static string? HerdBehaviorProblem(SpeciesRow r)
    {
        bool agrees = r.Role == Role.Scavenger && r.IsSmall ? r.Behavior == HerdBehavior.Opportunist // a raven or a fox
            : r.Role == Role.Human || r.IsSmall ? r.Behavior == HerdBehavior.Prey
            : r.Role switch
            {
                Role.Prey or Role.DangerousPrey => r.Behavior == HerdBehavior.Prey,
                Role.Predator => r.Behavior is HerdBehavior.Pack or HerdBehavior.Solitary,
                Role.Scavenger => r.Behavior == HerdBehavior.Scavenger,
                _ => false,
            };
        return agrees ? null : $"role {r.Role} does not live as a {r.Behavior} herd";
    }

    static IEnumerable<string> EffectProblems(Dictionary<EffectKind, EffectRow> rows, Dictionary<EffectKind, Dictionary<Capacity, double>> caps)
    {
        foreach (var (k, e) in rows)
        {
            if (e.TreatmentAboveSeverity is < 0 or >= 1) yield return $"effects.json {k}: treatmentAboveSeverity {e.TreatmentAboveSeverity} is not in 0-1";
            // Every effect fades or holds on its own; whatever makes one worse is the cause's to apply.
            if (e.SeverityPerHour > 0) yield return $"effects.json {k}: severityPerHour {e.SeverityPerHour} grows by itself";
            if (e.BleedMlPerHour < 0) yield return $"effects.json {k}: bleedMlPerHour {e.BleedMlPerHour}";
            foreach (var (c, d) in caps[k]) if (d is < -1 or > 0) yield return $"effects.json {k}: {c} {d} is not a loss in 0-1";
            if (e.MetabolicHeatDelta is < -1 or > 1) yield return $"effects.json {k}: metabolicHeatDelta {e.MetabolicHeatDelta} is not in -1..1";
            if (e.HealingDelta is < -1 or > 1) yield return $"effects.json {k}: healingDelta {e.HealingDelta} is not in -1..1";
            if (k == EffectKind.WellFed ? e.HungerFreeHours is not (> 0 and < double.PositiveInfinity) : e.HungerFreeHours != null)
                yield return $"effects.json {k}: hungerFreeHours {e.HungerFreeHours?.ToString() ?? "missing"}: only WellFed has it, a positive number of hours";
        }
    }

    static IEnumerable<string> ShelterProblems(SheltersFile f, IReadOnlyDictionary<string, GearDef> gear, Dictionary<ShelterKind, ShelterKindProps> kinds,
        Dictionary<Resource, ShelterMaterial> materials, Dictionary<BeddingKind, BeddingProps> beddings)
    {
        foreach (var (k, p) in kinds)
            foreach (var (stat, level, cap) in new[] { ("insulation", p.Insulation, p.InsulationCap), ("overhead", p.Overhead, p.OverheadCap), ("wind", p.Wind, p.WindCap) })
            {
                if (level is < 0 or > 1 || cap is < 0 or > 1) yield return $"shelters.json kind {k}: {stat} {level} (cap {cap}) is not in 0-1";
                if (level > cap) yield return $"shelters.json kind {k}: {stat} {level} is above its cap {cap}";
            }
        foreach (var (kind, t) in f.Tents)
        {
            if (!gear.TryGetValue(kind, out var g) || g.Tool != ToolType.Tent) { yield return $"shelters.json tents: no tent gear kind '{kind}'"; continue; }
            if (ShelterDefProblem(t, kinds) is string bad) yield return $"shelters.json tents {kind}: {bad}";
        }
        foreach (var (kind, g) in gear)
            if (g.Tool == ToolType.Tent && !f.Tents.ContainsKey(kind)) yield return $"shelters.json tents: no row for the tent {kind}";
        foreach (var (r, m) in materials)
            if (m.PieceKg <= 0 || new[] { m.Insulating, m.Waterproof, m.Structural }.Any(v => v is < 0 or > 1)) yield return $"shelters.json materials {r}: out of range";
        foreach (var (b, p) in beddings)
            if (p.QualityPct is <= 0 or > 1 || p.WarmthBonusF < 0) yield return $"shelters.json beddings {b}: out of range";
    }

    /// What a placed shelter definition must be: levels and caps fractions, a level never above its cap.
    static string? ShelterDefProblem(ShelterDef d, IReadOnlyDictionary<ShelterKind, ShelterKindProps> kinds)
    {
        var k = kinds[d.Kind];
        foreach (var (stat, level, cap) in new[] { ("insulation", d.Insulation ?? k.Insulation, d.InsulationCap ?? k.InsulationCap),
            ("overhead", d.Overhead ?? k.Overhead, d.OverheadCap ?? k.OverheadCap), ("wind", d.Wind ?? k.Wind, d.WindCap ?? k.WindCap) })
        {
            if (level is < 0 or > 1 || cap is < 0 or > 1) return $"{stat} {level} (cap {cap}) is not in 0-1";
            if (level > cap) return $"{stat} {level} is above its cap {cap}";
        }
        return null;
    }

    static IEnumerable<string> ButcherProblems(Dictionary<ButcherMode, ButcherFile> rows, Dictionary<ButcherMode, Dictionary<Resource, double>> yields)
    {
        foreach (var (m, r) in rows)
        {
            if (r.TimeFactor <= 0 || r.ScentIncrease < 0 || r.BloodySeverity is < 0 or > 1) yield return $"butchering.json {m}: out of range";
            if (yields[m].Values.Any(v => v < 0)) yield return $"butchering.json {m}: a negative yield factor";
        }
        // Every mode recovers a share of what full processing does, never more than was cut.
        if (yields.TryGetValue(ButcherMode.FullProcessing, out var full))
            foreach (var (m, y) in yields)
                foreach (var r in y.Keys.Union(full.Keys))
                    if (y.GetValueOrDefault(r, 1) > full.GetValueOrDefault(r, 1)) yield return $"butchering.json {m}: {r} above full processing's";
    }

    static IEnumerable<string> TerrainProblems(TerrainFile f, Dictionary<Terrain, TerrainRow> terrains, Dictionary<Cave, CaveRow> caves,
        IReadOnlyDictionary<string, IReadOnlyList<ForageItem>> forage)
    {
        foreach (var (t, r) in terrains)
        {
            string at = $"terrain.json terrains {t}";
            if (!f.Substrates.ContainsKey(r.Substrate)) yield return $"{at}: no substrate '{r.Substrate}'";
            if (Ground(r.Hazard, r.Wind, r.Cover, r.Visibility) is string bad) yield return $"{at}: {bad}";
            if (r.Passable)
            {
                if (r.TraversalMinutes is not > 0) yield return $"{at}: passable ground needs traversalMinutes";
                if (r.Forage == null) yield return $"{at}: passable ground needs its forage";
            }
            else if (r.TraversalMinutes != null || r.Forage != null || r.Trees != null) yield return $"{at}: a wall has no traversal, forage or trees";
            if (r.Forage != null && ForageRefProblem(r.Forage, forage) is string fb) yield return $"{at}: {fb}";
            if (r.Trees is { MinutesToFell: <= 0 }) yield return $"{at}: trees need minutesToFell";
        }
        foreach (var (e, r) in f.Edges)
            if (r.Blocking && r.TraversalMinutes != 0) yield return $"terrain.json edges {e}: a blocking edge adds no minutes";
        foreach (var c in new[] { Cave.Entrance, Cave.Floor }) if (!caves.ContainsKey(c)) yield return $"terrain.json caves: no row for {c}";
        if (f.Caves.ContainsKey(nameof(Cave.None))) yield return "terrain.json caves: None is not a cave";
        foreach (var (c, r) in caves)
        {
            if (Ground(r.Hazard, r.Wind, r.Cover, r.Visibility) is string bad) yield return $"terrain.json caves {c}: {bad}";
            if (ForageRefProblem(r.Forage, forage) is string fb) yield return $"terrain.json caves {c}: {fb}";
        }
        foreach (var (name, s) in f.Substrates)
            if (s.ThicknessM <= 0 || new[] { s.PoreCapacityPct, s.RetentionPct, s.InitialSaturationPct, s.SoftnessLevel }.Any(v => v is < 0 or > 1)
                || new[] { s.PermeabilityMPerMinute, s.DrainagePerMinute, s.RunoffPerMinute }.Any(v => v < 0))
                yield return $"terrain.json substrates {name}: out of range";
        foreach (var name in CodeSubstrates) if (!f.Substrates.ContainsKey(name)) yield return $"terrain.json: no substrate '{name}', which the code names";
        // The ice lies across the turn of the year, so it forms after it breaks up.
        if (f.WaterIce is not { ThawDay: >= 1 and <= 365, FreezeDay: >= 1 and <= 365 } || f.WaterIce.ThawDay >= f.WaterIce.FreezeDay)
            yield return "terrain.json waterIce: thawDay and freezeDay are days of the year, thawDay first";
        if (f.Footing is not { InjuryExponent: > 0, InjuryDivisor: >= 1, CarefulRiskFraction: >= 0 and <= 1, PaceMattersAbovePct: >= 0 and < 1 })
            yield return "terrain.json footing: out of range";
    }

    /// A named place may be windier than any terrain (the Wind Gap funnels it).
    /// Every wood a tree can be has a log weight and burns; a mixed stand's shares are fractions that make
    /// a whole; every count is [min, max] and every weight above 0.
    static IEnumerable<string> FellingProblems(FellingFile f, Dictionary<Resource, double> logKg, Dictionary<Resource, double> mixedWood,
        Dictionary<Resource, FuelProps> fuels, IReadOnlyList<LocationDef> locations)
    {
        const string at = "terrain.json felling";
        foreach (var (name, p, weighs) in new[] { ("logs", f.Logs, false), ("kindling", f.Kindling, true), ("tinder", f.Tinder, true) })
        {
            if (p.Count is not [>= 0 and var lo, var hi] || hi < lo || hi == 0) yield return $"{at} {name}: count is [min, max]";
            if (weighs != (p.Kg != null)) yield return $"{at} {name}: {(weighs ? "needs" : "has no")} kg";
            if (p.Kg != null && (p.Kg is not [> 0 and var klo, var khi] || khi < klo)) yield return $"{at} {name}: kg is [min, max] above 0";
        }
        if (f.LogKgSpreadPct is < 0 or >= 1) yield return $"{at}: logKgSpreadPct {f.LogKgSpreadPct} is not in 0-1";
        foreach (var (r, kg) in logKg)
        {
            if (kg <= 0) yield return $"{at} logKg {r}: {kg}";
            if (!fuels.ContainsKey(r)) yield return $"{at} logKg {r}: does not burn (fire.json fuels)";
        }
        if (mixedWood.Count == 0 || mixedWood.Values.Any(s => s <= 0) || Math.Abs(mixedWood.Values.Sum() - 1) > 1e-9)
            yield return $"{at}: mixedWood shares above 0 that sum to 1";
        var woods = mixedWood.Keys.Concat(locations.SelectMany(l => l.All.OfType<SiteTrees>()).Select(t => t.Wood).OfType<Resource>());
        foreach (var r in woods.Distinct()) if (!logKg.ContainsKey(r)) yield return $"{at}: no logKg for {r}, which trees can be";
    }

    static string? Ground(double hazard, double wind, double cover, double visibility, double maxWind = 2) =>
        hazard is < 0 or > 1 ? $"hazard {hazard} is not in 0-1"
        : wind < 0 || wind > maxWind ? $"wind {wind} is not in 0-{maxWind}"
        : cover is < 0 or > 1 ? $"cover {cover} is not in 0-1"
        : visibility is < 0 or > 2 ? $"visibility {visibility} is not in 0-2" : null;

    static string? ForageRefProblem(ForageRef r, IReadOnlyDictionary<string, IReadOnlyList<ForageItem>> forage) =>
        !forage.ContainsKey(r.Table) ? $"no forage table '{r.Table}'" : r.Density <= 0 ? $"forage density {r.Density}" : null;

    /// Everything a carcass can give (Carcasses.Create and the special yields).
    static readonly Resource[] CarcassYields =
        [Resource.RawMeat, Resource.RawFat, Resource.Bone, Resource.Sinew, Resource.Hide, Resource.Feather, Resource.Ivory, Resource.MammothHide];

    /// Every table that yields a discrete resource yields whole pieces inside its kind's range.
    static IEnumerable<string> PieceProblems(IReadOnlyDictionary<Resource, ResourceDef> resources, IReadOnlyDictionary<string, IReadOnlyList<ForageItem>> forage,
        IReadOnlyList<LocationDef> locations, DiscoveriesFile discoveries, IReadOnlyDictionary<string, IReadOnlyList<LootRoll>> loot, FellingFile felling,
        Dictionary<Resource, double> logKg, Dictionary<Resource, IReadOnlyList<double>> packKg)
    {
        IEnumerable<string> Range(string at, Resource r, double lo, double hi) =>
            resources[r] is { Discrete: true } d && (lo < d.MinPieceKg - 1e-9 || hi > d.MaxPieceKg + 1e-9)
                ? [$"{at}: a piece of {r} at {lo}-{hi} kg is outside its range, {d.MinPieceKg}-{d.MaxPieceKg} kg (items.json)"] : [];
        foreach (var (table, rows) in forage)
            foreach (var i in rows) foreach (var p in Range($"forage.json {table}", i.Resource, i.MinKg, i.MaxKg)) yield return p;
        foreach (var l in locations)
            foreach (var f in l.All)
                switch (f)
                {
                    case SiteForage fo: foreach (var i in fo.Items) foreach (var p in Range($"locations.json {l.Id} forage", i.Resource, i.MinKg, i.MaxKg)) yield return p; break;
                    case SiteStock { Items: { } stock }: foreach (var i in stock) foreach (var p in Range($"locations.json {l.Id} {f.GetType().Name}", i.Resource, i.KgPerUnit, i.KgPerUnit)) yield return p; break;
                }
        IEnumerable<FindItem> Flat(IEnumerable<FindItem> items) => items.SelectMany(i => i.Shuffle is { } s ? Flat(s) : [i]);
        foreach (var (name, d) in discoveries.Finds.Concat(discoveries.Details))
            foreach (var i in Flat(d.Items ?? []))
                foreach (var r in i.AnyOf ?? (i.Resource is Resource one ? [one] : []))
                    foreach (var p in Range($"discoveries.json {name}", r, i.KgPerUnit, i.KgPerUnitMax ?? i.KgPerUnit)) yield return p;
        IEnumerable<LootRoll> Rolls(IEnumerable<LootRoll> rolls) => rolls.SelectMany(x => x.Of is { } of ? Rolls(of).Prepend(x) : [x]);
        foreach (var (name, rolls) in loot)
            foreach (var x in Rolls(rolls))
                if (x.Resource is Resource r && x.Kg is [var lo, var hi]) foreach (var p in Range($"loot.json {name}", r, lo, hi)) yield return p;
        foreach (var (wood, kg) in logKg) foreach (var p in Range("terrain.json felling logKg", wood, kg * (1 - felling.LogKgSpreadPct), kg * (1 + felling.LogKgSpreadPct))) yield return p;
        foreach (var (r, pieces) in new[] { (Resource.Kindling, felling.Kindling), (Resource.Tinder, felling.Tinder) })
            if (pieces.Kg is [var lo, var hi]) foreach (var p in Range($"terrain.json felling {r}", r, lo, hi)) yield return p;
        foreach (var (r, pieces) in packKg)
            foreach (var kg in pieces) foreach (var p in Range("items.json startingPack", r, kg, kg)) yield return p;
        // A carcass gives its last pieces evenly (Carcasses.TakeReady): that needs a range at least two-fold.
        foreach (var r in CarcassYields)
            if (resources[r] is { Discrete: true } d && d.MinPieceKg > d.MaxPieceKg / 2)
                yield return $"items.json: {r} comes off a carcass, and its pieces need to run from at most half the largest";
    }

    static IEnumerable<string> ForageProblems(IReadOnlyDictionary<string, IReadOnlyList<ForageItem>> forage)
    {
        foreach (var (name, items) in forage)
        {
            if (items.Count == 0) yield return $"forage.json {name}: no items";
            if (items.Any(i => i.MinKg <= 0 || i.MinKg > i.MaxKg || i.Abundance <= 0)) yield return $"forage.json {name}: a bad item";
        }
        foreach (var name in CodeForage) if (!forage.ContainsKey(name)) yield return $"forage.json: no table '{name}', which the code names";
    }

    static IEnumerable<string> DiscoveryProblems(DiscoveriesFile f, Dictionary<Terrain, IReadOnlyList<DiscoveryDraw>> draws,
        Dictionary<Terrain, IReadOnlyList<string>> detailDraws, Dictionary<Terrain, TerrainRow> terrains, IReadOnlyDictionary<string, GearDef> gear,
        IReadOnlyDictionary<string, IReadOnlyList<LootRoll>> loot, IReadOnlyList<LocationDef> locations)
    {
        foreach (var (id, d) in f.Finds) foreach (var p in FindProblems(d, gear, loot, detail: false)) yield return $"discoveries.json finds {id}: {p}";
        foreach (var (id, d) in f.Details) foreach (var p in FindProblems(d, gear, loot, detail: true)) yield return $"discoveries.json details {id}: {p}";
        foreach (var l in locations)
            foreach (var find in l.All.OfType<SiteFind>())
                if (!f.Finds.ContainsKey(find.Find)) yield return $"locations.json {l.Id}: no find '{find.Find}' in discoveries.json";
        foreach (var (t, row) in terrains)
        {
            if (row.Passable != draws.ContainsKey(t)) yield return $"discoveries.json terrains: {t} {(row.Passable ? "needs" : "is a wall and has no")} draws";
            if (row.Passable != detailDraws.ContainsKey(t)) yield return $"discoveries.json detailDraws: {t} {(row.Passable ? "needs" : "is a wall and has no")} details";
        }
        foreach (var (where, list) in draws.Select(d => ($"terrains {d.Key}", d.Value)).Append(("cave", f.Cave)))
            foreach (var d in list)
            {
                if ((d.Find != null) == (d.WaitsFor != null)) yield return $"discoveries.json {where}: a draw is a find or waitsFor, one of them";
                if (d.Find != null && !f.Finds.ContainsKey(d.Find)) yield return $"discoveries.json {where}: no find '{d.Find}'";
                if (d.SpawnChance is <= 0 or > 1 || d.ExpectedHours <= 0) yield return $"discoveries.json {where}: {d.Find ?? d.WaitsFor} chance or hours";
            }
        foreach (var (t, ids) in detailDraws)
            foreach (var id in ids)
                if (!f.Details.ContainsKey(id) && !f.ExamineOnly.Contains(id)) yield return $"discoveries.json detailDraws {t}: no detail '{id}'";
        foreach (var id in f.ExamineOnly) if (f.Details.ContainsKey(id)) yield return $"discoveries.json: {id} is examine-only and has things to take";
    }

    static IEnumerable<string> FindProblems(FindDef d, IReadOnlyDictionary<string, GearDef> gear,
        IReadOnlyDictionary<string, IReadOnlyList<LootRoll>> loot, bool detail)
    {
        if (d.Found is not { Count: > 0 } || d.Found.Any(string.IsNullOrWhiteSpace)) yield return "no found lines";
        int shapes = (d.Work != null ? 1 : 0) + (d.Shelter != null ? 1 : 0) + (d.Carcass != null ? 1 : 0) + (d.Pile ? 1 : 0);
        if (shapes != 1) { yield return "a find is a stock (work), a shelter, a carcass or a pile, one of them"; yield break; }
        if (d.Loot != null && !loot.ContainsKey(d.Loot)) yield return $"no loot table '{d.Loot}'";
        if (d.Outcomes != null)
        {
            if (detail) yield return "a detail has no outcomes";
            if (!d.Pile && d.Work != StockKind.Salvage) yield return "only a salvage or a pile has outcomes";
            if (d.Loot != null || d.Items != null) yield return "outcomes stand in place of loot and items";
            if (d.Pile != (d.Minutes == 0)) yield return d.Pile ? "a pile has no minutes" : $"minutes {d.Minutes}";
            if (d.Outcomes.Count == 0) yield return "no outcomes";
            foreach (var o in d.Outcomes)
            {
                if (o.Weight <= 0 || o.Scale <= 0 || string.IsNullOrWhiteSpace(o.Line)) yield return $"outcome '{o.Line}': a weight, a scale and a line";
                foreach (var t in o.Loot) if (!loot.ContainsKey(t)) yield return $"outcome '{o.Line}': no loot table '{t}'";
            }
            yield break;
        }
        if (d.Carcass is { } c)
        {
            if (detail) yield return "a detail is not a carcass";
            if (c.Animals.Count == 0 || c.Animals.Any(a => a.Weight <= 0)) yield return "a carcass needs animals, each with a weight";
            if (c.HarvestedPct is not [var lo, var hi] || lo < 0 || hi < lo || hi >= 1) yield return "harvestedPct is [min, max] in 0-1";
            if (d.Items != null || d.Minutes != 0 || d.Loot != null) yield return "a carcass has no items, minutes or loot";
            yield break;
        }
        if (d.Pile)
        {
            if (detail) yield return "a detail is not a pile";
            if (d.Loot == null || d.Items != null || d.Minutes != 0) yield return "a pile holds a loot table, with no items or minutes";
            yield break;
        }
        if (d.Shelter != null)
        {
            if (detail) yield return "a detail is not a shelter";
            if (d.Items != null || d.Minutes != 0) yield return "a shelter has no items or minutes";
            if (new[] { d.InsulationSpread, d.OverheadSpread, d.WindSpread }.Any(s => s < 0)) yield return "a negative spread";
            if ((d.Insulation == null && d.InsulationSpread > 0) || (d.Overhead == null && d.OverheadSpread > 0) || (d.Wind == null && d.WindSpread > 0))
                yield return "a spread needs its level";
            // Levels at their lowest and highest rolls, and caps, are fractions; caps rise to meet levels.
            foreach (var v in new[] { d.Insulation, d.Insulation + d.InsulationSpread, d.Overhead, d.Overhead + d.OverheadSpread,
                d.Wind, d.Wind + d.WindSpread, d.InsulationCap, d.OverheadCap, d.WindCap })
                if (v is < 0 or > 1) { yield return $"a level or cap {v} is not in 0-1"; break; }
            yield break;
        }
        if (detail != (d.Work == StockKind.Detail)) yield return detail ? "a detail's work is Detail" : "a find's work is Harvest or Salvage";
        if (d.Minutes <= 0) yield return $"minutes {d.Minutes}";
        if (d.Loot != null)
        {
            if (d.Work != StockKind.Salvage) yield return "only a salvage is rolled from a loot table";
            if (d.Items != null) yield return "a find has items or a loot table, not both";
            yield break;
        }
        if (d.Items is not { Count: > 0 }) { yield return "no items"; yield break; }
        if (new[] { d.Insulation, d.Overhead, d.Wind, d.InsulationCap, d.OverheadCap, d.WindCap }.Any(v => v != null)) yield return "a stock has no shelter levels";
        foreach (var p in ItemProblems(d.Items, d.Work!.Value, gear)) yield return p;
    }

    static IEnumerable<string> ItemProblems(IReadOnlyList<FindItem> items, StockKind work, IReadOnlyDictionary<string, GearDef> gear)
    {
        foreach (var i in items)
        {
            int shapes = (i.Resource != null ? 1 : 0) + (i.AnyOf != null ? 1 : 0) + (i.Gear != null ? 1 : 0) + (i.Shuffle != null ? 1 : 0);
            if (shapes != 1) { yield return "an item is one of resource, anyOf, gear or shuffle"; continue; }
            if (i.Chance is <= 0 or > 1) yield return $"chance {i.Chance}";
            if (i.Gear != null)
            {
                if (!gear.ContainsKey(i.Gear)) yield return $"no gear kind '{i.Gear}'";
                if (work != StockKind.Salvage) yield return "only salvage holds gear";
                continue;
            }
            if (i.Shuffle != null)
            {
                if (i.Take <= 0 || i.TakeMax < i.Take || (i.TakeMax ?? i.Take) > i.Shuffle.Count) yield return "take [take, takeMax] of the shuffled";
                foreach (var p in ItemProblems(i.Shuffle, work, gear)) yield return p;
                continue;
            }
            if (i.AnyOf is { Count: 0 }) yield return "anyOf names none";
            if (i.Units <= 0 || i.UnitsMax < i.Units) yield return "units [units, unitsMax]";
            if (i.KgPerUnit <= 0 || i.KgPerUnitMax < i.KgPerUnit) yield return "kgPerUnit [kgPerUnit, kgPerUnitMax]";
            if (work == StockKind.Harvest && i.KgPerUnitMax != null) yield return "a harvest's units each weigh kgPerUnit";
            if (work == StockKind.Harvest && i.RespawnHoursPerUnit == null) yield return "a harvest's item needs respawnHoursPerUnit (0 never regrows)";
            if (work != StockKind.Harvest && i.RespawnHoursPerUnit != null) yield return "only a harvest regrows";
            if (i.RespawnHoursPerUnit < 0) yield return "respawnHoursPerUnit";
            if (i.Take != 0 || i.TakeMax != null) yield return "take is for shuffle";
        }
    }

    static IEnumerable<string> AccidentProblems(IReadOnlyDictionary<string, IReadOnlyList<Accident>> accidents)
    {
        foreach (var (name, pool) in accidents)
        {
            if (pool.Count == 0) yield return $"accidents.json {name}: no accidents";
            foreach (var a in pool)
            {
                if (a.Amount <= 0) yield return $"accidents.json {name}: amount {a.Amount}";
                if ((a.Effect != null) != (a.Severity > 0) || a.Severity > 1) yield return $"accidents.json {name}: an effect, and only an effect, has a severity in 0-1";
            }
        }
        foreach (var name in CodeAccidents) if (!accidents.ContainsKey(name)) yield return $"accidents.json: no pool '{name}', which the code names";
    }

    /// Every row is used: a table, find, detail, pool or loot table nothing names is a typo'd name
    /// somewhere else, or content that silently never appears.
    static IEnumerable<string> DeadRows(IReadOnlyList<LocationDef> locations, IReadOnlyDictionary<string, IReadOnlyList<LootRoll>> loot,
        TerrainFile terrain, IReadOnlyDictionary<string, IReadOnlyList<ForageItem>> forage, DiscoveriesFile discoveries,
        IReadOnlyDictionary<string, IReadOnlyList<Accident>> accidents)
    {
        var forageUsed = terrain.Terrains.Values.Select(t => t.Forage?.Table).Concat(terrain.Caves.Values.Select(c => c.Forage.Table)).Concat(CodeForage).ToHashSet();
        foreach (var name in forage.Keys) if (!forageUsed.Contains(name)) yield return $"forage.json {name}: no terrain or cave forages it";
        var findsUsed = discoveries.Terrains.Values.SelectMany(d => d).Concat(discoveries.Cave).Select(d => d.Find)
            .Concat(locations.SelectMany(l => l.All.OfType<SiteFind>()).Select(s => s.Find)).ToHashSet();
        foreach (var id in discoveries.Finds.Keys) if (!findsUsed.Contains(id)) yield return $"discoveries.json finds {id}: no draw finds it";
        var detailsUsed = discoveries.DetailDraws.Values.SelectMany(d => d).ToHashSet();
        foreach (var id in discoveries.Details.Keys.Concat(discoveries.ExamineOnly))
            if (!detailsUsed.Contains(id)) yield return $"discoveries.json details {id}: no terrain draws it";
        foreach (var name in accidents.Keys) if (!CodeAccidents.Contains(name)) yield return $"accidents.json {name}: no crossing draws from it";
        var lootUsed = locations.SelectMany(l => l.All.OfType<SiteStock>()).Select(s => s.Loot)
            .Concat(discoveries.Finds.Values.Select(f => f.Loot))
            .Concat(discoveries.Finds.Values.SelectMany(f => f.Outcomes ?? []).SelectMany(o => o.Loot)).ToHashSet();
        foreach (var name in loot.Keys) if (!lootUsed.Contains(name)) yield return $"loot.json {name}: no place is salvaged with it";
    }
}
