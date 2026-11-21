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
}
