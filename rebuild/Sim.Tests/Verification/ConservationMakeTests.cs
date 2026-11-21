using CsCheck;
using Survival;
using static Survival.Tests.Verification.ConservationWorlds;

namespace Survival.Tests.Verification;

/// Law 1 for making things: materials are neither made nor lost except as declared. Across any run of
/// starting, going on with, finishing and abandoning projects, the kilograms of each resource held -
/// as stacks, in unfinished projects, and in the recorded parts of every piece of gear - change by
/// exactly what the events say:
///
///   Δ held[r] = Made.Produced[r] - Made.Spent[r] - ProjectAbandoned.Lost[r]
///
/// so inputs in = result + waste + reclaimed. Pieces of gear, counted with those inside projects,
/// change only as finished work says: the result and any extra pieces made, the items consumed gone.
/// And no recipe makes more kilos than it spends.
public class ConservationMakeTests
{
    public enum OpKind { Make, Abandon, Wait }

    public sealed record Op(OpKind Kind, int Recipe, int Minutes);

    /// The pack's materials (kilos of each resource any recipe takes), the tools carried, a fire or not.
    public sealed record Scene(double[] PackKg, bool[] Tools, bool Fire, Op[] Ops);

    static readonly Resource[] Inputs = [.. Recipes.All.SelectMany(r => r.Materials).SelectMany(m => m.AnyOf).Distinct().Order()];
    static readonly string[] ToolKinds = ["sharp_rock", "flint_knife", "hammerstone", "bone_needle", "crude_spear", "wooden_spear"];

    static readonly Gen<Op> OpGen = Gen.Select(Gen.Frequency((6, Gen.Const(OpKind.Make)), (1, Gen.Const(OpKind.Abandon)), (1, Gen.Const(OpKind.Wait))),
        Gen.Int[0, Recipes.All.Count - 1], Gen.Int[1, 90], (k, r, m) => new Op(k, r, m));

    static readonly Gen<Scene> Scenes = Gen.Select(
        Gen.Frequency((1, Gen.Const(0.0)), (2, Gen.Double[0, 3])).Array[Inputs.Length],
        Gen.Bool.Array[ToolKinds.Length], Gen.Bool, OpGen.Array[1, 20],
        (kg, tools, fire, ops) => new Scene(kg, tools, fire, ops));

    static World Build(Scene s)
    {
        var w = Bare(-2);
        w.Minute = 3 * 60; // noon
        var pack = Items.Pack(w, P);
        pack.Kg.Clear();
        pack.Gear.Clear();
        for (int i = 0; i < Inputs.Length; i++) if (s.PackKg[i] > 0) Items.Add(pack, Inputs[i], s.PackKg[i]);
        for (int i = 0; i < ToolKinds.Length; i++)
            if (s.Tools[i])
            {
                var g = Items.Make(ToolKinds[i]);
                g.MadeOf = new() { [Resource.Pine] = 0.5 }; // parts recorded: they must travel into what it is made into
                pack.Gear.Add(g);
            }
        if (s.Fire)
        {
            var f = new FireState { Tile = Camp(w), LastUpdatedMinute = w.Minute, Pit = PitType.Open };
            Survival.Fire.AddFuel(f, Resource.BirchBark, 0.05);
            Survival.Fire.AddFuel(f, Resource.Kindling, 1.0);
            Survival.Fire.IgniteAll(f);
            Survival.Fire.AddFuel(f, Resource.Birch, 8);
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
        foreach (var ts in w.Tiles.Values) if (ts.Stash is { } stash) foreach (var g in InventoryGear(stash)) yield return g;
    }

    static IEnumerable<Gear> InventoryGear(Inventory inv) => inv.Gear.Concat(inv.Projects?.SelectMany(p => p.Materials.Gear) ?? []);

    /// Kilos held: stacks everywhere, materials in projects, and the recorded parts of all gear.
    static SortedDictionary<Resource, double> Held(World w)
    {
        var t = ItemKg(w);
        void Add(IEnumerable<KeyValuePair<Resource, double>> kgs) { foreach (var (r, kg) in kgs) t[r] = t.GetValueOrDefault(r) + kg; }
        foreach (var a in w.Agents.Values) if (Items.PackOf(a) is { } pack) foreach (var p in pack.Projects ?? []) Add(p.Materials.Kg);
        foreach (var ts in w.Tiles.Values) foreach (var p in ts.Stash?.Projects ?? []) Add(p.Materials.Kg);
        foreach (var g in AllGear(w)) Add(g.MadeOf ?? []);
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
            var recipe = Recipes.All[op.Recipe];
            Activity a = op.Kind switch
            {
                OpKind.Make => new Make(P, recipe.Id),
                OpKind.Abandon => new Abandon(P, recipe.Id),
                _ => new Wait(P),
            };
            var held = Held(w);
            var pieces = Pieces(w);
            var step = Sim.Step(w, a, op.Minutes);

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
                    case ProjectAbandoned gone: Move(gone.Lost, -1); break;
                }

            var after = Held(w);
            foreach (var r in declared.Keys.Union(after.Keys))
                Assert.True(Math.Abs(declared.GetValueOrDefault(r) - after.GetValueOrDefault(r)) < 1e-9,
                    $"{a} ({step.MinutesElapsed} min, {step.InterruptedReason}): {r} held {after.GetValueOrDefault(r)}, declared {declared.GetValueOrDefault(r)}");
            Assert.Equal(pieces.Where(kv => kv.Value != 0), Pieces(w));
        }
    }

    [Fact]
    public void Materials_AreNeitherMadeNorLost_ExceptAsDeclared() => Scenes.Check(Run, iter: 25);

    [Fact]
    [Trait("Category", TestCategories.Slow)]
    public void Materials_AreNeitherMadeNorLost_ExceptAsDeclared_Many() => Scenes.Check(Run, iter: 1000);
}
