namespace Survival.Tests;

/// The content tables (rebuild/Sim/Content/*.json). Loading checks every reference; these pin what
/// was ported and that nothing in it depends on load order or chance.
public class ContentTests
{
    [Fact]
    public void AllContent_LoadsAndEveryReferenceResolves()
    {
        // Content's type initializer throws on any broken name or reference; touching it is the check.
        Assert.Equal(93, Content.Locations.Count);
        // Old GridWorldGenerator.GetUniqueLocationCount: 83 distinct site factories, plus 4 cave sites and 6 stages.
        Assert.Equal(83, Content.Locations.Count(l => l.Placement == Placement.Named));
        Assert.Equal(4, Content.CaveSites.Count);
        Assert.Equal(["Pass Approach", "Lower Pass", "The Pass Proper", "Upper Descent", "Lower Descent", "Far Side"],
            Content.PassStages.Select(s => s.Name));
        foreach (var l in Content.Locations)
            foreach (var s in l.All.OfType<SiteStock>().Where(s => s.Loot != null))
                Assert.True(Content.Loot.ContainsKey(s.Loot!), $"{l.Id}: no loot table {s.Loot}");
        foreach (var rolls in Content.Loot.Values)
            foreach (var g in rolls.SelectMany(Flatten).SelectMany(r => r.Gear ?? []))
                Assert.True(Items.GearKinds.ContainsKey(g), $"no gear kind {g}");
        Assert.Contains(Content.Locations, l => l.Id == "wolf_den");
        Assert.Contains(Content.Locations, l => l.Id == "bear_cave");
        Assert.Contains(Content.Locations, l => l.Id == "bone_hollow");

        // The other tables: every enum kind has its row, and every name one table gives another resolves.
        var t = Content.Tables;
        Assert.All(Enum.GetValues<Resource>(), r => Assert.True(t.Resources.ContainsKey(r), $"{r} has no resource row"));
        Assert.All(Enum.GetValues<EffectKind>(), k => Assert.True(t.Effects.ContainsKey(k), $"{k} has no effect row"));
        Assert.All(Enum.GetValues<Species>(), s => Assert.True(t.Species.ContainsKey(s), $"{s} has no species row"));
        Assert.All(Enum.GetValues<Diet>(), d => Assert.True(t.Diets.ContainsKey(d), $"{d} has no diet"));
        Assert.All(Enum.GetValues<ShelterKind>(), k => Assert.True(t.ShelterKinds.ContainsKey(k), $"{k} has no shelter kind row"));
        // Every shelter and bedding the old Build path made has a camp recipe placing it.
        var camp = t.MakeRecipes.Where(r => r.Result.Placed).ToList();
        Assert.Equal([ShelterKind.BranchFrame, ShelterKind.LogFrame, ShelterKind.SnowShelter, ShelterKind.HideTent], camp.Select(r => r.Result.Shelter).OfType<ShelterKind>().Order());
        Assert.Equal([BeddingKind.Makeshift, BeddingKind.Padded, BeddingKind.MossPadded, BeddingKind.SleepingBag], camp.Select(r => r.Result.Bedding).OfType<BeddingKind>().Order());
        Assert.All(Enum.GetValues<BeddingKind>(), b => Assert.True(t.Beddings.ContainsKey(b), $"{b} has no bedding row"));
        Assert.All(Enum.GetValues<ButcherMode>(), m => Assert.True(t.Butchering.ContainsKey(m), $"{m} has no butchering row"));
        Assert.All(Enum.GetValues<Terrain>(), x => Assert.True(t.Terrains.ContainsKey(x), $"{x} has no terrain row"));
        Assert.All(Enum.GetValues<EdgeType>(), e => Assert.True(t.Edges.ContainsKey(e), $"{e} has no edge row"));
        Assert.All(Enum.GetValues<PitType>(), p => Assert.True(t.Pits.ContainsKey(p), $"{p} has no pit row"));
        Assert.All(Enum.GetValues<EquipSlot>(), s => Assert.True(t.SlotCoverage.ContainsKey(s), $"{s} has no slot coverage"));
        Assert.True(t.Caves.ContainsKey(Cave.Entrance) && t.Caves.ContainsKey(Cave.Floor));
        // Gear named anywhere: the starting kit, tents, finds.
        foreach (var k in t.StartingPack.Gear.Concat(t.StartingClothes)) Assert.True(t.Gear.ContainsKey(k.Kind), $"kit: no gear kind {k.Kind}");
        foreach (var kind in t.Tents.Keys) Assert.Equal(ToolType.Tent, t.Gear[kind].Tool);
        Assert.All(t.Gear.Where(g => g.Value.Tool == ToolType.Tent), g => Assert.True(t.Tents.ContainsKey(g.Key), $"tent {g.Key} has no shelter values"));
        var finds = t.Discoveries.Finds.Values.Concat(t.Discoveries.Details.Values).ToList();
        foreach (var g in finds.SelectMany(f => FlattenItems(f.Items ?? [])).Select(i => i.Gear).OfType<string>())
            Assert.True(t.Gear.ContainsKey(g), $"finds: no gear kind {g}");
        // Resources a camp recipe builds with are shelter materials; temperaments and substrates resolve.
        foreach (var r in camp)
            foreach (var m in r.Materials)
                Assert.All(m.AnyOf, res => Assert.True(t.ShelterMaterials.ContainsKey(res), $"{r.Id}: {res} is no material"));
        foreach (var row in t.Terrains.Values)
        {
            Assert.True(t.Substrates.ContainsKey(row.Substrate), $"no substrate {row.Substrate}");
            if (row.Forage is { } f) Assert.True(t.Forage.ContainsKey(f.Table), $"no forage table {f.Table}");
        }
        foreach (var row in t.Caves.Values) Assert.True(t.Forage.ContainsKey(row.Forage.Table), $"no forage table {row.Forage.Table}");
        // Discovery draws name real finds; detail draws name real details or examine-only ones.
        foreach (var d in t.Discoveries.Terrains.Values.SelectMany(x => x).Concat(t.Discoveries.Cave))
            Assert.True(d.Find == null ? d.WaitsFor != null : t.Discoveries.Finds.ContainsKey(d.Find), $"no find {d.Find}");
        foreach (var id in t.Discoveries.DetailDraws.Values.SelectMany(x => x))
            Assert.True(t.Discoveries.Details.ContainsKey(id) || t.Discoveries.ExamineOnly.Contains(id), $"no detail {id}");
        // Effects named anywhere: accidents. Every pool the code draws is there.
        foreach (var a in t.Accidents.Values.SelectMany(x => x).Where(a => a.Effect != null))
            Assert.True(t.Effects.ContainsKey(a.Effect!.Value), $"no effect {a.Effect}");
        Assert.All(new[] { "TripStumble", "SharpHazards", "IceSlip", "RockyTerrain", "FallImpact", "Sprains" }, n => Assert.True(t.Accidents.ContainsKey(n), n));
    }

    static IEnumerable<FindItem> FlattenItems(IEnumerable<FindItem> items) => items.SelectMany(i => (IEnumerable<FindItem>)[i, .. FlattenItems(i.Shuffle ?? [])]);

    /// Loaded once, at first touch, and nothing loaded can be changed by a caller.
    [Fact]
    public void Content_LoadsOnce_AndIsReadOnly()
    {
        Assert.Same(Content.Tables, Content.Tables);
        Assert.Same(Content.Tables.Resources, Items.Resources);
        Assert.Same(Content.Tables.Fuels, Fire.Props);
        Assert.True(Content.LoadMilliseconds > 0);
        foreach (var table in new object[] { Items.Resources, Items.GearKinds, Fire.Props, Shelters.Kinds, Shelters.Beddings,
            Content.Tables.Effects, Content.Tables.Species, Content.Tables.Terrains, Content.Tables.Forage })
            Assert.True(((System.Collections.IDictionary)table).IsReadOnly, $"{table.GetType().Name} can be written");
        // And everything inside them, however deep: no list or dictionary in a table can be cast back and written to.
        var writable = ContentStrictTests.WritableCollections(Content.Tables, "tables", new(ReferenceEqualityComparer.Instance)).ToList();
        Assert.True(writable.Count == 0, string.Join("\n", writable.Take(40)));
    }

    /// A table keyed by an enum is built in the enum's order, whatever order the file lists it in.
    [Fact]
    public void Loader_EnumTables_DoNotDependOnFileOrder()
    {
        var files = Content.EmbeddedFiles();
        const string pine = "  \"PineBranch\": { \"peakTempF\": 800, \"burnRateKgPerHour\": 1.2, \"minFireTempF\": 150, \"ignitionBonusPct\": 0.05 },\n";
        // Pine moved from the middle of the fuels to the top.
        files["fire.json"] = Edit(Edit(files["fire.json"], pine, ""), "\"fuels\": {\n", "\"fuels\": {\n" + pine);
        var reordered = Content.Parse(files);
        Assert.NotEqual(Content.EmbeddedFiles()["fire.json"], files["fire.json"]);
        Assert.Equal(Fire.Props.Keys, reordered.Fuels.Keys);
        Assert.Equal(Fire.Props.Values, reordered.Fuels.Values);
    }

    static IEnumerable<LootRoll> Flatten(LootRoll r) => [r, .. (r.Of ?? []).SelectMany(Flatten)];

    [Fact]
    public void EveryLootTable_RollsDeterministically_AndOnlyRealThings()
    {
        foreach (var (name, table) in Content.Loot.OrderBy(k => k.Key, StringComparer.Ordinal))
            for (int i = 0; i < 50; i++)
            {
                var a = Pools.RollLoot(table, Rng.ForSystem(i, name));
                var b = Pools.RollLoot(table, Rng.ForSystem(i, name));
                Assert.Equal(a.Items, b.Items);
                Assert.Equal(a.Gear.Select(g => (g.Kind, g.Durability)), b.Gear.Select(g => (g.Kind, g.Durability)));
                Assert.All(a.Items, x => Assert.True(x.KgPerUnit > 0));
                Assert.All(a.Gear, g => Assert.True(g.Durability != 0, $"{name} gave a broken {g.Kind}"));
            }
    }

    /// old game: ScrapTool and the Rock Shelter's tool cache rolled 20-60% of an unlimited (-1)
    /// maximum, which came out 0: every tool found broken (bug, fixed: unlimited stays whole,
    /// limited ones keep at least one use).
    [Fact]
    public void ScrapTools_AreWornNotBroken()
    {
        for (int i = 0; i < 200; i++)
        {
            var g = Assert.Single(Pools.RollLoot(Content.Loot["ScrapTool"], Rng.ForSystem(i, "scrap")).Gear);
            var max = Items.GearKinds[g.Kind].MaxDurability;
            if (max <= 0) Assert.Equal(-1, g.Durability);
            else Assert.InRange(g.Durability, 1, (int)(max * 0.6));
        }
    }

    [Trait("Category", TestCategories.Slow)]
    [Fact]
    public void DeepWoods_HasItsBerryBushOnAboutThreeTilesInTen()
    {
        // Old MakeForest: Utils.DetermineSuccess(0.3) per site, from the process RNG, so a seed never
        // gave the same woods twice (bug, fixed: rolled per tile from the seed).
        int woods = 0, bushes = 0;
        for (int seed = 1; seed <= 40; seed++)
        {
            var m = Map.Generate(seed);
            foreach (var (t, site) in m.Sites.Where(s => s.Value.Id == "forest"))
            {
                woods++;
                var f = Map.SiteFeatures(seed, m, t);
                Assert.Equal(f, Map.SiteFeatures(seed, m, t));
                if (f.Any(x => x is SiteStock { Name: "Wild Berry Bush" })) bushes++;
            }
        }
        Assert.True(woods > 30, $"{woods} deep woods");
        Assert.InRange(bushes / (double)woods, 0.15, 0.45);
    }

    // ---- the loader is strict: a broken file fails loudly, naming what is wrong ----

    static readonly string Locations = Content.Embedded("locations.json"), Loot = Content.Embedded("loot.json");

    /// The real files with one edit, which must be there to make.
    static string Edit(string json, string from, string to)
    {
        int at = json.IndexOf(from, StringComparison.Ordinal);
        Assert.True(at >= 0, $"no '{from}' to edit");
        return json[..at] + to + json[(at + from.Length)..];
    }

    static string Broken(string locations, string? loot = null) =>
        Assert.Throws<InvalidDataException>(() => Content.Parse(locations, loot ?? Loot)).Message;

    [Fact]
    public void Loader_TheRealFilesParse()
    {
        var (locations, loot) = Content.Parse(Locations, Loot);
        Assert.Equal(Content.Locations.Count, locations.Count);
        Assert.Equal(Content.Loot.Count, loot.Count);
    }

    [Fact]
    public void Loader_RefusesAnUnknownField() =>
        Assert.Contains("colour", Broken(Edit(Locations, "\"name\": \"Deep Woods\",", "\"name\": \"Deep Woods\", \"colour\": \"green\",")));

    [Fact]
    public void Loader_RefusesAMissingRequiredField() =>
        Assert.Contains("name", Broken(Edit(Locations, "\"name\": \"Deep Woods\",", "")));

    [Fact]
    public void Loader_RefusesABadEnum() =>
        Assert.Contains("locations.json", Broken(Edit(Locations, "\"terrain\": [ \"Forest\" ]", "\"terrain\": [ \"Jungle\" ]")));

    [Fact]
    public void Loader_RefusesADuplicateKey()
    {
        Assert.Contains("hazard", Broken(Edit(Locations, "\"hazard\": 0.2,", "\"hazard\": 0.2, \"hazard\": 0.9,")));
        // A loot table named twice: the second would silently replace the first.
        int open = Loot.IndexOf('{');
        Assert.Contains("loot.json", Broken(Locations, Loot[..(open + 1)] + "\"MedicinalForage\": [], " + Loot[(open + 1)..]));
    }

    [Fact]
    public void Loader_RefusesANullElement()
    {
        Assert.Contains("location forest: features[0] is null", Broken(Edit(Locations, "\"features\": [", "\"features\": [ null,")));
        Assert.Contains("locations[0] is null", Broken(Edit(Locations, "\"locations\": [", "\"locations\": [ null,")));
    }

    [Fact]
    public void Loader_RefusesAShelterOutOfRange()
    {
        Assert.Contains("Dense Thicket: wind 0.9 is above its cap 0.7",
            Broken(Edit(Locations, "\"type\": \"DenseThicket\", \"insulation\": 0.15, \"overhead\": 0.5, \"wind\": 0.6", "\"type\": \"DenseThicket\", \"insulation\": 0.15, \"overhead\": 0.5, \"wind\": 0.9")));
        Assert.Contains("not in 0-1", Broken(Edit(Locations, "\"insulationCap\": 0.4, \"overheadCap\": 0.6", "\"insulationCap\": 1.4, \"overheadCap\": 0.6")));
    }

    /// A cap left out is the kind's, and the level is checked against that: a Dense Thicket with no
    /// caps of its own may not start above the kind's 0.7 wind.
    [Fact]
    public void Loader_ChecksALevel_AgainstTheKindsCap_WhenTheCapIsLeftOut()
    {
        const string thicket = "\"wind\": 0.6, \"insulationCap\": 0.4, \"overheadCap\": 0.6, \"windCap\": 0.7 }";
        Content.Parse(Edit(Locations, thicket, "\"wind\": 0.6 }"), Loot);
        Assert.Contains("Dense Thicket: wind 0.9 is above its cap 0.7", Broken(Edit(Locations, thicket, "\"wind\": 0.9 }")));
    }

    [Fact]
    public void Loader_RefusesADanglingReference()
    {
        Assert.Contains("known loot table (NoSuchTable)", Broken(Edit(Locations, "\"loot\": \"MedicinalForage\"", "\"loot\": \"NoSuchTable\"")));
        Assert.Contains("no gear kind 'no_such_gear'", Broken(Locations, Edit(Loot, "\"gear\": [", "\"gear\": [ \"no_such_gear\",")));
    }

    // ---- every other table is as strict: one edit to one file, and loading names what is wrong ----

    static string BrokenIn(string file, string from, string to)
    {
        var files = Content.EmbeddedFiles();
        files[file] = Edit(files[file], from, to);
        return Assert.Throws<InvalidDataException>(() => Content.Parse(files)).Message;
    }

    [Fact]
    public void Loader_AllTheRealFilesParse_ToTheTablesLoaded()
    {
        var t = Content.Parse(Content.EmbeddedFiles());
        Assert.Equal(Content.Tables.Resources, t.Resources);
        Assert.Equal(Content.Tables.Fuels, t.Fuels);
        Assert.Equal(Content.Tables.Species, t.Species);
        Assert.Equal(Content.Tables.Beddings, t.Beddings);
        Assert.Equal(Content.Tables.Gear.Keys, t.Gear.Keys);
        Assert.Equal(Content.Tables.Discoveries.Finds.Keys, t.Discoveries.Finds.Keys);
    }

    [Theory]
    [InlineData("items.json", "\"name\": \"Stone Axe\",", "\"name\": \"Stone Axe\", \"colour\": \"grey\",", "colour")]
    [InlineData("fire.json", "\"maxFuelKg\": 12,", "\"maxFuelKg\": 12, \"smoke\": 1,", "smoke")]
    [InlineData("species.json", "\"weightKg\": 45,", "\"weightKg\": 45, \"legs\": 4,", "legs")]
    [InlineData("effects.json", "\"Wet\": { \"severityPerHour\": 0,", "\"Wet\": { \"severityPerHour\": 0, \"itch\": 1,", "itch")]
    [InlineData("shelters.json", "\"name\": \"Sleeping area\",", "\"name\": \"Sleeping area\", \"soft\": true,", "soft")]
    [InlineData("butchering.json", "\"timeFactor\": 0.5,", "\"timeFactor\": 0.5, \"mess\": 1,", "mess")]
    [InlineData("terrain.json", "\"traversalMinutes\": 4, \"blocking\": false }", "\"traversalMinutes\": 4, \"blocking\": false, \"fish\": 1 }", "fish")]
    [InlineData("forage.json", "\"resource\": \"WillowBark\", \"abundance\": 0.25,", "\"resource\": \"WillowBark\", \"abundance\": 0.25, \"colour\": 1,", "colour")]
    [InlineData("discoveries.json", "\"name\": \"Wild Berry Bush\",", "\"name\": \"Wild Berry Bush\", \"thorns\": true,", "thorns")]
    [InlineData("accidents.json", "\"effect\": \"Dazed\", \"severity\": 0.2", "\"effect\": \"Dazed\", \"severity\": 0.2, \"ouch\": 1", "ouch")]
    public void Loader_RefusesAnUnknownField_InEveryFile(string file, string from, string to, string named) =>
        Assert.Contains(named, BrokenIn(file, from, to));

    [Theory]
    // Keys are enum names exactly as spelled: the JSON reader alone would take these.
    [InlineData("items.json", "\"Tinder\": { \"category\": \"Fuel\", \"form\": \"Continuous\" },", "\"tinder\": { \"category\": \"Fuel\", \"form\": \"Continuous\" },", "no Resource 'tinder'")]
    [InlineData("species.json", "\"Fox\": {", "\"Fox, Wolf\": {", "no Species 'Fox, Wolf'")]
    [InlineData("effects.json", "\"Wet\":", "\"Soggy\":", "no EffectKind 'Soggy'")]
    // A kind the code has with no row.
    [InlineData("fire.json", "  \"Mound\": { \"maxFuelKg\": 15, \"windProtectionLevel\": 0.4, \"fuelEfficiencyFactor\": 1.0 },\n", "", "fire.json pits: no row for Mound")]
    [InlineData("recipes.json", "\"shelter\": \"BranchFrame\"", "\"shelter\": \"BranchFrames\"", "no ShelterKind 'BranchFrames'")]
    [InlineData("effects.json", "\"Bloody\":", "\"Bloodied\":", "effects.json: no row for Bloody")]
    // A value that is no kind at all.
    [InlineData("accidents.json", "\"effect\": \"Dazed\", \"severity\": 0.2", "\"effect\": \"Concussed\", \"severity\": 0.2", "accidents.json")]
    [InlineData("forage.json", "\"resource\": \"WillowBark\"", "\"resource\": \"Willow\"", "forage.json")]
    public void Loader_RefusesABadEnumName_InEveryFile(string file, string from, string to, string named) =>
        Assert.Contains(named, BrokenIn(file, from, to));

    [Theory]
    [InlineData("items.json", "{ \"kind\": \"hand_drill\" }", "{ \"kind\": \"hand_axe\" }", "no gear kind 'hand_axe'")]
    [InlineData("species.json", "\"temperament\": \"wolf\"", "\"temperament\": \"wolves\"", "no temperament 'wolves'")]
    [InlineData("shelters.json", "\"mammoth_hide_tent\": { \"name\"", "\"no_tent\": { \"name\"", "no tent gear kind 'no_tent'")]
    [InlineData("terrain.json", "\"substrate\": \"peat\"", "\"substrate\": \"bog\"", "no substrate 'bog'")]
    [InlineData("terrain.json", "\"table\": \"Wetland\"", "\"table\": \"Swamp\"", "no forage table 'Swamp'")]
    [InlineData("forage.json", "\"MixedForest\": [", "\"MixedWoods\": [", "no table 'MixedForest', which the code names")]
    [InlineData("discoveries.json", "{ \"find\": \"IceSource\"", "{ \"find\": \"IceSauce\"", "no find 'IceSauce'")]
    [InlineData("discoveries.json", "\"gear\": \"wooden_spear\"", "\"gear\": \"iron_spear\"", "no gear kind 'iron_spear'")]
    [InlineData("discoveries.json", "\"Rock\": [ \"stone_pile\", \"stone_pile\"", "\"Rock\": [ \"stone_pile\", \"gold_nugget\"", "no detail 'gold_nugget'")]
    [InlineData("accidents.json", "\"Sprains\": [", "\"Strains\": [", "no pool 'Sprains', which the code names")]
    public void Loader_RefusesADanglingReference_InEveryFile(string file, string from, string to, string named) =>
        Assert.Contains(named, BrokenIn(file, from, to));

    [Theory]
    [InlineData("forage.json", "\"Rocky\": [", "\"Rocky\": [ null,", "forage.json.Rocky[0] is null")]
    [InlineData("items.json", "\"startingClothes\": [", "\"startingClothes\": [ null,", "items.json.startingClothes[0] is null")]
    [InlineData("discoveries.json", "\"Water\": [ \"frozen_puddle\"", "\"Water\": [ null, \"frozen_puddle\"", "discoveries.json.detailDraws.Water[0] is null")]
    [InlineData("effects.json", "\"Sweating\": { \"severityPerHour\": -2, \"treatmentAboveSeverity\": null }", "\"Sweating\": null", "effects.json.Sweating is null")]
    [InlineData("discoveries.json", "\"shuffle\": [", "\"shuffle\": [ null,", "discoveries.json.finds.MedicineBundle.items[1].shuffle[0] is null")]
    public void Loader_RefusesANullElement_InEveryFile(string file, string from, string to, string named) =>
        Assert.Contains(named, BrokenIn(file, from, to));

    [Theory]
    [InlineData("fire.json", "\"minFireTempF\": 500,", "\"minFireTempF\": 5000,", "is not below peakTempF")]
    [InlineData("items.json", "\"Head\": 0.1,", "\"Head\": 0.2,", "slotCoverage sums to")]
    [InlineData("species.json", "\"sprintSeconds\": 40, \"runCostJPerKgM\": 3.3", "\"sprintSeconds\": 0, \"runCostJPerKgM\": 3.3", "sprintSeconds and runCostJPerKgM above 0")]
    [InlineData("species.json", "\"topSpeedMps\": 14, \"enduranceSpeedMps\": 8", "\"topSpeedMps\": 7, \"enduranceSpeedMps\": 8", "a top speed above an endurance speed")]
    [InlineData("species.json", "\"autumnFatPct\": { \"min\": 0.15", "\"autumnFatPct\": { \"min\": 0.05", "a range from the build's fat up")]
    [InlineData("effects.json", "\"treatmentAboveSeverity\": 0, ", "\"treatmentAboveSeverity\": 1.5, ", "is not in 0-1")]
    [InlineData("shelters.json", "\"insulation\": 0.60, \"overhead\": 0.90, \"wind\": 0.80, \"insulationCap\": 0.60", "\"insulation\": 0.70, \"overhead\": 0.90, \"wind\": 0.80, \"insulationCap\": 0.60", "insulation 0.7 is above its cap 0.6")]
    [InlineData("butchering.json", "\"RawMeat\": 0.8", "\"RawMeat\": 1.5", "RawMeat above full processing's")]
    [InlineData("terrain.json", "\"visibility\": 1.5,", "\"visibility\": 2.5,", "visibility 2.5 is not in 0-2")]
    [InlineData("discoveries.json", "\"spawnChance\": 0.12, \"expectedHours\": 0.8", "\"spawnChance\": 1.2, \"expectedHours\": 0.8", "chance or hours")]
    [InlineData("discoveries.json", "\"resource\": \"Berries\", \"units\": 5, \"kgPerUnit\": 0.4,", "\"resource\": \"Berries\", \"units\": 5, \"kgPerUnit\": 0.4, \"kgPerUnitMax\": 0.5,", "a harvest's units each weigh kgPerUnit")]
    [InlineData("accidents.json", "\"amount\": 0.15 },", "\"amount\": -0.15 },", "amount -0.15")]
    public void Loader_RefusesAValueOutOfRange_InEveryFile(string file, string from, string to, string named) =>
        Assert.Contains(named, BrokenIn(file, from, to));
}
