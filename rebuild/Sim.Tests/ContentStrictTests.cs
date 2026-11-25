using System.Collections;

namespace Survival.Tests;

/// The loader takes nothing on trust: enum values exactly as spelled, every field a row needs stated,
/// the dangerous defaults written out, ranges and agreements checked, no row nothing uses, and tables
/// nobody can change once loaded.
public class ContentStrictTests
{
    static string Edit(string json, string from, string to, bool all = false)
    {
        int at = json.IndexOf(from, StringComparison.Ordinal);
        Assert.True(at >= 0, $"no '{from}' to edit");
        return all ? json.Replace(from, to, StringComparison.Ordinal) : json[..at] + to + json[(at + from.Length)..];
    }

    static string BrokenIn(string file, string from, string to)
    {
        var files = Content.EmbeddedFiles();
        files[file] = Edit(files[file], from, to);
        return Assert.Throws<InvalidDataException>(() => Content.Parse(files)).Message;
    }

    // ---- 1. an enum value inside a row is one of its names, exactly ----

    [Theory]
    // Comma lists: the stock reader ORs them together ("Prey, Predator" was Predator; "Pine, Oak" was RawFish).
    [InlineData("species.json", "\"role\": \"Predator\"", "\"role\": \"Prey, Predator\"", "no Role 'Prey, Predator'")]
    [InlineData("species.json", "\"Grazer\": [ \"Tinder\" ]", "\"Grazer\": [ \"Pine, Oak\" ]", "no Resource 'Pine, Oak'")]
    [InlineData("species.json", "\"Grazer\": [ \"Tinder\" ]", "\"Grazer\": [ \"SphagnumMoss, Usnea\" ]", "no Resource 'SphagnumMoss, Usnea'")]
    // Case: the stock reader matches names ignoring it.
    [InlineData("items.json", "\"slot\": \"Chest\"", "\"slot\": \"chest\"", "no EquipSlot 'chest'")]
    [InlineData("recipes.json", "\"of\": \"Kindling\", \"count\": 4", "\"of\": \"kindling\", \"count\": 4", "no Resource 'kindling'")]
    [InlineData("forage.json", "\"resource\": \"Usnea\"", "\"resource\": \"usnea\"", "no Resource 'usnea'")]
    [InlineData("accidents.json", "\"effect\": \"Dazed\"", "\"effect\": \"dazed\"", "no EffectKind 'dazed'")]
    [InlineData("discoveries.json", "\"work\": \"Salvage\"", "\"work\": \"salvage\"", "no StockKind 'salvage'")]
    [InlineData("locations.json", "\"placement\": \"Named\"", "\"placement\": \"named\"", "no Placement 'named'")]
    [InlineData("loot.json", "\"resource\": \"Stone\"", "\"resource\": \"stone\"", "no Resource 'stone'")]
    // Numbers, quoted or not.
    [InlineData("accidents.json", "\"type\": \"Blunt\"", "\"type\": \"1\"", "no DamageType '1'")]
    [InlineData("loot.json", "\"resource\": \"Stone\"", "\"resource\": 3", "written as its name")]
    [InlineData("discoveries.json", "\"Berries\"", "\"Berries, Pine\"", "no Resource 'Berries, Pine'")]
    public void Loader_RefusesAnEnumValue_NotSpelledAsItsName(string file, string from, string to, string named) =>
        Assert.Contains(named, BrokenIn(file, from, to));

    // ---- 1b. every resource is discrete or continuous, and every table counts it that way (#142) ----

    [Theory]
    [InlineData("items.json", "\"Stone\": { \"category\": \"Material\", \"form\": \"Discrete\", \"pieceKg\": [0.08, 1.5] }", "\"Stone\": { \"category\": \"Material\" }", "Stone is neither discrete nor continuous")]
    [InlineData("items.json", "\"Stone\": { \"category\": \"Material\", \"form\": \"Discrete\", \"pieceKg\": [0.08, 1.5] }", "\"Stone\": { \"category\": \"Material\", \"form\": \"Discrete\" }", "discrete Stone needs pieceKg")]
    [InlineData("items.json", "\"Stone\": { \"category\": \"Material\", \"form\": \"Discrete\", \"pieceKg\": [0.08, 1.5] }", "\"Stone\": { \"category\": \"Material\", \"form\": \"Discrete\", \"pieceKg\": [0.5, 0.2] }", "discrete Stone needs pieceKg")]
    [InlineData("items.json", "\"Water\": { \"category\": \"Water\", \"hydrationMlPerKg\": 1000, \"form\": \"Continuous\" }", "\"Water\": { \"category\": \"Water\", \"hydrationMlPerKg\": 1000, \"form\": \"Continuous\", \"pieceKg\": [1, 2] }", "continuous Water has no pieces")]
    [InlineData("recipes.json", "{ \"of\": \"Stone\", \"count\": 2 }", "{ \"of\": \"Stone\", \"g\": 600 }", "Stone is discrete: a count of pieces, never a weight")]
    [InlineData("recipes.json", "{ \"of\": \"Stone\", \"count\": 2 }", "{ \"of\": \"Stone\", \"count\": 2, \"kg\": 0.6 }", "Stone names kg")]
    [InlineData("recipes.json", "{ \"of\": \"PlantFiber\", \"g\": 300 }", "{ \"of\": \"PlantFiber\", \"count\": 3 }", "a discrete input, and only one, is a count")]
    [InlineData("recipes.json", "\"succeed:extra_product:HideStrip:1=.5\"", "\"succeed:extra_product:Sinew:1=.5\"", "extra_product Sinew is discrete")]
    [InlineData("recipes.json", "\"succeed:extra_product:HideStrip:1=.5\"", "\"succeed:extra_product:HideStrip:1.5=.5\"", "extra_product HideStrip is whole pieces")]
    [InlineData("recipes.json", "\"succeed:extra_product:HideStrip:1=.5\"", "\"succeed:extra_product:HideStrip:2=.5\"", "a piece of HideStrip would weigh")]
    [InlineData("recipes.json", "\"result\": { \"resource\": \"ScrapedHide\", \"count\": 1, \"sharePct\": 0.8 }", "\"result\": { \"resource\": \"ScrapedHide\", \"g\": 800 }", "a discrete result ScrapedHide is a count of pieces")]
    [InlineData("items.json", "\"baseWeightKg\": 0.1, \"maxDurability\": 1, \"capabilities\": []", "\"baseWeightKg\": 0.5, \"maxDurability\": 1, \"capabilities\": []", "torch weighs 0.5 kg, above its lightest possible inputs")]
    [InlineData("forage.json", "{ \"resource\": \"Stone\", \"abundance\": 0.3, \"minKg\": 0.2, \"maxKg\": 0.4 }", "{ \"resource\": \"Stone\", \"abundance\": 0.3, \"minKg\": 0.2, \"maxKg\": 2.4 }", "a piece of Stone at 0.2-2.4 kg is outside its range")]
    public void Loader_RefusesAResource_NotCountedAsItsForm(string file, string from, string to, string named) =>
        Assert.Contains(named, BrokenIn(file, from, to));

    // ---- 1c. a recipe worked by a station rolls nothing, takes one piece and names what drives it (#184) ----

    [Theory]
    [InlineData("recipes.json", "\"inputs\": [ { \"of\": \"RawMeat\", \"count\": 1 } ], \"tools\": [], \"requires\": [], \"minutes\": 15", "\"inputs\": [ { \"of\": \"RawMeat\", \"count\": 1 } ], \"tools\": [\"Cutting\"], \"requires\": [], \"minutes\": 15", "uses no tools")]
    [InlineData("recipes.json", "\"sharePct\": 1 },\n  \"outcomes\": [] },\n \"cook_fish\"", "\"sharePct\": 1 },\n  \"outcomes\": [\"fail=.1\"] },\n \"cook_fish\"", "has no outcome table")]
    [InlineData("recipes.json", "\"inputs\": [ { \"of\": \"RawMeat\", \"count\": 1 } ], \"tools\": [], \"requires\": [], \"minutes\": 15", "\"inputs\": [ { \"of\": \"RawMeat\", \"count\": 1 }, { \"of\": \"Stone\", \"count\": 1 } ], \"tools\": [], \"requires\": [], \"minutes\": 15", "has one input")]
    [InlineData("recipes.json", "\"inputs\": [ { \"of\": \"RawMeat\", \"count\": 1 } ], \"tools\": [], \"requires\": [], \"minutes\": 15", "\"inputs\": [ { \"of\": \"RawMeat\", \"count\": 2 } ], \"tools\": [], \"requires\": [], \"minutes\": 15", "one piece of one discrete resource")]
    [InlineData("recipes.json", "\"cook_meat\": { \"name\": \"Cook meat\", \"category\": \"Processing\", \"station\": \"Fire\", \"driver\": \"Heat\"", "\"cook_meat\": { \"name\": \"Cook meat\", \"category\": \"Processing\", \"station\": \"Fire\"", "names its driver")]
    [InlineData("recipes.json", "\"cook_meat\": { \"name\": \"Cook meat\", \"category\": \"Processing\", \"station\": \"Fire\", \"driver\": \"Heat\"", "\"cook_meat\": { \"name\": \"Cook meat\", \"category\": \"Processing\", \"driver\": \"Heat\"", "a driver with no station")]
    [InlineData("recipes.json", "\"cook_meat\": { \"name\": \"Cook meat\", \"category\": \"Processing\", \"station\": \"Fire\", \"driver\": \"Heat\"", "\"cook_meat\": { \"name\": \"Cook meat\", \"category\": \"Processing\", \"station\": \"Oven\", \"driver\": \"Heat\"", "no StationKind 'Oven'")]
    [InlineData("fire.json", "\"Mound\": { \"room\": 3, ", "\"Mound\": { ", "room")]
    [InlineData("fire.json", "\"Mound\": { \"room\": 3, ", "\"Mound\": { \"room\": 0, ", "fire.json pits Mound: out of range")]
    public void Loader_RefusesAStationRecipe_ThatIsNotOnePieceRolledNothing_AndAPitWithNoRoom(string file, string from, string to, string named) =>
        Assert.Contains(named, BrokenIn(file, from, to));

    // ---- 2. a row's fields are required, whatever kind of row it is ----

    [Theory]
    // Oak would catch on any fire at all.
    [InlineData("fire.json", "\"Oak\": { \"peakTempF\": 950, \"burnRateKgPerHour\": 0.6, \"minFireTempF\": 500, \"ignitionBonusPct\": 0 }",
        "\"Oak\": { \"peakTempF\": 950, \"burnRateKgPerHour\": 0.6 }", "minFireTempF")]
    [InlineData("fire.json", "\"minFireTempF\": 500, ", "", "minFireTempF")]
    // A fall with no target or kind of hit would land on a random part as blunt.
    [InlineData("accidents.json", "{ \"target\": \"Chest\", \"type\": \"Blunt\", \"amount\": 0.10 }", "{ \"amount\": 0.10 }", "target")]
    [InlineData("accidents.json", "{ \"target\": \"Chest\", \"type\": \"Blunt\", \"amount\": 0.10 }", "{ \"target\": \"Chest\", \"amount\": 0.10 }", "type")]
    // A species with no nose or no scent of its own would smell nothing, or never be smelled (spec "Scent").
    [InlineData("species.json", "\"smellFactor\": 2.5, \"scentLevel\": 0.3, ", "\"scentLevel\": 0.3, ", "smellFactor")]
    [InlineData("species.json", "\"smellFactor\": 2.5, \"scentLevel\": 0.3, ", "\"smellFactor\": 2.5, ", "scentLevel")]
    public void Loader_RefusesAStructRow_MissingAField(string file, string from, string to, string named) =>
        Assert.Contains(named, BrokenIn(file, from, to));

    // ---- 3. where leaving a field out would be the dangerous choice, it must be written ----

    [Theory]
    [InlineData("terrain.json", "\"Cliff\": { \"traversalMinutes\": 0, \"blocking\": true }", "\"Cliff\": { \"traversalMinutes\": 0 }", "blocking")]
    [InlineData("terrain.json", "\"softnessLevel\": 0.0, \"holdsStandingWater\": false }", "\"softnessLevel\": 0.0 }", "holdsStandingWater")]
    [InlineData("shelters.json", "\"isNatural\": false, \"isSnow\": true", "\"isNatural\": false", "isSnow")]
    [InlineData("effects.json", "\"treatmentAboveSeverity\": 0, ", "", "treatmentAboveSeverity")]
    [InlineData("items.json", "\"Water\": { \"category\": \"Water\", \"hydrationMlPerKg\": 1000, \"form\": \"Continuous\" }", "\"Water\": { \"category\": \"Water\", \"form\": \"Continuous\" }", "water Water needs its hydrationMlPerKg")]
    [InlineData("items.json", "\"maxDurability\": 3, ", "", "maxDurability")]
    [InlineData("discoveries.json", "\"kgPerUnit\": 0.4, \"respawnHoursPerUnit\": 168", "\"kgPerUnit\": 0.4", "a harvest's item needs respawnHoursPerUnit")]
    [InlineData("locations.json", "\"kgPerUnit\": 0.4, \"respawnHoursPerUnit\": 168", "\"kgPerUnit\": 0.4", "respawnHoursPerUnit")]
    [InlineData("terrain.json", ", \"trees\": { \"name\": \"Forest Trees\", \"minutesToFell\": 100 }", "", "trees")]
    // A food with no scent would draw nothing to a pile of it.
    [InlineData("items.json", "\"kcalPerKg\": 2500, \"scentLevel\": 0.5,", "\"kcalPerKg\": 2500,", "CookedMeat: food, and only food, states its scentLevel")]
    [InlineData("items.json", "\"Stone\": { \"category\": \"Material\", \"form\": \"Discrete\", \"pieceKg\": [0.08, 1.5] }", "\"Stone\": { \"category\": \"Material\", \"scentLevel\": 0.1, \"form\": \"Discrete\", \"pieceKg\": [0.08, 1.5] }", "Stone: food, and only food, states its scentLevel")]
    public void Loader_RefusesAPermissiveDefault_LeftOut(string file, string from, string to, string named) =>
        Assert.Contains(named, BrokenIn(file, from, to));

    // ---- 4. ranges, agreements, dead rows and the names code asks for ----

    [Theory]
    [InlineData("terrain.json", "\"Cliff\": { \"traversalMinutes\": 0", "\"Cliff\": { \"traversalMinutes\": 5", "a blocking edge adds no minutes")]
    [InlineData("locations.json", "\"traversal\": 0.6,", "\"traversal\": -0.6,", "traversal -0.6")]
    [InlineData("locations.json", "\"hazard\": 0.2,", "\"hazard\": 1.2,", "hazard 1.2 is not in 0-1")]
    [InlineData("effects.json", "\"bleedMlPerHour\": 3000", "\"bleedMlPerHour\": -3000", "bleedMlPerHour -3000")]
    [InlineData("effects.json", "\"Pain\": { \"severityPerHour\": -0.15", "\"Pain\": { \"severityPerHour\": 0.15", "severityPerHour 0.15 grows by itself")]
    [InlineData("effects.json", "\"Moving\": -0.35", "\"Moving\": 0.35", "Moving 0.35 is not a loss")]
    // An effect names a capacity, blood or breath, and nothing else: not the gut, not the old names.
    [InlineData("effects.json", "\"Blood\": -0.3", "\"Digestion\": -0.3", "effects.json Shock capacities: Digestion is not a capacity, blood or breath")]
    [InlineData("effects.json", "\"Blood\": -0.3", "\"BloodPumping\": -0.3", "effects.json Shock capacities: no Capacity 'BloodPumping'")]
    [InlineData("species.json", "\"aggression\": 0.8", "\"aggression\": 1.8", "aggression in 0-1")]
    [InlineData("species.json", "\"smellFactor\": 2.5, \"scentLevel\": 0.3", "\"smellFactor\": 0, \"scentLevel\": 0.3", "smellFactor above 0, scentLevel in 0-1")]
    [InlineData("species.json", "\"smellFactor\": 2.5, \"scentLevel\": 0.3", "\"smellFactor\": 2.5, \"scentLevel\": 1.3", "smellFactor above 0, scentLevel in 0-1")]
    [InlineData("items.json", "\"kcalPerKg\": 1500, \"scentLevel\": 0.6", "\"kcalPerKg\": 1500, \"scentLevel\": -0.6", "RawMeat scentLevel -0.6 is not in 0-1")]
    [InlineData("items.json", "\"damage\": 4.84, \"class\": \"Pierce\"", "\"damage\": -4.84, \"class\": \"Pierce\"", "weapon damage -4.84 (above 0)")]
    [InlineData("items.json", "\"reachM\": 1.2,", "\"reachM\": -1.2,", "reachM -1.2")]
    [InlineData("items.json", "\"accuracy\": 0.45,", "\"accuracy\": 1.45,", "accuracy 1.45")]
    [InlineData("species.json", "\"reachM\": 2.5,", "\"reachM\": -2.5,", "reachM -2.5")]
    [InlineData("locations.json", "\"animals\": [\n      { \"animal\": \"Hare\", \"weight\": 0.6 },\n      { \"animal\": \"Fox\", \"weight\": 0.3 }\n     ]", "\"animals\": []", "small game needs a density, animals")]
    // Role and herd life agree: a caribou herd does not hunt as a pack.
    [InlineData("species.json", "\"temperament\": \"never\", \"behavior\": \"Prey\", \"diet\": \"Browser\"", "\"temperament\": \"never\", \"behavior\": \"Pack\", \"diet\": \"Browser\"", "species.json Caribou: role Prey does not live as a Pack herd")]
    [InlineData("species.json", "\"role\": \"Predator\"", "\"role\": \"Scavenger\"", "species.json Wolf: role Scavenger does not live as a Pack herd")]
    public void Loader_RefusesAValueOutOfRange_OrRowsThatDisagree(string file, string from, string to, string named) =>
        Assert.Contains(named, BrokenIn(file, from, to));

    [Theory]
    [InlineData("forage.json", "\"MixedForest\": [", "\"Orphan\": [ { \"resource\": \"Stone\", \"abundance\": 1, \"minKg\": 0.1, \"maxKg\": 0.2 } ],\n \"MixedForest\": [", "forage.json Orphan: no terrain or cave forages it")]
    [InlineData("discoveries.json", "\"finds\": {", "\"finds\": {\n  \"Orphan\": { \"name\": \"Orphan\", \"work\": \"Salvage\", \"minutes\": 5, \"items\": [ { \"resource\": \"Stone\", \"kgPerUnit\": 0.2 } ] },", "discoveries.json finds Orphan: no draw finds it")]
    [InlineData("discoveries.json", "\"details\": {", "\"details\": {\n  \"orphan\": { \"name\": \"Orphan\", \"work\": \"Detail\", \"minutes\": 5, \"items\": [ { \"resource\": \"Stone\", \"kgPerUnit\": 0.2 } ] },", "discoveries.json details orphan: no terrain draws it")]
    [InlineData("accidents.json", "\"Sprains\": [", "\"Orphan\": [ { \"target\": \"Chest\", \"type\": \"Blunt\", \"amount\": 0.1 } ],\n \"Sprains\": [", "accidents.json Orphan: no crossing draws from it")]
    [InlineData("loot.json", "\"AbandonedCamp\": [", "\"Orphan\": [ { \"resource\": \"Stone\", \"kg\": [0.1, 0.2] } ],\n \"AbandonedCamp\": [", "loot.json Orphan: no place is salvaged with it")]
    [InlineData("species.json", "\"temperaments\": {", "\"temperaments\": {\n  \"orphan\": { \"base\": 0, \"perMember\": 0, \"hungryAt\": 1.1, \"starvingAt\": 1.1, \"hungerBonus\": 0, \"defendBonus\": 0, \"cap\": 0 },", "temperament orphan: no species has it")]
    public void Loader_RefusesARowNothingUses(string file, string from, string to, string named) =>
        Assert.Contains(named, BrokenIn(file, from, to));

    /// NPCs are handed a stone axe and a flint knife by name: a rename fails at load, not at the first spawn.
    [Theory]
    [InlineData("stone_axe", "stone_hatchet")]
    [InlineData("flint_knife", "flint_blade")]
    public void Loader_RefusesARename_OfGearTheCodeNames(string kind, string renamed)
    {
        var files = Content.EmbeddedFiles();
        foreach (var file in Content.Files) files[file] = files[file].Replace($"\"{kind}\"", $"\"{renamed}\"", StringComparison.Ordinal);
        Assert.Contains($"no gear kind '{kind}', which the code names", Assert.Throws<InvalidDataException>(() => Content.Parse(files)).Message);
    }

    // ---- 5. nothing loaded can be changed, however deep ----

    /// Every collection reachable from the tables is a read-only view, and none is a List or an array
    /// that could be cast back and written to.
    internal static IEnumerable<string> WritableCollections(object? node, string path, HashSet<object> seen)
    {
        if (node == null || node is string or Enum || node.GetType().IsPrimitive) yield break;
        if (!node.GetType().IsValueType && !seen.Add(node)) yield break;
        var type = node.GetType();
        if (node is IDictionary d)
        {
            if (!d.IsReadOnly) yield return $"{path} is a writable {type.Name}";
            foreach (DictionaryEntry e in d) foreach (var p in WritableCollections(e.Value, $"{path}.{e.Key}", seen)) yield return p;
            yield break;
        }
        if (node is IList list)
        {
            if (!list.IsReadOnly || node is Array) yield return $"{path} is a writable {type.Name}";
            for (int i = 0; i < list.Count; i++) foreach (var p in WritableCollections(list[i], $"{path}[{i}]", seen)) yield return p;
            yield break;
        }
        if (node is IEnumerable) { yield return $"{path} is a {type.Name}, not a read-only list or dictionary"; yield break; }
        if (type.IsGenericType && type.FullName!.StartsWith("System.ValueTuple", StringComparison.Ordinal))
        {
            foreach (var f in type.GetFields()) foreach (var p in WritableCollections(f.GetValue(node), $"{path}.{f.Name}", seen)) yield return p;
            yield break;
        }
        if (type.Namespace != nameof(Survival)) yield break;
        foreach (var prop in type.GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance))
            if (prop.GetIndexParameters().Length == 0)
                foreach (var p in WritableCollections(prop.GetValue(node), $"{path}.{prop.Name}", seen)) yield return p;
    }

    [Fact]
    public void Content_InnerCollections_CannotBeCastBackAndWritten()
    {
        var found = WritableCollections(Content.Tables, "tables", new(ReferenceEqualityComparer.Instance)).ToList();
        found.AddRange(WritableCollections(new object[] { Content.Locations, Content.ById, Content.Loot, Content.PassStages, Content.CaveSites }.AsReadOnly(),
            "content", new(ReferenceEqualityComparer.Instance)));
        found.AddRange(Enum.GetValues<ButcherMode>().SelectMany(m => WritableCollections(Carcasses.ModeProps(m), $"ModeProps({m})", new(ReferenceEqualityComparer.Instance))));
        Assert.True(found.Count == 0, string.Join("\n", found.Take(40)));

        // The cases the review named, cast back the way a careless caller would.
        Assert.False(Content.Tables.Diets[Diet.Browser] is List<Resource>);
        Assert.False(Content.Tables.Forage["MixedForest"] is List<ForageItem>);
        Assert.False(Content.Tables.Accidents["Sprains"] is List<Accident>);
        Assert.False(Content.Tables.StartingClothes is List<KitGear>);
        Assert.False(Content.Tables.Sightings.Alert is List<double>);
        Assert.False(Content.Loot["AbandonedCamp"] is List<LootRoll>);
        var materials = Recipes.Get("log_frame").Materials;
        Assert.Throws<NotSupportedException>(() => ((IList)materials)[0] = materials[0]);
        Assert.Throws<NotSupportedException>(() => ((IDictionary<Resource, double>)Carcasses.ModeProps(ButcherMode.QuickStrip).YieldFactor).Clear());
    }
}
