namespace Survival.Tests;

/// Stone, flint, obsidian and bone tools (#145; spec 2026-11-24 recipes-that-make-sense, "Stone, flint, bone" and
/// "Stone and obsidian"): what each recipe takes and asks for, the stone as the hammer, the two axes, and where obsidian lies.
public class StoneToolsTests
{
    static readonly AgentId P = AgentId.Player;

    /// The bare seed-17 world at nine in the morning, the player at camp with an empty pack.
    static World Morning(out Inventory pack)
    {
        var w = Verification.ConservationWorlds.Bare(-2);
        pack = Items.Pack(w, P);
        TestKit.ClearResources(pack);
        pack.Gear.Clear();
        return w;
    }

    /// A binding is a sinew or 50 g of plant fibre; some rows bind with one, some with the other.
    static void Kit(Inventory pack, string recipe)
    {
        void Sinew() => Items.AddPiece(pack, Resource.Sinew, 0.1);
        void Fibre() => Items.Add(pack, Resource.PlantFiber, 0.05);
        switch (recipe)
        {
            case "crude_edge" or "stone": Items.AddPiece(pack, Resource.Stone, 0.3); break;
            case "flint_knife": Items.AddPiece(pack, Resource.Flint, 0.2); Items.AddPiece(pack, Resource.Stick, 0.3); Fibre(); break;
            case "obsidian_knife": Items.AddPiece(pack, Resource.Obsidian, 0.2); Items.AddPiece(pack, Resource.Stick, 0.3); Sinew(); break;
            case "bone_knife": Items.AddPiece(pack, Resource.Bone, 0.3); Items.AddPiece(pack, Resource.Stick, 0.3); Fibre(); break;
            case "ground_stone_axe": Items.AddPiece(pack, Resource.Stone, 0.8); Items.AddPiece(pack, Resource.BirchBranch, 1.5); Sinew(); break;
            case "flint_axe": Items.AddPiece(pack, Resource.Flint, 0.3); Items.AddPiece(pack, Resource.OakBranch, 1.5); Fibre(); break;
            case "bone_shovel": Items.AddPiece(pack, Resource.Bone, 0.8); Items.AddPiece(pack, Resource.PineBranch, 1.5); Sinew(); break;
            case "bone_needle": Items.AddPiece(pack, Resource.Bone, 0.1); break;
            default: throw new ArgumentException(recipe);
        }
    }

    /// The gear that gives each capability and nothing else the recipes ask for.
    static string ToolGiving(Capability cap) => cap switch
    {
        Capability.Striking => "stone",
        Capability.Cutting => "crude_edge",
        _ => throw new ArgumentException(cap.ToString()),
    };

    [Theory]
    [InlineData("crude_edge", "crude_edge", null, 5, new[] { Capability.Cutting })]
    [InlineData("stone", "stone", null, 1, new[] { Capability.Striking })]
    [InlineData("flint_knife", "flint_knife", Capability.Striking, 25, new[] { Capability.Cutting })]
    [InlineData("obsidian_knife", "obsidian_knife", Capability.Striking, 20, new[] { Capability.Cutting })]
    [InlineData("bone_knife", "bone_knife", Capability.Striking, 15, new[] { Capability.Cutting })]
    [InlineData("ground_stone_axe", "ground_stone_axe", Capability.Striking, 240, new Capability[0])]
    [InlineData("flint_axe", "flint_axe", Capability.Striking, 30, new[] { Capability.Cutting })]
    [InlineData("bone_shovel", "bone_shovel", Capability.Striking, 20, new[] { Capability.Digging })]
    [InlineData("bone_needle", "bone_needle", Capability.Cutting, 20, new[] { Capability.Sewing })]
    public void EachRecipe_IsMadeFromWhatItLists_AndRefusedWithoutItsTool(string recipe, string gear, Capability? tool, int minutes, Capability[] gives)
    {
        var w = Morning(out var pack);
        Kit(pack, recipe);
        if (tool is Capability cap)
        {
            Assert.Equal($"you need something for {Names.Of(cap)}", Sim.Refusal(w, new Make(P, recipe))?.Reason);
            pack.Gear.Add(Items.Make(ToolGiving(cap)));
        }
        var step = Sim.Step(w, new Make(P, recipe), Verbs.UntilDoneMinutes);
        Assert.Equal(Making.MadeIt, step.InterruptedReason);
        Assert.Equal(minutes, step.MinutesElapsed);
        var made = Assert.Single(pack.Gear, g => g.Kind == gear && g.MadeOf != null);
        Assert.Empty(Items.KgByResource(pack)); // every input went in, a piece or 50 g of each
        Assert.All(gives, c => Assert.Contains(c, Items.Def(made).Capabilities));
    }

    /// A crude edge and the stone need no tool; the edge is knapped by eye, the stone picked up in the dark.
    [Fact]
    public void TheCrudeEdge_NeedsLight_AndTheStone_DoesNot()
    {
        var w = Morning(out var pack);
        w.Minute = 14 * 60; // eleven at night, and no fire
        Items.AddPieces(pack, Resource.Stone, [0.3, 0.3]);
        Assert.Equal(Work.TooDark, Sim.Refusal(w, new Make(P, "crude_edge"))?.Reason);
        Assert.Null(Sim.Refusal(w, new Make(P, "stone")));
    }

    /// The stone is the hammer: it alone knaps a flint knife, and it frees what ice holds.
    [Fact]
    public void TheStone_GivesStriking_KnapsAFlintKnife_AndBreaksIce()
    {
        var w = Morning(out var pack);
        pack.Gear.Add(Items.Make("stone"));
        Assert.Equal(Capability.Striking, Capabilities.Of(pack));
        Assert.NotEqual(Capability.None, Capabilities.Of(pack) & Access.FreesIce);
        Kit(pack, "flint_knife");
        Sim.Step(w, new Make(P, "flint_knife"), Verbs.UntilDoneMinutes);
        Assert.Single(pack.Gear, g => g.Kind == "flint_knife");
    }

    /// About four hours of grinding, over as many sittings as it takes, makes the same axe.
    [Fact]
    public void AGroundStoneAxe_MadeInTwoSittings_IsTheSameAsOneMadeInOne()
    {
        static World Start()
        {
            var w = Morning(out var pack);
            pack.Gear.Add(Items.Make("stone"));
            Kit(pack, "ground_stone_axe");
            return w;
        }
        var whole = Start();
        Assert.Equal(Making.MadeIt, Sim.Step(whole, new Make(P, "ground_stone_axe"), Verbs.UntilDoneMinutes).InterruptedReason);

        var split = Start();
        Assert.Equal(100, Sim.Step(split, new Make(P, "ground_stone_axe"), 100).MinutesElapsed);
        Assert.Equal(100, Assert.Single(Items.Pack(split, P).Projects!).MinutesDone, 9);
        split = TestWorlds.Load(Save.ToJson(split));
        Assert.Equal(140, Sim.Step(split, new Make(P, "ground_stone_axe"), Verbs.UntilDoneMinutes).MinutesElapsed);

        Assert.Equal(whole.Minute, split.Minute);
        Assert.Equal(Save.ToJson(whole), Save.ToJson(split));
        Assert.Single(Items.Pack(split, P).Gear, g => g.Kind == "ground_stone_axe");
    }

    static World Grove(params string[] axes)
    {
        var w = TestWorlds.Bare(17);
        w.FixedWeather = new(0, WeatherCondition.Cloudy, 5, 0.1, 0, 0.8, WindDirection.North);
        w.PoolCache = new() { [Map.Of(w).Camp] = [new TreePool("Trees", Resource.BirchFirewood, 120)] };
        var pack = Items.Pack(w, P);
        foreach (var a in axes) pack.Gear.Add(Items.Make(a));
        pack.Gear.Add(Items.Make("large_bag"));
        pack.Gear.Add(Items.Make("large_bag"));
        return w;
    }

    /// The flint axe fells and cuts up a tree in less time than the ground stone axe, and loses more of its
    /// condition for each hour of chopping. Carrying both, the faster one does the work.
    [Fact]
    public void TheFlintAxe_FellsFaster_AndWearsMorePerHour()
    {
        static (int Minutes, double ConditionLostPerHour) Fell(params string[] axes)
        {
            var w = Grove(axes);
            var r = Sim.Step(w, new Gather(P, 0), 600);
            Assert.Equal("tree cut up", r.InterruptedReason);
            var used = Items.Pack(w, P).Gear.Where(g => Items.Def(g).Tool == ToolType.Axe).ToList();
            double lost = used.Sum(g => 1 - Items.ConditionPct(g));
            return (r.MinutesElapsed, lost / (r.MinutesElapsed / 60.0));
        }
        var stone = Fell("ground_stone_axe");
        var flint = Fell("flint_axe");
        Assert.Equal(120, stone.Minutes);
        Assert.True(flint.Minutes < stone.Minutes, $"flint {flint.Minutes} min, ground stone {stone.Minutes} min");
        Assert.True(flint.ConditionLostPerHour > stone.ConditionLostPerHour, $"flint {flint.ConditionLostPerHour:P1}/h, ground stone {stone.ConditionLostPerHour:P1}/h");

        var both = Fell("ground_stone_axe", "flint_axe");
        Assert.Equal(flint.Minutes, both.Minutes);
    }

    /// The forecast counts the faster axe: forty minutes bring the 120-minute tree down with a flint axe (at 32), not with
    /// the ground stone axe (at 48), and the flint axe's forty minutes do.
    [Fact]
    public void TheForecast_CountsTheFlintAxesSpeed()
    {
        var slow = Grove("ground_stone_axe");
        Assert.Empty(Work.ExpectedGather(slow, slow.Agent(P), new Gather(P, 0), 40));
        var w = Grove("flint_axe");
        var said = Work.ExpectedGather(w, w.Agent(P), new Gather(P, 0), 40);
        Assert.True(said.GetValueOrDefault(Resource.Stick) > 0, "the flint axe's tree is down");
        Assert.Contains(Sim.Step(w, new Gather(P, 0), 40).Events, e => e is TreeFelled);
    }

    // ---- content ----

    static readonly string[] Removed = ["sharp_rock", "hammerstone", "stone_knife", "stone_axe", "shale_knife"];

    [Fact]
    public void NoRemovedRecipeOrGearKind_Remains_AndNoLootTableNamesOne()
    {
        Assert.All(Removed, id => Assert.False(Recipes.Exists(id), $"recipe {id}"));
        Assert.All(Removed, id => Assert.False(Items.GearKinds.ContainsKey(id), $"gear {id}"));
        static IEnumerable<string> Gear(IEnumerable<LootRoll> rolls) => rolls.SelectMany(l => (l.Gear ?? []).Concat(Gear(l.Of ?? [])));
        Assert.Empty(Content.Loot.Values.SelectMany(Gear).Intersect(Removed));
        Assert.Empty(Content.Tables.Discoveries.Finds.Values.Concat(Content.Tables.Discoveries.Details.Values)
            .SelectMany(f => Flat(f.Items ?? [])).Select(i => i.Gear).OfType<string>().Intersect(Removed));
        Assert.DoesNotContain("Hammerstone", Enum.GetNames<ToolType>());
    }

    static IEnumerable<FindItem> Flat(IEnumerable<FindItem> items) => items.SelectMany(i => new[] { i }.Concat(Flat(i.Shuffle ?? [])));

    /// Every place a table names a resource, by where it is.
    static IEnumerable<(string Where, Resource Resource)> Sources()
    {
        foreach (var (table, items) in Content.Tables.Forage) foreach (var i in items) yield return ($"forage {table}", i.Resource);
        foreach (var l in Content.Locations)
        {
            foreach (var f in l.All.OfType<SiteForage>()) foreach (var i in f.Items) yield return (l.Id, i.Resource);
            foreach (var s in l.All.OfType<SiteStock>()) foreach (var i in s.Items ?? []) yield return (l.Id, i.Resource);
        }
        foreach (var (name, f) in Content.Tables.Discoveries.Finds.Concat(Content.Tables.Discoveries.Details))
            foreach (var i in Flat(f.Items ?? []))
                foreach (var r in (i.Resource is Resource one ? [one] : i.AnyOf ?? [])) yield return ($"find {name}", r);
        static IEnumerable<Resource> Rolled(IEnumerable<LootRoll> rolls) =>
            rolls.SelectMany(l => (l.Resource is Resource r ? [r] : Enumerable.Empty<Resource>()).Concat(Rolled(l.Of ?? [])));
        foreach (var (table, rolls) in Content.Loot) foreach (var r in Rolled(rolls)) yield return ($"loot {table}", r);
        foreach (var c in Clues.Table.Clues) foreach (var r in c.Resources ?? []) yield return ($"clue {c.Text}", r);
    }

    [Fact]
    public void NoTableNamesShale_AndObsidianLies_OnlyInTheCaveFindAndTheRareOutcrop()
    {
        Assert.DoesNotContain("Shale", Enum.GetNames<Resource>());
        var obsidian = Sources().Where(s => s.Resource == Resource.Obsidian).Select(s => s.Where).ToHashSet();
        Assert.Equal(new HashSet<string> { "find ObsidianSeam", "obsidian_outcrop" }, obsidian);
        Assert.Contains(Content.Tables.Discoveries.Cave, d => d.Find == "ObsidianSeam");
        // Rare: the glass shows at only some of the outcrops, so the valley's maps are drawn as before.
        var outcrop = Assert.Single(Content.Locations, l => l.Id == "obsidian_outcrop");
        var glass = Assert.Single(outcrop.All.OfType<SiteForage>(), f => f.Items.Any(i => i.Resource == Resource.Obsidian));
        Assert.True(glass.Chance <= 0.5, $"obsidian at {glass.Chance:P0} of the outcrops");
    }
}
