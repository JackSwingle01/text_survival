using CsCheck;
using Survival;
using static Survival.Tests.Verification.ConservationWorlds;

namespace Survival.Tests.Verification;

/// Law 1 for making things: materials are neither made nor lost except as declared. Across any run of
/// starting, going on with, finishing and abandoning projects, the kilograms of each resource held -
/// as stacks, in unfinished projects (carried or on a tile), and in the recorded parts of every piece
/// of gear - change by
/// exactly what the events say:
///
///   Δ held[r] = Made.Produced[r] - Made.Spent[r] - Built.Spent[r] - ProjectAbandoned.Lost[r]
///
/// so inputs in = result + waste + reclaimed. Pieces of gear, counted with those inside projects,
/// change only as finished work says: the result and any extra pieces made, the items consumed gone.
/// And no recipe makes more kilos than it spends. A station-worked recipe (Stations) turns the very piece put
/// on into its result, at the recipe's share of its weight:
///
///   Δ held[input] = -TransformationFinished.Kg / share, Δ held[made] = +TransformationFinished.Kg
///
/// and one with no input (melting snow) makes its result from nothing held: only Δ held[made] = +Kg.
public class ConservationMakeTests
{
    public enum OpKind { Make, Abandon, Wait, PutOn, TakeFinished, TakeBack, StopWork }

    public sealed record Op(OpKind Kind, int Recipe, int Minutes);

    /// The pack's materials (kilos of each resource any recipe takes), the tools carried, a fire or not.
    public sealed record Scene(double[] PackKg, bool[] Tools, bool Fire, Op[] Ops);

    static readonly Resource[] Inputs = [.. Recipes.All.SelectMany(r => r.Materials).SelectMany(m => m.AnyOf).Distinct().Order()];
    static readonly string[] ToolKinds = ["sharp_rock", "flint_knife", "hammerstone", "bone_needle", "crude_spear", "wooden_spear", "hide_gloves", "stone_knife"];

    static readonly Recipe[] AtTheFire = [.. Stations.FireRecipes];

    static readonly Gen<Op> OpGen = Gen.Select(Gen.Frequency((6, Gen.Const(OpKind.Make)), (1, Gen.Const(OpKind.Abandon)), (1, Gen.Const(OpKind.Wait)),
            (2, Gen.Const(OpKind.PutOn)), (1, Gen.Const(OpKind.TakeFinished)), (1, Gen.Const(OpKind.TakeBack)), (1, Gen.Const(OpKind.StopWork))),
        Gen.Int[0, Recipes.ByHand.Count - 1], Gen.Int[1, 90], (k, r, m) => new Op(k, r, m));

    static readonly Gen<Scene> Scenes = Gen.Select(
        Gen.Frequency((1, Gen.Const(0.0)), (2, Gen.Double[0, 3])).Array[Inputs.Length],
        Gen.Bool.Array[ToolKinds.Length], Gen.Bool, OpGen.Array[1, 20],
        (kg, tools, fire, ops) => new Scene(kg, tools, fire, ops));

    static World Build(Scene s)
    {
        var w = Bare(-2);
        w.Minute = 3 * 60; // noon
        var pack = Items.Pack(w, P);
        TestKit.ClearResources(pack);
        pack.Gear.Clear();
        for (int i = 0; i < Inputs.Length; i++) if (s.PackKg[i] > 0) TestKit.Give(pack, Inputs[i], s.PackKg[i]);
        for (int i = 0; i < ToolKinds.Length; i++)
            if (s.Tools[i])
            {
                var g = Items.Make(ToolKinds[i]);
                if (Recipes.All.Any(r => r.Result.Repair?.Kinds.Contains(g.Kind) == true)) g.Durability = (g.Durability + 1) / 2; // half worn: a whole one has nothing to repair
                g.MadeOf = new(); TestKit.Give(g.MadeOf, Resource.PineBranch, 1.5); // parts recorded: they must travel into what it is made into
                pack.Gear.Add(g);
            }
        if (s.Fire)
        {
            var f = new FireState { Tile = Camp(w), LastUpdatedMinute = w.Minute, Pit = PitType.Open };
            Survival.Fire.AddFuel(f, Resource.BirchBark, 0.05);
            Survival.Fire.AddFuel(f, Resource.Stick, 1.0);
            Survival.Fire.IgniteAll(f);
            Survival.Fire.AddFuel(f, Resource.BirchBranch, 8);
            w.Fires[new FireId(w.NextFireId++)] = f;
        }
        return w;
    }

    /// Every piece of gear anywhere, with the items inside unfinished projects.
    static IEnumerable<Gear> AllGear(World w)
    {
        foreach (var a in w.Agents.Values)
        {
            if (Items.PackOf(a) is { } pack) foreach (var g in InventoryGear(pack)) yield return g;
            foreach (var b in a.Members) foreach (var g in b.Worn) yield return g;
        }
        foreach (var ts in w.Tiles.Values) if (ts.Pile is { } pile) foreach (var g in InventoryGear(pile)) yield return g;
        foreach (var p in w.Projects.Values) foreach (var g in p.Materials.Gear) yield return g;
        foreach (var s in w.Shelters.Values) if (s.Tent is { } tent) yield return tent;
    }

    static IEnumerable<Gear> InventoryGear(Inventory inv) => inv.Gear.Concat(inv.Projects?.SelectMany(p => p.Materials.Gear) ?? []);

    /// Kilos held: stacks everywhere, materials in projects, and the recorded parts of all gear.
    static SortedDictionary<Resource, double> Held(World w)
    {
        var t = ItemKg(w);
        void Add(IEnumerable<KeyValuePair<Resource, double>> kgs) { foreach (var (r, kg) in kgs) t[r] = t.GetValueOrDefault(r) + kg; }
        foreach (var a in w.Agents.Values) if (Items.PackOf(a) is { } pack) foreach (var p in pack.Projects ?? []) Add(Items.KgByResource(p.Materials));
        foreach (var ts in w.Tiles.Values) foreach (var p in ts.Pile?.Projects ?? []) Add(Items.KgByResource(p.Materials));
        foreach (var p in w.Projects.Values) Add(Items.KgByResource(p.Materials));
        foreach (var g in AllGear(w)) if (g.MadeOf is { } made) Add(Items.KgByResource(made));
        return t;
    }

    static SortedDictionary<string, int> Pieces(World w)
    {
        var t = new SortedDictionary<string, int>();
        foreach (var g in AllGear(w)) t[g.Kind] = t.GetValueOrDefault(g.Kind) + 1;
        return t;
    }

    static void Run(Scene s)
    {
        var w = Build(s);
        foreach (var op in s.Ops)
        {
            if (!Sim.IsAlive(w.Agent(P))) break;
            var recipe = Recipes.ByHand[op.Recipe];
            // A repair works on a piece it takes: one under the work, else one held.
            var pack = Items.Pack(w, P);
            string? target = recipe.Result.Repair is { } repair
                ? pack.Projects?.FirstOrDefault(p => p.Recipe == recipe.Id)?.Target ?? pack.Gear.Select(g => g.Kind).FirstOrDefault(repair.Kinds.Contains)
                : null;
            Activity a = op.Kind switch
            {
                OpKind.Make => new Make(P, recipe.Id, target),
                OpKind.Abandon => new Abandon(P, recipe.Id, target),
                OpKind.PutOn => new PutOn(P, AtTheFire[op.Recipe % AtTheFire.Length].Id),
                OpKind.TakeFinished => new TakeFinished(P),
                OpKind.TakeBack => Stations.At(w, new FireId(0)).Working.ElementAtOrDefault(op.Minutes % 3) is { } x
                    ? new TakeBack(P, x.Input, x.InputKg) : new TakeBack(P, Resource.RawMeat, 0.4),
                OpKind.StopWork => new StopWork(P, "melt_snow"),
                _ => new Wait(P),
            };
            var held = Held(w);
            var pieces = Pieces(w);
            var step = Sim.Step(w, a, a is PutOn or TakeFinished or TakeBack or StopWork ? Stations.HandlingMinutes : op.Minutes);

            var declared = new SortedDictionary<Resource, double>(held);
            void Move(IEnumerable<KeyValuePair<Resource, double>> kgs, int sign) { foreach (var (r, kg) in kgs) declared[r] = declared.GetValueOrDefault(r) + sign * kg; }
            foreach (var e in step.Events)
                switch (e)
                {
                    case Made m:
                        {
                            Move(m.Produced, +1);
                            Move(m.Spent, -1);
                            Assert.True(m.Produced.Values.Sum() <= m.Spent.Values.Sum() + 1e-9, $"{m.Recipe} made more than it spent");
                            var r = Recipes.Get(m.Recipe);
                            if (m.Succeeded)
                            {
                                if (r.Result.Gear is string kind) pieces[kind] = pieces.GetValueOrDefault(kind) + 1;
                                foreach (var used in r.Items) pieces[used]--;
                            }
                            foreach (var extra in m.ExtraGear) pieces[extra] = pieces.GetValueOrDefault(extra) + 1;
                            break;
                        }
                    case Built b: Move(b.Spent, -1); break;
                    case TransformationFinished t:
                        {
                            var r = Recipes.Get(t.Recipe);
                            if (!Stations.FromNothing(r)) Move([new(Stations.InputOf(r), t.Kg / r.Result.SharePct)], -1); // snow melts from nothing held
                            Move([new(t.Made, t.Kg)], +1);
                            break;
                        }
                    case ProjectAbandoned gone: Move(gone.Lost, -1); break;
                }

            var after = Held(w);
            foreach (var r in declared.Keys.Union(after.Keys))
                Assert.True(Math.Abs(declared.GetValueOrDefault(r) - after.GetValueOrDefault(r)) < 1e-9,
                    $"{a} ({step.MinutesElapsed} min, {step.InterruptedReason}): {r} held {after.GetValueOrDefault(r)}, declared {declared.GetValueOrDefault(r)}");
            Assert.Equal(pieces.Where(kv => kv.Value != 0), Pieces(w));
            Assert.Null(BrokenPiece(w)); // a make, a failure or an abandon never splits a piece (#142)
        }
    }

    [Fact]
    public void Materials_AreNeitherMadeNorLost_ExceptAsDeclared() => Scenes.Check(Run, iter: 25);

    [Fact]
    [Trait("Category", TestCategories.Slow)]
    public void Materials_AreNeitherMadeNorLost_ExceptAsDeclared_Many() => Scenes.Check(Run, iter: 1000);
}
