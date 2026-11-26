namespace Survival.Tests;

/// An outcome may cost time, a piece, or a share of an amount, and never leaves a fraction of a piece (#289):
/// waste and saving on a discrete input name whole pieces, and a repair restores its share rounded up.
public class WholePieceOutcomeTests
{
    static readonly AgentId P = AgentId.Player;

    /// The recipe's project, holding exactly its inputs (the first alternative of each; every piece the lightest
    /// or the heaviest of its kind), completed as the outcome named. Null outcome: the plain result.
    static void Finish(World w, Recipe r, Outcome? outcome, bool heaviest)
    {
        var pack = Items.Pack(w, P);
        TestKit.ClearResources(pack);
        pack.Gear.Clear();
        pack.Projects = null;
        TestKit.ClearResources(Items.PileAt(w, w.Agent(P).Tile));
        string? target = r.Result.Repair?.Kinds[0];
        var p = new MakeProject { Recipe = r.Id, Target = target };
        if (target != null)
        {
            var piece = Items.Make(target);
            piece.Durability = piece.MaxDurability / 2;
            p.Materials.Gear.Add(piece);
        }
        foreach (var kind in r.Items) p.Materials.Gear.Add(Items.Make(kind));
        foreach (var m in r.Materials)
        {
            var res = m.AnyOf[0];
            if (!Items.IsDiscrete(res)) { Items.Add(p.Materials, res, m.Kg); continue; }
            var def = Items.Resources[res];
            for (int i = 0; i < m.Count; i++) Items.AddPiece(p.Materials, res, heaviest ? def.MaxPieceKg : def.MinPieceKg);
        }
        pack.Projects = [p];
        Making.Complete(w, w.Agent(P), r, outcome, [], target);
    }

    /// Pieces of r held in the pack and on the pile under it.
    static int Held(World w, Resource r) => Items.Count(Items.Pack(w, P), r) + Items.Count(Items.PileAt(w, w.Agent(P).Tile), r);

    static int Named<T>(Outcome? o, Resource r) where T : Effect =>
        (int)(o?.Effects.OfType<T>().Sum(e => e switch { WasteMaterial x when x.Input == r => x.Amount, SaveMaterial x when x.Input == r => x.Amount, _ => 0 }) ?? 0);

    [Fact]
    public void EveryOutcome_LeavesWholePieces_AndTakesThePiecesItNames()
    {
        var w = MakeTests.Noon(out _);
        var checkedPieces = 0;
        foreach (var r in Recipes.ByHand.Where(r => !r.Result.Placed))
            foreach (var o in r.Outcomes.Prepend(null))
                foreach (bool heaviest in new[] { false, true })
                {
                    Finish(w, r, o, heaviest);
                    var where = $"{r.Id} {o?.Text ?? "plain"} {(heaviest ? "heaviest" : "lightest")}";
                    foreach (var inv in new[] { Items.Pack(w, P), Items.PileAt(w, w.Agent(P).Tile) })
                        foreach (var (res, pieces) in inv.Pieces)
                            Assert.All(pieces, kg => Assert.InRange(kg, Items.Resources[res].MinPieceKg - 1e-9, Items.Resources[res].MaxPieceKg + 1e-9));
                    bool failed = o?.Kind == OutcomeKind.Fail;
                    foreach (var m in r.Materials.Where(m => Items.IsDiscrete(m.AnyOf[0])))
                    {
                        var res = m.AnyOf[0];
                        int wasted = Named<WasteMaterial>(o, res), saved = Named<SaveMaterial>(o, res);
                        int took = failed ? 0 : m.Count - wasted - saved;
                        // A result that is this very resource (none is) would be counted here too.
                        Assert.True(r.Result.Resource != res, where);
                        Assert.True(m.Count - wasted - took == Held(w, res), $"{where}: {res} {Held(w, res)} pieces left, not {m.Count - wasted - took}");
                        checkedPieces++;
                    }
                }
        Assert.True(checkedPieces > 100, $"only {checkedPieces} inputs checked");
    }

    /// The fourteen whose waste was once written as a share of whole pieces (#289): what failing does.
    [Theory]
    [InlineData("hide_belt", Resource.HideStrip, 1)]
    [InlineData("hide_gloves", Resource.HideStrip, 1)]
    [InlineData("hide_cap", Resource.HideStrip, 1)]
    [InlineData("hide_wrap", Resource.CuredHide, 1)]
    [InlineData("hide_leggings", Resource.CuredHide, 1)]
    [InlineData("mammoth_hide_tent", Resource.CuredMammothHide, 1)]
    [InlineData("large_bag", Resource.CuredHide, 1)]
    [InlineData("hide_tent", Resource.CuredHide, 1)]
    [InlineData("scrape_hide", Resource.Hide, 0)]
    [InlineData("scrape_mammoth_hide", Resource.MammothHide, 0)]
    [InlineData("hide_boots", Resource.CuredHide, 0)]
    [InlineData("mammoth_hide_pack", Resource.CuredMammothHide, 0)]
    [InlineData("mammoth_hide_coat", Resource.CuredMammothHide, 0)]
    [InlineData("mammoth_hood", Resource.CuredMammothHide, 0)]
    public void Failing_LosesWholePieces(string recipe, Resource input, int lost)
    {
        var w = MakeTests.Noon(out _);
        var r = Recipes.Get(recipe);
        var failure = r.Outcomes.Single(o => o.Kind == OutcomeKind.Fail && !o.Effects.Any(e => e is BreakTool));
        Finish(w, r, failure, heaviest: false);
        Assert.Equal(r.Materials.Single(m => m.AnyOf.Contains(input)).Count - lost, Held(w, input));
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(2, 1)]
    [InlineData(3, 2)]
    [InlineData(5, 3)]
    public void Sharpening_RestoresHalfTheMissingUses_RoundedUp(int missing, int restored)
    {
        Assert.Equal(0.5, Recipes.Get("sharpen").Result.Repair!.RestoresPct);
        var knife = Items.Make("obsidian_knife");
        knife.Durability = knife.MaxDurability - missing;
        Making.Restore(knife, 0.5);
        Assert.Equal(knife.MaxDurability - missing + restored, knife.Durability);
    }
}
