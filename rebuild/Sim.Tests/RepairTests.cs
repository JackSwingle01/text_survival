namespace Survival.Tests;

/// Repairs (#148): a recipe whose result is a repair works on one held piece of gear and restores a share of its
/// missing condition. The piece keeps its identity and its parts record.
public class RepairTests
{
    static readonly AgentId P = AgentId.Player;

    static World Noon(out Inventory pack)
    {
        var w = Verification.ConservationWorlds.Bare(-2);
        w.Minute = 3 * 60; // the clock starts at nine in the morning
        pack = Items.Pack(w, P);
        TestKit.ClearResources(pack);
        pack.Gear.Clear();
        return w;
    }

    static Gear Worn(string kind, int durability)
    {
        var g = Items.Make(kind);
        g.Durability = durability;
        return g;
    }

    static void SewingKit(Inventory pack)
    {
        TestKit.GivePieces(pack, Resource.HideStrip, 1);
        TestKit.GivePieces(pack, Resource.Sinew, 1);
        pack.Gear.Add(Items.Make("bone_needle"));
    }

    /// Begins the work, then completes it with the plain result.
    static void Repair(World w, string recipe, string target)
    {
        Assert.Null(Sim.Step(w, new Make(P, recipe, target), 1).InterruptedReason);
        Assert.Equal(Making.MadeIt, Making.Complete(w, w.Agent(P), Recipes.Get(recipe), null, [], target));
    }

    [Theory]
    [InlineData("hide_gloves", 40, 72)]       // 50% → 90%
    [InlineData("bear_hide_chest", 135, 147)] // 90% → 98%
    [InlineData("hide_cap", 72, 78)]          // 90% → 97.5%: 6.4 points, in whole ones
    public void Mending_RestoresFourFifthsOfTheMissingCondition(string kind, int before, int after)
    {
        var w = Noon(out var pack);
        SewingKit(pack);
        var piece = Worn(kind, before);
        pack.Gear.Add(piece);
        Repair(w, Recipes.All.Single(r => r.Result.Repair?.Kinds.Contains(kind) == true && r.Id.StartsWith("mend")).Id, kind);
        Assert.Equal(after, piece.Durability);
        Assert.Contains(piece, pack.Gear);
        Assert.Equal(0, Items.Count(pack, Resource.HideStrip));
        Assert.Equal(0, Items.Count(pack, Resource.Sinew));
    }

    [Fact]
    public void Sharpening_RestoresHalfTheMissingCondition_WithNoInputs()
    {
        var w = Noon(out var pack);
        var knife = Worn("stone_knife", 4); // 40%
        pack.Gear.Add(knife);
        Assert.Equal("you need something for striking", Sim.Refusal(w, new Make(P, "sharpen", "stone_knife"))?.Reason);
        pack.Gear.Add(Items.Make("hammerstone"));
        Repair(w, "sharpen", "stone_knife");
        Assert.Equal(7, knife.Durability); // 70%
    }

    [Fact]
    public void Mending_IsRefused_WithoutSewing_OrAHideStrip()
    {
        var w = Noon(out var pack);
        pack.Gear.Add(Worn("hide_boots", 40));
        TestKit.GivePieces(pack, Resource.Sinew, 1);
        Assert.Equal("you need 1 hide strip", Sim.Refusal(w, new Make(P, "mend", "hide_boots"))?.Reason);
        TestKit.GivePieces(pack, Resource.HideStrip, 1);
        Assert.Equal("you need something for sewing", Sim.Refusal(w, new Make(P, "mend", "hide_boots"))?.Reason);
        pack.Gear.Add(Items.Make("bone_needle"));
        Assert.Null(Sim.Refusal(w, new Make(P, "mend", "hide_boots")));
    }

    [Fact]
    public void Mending_TakesTheGarmentOff_AndPutsItBackOn()
    {
        var w = Noon(out var pack);
        SewingKit(pack);
        var body = w.Agent(P).Body;
        body.Worn.Clear();
        var wrap = Worn("hide_wrap", 40);
        body.Worn.Add(wrap);
        Assert.Null(Sim.Step(w, new Make(P, "mend_wrap", "hide_wrap"), 1).InterruptedReason);
        Assert.DoesNotContain(wrap, body.Worn);
        Assert.Equal(20, Recipes.Get("mend_wrap").Minutes);
        Assert.Equal(15, Recipes.Get("mend").Minutes);
        Assert.Equal(Making.MadeIt, Making.Complete(w, w.Agent(P), Recipes.Get("mend_wrap"), null, [], "hide_wrap"));
        Assert.Contains(wrap, body.Worn);
        Assert.Equal(72, wrap.Durability);
    }

    [Fact]
    public void TheMendedPiece_KeepsItsPartsRecord_AndTheStripAndSinewJoinIt()
    {
        var w = Noon(out var pack);
        SewingKit(pack);
        double strip = Items.Have(pack, Resource.HideStrip), sinew = Items.Have(pack, Resource.Sinew);
        var gloves = Worn("hide_gloves", 20);
        gloves.MadeOf = new();
        TestKit.GivePieces(gloves.MadeOf, Resource.HideStrip, 2);
        double madeOfStrips = Items.Have(gloves.MadeOf, Resource.HideStrip);
        pack.Gear.Add(gloves);
        Repair(w, "mend", "hide_gloves");
        Assert.Equal(3, Items.Count(gloves.MadeOf, Resource.HideStrip));
        Assert.Equal(madeOfStrips + strip, Items.Have(gloves.MadeOf, Resource.HideStrip), 9);
        Assert.Equal(sinew, Items.Have(gloves.MadeOf, Resource.Sinew), 9);
    }

    [Fact]
    public void Verbs_OfferARepair_ForEachHeldPieceThatCanTakeIt_AndNoOther()
    {
        var w = Noon(out var pack);
        SewingKit(pack);
        pack.Gear.Add(Items.Make("hammerstone"));
        pack.Gear.Add(Worn("hide_gloves", 40));
        pack.Gear.Add(Worn("stone_knife", 4));
        pack.Gear.Add(Items.Make("hide_cap")); // whole: nothing to mend
        var body = w.Agent(P).Body;
        body.Worn.Clear();
        body.Worn.Add(Worn("hide_boots", 10));
        var repairs = Verbs.At(w, P).Make.Where(v => v.Activity is Make { Target: not null }).ToList();
        Assert.Equal(["mend hide_boots", "mend hide_gloves", "sharpen stone_knife"],
            repairs.Select(v => (Make)v.Activity).Select(m => $"{m.Recipe} {m.Target}").Order());
        Assert.Contains(repairs, v => v.Label == "Mend hide gloves" && v.Refused == null);
        Assert.Contains(repairs, v => v.Label == "Sharpen stone knife" && v.Refused == null);
        // Sharpening a garment, or mending a knife, is not offered, and refused if asked for.
        Assert.NotNull(Sim.Refusal(w, new Make(P, "sharpen", "hide_gloves")));
        Assert.NotNull(Sim.Refusal(w, new Make(P, "mend", "stone_knife")));
        Assert.NotNull(Sim.Refusal(w, new Make(P, "mend")));
        Assert.NotNull(Sim.Refusal(w, new Make(P, "mend", "hide_cap"))); // whole
    }

    [Fact]
    public void Abandoning_GivesThePieceBack_AsItWas()
    {
        var w = Noon(out var pack);
        SewingKit(pack);
        var gloves = Worn("hide_gloves", 40);
        pack.Gear.Add(gloves);
        Assert.Null(Sim.Step(w, new Make(P, "mend", "hide_gloves"), 2).InterruptedReason);
        Assert.DoesNotContain(gloves, pack.Gear);
        var go = Verbs.At(w, P).Make.Single(v => v.Activity is Make { Target: "hide_gloves" });
        Assert.Equal("Go on mending hide gloves", go.Label);
        Sim.Step(w, new Abandon(P, "mend", "hide_gloves"), 1);
        Assert.Contains(gloves, pack.Gear);
        Assert.Equal(40, gloves.Durability);
    }
}
